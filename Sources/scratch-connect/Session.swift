import Foundation
import Swifter

protocol Session {
    // Override this in your hardware-specific session
    func call(_ method: String, withParams params: [String:Any]) throws -> Codable?
    init(withSocket wss: WebSocketSession)

    // These are implemented for you in the extension
    func didReceiveRequest(_ json: [String: Any]) throws -> Data?
    func didReceiveResponse(_ json: [String: Any]) throws -> Data?
}

extension Session {
    func didReceiveRequest(_ json: [String: Any]) throws -> Data? {
        guard let method = json["method"] as? String else {
            throw SerializationError.Invalid("method value missing or not a string")
        }

        // optional: dictionary of parameters by name
        // TODO: do we want to support passing parameters by position?
        let params: [String:Any] = (json["params"] as? [String:Any]) ?? [String:Any]()

        let result: Codable? = try call(method, withParams: params)

        var response: [String:Any?] = [
            "jsonrpc": "2.0",
            "result": result
        ]
        if let id = json["id"] {
            response["id"] = id
        }
        return try JSONSerialization.data(withJSONObject: response)
    }

    func didReceiveResponse(_ json: [String: Any]) throws -> Data? {
        // TODO
        return nil
    }
}
