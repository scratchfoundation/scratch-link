import Foundation

class BundleInfo {
    private static let defaultTitle = "Scratch Link"
    private static let defaultVersion = "(unknown version)"

    static func getTitle() -> String {
        return Bundle.main.object(forInfoDictionaryKey: "CFBundleDisplayName") as? String ?? BundleInfo.defaultTitle
    }

    static func getVersion() -> String {
        return Bundle.main.object(forInfoDictionaryKey: "CFBundleVersion") as? String ?? BundleInfo.defaultVersion
    }

    static func getVersionDetail() -> String {
        return Bundle.main.object(forInfoDictionaryKey: "ScratchVersionDetail") as? String ?? BundleInfo.defaultVersion
    }
}
