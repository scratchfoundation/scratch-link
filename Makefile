# This Makefile generates icon and tile images from the sources in `Assets/`.
# This doesn't need to be run every time: just when there's a significant change to the source assets or
# if we need different icons or tiles.

# I recommend running "make" with the "-j" parameter to parallelize these jobs.
# On my computer, a full run takes ~45 sec with "-j" or ~3.5 minutes without.

# Requirements:
# - cairosvg
# - convert (from ImageMagick)
# - optipng

MAC_IMAGES = \
	scratch-link-mac/Assets.xcassets/AppIcon.appiconset/AppIcon-16.png \
	scratch-link-mac/Assets.xcassets/AppIcon.appiconset/AppIcon-16@2x.png \
	scratch-link-mac/Assets.xcassets/AppIcon.appiconset/AppIcon-32.png \
	scratch-link-mac/Assets.xcassets/AppIcon.appiconset/AppIcon-32@2x.png \
	scratch-link-mac/Assets.xcassets/AppIcon.appiconset/AppIcon-128.png \
	scratch-link-mac/Assets.xcassets/AppIcon.appiconset/AppIcon-128@2x.png \
	scratch-link-mac/Assets.xcassets/AppIcon.appiconset/AppIcon-256.png \
	scratch-link-mac/Assets.xcassets/AppIcon.appiconset/AppIcon-256@2x.png \
	scratch-link-mac/Assets.xcassets/AppIcon.appiconset/AppIcon-512.png \
	scratch-link-mac/Assets.xcassets/AppIcon.appiconset/AppIcon-512@2x.png \
	scratch-link-mac/Assets.xcassets/StatusBarIcon.iconset/icon_16x16.png \
	scratch-link-mac/Assets.xcassets/StatusBarIcon.iconset/icon_16x16@2x.png \
	scratch-link-mac/Assets.xcassets/StatusBarIcon.iconset/icon_32x32.png \
	scratch-link-mac/Assets.xcassets/StatusBarIcon.iconset/icon_32x32@2x.png \
	scratch-link-mac/Assets.xcassets/StatusBarIcon.iconset/icon_128x128.png \
	scratch-link-mac/Assets.xcassets/StatusBarIcon.iconset/icon_128x128@2x.png \
	scratch-link-mac/Assets.xcassets/StatusBarIcon.iconset/icon_256x256.png \
	scratch-link-mac/Assets.xcassets/StatusBarIcon.iconset/icon_256x256@2x.png \
	scratch-link-mac/Assets.xcassets/StatusBarIcon.iconset/icon_512x512.png \
	scratch-link-mac/Assets.xcassets/StatusBarIcon.iconset/icon_512x512@2x.png \
	Scratch\ Link\ Safari\ Helper/Scratch\ Link\ Safari\ Extension/Resources/images/icon-48.png \
	Scratch\ Link\ Safari\ Helper/Scratch\ Link\ Safari\ Extension/Resources/images/icon-64.png \
	Scratch\ Link\ Safari\ Helper/Scratch\ Link\ Safari\ Extension/Resources/images/icon-96.png \
	Scratch\ Link\ Safari\ Helper/Scratch\ Link\ Safari\ Extension/Resources/images/icon-128.png \
	Scratch\ Link\ Safari\ Helper/Scratch\ Link\ Safari\ Extension/Resources/images/icon-256.png \
	Scratch\ Link\ Safari\ Helper/Scratch\ Link\ Safari\ Extension/Resources/images/icon-512.png \
	Scratch\ Link\ Safari\ Helper/Scratch\ Link\ Safari\ Extension/Resources/images/toolbar-icon-16.png \
	Scratch\ Link\ Safari\ Helper/Scratch\ Link\ Safari\ Extension/Resources/images/toolbar-icon-19.png \
	Scratch\ Link\ Safari\ Helper/Scratch\ Link\ Safari\ Extension/Resources/images/toolbar-icon-32.png \
	Scratch\ Link\ Safari\ Helper/Scratch\ Link\ Safari\ Extension/Resources/images/toolbar-icon-38.png \
	Scratch\ Link\ Safari\ Helper/Scratch\ Link\ Safari\ Extension/Resources/images/toolbar-icon-48.png \
	Scratch\ Link\ Safari\ Helper/Scratch\ Link\ Safari\ Extension/Resources/images/toolbar-icon-72.png

.PHONY: all clean mac

all: mac

clean:
	rm -vf $(MAC_IMAGES)

mac: $(MAC_IMAGES)

# Assumes the input SVG is square and that pixel [0,0] is a good background color
# Pads the output horizontally, using the background color, to match the requested size
# Usage: $(eval $(call svg2png,outpath/outfile.png,Assets/infile.svg,width,height,dpi))
define svg2png
$(1): $(2)
	BORDER_COLOR=`convert -background none -format '%[pixel:u.p{0,0}]' "$$<" info:` && \
	echo "Detected border color: $$$${BORDER_COLOR}" && \
	cairosvg --output-height "$(4)" -f png -o - "$$<" | \
		convert -background none -bordercolor "$$$${BORDER_COLOR}" -gravity center -extent "$(3)x$(4)" -density "$(5)" - "$$@" && \
	optipng -o7 -zm1-9 "$$@"
endef

