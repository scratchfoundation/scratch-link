// <copyright file="WinBLESession.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink.Win.BLE;

using Fleck;
using ScratchLink.BLE;
using ScratchLink.Extensions;
using ScratchLink.JsonRpc;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

/// <summary>
/// Implements a BLE session on Windows.
/// </summary>
/// <remarks>
/// We could use <c>BluetoothLEDevice</c> as <c>TDiscoveredPeripheral</c>, but <c>FromBluetoothAddressAsync</c> causes a connection
/// so that wouldn't be polite.
/// </remarks>
internal class WinBLESession : BLESession<BluetoothLEAdvertisementReceivedEventArgs, ulong, Guid>
{
    /// <summary>
    /// The minimum value for RSSI during discovery: peripherals with a weaker signal will be ignored.
    /// </summary>
    protected const short MinimumSignalStrength = -70;

    /// <summary>
    /// Hysteresis margin for signal strength threshold.
    /// </summary>
    protected const short SignalStrengthMargin = 5;

    /// <summary>
    /// Time, in milliseconds, after which a peripheral will be considered "out of range".
    /// </summary>
    protected const double OutOfRangeTimeout = 2000;

    private BluetoothLEAdvertisementWatcher watcher;
    private IEnumerable<BLEScanFilter> filters;
    private BluetoothLEDevice connectedPeripheral;

    /// <summary>
    /// Initializes a new instance of the <see cref="WinBLESession"/> class.
    /// </summary>
    /// <param name="webSocket">The WebSocket connection for this session.</param>
    public WinBLESession(IWebSocketConnection webSocket)
        : base(webSocket)
    {
    }

    /// <inheritdoc/>
    protected override bool IsConnected => this.connectedPeripheral != null;

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        // TODO

        base.Dispose(disposing);
    }

    /// <inheritdoc/>
    protected override Task<object> DoDiscover(List<BLEScanFilter> filters)
    {
        if (this.watcher?.Status == BluetoothLEAdvertisementWatcherStatus.Started)
        {
            // unhook this watcher since we're about to replace it
            this.watcher.Received -= this.Watcher_AdvertisementReceived;
            this.watcher.Stop();
        }

        this.watcher = new BluetoothLEAdvertisementWatcher()
        {
            SignalStrengthFilter =
            {
                InRangeThresholdInDBm = MinimumSignalStrength,
                OutOfRangeThresholdInDBm = MinimumSignalStrength - SignalStrengthMargin,
                OutOfRangeTimeout = TimeSpan.FromMilliseconds(OutOfRangeTimeout),
            },
        };
        this.ClearDiscoveredPeripherals();
        this.filters = filters;
        this.watcher.Received += this.Watcher_AdvertisementReceived;
        this.watcher.Start();

        return Task.FromResult<object>(null);
    }

    /// <inheritdoc/>
    protected override async Task<object> DoConnect(BluetoothLEAdvertisementReceivedEventArgs discoveredPeripheral)
    {
        var peripheral = await BluetoothLEDevice.FromBluetoothAddressAsync(discoveredPeripheral.BluetoothAddress);

        // prove we're actually connected (and refresh the system cache if necessary)
        var servicesResult = await peripheral?.GetGattServicesAsync(BluetoothCacheMode.Uncached);
        if (servicesResult?.Status != GattCommunicationStatus.Success)
        {
            throw JsonRpc2Error.ApplicationError($"failed to enumerate GATT services: {servicesResult.Status}").ToException();
        }

        this.connectedPeripheral = peripheral;
        peripheral.ConnectionStatusChanged += this.Peripheral_ConnectionStatusChanged;

        this.watcher.Stop();
        this.watcher = null;
        this.ClearDiscoveredPeripherals();

        return null;
    }

    /// <inheritdoc/>
    protected override Guid GetDefaultServiceId()
    {
        var defaultService = this.connectedPeripheral.GattServices
            .FirstOrDefault(service =>
                this.AllowedServices.Contains(service.Uuid) &&
                !this.GattHelpers.IsBlocked(service.Uuid));
        return defaultService.Uuid;
    }

    /// <inheritdoc/>
    protected override Guid GetDefaultCharacteristicId(Guid serviceId)
    {
        var defaultService = this.connectedPeripheral.GattServices
            .FirstOrDefault(service =>
                this.AllowedServices.Contains(service.Uuid) &&
                !this.GattHelpers.IsBlocked(service.Uuid));
        var defaultCharacteristic = defaultService.GetAllCharacteristics()
            .FirstOrDefault(characteristic =>
                !this.GattHelpers.IsBlocked(characteristic.Uuid));
        return defaultCharacteristic.Uuid;
    }

    /// <inheritdoc/>
    protected override async Task<IBLEEndpoint> DoGetEndpoint(Guid serviceId, Guid characteristicId)
    {
        var serviceResult = await this.connectedPeripheral.GetGattServicesForUuidAsync(serviceId, BluetoothCacheMode.Uncached);
        var service = serviceResult.Services.FirstOrDefault();
        var characteristicResult = await service.GetCharacteristicsForUuidAsync(characteristicId, BluetoothCacheMode.Uncached);
        var characteristic = characteristicResult.Characteristics.FirstOrDefault();
        return new WinBLEEndpoint(characteristic);
    }

    private async void Watcher_AdvertisementReceived(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
    {
        if (args.RawSignalStrengthInDBm == -127)
        {
            // TODO: figure out why we get redundant(?) advertisements with RSSI=-127
            return;
        }

        if (args.AdvertisementType != BluetoothLEAdvertisementType.ConnectableDirected &&
            args.AdvertisementType != BluetoothLEAdvertisementType.ConnectableUndirected)
        {
            // Advertisement does not indicate that the device can connect
            return;
        }

        var manufacturerData = args.Advertisement.ManufacturerData.ToDictionary(
            item => (int)item.CompanyId,
            item => (IEnumerable<byte>)item.Data.ToArray());

        foreach (var item in args.Advertisement.ManufacturerData)
        {
            manufacturerData[item.CompanyId] = item.Data.ToArray();
        }

        if (!this.filters.Any(filter => filter.Matches(args.Advertisement.LocalName, args.Advertisement.ServiceUuids, manufacturerData)))
        {
            // No matching filters
            return;
        }

        // the device must have passed a filter!
        await this.OnPeripheralDiscovered(args, args.BluetoothAddress, args.Advertisement.LocalName, args.RawSignalStrengthInDBm);
    }

    private void Peripheral_ConnectionStatusChanged(BluetoothLEDevice sender, object args)
    {
        switch (sender.ConnectionStatus)
        {
            case BluetoothConnectionStatus.Connected:
                // do nothing
                break;
            case BluetoothConnectionStatus.Disconnected:
                this.EndSession();
                break;
        }
    }
}
