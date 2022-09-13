// <copyright file="CBCentralManagerEventDelegate.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink.Mac.BLE;

using System;
using CoreBluetooth;
using Foundation;

/// <summary>
/// Reflect delegate methods to events.
/// The default delegate does this too, but is inaccessible when instantiating the manager with options.
/// It'd be nice to reflect them to the events on the manager itself, but C# doesn't allow that.
/// So instead, this delegate mimics and replaces the events on the manager.
/// </summary>
public class CBCentralManagerEventDelegate : CBCentralManagerDelegate
{
    /// <summary>
    /// Event indicating that a peripheral has connected.
    /// </summary>
    public event EventHandler<CBPeripheralEventArgs> ConnectedPeripheralEvent;

    /// <summary>
    /// Event indicating that a peripheral has disconnected.
    /// </summary>
    public event EventHandler<CBPeripheralErrorEventArgs> DisconnectedPeripheralEvent;

    /// <summary>
    /// Event indicating that a peripheral has been discovered.
    /// </summary>
    public event EventHandler<CBDiscoveredPeripheralEventArgs> DiscoveredPeripheralEvent;

    /// <summary>
    /// Event indicating that a peripheral has failed to connect.
    /// </summary>
    public event EventHandler<CBPeripheralErrorEventArgs> FailedToConnectPeripheralEvent;

    /// <summary>
    /// Event indicating that a list of connected peripherals has been retrieved.
    /// </summary>
    public event EventHandler<CBPeripheralsEventArgs> RetrievedConnectedPeripheralsEvent;

    /// <summary>
    /// Event indicating that a list of peripherals has been retrieved.
    /// </summary>
    public event EventHandler<CBPeripheralsEventArgs> RetrievedPeripheralsEvent;

    /// <summary>
    /// Event indicating that the manager's state has updated.
    /// </summary>
    public event EventHandler UpdatedStateEvent;

    /// <summary>
    /// Event indicating that the manager's state will be restored.
    /// </summary>
    public event EventHandler<CBWillRestoreEventArgs> WillRestoreStateEvent;

    /// <inheritdoc/>
    public override void ConnectedPeripheral(CBCentralManager central, CBPeripheral peripheral)
    {
        var eventHandler = this.ConnectedPeripheralEvent;
        if (eventHandler != null)
        {
            eventHandler(central, new (peripheral));
        }
    }

    /// <inheritdoc/>
    public override void DisconnectedPeripheral(CBCentralManager central, CBPeripheral peripheral, NSError error)
    {
        var eventHandler = this.DisconnectedPeripheralEvent;
        if (eventHandler != null)
        {
            eventHandler(central, new (peripheral, error));
        }
    }

    /// <inheritdoc/>
    public override void DiscoveredPeripheral(CBCentralManager central, CBPeripheral peripheral, NSDictionary advertisementData, NSNumber RSSI)
    {
        var eventHandler = this.DiscoveredPeripheralEvent;
        if (eventHandler != null)
        {
            eventHandler(central, new (peripheral, advertisementData, RSSI));
        }
    }

    /// <inheritdoc/>
    public override void FailedToConnectPeripheral(CBCentralManager central, CBPeripheral peripheral, NSError error)
    {
        var eventHandler = this.FailedToConnectPeripheralEvent;
        if (eventHandler != null)
        {
            eventHandler(central, new (peripheral, error));
        }
    }

    /// <inheritdoc/>
    public override void RetrievedConnectedPeripherals(CBCentralManager central, CBPeripheral[] peripherals)
    {
        var eventHandler = this.RetrievedConnectedPeripheralsEvent;
        if (eventHandler != null)
        {
            eventHandler(central, new (peripherals));
        }
    }

    /// <inheritdoc/>
    public override void RetrievedPeripherals(CBCentralManager central, CBPeripheral[] peripherals)
    {
        var eventHandler = this.RetrievedPeripheralsEvent;
        if (eventHandler != null)
        {
            eventHandler(central, new (peripherals));
        }
    }

    /// <inheritdoc/>
    public override void UpdatedState(CBCentralManager central)
    {
        this.UpdatedStateEvent?.Invoke(central, EventArgs.Empty);
    }

    /// <inheritdoc/>
    public override void WillRestoreState(CBCentralManager central, NSDictionary dict)
    {
        var eventHandler = this.WillRestoreStateEvent;
        if (eventHandler != null)
        {
            eventHandler(central, new (dict));
        }
    }
}
