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
using ScratchLink.JsonRpc;

/// <summary>
/// Implements a BLE session on MacOS.
/// </summary>
internal class MacBLESession : BLESession<CBUUID>
{
    /// <summary>
    /// Maximum time to wait for Bluetooth to settle to a known state.
    /// </summary>
    protected static readonly TimeSpan BluetoothSettleTimeout = TimeSpan.FromSeconds(3);

    /// <summary>
    /// The minimum value for RSSI during discovery: peripherals with a weaker signal will be ignored.
    /// </summary>
    protected static readonly NSNumber MinimumSignalStrength = -70;

    private readonly CBCentralManager cbManager;

    private readonly Dictionary<NSUuid, CBPeripheral> discoveredPeripherals = new ();

    private readonly SemaphoreSlim filterLock = new (1);
    private List<BLEScanFilter> filters;
    private HashSet<CBUUID> allowedServices;
    private IEnumerable<CBUUID> optionalServices;

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
    protected override async Task<object> DoDiscover(List<BLEScanFilter> filters, HashSet<CBUUID> optionalServices)
    {
        var allowedServices = filters.Aggregate(
            optionalServices.OrEmpty().ToHashSet(), // start with a clone of the optional services list
            (result, filter) =>
            {
                result.UnionWith(filter.RequiredServices);
                return result;
            });

        var currentState = await this.GetSettledBluetoothState();
        if (currentState != BluetoothState.Available)
        {
            throw new JsonRpc2Exception(JsonRpc2Error.ApplicationError("Bluetooth is not available"));
        }

        await this.filterLock.WaitAsync(this.CancellationToken);
        try
        {
            this.filters = filters;
            this.allowedServices = allowedServices;
            this.optionalServices = optionalServices;
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
    protected override Task<object> DoConnect(JsonElement jsonPeripheralId)
    {
        throw new NotImplementedException();
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

        // TODO: tie the task to this.CancellationToken too
        var completionSource = new TaskCompletionSource<BluetoothState>();
        EventHandler<BluetoothState> listener = null;

        var delayTimer = Task.Delay(BluetoothSettleTimeout);

        listener = (object sender, BluetoothState newState) =>
        {
            this.BluetoothStateSettled -= listener;
            completionSource.TrySetResult(newState);
        };
        this.BluetoothStateSettled += listener;

        Task.Delay(BluetoothSettleTimeout).ContinueWith(_ =>
        {
            this.BluetoothStateSettled -= listener;
            completionSource.TrySetException(new TimeoutException("Bluetooth state unknown"));
        });

        return completionSource.Task;
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

        // ping GetSettledBluetoothState()
        this.BluetoothStateSettled(this, currentState);

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
            allServices.UnionWith(advertisedServices.EnumerateAs<CBUUID>());
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
}
