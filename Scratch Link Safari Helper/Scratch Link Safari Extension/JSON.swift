//
//  JSONPromise.swift
//  Scratch Link Safari Extension
//
//  Created by Christopher Willis-Ford on 3/30/22.
//

import Foundation

typealias JSONValue = AnyHashable
typealias JSONObject = Dictionary<String, JSONValue?>

enum JSONObjectResult {
    case success(JSONObject? = nil)
    case failure(JSONValue)
}

enum JSONValueResult {
    case success(JSONValue? = nil)
    case failure(JSONValue)
}
