SIGN_ID=Code-to-Learn Foundation
APP_ID=edu.mit.scratch.scratch-link
APP_BUNDLE=Scratch\ Link.app
CONFIG=release

# Assume there's a recent tag called something like "1.1" and turn "1.1-2-g123abc" into "1.1.2"
APP_VERSION=$(shell git describe --always --long | sed 's/-\([0-9]*\)-g.*/.\1/')
VERSION_DETAIL=$(shell git describe --always --long --dirty --exclude '*')
BIN_PATH=$(shell swift build --configuration $(CONFIG) --show-bin-path)

BIN_FILE=$(BIN_PATH)/scratch-link
APP_DEST=dist/$(APP_BUNDLE)
PACKAGE_FILE="scratch-link-$(APP_VERSION).pkg"
ZIP_FILE="scratch-link-$(APP_VERSION).zip"
SWIFT_SOURCES=$(wildcard Sources/scratch-link/*.swift)

.PHONY: all bin clean dist dist-mas dist-devid distclean lint resolve-dependencies uninstall xcodeproj zip

all: $(APP_DEST)

show-version:
	@echo APP_VERSION:    $(APP_VERSION)
	@echo VERSION_DETAIL: $(VERSION_DETAIL)

lint: $(SWIFT_SOURCES)
	swiftlint lint --path Sources

bin: $(BIN_FILE)

dist: dist-mas dist-devid

# For distribution to the Mac App Store
dist-mas: dist/mas-$(PACKAGE_FILE)

dist/mas-$(PACKAGE_FILE): dist/tmp.mas/$(APP_BUNDLE)
	productbuild --sign "3rd Party Mac Developer Installer: $(SIGN_ID)" --component-compression auto --component "$<" /Applications "$@"

dist/tmp.mas/$(APP_BUNDLE): dist/$(APP_BUNDLE)
	rm -rf dist/tmp.mas/
	mkdir -p dist/tmp.mas/
	cp -aR "$<" "$@"
	xcrun swift-stdlib-tool --copy --sign "3rd Party Mac Developer Application: $(SIGN_ID)" --platform macosx --scan-folder "$@/Contents/MacOS" --destination "$@/Contents/Frameworks"
	test -r "$@/Contents/Frameworks/libswiftCore.dylib" # verify swift-stdlib-tool did its job
	codesign --sign "3rd Party Mac Developer Application: $(SIGN_ID)" --identifier "$(APP_ID)" --deep --entitlements Packaging/entitlements.plist "$@"
	find "$@" -type f -perm +111 -print0 | xargs -0 codesign --verify --verbose

# For "Developer ID" distribution outside the Mac App Store
dist-devid: dist/$(PACKAGE_FILE)

resolve-dependencies:
	swift package resolve

zip: dist/$(ZIP_FILE)

dist/$(PACKAGE_FILE): dist/tmp.devid/app/$(APP_BUNDLE)
	pkgbuild --analyze --root dist/tmp.devid/app/ dist/tmp.devid/components.plist
	plutil -replace "BundleIsRelocatable" -bool "NO" dist/tmp.devid/components.plist
	pkgbuild --root dist/tmp.devid/app/ --install-location /Applications/ --component-plist dist/tmp.devid/components.plist dist/tmp.devid/ScratchLink.pkg
	productbuild --synthesize --package dist/tmp.devid/ScratchLink.pkg dist/tmp.devid/distribution.plist
	# TODO: add a README and/or LICENSE to dist/tmp.devid/distribution.plist
	productbuild --sign "Developer ID Installer: $(SIGN_ID)" --component-compression auto --distribution dist/tmp.devid/distribution.plist --package-path dist/tmp.devid/ "$@"

dist/tmp.devid/app/$(APP_BUNDLE): dist/$(APP_BUNDLE)
	rm -rf dist/tmp.devid/app
	mkdir -p dist/tmp.devid/app
	cp -aR "$<" "$@"
	xcrun swift-stdlib-tool --copy --sign "Developer ID Application: $(SIGN_ID)" --platform macosx --scan-folder "$@/Contents/MacOS" --destination "$@/Contents/Frameworks"
	test -r "$@/Contents/Frameworks/libswiftCore.dylib" # verify swift-stdlib-tool did its job
	codesign --sign "Developer ID Application: $(SIGN_ID)" --identifier "$(APP_ID)" --deep --entitlements Packaging/entitlements.plist "$@"
	find "$@" -type f -perm +111 -print0 | xargs -0 codesign --verify --verbose

$(BIN_FILE): $(SWIFT_SOURCES)
	@echo "Build Version: $(APP_VERSION) $(VERSION_DETAIL)"
	swift build --configuration $(CONFIG) --no-static-swift-stdlib -Xlinker -rpath -Xlinker '@executable_path/../Frameworks'

xcodeproj:
	swift package generate-xcodeproj

dist/$(ZIP_FILE): dist/$(APP_BUNDLE)
	cd dist && zip -v9r $(ZIP_FILE) $(APP_BUNDLE)

$(APP_DEST): $(BIN_FILE) Packaging/entitlements.plist Packaging/Info.plist dist/Scratch\ Link.iconset dist/iconTemplate.iconset
	rm -rf "$@"
	mkdir -p "$@/Contents/MacOS"
	cp $(BIN_FILE) "$@/Contents/MacOS/"
	cp Packaging/Info.plist "$@/Contents/"
	plutil -replace "CFBundleVersion" -string "$(APP_VERSION)" "$@/Contents/Info.plist"
	plutil -replace "CFBundleShortVersionString" -string "$(APP_VERSION)" "$@/Contents/Info.plist"
	plutil -replace "ScratchVersionDetail" -string "$(VERSION_DETAIL)" "$@/Contents/Info.plist"
	mkdir -p "$@/Contents/Resources"
	cp -rv ../Certificates/out/scratch-device-manager.pem "$@/Contents/Resources/"
	iconutil -c icns --output "$@/Contents/Resources/Scratch Link.icns" "dist/Scratch Link.iconset"
	iconutil -c icns --output "$@/Contents/Resources/iconTemplate.icns" "dist/iconTemplate.iconset"

dist/Scratch\ Link.iconset: ../Assets/Mac/PNG/Master\ 1024x1024.png

# Status bar icon template
dist/iconTemplate.iconset: ../Assets/Mac/PNG/Mac\ Glyph\ Icon\ 1024x1024.png

# Generate a full icon set from an input image.
# The @2x are marked as 144 DPI while the others are marked as 72 DPI.
%.iconset:
	mkdir -p "$@"
	sips -s dpiWidth 72 -s dpiHeight 72 -z 16 16 "$<" --out "$@/icon_16x16.png"
	sips -s dpiWidth 72 -s dpiHeight 72 -z 32 32 "$<" --out "$@/icon_32x32.png"
	sips -s dpiWidth 72 -s dpiHeight 72 -z 128 128 "$<" --out "$@/icon_128x128.png"
	sips -s dpiWidth 72 -s dpiHeight 72 -z 256 256 "$<" --out "$@/icon_256x256.png"
	sips -s dpiWidth 72 -s dpiHeight 72 -z 512 512 "$<" --out "$@/icon_512x512.png"
	sips -s dpiWidth 144 -s dpiHeight 144 -z 32 32 "$<" --out "$@/icon_16x16@2x.png"
	sips -s dpiWidth 144 -s dpiHeight 144 -z 64 64 "$<" --out "$@/icon_32x32@2x.png"
	sips -s dpiWidth 144 -s dpiHeight 144 -z 256 256 "$<" --out "$@/icon_128x128@2x.png"
	sips -s dpiWidth 144 -s dpiHeight 144 -z 512 512 "$<" --out "$@/icon_256x256@2x.png"
	sips -s dpiWidth 144 -s dpiHeight 144 -z 1024 1024 "$<" --out "$@/icon_512x512@2x.png"
	for PNG in "$@"/icon_*.png; do pngcrush -new -brute -ow "$${PNG}"; done

clean:
	rm -rf dist/ .build/*-apple-*/debug .build/*-apple-*/release "$(BIN_FILE)"

distclean: clean
	rm -rf .build

uninstall:
	@echo "These steps will probably fail unless run with sudo..."
	rm -rf /Library/Scratch/$(APP_BUNDLE)
	rmdir /Library/Scratch || true
	rm -f "/Library/LaunchAgents/$(APP_ID).plist"
	pkgutil --forget "$(APP_ID)"
