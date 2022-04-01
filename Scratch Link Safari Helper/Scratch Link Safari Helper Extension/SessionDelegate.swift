//
//  SessionDelegate.swift
//  Scratch Link Safari Helper Extension
//
//  Created by Christopher Willis-Ford on 3/30/22.
//

import Foundation

class SessionDelegate: NSObject, URLSessionWebSocketDelegate {
    
    let sessionID: UInt32
    let sessionType: String
    var webSocket: URLSessionWebSocketTask?
    var openCallback: ((JSONResult) -> Void)?
    var receiveCallback: ((JSONResult) -> Void)?
    var closeCallbacks: [(JSONResult) -> Void]
    var pendingRequests: [Int: (JSONResult) -> Void]
    
    // MARK: - Public API
    
    static func open(sessionID: UInt32, sessionType: String, completion: @escaping (JSONResult) -> Void) -> SessionDelegate {
        let session = SessionDelegate(sessionID, sessionType, completion)
        let urlSession = URLSession(configuration: .default, delegate: session, delegateQueue: OperationQueue())
        let webSocket = urlSession.webSocketTask(with: URL(string: "ws://localhost:20111/scratch/" + sessionType)!)
        
        session.start(webSocket)
        
        return session
    }
    
    func setReceiver(onReceive: @escaping (JSONResult) -> Void) {
        self.receiveCallback = onReceive
    }
    
    func send(messageJSON: JSON, completion: @escaping (JSONResult) -> Void) {
        guard let webSocket = webSocket else {
            return completion(.failure("session not open"))
        }
        guard let messageData = try? JSONSerialization.data(withJSONObject: messageJSON) else {
            return completion(.failure("attempt to send malformed message"))
        }
        let messageObject = URLSessionWebSocketTask.Message.data(messageData)
        webSocket.send(messageObject) { error in
            if let error = error {
                return completion(.failure(error.localizedDescription))
            } else if let id = messageJSON["id"] as? Int {
                self.pendingRequests[id] = completion
            }
        }
    }

    func close(completion: @escaping (JSONResult) -> Void) {
        guard let webSocket = webSocket else {
            return completion(.failure("session not open"))
        }
        closeCallbacks.append(completion)
        webSocket.cancel(with: .normalClosure, reason: nil)
    }

    // MARK: - Internals
    
    private init (_ sessionID: UInt32, _ sessionType: String, _ openCallback: @escaping (JSONResult) -> Void) {
        self.sessionType = sessionType
        self.sessionID = sessionID
        self.webSocket = nil
        self.openCallback = openCallback
        self.closeCallbacks = []
        self.pendingRequests = [:]
        super.init()
    }

    private func start(_ webSocket: URLSessionWebSocketTask) {
        self.webSocket = webSocket
        webSocket.resume()
        listen()
    }
    
    internal func urlSession(_ session: URLSession, webSocketTask: URLSessionWebSocketTask, didOpenWithProtocol protocol: String?) {
        openCallback!(.success(sessionID))
        openCallback = nil
    }

    internal func urlSession(_ session: URLSession, webSocketTask: URLSessionWebSocketTask, didCloseWith closeCode: URLSessionWebSocketTask.CloseCode, reason: Data?) {
        if let openCallback = openCallback {
            openCallback(.failure("closed"))
        }
        let oldCallbacks = closeCallbacks
        closeCallbacks = []
        for callback in oldCallbacks {
            callback(.success(nil))
        }
    }

    private func listen() {
        let messageMalformed = "received malformed message from Scratch Link"
        func receiveHandler(result: Result<URLSessionWebSocketTask.Message, Error>) -> Void {
            switch result {
            case .success(let response):
                switch response {
                case .string(let responseText):
                    if let responseJSON = try? JSONSerialization.jsonObject(with: responseText.data(using: .utf8)!, options: []) as? JSON {
                        receiveWrapper(.success(responseJSON))
                    } else {
                        receiveWrapper(.failure(messageMalformed))
                    }
                case .data(let responseData):
                    if let responseJSON = try? JSONSerialization.jsonObject(with: responseData, options: []) as? JSON {
                        receiveWrapper(.success(responseJSON))
                    } else {
                        receiveWrapper(.failure(messageMalformed))
                    }
                    break // TODO: use responseData
                @unknown default:
                    break // TODO: report error
                }
            case .failure(let error):
                receiveWrapper(.failure(error.localizedDescription))
            }
            webSocket?.receive(completionHandler: receiveHandler)
        }
        webSocket?.receive(completionHandler: receiveHandler)
    }
    
    private func receiveWrapper(_ result: JSONResult) {
        if case let .success(rpcResult) = result,
           let rpcResultJSON = rpcResult as? JSON,
           let id = rpcResultJSON["id"] as? Int,
           let pendingRequest = pendingRequests[id] {
            // if all that is true, this is a response to a request through `send()`
            pendingRequests.removeValue(forKey: id)
            pendingRequest(result)
        } else {
            // otherwise it's something else and we should report it out unmodified
            receiveCallback?(result)
        }
    }
}
