// <copyright file="WinBTSession.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink.Win.BLE;

using Fleck;
using ScratchLink.BT;
using ScratchLink.Extensions;
using ScratchLink.JsonRpc;
using System.Diagnostics;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Devices.Enumeration;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

/// <summary>
/// Implements a classic Bluetooth (RFCOMM) session on Windows.
/// </summary>
internal class WinBTSession : BTSession<DeviceInformation, string>
{
    private readonly Dictionary<string, DeviceInformation> deviceWatcherResults = new ();

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
        var selector = BuildSelector(major, minor);

        try
        {
            this.watcher = DeviceInformation.CreateWatcher(
                selector,
                new List<string>
                {
                    AQS.SignalStrength,
                    AQS.IsPresent,
                },
                DeviceInformationKind.AssociationEndpoint);
            this.watcher.Added += this.PeripheralAdded;
            this.watcher.Updated += this.PeripheralUpdated;
            this.watcher.Removed += this.PeripheralRemoved;
            this.watcher.EnumerationCompleted += this.EnumerationCompleted;
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

        try
        {
            var service = servicesResult.Services[0];
            this.connectedSocket = new StreamSocket();
            await this.connectedSocket.ConnectAsync(service.ConnectionHostName, service.ConnectionServiceName);
            this.socketWriter = new DataWriter(this.connectedSocket.OutputStream);
            this.socketReader = new DataReader(this.connectedSocket.InputStream)
            {
                ByteOrder = ByteOrder.LittleEndian,
            };
        }
        catch (Exception e)
        {
            Trace.WriteLine($"Encountered exception trying to connect: {e}");
            this.CloseConnection();
            throw JsonRpc2Error.ServerError(-32500, "Could not connect to RFCOMM channel.").ToException();
        }

        this.ListenForMessages();

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

    /// <summary>
    /// Build an AQS string to find the Bluetooth peripheral devices that we're interested in.
    /// Similar to <see cref="BluetoothDevice.GetDeviceSelectorFromClassOfDevice" /> but tuned for our use case.
    /// </summary>
    /// <param name="major">The major device class to search for.</param>
    /// <param name="minor">The minor device class to search for.</param>
    /// <returns>The query string, ready for <see cref="DeviceWatcher" />.
    private static string BuildSelector(BluetoothMajorClass major, BluetoothMinorClass minor)
    {
        const string isBluetoothDevice = $"{AQS.ProtocolId}:=\"{AQS.BluetoothDeviceClassId}\"";
        const string isPaired = $"{AQS.IsPaired}:={AQS.BooleanTrue}";
        const string canPair = $"{AQS.CanPair}:={AQS.BooleanTrue}";

        var hasCorrectMajorClass = $"{AQS.BluetoothMajorClass}:={(int)major}";
        var hasCorrectMinorClass = $"{AQS.BluetoothMinorClass}:={(int)minor}";
        var hasCorrectClasses = $"({hasCorrectMajorClass} AND {hasCorrectMinorClass})";

        return $"{isBluetoothDevice} AND ({isPaired} OR {canPair}) AND {hasCorrectClasses}";
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
        this.socketReader?.Dispose();
        this.socketReader = null;

        this.socketWriter?.Dispose();
        this.socketWriter = null;

        this.connectedSocket?.Dispose();
        this.connectedSocket = null;
    }

    private void PeripheralAdded(DeviceWatcher sender, DeviceInformation deviceInformation)
    {
        this.deviceWatcherResults[deviceInformation.Id] = deviceInformation;
        this.ReportPeripheral(deviceInformation);
    }

    private void PeripheralUpdated(DeviceWatcher sender, DeviceInformationUpdate deviceInformationUpdate)
    {
        if (this.deviceWatcherResults.TryGetValue(deviceInformationUpdate.Id, out var deviceInformation))
        {
            deviceInformation.Update(deviceInformationUpdate);
            this.ReportPeripheral(deviceInformation);
        }
        else
        {
            Debug.Print($"Received update for unknown peripheral {deviceInformationUpdate.Id}");
        }
    }

    private void ReportPeripheral(DeviceInformation deviceInformation)
    {
        // Debugging hint: set a watch for deviceInformation.Properties.ToList()
        // Warning: System.Devices.Aep.IsPresent can be true even if the device isn't actually present / turned on.
        //
        // For now, let's treat that false positive as OK. Possible future strategies:
        // - only display devices that have received at least 2 updates
        //   - on my system, paired-but-absent devices get an initial "Add" with isPresent=false, then an "Update"
        //     with isPresent=true
        // - find another property that indicates whether the device is truly present
        //   - I can't find documentation on `System.DeviceInterface.Bluetooth.Flags` values but it looks like
        //     `Flags & 128` might indicate presence.
        if (!deviceInformation.Properties.TryGetValueAs(AQS.IsPresent, out bool? isPresent) || isPresent != true)
        {
            Debug.Print($"Ignoring absent device '{deviceInformation.Name}' with ID={deviceInformation.Id}");
            return;
        }

        deviceInformation.Properties.TryGetValueAs(AQS.SignalStrength, out int? rssi);

        _ = this.OnPeripheralDiscovered(deviceInformation, deviceInformation.Id, deviceInformation.Name, rssi);
    }

    private void PeripheralRemoved(DeviceWatcher sender, DeviceInformationUpdate deviceInformation)
    {
        // This method does nothing, but having an event handler for <see cref="DeviceWatcher.Removed"/> is
        // necessary according to the documentation:
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

        this.watcher.Added -= this.PeripheralAdded;
        this.watcher.Updated -= this.PeripheralUpdated;
        this.watcher.Removed -= this.PeripheralRemoved;
        this.watcher.EnumerationCompleted -= this.EnumerationCompleted;
        this.watcher.Stopped -= this.EnumerationStopped;
        this.watcher = null;
    }

    /// <summary>
    /// String constants used for building AQS queries. Also good for asking DeviceWatcher for additional properties.
    /// Things we can look for are listed here:
    /// <seealso href="https://docs.microsoft.com/en-us/windows/uwp/devices-sensors/device-information-properties"/>.
    /// </summary>
    /// <seealso href="https://learn.microsoft.com/en-us/windows/win32/properties/devices-bumper" />
    /// <seealso href="https://github.com/microsoft/Windows-universal-samples/blob/main/Samples/DeviceEnumerationAndPairing/cs/DisplayHelpers.cs" />
    /// <seealso href="https://github.com/microsoft/Windows-universal-samples/blob/main/Samples/DeviceEnumerationAndPairing/cs/Scenario2_DeviceWatcher.xaml.cs" />
    /// <seealso href="https://github.com/microsoft/Windows-universal-samples/blob/main/Samples/DeviceEnumerationAndPairing/cs/Scenario8_PairDevice.xaml.cs" />
    private static class AQS
    {
        /// <summary>
        /// Microsoft's Class ID for Bluetooth devices. Check this against Protocol ID.
        /// </summary>
        internal const string BluetoothDeviceClassId = "{E0CBF06C-CD8B-4647-BB8A-263B43F0F974}";

        /// <summary>
        /// A 16-bit integer property representing the major device class of a Bluetooth device.
        /// </summary>
        internal const string BluetoothMajorClass = "System.Devices.Aep.Bluetooth.Cod.Major";

        /// <summary>
        /// A 16-bit integer property representing the minor device class of a Bluetooth device.
        /// </summary>
        internal const string BluetoothMinorClass = "System.Devices.Aep.Bluetooth.Cod.Minor";

        /// <summary>
        /// The Boolean false value, typed for structured queries.
        /// </summary>
        internal const string BooleanFalse = "System.StructuredQueryType.Boolean#False";

        /// <summary>
        /// The Boolean true value, typed for structured queries.
        /// </summary>
        internal const string BooleanTrue = "System.StructuredQueryType.Boolean#True";

        /// <summary>
        /// A Boolean property indicating whether an Association Endpoint can be paired with the system.
        /// </summary>
        internal const string CanPair = "System.Devices.Aep.CanPair";

        /// <summary>
        /// A Boolean property indicating whether an Association Endpoint is paired with the system.
        /// </summary>
        internal const string IsPaired = "System.Devices.Aep.IsPaired";

        /// <summary>
        /// A Boolean property indicating whether or not the device is present.
        /// Note that this can be <c>true</c> for a paired device even if it's not actually present.
        /// See <see href="https://github.com/MicrosoftDocs/windows-dev-docs/issues/2881" />.
        /// </summary>
        internal const string IsPresent = "System.Devices.Aep.IsPresent";

        /// <summary>
        /// A GUID property for the identity of the protocol used to discover this device.
        /// </summary>
        internal const string ProtocolId = "System.Devices.Aep.ProtocolId";

        /// <summary>
        /// A 32-bit integer property representing relative signal strength.
        /// </summary>
        internal const string SignalStrength = "System.Devices.Aep.SignalStrength";
    }
}
