//
//  bt.swift
//  SDM
//
//  Created by LabVIEW on 4/6/18.
//  Copyright © 2018 NI. All rights reserved.
//

import Foundation
import IOBluetooth

class SessionStub: NSObject {
    private let id: Int
    init(id: Int) {
        self.id = id
        super.init()
    }
    func writeText(_ text: String) {
        print("Session \(id) received \(text)")
    }
}

class ScratchBT: NSObject, IOBluetoothRFCOMMChannelDelegate, IOBluetoothDeviceInquiryDelegate {
    private var inquiry: IOBluetoothDeviceInquiry?
    private var connectedChannel: IOBluetoothRFCOMMChannel?
    private var sequenceId = 0
    private var refcon = 0
    private var wss: SessionStub
    
    init(forSession session: SessionStub) {
        wss = session
        super.init()
        inquiry = IOBluetoothDeviceInquiry(delegate: self)
    }
    
    func scan(inMajorDeviceClass major: Int, inMinorDeviceClass minor: Int) {
        // TODO validate major & minor device types
        inquiry?.setSearchCriteria(BluetoothServiceClassMajor(kBluetoothServiceClassMajorAny),
                                   majorDeviceClass: BluetoothDeviceClassMajor(major),
                                   minorDeviceClass: BluetoothDeviceClassMinor(minor))
        inquiry?.inquiryLength = 30
        inquiry?.updateNewDeviceNames = true
        let inquiryStatus = inquiry?.start()
        sendWSSResponse(returnCode: inquiryStatus)
    }
    
    func connect(toDevice deviceId: String) {
        inquiry?.stop()
        let availableDevices = inquiry?.foundDevices()
        var deviceNotAvailable = true
        for device in availableDevices! {
            let bluetoothDevice = device as! IOBluetoothDevice
            if (bluetoothDevice.addressString == deviceId) {
                let connectionResult =
                    bluetoothDevice.openRFCOMMChannelAsync(&connectedChannel, withChannelID: 1, delegate: self)
                sendWSSResponse(returnCode: connectionResult)
                deviceNotAvailable = false
                break
            }
        }
        if deviceNotAvailable {
            // we cannot connect if we do not know about the device
            sendWSSResponse(returnCode: IOReturn(1))
        }
    }
    
    func disconnect(fromDevice deviceId: String) {
        let bluetoothDevice = connectedChannel?.getDevice()
        if (bluetoothDevice?.addressString == deviceId) {
            let disconnectionResult = connectedChannel?.close()
            // release the connected channel and reset reference counter so we can reuse it
            connectedChannel = nil
            refcon = 0
            sendWSSResponse(returnCode: disconnectionResult)
        } else {
            sendWSSResponse(returnCode: IOReturn(1))
        }
    }
    
    func sendMessage(toDevice deviceId: String, message: [UInt8]) {
        let bluetoothDevice = connectedChannel?.getDevice()
        if connectedChannel?.isOpen() == false || bluetoothDevice?.addressString != deviceId {
            sendWSSResponse(returnCode: IOReturn(1))
            return
        }
        
        var data = message
        let mtu = connectedChannel?.getMTU()
        if message.count <= Int(mtu!) {
            let messageResult = connectedChannel?.writeAsync(&data, length: UInt16(message.count), refcon: &refcon)
            if (messageResult != kIOReturnSuccess) {
                sendWSSResponse(returnCode: messageResult)
            } // a success here may be false positive, handle in callback
        } else {
            // split it up and send in chunks
        }
    }
    
    /*
     * IOBluetoothDeviceInquiryDelegate implementation
     */
    
    func deviceInquiryDeviceFound(_ sender: IOBluetoothDeviceInquiry!, device: IOBluetoothDevice!) {
        do {
            let response: [String: Any] = [
                "jsonrpc": "2.0",
                "method": "didDiscoverPeripheral",
                "params": [
                    "uuid": device.addressString,
                    "name": device.name,
                    "rssi": device.rawRSSI()
                ]
            ]
            let responseData = try JSONSerialization.data(withJSONObject: response)
            if let responseString = String(bytes: responseData, encoding: .utf8) {
                wss.writeText(responseString)
            }
        } catch {
            print("Error handling discovered device: \(error)")
        }
    }
    
    func deviceInquiryComplete(_ sender: IOBluetoothDeviceInquiry!, error: IOReturn, aborted: Bool) {
        print("inquiry complete: aborted = \(aborted)")
    }
    
    /*
     * IOBluetoothRFCOMMChannelDelegate implementation
     */
    
    func rfcommChannelOpenComplete(_ rfcommChannel: IOBluetoothRFCOMMChannel!, status error: IOReturn) {
        wss.writeText("connected with error \(error)")
    }
    
    func rfcommChannelData(_ rfcommChannel: IOBluetoothRFCOMMChannel!,
                           data dataPointer: UnsafeMutableRawPointer!,
                           length dataLength: Int) {
        let device = rfcommChannel.getDevice()
        let encodedMessage = base64Encode(dataPointer, length: dataLength)
        do {
            let response: [String: Any] = [
                "jsonrpc": "2.0",
                "method": "didDiscoverMessage",
                "params": [
                    "uuid": device?.addressString,
                    "message": encodedMessage,
                    "encoding": "base64"
                ]
            ]
            let responseData = try JSONSerialization.data(withJSONObject: response)
            if let responseString = String(bytes: responseData, encoding: .utf8) {
                wss.writeText(responseString)
            }
        } catch {
            print("Error receiving message: \(error)")
        }
    }
    
    func rfcommChannelWriteComplete(_ rfcommChannel: IOBluetoothRFCOMMChannel!,
                                    refcon: UnsafeMutableRawPointer!,
                                    status error: IOReturn) {
        sendWSSResponse(returnCode: error) // include number of bytes sent to device
    }
    
    /*
     * Helper methods
     */
    
    func sendWSSResponse(returnCode: IOReturn?) {
        do {
            if (returnCode == kIOReturnSuccess) {
                let response: [String: Any] = [
                    "jsonrpc": "2.0",
                    "id": sequenceId,
                    "result": returnCode
                ]
                let responseData = try JSONSerialization.data(withJSONObject: response)
                if let responseString = String(bytes: responseData, encoding: .utf8) {
                    wss.writeText(responseString)
                }
                sequenceId += 1
            } else {
                let response: [String: Any] = [
                    "jsonrpc": "2.0",
                    "id": sequenceId,
                    "error": returnCode
                ]
                let responseData = try JSONSerialization.data(withJSONObject: response)
                if let responseString = String(bytes: responseData, encoding: .utf8) {
                    wss.writeText(responseString)
                }
                sequenceId += 1
            }
        } catch {
            print("Error sending WSS response: \(error)")
        }
    }
    
    func base64Encode(_ buffer: UnsafeMutableRawPointer, length: Int) -> String {
        var array: [UInt8] = Array(repeating: 0, count: length)
        for index in 0..<length {
            array[index] = buffer.load(fromByteOffset: index, as: UInt8.self)
        }
        let data = Data(array)
        return data.base64EncodedString(options: NSData.Base64EncodingOptions(rawValue: 0))
    }
}
