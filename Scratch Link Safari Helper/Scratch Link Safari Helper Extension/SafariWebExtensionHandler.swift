//
//  SafariWebExtensionHandler.swift
//  Scratch Link Safari Helper Extension
//
//  Created by Christopher Willis-Ford on 3/16/22.
//

import Foundation
import SafariServices
import os.log

let SFExtensionMessageKey = "message"

var sessionMap = Dictionary<UInt32, URLSessionWebSocketTask>()

// WARNING: This class is reinstantiated for each request from the browser!
class SafariWebExtensionHandler: NSObject, NSExtensionRequestHandling, URLSessionWebSocketDelegate {

    typealias JSON = Dictionary<String, AnyHashable?>
    typealias MethodHandler = (
        _ sessionID: UInt32,
        _ method: String,
        _ params: JSON?,
        _ responseID: UInt32?
    ) -> JSONResult
    
    enum JSONResult {
        case success(AnyHashable? = nil)
        case failure(AnyHashable)
    }
    
    let myBundleIdentifier = Bundle.main.bundleIdentifier ?? "nil"

	func beginRequest(with context: NSExtensionContext) {
        guard
            let message = getMessage(from: context),
            let session = message["session"] as? UInt32,
            let method = message["method"] as? String
        else {
            os_log(.error, "Ignoring malformed message")
            return
        }
        
        let id = message["id"] as? UInt32
        let params = message["params"] as? JSON
        
        os_log(.default, "Received message from browser.runtime.sendNativeMessage: %@", message)

        let handler: MethodHandler = {
            switch method {
            case "open":
                return openSession
            case "close":
                return closeSession
            case "send":
                return sendMessage
            default:
                return unrecognizedMethod
            }
        }()

        let result = handler(session, method, params, id)
        if let id = id {
            var response: JSON = [
                "jsonrpc": "2.0",
                "session": session,
                "id": id
            ]
            switch result {
            case .success(let result):
                response["result"] = result
            case .failure(let error):
                response["error"] = error
            }
            self.completeContextRequest(for: context, withMessage: response)
        }
    }
    
    func getMessage(from context: NSExtensionContext) -> JSON? {
        guard let item = context.inputItems[0] as? NSExtensionItem else {
            return nil
        }
        return item.userInfo?[SFExtensionMessageKey] as? JSON
    }

    func openSession(with sessionID: UInt32, method: String, params: JSON?, id: UInt32?) -> JSONResult {

        guard let sessionType = params?["type"] as? String else {
            return .failure("call to 'open' with bad or missing 'type' parameter")
        }
        
        let urlSession = URLSession(configuration: .default, delegate: self, delegateQueue: OperationQueue())
        let task = urlSession.webSocketTask(with: URL(string: "ws://localhost:20111/scratch/" + sessionType)!)
        
        sessionMap[sessionID] = task

        task.resume()
        
        startListening(to: task, withSessionID: sessionID)
        
        // TODO: wait for didOpenWithProtocol
        return .success()
    }
    
    func closeSession(with sessionID: UInt32, method: String, params: JSON?, id: UInt32?) -> JSONResult {
        guard let task = sessionMap[sessionID] else {
            return .failure("attempt to close unrecognized session")
        }
        task.cancel(with: .normalClosure, reason: nil)
        // TODO: wait for didCloseWith
        return .success()
    }
    
    func sendMessage(with sessionID: UInt32, method: String, params: JSON?, id: UInt32?) -> JSONResult {
        guard let webSocket = sessionMap[sessionID] else {
            return .failure("session not open")
        }
        guard let messageJSON = params else {
            return .failure("attempt to send empty message")
        }
        guard let messageData = try? JSONSerialization.data(withJSONObject: messageJSON) else {
            return .failure("attempt to send malformed message")
        }
        let messageObject = URLSessionWebSocketTask.Message.data(messageData)
        webSocket.send(messageObject) { error in
            // TODO: asynchronously report success / failure
        }
        return .success()
    }

