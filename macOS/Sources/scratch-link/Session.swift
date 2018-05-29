import Foundation
import Swifter

class Session {
    typealias RequestID = Int
    typealias JSONRPCCompletionHandler = (_ result: Any?, _ error: JSONRPCError?) -> Void

    private let wss: WebSocketSession
    private var nextId: RequestID
    private var completionHandlers: [RequestID:JSONRPCCompletionHandler]

    required init(withSocket wss: WebSocketSession) {
        self.wss = wss
        self.nextId = 0
        self.completionHandlers = [RequestID:JSONRPCCompletionHandler]()
    }

    // Override this to clean up session-specific resources, if any.
    func sessionWasClosed() {
        if completionHandlers.count > 0 {
            print("Warning: session was closed with \(completionHandlers.count) pending requests")
            for (_, completionHandler) in completionHandlers {
                completionHandler(nil, JSONRPCError.InternalError(data: "Session closed"))
            }
        }
    }

    // Override this to handle received RPC requests & notifications.
    // Call the completion handler when done with a request:
    // - pass your call's "return value" (or nil) as `result` on success
    // - pass an instance of `JSONRPCError` for `error` on failure
    // You may also throw a `JSONRPCError` (or any other `Error`) iff it is encountered synchronously.
    func didReceiveCall(_ method: String, withParams params: [String:Any],
                        completion: @escaping JSONRPCCompletionHandler) throws {
        preconditionFailure("Must override didReceiveCall")
    }

    // Pass nil for the completion handler to send a Notification
    // Note that the closure is automatically @escaping by virtue of being part of an aggregate (Optional)
    func sendRemoteRequest(_ method: String, withParams params: [String:Any]? = nil,
                           completion: JSONRPCCompletionHandler? = nil) {
        var request: [String: Any?] = [
            "jsonrpc": "2.0",
            "method": method
        ]

        if params != nil {
            request["params"] = params
        }

        if completion != nil {
            let requestId = getNextId()
            request["id"] = requestId
            completionHandlers[requestId] = completion
        }

        do {
            let requestData = try JSONSerialization.data(withJSONObject: request)
            if let requestText = String(bytes: requestData, encoding: .utf8) {
                wss.writeText(requestText)
            } else {
                print("Error encoding request text. Request: \(request)")
            }
        } catch {
            print("Error serializing request JSON: \(error)")
            print("Request was: \(request)")
        }
    }

    func didReceiveData(_ data: Data, completion: @escaping (_ jsonResponseData: Data?) -> Void) {

        var responseId: Any = NSNull() // initialize with null until we try to read the real ID

        func makeResponse(_ result: Any?, _ error: JSONRPCError?) -> [String: Any] {
            var response: [String: Any] = [
                "jsonrpc": "2.0"
            ]
            response["id"] = responseId
            if let error = error {
                var jsonError: [String: Any] = [
                    "code": error.code,
                    "message": error.message
                ]
                if let data = error.data {
                    jsonError["data"] = data
                }
                response["error"] = jsonError
            } else {
                // If there's no error then we must include this as a success flag, even if the value is null
                response["result"] = result ?? NSNull()
            }

            return response
        }

        func sendResponse(_ result: Any?, _ error: JSONRPCError?) {
            do {
                let response = makeResponse(result, error)
                let jsonData = try JSONSerialization.data(withJSONObject: response)
                completion(jsonData)
            } catch let firstError {
                do {
                    let errorResponse = makeResponse(nil, JSONRPCError(
                            code: 2, message: "Could not encode response", data: String(describing: firstError)))
                    let jsonData = try JSONSerialization.data(withJSONObject: errorResponse)
                    completion(jsonData)
                } catch let secondError {
                    print("Failure to report failure to encode!")
                    print("Initial error: \(String(describing: firstError))")
                    print("Secondary error: \(String(describing: secondError))")
                }
            }
        }

        do {
            guard let json = try JSONSerialization.jsonObject(with: data, options: []) as? [String: Any] else {
                throw JSONRPCError.ParseError(data: "unrecognized message structure")
            }

            // do this as early as possible so that error responses can include it. If it's not present, use null.
            responseId = json["id"] ?? NSNull()

            // property "jsonrpc" must be exactly "2.0"
            if json["jsonrpc"] as? String != "2.0" {
                throw JSONRPCError.InvalidRequest(data: "unrecognized JSON-RPC version string")
            }

            if json.keys.contains("method") {
                try didReceiveRequest(json, completion: sendResponse)
            } else if json.keys.contains("result") || json.keys.contains("error") {
                try didReceiveResponse(json)
            } else {
                throw JSONRPCError.InvalidRequest(data: "message is neither request nor response")
            }
        } catch let error where error is JSONRPCError {
            sendResponse(nil, error as? JSONRPCError)
        } catch {
            sendResponse(nil, JSONRPCError(
                    code: 1, message: "Unhandled error encountered during call", data: String(describing: error)))
        }
    }

    func didReceiveRequest(_ json: [String: Any], completion: @escaping JSONRPCCompletionHandler) throws {
        guard let method = json["method"] as? String else {
            throw JSONRPCError.InvalidRequest(data: "method value missing or not a string")
        }

        // optional: dictionary of parameters by name
        let params: [String: Any] = (json["params"] as? [String: Any]) ?? [String: Any]()

        // On success, this will call makeResponse with a result
        try didReceiveCall(method, withParams: params, completion: completion)
    }

    func didReceiveResponse(_ json: [String: Any]) throws {
        guard let id = json["id"] as? RequestID else {
            throw JSONRPCError.InvalidRequest(data: "response ID value missing or wrong type")
        }

        guard let completionHandler = completionHandlers.removeValue(forKey: id) else {
            throw JSONRPCError.InvalidRequest(data: "response ID does not correspond to any open request")
        }

        if let errorJSON = json["error"] as? [String:Any] {
            let error = JSONRPCError(fromJSON: errorJSON)
            completionHandler(nil, error)
        } else {
            let rawResult = json["result"]
            let result = (rawResult is NSNull ? nil : rawResult)
            completionHandler(result, nil)
        }
    }

    private func getNextId() -> RequestID {
        let result = self.nextId;
        self.nextId += 1;
        return result;
    }
}
