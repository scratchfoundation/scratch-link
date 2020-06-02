import Foundation
import IOBluetooth
import PerfectWebSockets

class BTSession: Session, IOBluetoothRFCOMMChannelDelegate, IOBluetoothDeviceInquiryDelegate {
    private var inquiry: IOBluetoothDeviceInquiry
    private var connectedChannel: IOBluetoothRFCOMMChannel?
    private let rfcommQueue = DispatchQueue(label: "ScratchLink.BTSession.rfcommQueue")
    private var state: SessionState = .initial
    private var ouiPrefix: String

    enum SessionState {
        case initial
        case discovery
        case connected
    }

    required init(withSocket webSocket: WebSocket) throws {
        ouiPrefix = ""
        inquiry = IOBluetoothDeviceInquiry(delegate: nil)
        try super.init(withSocket: webSocket)
        inquiry.delegate = self
    }

    override func didReceiveCall(_ method: String, withParams params: [String: Any],
                                 completion: @escaping JSONRPCCompletionHandler) throws {
        switch state {
        case .initial:
            if method == "discover" {
                if let major = params["majorDeviceClass"] as? UInt, let minor = params["minorDeviceClass"] as? UInt {
                    if let prefix = params["ouiPrefix"] as? String { self.ouiPrefix = prefix }
                    state = .discovery
                    discover(inMajorDeviceClass: major, inMinorDeviceClass: minor, completion: completion)
                } else {
                    completion(nil, JSONRPCError.invalidParams(data: "majorDeviceClass and minorDeviceClass required"))
                }
                return
            }
        case .discovery:
            if method == "connect" {
                if let peripheralId = params["peripheralId"] as? String {
                    connect(toDevice: peripheralId, completion: completion)
                } else {
                    completion(nil, JSONRPCError.invalidParams(data: "peripheralId required"))
                }
                return
            }
        case .connected:
            if method == "send" {
                if connectedChannel == nil || connectedChannel?.isOpen() == false {
                    completion(nil, JSONRPCError.invalidRequest(data: "No peripheral connected"))
                } else {
                    let decodedMessage = try EncodingHelpers.decodeBuffer(fromJSON: params)
                    sendMessage(decodedMessage, completion: completion)
                }
                return
            }
        }
        // unrecognized method in this state: pass to base class
        try super.didReceiveCall(method, withParams: params, completion: completion)
    }

    override func sessionWasClosed() {
        super.sessionWasClosed()
        inquiry.stop()
        connectedChannel?.setDelegate(nil)
        connectedChannel?.close()
        connectedChannel = nil
    }

    func discover(inMajorDeviceClass major: UInt, inMinorDeviceClass minor: UInt,
                  completion: @escaping JSONRPCCompletionHandler) {
        // see https://www.bluetooth.com/specifications/assigned-numbers/baseband for available device classes
        // LEGO EV3 is major class toy (8), minor class robot (1)
        inquiry.setSearchCriteria(BluetoothServiceClassMajor(kBluetoothServiceClassMajorAny),
                                   majorDeviceClass: BluetoothDeviceClassMajor(major),
                                   minorDeviceClass: BluetoothDeviceClassMinor(minor))
        inquiry.inquiryLength = 20
        inquiry.updateNewDeviceNames = true
        let inquiryStatus = inquiry.start()
        let error = inquiryStatus != kIOReturnSuccess ?
            JSONRPCError.serverError(code: -32500, data: "Device inquiry failed to start") : nil

        completion(nil, error)
    }

    func connect(toDevice deviceId: String,
                 completion: @escaping JSONRPCCompletionHandler) {
        inquiry.stop()
        let availableDevices = inquiry.foundDevices() as? [IOBluetoothDevice]
        if let device = availableDevices?.first(where: {$0.addressString == deviceId}) {
            rfcommQueue.async {
                let connectionResult = device.openRFCOMMChannelSync(&self.connectedChannel,
                     withChannelID: 1,
                     delegate: self)
                if connectionResult != kIOReturnSuccess {
                    completion(nil, JSONRPCError.serverError(code: -32500, data:
                        "Connection process could not start or channel was not found"))
                } else {
                    self.state = .connected
                    completion(nil, nil)
                }

                // run loop specifically for this device: necessary to get delegate callbacks
                var nextCheck = Date()
                while (self.connectedChannel?.isOpen() ?? false) &&
                              RunLoop.current.run(mode: .default, before: nextCheck) {
                    nextCheck.addTimeInterval(0.5)
                }
                print("RFCOMM run loop exited")
                do {
                    try self.sendErrorNotification(JSONRPCError.applicationError(data: "RFCOMM run loop exited"))
                } catch {
                    print("Failed to inform client that RFCOMM loop exited: \(String(describing: error))")
                }
                self.sessionWasClosed()
            }
        } else {
            completion(nil, JSONRPCError.invalidRequest(data: "Device \(deviceId) not available for connection"))
            inquiry.start()
        }
    }

