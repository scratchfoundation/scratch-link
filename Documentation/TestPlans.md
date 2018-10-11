# Testing Scratch Link

All tests require access to Scratch 3.0 through <https://beta.scratch.mit.edu> or similar.

## Required Equipment

At least one of each:

- BBC micro:bit (a BLE device with constant polling)
- LEGO WeDo 2.0 (a BLE device without constant polling)
- LEGO EV3 (a BT device)
- Computer running Windows version 1607 / build 14393 or higher
- Computer running macOS version 10.10 "Yosemite" or higher

## Test Cases

### Scratch Link startup & shutdown

1. Start Scratch Link.
2. Click the Scratch Link icon and verify the version number.
3. Click "Exit" or "Quit" and verify that Scratch Link quits.

### BBC micro:bit

1. Start Scratch 3.0 & Scratch Link.
2. Load the "micro:bit" extension.
3. Connect to the micro:bit.
4. **Test the `display text` block.**
5. **Test the `tilt angle` block.**
    1. Check that the value changes when tilting the micro:bit
    2. Using something like `forever point in direction (tilt angle)`, check that tilt angle latency is acceptable.
6. Disconnect power from the micro:bit.
7. **Verify that Scratch displays a disconnect notification.**
8. Reconnect power to the micro:bit.
9. **Verify that Scratch can connect to the micro:bit again.**

### LEGO WeDo 2.0

1. Start Scratch 3.0 & Scratch Link.
2. Load the "LEGO WeDo 2.0" extension.
3. Connect to the WeDo 2.
4. **Test the `set light color to ()` block.**
5. **Test either the `tilt angle` block or the `distance` block.**
    1. Check that the reporter's value changes appropriately.
    2. Using something like `forever point in direction ()`, check that sensor latency is acceptable.
6. Hold the button on the WeDo 2 to turn it off.
7. **Verify that Scratch displays a disconnect notification.**
8. **Verify that Scratch can connect to the WeDo 2 again.**

### LEGO EV3

1. Turn on the EV3 -- startup takes a while.
2. Start Scratch 3.0 & Scratch Link.
3. Load the "LEGO MINDSTORMS EV3" extension.
4. Connect to the EV3.
5. **Test the `beep note () for () secs` block.**
6. **Test either the `distance` block or the `brightness` block.**
    1. Check that the reporter's value changes appropriately.
    2. Using something like `forever point in direction ()`, check that sensor latency is acceptable.
7. Turn off the EV3.
8. **Verify that Scratch displays a disconnect notification.**
9. Turn on the EV3 again.
10. **Verify that Scratch can connect to the EV3 again.**

### Bluetooth Toggle

1. Start Scratch 3.0 & Scratch Link.
2. Load the extension for any BLE or BT peripheral and connect to the peripheral.
3. Using the Windows or macOS controls, disable Bluetooth.
4. **Verify that Scratch displays a disconnect notification.**
5. Turn on Bluetooth again.
6. **Verify that Scratch can connect to the peripheral again.**

### Computer Sleep

1. Start Scratch 3.0 & Scratch Link.
2. Load the extension for any BLE or BT peripheral and connect to the peripheral.
3. Cause your computer to enter sleep mode.
4. After a few seconds, wake your computer.
5. **Verify that Scratch displays a disconnect notification.**
6. **Verify that Scratch can connect to the peripheral again.**
