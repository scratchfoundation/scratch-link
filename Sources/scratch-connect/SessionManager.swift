import Foundation
import Swifter

protocol SessionManagerBase: WebSocketSessionDelegate {
    // Hardware-specific, must be implemented in hardware-specific code

    // Type-specific, implemented in SessionManager<T>
    func getSession(forSocket wss: WebSocketSession) -> Session

    // Not type-specific, implemented in SessionManagerBase
    func makeSocketHandler() -> ((HttpRequest) -> HttpResponse)
    func session(_ wss: WebSocketSession, didReceiveText text: String)
    func session(_ wss: WebSocketSession, didReceiveBinary data: [UInt8])
    func session(_ wss: WebSocketSession, didReceiveJSON data: Data) throws -> Data?
}

// TODO: implement remaining JSON-RPC 2.0 features: message batching, error responses
extension SessionManagerBase {
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

class SessionManager<SessionType: Session>: SessionManagerBase {
    var sessions = [WebSocketSession:SessionType]()

    func getSession(forSocket wss: WebSocketSession) -> Session {
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