    func sendMessage(_ message: Data,
                     completion: @escaping JSONRPCCompletionHandler) {
        guard let connectedChannel = connectedChannel else {
            completion(nil, JSONRPCError.serverError(code: -32500, data: "No peripheral connected"))
            return
        }
        var data = message
        let mtu = connectedChannel.getMTU()
        let maxMessageSize = Int(mtu)
        if message.count <= maxMessageSize {
            DispatchQueue.global(qos: .userInitiated).async {
                let messageResult = data.withUnsafeMutableBytes { (bytes: UnsafeMutableRawBufferPointer) in
                    return connectedChannel.writeSync(bytes.baseAddress, length: UInt16(bytes.count))
                }
                if messageResult != kIOReturnSuccess {
                    completion(nil, JSONRPCError.serverError(code: -32500, data: "Failed to send message"))
                } else {
                    completion(message.count, nil)
                }
            }
        } else {
            // taken from https://stackoverflow.com/a/38156873
            let chunks = stride(from: 0, to: data.count, by: maxMessageSize).map {
                Array(data[$0..<min($0 + maxMessageSize, data.count)])
            }

            DispatchQueue.global(qos: .userInitiated).async {
                var succeeded = 0
                var bytesSent = 0
                for chunk in chunks {
                    var mutableChunk = chunk
                    let intermediateResult = connectedChannel.writeSync(&mutableChunk, length: UInt16(chunk.count))
                    succeeded += Int(intermediateResult)
                    if intermediateResult == kIOReturnSuccess {
                        bytesSent += chunk.count
                    }
                }
                completion(bytesSent, succeeded == 0 ? nil : JSONRPCError.serverError(code: -32500,
                      data: "Failed to send message"))
            }
        }
    }

    /*
     * IOBluetoothDeviceInquiryDelegate implementation
     */

    func deviceInquiryDeviceFound(_ sender: IOBluetoothDeviceInquiry!, device: IOBluetoothDevice!) {
        if(device.addressString.hasPrefix(self.ouiPrefix)) {
            let peripheralData: [String: Any] = [
                "peripheralId": device.addressString as Any,
                "name": device.name as Any,

                // BT on Mac can't get a real RSSI without connecting (device.rawRSSI() is +127 unless connected)
                "rssi": RSSI.unsupported.rawValue as Any
            ]
            sendRemoteRequest("didDiscoverPeripheral", withParams: peripheralData)
        }
    }

    func deviceInquiryComplete(_ sender: IOBluetoothDeviceInquiry!, error: IOReturn, aborted: Bool) {
        print("Inquiry finished")
        if !aborted {
            sender.start()
        }
    }

    func deviceInquiryStarted(_ sender: IOBluetoothDeviceInquiry!) {
        print("Inquiry started")
    }

    func deviceInquiryUpdatingDeviceNamesStarted(_ sender: IOBluetoothDeviceInquiry!, devicesRemaining: UInt32) {
        print("name updates remaining: \(devicesRemaining)")
    }

    func deviceInquiryDeviceNameUpdated(
        _ sender: IOBluetoothDeviceInquiry!, device: IOBluetoothDevice!, devicesRemaining: UInt32) {
        print("name updated: \(String(describing: device.name))")
    }

    /*
     * IOBluetoothRFCOMMChannelDelegate implementation
     */

    func rfcommChannelData(_ rfcommChannel: IOBluetoothRFCOMMChannel!,
                           data dataPointer: UnsafeMutableRawPointer!,
                           length dataLength: Int) {
        let value = Data(bytesNoCopy: dataPointer, count: dataLength, deallocator: .none)
        guard let responseData = EncodingHelpers.encodeBuffer(value, withEncoding: "base64") else {
            // TODO: should this send a default or error message so the client knows something's wrong?
            print("failed to encode RFCOMM data")
            return
        }
        sendRemoteRequest("didReceiveMessage", withParams: responseData)
    }
}
