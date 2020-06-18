#!/bin/bash
set -e

function display_info () {
	echo "*****"
	echo "This script generates self-signed certificates for the purposes of testing Scratch Link."
	echo "These certificates may not work with a normal browser configuration and should not be used for release builds."
	echo "Please do not commit these self-signed files!"
	echo "*****"
}

# Usage: prep_openssl path/to/destination/directory
function prep_openssl () {
	mkdir -p "$1"/{certs,crl,newcerts,private}
	echo 1000 > "$1"/serial
	touch "$1"/{index.txt,index.txt.attr}
	cat > "$1"/openssl.conf <<-EOF
		[ ca ]
		default_ca = CA_default

		[ CA_default ]
		dir = '$1'
		certs = $1/certs
		crl_dir = $1/crl
		database = $1/index.txt
		new_certs_dir = $1/newcerts
		certificate = $1/certificate-authority.pem
		serial = $1/serial
		crl = $1/crl.pem
		private_key = $1/private/ca.key.pem
		RANDFILE = $1/.rnd
		nameopt = default_ca
		certopt = default_ca
		policy = policy_match
		default_days = 3650

		[ policy_match ]
		countryName = optional
		stateOrProvinceName = optional
		organizationName = optional
		organizationalUnitName = optional
		commonName = supplied
		emailAddress = optional

		[ req ]
		req_extensions = v3_req
		distinguished_name = req_distinguished_name

		[ req_distinguished_name ]

		[ v3_req ]

		[ req_ca ]
		subjectKeyIdentifier = hash
		keyUsage = critical, keyCertSign, cRLSign
		basicConstraints = critical, CA:TRUE

		[ req_int ]
		authorityKeyIdentifier = keyid
		subjectKeyIdentifier = hash
		keyUsage = critical, digitalSignature, keyCertSign, cRLSign
		basicConstraints = critical, CA:TRUE, pathlen:0
		extendedKeyUsage = serverAuth, clientAuth
		certificatePolicies = 2.5.29.32.0, 2.23.140.1.2.1

		[ req_cert ]
		authorityKeyIdentifier = keyid
		subjectKeyIdentifier = hash
		keyUsage = critical, digitalSignature, keyEncipherment
		basicConstraints = critical, CA:FALSE
		extendedKeyUsage = serverAuth, clientAuth
		certificatePolicies = 1.3.6.1.4.1.6449.1.2.2.7, 2.23.140.1.2.1
		subjectAltName = DNS:device-manager.scratch.mit.edu, DNS:www.device-manager.scratch.mit.edu
		EOF
}

function generate_all () {
	prep_openssl mock/ca
	openssl genrsa -out mock/ca/private/ca.key 4096
	openssl req -config mock/ca/openssl.conf -new -x509 -sha384 -extensions req_ca -subj '/CN=mock-ca' -key mock/ca/private/ca.key -out certificate-authority.cer

	prep_openssl mock/intermediate
	openssl genrsa -out mock/intermediate/private/intermediate.key 2048
	openssl req -config mock/intermediate/openssl.conf -new -sha384 -key mock/intermediate/private/intermediate.key -out mock/intermediate/certs/intermediate.csr -subj '/CN=mock-intermediate'
	openssl ca -batch -config mock/ca/openssl.conf -md sha384 -extensions req_int -notext -keyfile mock/ca/private/ca.key -cert certificate-authority.cer -in mock/intermediate/certs/intermediate.csr -out intermediate.cer

	mkdir -p mock/scratch-device-manager
	openssl req -new -keyout scratch-device-manager.key -newkey rsa:2048 -subj "/OU=Domain Control Validated/OU=PositiveSSL/CN=device-manager.scratch.mit.edu" -nodes -out mock/scratch-device-manager/scratch-device-manager.request
	openssl ca -batch -config mock/intermediate/openssl.conf -md sha256 -extensions req_cert -keyfile mock/intermediate/private/intermediate.key -cert intermediate.cer -out scratch-device-manager.cer -infiles mock/scratch-device-manager/scratch-device-manager.request
}

if [ -f scratch-device-manager.cer -o -f scratch-device-manager.key ]; then
	echo "Refusing to overwrite existing files"
else
	generate_all
	display_info
fi
