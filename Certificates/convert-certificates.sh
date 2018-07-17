#!/bin/bash

######

# Inputs: if you don't know how to get these files, ask @cwillisf or @colbygk
# - dm.pem (renamed from "dm-pem.txt") from "Device Manager Cert Bundle"
# - scratch-device-manager.key from "Device Manager Key"

# Output: various files in the "out" directory

# A few intermediate files are created in the "int" directory as well
# TODO: learn better openssl-fu and avoid the intermediate files (maybe)

######

mkdir -p int out

# Extract each certificate from the PEM: ours, intermediate, CA
if [ "`uname`" == "Darwin" ]; then
	split -p "-----BEGIN CERTIFICATE-----" dm.pem int/cert-
	mv int/{cert-aa,scratch-device-manager.pem}
	mv int/{cert-ab,int.pem}
	mv int/{cert-ac,ca.pem}
else
	csplit -f int/cert- dm.pem '/-----BEGIN CERTIFICATE-----/' '{2}'
	rm int/cert-00 # empty
	mv int/{cert-01,scratch-device-manager.pem}
	mv int/{cert-02,int.pem}
	mv int/{cert-03,ca.pem}
fi

# Mac and Windows both want a single PFX containing the certificate along with its private key
openssl pkcs12 \
	-inkey scratch-device-manager.key \
	-in int/scratch-device-manager.pem \
	-name "Scratch Link & Scratch Device Manager" \
	-passout pass:Scratch \
	-export -out out/scratch-device-manager.pfx

# Mac wants a DER file for the CA & intermediates (just one intermediate in our case)
openssl x509 -in int/int.pem -outform der -out out/int.der
openssl x509 -in int/ca.pem -outform der -out out/ca.der

# Copy the PFX for the Windows build (the Mac Makefile "pulls" its certificates)
cp -v out/scratch-device-manager.pfx ../Windows/scratch-link/Resources/

ls -l out/
