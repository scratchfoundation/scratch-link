import IOBluetooth

// Use this class to act as a IOBluetoothRFCOMMChannelDelegate without conforming to NSObjectProtocol or inheriting
// NSObject.
// Usage:
// 1. Conform to SwiftIOBluetoothRFCOMMChannelDelegate instead of IOBluetoothRFCOMMChannelDelegate
// 2. Create an instance of IOBluetoothRFCOMMChannelDelegateHelper
// 3. Set the IOBluetoothRFCOMMChannelDelegateHelper as the IOBluetoothRFCOMMChannel's delegate
// 4. Set your SwiftIOBluetoothRFCOMMChannelDelegate-conforming object as the IOBluetoothRFCOMMChannelDelegateHelper's
//    delegate
class IOBluetoothRFCOMMChannelDelegateHelper: NSObject, IOBluetoothRFCOMMChannelDelegate {
    weak var delegate: SwiftIOBluetoothRFCOMMChannelDelegate?

    @available(OSX 10.2, *)
    func rfcommChannelClosed(_ rfcommChannel: IOBluetoothRFCOMMChannel!) {
        delegate?.rfcommChannelClosed?(rfcommChannel)
    }

    @available(OSX 10.2, *)
    func rfcommChannelControlSignalsChanged(_ rfcommChannel: IOBluetoothRFCOMMChannel!) {
        delegate?.rfcommChannelControlSignalsChanged?(rfcommChannel)
    }

    @available(OSX 10.2, *)
    func rfcommChannelData(_ rfcommChannel: IOBluetoothRFCOMMChannel!,
                                          data dataPointer: UnsafeMutableRawPointer!, length dataLength: Int) {
        delegate?.rfcommChannelData?(rfcommChannel, data: dataPointer, length: dataLength)
    }

    @available(OSX 10.2, *)
    func rfcommChannelFlowControlChanged(_ rfcommChannel: IOBluetoothRFCOMMChannel!) {
        delegate?.rfcommChannelFlowControlChanged?(rfcommChannel)
    }

    @available(OSX 10.2, *)
    func rfcommChannelOpenComplete(_ rfcommChannel: IOBluetoothRFCOMMChannel!, status error: IOReturn) {
        delegate?.rfcommChannelOpenComplete?(rfcommChannel, status: error)
    }

    @available(OSX 10.2, *)
    func rfcommChannelQueueSpaceAvailable(_ rfcommChannel: IOBluetoothRFCOMMChannel!) {
        delegate?.rfcommChannelQueueSpaceAvailable?(rfcommChannel)
    }

    @available(OSX 10.2, *)
    func rfcommChannelWriteComplete(_ rfcommChannel: IOBluetoothRFCOMMChannel!,
                                                   refcon: UnsafeMutableRawPointer!, status error: IOReturn) {
        delegate?.rfcommChannelWriteComplete?(rfcommChannel, refcon: refcon, status: error)
    }
}

// This is a copy of IOBluetoothRFCOMMChannelDelegate without NSObjectProtocol conformance.
@objc protocol SwiftIOBluetoothRFCOMMChannelDelegate {
    @available(OSX 10.2, *)
    @objc optional func rfcommChannelClosed(_ rfcommChannel: IOBluetoothRFCOMMChannel!)

    @available(OSX 10.2, *)
    @objc optional func rfcommChannelControlSignalsChanged(_ rfcommChannel: IOBluetoothRFCOMMChannel!)

    @available(OSX 10.2, *)
    @objc optional func rfcommChannelData(_ rfcommChannel: IOBluetoothRFCOMMChannel!,
                                          data dataPointer: UnsafeMutableRawPointer!, length dataLength: Int)

    @available(OSX 10.2, *)
    @objc optional func rfcommChannelFlowControlChanged(_ rfcommChannel: IOBluetoothRFCOMMChannel!)

    @available(OSX 10.2, *)
    @objc optional func rfcommChannelOpenComplete(_ rfcommChannel: IOBluetoothRFCOMMChannel!, status error: IOReturn)

    @available(OSX 10.2, *)
    @objc optional func rfcommChannelQueueSpaceAvailable(_ rfcommChannel: IOBluetoothRFCOMMChannel!)

    @available(OSX 10.2, *)
    @objc optional func rfcommChannelWriteComplete(_ rfcommChannel: IOBluetoothRFCOMMChannel!,
                                                   refcon: UnsafeMutableRawPointer!, status error: IOReturn)
}
