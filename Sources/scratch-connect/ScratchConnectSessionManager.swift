import Foundation
import Swifter

protocol ScratchConnectSessionManagerBase: WebSocketSessionDelegate {
    // Hardware-specific, must be implemented in hardware-specific code

    // Type-specific, implemented in ScratchConnectSessionManager<T>
    func getSession(forSocket wss: WebSocketSession) -> ScratchConnectSession

    // Not type-specific implemented in ScratchConnectSessionManagerBase
    func makeSocketHandler() -> ((HttpRequest) -> HttpResponse)
    func session(_ wss: WebSocketSession, didReceiveText text: String)
    func session(_ wss: WebSocketSession, didReceiveBinary data: [UInt8])
    func session(_ wss: WebSocketSession, didReceiveJSON data: Data) throws -> Data?
}

// TODO: implement remaining JSON-RPC 2.0 features: message batching, error responses
extension ScratchConnectSessionManagerBase {
    func makeSocketHandler() -> ((HttpRequest) -> HttpResponse) {
        return websocket(session(_:didReceiveText:), session(_:didReceiveBinary:))
    }

    func session(_ wss: WebSocketSession, didReceiveText text: String) {
        do {
            guard let data = text.data(using: .utf8) else {
                throw SerializationError.Internal("text decoding")
            }
            if let result = try session(wss, didReceiveJSON: data) {
                guard let jsonReply = String(bytes: result, encoding: .utf8) else {
                    throw SerializationError.Internal("reply encoding")
                }
                wss.writeText(jsonReply)
            }
        } catch {
            print("Error handling text message: \(error)")
        }
    }

    func session(_ wss: WebSocketSession, didReceiveBinary data: [UInt8]) {
        do {
            if let result = try session(wss, didReceiveJSON: Data(data)) {
                let jsonReply = [UInt8](result)
                wss.writeBinary(jsonReply)
            }
        } catch let error {
            print("Error handling binary message: \(error)")
        }
    }

    func session(_ wss: WebSocketSession, didReceiveJSON data: Data) throws -> Data? {
        guard let json = try JSONSerialization.jsonObject(with: data, options: []) as? [String: Any] else {
            throw SerializationError.Invalid("top-level message structure")
        }

        // property "jsonrpc" must be exactly "2.0"
        if json["jsonrpc"] as? String != "2.0" {
            throw SerializationError.Invalid("JSON-RPC version string")
        }

        // If we made it this far, make sure we hear about this socket going away
        let session = getSession(forSocket: wss)

        if json.keys.contains("method") {
            return try session.didReceiveRequest(json)
        } else if json.keys.contains("result") || json.keys.contains("error") {
            return try session.didReceiveResponse(json)
        } else {
            throw SerializationError.Invalid("message is neither request nor response")
        }
    }
}

class ScratchConnectSessionManager<SessionType: ScratchConnectSession>: ScratchConnectSessionManagerBase {
    var sessions = [WebSocketSession:SessionType]()

    func getSession(forSocket wss: WebSocketSession) -> ScratchConnectSession {
        if let session = sessions[wss] {
            return session
        }
        let session = SessionType.init(withSocket: wss)
        sessions[wss] = session
        wss.delegate = self
        return session
    }

    func sessionWillClose(_ wss: WebSocketSession) {
        wss.delegate = nil
        sessions.removeValue(forKey: wss)
    }
}
