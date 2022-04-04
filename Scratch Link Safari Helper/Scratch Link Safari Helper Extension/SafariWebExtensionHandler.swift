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
        _ params: JSONObject?,
        _ responseID: UInt32?,
        _ completion: @escaping (JSONValueResult) -> Void
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
        let params = message["params"] as? JSONObject
        
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
                var response: JSONObject = [
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
    
    func getMessage(from context: NSExtensionContext) -> JSONObject? {
        guard let item = context.inputItems[0] as? NSExtensionItem else {
            return nil
        }
        return item.userInfo?[SFExtensionMessageKey] as? JSONObject
    }

    func openSession(with sessionID: UInt32, method: String, params: JSONObject?, id: UInt32?, completion: @escaping (JSONValueResult) -> Void) -> Void {

        guard let sessionType = params?["type"] as? String else {
            return completion(.failure("call to 'open' with bad or missing 'type' parameter"))
        }
        
        let session = SessionDelegate.open(sessionID: sessionID, sessionType: sessionType, completion: completion)
        sessionMap[sessionID] = session

        session.setReceiver { message in
            let messageBundle: [String: Any] = [
                "session": sessionID,
                "message": message
            ]
            SFSafariApplication.dispatchMessage(withName: "native dispatchMessage", toExtensionWithIdentifier: myBundleIdentifier, userInfo: messageBundle, completionHandler: nil)
        }
    }

    func sendMessage(with sessionID: UInt32, method: String, params: JSONObject?, id: UInt32?, completion: @escaping (JSONValueResult) -> Void) -> Void {
        guard let session = sessionMap[sessionID] else {
            return completion(.failure("attempt to send message on unrecognized session"))
        }
        guard let message = params else {
            return completion(.failure("attempt to send empty message"))
        }
        session.send(messageJSON: message) { result in
            switch result {
            case .success(let value):
                return completion(.success(value))
            case .failure(let error):
                return completion(.failure(error))
            }
        }
    }
    
    func closeSession(with sessionID: UInt32, method: String, params: JSONObject?, id: UInt32?, completion: @escaping (JSONValueResult) -> Void) -> Void {
        guard let session = sessionMap[sessionID] else {
            return completion(.failure("attempt to close unrecognized session"))
        }
        session.close(completion: completion)
    }
    
    func unrecognizedMethod(with sessionID: UInt32, method: String, params: JSONObject?, id: UInt32?, completion: @escaping (JSONValueResult) -> Void) -> Void {
        os_log(.error, "Ignoring call to unrecognized method: %@", method)
        return completion(.failure("unrecognized method"))
    }
    
    func completeContextRequest(for context: NSExtensionContext, withMessage message: JSONObject?, completionHandler: ((Bool) -> Void)? = nil) {
        let response = NSExtensionItem()
        if let message = message {
            response.userInfo = [ SFExtensionMessageKey: message ]
        }
        context.completeRequest(returningItems: [response], completionHandler: completionHandler)
    }
}