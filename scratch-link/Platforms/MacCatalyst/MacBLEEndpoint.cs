// <copyright file="MacBLEEndpoint.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink.Platforms.MacCatalyst;

using CoreBluetooth;
using Foundation;
using ScratchLink.BLE;

/// <summary>
/// Implement <see cref="IBLEEndpoint"/> by wrapping <see cref="CBCharacteristic"/>.
/// </summary>
internal class MacBLEEndpoint : IBLEEndpoint
{
    private readonly CBCharacteristic characteristic;

    /// <summary>
    /// Initializes a new instance of the <see cref="MacBLEEndpoint"/> class.
    /// </summary>
    /// <param name="characteristic">The CoreBluetooth characteristic object to wrap.</param>
    public MacBLEEndpoint(CBCharacteristic characteristic)
    {
        this.characteristic = characteristic;
    }

    /// <inheritdoc/>
    public Task<int> Write(byte[] buffer, bool? withResponse, CancellationToken cancellationToken)
    {
        var peripheral = this.characteristic.Service.Peripheral;
        var writeType = (withResponse ?? !this.characteristic.Properties.HasFlag(CBCharacteristicProperties.WriteWithoutResponse))
            ? CBCharacteristicWriteType.WithResponse
            : CBCharacteristicWriteType.WithoutResponse;

        cancellationToken.ThrowIfCancellationRequested();
        peripheral.WriteValue(NSData.FromArray(buffer), this.characteristic, writeType);

        return Task.FromResult(buffer.Length);
    }

    /// <inheritdoc/>
    public async Task<byte[]> Read(CancellationToken cancellationToken)
    {
        var peripheral = this.characteristic.Service.Peripheral;

        using (var updatedValueAwaiter = new EventAwaiter<CBCharacteristicEventArgs>(
            h => peripheral.UpdatedCharacterteristicValue += h,
            h => peripheral.UpdatedCharacterteristicValue -= h))
        {
            while (true)
            {
                peripheral.ReadValue(this.characteristic);
                var characteristicValueUpdated = await updatedValueAwaiter.MakeTask(TimeSpan.FromSeconds(5), cancellationToken);
                if (characteristicValueUpdated.Characteristic.UUID.Equals(this.characteristic.UUID))
                {
                    return characteristicValueUpdated.Characteristic.Value.ToArray();
                }
            }
        }
    }
}
