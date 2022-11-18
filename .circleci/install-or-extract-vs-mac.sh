#!/bin/bash
set -e
CACHE_ARCHIVE="$1"
VS_MANIFEST="$HOME/Library/Caches/VisualStudioInstaller/downloads/InstallationManifest.json"

# This helps reduce log output from `tar -v` and similar without eliminating their output entirely.
# It converts a list of files into a list of directories, similar to `dirname`, then runs that list through `uniq`.
function reduce_log() {
    sed 's|\(.*/\).*|\1|g' | uniq
}

# This is the same as reduce_log, but specifically for the output of `cp -v`.
function reduce_log_cp() {
    sed 's|\(.*/\).* -> \(.*/\).*|\1 -> \2|g' | uniq
}

# Extract "Visual Studio for Mac" and its dependencies from an existing archive file.
function extract_vs() {
    echo "Cache found. Extracting..."
    sudo tar -x -C / -P -p --mac-metadata --totals -v -f "$CACHE_ARCHIVE" 2>&1 | reduce_log
}

# Download and install "Visual Studio for Mac" and its dependencies.
# Because VS for Mac doesn't have official automated or offline installers, this gets a bit complicated...
function install_vs() {
    echo "Cache not found. Installing..."

    VS_TMP="$(mktemp -d -t vs-installers)"

    echo "Installing Homebrew packages..."
    brew tap -v homebrew/cask-versions
    brew install -v visual-studio jq

    echo "Starting installer app..."
    for F in "/usr/local/Caskroom/visual-studio"/17.*/"Install Visual Studio for Mac.app"; do
        VS_INSTALLER="$F"
    done
    sudo xattr -d -r com.apple.quarantine "$VS_INSTALLER"
    open "$VS_INSTALLER"

    echo "Waiting for installer to download manifest..."
    while [ ! -f "$VS_MANIFEST" ]; do
      sleep 1
    done
    echo "Waiting a bit longer to be sure the installer is done writing the manifest..."
    sleep 10

    function install_pkg() {
      echo "Installing PKG: $1..."
      sudo installer -verbose -pkg "$1" -target /
    }

    function install_dmg() {
      ATTACH_LOG="$VS_TMP/attach.log"
      echo "Installing DMG: $1..."
      sudo hdiutil attach -noverify "$1" | tee "$ATTACH_LOG"
      grep "/Volumes/" "$ATTACH_LOG" | while read MOUNT_DEV MOUNT_FS MOUNT_PATH; do
        echo "  Looking to install from mount path: $MOUNT_PATH"
        # This is enough for Visual Studio but certainly not for the all DMGs.
        sudo cp -av "$MOUNT_PATH"/*.app /Applications/ | reduce_log_cp
      done
      grep "/Volumes/" "$ATTACH_LOG" | while read MOUNT_DEV MOUNT_FS MOUNT_PATH; do
        echo "  Attempting to detach: $MOUNT_PATH"
        sudo hdiutil detach "$MOUNT_PATH" || true
      done
    }

    function install_generic_name() {
      ITEM_URL="`jq -r ".items[] | select(.genericName == \\\"$1\\\").url" < "$VS_MANIFEST"`"
      if [ -z "$ITEM_URL" -o "$ITEM_URL" == "null" ]; then
        echo "No URL found. Skipping: $1"
        return
      fi
      ITEM_FILE="$VS_TMP/${ITEM_URL##*/}"
      echo "Downloading $ITEM_URL as $ITEM_FILE..."
      curl -C - -o "$ITEM_FILE" -L "$ITEM_URL"
      if [[ "$ITEM_FILE" == *.pkg ]]; then
        install_pkg "$ITEM_FILE"
      elif [[ "$ITEM_FILE" == *.dmg ]]; then
        install_dmg "$ITEM_FILE"
      else
        echo "Unknown file type: $ITEM_FILE"
        return 1
      fi
    }

    # To figure out these names:
    # 1. Start the "Visual Studio for Mac" installer app.
    # 2. Wait for it to fully load. It'll download the installation manifest automatically.
    # 3. Open the installation manifest (see $VS_MANIFEST above) and collect the `genericName` items of interest.
    # Make sure to collect the dependencies as well! This script doesn't currently respect the `dependsOn` field.
    #
    # Currently relevant dependencies:
    #  - DotNet6Sdk depends on DotNetCoreOld
    #  - macOS depends on DotNet6Sdk
    #  - Xamarin.Mac depends on XProfiler and MONO
    # Note that the "macOS" item doesn't currently install anything itself. It's here in case that changes in the future.
    # Also, "brew install visual-studio" installs the Mono MDK as a dependency, so we can skip "MONO" here.
    for NAME in DotNetCoreOld DotNet6Sdk XProfiler macOS XAMMAC VisualStudioMac; do
      install_generic_name "$NAME"
    done

    # Using "find" and "sort" forces the files to be in sorted order, which helps "reduce_log" do a better job.
    echo "Caching for next time..."
    sudo find \
        "/Applications/Visual Studio.app" \
        "/Applications/Xamarin Profiler.app" \
        "/Library/Frameworks/Mono.framework" \
        "/Library/Frameworks/Xamarin.Mac.framework" \
        "/usr/local/share/dotnet" \
        "/etc/paths.d/dotnet" \
        "/etc/paths.d/dotnet-cli-tools" \
        "/etc/paths.d/mono-commands" \
        -not -type d -print0 | sort -z | \
        sudo tar -c -a -C / -P -p --mac-metadata --totals -v -f "$CACHE_ARCHIVE" --null -T - \
        2>&1 | reduce_log
}

if [ -f "$CACHE_ARCHIVE" ]; then
  extract_vs
else
  install_vs
fi

echo "All done."
