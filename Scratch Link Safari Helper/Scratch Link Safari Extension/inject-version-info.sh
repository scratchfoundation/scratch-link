#!/bin/bash

#  inject-version-info.sh
#  Scratch Link Safari Helper
#
#  Created by Christopher Willis-Ford on 9/7/22.
#  

# The variable SCRIPT_INPUT_FILE_0 should be the path to the manifest.json in the source tree
# The variable SCRIPT_OUTPUT_FILE_0 should be the path to which this script should write the modified manifest.json

# jq doesn't edit a file in-place, which could be a problem if both paths are the same
# modify it in two steps to cover that possibility (and it still works if they're different)

JQ_SCRIPT=".version = \"${CURRENT_PROJECT_VERSION}\"|.version_name = \"${MARKETING_VERSION}\""

# first, read the manifest file and store the modified contents in a variable
NEW_MANIFEST_CONTENTS="$(jq "${JQ_SCRIPT}" "${SCRIPT_INPUT_FILE_0}")"
echo "Injected version=${CURRENT_PROJECT_VERSION}, version_name=${MARKETING_VERSION} into ${SCRIPT_INPUT_FILE_0}"

# second, write the new contents to the manifest file
echo -E "${NEW_MANIFEST_CONTENTS}" > "${SCRIPT_OUTPUT_FILE_0}"
echo "Result saved as ${SCRIPT_OUTPUT_FILE_0}"
