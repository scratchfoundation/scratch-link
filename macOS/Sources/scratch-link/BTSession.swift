import Foundation
import IOBluetooth
import Swifter

class BTSession: Session, IOBluetoothRFCOMMChannelDelegate, IOBluetoothDeviceInquiryDelegate {
    private var inquiry: IOBluetoothDeviceInquiry
    private var connectedChannel: IOBluetoothRFCOMMChannel?
    private let rfcommQueue = DispatchQueue(label: "ScratchLink.BTSession.rfcommQueue")
    private var state: SessionState = .Initial
    
    enum SessionState {
        case Initial
        case Discovery
        case Connected
    }
    
    required init(withSocket wss: WebSocketSession) {
        inquiry = IOBluetoothDeviceInquiry(delegate: nil)
        super.init(withSocket: wss)
        inquiry.delegate = self
    }
    
    override func didReceiveCall(_ method: String, withParams params: [String:Any],
                                 completion: @escaping JSONRPCCompletionHandler) throws {
        switch state {
        case .Initial:
            if method != "discover" {
                completion(nil, JSONRPCError.MethodNotFound(data: "Cannot call \(method) in initial state"))
                return;
            }
            if let major = params["majorDeviceClass"] as? UInt, let minor = params["minorDeviceClass"] as? UInt {
                state = .Discovery
                discover(inMajorDeviceClass: major, inMinorDeviceClass: minor, completion: completion)
            } else {
                completion(nil, JSONRPCError.InvalidParams(data: "majorDeviceClass and minorDeviceClass required"))
            }
        case .Discovery:
            if method != "connect" {
                completion(nil, JSONRPCError.MethodNotFound(data: "Cannot call \(method) in discovery state"))
                return
            }
            if let peripheralId = params["peripheralId"] as? String {
                connect(toDevice: peripheralId, completion: completion)
            } else {
                completion(nil, JSONRPCError.InvalidParams(data: "peripheralId required"))
            }
        case .Connected:
            if method != "send" {
                completion(nil, JSONRPCError.MethodNotFound(data: "Cannot call \(method) in connected state"))
            }
            if connectedChannel == nil || connectedChannel?.isOpen() == false {
                completion(nil, JSONRPCError.InvalidRequest(data: "No peripheral connected"))
            } else if let message = params["message"] as? String, let encoding = params["encoding"] as? String {
                if encoding == "base64" {
                    let decodedMessage = base64Decode(message)
                    if decodedMessage.count > 0 {
                        sendMessage(decodedMessage, completion: completion)
                    } else {
                        completion(nil, JSONRPCError.InvalidParams(data: "Invalid base64 string"))
                    }
                } else {
                    completion(nil, JSONRPCError.InvalidParams(data: "Unsupported encoding"))
                }
            }
        }
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
            JSONRPCError.ServerError(code: -32500, data: "Device inquiry failed to start") : nil
        
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
                if (connectionResult != kIOReturnSuccess) {
                    completion(nil, JSONRPCError.ServerError(code: -32500, data:
                        "Connection process could not start or channel was not found"))
                } else {
                    self.state = .Connected
                    completion(nil, nil)
                }

                // runloop specifically for this device: necessary to get delegate callbacks
                var nextCheck = Date()
                while (self.connectedChannel?.isOpen() ?? false) &&
                              RunLoop.current.run(mode: .defaultRunLoopMode, before: nextCheck) {
                    nextCheck.addTimeInterval(0.5)
                }
                print("RFCOMM run loop exited")
            }
        } else {
            completion(nil, JSONRPCError.InvalidRequest(data: "Device \(deviceId) not available for connection"))
            inquiry.start()
        }
    }
    
    func sendMessage(_ message: [UInt8],
                     completion: @escaping JSONRPCCompletionHandler) {
        guard let connectedChannel = connectedChannel else {
            completion(nil, JSONRPCError.ServerError(code: -32500, data: "No peripheral connected"))
            return
        }
        var data = message
        let mtu = connectedChannel.getMTU()
        let maxMessageSize = Int(mtu)
        if message.count <= maxMessageSize {
            DispatchQueue.global(qos: .userInitiated).async {
                let messageResult = connectedChannel.writeSync(&data, length: UInt16(message.count))
                if messageResult != kIOReturnSuccess {
                    completion(nil, JSONRPCError.ServerError(code: -32500, data: "Failed to send message"))
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
                completion(bytesSent, succeeded == 0 ? nil : JSONRPCError.ServerError(code: -32500,
                      data: "Failed to send message"))
            }
        }
    }
    
    /*
     * IOBluetoothDeviceInquiryDelegate implementation
     */
    
    func deviceInquiryDeviceFound(_ sender: IOBluetoothDeviceInquiry!, device: IOBluetoothDevice!) {
        let peripheralData: [String: Any] = [
            "peripheralId": device.addressString,
            "name": device.name,
            "rssi": device.rawRSSI()
        ]
        sendRemoteRequest("didDiscoverPeripheral", withParams: peripheralData)
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
    
    func deviceInquiryDeviceNameUpdated(_ sender: IOBluetoothDeviceInquiry!, device: IOBluetoothDevice!, devicesRemaining: UInt32) {
        print("name updated: \(device.name)")
    }
    
    /*
     * IOBluetoothRFCOMMChannelDelegate implementation
     */
    
    func rfcommChannelData(_ rfcommChannel: IOBluetoothRFCOMMChannel!,
                           data dataPointer: UnsafeMutableRawPointer!,
                           length dataLength: Int) {
        let encodedMessage = base64Encode(dataPointer, length: dataLength)
        let responseData: [String: Any] = [
            "message": encodedMessage,
            "encoding": "base64"
        ]
        sendRemoteRequest("didReceiveMessage", withParams: responseData)
    }
    
    /*
     * Helper methods
     */
    
    func base64Encode(_ buffer: UnsafeMutableRawPointer, length: Int) -> String {
        var array: [UInt8] = Array(repeating: 0, count: length)
        for index in 0..<length {
            array[index] = buffer.load(fromByteOffset: index, as: UInt8.self)
        }
        let data = Data(array)
        return data.base64EncodedString(options: NSData.Base64EncodingOptions(rawValue: 0))
    }
    
    func base64Decode(_ base64String: String) -> [UInt8] {
        if let data = Data(base64Encoded: base64String) {
            return [UInt8](data)
        }
        return []
    }
}
