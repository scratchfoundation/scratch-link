#!/bin/bash
set -e
set -x

######

# Inputs: certificate files in the "in" or "mock" directory.
# - If you're a member of the Scratch team and need the real certificates, ask cwillisf or colbygk.
# - Otherwise run "mock-certificates.sh" which will generate files in the "mock" directory.

# Output: various files in the "out" directory

# A few intermediate files are created in the "temp" directory as well
# TODO: learn better openssl-fu and avoid the intermediate files (maybe)

######

KEYFILE="device-manager.key"
CRTFILE="device-manager_scratch_mit_edu.crt"
CA_FILE="device-manager_scratch_mit_edu.ca-bundle"

# https://xkcd.com/221/
# see also roll.sh
IV="524919A0208051C68A443E4AA681D841"
KEY="FA5CF728AE0C2CB943151CD535B003E18EC29447833C9D51ED2D99217B9617B0"

# $1: input file
# $2: output file
function encryptFile () {
	# the '-p' causes OpenSSL to output the key & iv
	# the 'sed' command reformats them for easier use in scratch-link code
	openssl enc -nosalt -p -aes-256-cbc -K "$KEY" -iv "$IV" -in "$1" -out "$2" | \
		sed "s/\([0-9A-Fa-f][0-9A-Fa-f]\)/0x\1,/g"
}

mkdir -p temp out

if [ -r "in/${KEYFILE}" ]; then
	SDM_CERT_DIR="in"
	echo "Converting from real certificates"
else
	SDM_CERT_DIR="mock"
	echo "Converting from mock certificates"
fi

# Windows wants a single PFX containing the certificate along with its private key
openssl pkcs12 \
	-inkey "${SDM_CERT_DIR}/${KEYFILE}" \
	-in "${SDM_CERT_DIR}/${CRTFILE}" \
	-name "Scratch Link & Scratch Device Manager" \
	-passout pass:Scratch \
	-export -out temp/scratch-device-manager.pfx

encryptFile temp/scratch-device-manager.pfx out/scratch-device-manager.pfx.enc

# Perfect on Mac wants a single PEM containing the certificate and key along with the whole CA chain
# Using grep this way enforces newlines between files
grep -h ^ \
	"${SDM_CERT_DIR}/${CRTFILE}" \
	"${SDM_CERT_DIR}/${CA_FILE}" \
	"${SDM_CERT_DIR}/${KEYFILE}" \
	| tr -d '\r' \
	> temp/scratch-device-manager.pem

encryptFile temp/scratch-device-manager.pem out/scratch-device-manager.pem.enc

ls -l out/
