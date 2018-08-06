import Foundation

/// Helper methods to deal with encoding and decoding JSON message payloads
class EncodingHelpers {

    /// Decode the `message` property of `json` into a Data object.
    /// If the JSON has an `encoding` property, use that method. Otherwise, assume the message is Unicode text.
    ///
    /// - parameters:
    ///   - json: a JSON object containing a `message` property and optionally an `encoding` property.
    /// - returns: a Data object containing the decoded data
    /// - throws: a `JSONRPCError` if the message could not be decoded
    public static func decodeBuffer(fromJSON json: [String: Any]) throws -> Data {
        guard let message = json["message"] as? String else {
            throw JSONRPCError.invalidParams(data: "missing message property")
        }
        let encoding = json["encoding"] as? String

        switch encoding {
        case .some("base64"): // "message" is encoded with Base64
            if let result = Data(base64Encoded: message) {
                return result
            } else {
                throw JSONRPCError.invalidParams(data: "failed to decode Base64 message")
            }
        case .none: // "message" is a Unicode string with no additional encoding
            if let result = message.data(using: .utf8) {
                return result
            } else {
                throw JSONRPCError.internalError(data: "failed to transcode message to UTF-8")
            }
        default:
            throw JSONRPCError.invalidParams(data: "unsupported encoding: \(encoding!)")
        }
    }

    /// Encode `data` using `encoding`, either into `destination` or a new JSON object.
    ///
    /// - parameters:
    ///   - data: The data to encode
    ///   - encoding: The type of encoding to use, or null to "encode" as a Unicode string
    ///   - destination: The optional object to encode into. If not null, the "message" and "encoding" properties will
    ///     be adjusted as necessary. If null, a new object will be created with "message" and (possibly) "encoding"
    ///     properties.
    /// - returns: The object to which the encoded message was written, or nil if the encoding is unsupported
    public static func encodeBuffer(
            _ data: Data, withEncoding encoding: String?, intoObject destination: [String: Any]? = nil)
                    -> [String: Any]? {
        var result = destination ?? [:]

        switch encoding {
        case .some("base64"):
            result["encoding"] = "base64"
            result["message"] = data.base64EncodedString(options: NSData.Base64EncodingOptions(rawValue: 0))
        case .none:
            result.removeValue(forKey: "encoding")
            result["message"] = String.init(data: data, encoding: String.Encoding.utf8)
        default:
            return nil
        }

        return result
    }
}
