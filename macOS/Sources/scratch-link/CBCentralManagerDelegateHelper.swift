import CoreBluetooth

// Use this class to act as a CBCentralManagerDelegate without conforming to NSObjectProtocol or inheriting NSObject.
// Usage:
// 1. Conform to SwiftCBCentralManagerDelegate instead of CBCentralManagerDelegate
// 2. Create an instance of CBCentralManagerDelegateHelper
// 3. Set the CBCentralManagerDelegateHelper as the CBCentralManager's delegate
// 4. Set your SwiftCBCentralManagerDelegate-conforming object as the CBCentralManagerDelegateHelper's delegate
class CBCentralManagerDelegateHelper: NSObject, CBCentralManagerDelegate {
    weak var delegate: SwiftCBCentralManagerDelegate?

    @available(OSX 10.7, *)
    func centralManagerDidUpdateState(_ central: CBCentralManager) {
        delegate?.centralManagerDidUpdateState(central)
    }

    @available(OSX 10.7, *)
    func centralManager(_ central: CBCentralManager, willRestoreState dict: [String: Any]) {
        delegate?.centralManager?(central, willRestoreState: dict)
    }

    @available(OSX 10.7, *)
    func centralManager(_ central: CBCentralManager, didDiscover peripheral: CBPeripheral,
                        advertisementData: [String: Any], rssi RSSI: NSNumber) {
        delegate?.centralManager?(central, didDiscover: peripheral, advertisementData: advertisementData, rssi: RSSI)
    }

    @available(OSX 10.7, *)
    func centralManager(_ central: CBCentralManager, didConnect peripheral: CBPeripheral) {
        delegate?.centralManager?(central, didConnect: peripheral)
    }

    @available(OSX 10.7, *)
    func centralManager(_ central: CBCentralManager, didFailToConnect peripheral: CBPeripheral, error: Error?) {
        delegate?.centralManager?(central, didFailToConnect: peripheral, error: error)
    }

    @available(OSX 10.7, *)
    func centralManager(_ central: CBCentralManager, didDisconnectPeripheral peripheral: CBPeripheral, error: Error?) {
        delegate?.centralManager?(central, didDisconnectPeripheral: peripheral, error: error)
    }
}

// This is a copy of CBCentralManagerDelegate without NSObjectProtocol conformance.
@objc protocol SwiftCBCentralManagerDelegate {
    @available(OSX 10.7, *)
    func centralManagerDidUpdateState(_ central: CBCentralManager)

    @available(OSX 10.7, *)
    @objc optional func centralManager(_ central: CBCentralManager, willRestoreState dict: [String: Any])

    @available(OSX 10.7, *)
    @objc optional func centralManager(_ central: CBCentralManager, didDiscover peripheral: CBPeripheral,
                                       advertisementData: [String: Any], rssi RSSI: NSNumber)

    @available(OSX 10.7, *)
    @objc optional func centralManager(_ central: CBCentralManager, didConnect peripheral: CBPeripheral)

    @available(OSX 10.7, *)
    @objc optional func centralManager(_ central: CBCentralManager, didFailToConnect peripheral: CBPeripheral,
                                       error: Error?)

    @available(OSX 10.7, *)
    @objc optional func centralManager(_ central: CBCentralManager, didDisconnectPeripheral peripheral: CBPeripheral,
                                       error: Error?)
}
