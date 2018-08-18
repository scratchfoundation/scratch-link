import Foundation
import PerfectHTTP
import PerfectWebSockets

protocol SessionManagerBase {
    func makeSessionHandler(forRequest request: HTTPRequest) throws -> WebSocketHandler
}

class SessionManager<SessionType: Session>: SessionManagerBase, WebSocketSessionHandler {
    let socketProtocol: String? = nil

    func makeSessionHandler(forRequest request: HTTPRequest) throws -> WebSocketHandler {
        return WebSocketHandler(handlerProducer: { (_: HTTPRequest, _: [String]) -> WebSocketSessionHandler? in
            return self
        })
    }

    func logMessage(
        socket: WebSocket, webMessage: String, localDetails: String, completion: @escaping (() -> Void) = {}) {
        print("\(webMessage): \(localDetails)")
        socket.sendStringMessage(string: webMessage, final: true, completion: completion)
    }

    func handleSession(request req: HTTPRequest, socket: WebSocket) {
        do {
            let session = try SessionType.init(withSocket: socket)
            session.handleSession(webSocket: socket)
        } catch {
            logMessage(
                socket: socket,
                webMessage: "Session init failed for path \(req.path)",
                localDetails: error.localizedDescription) {
                socket.close()
            }
        }
    }
}
