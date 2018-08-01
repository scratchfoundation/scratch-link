import Foundation
import PerfectHTTP
import PerfectWebSockets

protocol SessionManagerBase {
    func makeSessionHandler(forRequest request: HTTPRequest) throws -> WebSocketHandler
}

class SessionManager<SessionType: Session>: SessionManagerBase, WebSocketSessionHandler {
    let socketProtocol: String? = nil
    var sessions = [SessionType]()

    func makeSessionHandler(forRequest request: HTTPRequest) throws -> WebSocketHandler {
        return WebSocketHandler(handlerProducer: {
            (request: HTTPRequest, protocols: [String]) -> WebSocketSessionHandler? in
            return self
        })
    }

    func handleSession(request req: HTTPRequest, socket: WebSocket) {
        do {
            sessions.append(try SessionType.init(withSocket: socket))
        } catch {
            let webMessage = "Session init failed for path \(req.path)"
            print("\(webMessage): \(error)")
            socket.sendStringMessage(string: webMessage, final: true) {
                socket.close()
            }
        }
    }
}
