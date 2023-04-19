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

WINDOWS_IMAGES = \
	scratch-link-win/scratch-link.ico \
	scratch-link-win/scratch-link-tray.ico \
	scratch-link-win-msix/Images/LockScreenLogo.scale-200.png \
	scratch-link-win-msix/Images/SplashScreen.scale-200.png \
	scratch-link-win-msix/Images/Square150x150Logo.scale-200.png \
	scratch-link-win-msix/Images/Square44x44Logo.scale-200.png \
	scratch-link-win-msix/Images/Square44x44Logo.targetsize-24_altform-unplated.png \
	scratch-link-win-msix/Images/StoreLogo.png \
	scratch-link-win-msix/Images/Wide310x150Logo.scale-200.png

.PHONY: all clean mac windows

all: mac windows

clean:
	rm -vf $(MAC_IMAGES) $(WINDOWS_IMAGES)

mac: $(MAC_IMAGES)

windows: $(WINDOWS_IMAGES)

# Assumes the input SVG is square and that pixel [0,0] is a good background color
# Pads the output horizontally, using the background color, to match the requested size
# Usage: $(eval $(call svg2png,outpath/outfile.png,Assets/infile.svg,width,height,dpi))
define svg2png
$(1): $(2)
	./svg-convert.sh "$$<" "$$@" "$(3)" "$(4)" "$(5)"
endef

# Usage: $(eval $(call svg2ico,outpath/outfile.ico,Assets/infile.svg,size1 size2...))
define svg2ico
$(1): $(2)
	./svg-convert.sh "$$<" "$$@" $(3)
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

# Windows app & tray icons
# See also:
#   https://stackoverflow.com/q/3236115
#   https://iconhandbook.co.uk/reference/chart/windows/
$(eval $(call svg2ico,scratch-link-win/scratch-link.ico,Assets/square.svg,256 128 96 64 48 32 24 16))
$(eval $(call svg2ico,scratch-link-win/scratch-link-tray.ico,Assets/simplified.svg,32 24 16))

# Windows MSIX
# TODO: does Microsoft really want DPI=72 for all of these?
# See https://learn.microsoft.com/en-us/windows/apps/design/layout/screen-sizes-and-breakpoints-for-responsive-design#effective-pixels-and-scale-factor
$(eval $(call svg2png,scratch-link-win-msix/Images/LockScreenLogo.scale-200.png,Assets/rounded.svg,48,48,72))
$(eval $(call svg2png,scratch-link-win-msix/Images/SplashScreen.scale-200.png,Assets/rounded.svg,1240,600,72))
$(eval $(call svg2png,scratch-link-win-msix/Images/Square44x44Logo.scale-200.png,Assets/rounded.svg,88,88,72))
$(eval $(call svg2png,scratch-link-win-msix/Images/Square44x44Logo.targetsize-24_altform-unplated.png,Assets/rounded.svg,24,24,72))
$(eval $(call svg2png,scratch-link-win-msix/Images/Square150x150Logo.scale-200.png,Assets/rounded.svg,300,300,72))
$(eval $(call svg2png,scratch-link-win-msix/Images/StoreLogo.png,Assets/rounded.svg,50,50,72))
$(eval $(call svg2png,scratch-link-win-msix/Images/Wide310x150Logo.scale-200.png,Assets/rounded.svg,620,300,72))
