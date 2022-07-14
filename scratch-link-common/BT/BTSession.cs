// <copyright file="BTSession.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink.BT;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Fleck;
using Microsoft.Extensions.DependencyInjection;
using ScratchLink.Extensions;
using ScratchLink.JsonRpc;

/// <summary>
/// Implements the cross-platform portions of a Bluetooth Classic (RFCOMM) session.
/// </summary>
internal abstract class BTSession : Session
{
    /// <summary>
    /// PIN code for auto-pairing.
    /// </summary>
    protected const string AutoPairingCode = "0000";

    /// <summary>
    /// Initializes a new instance of the <see cref="BTSession"/> class.
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

        // TODO: parse ouiPrefixString to bytes
        var ouiPrefixString = args?.TryGetProperty("ouiPrefix")?.GetString();

        return this.DoDiscover((byte)majorDeviceClass, (byte)minorDeviceClass, null);
    }

    /// <summary>
    /// Platform-specific implementation for peripheral device discovery.
    /// </summary>
    /// <param name="majorDeviceClass">Discover peripherals with this major device class.</param>
    /// <param name="minorDeviceClass">Discover peripherals with this minor device class.</param>
    /// <param name="ouiPrefix">If set, discover peripherals matching this 3-byte OUI prefix.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    protected abstract Task<object> DoDiscover(byte majorDeviceClass, byte minorDeviceClass, byte[] ouiPrefix);

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
        throw new NotImplementedException();
    }

    /// <summary>
    /// Platform-specific implementation for connecting to a peripheral device.
    /// </summary>
    /// <param name="jsonPeripheralId">A JSON element representing a platform-specific peripheral ID.</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
    protected abstract Task<object> DoConnect(JsonElement jsonPeripheralId);

    /// <summary>
    /// Implement the JSON-RPC "send" request to send data to the connected peripheral.
    /// </summary>
    /// <param name="methodName">The name of the method being called ("send").</param>
    /// <param name="args">
    /// A buffer containing the data to send and optionally the message encoding.
    /// </param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    protected Task<object> HandleSend(string methodName, JsonElement? args)
    {
        throw new NotImplementedException();
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
