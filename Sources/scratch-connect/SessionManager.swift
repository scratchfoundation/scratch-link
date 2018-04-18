import Foundation
import Swifter

protocol SessionManagerBase: WebSocketSessionDelegate {
    // Hardware-specific, must be implemented in hardware-specific code

    // Type-specific, implemented in SessionManager<T>
    func getSession(forSocket wss: WebSocketSession) -> Session
}

// TODO: implement remaining JSON-RPC 2.0 features: message batching, error responses
extension SessionManagerBase {
    func makeSocketHandler() -> ((HttpRequest) -> HttpResponse) {
        return websocket(session(_:didReceiveText:), session(_:didReceiveBinary:))
    }

    static func makeJSONData(fromObject object: [String: Any]) -> Data? {
        do {
            return try JSONSerialization.data(withJSONObject: object)
        } catch {
            print("Error attempting to serialize JSON from object: \(String(describing: object))")
            return nil
        }
    }

    func session(_ wss: WebSocketSession, didReceiveText text: String) {
        do {
            // Try to encode the string as UTF8 bytes, since that's what the JSON decoder wants
            guard let data = text.data(using: .utf8) else {
                throw SerializationError.Internal("text encoding")
            }
            session(wss, didReceiveData: data) { response in // (response: [String: Any]?) -> Void in
                if let response = response {
                    do {
                        let jsonData = try JSONSerialization.data(withJSONObject: response)
                        if let jsonReply = String(bytes: jsonData, encoding: .utf8) {
                            wss.writeText(jsonReply)
                        } else {
                            print("Could not decode response JSON data to text: \(String(describing: response))")
                        }
                    } catch {
                        print("Error attempting to serialize response to JSON data: \(String(describing: response))")
                    }
                }
            }
        } catch {
            print("Error handling text message: \(error)")
        }
    }

    func session(_ wss: WebSocketSession, didReceiveBinary data: [UInt8]) {
        session(wss, didReceiveData: Data(data)) { response in
            if let response = response {
                let jsonReply = [UInt8](response)
                wss.writeBinary(jsonReply)
            }
        }
    }

    func session(_ wss: WebSocketSession, didReceiveData data: Data,
                 completion: @escaping (_ jsonResponseData: Data?) -> Void) {
        let session = getSession(forSocket: wss)
        session.didReceiveData(data, completion: completion)
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
