//
//  ScratchLog.swift
//  Scratch Link Safari Extension
//
//  Created by Christopher Willis-Ford on 9/30/22.
//

import Foundation
import os.log

class ScratchLog {
    public static let logSubsystem = "org.scratch.link";
    public static let logCategory = "safari-extension";

    public static let shared = ScratchLog()

    public static func log(_ message: StaticString, type: OSLogType = .default, _ args: CVarArg...) {
        shared.doLog(message, type: type, args)
    }

    private let scratchLog: OSLog

    private init() {
        self.scratchLog = OSLog(subsystem: ScratchLog.logSubsystem, category: ScratchLog.logCategory)
    }

    private func doLog(_ message: StaticString, type: OSLogType = .default, _ args: CVarArg...) {
        os_log(message, log: scratchLog, type: type, args)
    }
}
