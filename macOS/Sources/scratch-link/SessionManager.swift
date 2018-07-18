import Foundation
import Telegraph

protocol SessionManagerBase {
    func makeSession(forSocket webSocket: WebSocket) -> Session
}

class SessionManager<SessionType: Session>: SessionManagerBase {
    func makeSession(forSocket webSocket: WebSocket) -> Session {
        return SessionType.init(withSocket: webSocket)
    }
}
