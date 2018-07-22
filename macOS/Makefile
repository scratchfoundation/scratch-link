SIGN_ID=Code-to-Learn\ Foundation
APP_ID=edu.mit.scratch.scratch-link
APP_BUNDLE=Scratch\ Link.app
APP_VERSION=1.0
CONFIG=release

BIN_PATH=$(shell swift build --configuration $(CONFIG) --show-bin-path)

# TODO: don't hard-code this. Probably `swift` or `swiftc` or `xcodebuild` or something knows...
SWIFT_LIB_PATH=/Library/Developer/CommandLineTools/usr/lib/swift/macosx

APP_DEST=Release/$(APP_BUNDLE)
VERSION_DATE=$(shell date -u -r `git log -1 --date=short --pretty=format:%ct` '+%Y%m%d%H%M')
APP_VERSION_EXT=$(APP_VERSION).$(VERSION_DATE)

.PHONY: all clean distclean uninstall xcodeproj

all: $(APP_DEST)

dist: $(APP_DEST)
	productbuild --sign "Code-to-Learn Foundation" --component "$<" /Applications \
	    "Release/scratch-link-`plutil -extract CFBundleVersion xml1 -o - Release/Scratch\ Link.app/Contents/Info.plist | xmllint -xpath '/plist/string/text()' -`.pkg"

$(BIN_PATH)/scratch-link: $(wildcard Sources/scratch-link/*.swift) Carthage
	swift build --configuration $(CONFIG) --no-static-swift-stdlib -Xswiftc -F -Xswiftc Carthage/Build/Mac/ -Xlinker -rpath -Xlinker '@executable_path/../Frameworks'

Carthage:
	carthage update --platform macOS && touch Carthage

xcodeproj:
	swift package generate-xcodeproj --xcconfig-overrides Carthage.xcconfig

$(APP_DEST): $(BIN_PATH)/scratch-link Packaging/entitlements.plist Packaging/Info.plist Release/Scratch\ Link.iconset Release/iconTemplate.iconset
	rm -rf "$@"
	mkdir -p "$@/Contents"
	cp -av "Carthage/Build/Mac/" "$@/Contents/Frameworks/"
	cp -av $(SWIFT_LIB_PATH)/libswift*.dylib "$@/Contents/Frameworks/"
	mkdir -p "$@/Contents/MacOS"
	cp $(BIN_PATH)/scratch-link "$@/Contents/MacOS/"
	cp Packaging/Info.plist "$@/Contents/"
	plutil -replace "CFBundleVersion" -string "$(APP_VERSION_EXT)" "$@/Contents/Info.plist"
	plutil -replace "CFBundleShortVersionString" -string "$(APP_VERSION_EXT)" "$@/Contents/Info.plist"
	mkdir -p "$@/Contents/Resources"
	cp -rv ../Certificates/out/* "$@/Contents/Resources/"
	iconutil -c icns --output "$@/Contents/Resources/Scratch Link.icns" "Release/Scratch Link.iconset"
	iconutil -c icns --output "$@/Contents/Resources/iconTemplate.icns" "Release/iconTemplate.iconset"
	codesign --sign $(SIGN_ID) --identifier "$(APP_ID)" --deep --entitlements Packaging/entitlements.plist "$@"
	codesign --display -vv "$@"

Release/Scratch\ Link.iconset: ../Images/icon_1024x1024.png

# Status bar icon template
Release/iconTemplate.iconset: Images/iconTemplate.png

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

clean:
	rm -rf Release "$(BIN_PATH)/scratch-link"

distclean: clean
	rm -rf .build

uninstall:
	@echo "These steps will probably fail unless run with sudo..."
	rm -rf /Library/Scratch/$(APP_BUNDLE)
	rmdir /Library/Scratch || true
	rm -f "/Library/LaunchAgents/$(APP_ID).plist"
	pkgutil --forget "$(APP_ID)"
