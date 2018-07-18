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

### Certificates

**These steps are necessary regardless of platform.**

Scratch Link provides Secure WebSocket (WSS) communication and uses digital certificates to do so. These certificates
are **not** provided in this repository.

To prepare certificates for Scratch Link development:
* Obtain the raw certificates: see `Certificates/convert-certificates.sh` for details.
* `cd Certificates` and run `./convert-certificates.sh` to prepare the necessary certificate files.
  * On Windows, this script can be run using [Cygwin](https://www.cygwin.com/) or
    [WSL](https://docs.microsoft.com/en-us/windows/wsl/install-win10).

### macOS

The macOS version of this project is in the `macOS` subdirectory. It uses Swift 4.1, the Swift Package Manager, and
[Carthage](https://github.com/Carthage/Carthage#installing-carthage). Ensure that Carthage is installed before
attempting to build Scratch Link for macOS.

The build is primarily controlled through `make`:
* Build the app bundle with `make`, which will automatically:
  1. Run `Carthage` to download and build frameworks used by Scratch Link
  2. Compile Scratch Link code using `swift build`
  3. Create an app bundle at `Release/Scratch Link.app`
  4. Copy all necessary frameworks and dylibs into the app bundle
  5. Generate and/or copy other resources into the app bundle (certificates, icons, etc.)
  6. Sign the app bundle with a certificate from your keychain
* Prepare a PKG, ready to submit to the Mac App Store, with `make dist`
* Run the app in any of these ways:
  * Use Finder to activate the `Scratch Link` bundle in the `Release` directory
  * Run `"Release/Scratch Link.app/Contents/MacOS/scratch-link"`
    * Debug output **will** appear in the terminal
  * Type `open "Release/Scratch Link.app"` in a terminal
    * Debug output will **not** appear in the terminal
* Create an Xcode project file with `make xcodeproj`
  * If your workflow uses the Xcode project file (Xcode, AppCode, etc.) you should re-run this command each time you
    add or remove source files.
  * Any changes you make to the Xcode project file will be discarded when you run this command.
  * You may not be able to run the project using the Xcode project file, but completion and building should work.

### Windows

The Windows version of this project is in the `Windows` subdirectory. It uses Visual Studio 2017 and targets Windows
10.0.15063.0 and higher.

* Ensure that the Windows 10.0.15063 SDK is installed
* Build, run, and debug by opening the Solution (`*.sln`) file in Visual Studio 2017
