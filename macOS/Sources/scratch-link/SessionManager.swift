import Foundation
import Telegraph

protocol SessionManagerBase {
    func makeSession(forSocket webSocket: WebSocket) throws -> Session
}

class SessionManager<SessionType: Session>: SessionManagerBase {
    func makeSession(forSocket webSocket: WebSocket) throws -> Session {
        return try SessionType.init(withSocket: webSocket)
    }
}
