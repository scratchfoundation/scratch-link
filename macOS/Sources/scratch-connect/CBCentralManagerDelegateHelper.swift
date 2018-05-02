import CoreBluetooth

// Use this class to act as a CBCentralManagerDelegate without conforming to NSObjectProtocol or inheriting NSObject.
// Usage:
// 1. Conform to SwiftCBCentralManagerDelegate instead of CBCentralManagerDelegate
// 2. Create an instance of CBCentralManagerDelegateHelper
// 3. Set the CBCentralManagerDelegateHelper as the CBCentralManager's delegate
// 4. Set your SwiftCBCentralManagerDelegate-conforming object as the CBCentralManagerDelegateHelper's delegate
class CBCentralManagerDelegateHelper: NSObject, CBCentralManagerDelegate {
    weak var delegate: SwiftCBCentralManagerDelegate?

    func centralManagerDidUpdateState(_ central: CBCentralManager) {
        delegate?.centralManagerDidUpdateState(central)
    }

    func centralManager(_ central: CBCentralManager, willRestoreState dict: [String: Any]) {
        delegate?.centralManager?(central, willRestoreState: dict)
    }

    func centralManager(_ central: CBCentralManager, didDiscover peripheral: CBPeripheral,
                        advertisementData: [String: Any], rssi RSSI: NSNumber) {
        delegate?.centralManager?(central, didDiscover: peripheral, advertisementData: advertisementData, rssi: RSSI)
    }

    func centralManager(_ central: CBCentralManager, didConnect peripheral: CBPeripheral) {
        delegate?.centralManager?(central, didConnect: peripheral)
    }

    func centralManager(_ central: CBCentralManager, didFailToConnect peripheral: CBPeripheral, error: Error?) {
        delegate?.centralManager?(central, didFailToConnect: peripheral, error: error)
    }

    func centralManager(_ central: CBCentralManager, didDisconnectPeripheral peripheral: CBPeripheral, error: Error?) {
        delegate?.centralManager?(central, didDisconnectPeripheral: peripheral, error: error)
    }
}

// This is a copy of CBCentralManagerDelegate without NSObjectProtocol conformance.
@available(OSX 10.7, *)
@objc protocol SwiftCBCentralManagerDelegate {
    func centralManagerDidUpdateState(_ central: CBCentralManager)
    @objc optional func centralManager(_ central: CBCentralManager, willRestoreState dict: [String : Any])
    @objc optional func centralManager(_ central: CBCentralManager, didDiscover peripheral: CBPeripheral,
                                       advertisementData: [String : Any], rssi RSSI: NSNumber)
    @objc optional func centralManager(_ central: CBCentralManager, didConnect peripheral: CBPeripheral)
    @objc optional func centralManager(_ central: CBCentralManager, didFailToConnect peripheral: CBPeripheral,
                                       error: Error?)
    @objc optional func centralManager(_ central: CBCentralManager, didDisconnectPeripheral peripheral: CBPeripheral,
                                       error: Error?)
}
