// <copyright file="MacBLESession.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink.Platforms.MacCatalyst;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text.Json;
using CoreBluetooth;
using Foundation;
using ScratchLink.BLE;
using ScratchLink.Extensions;
using ScratchLink.JsonRpc;
using ScratchLink.Platforms.MacCatalyst.Extensions;

/// <summary>
/// Implements a BLE session on MacOS.
/// </summary>
internal class MacBLESession : BLESession<CBUUID>
{
    /// <summary>
    /// The minimum value for RSSI during discovery: peripherals with a weaker signal will be ignored.
    /// </summary>
    protected static readonly NSNumber MinimumSignalStrength = -70;

    private readonly CBCentralManager cbManager;

    private readonly Dictionary<NSUuid, CBPeripheral> discoveredPeripherals = new ();

    private readonly SemaphoreSlim filterLock = new (1);
    private List<BLEScanFilter> filters;

    private CBPeripheral connectedPeripheral;

    /// <summary>
    /// Initializes a new instance of the <see cref="MacBLESession"/> class.
    /// </summary>
    /// <param name="context">The web socket context.</param>
    public MacBLESession(WebSocketContext context)
        : base(context)
    {
        this.cbManager = new ();
        this.cbManager.UpdatedState += this.WrapEventHandler(this.CbManager_UpdatedState);
        this.cbManager.DiscoveredPeripheral += this.WrapEventHandler<CBDiscoveredPeripheralEventArgs>(this.CbManager_DiscoveredPeripheral);

        this.CancellationToken.Register(() =>
        {
            this.cbManager.StopScan();
            this.cbManager.CancelPeripheralConnection(this.connectedPeripheral);
        });
    }

    private event EventHandler<BluetoothState> BluetoothStateSettled;

    private enum BluetoothState
    {
        Unavailable,
        Available,
        Unknown,
    }

    /// <inheritdoc/>
    protected override bool IsConnected
    {
        get
        {
            return this.connectedPeripheral != null;
        }
    }

    private BluetoothState CurrentBluetoothState
    {
        get => this.cbManager.State switch
        {
            CBManagerState.Unsupported => BluetoothState.Unavailable,
            CBManagerState.Unauthorized => BluetoothState.Unavailable,
            CBManagerState.PoweredOff => BluetoothState.Unavailable,

            CBManagerState.PoweredOn => BluetoothState.Available,

            // Resetting probably means the OS Bluetooth stack crashed and will "power on" again soon
            CBManagerState.Resetting => BluetoothState.Unknown,
            CBManagerState.Unknown => BluetoothState.Unknown,
            _ => BluetoothState.Unknown
        };
    }

    /// <inheritdoc/>
    protected override async Task<object> DoDiscover(List<BLEScanFilter> filters)
    {
        var currentState = await this.GetSettledBluetoothState();
        if (currentState != BluetoothState.Available)
        {
            throw new JsonRpc2Exception(JsonRpc2Error.ApplicationError("Bluetooth is not available"));
        }

        await this.filterLock.WaitAsync(this.CancellationToken);
        try
        {
            this.filters = filters;
            this.discoveredPeripherals.Clear();
            this.cbManager.ScanForPeripherals(null, new PeripheralScanningOptions()
            {
                AllowDuplicatesKey = true,
            });
        }
        finally
        {
            this.filterLock.Release();
        }

        return null;
    }

