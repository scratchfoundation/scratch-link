#!/usr/bin/env bash
set -e

function show_usage () {
	echo "OVERVIEW: Notarize a file then staple the results"
	echo ""
	echo "USAGE: $0 bundleID sourceToNotarize destinationToStaple tempDirectory"
	echo ""
	echo "ALL parameters are required:"
	echo "  bundleID: the 'primary bundle ID' for the upload (see altool docs)"
	echo "  sourceToNotarize: a zipped app bundle, a PKG, a DMG, etc., which will be uploaded to Apple's servers."
	echo "  destinationToStaple: an app bundle directory, a PKG, a DMG, etc., which will be stapled after notarization."
	echo "  tempDirectory: a directory in which to place temporary files, which could help with troubleshooting."
	echo ""
	echo "The environment variable AC_USERNAME must be set to an Apple ID which will be used for uploading to Apple."
	echo "If you set the environment variable AS_PASSWORD then that will be used; otherwise your keychain must include"
	echo "an item labeled 'Application Loader: \$AC_USERNAME'"
	false
}

# patterned after https://nativeconnect.app/blog/mac-app-notarization-from-the-command-line/
function do_notarize () {
	BUNDLE_ID="$1"
	SRC="$2"
	DST="$3"
	TMP_DIR="$4"

	PLIST_UPLOAD="${TMP_DIR}/notarize-upload.plist"
	PLIST_STATUS="${TMP_DIR}/notarize-status.plist"
	if [ "${AC_PASSWORD}" == "" ]; then
		AC_PASSWORD="@keychain:Application Loader: ${AC_USERNAME}"
	fi

	echo "Uploading ${SRC} for notarization..."
	time xcrun altool --notarize-app --primary-bundle-id "${BUNDLE_ID}" -u "${AC_USERNAME}" -p "${AC_PASSWORD}" -f "${SRC}" --output-format xml > "${PLIST_UPLOAD}"
	REQUEST_UUID=`/usr/libexec/PlistBuddy -c "Print :notarization-upload:RequestUUID" "${PLIST_UPLOAD}"`
	echo "Waiting for notarization task with UUID=${REQUEST_UUID}. This can take a while..."
	time while sleep 30s; do
		xcrun altool --notarization-info "${REQUEST_UUID}" -u "${AC_USERNAME}" -p "${AC_PASSWORD}" --output-format xml > "${PLIST_STATUS}"
		NOTARIZE_STATUS="`/usr/libexec/PlistBuddy -c "Print :notarization-info:Status" "${PLIST_STATUS}"`"
		echo "Notarization ${REQUEST_UUID} status is: ${NOTARIZE_STATUS}"
		if [ "${NOTARIZE_STATUS}" != "in progress" ]; then
			break
		fi
	done
	if [ "${NOTARIZE_STATUS}" != "success" ]; then
		return 1
	fi
	echo "Stapling ${DST}"
	xcrun stapler staple "${DST}"
}

if [ "$#" -lt 4 ]; then
	show_usage
elif [ "$AC_USERNAME" == "" ]; then
	echo "Cannot notarize unless AC_USERNAME is set"
	false
else
	do_notarize "$@"
fi
