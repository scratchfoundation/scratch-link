//
//  SessionDelegate.swift
//  Scratch Link Safari Helper Extension
//
//  Created by Christopher Willis-Ford on 3/30/22.
//

import Foundation
import os.log

fileprivate let logger = OSLog(subsystem: Bundle.main.bundleIdentifier ?? "nil", category: "SessionDelegate")

class SessionDelegate: NSObject, URLSessionWebSocketDelegate {
    
    let sessionID: UInt32
    let sessionType: String
    var webSocket: URLSessionWebSocketTask? // only available in macOS 10.15 or newer
    var openCallback: ((JSONValueResult) -> Void)?
    var receiveCallback: ((JSONObject) -> Void)?
    var closeCallbacks: [(JSONValueResult) -> Void]
    var pendingRequests: [Int: (JSONObjectResult) -> Void]
    
    // MARK: - Public API
    
    static func open(sessionID: UInt32, sessionType: String, completion: @escaping (JSONValueResult) -> Void) -> SessionDelegate {
        let session = SessionDelegate(sessionID, sessionType, completion)
        let urlSession = URLSession(configuration: .default, delegate: session, delegateQueue: OperationQueue())
        let webSocket = urlSession.webSocketTask(with: URL(string: "ws://localhost:20111/scratch/" + sessionType)!)
        
        session.start(webSocket)
        
        return session
    }
    
    func setReceiver(onReceive: @escaping (JSONObject) -> Void) {
        self.receiveCallback = onReceive
    }
    
    func send(messageJSON: JSONObject, completion: @escaping (JSONObjectResult) -> Void) {
        guard let webSocket = webSocket else {
            return completion(.failure("attempt to send with a session that is not open"))
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

    func close(completion: @escaping (JSONValueResult) -> Void) {
        guard let webSocket = webSocket else {
            return completion(.failure("attempt to close session that is not open"))
        }
        closeCallbacks.append(completion)
        webSocket.cancel(with: .normalClosure, reason: nil)
    }

    // MARK: - Internals
    
    private init (_ sessionID: UInt32, _ sessionType: String, _ openCallback: @escaping (JSONValueResult) -> Void) {
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
        os_log("session closed")
        let oldCallbacks = closeCallbacks
        closeCallbacks = []
        for callback in oldCallbacks {
            callback(.success(nil))
        }
    }

    private func listen() {
        let messageMalformed: StaticString = "received malformed message from Scratch Link"
        func receiveHandler(result: Result<URLSessionWebSocketTask.Message, Error>) -> Void {
            switch result {
            case .success(let response):
                switch response {
                case .string(let responseText):
                    if let responseJSON = try? JSONSerialization.jsonObject(with: responseText.data(using: .utf8)!, options: []) as? JSONObject {
                        receiveWrapper(responseJSON)
                    } else {
                        os_log(messageMalformed)
                    }
                case .data(let responseData):
                    if let responseJSON = try? JSONSerialization.jsonObject(with: responseData, options: []) as? JSONObject {
                        receiveWrapper(responseJSON)
                    } else {
                        os_log(messageMalformed)
                    }
                    break // TODO: use responseData
                @unknown default:
                    break // TODO: report error
                }
            case .failure(let error):
                let errorString = error.localizedDescription
                if #available(macOSApplicationExtension 11.0, *) {
                    os_log("error receiving from Scratch Link: \(errorString)")
                } else {
                    os_log("error receiving from Scratch Link")
                }
            }
            if webSocket?.state == .running {
                webSocket?.receive(completionHandler: receiveHandler)
            }
            else {
                os_log("skipping receive: socket not running")
            }
        }
        webSocket?.receive(completionHandler: receiveHandler)
    }
    
    private func receiveWrapper(_ receivedJSON: JSONObject) {
        if receivedJSON["method"] == nil,
           let id = receivedJSON["id"] as? Int,
           let pendingRequest = pendingRequests[id] {
            // if all that is true, this is a response to a request through `send()`
            pendingRequests.removeValue(forKey: id)
            pendingRequest(.success(receivedJSON))
        } else {
            // otherwise it's something else and we should report it out unmodified
            receiveCallback?(receivedJSON)
        }
    }
}
