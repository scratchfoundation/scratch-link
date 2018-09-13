import Foundation

enum RSSI {
    case Unknown // +127
    case Unsupported // 0
    case Wired // nil
    case Valid(Int)

    init(rawValue: NSNumber) {
        if let intValue = rawValue as? Int {
            if (intValue >= 0) {
                self = .Unknown
            } else {
                self = .Valid(intValue)
            }
        } else {
            self = .Unknown
        }
    }

    var rawValue: Int? {
        get {
            switch self {
            case .Unknown: return 127
            case .Unsupported: return 0
            case .Wired: return nil
            case .Valid(let value): return value
            }
        }
    }
}
