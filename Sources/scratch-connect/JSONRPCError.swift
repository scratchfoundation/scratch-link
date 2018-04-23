import Foundation

struct JSONRPCError: Codable, Error {
    typealias ErrorData = String // TODO: support richer error data

    let code: Int
    let message: String
    let data: ErrorData?

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
}
