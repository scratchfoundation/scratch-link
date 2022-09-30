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
    var webSocket: URLSessionWebSocketTask? // only available in macOS 10.15 or newer
    var openCallback: ((JSONValueResult) -> Void)?
    var closeCallbacks: [(JSONValueResult) -> Void]
    var pendingRequests: [JSONValue: (JSONObjectResult) -> Void]

    // accumulate responses for polling
    let pendingResponsesQ: DispatchQueue
    var pendingResponses: [JSONObject]

    // MARK: - Public API

    static func open(sessionID: UInt32, sessionType: String, completion: @escaping (JSONValueResult) -> Void) -> SessionDelegate {
        let session = SessionDelegate(sessionID, sessionType, completion)
        let urlSession = URLSession(configuration: .default, delegate: session, delegateQueue: OperationQueue())
        let webSocket = urlSession.webSocketTask(with: URL(string: "ws://localhost:20111/scratch/" + sessionType)!)

        session.start(webSocket)

        return session
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
            } else if let id = messageJSON["id"] as? JSONValue {
                self.pendingRequests[id] = completion
            }
        }
    }

    func poll(completion: @escaping (JSONValueResult) -> Void) {
        // take ownership of the current message queue and replace it with a new, empty one
        // unless there are no messages, in which case don't do anything
        let responses = pendingResponsesQ.sync {() -> [JSONObject]? in
            if pendingResponses.isEmpty {
                return nil
            }

            let responses = pendingResponses
            pendingResponses = [JSONObject]()
            return responses
        }

        return completion(.success(responses))
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
        self.pendingResponsesQ = DispatchQueue(label: "controls messages pending for JS")
        self.pendingResponses = [JSONObject]()
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
        ScratchLog.log("session closed", type: .info)
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
                        onMessageFromScratchLink(responseJSON)
                    } else {
                        ScratchLog.log(messageMalformed, type: .error)
                    }
                case .data(let responseData):
                    if let responseJSON = try? JSONSerialization.jsonObject(with: responseData, options: []) as? JSONObject {
                        onMessageFromScratchLink(responseJSON)
                    } else {
                        ScratchLog.log(messageMalformed, type: .error)
                    }
                    break // TODO: use responseData
                @unknown default:
                    break // TODO: report error
                }
            case .failure(let error):
                ScratchLog.log("error receiving from Scratch Link: %{public}@", type: .error, String(describing: error))
            }
            if webSocket?.state == .running {
                webSocket?.receive(completionHandler: receiveHandler)
            }
            else {
                ScratchLog.log("skipping receive: socket not running", type: .error)
            }
        }
        webSocket?.receive(completionHandler: receiveHandler)
    }

    private func onMessageFromScratchLink(_ receivedJSON: JSONObject) {
        if receivedJSON["method"] == nil,
           let id = receivedJSON["id"] as? JSONValue,
           let pendingRequest = pendingRequests[id] {
            // if all that is true, this is a response to a request through `send()`
            pendingRequests.removeValue(forKey: id)
            pendingRequest(.success(receivedJSON))
        } else {
            // otherwise it's something else and we should report it out unmodified
            pendingResponsesQ.async {
                self.pendingResponses.append(receivedJSON)
            }
        }
    }
}
