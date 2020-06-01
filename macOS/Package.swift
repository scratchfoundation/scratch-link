// swift-tools-version:5.1
import PackageDescription

let package = Package(
    name: "scratch-link",
    platforms: [
        .macOS(.v10_10)
    ],
    dependencies: [
        .package(url:"https://github.com/PerfectlySoft/Perfect-HTTPServer.git", from: "3.0.0"),
        .package(url:"https://github.com/PerfectlySoft/Perfect-WebSockets.git", from: "3.0.0")
    ],
    targets: [
        .target(
            name: "scratch-link",
            dependencies: ["PerfectHTTPServer", "PerfectWebSockets"])
    ]
)
