#!/bin/bash

#  inject-version-info.sh
#  Scratch Link Safari Helper
#
#  Created by Christopher Willis-Ford on 9/7/22.
#  

MANIFEST_PATH="${CONFIGURATION_BUILD_DIR}/${UNLOCALIZED_RESOURCES_FOLDER_PATH}/manifest.json"

# jq doesn't edit a file in-place, so modify it in two steps:

# first, read the manifest file and store the modified contents in a variable
NEW_MANIFEST_CONTENTS="$(jq ".version = \"${MARKETING_VERSION}\"" "${MANIFEST_PATH}")"

# second, write the new contents to the manifest file
echo -E "${NEW_MANIFEST_CONTENTS}" > "${MANIFEST_PATH}"

echo "Injected version=${MARKETING_VERSION} into ${MANIFEST_PATH}"
