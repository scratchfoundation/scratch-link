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
let myBundleIdentifier = Bundle.main.bundleIdentifier ?? "nil"

var sessionMap = Dictionary<UInt32, SessionDelegate>()

func getUnusedSessionID() -> UInt32 {
    while true {
        let proposedID = arc4random_uniform(UInt32.max)
        if sessionMap[proposedID] == nil {
            return proposedID
        }
    }
}

// WARNING: This class is reinstantiated for each request from the browser!
// Any information that must be retained across requests, like the session map, must be stored outside this class.
class SafariWebExtensionHandler: NSObject, NSExtensionRequestHandling {

    typealias MethodHandler = (
        _ sessionID: UInt32,
        _ method: String,
        _ params: JSON?,
        _ responseID: UInt32?,
        _ completion: @escaping (JSONResult) -> Void
    ) -> Void
    
	func beginRequest(with context: NSExtensionContext) {
        guard
            let message = getMessage(from: context),
            let method = message["method"] as? String,
            let sessionID = (
                method == "open" ? getUnusedSessionID() : message["session"] as? UInt32
            )
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

        handler(sessionID, method, params, id) { result in
            if let id = id {
                var response: JSON = [
                    "jsonrpc": "2.0",
                    "session": sessionID,
                    "id": id
                ]
                switch result {
                case .success(let result):
                    response["result"] = result
                    if method == "open" {
                        response["session"] = result
                    }
                case .failure(let error):
                    response["error"] = error
                }
                self.completeContextRequest(for: context, withMessage: response)
            }
        }
    }
    
    func getMessage(from context: NSExtensionContext) -> JSON? {
        guard let item = context.inputItems[0] as? NSExtensionItem else {
            return nil
        }
        return item.userInfo?[SFExtensionMessageKey] as? JSON
    }

    func openSession(with sessionID: UInt32, method: String, params: JSON?, id: UInt32?, completion: @escaping (JSONResult) -> Void) -> Void {

        guard let sessionType = params?["type"] as? String else {
            return completion(.failure("call to 'open' with bad or missing 'type' parameter"))
        }
        
        let session = SessionDelegate.open(sessionID: sessionID, sessionType: sessionType, completion: completion)
        sessionMap[sessionID] = session

        session.setReceiver { result in
            SFSafariApplication.dispatchMessage(withName: "native dispatchMessage", toExtensionWithIdentifier: myBundleIdentifier, userInfo: ["result": result], completionHandler: nil)
        }
    }
    
    func sendMessage(with sessionID: UInt32, method: String, params: JSON?, id: UInt32?, completion: @escaping (JSONResult) -> Void) -> Void {
        guard let session = sessionMap[sessionID] else {
            return completion(.failure("attempt to send message on unrecognized session"))
        }
        guard let message = params else {
            return completion(.failure("attempt to send empty message"))
        }
        session.send(messageJSON: message) { result in
            return completion(result)
        }
    }
    
    func closeSession(with sessionID: UInt32, method: String, params: JSON?, id: UInt32?, completion: @escaping (JSONResult) -> Void) -> Void {
        guard let session = sessionMap[sessionID] else {
            return completion(.failure("attempt to close unrecognized session"))
        }
        session.close(completion: completion)
    }
    
    func unrecognizedMethod(with sessionID: UInt32, method: String, params: JSON?, id: UInt32?, completion: @escaping (JSONResult) -> Void) -> Void {
        os_log(.error, "Ignoring call to unrecognized method: %@", method)
        return completion(.failure("unrecognized method"))
    }
    
    func completeContextRequest(for context: NSExtensionContext, withMessage message: JSON?, completionHandler: ((Bool) -> Void)? = nil) {
        let response = NSExtensionItem()
        if let message = message {
            response.userInfo = [ SFExtensionMessageKey: message ]
        }
        context.completeRequest(returningItems: [response], completionHandler: completionHandler)
    }
}