# macOS app icon
$(eval $(call svg2png,scratch-link-mac/Assets.xcassets/AppIcon.appiconset/AppIcon-16.png,Assets/rounded.svg,16,16,72))
$(eval $(call svg2png,scratch-link-mac/Assets.xcassets/AppIcon.appiconset/AppIcon-16@2x.png,Assets/rounded.svg,32,32,144))
$(eval $(call svg2png,scratch-link-mac/Assets.xcassets/AppIcon.appiconset/AppIcon-32.png,Assets/rounded.svg,32,32,72))
$(eval $(call svg2png,scratch-link-mac/Assets.xcassets/AppIcon.appiconset/AppIcon-32@2x.png,Assets/rounded.svg,64,64,144))
$(eval $(call svg2png,scratch-link-mac/Assets.xcassets/AppIcon.appiconset/AppIcon-128.png,Assets/rounded.svg,128,128,72))
$(eval $(call svg2png,scratch-link-mac/Assets.xcassets/AppIcon.appiconset/AppIcon-128@2x.png,Assets/rounded.svg,256,256,144))
$(eval $(call svg2png,scratch-link-mac/Assets.xcassets/AppIcon.appiconset/AppIcon-256.png,Assets/rounded.svg,256,256,72))
$(eval $(call svg2png,scratch-link-mac/Assets.xcassets/AppIcon.appiconset/AppIcon-256@2x.png,Assets/rounded.svg,512,512,144))
$(eval $(call svg2png,scratch-link-mac/Assets.xcassets/AppIcon.appiconset/AppIcon-512.png,Assets/rounded.svg,512,512,72))
$(eval $(call svg2png,scratch-link-mac/Assets.xcassets/AppIcon.appiconset/AppIcon-512@2x.png,Assets/rounded.svg,1024,1024,144))

# macOS app status bar icon
$(eval $(call svg2png,scratch-link-mac/Assets.xcassets/StatusBarIcon.iconset/icon_16x16.png,Assets/glyph.svg,16,16,72))
$(eval $(call svg2png,scratch-link-mac/Assets.xcassets/StatusBarIcon.iconset/icon_16x16@2x.png,Assets/glyph.svg,32,32,144))
$(eval $(call svg2png,scratch-link-mac/Assets.xcassets/StatusBarIcon.iconset/icon_32x32.png,Assets/glyph.svg,32,32,72))
$(eval $(call svg2png,scratch-link-mac/Assets.xcassets/StatusBarIcon.iconset/icon_32x32@2x.png,Assets/glyph.svg,64,64,144))
$(eval $(call svg2png,scratch-link-mac/Assets.xcassets/StatusBarIcon.iconset/icon_128x128.png,Assets/glyph.svg,128,128,72))
$(eval $(call svg2png,scratch-link-mac/Assets.xcassets/StatusBarIcon.iconset/icon_128x128@2x.png,Assets/glyph.svg,256,256,144))
$(eval $(call svg2png,scratch-link-mac/Assets.xcassets/StatusBarIcon.iconset/icon_256x256.png,Assets/glyph.svg,256,256,72))
$(eval $(call svg2png,scratch-link-mac/Assets.xcassets/StatusBarIcon.iconset/icon_256x256@2x.png,Assets/glyph.svg,512,512,144))
$(eval $(call svg2png,scratch-link-mac/Assets.xcassets/StatusBarIcon.iconset/icon_512x512.png,Assets/glyph.svg,512,512,72))
$(eval $(call svg2png,scratch-link-mac/Assets.xcassets/StatusBarIcon.iconset/icon_512x512@2x.png,Assets/glyph.svg,1024,1024,144))

# macOS Safari extension icon
$(eval $(call svg2png,Scratch\ Link\ Safari\ Helper/Scratch\ Link\ Safari\ Extension/Resources/images/icon-48.png,Assets/rounded.svg,48,48,72))
$(eval $(call svg2png,Scratch\ Link\ Safari\ Helper/Scratch\ Link\ Safari\ Extension/Resources/images/icon-64.png,Assets/rounded.svg,64,64,72))
$(eval $(call svg2png,Scratch\ Link\ Safari\ Helper/Scratch\ Link\ Safari\ Extension/Resources/images/icon-96.png,Assets/rounded.svg,96,96,72))
$(eval $(call svg2png,Scratch\ Link\ Safari\ Helper/Scratch\ Link\ Safari\ Extension/Resources/images/icon-128.png,Assets/rounded.svg,128,128,72))
$(eval $(call svg2png,Scratch\ Link\ Safari\ Helper/Scratch\ Link\ Safari\ Extension/Resources/images/icon-256.png,Assets/rounded.svg,256,256,72))
$(eval $(call svg2png,Scratch\ Link\ Safari\ Helper/Scratch\ Link\ Safari\ Extension/Resources/images/icon-512.png,Assets/rounded.svg,512,512,72))

# macOS Safari extension toolbar icon
$(eval $(call svg2png,Scratch\ Link\ Safari\ Helper/Scratch\ Link\ Safari\ Extension/Resources/images/toolbar-icon-16.png,Assets/glyph.svg,16,16,72))
$(eval $(call svg2png,Scratch\ Link\ Safari\ Helper/Scratch\ Link\ Safari\ Extension/Resources/images/toolbar-icon-19.png,Assets/glyph.svg,19,19,72))
$(eval $(call svg2png,Scratch\ Link\ Safari\ Helper/Scratch\ Link\ Safari\ Extension/Resources/images/toolbar-icon-32.png,Assets/glyph.svg,32,32,72))
$(eval $(call svg2png,Scratch\ Link\ Safari\ Helper/Scratch\ Link\ Safari\ Extension/Resources/images/toolbar-icon-38.png,Assets/glyph.svg,38,38,72))
$(eval $(call svg2png,Scratch\ Link\ Safari\ Helper/Scratch\ Link\ Safari\ Extension/Resources/images/toolbar-icon-48.png,Assets/glyph.svg,48,48,72))
$(eval $(call svg2png,Scratch\ Link\ Safari\ Helper/Scratch\ Link\ Safari\ Extension/Resources/images/toolbar-icon-72.png,Assets/glyph.svg,72,72,72))
