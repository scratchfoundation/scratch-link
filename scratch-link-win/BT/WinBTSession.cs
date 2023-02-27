// <copyright file="WinBTSession.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink.Win.BLE;

using Fleck;
using ScratchLink.BT;
using ScratchLink.JsonRpc;
using System.Diagnostics;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Devices.Enumeration;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

internal class WinBTSession : BTSession<DeviceInformation, string>
{
    // Things we can look for are listed here:
    // <a href="https://docs.microsoft.com/en-us/windows/uwp/devices-sensors/device-information-properties" />

    /// <summary>
    /// Signal strength property.
    /// </summary>
    private const string SignalStrengthPropertyName = "System.Devices.Aep.SignalStrength";

    /// <summary>
    /// Indicates that the device returned is actually available and not discovered from a cache.
    /// </summary>
    private const string IsPresentPropertyName = "System.Devices.Aep.IsPresent";

    /// <summary>
    /// Bluetooth MAC address.
    /// </summary>
    private const string BluetoothAddressPropertyName = "System.Devices.Aep.DeviceAddress";

    private DeviceWatcher watcher;
    private StreamSocket connectedSocket;
    private DataWriter socketWriter;
    private DataReader socketReader;

    /// <summary>
    /// Initializes a new instance of the <see cref="WinBTSession"/> class.
    /// </summary>
    /// <param name="webSocket">The WebSocket connection for this session.</param>
    public WinBTSession(IWebSocketConnection webSocket)
        : base(webSocket)
    {
    }

