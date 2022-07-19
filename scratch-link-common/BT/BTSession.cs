// <copyright file="BTSession.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink.BT;

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Fleck;
using ScratchLink.Extensions;
using ScratchLink.JsonRpc;

/// <summary>
/// Implements the cross-platform portions of a Bluetooth Classic (RFCOMM) session.
/// </summary>
/// <typeparam name="TDevice">Platform-specific device reference. Used to make a device connection.</typeparam>
/// <typeparam name="TDeviceId">Platform-specific device address. Used for tracking device discovery records.</typeparam>
internal abstract class BTSession<TDevice, TDeviceId> : Session
{
    /// <summary>
    /// PIN code for auto-pairing.
    /// </summary>
    protected const string AutoPairingCode = "0000";

    private readonly Dictionary<TDeviceId, string> deviceIdToString = new ();
    private readonly Dictionary<string, TDevice> availableDevices = new ();

    /// <summary>
    /// Initializes a new instance of the <see cref="BTSession{TDevice, TDeviceId}"/> class.
    /// </summary>
    /// <inheritdoc cref="Session.Session(IWebSocketConnection)"/>
    public BTSession(IWebSocketConnection webSocket)
        : base(webSocket)
    {
        this.Handlers["discover"] = this.HandleDiscover;
        this.Handlers["connect"] = this.HandleConnect;
        this.Handlers["send"] = this.HandleSend;
    }

    /// <summary>
    /// Implement the JSON-RPC "discover" request to search for peripherals which match the device class information
    /// provided in the parameters. Valid in the initial state; transitions to discovery state on success.
    /// </summary>
    /// <param name="methodName">The name of the method being called ("discover").</param>
    /// <param name="args">
    /// JSON object containing two integer properties named <c>majorDeviceClass</c> and <c>minorDeviceClass</c>.
    /// </param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    protected Task<object> HandleDiscover(string methodName, JsonElement? args)
    {
        var majorDeviceClass = args?.TryGetProperty("majorDeviceClass")?.GetByte();
        var minorDeviceClass = args?.TryGetProperty("minorDeviceClass")?.GetByte();

        if (majorDeviceClass == null || minorDeviceClass == null)
        {
            throw new JsonRpc2Exception(JsonRpc2Error.InvalidParams("majorDeviceClass and minorDeviceClass required"));
        }

        this.availableDevices.Clear();
        return this.DoDiscover((byte)majorDeviceClass, (byte)minorDeviceClass);
    }

    /// <summary>
    /// Platform-specific implementation for peripheral device discovery.
    /// </summary>
    /// <param name="majorDeviceClass">Discover peripherals with this major device class.</param>
    /// <param name="minorDeviceClass">Discover peripherals with this minor device class.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    protected abstract Task<object> DoDiscover(byte majorDeviceClass, byte minorDeviceClass);

    /// <summary>
    /// Implement the JSON-RPC "connect" request to connect to a particular peripheral.
    /// Valid in the discovery state; transitions to connected state on success.
    /// </summary>
    /// <param name="methodName">The name of the method being called ("connect").</param>
    /// <param name="args">
    /// A JSON object containing the ID of a peripheral found by the most recent discovery request.
    /// </param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    protected Task<object> HandleConnect(string methodName, JsonElement? args)
    {
        var peripheralId = args?.TryGetProperty("peripheralId")?.GetString();

        if (peripheralId == null)
        {
            throw new JsonRpc2Exception(JsonRpc2Error.InvalidParams("peripheralId required"));
        }

        if (!this.availableDevices.TryGetValue(peripheralId, out var device))
        {
            throw new JsonRpc2Exception(JsonRpc2Error.InvalidRequest(string.Format("Device {0} not available for connection", peripheralId)));
        }

        return this.DoConnect(device);
    }

    /// <summary>
    /// Platform-specific implementation for connecting to a peripheral device.
    /// </summary>
    /// <param name="device">The requested device.</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
    protected abstract Task<object> DoConnect(TDevice device);

    /// <summary>
    /// Implement the JSON-RPC "send" request to send data to the connected peripheral.
    /// </summary>
    /// <param name="methodName">The name of the method being called ("send").</param>
    /// <param name="args">
    /// A buffer containing the data to send and optionally the message encoding.
    /// </param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    protected async Task<object> HandleSend(string methodName, JsonElement? args)
    {
        var buffer = EncodingHelpers.DecodeBuffer((JsonElement)args);

        var bytesWritten = await this.DoSend(buffer);

        return bytesWritten;
    }

    /// <summary>
    /// Platform-specific implementation for sending a buffer to the peripheral device.
    /// </summary>
    /// <param name="buffer">The data buffer to send.</param>
    /// <returns>The number of bytes sent.</returns>
    protected abstract Task<int> DoSend(byte[] buffer);

    /// <summary>
    /// Track a discovered device and report it to the client.
    /// </summary>
    /// <param name="device">The platform-specific device reference or record.</param>
    /// <param name="deviceId">The internal system address of this device.</param>
    /// <param name="displayName">A user-friendly name, if possible.</param>
    /// <param name="rssi">A relative signal strength indicator.</param>
    /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
    protected async Task OnDeviceFound(TDevice device, TDeviceId deviceId, string displayName, int rssi)
    {
        var peripheralId = this.GetPeripheralId(deviceId);
        this.availableDevices[peripheralId] = device;

        var message = new BTPeripheralDiscovered
        {
            PeripheralId = peripheralId,
            Name = displayName,
            RSSI = rssi,
        };
        await this.SendRequest("didDiscoverPeripheral", message, this.CancellationToken);
    }

    private string GetPeripheralId(TDeviceId deviceId)
    {
        if (!this.deviceIdToString.TryGetValue(deviceId, out var peripheralId))
        {
            peripheralId = Guid.NewGuid().ToString();
            this.deviceIdToString[deviceId] = peripheralId;
        }

        return peripheralId;
    }

    /// <summary>
    /// JSON-ready class to use when reporting that a peripheral was discovered.
    /// </summary>
    protected class BTPeripheralDiscovered
    {
        /// <summary>
        /// Gets or sets the advertised name of the peripheral.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the ID which can be used for connecting to this peripheral.
        /// </summary>
        [JsonPropertyName("peripheralId")]
        public string PeripheralId { get; set; }

        /// <summary>
        /// Gets or sets the relative signal strength of the advertisement.
        /// </summary>
        [JsonPropertyName("rssi")]
        public int RSSI { get; set; }
    }
}
