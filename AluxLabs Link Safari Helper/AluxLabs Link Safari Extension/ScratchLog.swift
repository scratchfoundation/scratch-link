//
//  AluxLabsLog.swift
//  AluxLabs Link Safari Extension
//
//  Created by Christopher Willis-Ford on 9/30/22.
//

import Foundation
import os.log

class AluxLabsLog {
    public static let logSubsystem = "com.aluxlabs.link";
    public static let logCategory = "safari-extension";

    public static let shared = AluxLabsLog()

    public static func log(_ message: StaticString, type: OSLogType = .default, _ args: CVarArg...) {
        shared.doLog(message, type: type, args)
    }

    private let aluxLabsLog: OSLog

    private init() {
        self.aluxLabsLog = OSLog(subsystem: AluxLabsLog.logSubsystem, category: AluxLabsLog.logCategory)
    }

    private func doLog(_ message: StaticString, type: OSLogType = .default, _ args: CVarArg...) {
        os_log(message, log: aluxLabsLog, type: type, args)
    }
}
