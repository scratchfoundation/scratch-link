SIGN_ID="Code-to-Learn Foundation"
SWIFT=/usr/bin/env swift
BIN_PATH=$(shell $(SWIFT) build --configuration release --show-bin-path)

.PHONY: all clean

all: Release/scratch-link

$(BIN_PATH)/scratch-link:
	$(SWIFT) build --configuration release --static-swift-stdlib

Release/scratch-link: $(BIN_PATH)/scratch-link
	mkdir -p Release
	cp $(BIN_PATH)/scratch-link Release/scratch-link
	codesign --sign $(SIGN_ID) --entitlements Packaging/entitlements.plist Release/scratch-link

clean:
	rm -rf .build Release
