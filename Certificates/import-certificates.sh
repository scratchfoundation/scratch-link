#!/bin/bash
set -e

# This script is designed to run in a CI environment which sets certain environment variables.
# Under normal circumstances this should not be necessary for local development, even for Scratch team members.

function decodeToFile () {
	if [ -z "$1" ]; then
		echo "Missing or invalid file name"
		return 1
	fi
	if [ -z "$2" ]; then
		echo "Missing environment variable contents for file: $1"
		return 2
	fi
	echo "$2" | base64 -D -o "$1"
}

decodeToFile scratch-device-manager.cer "${SDM_CERT}"
decodeToFile certificate-authority.cer "${SDM_CERT_CA}"
decodeToFile intermediate.cer "${SDM_CERT_INT}"
decodeToFile scratch-device-manager.key "${SDM_CERT_KEY}"
