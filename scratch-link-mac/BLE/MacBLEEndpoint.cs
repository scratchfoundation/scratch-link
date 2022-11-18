// <copyright file="MacBLEEndpoint.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink.Mac.BLE;

using System;
using System.Threading;
using System.Threading.Tasks;
using CoreBluetooth;
using Foundation;
using ScratchLink.BLE;

/// <summary>
/// Implement <see cref="IBLEEndpoint"/> by wrapping <see cref="CBCharacteristic"/>.
/// </summary>
internal class MacBLEEndpoint : IBLEEndpoint
{
    private readonly CBCharacteristic characteristic;
    private EventHandler<CBCharacteristicEventArgs> notifier;

    /// <summary>
    /// Initializes a new instance of the <see cref="MacBLEEndpoint"/> class.
    /// </summary>
    /// <param name="characteristic">The CoreBluetooth characteristic object to wrap.</param>
    public MacBLEEndpoint(CBCharacteristic characteristic)
    {
        this.characteristic = characteristic;
    }

    /// <inheritdoc/>
    string IBLEEndpoint.ServiceId => this.characteristic.Service.UUID.ToString();

    /// <inheritdoc/>
    string IBLEEndpoint.CharacteristicId => this.characteristic.UUID.ToString();

    /// <inheritdoc/>
    Task<int> IBLEEndpoint.Write(byte[] buffer, bool? withResponse)
    {
        var peripheral = this.characteristic.Service.Peripheral;
        var writeType = (withResponse ?? !this.characteristic.Properties.HasFlag(CBCharacteristicProperties.WriteWithoutResponse))
            ? CBCharacteristicWriteType.WithResponse
            : CBCharacteristicWriteType.WithoutResponse;

        peripheral.WriteValue(NSData.FromArray(buffer), this.characteristic, writeType);

        return Task.FromResult(buffer.Length);
    }

    /// <inheritdoc/>
    async Task<byte[]> IBLEEndpoint.Read()
    {
        var peripheral = this.characteristic.Service.Peripheral;

        // Note: the typo in `UpdatedCharacterteristicValue` is in the SDK
        using (var updatedValueAwaiter = new EventAwaiter<CBCharacteristicEventArgs>(
            h => peripheral.UpdatedCharacterteristicValue += h,
            h => peripheral.UpdatedCharacterteristicValue -= h))
        {
            while (true)
            {
                peripheral.ReadValue(this.characteristic);
                var characteristicValueUpdated = await updatedValueAwaiter.MakeTask(TimeSpan.FromSeconds(5), CancellationToken.None);
                if (characteristicValueUpdated.Characteristic.UUID.Equals(this.characteristic.UUID))
                {
                    return characteristicValueUpdated.Characteristic.Value.ToArray();
                }
            }
        }
    }

    /// <inheritdoc/>
    Task IBLEEndpoint.StartNotifications(Action<byte[]> notifier)
    {
        var peripheral = this.characteristic.Service.Peripheral;

        if (this.notifier != null)
        {
            // Note: the typo in `UpdatedCharacterteristicValue` is in the SDK
            peripheral.UpdatedCharacterteristicValue -= this.notifier;
        }

        this.notifier = (o, e) =>
        {
            if (e.Characteristic.UUID.Equals(this.characteristic.UUID))
            {
                notifier(e.Characteristic.Value.ToArray());
            }
        };

        peripheral.UpdatedCharacterteristicValue += this.notifier;

        peripheral.SetNotifyValue(true, this.characteristic);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    Task IBLEEndpoint.StopNotifications()
    {
        if (this.notifier != null)
        {
            // Note: the typo in `UpdatedCharacterteristicValue` is in the SDK
            var peripheral = this.characteristic.Service.Peripheral;
            peripheral.SetNotifyValue(false, this.characteristic);
            peripheral.UpdatedCharacterteristicValue -= this.notifier;
            this.notifier = null;
        }

        return Task.CompletedTask;
    }
}
