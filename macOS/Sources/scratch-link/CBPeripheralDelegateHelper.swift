import CoreBluetooth

// Use this class to act as a CBPeripheralDelegate without conforming to NSObjectProtocol or inheriting NSObject.
// Usage:
// 1. Conform to SwiftCBPeripheralDelegate instead of CBPeripheralDelegate
// 2. Create an instance of CBPeripheralDelegateHelper
// 3. Set the CBPeripheralDelegateHelper as the delegate for one or more CBPeripheral
// 4. Set your SwiftCBPeripheralDelegate-conforming object as the CBPeripheralDelegateHelper's delegate
class CBPeripheralDelegateHelper: NSObject, CBPeripheralDelegate {
    weak var delegate: SwiftCBPeripheralDelegate?

    @available(OSX 10.9, *)
    func peripheralDidUpdateName(_ peripheral: CBPeripheral) {
        delegate?.peripheralDidUpdateName?(peripheral)
    }

    @available(OSX 10.9, *)
    func peripheral(_ peripheral: CBPeripheral, didModifyServices invalidatedServices: [CBService]) {
        delegate?.peripheral?(peripheral, didModifyServices: invalidatedServices)
    }

    @available(OSX, introduced: 10.7, deprecated: 10.13)
    func peripheralDidUpdateRSSI(_ peripheral: CBPeripheral, error: Error?) {
        delegate?.peripheralDidUpdateRSSI?(peripheral, error: error)
    }

    @available(OSX 10.13, *)
    func peripheral(_ peripheral: CBPeripheral, didReadRSSI RSSI: NSNumber, error: Error?) {
        delegate?.peripheral?(peripheral, didReadRSSI: RSSI, error: error)
    }

    @available(OSX 10.7, *)
    func peripheral(_ peripheral: CBPeripheral, didDiscoverServices error: Error?) {
        delegate?.peripheral?(peripheral, didDiscoverServices: error)
    }

    @available(OSX 10.7, *)
    func peripheral(_ peripheral: CBPeripheral, didDiscoverIncludedServicesFor service: CBService, error: Error?) {
        delegate?.peripheral?(peripheral, didDiscoverIncludedServicesFor: service, error: error)
    }

    @available(OSX 10.7, *)
    func peripheral(_ peripheral: CBPeripheral, didDiscoverCharacteristicsFor service: CBService, error: Error?) {
        delegate?.peripheral?(peripheral, didDiscoverCharacteristicsFor: service, error: error)
    }

    @available(OSX 10.7, *)
    func peripheral(_ peripheral: CBPeripheral, didUpdateValueFor characteristic: CBCharacteristic, error: Error?) {
        delegate?.peripheral?(peripheral, didUpdateValueFor: characteristic, error: error)
    }

    @available(OSX 10.7, *)
    func peripheral(
        _ peripheral: CBPeripheral, didWriteValueFor characteristic: CBCharacteristic, error: Error?) {
        delegate?.peripheral?(peripheral, didWriteValueFor: characteristic, error: error)
    }

    @available(OSX 10.7, *)
    func peripheral(
        _ peripheral: CBPeripheral, didUpdateNotificationStateFor characteristic: CBCharacteristic, error: Error?) {
        delegate?.peripheral?(peripheral, didUpdateNotificationStateFor: characteristic, error: error)
    }

    @available(OSX 10.7, *)
    func peripheral(
        _ peripheral: CBPeripheral, didDiscoverDescriptorsFor characteristic: CBCharacteristic, error: Error?) {
        delegate?.peripheral?(peripheral, didDiscoverDescriptorsFor: characteristic, error: error)
    }

    @available(OSX 10.7, *)
    func peripheral(_ peripheral: CBPeripheral, didUpdateValueFor descriptor: CBDescriptor, error: Error?) {
        delegate?.peripheral?(peripheral, didUpdateValueForDescriptor: descriptor, error: error)
    }

    @available(OSX 10.7, *)
    func peripheral(_ peripheral: CBPeripheral, didWriteValueFor descriptor: CBDescriptor, error: Error?) {
        delegate?.peripheral?(peripheral, didWriteValueForDescriptor: descriptor, error: error)
    }

    @available(OSX 10.7, *)
    func peripheralIsReady(toSendWriteWithoutResponse peripheral: CBPeripheral) {
        delegate?.peripheralIsReady?(toSendWriteWithoutResponse: peripheral)
    }

    @available(OSX 10.13, *)
    func peripheral(_ peripheral: CBPeripheral, didOpen channel: CBL2CAPChannel?, error: Error?) {
        delegate?.peripheral?(peripheral, didOpen: channel, error: error)
    }
}

// This is a copy of CBPeripheralDelegate without NSObjectProtocol conformance.
@objc protocol SwiftCBPeripheralDelegate {
    @available(OSX 10.9, *)
    @objc optional func peripheralDidUpdateName(_ peripheral: CBPeripheral)

    @available(OSX 10.9, *)
    @objc optional func peripheral(_ peripheral: CBPeripheral, didModifyServices invalidatedServices: [CBService])

    @available(OSX, introduced: 10.7, deprecated: 10.13)
    @objc optional func peripheralDidUpdateRSSI(_ peripheral: CBPeripheral, error: Error?)

    @available(OSX 10.13, *)
    @objc optional func peripheral(_ peripheral: CBPeripheral, didReadRSSI RSSI: NSNumber, error: Error?)

    @available(OSX 10.7, *)
    @objc optional func peripheral(_ peripheral: CBPeripheral, didDiscoverServices error: Error?)

    @available(OSX 10.7, *)
    @objc optional func peripheral(
        _ peripheral: CBPeripheral, didDiscoverIncludedServicesFor service: CBService, error: Error?)

    @available(OSX 10.7, *)
    @objc optional func peripheral(
        _ peripheral: CBPeripheral, didDiscoverCharacteristicsFor service: CBService, error: Error?)

    @available(OSX 10.7, *)
    @objc optional func peripheral(
        _ peripheral: CBPeripheral, didUpdateValueFor characteristic: CBCharacteristic, error: Error?)

    @available(OSX 10.7, *)
    @objc optional func peripheral(
        _ peripheral: CBPeripheral, didWriteValueFor characteristic: CBCharacteristic, error: Error?)

    @available(OSX 10.7, *)
    @objc optional func peripheral(
        _ peripheral: CBPeripheral, didUpdateNotificationStateFor characteristic: CBCharacteristic, error: Error?)

    @available(OSX 10.7, *)
    @objc optional func peripheral(
        _ peripheral: CBPeripheral, didDiscoverDescriptorsFor characteristic: CBCharacteristic, error: Error?)

    @available(OSX 10.7, *)
    @objc optional func peripheral(
        _ peripheral: CBPeripheral, didUpdateValueForDescriptor descriptor: CBDescriptor, error: Error?)

    @available(OSX 10.7, *)
    @objc optional func peripheral(
        _ peripheral: CBPeripheral, didWriteValueForDescriptor descriptor: CBDescriptor, error: Error?)

    @available(OSX 10.7, *)
    @objc optional func peripheralIsReady(toSendWriteWithoutResponse peripheral: CBPeripheral)

    @available(OSX 10.13, *)
    @objc optional func peripheral(_ peripheral: CBPeripheral, didOpen channel: CBL2CAPChannel?, error: Error?)
}
