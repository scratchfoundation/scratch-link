#!/usr/bin/env bash

usage () {
	echo "Usage: svg-convert input.svg output.png width height DPI"
	echo "Usage: svg-convert input.svg output.ico size1 [size2 [size3 [...]]]"
	echo "Convert an SVG into a PNG or ICO"
	echo "Assumes the input SVG is square and that pixel [1,1] is a good background color"
	echo "PNG conversion:"
	echo "  Pads the output horizontally, using the background color, to match the requested size"
	echo "  The resulting PNG is optimized using optipng"
	echo "ICO conversion:"
	echo "  Assumes each desired size is square"
	echo "  Sizes above 256 may not be supported"
	echo "  List sizes in decreasing order to make sure the largest suitable icon size is used in every context"
	echo "  Repeats PNG conversion once per desired size, then glues the results together into an ICO file"
	echo "  PNG optimizations are performed in parallel so progress reporting will likely be garbled"
}

if [ -z "$3" ]; then
	usage
	exit 1
fi

set -e

INPUT_SVG="$1"
shift 1

# DISPLAY="" prevents 'convert' from trying to contact an X server, which can slow it down quite a bit.
# This might cause problems if the SVG contains text but does not embed the necessary font(s).
echo "Detecting border color..."
BORDER_COLOR=$(DISPLAY="" convert -background none -format '%[pixel:u.p{1,1}]' "${INPUT_SVG}" info:)
echo "Detected border color: ${BORDER_COLOR}"

# Usage: svg2png output.png width height DPI
svg2png () {
	OUTPUT_PNG="$1"
	PNG_WIDTH="$2"
	PNG_HEIGHT="$3"
	PNG_DPI="$4"
	set -x
	cairosvg --output-height "${PNG_HEIGHT}" -f png -o - "${INPUT_SVG}" |
		DISPLAY="" convert -background "${BORDER_COLOR}" -gravity center -extent "${PNG_WIDTH}x${PNG_HEIGHT}" -density "${PNG_DPI}" - "${OUTPUT_PNG}"
	optipng -o7 -zm1-9 "$OUTPUT_PNG"
}

# Usage: svg2ico output.ico sizes...
svg2ico () {
	set -e
	OUTPUT_ICO="$1"
	shift
	SVG2ICO_TMP=$(mktemp -d -t svg2ico-XXXXXXXXXX)
	trap 'echo "Cleaning up..." >&2; rm -rv "${SVG2ICO_TMP}"; exit' EXIT HUP INT TERM
	ICO_IMAGES=()
	for SIZE in "$@"; do
		ICO_PNG="${SVG2ICO_TMP}/icon-${SIZE}.png"
		ICO_IMAGES+=("${ICO_PNG}")
		svg2png "${ICO_PNG}" "${SIZE}" "${SIZE}" 96 &
	done
	wait # for all PNGs to be created and optimized
	(set -x && DISPLAY="" convert "${ICO_IMAGES[@]}" "${OUTPUT_ICO}")
}

# $1 = output file
if [[ "${1}" =~ \.[Pp][Nn][Gg] ]]; then
	svg2png "$@"
elif [[ "${1}" =~ \.[Ii][Cc][Oo] ]]; then
	svg2ico "$@"
else
	echo "Unknown output file type: ${1}"
	usage
	exit 2
fi
