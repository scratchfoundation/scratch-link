SIGN_ID=Code-to-Learn\ Foundation
APP_ID=edu.mit.scratch.scratch-link
APP_BUNDLE=Scratch\ Link.app
APP_VERSION=1.0

BIN_PATH=$(shell swift build --configuration release --show-bin-path)

APP_DEST=Release/$(APP_ID)/Library/Scratch/$(APP_BUNDLE)
APP_VERSION_EXT=$(APP_VERSION).$(shell date -u "+%Y%m%d%H%M")

.PHONY: all clean distclean uninstall

all: Release/scratch-link.pkg

$(BIN_PATH)/scratch-link: $(wildcard Sources/scratch-link/*.swift)
	swift build --configuration release --static-swift-stdlib

$(APP_DEST): $(BIN_PATH)/scratch-link Packaging/entitlements.plist Packaging/Info.plist Release/Scratch\ Link.iconset Release/iconTemplate.iconset
	rm -rf "$@"
	mkdir -p "$@/Contents/MacOS"
	cp $(BIN_PATH)/scratch-link "$@/Contents/MacOS/"
	cp Packaging/Info.plist "$@/Contents/"
	plutil -replace "CFBundleVersion" -string "$(APP_VERSION_EXT)" "$@/Contents/Info.plist"
	plutil -replace "CFBundleShortVersionString" -string "$(APP_VERSION_EXT)" "$@/Contents/Info.plist"
	mkdir -p "$@/Contents/Resources"
	iconutil -c icns --output "$@/Contents/Resources/Scratch Link.icns" "Release/Scratch Link.iconset"
	iconutil -c icns --output "$@/Contents/Resources/iconTemplate.icns" "Release/iconTemplate.iconset"
	codesign --sign $(SIGN_ID) --identifier "$(APP_ID)" --deep --entitlements Packaging/entitlements.plist "$@"
	codesign --display -vv "$@"

# The following files should exist:
# icon_16x16.png, icon_32x32.png, icon_128x128.png, icon_256x256.png
# icon_16x16@2x.png, icon_32x32@2x.png, icon_128x128@2x.png, icon_256x256@2x.png
# The @2x files should be stored at 144 pixels per inch while the others should be stored at 72 pixels per inch.
Release/Scratch\ Link.iconset: ../Images/icon_1024x1024.png

# Status bar icon template
Release/iconTemplate.iconset: Images/iconTemplate.png

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

Release/$(APP_ID)/Library/LaunchAgents/$(APP_ID).plist: Packaging/launchd.plist
	mkdir -p "Release/$(APP_ID)/Library/LaunchAgents/"
	cp "$<" "$@"

Release/scratch-link.pkg: $(APP_DEST) Release/$(APP_ID)/Library/LaunchAgents/$(APP_ID).plist
	productbuild --identifier $(APP_ID) --product Packaging/requirements.plist --version $(APP_VERSION) --root Release/$(APP_ID) / --sign "Code-to-Learn Foundation" "$@"

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