    /// <inheritdoc/>
    protected override bool IsConnected => this.connectedSocket != null;

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (this.watcher != null &&
            (this.watcher.Status == DeviceWatcherStatus.Started ||
             this.watcher.Status == DeviceWatcherStatus.EnumerationCompleted))
        {
            this.watcher.Stop();
        }
        if (this.connectedSocket != null)
        {
            this.CloseConnection();
        }
    }

    /// <inheritdoc/>
    protected override Task<object> DoDiscover(byte majorDeviceClass, byte minorDeviceClass)
    {
        var major = (BluetoothMajorClass)majorDeviceClass;
        var minor = (BluetoothMinorClass)minorDeviceClass;

        var deviceClass = BluetoothClassOfDevice.FromParts(
            major,
            minor,
            BluetoothServiceCapabilities.None);
        var selector = BluetoothDevice.GetDeviceSelectorFromClassOfDevice(deviceClass);

        try
        {
            this.watcher = DeviceInformation.CreateWatcher(selector, new List<string>
            {
                SignalStrengthPropertyName,
                IsPresentPropertyName,
                BluetoothAddressPropertyName,
            });
            this.watcher.Added += this.PeripheralDiscovered;
            this.watcher.EnumerationCompleted += this.EnumerationCompleted;
            this.watcher.Updated += this.PeripheralUpdated;
            this.watcher.Stopped += this.EnumerationStopped;
            this.watcher.Start();
        }
        catch (ArgumentException)
        {
            throw JsonRpc2Error.ServerError(-32500, "Device inquiry failed to start").ToException();
        }

        return Task.FromResult<object>(null);
    }

    /// <inheritdoc/>
    protected override async Task<object> DoConnect(DeviceInformation device, string pinString)
    {
        var bluetoothDevice = await BluetoothDevice.FromIdAsync(device.Id);
        if (!bluetoothDevice.DeviceInformation.Pairing.IsPaired)
        {
            var pairingResult = await this.Pair(bluetoothDevice, pinString);
            if (pairingResult != DevicePairingResultStatus.Paired &&
                pairingResult != DevicePairingResultStatus.AlreadyPaired)
            {
                // TODO: throw?
                Trace.WriteLine($"Failed to pair BT device with address={device}");
            }
        }

        var servicesResult = await bluetoothDevice.GetRfcommServicesForIdAsync(
            RfcommServiceId.SerialPort,
            BluetoothCacheMode.Uncached);
        var service = servicesResult.Services.FirstOrDefault();
        if (service != null)
        {
            this.connectedSocket = new StreamSocket();
            await this.connectedSocket.ConnectAsync(service.ConnectionHostName, service.ConnectionServiceName);
            this.socketWriter = new DataWriter(this.connectedSocket.OutputStream);
            this.socketReader = new DataReader(this.connectedSocket.InputStream)
            {
                ByteOrder = ByteOrder.LittleEndian,
            };
            this.ListenForMessages();
        }
        else
        {
            throw JsonRpc2Error.ServerError(-32500, "Could not connect to RFCOMM channel.").ToException();
        }

        return null;
    }

    /// <summary>
    /// Attempt to automatically pair with a BT peripheral device.
    /// </summary>
    /// <param name="bluetoothDevice">The <c>BluetoothDevice</c> to pair with.</param>
    /// <param name="pinString">The PIN code, if provided by the client. Otherwise, null.</param>
    /// <returns>The resulting status of the pairing attempt.</returns>
    protected async Task<DevicePairingResultStatus> Pair(BluetoothDevice bluetoothDevice, string pinString)
    {
        void CustomOnPairingRequested(DeviceInformationCustomPairing sender, DevicePairingRequestedEventArgs args)
        {
            args.Accept(pinString);
        }

        bluetoothDevice.DeviceInformation.Pairing.Custom.PairingRequested += CustomOnPairingRequested;
        try
        {
            var pairingResult = await bluetoothDevice.DeviceInformation.Pairing.Custom.PairAsync(
                DevicePairingKinds.ProvidePin);
            return pairingResult.Status;
        }
        finally
        {
            bluetoothDevice.DeviceInformation.Pairing.Custom.PairingRequested -= CustomOnPairingRequested;
        }
    }

    /// <inheritdoc/>
    protected override async Task<int> DoSend(byte[] buffer)
    {
        if (this.socketWriter == null)
        {
            throw JsonRpc2Error.InvalidRequest("cannot send when not connected").ToException();
        }

        try
        {
            this.socketWriter.WriteBytes(buffer);
            await this.socketWriter.StoreAsync();
        }
        catch (ObjectDisposedException)
        {
            throw JsonRpc2Error.InternalError("send encountered an error").ToException();
        }

        return buffer.Length;
    }

    private async void ListenForMessages()
    {
        try
        {
            // this pattern only works for devices that send "packets" that start with a 16-bit packet size
            // TODO: just relay all received data regardless of format
            while (true)
            {
                await this.socketReader.LoadAsync(sizeof(ushort));
                var messageSize = this.socketReader.ReadUInt16();
                var headerBytes = BitConverter.GetBytes(messageSize);

                var messageBytes = new byte[messageSize];
                await this.socketReader.LoadAsync(messageSize);
                this.socketReader.ReadBytes(messageBytes);

                var totalBytes = new byte[headerBytes.Length + messageSize];
                Array.Copy(headerBytes, totalBytes, headerBytes.Length);
                Array.Copy(messageBytes, 0, totalBytes, headerBytes.Length, messageSize);

                _ = this.DidReceiveMessage(totalBytes);
            }
        }
        catch (Exception e)
        {
            Debug.Print($"Closing connection to peripheral: {e.Message}");
            this.CloseConnection();
        }
    }

    private void CloseConnection()
    {
        this.socketReader.Dispose();
        this.socketWriter.Dispose();
        this.connectedSocket.Dispose();
    }

    private void PeripheralDiscovered(DeviceWatcher sender, DeviceInformation deviceInformation)
    {
        if (!deviceInformation.Properties.TryGetValue(IsPresentPropertyName, out var isPresent)
            || isPresent == null || (bool)isPresent == false)
        {
            return;
        }

        deviceInformation.Properties.TryGetValue(SignalStrengthPropertyName, out var rssi);

        _ = this.OnPeripheralDiscovered(deviceInformation, deviceInformation.Id, deviceInformation.Name, (int)rssi);
    }

    private void PeripheralUpdated(DeviceWatcher sender, DeviceInformationUpdate args)
    {
        // This method does nothing, but having an event handler for <see cref="DeviceWatcher.Updated"/> seems to
        // be necessary for timely "didDiscoverPeripheral" notifications. If there is no handler, all discovered
        // peripherals are notified right before enumeration completes.
        // See also: https://learn.microsoft.com/en-us/uwp/api/windows.devices.enumeration.devicewatcher.updated
    }

    private void EnumerationCompleted(DeviceWatcher sender, object args)
    {
        Debug.Print("Enumeration completed.");
    }

    private void EnumerationStopped(DeviceWatcher sender, object args)
    {
        if (this.watcher.Status == DeviceWatcherStatus.Aborted)
        {
            Debug.Print("Enumeration stopped unexpectedly.");
        }
        else if (this.watcher.Status == DeviceWatcherStatus.Stopped)
        {
            Debug.Print("Enumeration stopped.");
        }

        this.watcher.Added -= this.PeripheralDiscovered;
        this.watcher.EnumerationCompleted -= this.EnumerationCompleted;
        this.watcher.Updated -= this.PeripheralUpdated;
        this.watcher.Stopped -= this.EnumerationStopped;
        this.watcher = null;
    }
}
