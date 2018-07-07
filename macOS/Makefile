SIGN_ID=Code-to-Learn\ Foundation
APP_ID=edu.mit.scratch.scratch-link
APP_BUNDLE=Scratch\ Link.app
APP_VERSION=1.0.0

BIN_PATH=$(shell swift build --configuration release --show-bin-path)

.PHONY: all clean distclean uninstall

all: Release/scratch-link.pkg

$(BIN_PATH)/scratch-link:
	swift build --configuration release --static-swift-stdlib

Release/$(APP_ID)/Library/Scratch/$(APP_BUNDLE): $(BIN_PATH)/scratch-link Packaging/entitlements.plist Packaging/Info.plist
	rm -rf "$@"
	mkdir -p "$@/Contents/MacOS"
	cp $(BIN_PATH)/scratch-link "$@/Contents/MacOS/"
	cp Packaging/Info.plist "$@/Contents/"
	plutil -replace "CFBundleVersion" -string "${APP_VERSION}" "$@/Contents/Info.plist"
	codesign --sign $(SIGN_ID) --identifier "$(APP_ID)" --deep --entitlements Packaging/entitlements.plist "$@"

Release/$(APP_ID)/Library/LaunchAgents/$(APP_ID).plist: Packaging/launchd.plist
	mkdir -p "Release/$(APP_ID)/Library/LaunchAgents/"
	cp "$<" "$@"

Release/scratch-link.pkg: Release/$(APP_ID)/Library/Scratch/$(APP_BUNDLE) Release/$(APP_ID)/Library/LaunchAgents/$(APP_ID).plist
	productbuild --version $(APP_VERSION) --root Release/$(APP_ID) / --sign "Code-to-Learn Foundation" "$@"

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
