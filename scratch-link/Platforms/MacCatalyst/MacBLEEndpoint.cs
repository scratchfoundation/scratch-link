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
    public int Write(byte[] buffer, bool? withResponse)
    {
        var peripheral = this.characteristic.Service.Peripheral;
        var writeType = (withResponse ?? !this.characteristic.Properties.HasFlag(CBCharacteristicProperties.WriteWithoutResponse))
            ? CBCharacteristicWriteType.WithResponse
            : CBCharacteristicWriteType.WithoutResponse;

        peripheral.WriteValue(NSData.FromArray(buffer), this.characteristic, writeType);

        return buffer.Length;
    }
}
