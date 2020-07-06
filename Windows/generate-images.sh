#!/bin/sh

# Windows users: please run this script using Cygwin or WSL
# To do so, you may need to convert this file from Windows CRLF line endings to LF

# Generate app icon using ImageMagick
convert -verbose "../Assets/Windows/vector/Windows Menu 400x400.svg" -define icon:auto-resize "./scratch-link/Resources/ScratchLink.ico"
convert -verbose "../Assets/Windows/vector/Windows Tray 400x400.svg" -define icon:auto-resize "./scratch-link/Resources/NotifyIcon.ico"
