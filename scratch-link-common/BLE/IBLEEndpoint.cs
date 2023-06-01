// <copyright file="IBLEEndpoint.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink.BLE;

using System;
using System.Threading.Tasks;

/// <summary>
/// Interface representing a GATT "endpoint" -- a characteristic on a service.
/// </summary>
public interface IBLEEndpoint
{
    /// <summary>
    /// Gets a string representing the UUID of the service.
    /// </summary>
    string ServiceId { get; }

    /// <summary>
    /// Gets a string representing the UUID of the characteristic.
    /// </summary>
    string CharacteristicId { get; }

    /// <summary>
    /// Write a buffer to this service characteristic.
    /// </summary>
    /// <param name="buffer">A buffer of bytes to write to the characteristic.</param>
    /// <param name="withResponse">Whether or not to set the "with response" BLE flag. Null for default, true/false to override the default.</param>
    /// <returns>The number of bytes written.</returns>
    Task<int> Write(byte[] buffer, bool? withResponse);

    /// <summary>
    /// Read the current value of this service characteristic.
    /// </summary>
    /// <returns>The bytes read from the characteristic's value.</returns>
    Task<byte[]> Read();

    /// <summary>
    /// Start reporting changes in the value of this characteristic.
    /// </summary>
    /// <param name="notifier">An action to execute when the value changes.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task StartNotifications(Action<byte[]> notifier);

    /// <summary>
    /// Stop reporting changes in the value of this characteristic.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task StopNotifications();
}
