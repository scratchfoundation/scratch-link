#!/bin/bash
set -e

######

# Inputs: if you don't know how to get these files, ask @cwillisf or @colbygk
# - Open "Device Manager Cert & Key"
# - Save the certificate as "scratch-device-manager.cer"
# - Save the key as "scratch-device-manager.key"

# Output: various files in the "out" directory

# A few intermediate files are created in the "int" directory as well
# TODO: learn better openssl-fu and avoid the intermediate files (maybe)

######

mkdir -p out

# Code to split a PEM, in case a future version of the certificate goes back to PEM format:
#if [ "`uname`" == "Darwin" ]; then
#	split -p "-----BEGIN CERTIFICATE-----" dm.pem int/cert-
#	mv int/{cert-aa,scratch-device-manager.pem}
#	mv int/{cert-ab,int.pem}
#	mv int/{cert-ac,ca.pem}
#else
#	csplit -f int/cert- dm.pem '/-----BEGIN CERTIFICATE-----/' '{2}'
#	rm int/cert-00 # empty
#	mv int/{cert-01,scratch-device-manager.pem}
#	mv int/{cert-02,int.pem}
#	mv int/{cert-03,ca.pem}
#fi

# Windows wants a single PFX containing the certificate along with its private key
openssl pkcs12 \
	-inkey scratch-device-manager.key \
	-in scratch-device-manager.cer \
	-name "Scratch Link & Scratch Device Manager" \
	-passout pass:Scratch \
	-export -out out/scratch-device-manager.pfx

# Perfect on Mac wants a single PEM containing the certificate and key along with the whole CA chain
# Using grep this way enforces newlines between files
grep -h ^ {scratch-device-manager,intermediate,certificate-authority}.cer scratch-device-manager.key \
	| tr -d '\r' \
	> out/scratch-device-manager.pem

# Copy the PFX for the Windows build (the Mac Makefile "pulls" its certificates)
cp -v out/scratch-device-manager.pfx ../Windows/scratch-link/Resources/

ls -l out/
