# Scratch Link 2.0

Scratch Link is a helper application which allows Scratch 3.0 to communicate with hardware peripherals. Scratch Link
replaces the Scratch Device Manager and Scratch Device Plug-in.

System Requirements:

| | Minimum
| --- | ---
| macOS | 10.15 "Catalina"
| Windows | Windows 10 build 17763

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

Scratch Link 2.0 uses a new versioning scheme based on [GitVersion](https://gitversion.net/docs/). The major, minor,
and patch versions are now determined by GitVersion analyzing `main` branch commit messages in "Conventional Commit"
format. Only `main` branch builds bump the release version number. Builds from `develop` get a prerelease version
number, with a build number calculated by GitVersion analyzing the number of commits since the last release.

For submission to app stores, the most interesting GitVersion properties are `Major`, `Minor`, `Patch`, and
`WeightedPreReleaseNumber`.

For Apple builds:
* `CFBundleShortVersionString` should be `Major`.`Minor`.`Patch`
* `CFBundleVersion` should be `WeightedPreReleaseNumber`

This should satisfy Apple's requirements that `CFBundleShortVersionString` is unique for published releases, and the
`(CFBundleShortVersionString,CFBundleVersion)` tuple is unique for uploaded builds.

### Secure WebSockets

Previous versions of Scratch Link used Secure WebSockets (`wss://`) to communicate with Scratch. This is no longer the
case: new versions of Scratch Link use regular WebSockets (`ws://`). It is no longer necessary to prepare an SSL
certificate for Scratch Link.

This change caused an incompatibility with some browsers, including Safari. The macOS version of Scratch Link 2.0
includes a Safari extension to resolve this incompatibility.

### Microsoft Store platforms

In theory, the Windows version of this application could be built for 5 different "platform" values:

- `x86`
- `x64`
- `ARM` (or `ARM32`)
- `ARM64`
- `AnyCPU` (displayed as `Any CPU`)

In practice (checked with the `file` command):

- `x86`, `x64`, and `ARM64` all work as expected
- Building `ARM32` results in `x86` binaries so it seems like the wrong name for this platform (bug in VS2022?)
- Building `ARM` results in `ARMv7 Thumb` binaries. Switching to this requires hand-editing the `csproj` file.
- Building `AnyCPU` results in `x86` binaries, but maybe in "IL Only" mode? Needs more investigation.

Also, trying to build an MSIX from `ARM`, `ARM32` or `AnyCPU` fails. This might be due to Scratch Link using the
"Desktop Bridge" which appears to a) require native components (so no `AnyCPU`) and b) be unsupported by 32-bit ARM.

Scratch Link 1.4 seemed to work in `AnyCPU` mode even though it also used the Desktop Bridge, so there's something
missing here. Maybe the newer version of Desktop Bridge requires native components? Or maybe I just misconfigured
something...

Side note: as of Visual Studio 2022 version 17.4.4, the _project_ configuration name for the MSIX project must match
the _solution_ configuration name used for building it, or bundle validation will fail.
