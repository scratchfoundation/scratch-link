// <copyright file="IBLEEndpoint.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink.BLE;

/// <summary>
/// Interface representing a GATT "endpoint" -- a characteristic on a service.
/// </summary>
public interface IBLEEndpoint
{
    /// <summary>
    /// Write a buffer to this service characteristic.
    /// </summary>
    /// <param name="buffer">A buffer of bytes to write to the characteristic.</param>
    /// <param name="withResponse">Whether or not to set the "with response" BLE flag. Null for default, true/false to override the default.</param>
    /// <param name="cancellationToken">The cancellation token to use to cancel the operation.</param>
    /// <returns>The number of bytes written.</returns>
    Task<int> Write(byte[] buffer, bool? withResponse, CancellationToken cancellationToken);

    /// <summary>
    /// Read the current value of this service characteristic.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token to use to cancel the operation.</param>
    /// <returns>The bytes read from the characteristic's value.</returns>
    Task<byte[]> Read(CancellationToken cancellationToken);
}
