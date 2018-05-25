# Codename: President Business

This is a work-in-progress intended as a replacement for LLK/scratch-device-manager as a means to connect Scratch to
external hardware devices.

## Getting started

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
