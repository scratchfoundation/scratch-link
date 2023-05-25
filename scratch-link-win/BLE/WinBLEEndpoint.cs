// <copyright file="WinBLEEndpoint.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink.Win.BLE;

using ScratchLink.BLE;
using ScratchLink.JsonRpc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Foundation;

/// <summary>
/// Implement <see cref="IBLEEndpoint"/> by wrapping <see cref="GattCharacteristic"/>.
/// </summary>
internal class WinBLEEndpoint : IBLEEndpoint
{
    private readonly GattCharacteristic characteristic;
    private TypedEventHandler<GattCharacteristic, GattValueChangedEventArgs> notifier;

    /// <summary>
    /// Initializes a new instance of the <see cref="WinBLEEndpoint"/> class.
    /// </summary>
    /// <param name="characteristic">The GATT characteristic object to wrap.</param>
    public WinBLEEndpoint(GattCharacteristic characteristic)
    {
        this.characteristic = characteristic;
    }

    /// <inheritdoc/>
    public string ServiceId => this.characteristic.Service.Uuid.ToString();

    /// <inheritdoc/>
    public string CharacteristicId => this.characteristic.Uuid.ToString();

    /// <inheritdoc/>
    public async Task<byte[]> Read()
    {
        var readResult = await this.characteristic.ReadValueAsync(Windows.Devices.Bluetooth.BluetoothCacheMode.Uncached);
        return readResult.Status switch
        {
            // Calling ToArray() on a buffer of length 0 throws an ArgumentException
            GattCommunicationStatus.Success => readResult.Value.Length > 0 ? readResult.Value.ToArray() : Array.Empty<byte>(),
            GattCommunicationStatus.Unreachable => throw JsonRpc2Error.ApplicationError("destination unreachable").ToException(),
            GattCommunicationStatus.ProtocolError => throw JsonRpc2Error.ApplicationError("protocol error").ToException(),
            GattCommunicationStatus.AccessDenied => throw JsonRpc2Error.ApplicationError("access denied").ToException(),
            _ => throw JsonRpc2Error.ApplicationError($"unknown result from read: {readResult.Status}").ToException(),
        };
    }

    /// <inheritdoc/>
    public async Task StartNotifications(Action<byte[]> notifier)
    {
        TypedEventHandler<GattCharacteristic, GattValueChangedEventArgs> newNotifier = (o, e) =>
        {
            notifier(e.CharacteristicValue.ToArray());
        };

        // if notifications are already active, just install the new notifier
        if (this.notifier != null)
        {
            this.characteristic.ValueChanged -= this.notifier;
            this.notifier = newNotifier;
            this.characteristic.ValueChanged += this.notifier;
            return;
        }

        // only fill `notifier` after successfully enabling notifications
        try
        {
            this.characteristic.ValueChanged += newNotifier;
            this.notifier = await this.characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Notify) switch
            {
                GattCommunicationStatus.Success => newNotifier,
                _ => throw JsonRpc2Error.ApplicationError("failed to start notifications").ToException(),
            };
        }
        catch (Exception)
        {
            this.characteristic.ValueChanged -= newNotifier;
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task StopNotifications()
    {
        if (this.notifier == null)
        {
            return;
        }

        switch (await this.characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.None))
        {
        case GattCommunicationStatus.Success:
            this.characteristic.ValueChanged -= this.notifier;
            this.notifier = null;
            break;
        default:
            throw JsonRpc2Error.ApplicationError("failed to stop notifications").ToException();
        }
    }

    /// <inheritdoc/>
    public async Task<int> Write(byte[] buffer, bool? withResponse)
    {
        var writeType = (withResponse ?? !this.characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.WriteWithoutResponse))
            ? GattWriteOption.WriteWithResponse
            : GattWriteOption.WriteWithoutResponse;
        var result = await this.characteristic.WriteValueWithResultAsync(
            buffer.AsBuffer(),
            writeType);

        switch (result.Status)
        {
            case GattCommunicationStatus.Success:
                return buffer.Length;
            case GattCommunicationStatus.ProtocolError:
                // "ProtocolError 3"
                throw JsonRpc2Error.ApplicationError($"Error while attempting to write: {result.Status} {result.ProtocolError}").ToException();
            default:
                // "Unreachable"
                throw JsonRpc2Error.ApplicationError($"Error while attempting to write: {result.Status}").ToException();
        }
    }
}
