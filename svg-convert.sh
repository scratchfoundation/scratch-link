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

echo "Detecting border color..."
BORDER_COLOR=$(convert -background none -format '%[pixel:u.p{1,1}]' "${INPUT_SVG}" info:)
echo "Detected border color: ${BORDER_COLOR}"

# Usage: svg2png output.png width height DPI
svg2png () {
	OUTPUT_PNG="$1"
	PNG_WIDTH="$2"
	PNG_HEIGHT="$3"
	PNG_DPI="$4"
	set -x
	cairosvg --output-height "${PNG_HEIGHT}" -f png -o - "${INPUT_SVG}" |
		convert -background "${BORDER_COLOR}" -gravity center -extent "${PNG_WIDTH}x${PNG_HEIGHT}" -density "${PNG_DPI}" - "${OUTPUT_PNG}"
	optipng -o7 -zm1-9 "$OUTPUT_PNG"
}

# Usage: svg2ico output.ico sizes...
svg2ico () {
	OUTPUT_ICO="$1"
	shift
	SVG2ICO_TMP=$(mktemp -d -t svg2ico-XXXXXXXXXX)
	trap 'echo "Cleaning up..." >&2; rm -rv "${SVG2ICO_TMP}"; exit' ERR EXIT HUP INT TERM
	ICO_PNGS=()
	for SIZE in "$@"; do
		ICO_PNG="${SVG2ICO_TMP}/icon-${SIZE}.png"
		ICO_PNGS+=("${ICO_PNG}")
		svg2png "${ICO_PNG}" "${SIZE}" "${SIZE}" 96 &
	done
	wait # for all PNGs to be created and optimized
	(set -x && convert "${ICO_PNGS[@]}" "${OUTPUT_ICO}")
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
