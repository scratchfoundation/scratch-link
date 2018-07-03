# Scratch Link

Scratch Link is a helper application which allows Scratch 3.0 to communicate with hardware peripherals. Scratch Link
replaces the Scratch Device Manager and Scratch Device Plug-in.

System Requirements:

&nbsp; | Minimum | Recommended
--- | --- | ---
macOS | 10.10 "Yosemite" |
Windows&nbsp;10 | "Anniversary&nbsp;Update" Version&nbsp;1607 (build&nbsp;14393) | "Creators&nbsp;Update" Version&nbsp;1703 (build&nbsp;15063) or newer

## Using Scratch Link with Scratch

Scratch Link is a work in progress and is not yet ready for most users.

## Development: Getting started

### Documentation

The general network protocol and all supported hardware protocols are documented in Markdown files in the
`Documentation` subdirectory.

### macOS

The macOS version of this project is in the `macOS` subdirectory. It uses Swift 4.1 and the Swift Package Manager.

* Build the project with `swift build`
* Run the project with `swift run`
* Run project tests with `swift test`
* Create an Xcode project file with `swift package generate-xcodeproj`
  * If your workflow uses the Xcode project file (Xcode, AppCode, etc.) you should re-run this command each time you
    add or remove source files.
  * Any changes you make to the Xcode project file will be discarded when you run this command.

### Windows

The Windows version of this project is in the `Windows` subdirectory. It uses Visual Studio 2017 and targets Windows
10.0.15063.0 and higher.

* Ensure that the Windows 10.0.15063 SDK is installed
* Build, run, and debug by opening the Solution (`*.sln`) file in Visual Studio 2017
