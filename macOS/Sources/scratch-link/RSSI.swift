import Foundation

enum RSSI {
    case unknown // +127
    case unsupported // 0
    case wired // nil
    case valid(Int)

    init(rawValue: NSNumber) {
        if let intValue = rawValue as? Int {
            if intValue >= 0 {
                self = .unknown
            } else {
                self = .valid(intValue)
            }
        } else {
            self = .unknown
        }
    }

    var rawValue: Int? {
        switch self {
        case .unknown: return 127
        case .unsupported: return 0
        case .wired: return nil
        case .valid(let value): return value
        }
    }
}
