#!/bin/sh

# Windows users: please run this script using Cygwin or WSL

# Generate app icon using ImageMagick
convert -verbose "../Assets/Windows/SVG/Windows Menu 400x400.svg" -define icon:auto-resize "./scratch-link/Resources/ScratchLink.ico"
convert -verbose "../Assets/Windows/SVG/Windows Tray 400x400.svg" -define icon:auto-resize "./scratch-link/Resources/NotifyIcon.ico"
