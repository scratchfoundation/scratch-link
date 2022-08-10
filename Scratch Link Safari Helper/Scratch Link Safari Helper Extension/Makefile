
# This Makefile generates the icons for the extension
# Unfortunately a web extension requires a different format of icon, so it can't share with the app

# These sizes are taken from the Xcode Safari extension template project
# The images in that template project don't specify their DPI
ICON_MAIN_SIZES=48 64 96 128 256 512
ICON_TOOL_SIZES=16 19 32 38 48 72

ICON_MAIN_FILES=$(foreach size,$(ICON_MAIN_SIZES),Resources/images/icon-$(size).png)
ICON_TOOL_FILES=$(foreach size,$(ICON_TOOL_SIZES),Resources/images/toolbar-icon-$(size).png)

.PHONY: all clean main toolbar

all: main toolbar

clean:
	rm -f $(ICON_MAIN_FILES) $(ICON_TOOL_FILES)

main: $(ICON_MAIN_FILES)

toolbar: $(ICON_TOOL_FILES)

define ICON_MAIN_RULE
Resources/images/icon-$(1).png: ../../Assets/Mac/raster/Master\ 1024x1024.png
	mkdir -p Resources/images/
	sips -z $(1) $(1) "$$<" --out "$$@"
	pngcrush -new -brute -ow "$$@"
endef

$(foreach size,$(ICON_MAIN_SIZES),$(eval $(call ICON_MAIN_RULE,$(size))))

define ICON_TOOL_RULE
Resources/images/toolbar-icon-$(1).png: ../../Assets/Mac/raster/Mac\ Glyph\ Icon\ 1024x1024.png
	mkdir -p Resources/images/
	sips -z $(1) $(1) "$$<" --out "$$@"
	pngcrush -new -brute -ow "$$@"
endef

$(foreach size,$(ICON_TOOL_SIZES),$(eval $(call ICON_TOOL_RULE,$(size))))
