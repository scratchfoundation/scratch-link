# Scratch Link 2.0

Scratch Link is a helper application which allows Scratch 3.0 to communicate with hardware peripherals. Scratch Link
replaces the Scratch Device Manager and Scratch Device Plug-in.

System Requirements:

| | Minimum
| --- | ---
| macOS | 10.15 "Catalina"
| Windows | Windows 10 build 17763

The Windows version requires the Windows App Runtime version 1.2, and will install it automatically if possible.

Manual installation is available here (choose your platform):

* https://aka.ms/windowsappsdk/1.2/latest/windowsappruntimeinstall-x64.exe
* https://aka.ms/windowsappsdk/1.2/latest/windowsappruntimeinstall-x86.exe
* https://aka.ms/windowsappsdk/1.2/latest/windowsappruntimeinstall-ARM64.exe

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

### Version numbers

Scratch Link 2.0 uses [semantic-release](https://semantic-release.gitbook.io/semantic-release/) to control its version
number. The `develop` branch is treated as a pre-release branch, and `main` is treated as a release branch. Each time
a change is merged to either of those branches, `semantic-release` will calculate a new version number.

Apple requires that `CFBundleShortVersionString` is unique for published releases. The App Store will also reject an
upload unless the `CFBundleVersion` tuple is greater than that of previously uploaded builds. To make this easy, we
set both to the version calculated by `semantic-release`. The uniqueness requirement means we can't "try again" on
the same version number, but that just enforces the semantic versioning so it's arguably a good thing.

Extended version information is available within the application. This extended information is similar to `git
describe`.

### Secure WebSockets

Some previous versions of Scratch Link used Secure WebSockets (`wss://`) to communicate with Scratch. This is no
longer the case: new versions of Scratch Link use regular WebSockets (`ws://`). It is no longer necessary to prepare
an SSL certificate for Scratch Link.

This change caused an incompatibility with some browsers, including Safari. The macOS version of Scratch Link 2.0
includes a Safari extension to resolve this incompatibility.

### Windows platforms and installer size

The `PublishReadyToRun` (R2R) setting enables ahead-of-time (AOT) compilation, as opposed to just-in-time (JIT)
compilation. This can improve performance, especially at startup. The drawback is [R2R binaries are larger because
they contain both intermediate language (IL) code, which is still needed for some scenarios, and the native version
of the same code.](https://learn.microsoft.com/en-us/dotnet/core/deploying/ready-to-run)

Recent versions of .NET (5.0 and above) can build a "Framework-Dependent Application" or a "Self-Contained
Application" depending on settings.

* A self-contained application includes the .NET runtime framework. This includes a platform-specific (x86, x64, or
  ARM64) version of `dotnet.exe` to host the application.
  * Cannot be built for "AnyCPU" because it must include the native portion of the runtime.
  * The app can be "trimmed" to include only the portions of the framework needed by the application, but it'll
    still be larger than a framework-dependent application.
* A framework-dependent application does not include the framework at all; it must be installed separately.
  * The generated MSIX will trigger automatic framework installation if necessary (requires Internet connection).
  * Can be built for "AnyCPU" since it doesn't include the native portion (or any other portion) of the runtime.
  * Can be built for a specific CPU if desired.
  * Debugging this requires setting `<WindowsPackageType>None</WindowsPackageType>` in the project file.

When packaging an application:

* An MSIX file (`*.msix`) can contain exactly one platform (x86, x64, ARM64).
* An MSIX Bundle (`*.msixbundle`) can contain more than one MSIX -- one for each platform, for example.

Ideally, it would be possible to package a single "AnyCPU" build of the app with stub MSIX files to install each
platform-specific copy of the framework, resulting in a Bundle that's only a little larger than a single copy of the
app. More investigation needed.

However, it is possible to build a platform-specific MSIX containing an AnyCPU build of the app. That's much smaller
than a platform-specific build of the app, so even with 3 full copies of the AnyCPU app -- one each packaged for x86,
x64, and ARM64 -- the resulting bundle is significantly smaller.

Disabling R2R and bundling AnyCPU builds of the app generated a bundle roughly 12% of the size of a bundle of
self-contained apps for the same set of platforms.
