import Foundation

struct JSONRPCError: Codable, Error {
    typealias ErrorData = String // TODO: support richer error data

    let code: Int
    let message: String
    let data: ErrorData?

    init(code: Int, message: String, data: ErrorData? = nil) {
        self.code = code
        self.message = message
        self.data = data
    }

    static func ParseError(data: ErrorData? = nil) -> JSONRPCError {
        return JSONRPCError(code: -32700, message: "Parse Error", data: data)
    }

    static func InvalidRequest(data: ErrorData? = nil) -> JSONRPCError {
        return JSONRPCError(code: -32600, message: "Invalid Request", data: data)
    }

    static func MethodNotFound(data: ErrorData? = nil) -> JSONRPCError {
        return JSONRPCError(code: -32601, message: "Method Not Found", data: data)
    }

    static func InvalidParams(data: ErrorData? = nil) -> JSONRPCError {
        return JSONRPCError(code: -32602, message: "Invalid Params", data: data)
    }

    static func InternalError(data: ErrorData? = nil) -> JSONRPCError {
        return JSONRPCError(code: -32603, message: "Internal Error", data: data)
    }

    static func ServerError(code: Int, data: ErrorData? = nil) -> JSONRPCError {
        return JSONRPCError(code: code, message: "Server Error", data: data)
    }

    /// MARK: Codable

    enum CodingKeys: String, CodingKey {
        case code = "code"
        case message = "message"
        case data = "data"
    }

    init(from decoder: Decoder) throws {
        let values = try decoder.container(keyedBy: CodingKeys.self)
        code = try values.decode(Int.self, forKey: .code)
        message = try values.decode(String.self, forKey: .message)

        if values.contains(.data) {
            data = try values.decode(ErrorData.self, forKey: .data)
        } else {
            data = nil
        }
    }

    func encode(to encoder: Encoder) throws {
        print("hi I'm encoding")
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(code, forKey: .code)
        try container.encode(message, forKey: .message)
        if let data = self.data { // JSONSerialization can't handle optional values
            try container.encode(data, forKey: .data)
        }
    }
}
