//
//  main.swift
//  SDM
//
//  Created by LabVIEW on 3/26/18.
//  Copyright © 2018 NI. All rights reserved.
//

import Foundation
import IOBluetooth
import IOBluetoothUI

// Store this device so we can connect to it later
var myDevice : IOBluetoothDevice!

// A bunch of different commands to send to the brick
var beepCommand : [UInt8] = [0x0f, 0x00, 0x00, 0x00, 0x80, 0x00, 0x00, 0x94, 0x01, 0x81, 0x02, 0x82, 0xe8, 0x03, 0x82, 0xe8, 0x03]
var driveCommand : [UInt8] = [0x12, 0x00, 0x00, 0x00, 0x80, 0x00, 0x00, 0xae, 0x00, 0x06, 0x81, 0x32, 0x00, 0x82, 0x84, 0x03, 0x82, 0xb4, 0x00, 0x01]
var sensorReadCommand : [UInt8] = [0x0d, 0x00, 0x00, 0x00, 0x00, 0x04, 0x00, 0x99, 0x1d, 0x00, 0x00, 0x00, 0x02, 0x01, 0x60]
var sensor2ReadCommand : [UInt8] = [0x0d, 0x00, 0x00, 0x00, 0x00, 0x04, 0x00, 0x99, 0x1d, 0x00, 0x01, 0x00, 0x02, 0x01, 0x60]
var combinedCommand = sensor2ReadCommand + sensorReadCommand
var beepCommandPt1 : [UInt8] = [0x0f, 0x00, 0x00, 0x00, 0x80, 0x00, 0x00, 0x94]
var beepCommandPt2 : [UInt8] = [0x01, 0x81, 0x02, 0x82, 0xe8, 0x03, 0x82, 0xe8, 0x03, 0x0d, 0x00, 0x00, 0x00, 0x00, 0x04, 0x00, 0x99]
var beepCommandPt3 : [UInt8] = [0x1d, 0x00, 0x00, 0x00, 0x02, 0x01, 0x60]

var sensorReadPt1 : [UInt8] = [0x0d, 0x00, 0x00, 0x00, 0x00, 0x04]
var sensorReadPt2 : [UInt8] = [0x00, 0x99, 0x1d, 0x00, 0x00, 0x00, 0x02, 0x01]
var sensorReadPt3 : [UInt8] = [0x60, 0x0d, 0x00, 0x00, 0x00, 0x00]
var sensorReadPt4 : [UInt8] = [0x04, 0x00, 0x99, 0x1d, 0x00, 0x01, 0x00, 0x02, 0x01, 0x60]

let channelMTU = 1007 // this is the longest possible message we can send over the RFCOMM channel to EV3
var massiveCommand : [UInt8] = Array(repeating: UInt8(0), count: channelMTU)

// This was used for printing the device info after a successful SDP query. The important bit
// is that the available RFCOMM channel on the EV3 is channel id 1.
// If other devices have different channel ids for RFCOMM, this could be useful, but EV3 is always 1.
class AsyncCallbacks : IOBluetoothDeviceAsyncCallbacks {
    func remoteNameRequestComplete(_ device: IOBluetoothDevice!, status: IOReturn) {
        print("remote name request complete : \(status)")
    }
    
    func connectionComplete(_ device: IOBluetoothDevice!, status: IOReturn) {
        print("connection complete : \(status)")
    }
    
    func sdpQueryComplete(_ device: IOBluetoothDevice!, status: IOReturn) {
        print(device)
    }
}

// Handlers for RFCOMM channel events
class ChannelDelegate : IOBluetoothRFCOMMChannelDelegate {
    func rfcommChannelOpenComplete(_ rfcommChannel: IOBluetoothRFCOMMChannel!, status error: IOReturn) {
        print("channel opened: \(error)")
        // My brief research points to MTU being configurable, so this might be useful for non-EV3 BT devices
        print("mtu: \(rfcommChannel.getMTU())")
        var refcon = 0;
        Timer.scheduledTimer(withTimeInterval: 2, repeats: true) { _ in
            // keep polling for sensor values to simulate active communication with brick
            rfcommChannel?.writeAsync(&sensorReadCommand, length: UInt16(sensorReadCommand.count), refcon: &refcon)
        }
    }
    func rfcommChannelData(_ rfcommChannel: IOBluetoothRFCOMMChannel!, data dataPointer: UnsafeMutableRawPointer!, length dataLength: Int) {
        let size = dataPointer.load(as: UInt8.self) // first byte of response is message length (not counting the size bytes in the length)
        for index in 0...Int(size)+1 { // response is size + 2 size bytes. `...` includes the last value; `..<` would not
            print(dataPointer.load(fromByteOffset: index, as: UInt8.self))
        }
        print("\n")
    }
    func rfcommChannelWriteComplete(_ rfcommChannel: IOBluetoothRFCOMMChannel!, refcon: UnsafeMutableRawPointer!, status error: IOReturn) {
        if (error == kIOReturnSuccess) {
            print("sent successfully!")
        } else {
            print("LilTrog doesn't love me")
        }
    }
    func rfcommChannelClosed(_ rfcommChannel: IOBluetoothRFCOMMChannel!) {
        print("disconnected")
    }
}

// Enumeration callbacks.
class InquiryDelegate : IOBluetoothDeviceInquiryDelegate {
    var channelDelegate : IOBluetoothRFCOMMChannelDelegate!
    var target : IOBluetoothDeviceAsyncCallbacks?
    func deviceInquiryStarted(_ sender: IOBluetoothDeviceInquiry) {
        print("Inquiry Started...")
    }
    func deviceInquiryDeviceFound(_ sender: IOBluetoothDeviceInquiry, device: IOBluetoothDevice) {
        print("\(device.addressString!) aka \(device.name!), paired = \(device.isPaired())")
        print("\(device.rawRSSI())")
        if (device.addressString! == "00-16-53-3d-05-04") { // LilTrog
//            device.performSDPQuery(target)
            myDevice = device
//            sender.stop() // stop inquiry once we found the device
        }
    }
    func deviceInquiryComplete(_ sender: IOBluetoothDeviceInquiry!, error: IOReturn, aborted: Bool) {
        var channel : IOBluetoothRFCOMMChannel?
        myDevice.openRFCOMMChannelAsync(&channel, withChannelID: 1, delegate: channelDelegate)
        print("inquiry complete!")
    }
}

var channelDelegate = ChannelDelegate() // Handle channel messages
var callbacks = AsyncCallbacks() // Handle SDP query response

//reference the following outside of any class:
var inquiryDelegate = InquiryDelegate()
inquiryDelegate.channelDelegate = channelDelegate
inquiryDelegate.target = callbacks

// Set up inquiry: look for Toy Robots for 30 seconds
var inquiry = IOBluetoothDeviceInquiry(delegate: inquiryDelegate)
inquiry?.setSearchCriteria(BluetoothServiceClassMajor(kBluetoothServiceClassMajorAny),
                           majorDeviceClass: BluetoothDeviceClassMajor(kBluetoothDeviceClassMajorToy),
                           minorDeviceClass: BluetoothDeviceClassMinor(kBluetoothDeviceClassMinorToyRobot))
inquiry?.inquiryLength = 30
inquiry?.updateNewDeviceNames = true
inquiry?.start()

var anotherInquiryDelegate = InquiryDelegate()
var anotherInquiry = IOBluetoothDeviceInquiry(delegate: anotherInquiryDelegate)
anotherInquiry?.updateNewDeviceNames = true
anotherInquiry?.start()

// Allow continous running
RunLoop.main.run()
