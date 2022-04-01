//
//  JSONPromise.swift
//  Scratch Link Safari Helper Extension
//
//  Created by Christopher Willis-Ford on 3/30/22.
//

import Foundation
import Combine

typealias JSON = Dictionary<String, AnyHashable?>

enum JSONResult {
    case success(AnyHashable? = nil)
    case failure(AnyHashable)
}
