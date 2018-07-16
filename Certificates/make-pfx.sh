#!/bin/sh

# Windows likes a single PFX containing both the certificate and private key
# Inputs: scratch-device-manager.crt, scratch-device-manager.key
# Output: scratch-device-manager.pfx
openssl pkcs12 \
	-inkey scratch-device-manager.key \
	-in scratch-device-manager.crt \
	-name "Scratch Link & Scratch Device Manager" \
	-passout pass: \
	-export -out scratch-device-manager.pfx
mv -v scratch-device-manager.pfx ../Windows/scratch-link/Resources/
