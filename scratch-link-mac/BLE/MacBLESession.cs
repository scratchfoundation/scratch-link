// <copyright file="MacBLESession.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink.Mac.BLE;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CoreBluetooth;
using CoreFoundation;
using Fleck;
using Foundation;
using ScratchLink.BLE;
using ScratchLink.Extensions;
using ScratchLink.JsonRpc;
using ScratchLink.Mac.Extensions;

/// <summary>
/// Implements a BLE session on MacOS.
/// </summary>
internal class MacBLESession : BLESession<CBPeripheral, NSUuid, CBUUID>
{
    /// <summary>
    /// The minimum value for RSSI during discovery: peripherals with a weaker signal will be ignored.
    /// </summary>
    protected static readonly NSNumber MinimumSignalStrength = -70;

    private readonly CBCentralManager cbManager;
    private readonly CBCentralManagerEventDelegate cbDelegate;

    private readonly Dictionary<NSUuid, CBPeripheral> discoveredPeripherals = new ();

    private readonly SemaphoreSlim btSettledEventLock = new (1);

    private readonly SemaphoreSlim filterLock = new (1);
    private List<BLEScanFilter> filters;

    private CBPeripheral connectedPeripheral;

    /// <summary>
    /// Initializes a new instance of the <see cref="MacBLESession"/> class.
    /// </summary>
    /// <param name="webSocket">The web socket.</param>
    public MacBLESession(IWebSocketConnection webSocket)
        : base(webSocket)
    {
        Trace.WriteLine("before CBCM init");

        this.cbDelegate = new CBCentralManagerEventDelegate();

        var options = new CBCentralInitOptions
        {
            ShowPowerAlert = true,
        };

#if DEBUG
        this.cbDelegate.ConnectedPeripheralEvent += (o, e) => Trace.WriteLine("ConnectedPeripheral");
        this.cbDelegate.DisconnectedPeripheralEvent += (o, e) => Trace.WriteLine("DisconnectedPeripheral");
        this.cbDelegate.DiscoveredPeripheralEvent += (o, e) => Trace.WriteLine("DiscoveredPeripheral");
        this.cbDelegate.FailedToConnectPeripheralEvent += (o, e) => Trace.WriteLine("FailedToConnectPeripheral");
        this.cbDelegate.RetrievedConnectedPeripheralsEvent += (o, e) => Trace.WriteLine("RetrievedConnectedPeripherals");
        this.cbDelegate.RetrievedPeripheralsEvent += (o, e) => Trace.WriteLine("RetrievedPeripherals");
        this.cbDelegate.UpdatedStateEvent += (o, e) => Trace.WriteLine($"UpdatedState {(o as CBCentralManager).State}");

        // this.cbDelegate.WillRestoreStateEvent += (o, e) => Trace.WriteLine("WillRestoreState");
#endif

        this.cbDelegate.UpdatedStateEvent += this.WrapEventHandler(this.CbManager_UpdatedState);
        this.cbDelegate.DiscoveredPeripheralEvent += this.WrapEventHandler<CBDiscoveredPeripheralEventArgs>(this.CbManager_DiscoveredPeripheral);
        this.cbDelegate.DisconnectedPeripheralEvent += this.WrapEventHandler<CBPeripheralErrorEventArgs>(this.CbManager_DisconnectedPeripheral);

        this.cbManager = new (this.cbDelegate, null, options);
        Trace.WriteLine("after CBCM init");
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

    private BluetoothState CurrentBluetoothState { get; set; } = BluetoothState.Unknown;

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (!this.DisposedValue)
        {
            this.cbManager.StopScan();
            if (this.connectedPeripheral != null)
            {
                this.cbManager.CancelPeripheralConnection(this.connectedPeripheral);
                this.connectedPeripheral = null;
            }

            this.cbManager.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc/>
    protected override async Task<object> DoDiscover(List<BLEScanFilter> filters)
    {
        Trace.WriteLine("in DoDiscover");
        var currentState = await this.GetSettledBluetoothState();
        if (currentState != BluetoothState.Available)
        {
            throw JsonRpc2Error.ApplicationError("Bluetooth is not available").ToException();
        }

        using (await this.filterLock.WaitDisposableAsync(DefaultLockTimeout))
        {
            this.filters = filters;
            this.discoveredPeripherals.Clear();
            this.cbManager.ScanForPeripherals(null, new PeripheralScanningOptions()
            {
                AllowDuplicatesKey = true,
            });
        }

        return null;
    }

    /// <inheritdoc/>
    protected override async Task<object> DoConnect(CBPeripheral peripheral)
    {
        using (await this.filterLock.WaitDisposableAsync(DefaultLockTimeout))
        {
            if (this.connectedPeripheral != null)
            {
                throw JsonRpc2Error.InvalidRequest("already connected or connecting").ToException();
            }

            this.cbManager.StopScan();
            this.connectedPeripheral = peripheral;
        }

#if DEBUG
        this.connectedPeripheral.DidOpenL2CapChannel += (o, e) => Trace.WriteLine("DidOpenL2CapChannel");
        this.connectedPeripheral.DiscoveredCharacteristic += (o, e) => Trace.WriteLine("DiscoveredCharacteristic");
        this.connectedPeripheral.DiscoveredDescriptor += (o, e) => Trace.WriteLine("DiscoveredDescriptor");
        this.connectedPeripheral.DiscoveredIncludedService += (o, e) => Trace.WriteLine("DiscoveredIncludedService");
        this.connectedPeripheral.DiscoveredService += (o, e) => Trace.WriteLine("DiscoveredService");
        this.connectedPeripheral.IsReadyToSendWriteWithoutResponse += (o, e) => Trace.WriteLine("IsReadyToSendWriteWithoutResponse");
        this.connectedPeripheral.ModifiedServices += (o, e) => Trace.WriteLine("ModifiedServices");
        this.connectedPeripheral.RssiRead += (o, e) => Trace.WriteLine("RssiRead");
        this.connectedPeripheral.RssiUpdated += (o, e) => Trace.WriteLine("RssiUpdated");
        this.connectedPeripheral.UpdatedName += (o, e) => Trace.WriteLine("UpdatedName");
        this.connectedPeripheral.UpdatedNotificationState += (o, e) => Trace.WriteLine("UpdatedNotificationState");
        this.connectedPeripheral.UpdatedValue += (o, e) => Trace.WriteLine("UpdatedValue");
        this.connectedPeripheral.WroteCharacteristicValue += (o, e) => Trace.WriteLine("WroteCharacteristicValue");
        this.connectedPeripheral.WroteDescriptorValue += (o, e) => Trace.WriteLine("WroteDescriptorValue");

        // this one is especially noisy
        // this.connectedPeripheral.UpdatedCharacterteristicValue += (o, e) => Trace.WriteLine("UpdatedCharacterteristicValue");
#endif

        // wait for the connection to complete
        using (var connectAwaiter = new EventAwaiter<CBPeripheralEventArgs>(
            h => this.cbDelegate.ConnectedPeripheralEvent += h,
            h => this.cbDelegate.ConnectedPeripheralEvent -= h))
        {
            this.cbManager.ConnectPeripheral(this.connectedPeripheral);

            var connectArgs = await connectAwaiter.MakeTask(BluetoothTimeouts.Connection, CancellationToken.None);

            if (this.connectedPeripheral != connectArgs.Peripheral)
            {
                this.connectedPeripheral = null;
                throw JsonRpc2Error.InternalError("did not connect to correct peripheral").ToException();
            }
        }

        using (var servieDiscoveryAwaiter = new EventAwaiter<NSErrorEventArgs>(
            h => this.connectedPeripheral.DiscoveredService += h,
            h => this.connectedPeripheral.DiscoveredService -= h))
        {
            // discover services before we report that we're connected
            // TODO: the documentation says "setting the parameter to nil is considerably slower and is not recommended"
            // but if I provide `allowedServices` then `peripheral.services` doesn't get populated...
            this.connectedPeripheral.DiscoverServices(null);

            // Wait for the services to actually be discovered
            // Note that while the C# name for this event is "DiscoveredService" (singular),
            // the Obj-C / Swift name is "peripheral:didDiscoverServices:" (plural).
            // In practice, this event actually means that `peripheral.services` is now populated.
            await servieDiscoveryAwaiter.MakeTask(BluetoothTimeouts.ServiceDiscovery, CancellationToken.None);
        }

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
            using var characteristicDiscoveryAwaiter = new EventAwaiter<CBServiceEventArgs>(
                h => service.Peripheral.DiscoveredCharacteristic += h,
                h => service.Peripheral.DiscoveredCharacteristic -= h);

            while (service.Characteristics == null)
            {
                service.Peripheral.DiscoverCharacteristics(service);
                await characteristicDiscoveryAwaiter.MakeTask(BluetoothTimeouts.ServiceDiscovery, CancellationToken.None);
            }
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
    private async ValueTask<BluetoothState> GetSettledBluetoothState()
    {
        Trace.WriteLine("about to GetSettled");

        // Do this first to make sure we can't miss an event
        using var settledAwaiter = new EventAwaiter<BluetoothState>(
            async h =>
            {
                Trace.WriteLine("about to hook");
                using (await this.btSettledEventLock.WaitDisposableAsync(DefaultLockTimeout))
                {
                    Trace.WriteLine("hooking");
                    this.BluetoothStateSettled += h;
                    Trace.WriteLine("hooked");
                }
            },
            async h =>
            {
                Trace.WriteLine("about to unhook");
                using (await this.btSettledEventLock.WaitDisposableAsync(DefaultLockTimeout))
                {
                    Trace.WriteLine("unhooking");
                    this.BluetoothStateSettled -= h;
                    Trace.WriteLine("unhooked");
                }
            });

        var bluetoothState = this.CurrentBluetoothState;
        Trace.WriteLine($"starting state is {bluetoothState}");

        if (bluetoothState == BluetoothState.Unknown)
        {
            Trace.WriteLine("awaiting");
            bluetoothState = await settledAwaiter.MakeTask(BluetoothTimeouts.SettleManagerState, CancellationToken.None);
            Trace.WriteLine($"settled state is {bluetoothState}");
        }

        return bluetoothState;
    }

    private async void CbManager_UpdatedState(object sender, EventArgs e)
    {
        // this can fire during construction of `this.cbManager`, before it actually gets set
        // in that case `this.cbManager` is null, so access the manager through `sender` instead
        var cbManager = sender as CBCentralManager;
        var cbState = cbManager.State;

        switch (cbState)
        {
            case CBCentralManagerState.Resetting:
                Trace.WriteLine("Bluetooth is resetting");
                break;
            case CBCentralManagerState.Unsupported:
                Trace.WriteLine("Bluetooth is unsupported");
                break;
            case CBCentralManagerState.Unauthorized:
                Trace.WriteLine("Bluetooth is unauthorized");
                break;
            case CBCentralManagerState.PoweredOff:
                Trace.WriteLine("Bluetooth is now powered off");
                break;
            case CBCentralManagerState.PoweredOn:
                Trace.WriteLine("Bluetooth is now powered on");
                break;
            case CBCentralManagerState.Unknown:
            default:
                Trace.WriteLine($"Bluetooth transitioned to unknown state: {cbState}");
                break;
        }

        this.CurrentBluetoothState = cbState switch
        {
            CBCentralManagerState.Unsupported => BluetoothState.Unavailable,
            CBCentralManagerState.Unauthorized => BluetoothState.Unavailable,
            CBCentralManagerState.PoweredOff => BluetoothState.Unavailable,

            CBCentralManagerState.PoweredOn => BluetoothState.Available,

            // Resetting probably means the OS Bluetooth stack crashed and will "power on" again soon
            CBCentralManagerState.Resetting => BluetoothState.Unknown,
            CBCentralManagerState.Unknown => BluetoothState.Unknown,
            _ => BluetoothState.Unknown
        };

        if (this.CurrentBluetoothState == BluetoothState.Unknown)
        {
            // just wait until the OS makes a decision
            return;
        }

        using (await this.btSettledEventLock.WaitDisposableAsync(DefaultLockTimeout))
        {
            this.BluetoothStateSettled?.Invoke(this, this.CurrentBluetoothState);
        }

        // drop the peripheral & session if necessary
        if (this.CurrentBluetoothState != BluetoothState.Available && this.connectedPeripheral != null)
        {
            cbManager.CancelPeripheralConnection(this.connectedPeripheral);
            this.connectedPeripheral.Dispose();
            this.connectedPeripheral = null;

            try
            {
                await this.SendErrorNotification(JsonRpc2Error.ApplicationError("Bluetooth is unavailable"));
            }
            catch (Exception sendErrorException)
            {
                Trace.WriteLine($"Failed to report error to client due to: {sendErrorException}");
            }

            this.EndSession();
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

        using (await this.filterLock.WaitDisposableAsync(DefaultLockTimeout))
        {
            if (!this.filters.Any(filter => filter.Matches(peripheral.Name, allServices, manufacturerData)))
            {
                // no matching filters
                return;
            }
        }

        // the device must have passed the filter!
        await this.OnPeripheralDiscovered(peripheral, peripheral.Identifier, peripheral.Name, rssi.Int32Value);
    }

    private void CbManager_DisconnectedPeripheral(object sender, CBPeripheralErrorEventArgs e)
    {
        if (this.connectedPeripheral != e.Peripheral)
        {
            return;
        }

        this.EndSession();
    }

    /// <summary>
    /// Timeouts for Bluetooth operations.
    /// </summary>
    protected static class BluetoothTimeouts
    {
        /// <summary>
        /// Maximum time to wait for the Bluetooth manager to settle to a known state.
        /// </summary>
        public static readonly TimeSpan SettleManagerState = TimeSpan.FromSeconds(30); // TODO: decrease before release

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
