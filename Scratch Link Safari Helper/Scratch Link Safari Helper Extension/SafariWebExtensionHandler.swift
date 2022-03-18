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

class SafariWebExtensionHandler: NSObject, NSExtensionRequestHandling {

	func beginRequest(with context: NSExtensionContext) {
        let item = context.inputItems[0] as! NSExtensionItem
        let message = item.userInfo?[SFExtensionMessageKey]
        os_log(.default, "Received message from browser.runtime.sendNativeMessage: %@", message as! CVarArg)

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
                            if let responseJson = try? JSONSerialization.jsonObject(with: responseText.data(using: .utf8)!, options: []) {
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
    
    func completeContextRequest(for context: NSExtensionContext, withMessage message: [AnyHashable: Any], completionHandler: ((Bool) -> Void)? = nil) {
        let response = NSExtensionItem()
        response.userInfo = [ SFExtensionMessageKey: message ]
        context.completeRequest(returningItems: [response], completionHandler: completionHandler)
    }
}
