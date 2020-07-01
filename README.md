# Scratch Link

Scratch Link is a helper application which allows Scratch 3.0 to communicate with hardware peripherals. Scratch Link
replaces the Scratch Device Manager and Scratch Device Plug-in.

System Requirements:

| | Minimum
| --- | ---
| macOS | 10.10 "Yosemite"
| Windows&nbsp;10 | Version&nbsp;1709 (build&nbsp;16299) "Fall&nbsp;Creators&nbsp;Update" or newer

## Using Scratch Link with Scratch 3.0

To use Scratch Link with Scratch 3.0:

1. Install and run Scratch Link
2. Open [Scratch 3.0](https://scratch.mit.edu)
3. Select the "Add Extension" button (looks like Scratch blocks with a `+` at the bottom of the block categories list)
4. Select a compatible extension such as the micro:bit or LEGO EV3 extension.
5. Follow the prompts to connect your peripheral.
6. Build a project with the new extension blocks. Scratch Link will help Scratch communicate with your peripheral.

## Development: Getting started

### Documentation

The general network protocol and all supported hardware protocols are documented in Markdown files in the
`Documentation` subdirectory. Please note that network protocol stability and compatibility are high priorities for
this project. Changes to the protocol are unlikely to be accepted without very strong justification combined with
thorough documentation.

Please use [markdownlint](https://www.npmjs.com/package/markdownlint) to check documentation changes before submitting
a pull request.

### Certificates

**These steps are necessary regardless of platform.**

Scratch Link provides Secure WebSocket (WSS) communication and uses digital certificates to do so. These certificates
are **not** provided in this repository.

To prepare certificates for Scratch Link development, run the following commands. These commands should be run from a
`bash` prompt (or `zsh`, etc.), which on Windows means using something like [Cygwin](https://www.cygwin.com/) or
[WSL](https://docs.microsoft.com/en-us/windows/wsl/install-win10).

1. `cd Certificates`
2. Run `./mock-certificates.sh` to generate self-signed certificates.
3. Run `./convert-certificates.sh` to prepare the certificates for use by Scratch Link.

If you are a member of the Scratch team and need the real certificates,
see `Certificates/convert-certificates.sh` for details.

### macOS

The macOS version of this project is in the `macOS` subdirectory. It uses Swift 5.2 and the Swift Package Manager.

Developer prerequisites on macOS, most of which are available through [Homebrew](https://brew.sh/):

* Xcode Command Line Tools
  * Install with `xcode-select --install`
* [git](https://git-scm.com/)
* [pngcrush](https://pmt.sourceforge.io/pngcrush/)
* [swiftlint](https://github.com/realm/SwiftLint) (optional)
* Swift Version Manager [swiftenv](https://swiftenv.fuller.li/) (optional)

The build is primarily controlled through `make`:

* Build the app bundle with `make`, which will automatically:
  1. Compile Scratch Link code using `swift build`
  2. Create an app bundle at `dist/Scratch Link.app`
  3. Copy all necessary frameworks and dylibs into the app bundle
  4. Generate and/or copy other resources into the app bundle (certificates, icons, etc.)
* Build PKG installers with `make dist`, which runs both of these:
  * Build a PKG for the Mac App Store with `make dist-mas`
  * Build a PKG for non-Store distribution ("Developer ID") with `make dist-devid`
* Run the app in any of these ways:
  * Use Finder to activate the `Scratch Link` bundle in the `dist` directory
  * Run `"dist/Scratch Link.app/Contents/MacOS/scratch-link"`
    * Debug output **will** appear in the terminal
  * Type `open "dist/Scratch Link.app"` in a terminal
    * Debug output will **not** appear in the terminal
* Create an Xcode project file with `make xcodeproj`
  * If your workflow uses the Xcode project file (Xcode, AppCode, etc.) you should re-run this command each time you
    add or remove source files.
  * Any changes you make to the Xcode project file will be discarded when you run this command.
  * You may not be able to run the project using the Xcode project file, but completion and building should work.

### Windows

The Windows version of this project is in the `Windows` subdirectory.

Prerequisites:

* [Visual Studio 2017 or newer](https://visualstudio.microsoft.com/vs/) (Community Edition is sufficient)
* Windows 10.0.16299 SDK (install with Visual Studio)
* Some of the Scratch Link project files depend on NuGet packages. Visual Studio should prompt you to install these
  packages when you open the Solution file. Without these packages, Scratch Link may fail to build or run.

Optional:

* [MSBuildStructuredLog](https://github.com/KirillOsenkov/MSBuildStructuredLog) is a huge help for debugging MSBuild.

Build, run, and debug by opening the Solution (`*.sln`) file in Visual Studio.

#### Signing the MSI installer

*This section applies to Scratch Team members only.*

To build and sign the `ScratchLinkSetup` installer (MSI), you must install the appropriate signing certificate.
Contact another Scratch Team member to obtain the certificate, then install it with these steps:

1. Open "Manage User Certificates"
2. Expand "Personal"
3. Right-click "Certificates" under "Personal"
4. Select "Import..."
5. Follow the steps to import the signing certificate.
   * You may need to change the file browser to `Personal Information Exchange (*.pfx;*.p12)`.
   * When prompted, enter the password for the certificate file you're importing.
   * On the last step, make sure the certificate store is listed as "Personal"

You can verify that you've installed the correct certificate by comparing the thumbprint in the Certificate Manager to
the one listed in the post-build event in the `ScratchLinkSetup` project.

#### Known Issues for Developers

1. Building the `ScratchLinkSetup` project may fail with a `System.IO.DirectoryNotFoundException` if the Windows case
   sensitivity flag is enabled on any directory in the path to the Scratch Link project files. This flag can become
   enabled when WSL is used to create or manipulate directories.
   * Solution: Use `fsutil file queryCaseSensitiveInfo myDirName` to check if `myDirName` has its case sensitivity
     flag set. If so, use `fsutil file setCaseSensitiveInfo myDirName disable` to clear the flag.
   * More detail: <https://github.com/wixtoolset/issues/issues/5809>
2. The `make` step may fail if the path to the Scratch Link directory contains whitespace.
   * Solution: Move Scratch Link to a directory without whitespace in the path.
   * More detail: <http://savannah.gnu.org/bugs/?712> and <https://github.com/LLK/scratch-link/issues/66>
