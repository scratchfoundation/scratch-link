import Foundation

struct JSONRPCError: Error {
    typealias ErrorData = String // TODO: support richer error data

    let code: Int
    let message: String
    let data: ErrorData?

    init(code: Int, message: String, data: ErrorData? = nil) {
        self.code = code
        self.message = message
        self.data = data
    }

    init(fromJSON json: [String:Any]) {
        if let code = json["code"] as? Int, let message = json["message"] as? String {
            self.code = code
            self.message = message
            self.data = json["data"] as? ErrorData
        } else {
            // Consider it a parse error
            code = -32700
            message = "Parse Error"
            data = "Could not parse error JSON"
        }
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

    static func ApplicationError(data: ErrorData? = nil) -> JSONRPCError {
        return JSONRPCError(code: -32500, message: "Application Error", data: data)
    }
}
