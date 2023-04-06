# Scratch Link 2.0

Scratch Link is a helper application which allows Scratch 3.0 to communicate with hardware peripherals. Scratch Link
replaces the Scratch Device Manager and Scratch Device Plug-in.

System Requirements:

| | Minimum
| --- | ---
| macOS | 10.15 "Catalina"
| Windows | Coming soon! Use Scratch Link 1.4.x for now.

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

Apple requires that `CFBundleShortVersionString` is unique for published releases, and the
`(CFBundleShortVersionString,CFBundleVersion)` tuple is unique for uploaded builds. The `CFBundleShortVersionString`
is version calculated by `semantic-release`, and `CFBundleVersion` is calculated from the number of commits since the
tag made by `semantic-release`. This information is available through `git describe`.

### Secure WebSockets

Some previous versions of Scratch Link used Secure WebSockets (`wss://`) to communicate with Scratch. This is no
longer the case: new versions of Scratch Link use regular WebSockets (`ws://`). It is no longer necessary to prepare
an SSL certificate for Scratch Link.

This change caused an incompatibility with some browsers, including Safari. The macOS version of Scratch Link 2.0
includes a Safari extension to resolve this incompatibility.
