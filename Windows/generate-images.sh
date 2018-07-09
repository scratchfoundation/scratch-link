#!/bin/sh

# Windows users: please run this script using Cygwin or WSL

# Generate app icon using ImageMagick
convert "../Images/icon_1024x1024.png" -define icon:auto-resize "./scratch-link/Resources/ScratchLink.ico"
