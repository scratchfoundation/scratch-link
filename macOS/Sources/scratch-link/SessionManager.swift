import Foundation
import PerfectHTTP
import PerfectWebSockets

protocol SessionManagerBase {
    func makeSessionHandler(forRequest request: HTTPRequest) throws -> WebSocketHandler
}

class SessionManager<SessionType: Session>: SessionManagerBase, WebSocketSessionHandler {
    let socketProtocol: String? = nil

    func makeSessionHandler(forRequest request: HTTPRequest) throws -> WebSocketHandler {
        return WebSocketHandler(handlerProducer: {
            (request: HTTPRequest, protocols: [String]) -> WebSocketSessionHandler? in
            return self
        })
    }

    func handleSession(request req: HTTPRequest, socket: WebSocket) {
        do {
            let session = try SessionType.init(withSocket: socket)
            session.handleSession(request: req, socket: socket)
        } catch {
            let webMessage = "Session init failed for path \(req.path)"
            print("\(webMessage): \(error)")
            socket.sendStringMessage(string: webMessage, final: true) {
                socket.close()
            }
        }
    }
}