    func unrecognizedMethod(with sessionID: UInt32, method: String, params: JSON?, id: UInt32?) -> JSONResult {
        os_log(.error, "Ignoring call to unrecognized method: %@", method)
        return .failure("unrecognized method")
    }
    
    func urlSession(_ session: URLSession, webSocketTask: URLSessionWebSocketTask, didOpenWithProtocol protocol: String?) {
        // TODO: let openSession send its result
    }

    func urlSession(_ session: URLSession, webSocketTask: URLSessionWebSocketTask, didCloseWith closeCode: URLSessionWebSocketTask.CloseCode, reason: Data?) {
        // TODO: let closeSession send its result (if pending)
        // TODO: remove session from session map
    }

    func startListening(to task: URLSessionWebSocketTask, withSessionID sessionID: UInt32) {
        func receiveHandler(result: Result<URLSessionWebSocketTask.Message, Error>) -> Void {
            switch result {
            case .success(let response):
                switch response {
                case .string(let responseText):
                    if let responseJson = try? JSONSerialization.jsonObject(with: responseText.data(using: .utf8)!, options: []) as? JSON {
                        // TODO: use responseJson
                    } else {
                        // TODO: use responseText
                    }
                case .data(let responseData):
                    break // TODO: use responseData
                @unknown default:
                    break // TODO: report error
                }
            case .failure(let error):
                break // TODO: report error
            }
            if sessionMap[sessionID] != nil {
                task.receive(completionHandler: receiveHandler)
            }
        }
        task.receive(completionHandler: receiveHandler)
    }
    
    func completeContextRequest(for context: NSExtensionContext, withMessage message: JSON?, completionHandler: ((Bool) -> Void)? = nil) {
        let response = NSExtensionItem()
        if let message = message {
            response.userInfo = [ SFExtensionMessageKey: message ]
        }
        context.completeRequest(returningItems: [response], completionHandler: completionHandler)
        DispatchQueue.main.asyncAfter(deadline: .now() + 5.0) {
            context.completeRequest(returningItems: [response], completionHandler: completionHandler)
        }
    }

    func spare(with context: NSExtensionContext) {
        SFSafariApplication.dispatchMessage(withName: "native dispatchMessage", toExtensionWithIdentifier: myBundleIdentifier, userInfo: ["dispatchMessage userInfo": "here"]) {(error) -> Void in
            os_log(.default, "Dispatching message to the extension finished")
        }

        let task = URLSession.shared.webSocketTask(with: URL(string: "ws://localhost:20111/scratch/ble")!)
        task.resume()
        let versionRequestJson: [String: Any?] = [
            "jsonrpc": "2.0",
            "id": 42,
            "method": "getVersion"
        ]
        let versionRequestData = try? JSONSerialization.data(withJSONObject: versionRequestJson)
        let versionRequest = URLSessionWebSocketTask.Message.data(versionRequestData!)
        task.send(versionRequest) { error in
            if let error = error {
                self.completeContextRequest(for: context, withMessage: [
                    "sendFailed": error.localizedDescription,
                    "userInfo": "\((error as NSError).userInfo)"
                ])
                task.cancel(with: .normalClosure, reason: nil)
            } else {
                task.receive { result in
                    switch result {
                    case .success(let response):
                        switch response {
                        case .string(let responseText):
                            if let responseJson = try? JSONSerialization.jsonObject(with: responseText.data(using: .utf8)!, options: []) as? JSON {
                                self.completeContextRequest(for: context, withMessage: ["responseJson": responseJson])
                            } else {
                            self.completeContextRequest(for: context, withMessage: ["responseText": responseText])
                            }
                        case .data(let responseData):
                            self.completeContextRequest(for: context, withMessage: ["responseData": responseData.count])
                        @unknown default:
                            self.completeContextRequest(for: context, withMessage: ["unknown response": true])
                        }
                    case .failure(let error):
                        self.completeContextRequest(for: context, withMessage: ["send failed": error.localizedDescription])
                    }
                    task.cancel(with: .normalClosure, reason: nil)
                }
            }
        }
    }
}