    /// <inheritdoc/>
    protected override async Task<object> DoConnect(JsonElement jsonPeripheralId)
    {
        NSUuid peripheralId = null;

        try
        {
            var peripheralIdString = jsonPeripheralId.GetString();
            peripheralId = new NSUuid(peripheralIdString);
        }
        catch
        {
            // ignore any exceptions: just check below for a valid peripheralId
        }

        if (peripheralId == null)
        {
            throw new JsonRpc2Exception(JsonRpc2Error.InvalidParams("malformed peripheralId"));
        }

        await this.filterLock.WaitAsync(this.CancellationToken);
        try
        {
            if (this.connectedPeripheral != null)
            {
                throw new JsonRpc2Exception(JsonRpc2Error.InvalidRequest("already connected or connecting"));
            }

            if (!this.discoveredPeripherals.TryGetValue(peripheralId, out var discoveredPeripheral))
            {
                throw new JsonRpc2Exception(JsonRpc2Error.InvalidParams("invalid peripheralId: " + peripheralId));
            }

            this.cbManager.StopScan();
            this.connectedPeripheral = discoveredPeripheral;
        }
        finally
        {
            this.filterLock.Release();
        }

        // wait for the connection to complete
        var connectArgs = await EventAwaiter.MakeTask<CBPeripheralEventArgs>(
            h =>
            {
                this.cbManager.ConnectedPeripheral += h;
                this.cbManager.ConnectPeripheral(this.connectedPeripheral);
            },
            h => this.cbManager.ConnectedPeripheral -= h,
            BluetoothTimeouts.Connection,
            this.CancellationToken);

        if (this.connectedPeripheral != connectArgs.Peripheral)
        {
            this.connectedPeripheral = null;
            throw new JsonRpc2Exception(JsonRpc2Error.InternalError("did not connect to correct peripheral"));
        }

        // We must register at least one event handler before calling DiscoverServices(), otherwise DiscoveredService doesn't trigger.
        // I suspect internally it's registering peripheral.delegate the first time an event handler is attached.
        // We're likely to want this event later anyway, so it's a convenient candidate.
        this.connectedPeripheral.UpdatedCharacterteristicValue += this.ConnectedPeripheral_UpdatedCharacterteristicValue;

        // discover services before we report that we're connected
        // TODO: the documentation says "setting the parameter to nil is considerably slower and is not recommended"
        // but if I provide `allowedServices` then `peripheral.services` doesn't get populated...
        this.connectedPeripheral.DiscoverServices(null);

        // Wait for the services to actually be discovered
        // Note that while the C# name for this event is "DiscoveredService" (singular),
        // the Obj-C / Swift name is "peripheral:didDiscoverServices:" (plural).
        // In practice, this event actually means that `peripheral.services` is now populated.
        await EventAwaiter.MakeTask<NSErrorEventArgs>(
            h => this.connectedPeripheral.DiscoveredService += h,
            h => this.connectedPeripheral.DiscoveredService -= h,
            BluetoothTimeouts.ServiceDiscovery,
            this.CancellationToken);

        // the "connect" request is now complete!
        return null;
    }

    /// <inheritdoc/>
    protected override CBUUID GetDefaultServiceId()
    {
        var services = this.connectedPeripheral?.Services.OrEmpty();

        // find the first service that isn't blocked in any way
        return services
            .Select(service => service.UUID)
            .FirstOrDefault(serviceId =>
                this.AllowedServices.Contains(serviceId) &&
                !this.GattHelpers.IsBlocked(serviceId));
    }

    /// <inheritdoc/>
    protected override CBUUID GetDefaultCharacteristicId(CBUUID serviceId)
    {
        var service = this.connectedPeripheral?
            .Services.OrEmpty()
            .FirstOrDefault(service => service.UUID == serviceId);

        var characteristics = service?.Characteristics.OrEmpty();

        // find the specified service
        // then find its first characteristic that isn't blocked in any way
        return characteristics
            .Select(characteristic => characteristic.UUID)
            .FirstOrDefault(characteristicId =>
                !this.GattHelpers.IsBlocked(characteristicId));
    }

    /// <inheritdoc/>
    protected override async Task<IBLEEndpoint> DoGetEndpoint(CBUUID serviceId, CBUUID characteristicId)
    {
        var service = this.connectedPeripheral?
            .Services.OrEmpty()
            .FirstOrDefault(service => serviceId.Equals(service.UUID));

        if (service.Characteristics == null)
        {
            await EventAwaiter.MakeTask<CBServiceEventArgs>(
                h =>
                {
                    service.Peripheral.DiscoveredCharacteristics += h;
                    service.Peripheral.DiscoverCharacteristics(service);
                },
                h => service.Peripheral.DiscoveredCharacteristics -= h,
                BluetoothTimeouts.ServiceDiscovery,
                this.CancellationToken);
        }

        var characteristic = service?
            .Characteristics.OrEmpty()
            .FirstOrDefault(characteristic => characteristicId.Equals(characteristic.UUID));

        if (characteristic == null)
        {
            return null;
        }

        return new MacBLEEndpoint(characteristic);
    }

    /// <summary>
    /// Wait until CurrentBluetoothState is either "Available" or "Unavailable" (not "Unknown").
    /// </summary>
    /// <returns>A task for the settled Bluetooth state.</returns>
    /// <exception cref="TimeoutException">Thrown if the Bluetooth state doesn't settle.</exception>
    private Task<BluetoothState> GetSettledBluetoothState()
    {
        var initialState = this.CurrentBluetoothState;
        if (initialState != BluetoothState.Unknown)
        {
            return Task.FromResult(initialState);
        }

        return EventAwaiter.MakeTask(this.BluetoothStateSettled, BluetoothTimeouts.SettleManagerState, this.CancellationToken);
    }

