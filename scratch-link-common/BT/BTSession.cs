// <copyright file="BTSession.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink.BT;

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Fleck;
using ScratchLink.Extensions;
using ScratchLink.JsonRpc;

/// <summary>
/// Implements the cross-platform portions of a Bluetooth Classic (RFCOMM) session.
/// </summary>
/// <inheritdoc cref="PeripheralSession{TPeripheral, TPeripheralAddress}"/>
internal abstract class BTSession<TPeripheral, TPeripheralAddress> : PeripheralSession<TPeripheral, TPeripheralAddress>
    where TPeripheral : class
{
    /// <summary>
    /// PIN code for auto-pairing.
    /// </summary>
    protected const string AutoPairingCode = "0000";

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
            throw JsonRpc2Error.InvalidParams("majorDeviceClass and minorDeviceClass required").ToException();
        }

        this.ClearPeripherals();
        return this.DoDiscover((byte)majorDeviceClass, (byte)minorDeviceClass);
    }

    /// <summary>
    /// Platform-specific implementation for peripheral device discovery.
    /// </summary>
    /// <param name="majorDeviceClass">Discover peripherals with this major device class.</param>
    /// <param name="minorDeviceClass">Discover peripherals with this minor device class.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    protected abstract Task<object> DoDiscover(byte majorDeviceClass, byte minorDeviceClass);

    /// <inheritdoc/>
    protected override Task<object> DoConnect(TPeripheral peripheral, JsonElement? args)
    {
        var pinString = args?.TryGetProperty("pin")?.GetString();

        return this.DoConnect(peripheral, pinString);
    }

    /// <summary>
    /// Platform-specific implementation for connecting to a BT peripheral device.
    /// </summary>
    /// <param name="peripheral">The requested BT peripheral device.</param>
    /// <param name="pinString">The PIN code, if provided by the client. Otherwise, null.</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
    protected abstract Task<object> DoConnect(TPeripheral peripheral, string pinString);

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
    /// Report to the client that data was received from the peripheral device.
    /// </summary>
    /// <param name="buffer">The bytes received.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    protected async Task DidReceiveMessage(byte[] buffer)
    {
        var messageData = EncodingHelpers.EncodeBuffer(buffer, "base64");

        var message = new BTMessageReceived
        {
            Encoding = "base64",
            Message = messageData,
        };
        await this.SendNotification("didReceiveMessage", message);
    }

    /// <summary>
    /// Track a discovered device and report it to the client.
    /// </summary>
    /// <param name="peripheral">The platform-specific device reference or record.</param>
    /// <param name="peripheralAddress">The internal system address of this device.</param>
    /// <param name="displayName">A user-friendly name, if possible.</param>
    /// <param name="rssi">A relative signal strength indicator.</param>
    /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
    protected async Task OnPeripheralDiscovered(TPeripheral peripheral, TPeripheralAddress peripheralAddress, string displayName, int rssi)
    {
        var peripheralId = this.RegisterPeripheral(peripheral, peripheralAddress);

        var message = new BTPeripheralDiscovered
        {
            PeripheralId = peripheralId,
            Name = displayName,
            RSSI = rssi,
        };

        await this.SendRequest("didDiscoverPeripheral", message);
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

    /// <summary>
    /// JSON-ready class to use when reporting that data was received from a peripheral.
    /// </summary>
    protected class BTMessageReceived
    {
        /// <summary>
        /// Gets or sets an optional encoding specifier (like "base64").
        /// If this is missing, <see cref="Message"/> must be a UTF-8 string.
        /// </summary>
        [JsonPropertyName("encoding")]
        public string Encoding { get; set; }

        /// <summary>
        /// Gets or sets the message content, encoded as specified in the <see cref="Encoding"/> property.
        /// </summary>
        [JsonPropertyName("message")]
        public string Message { get; set; }
    }
}
