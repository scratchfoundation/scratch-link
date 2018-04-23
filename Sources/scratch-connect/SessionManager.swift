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

    func session(_ wss: WebSocketSession, didReceiveText text: String) {
        do {
            // Try to encode the string as UTF8 bytes, since that's what the JSON decoder wants
            guard let data = text.data(using: .utf8) else {
                throw SerializationError.Internal("text encoding")
            }
            session(wss, didReceiveData: data) { (jsonResponseData:Data?) in
                if let jsonResponseData = jsonResponseData {
                    if let jsonResponseText = String(bytes: jsonResponseData, encoding: .utf8) {
                        wss.writeText(jsonResponseText)
                    } else {
                        print("Could not decode response JSON data to text: \(String(describing: jsonResponseData))")
                    }
                }
            }
        } catch {
            print("Error handling text message: \(error)")
        }
    }

    func session(_ wss: WebSocketSession, didReceiveBinary data: [UInt8]) {
        session(wss, didReceiveData: Data(data)) { (jsonResponseData:Data?) in
            if let jsonResponseData = jsonResponseData {
                let jsonResponseBytes = [UInt8](jsonResponseData)
                wss.writeBinary(jsonResponseBytes)
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