    private async void CbManager_UpdatedState(object sender, EventArgs e)
    {
        switch (this.cbManager.State)
        {
            case CBManagerState.Resetting:
                Debug.Print("Bluetooth is resetting");
                break;
            case CBManagerState.Unsupported:
                Debug.Print("Bluetooth is unsupported");
                break;
            case CBManagerState.Unauthorized:
                Debug.Print("Bluetooth is unauthorized");
                break;
            case CBManagerState.PoweredOff:
                Debug.Print("Bluetooth is now powered off");
                break;
            case CBManagerState.PoweredOn:
                Debug.Print("Bluetooth is now powered on");
                break;
            case CBManagerState.Unknown:
            default:
                Debug.Print($"Bluetooth transitioned to unknown state: {this.cbManager.State}");
                break;
        }

        var currentState = this.CurrentBluetoothState;

        if (currentState == BluetoothState.Unknown)
        {
            // just wait until the OS makes a decision
            return;
        }

        this.BluetoothStateSettled?.Invoke(this, currentState);

        // drop the peripheral & session if necessary
        if (currentState != BluetoothState.Available && this.connectedPeripheral != null)
        {
            this.cbManager.CancelPeripheralConnection(this.connectedPeripheral);
            this.connectedPeripheral.Dispose();
            this.connectedPeripheral = null;

            try
            {
                await this.SendErrorNotification(JsonRpc2Error.ApplicationError("Bluetooth is unavailable"), this.CancellationToken);
            }
            catch (Exception sendErrorException)
            {
                Debug.Print("Failed to report error to client due to: ", sendErrorException);
            }

            this.Dispose();
        }
    }

    private async Task CbManager_DiscoveredPeripheral(object sender, CBDiscoveredPeripheralEventArgs e)
    {
        var rssi = e.RSSI;
        if (rssi.CompareTo(MinimumSignalStrength) < 0)
        {
            // signal too weak
            return;
        }

        var peripheral = e.Peripheral;
        if (peripheral.State != CBPeripheralState.Disconnected)
        {
            // doesn't look like we could connect
            return;
        }

        var advertisementData = e.AdvertisementData;

        var allServices = new HashSet<CBUUID>();

        allServices.UnionWith(peripheral.Services.OrEmpty().Select(service => service.UUID));

        if (advertisementData.TryGetValue<NSArray>(CBAdvertisement.DataServiceUUIDsKey, out var advertisedServices))
        {
            // Note: `NSArray.FromArray<T>(myArray)` means "convert NSArray myArray to T[]"
            allServices.UnionWith(NSArray.FromArray<CBUUID>(advertisedServices));
        }

        var manufacturerData = new Dictionary<int, IEnumerable<byte>>();
        if (advertisementData[CBAdvertisement.DataManufacturerDataKey] is NSData advertisedManufacturerData)
        {
            // take two first bytes of advertisementData and use as Device ID
            // TODO: figure out whether it's possible to have a device with two manufacturerData items
            // if so, fix this code to handle that
            var advertisedId = advertisedManufacturerData[0] | (advertisedManufacturerData[1] << 8);

            manufacturerData[advertisedId] = advertisedManufacturerData.Skip(2);
        }

        await this.filterLock.WaitAsync(this.CancellationToken);
        try
        {
            if (!this.filters.Any(filter => filter.Matches(peripheral.Name, allServices, manufacturerData)))
            {
                // no matching filters
                return;
            }
        }
        finally
        {
            this.filterLock.Release();
        }

        // the device must have passed the filter!
        this.discoveredPeripherals[peripheral.Identifier] = peripheral;
        await this.SendNotification(
            "didDiscoverPeripheral",
            new BLEPeripheralDiscovered()
            {
                Name = peripheral.Name,
                PeripheralId = peripheral.Identifier.ToString(),
                RSSI = rssi.Int32Value,
            },
            this.CancellationToken);
    }

    private void ConnectedPeripheral_UpdatedCharacterteristicValue(object sender, CBCharacteristicEventArgs e)
    {
        // TODO
    }

    /// <summary>
    /// Timeouts for Bluetooth operations.
    /// </summary>
    protected static class BluetoothTimeouts
    {
        /// <summary>
        /// Maximum time to wait for the Bluetooth manager to settle to a known state.
        /// </summary>
        public static readonly TimeSpan SettleManagerState = TimeSpan.FromSeconds(3);

        /// <summary>
        /// Maximum time to allow for connecting to a Bluetooth peripheral.
        /// </summary>
        public static readonly TimeSpan Connection = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Maximum time to allow for discovering services on a connected peripheral.
        /// </summary>
        public static readonly TimeSpan ServiceDiscovery = TimeSpan.FromSeconds(30);
    }
}
