// <copyright file="SerialSession.cs" company="ALUX">
// Copyright (c) 2026 ALUX, Inc. All rights reserved.
// Based on scratch-link by Scratch Foundation, licensed under AGPL-3.0-only.
// </copyright>

namespace ScratchLink.Serial;

using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Fleck;
using ScratchLink.Extensions;
using ScratchLink.JsonRpc;

/// <summary>
/// Cross-platform base for a USB Serial transport session. Uses Serial-specific
/// notification names (<c>serialDidReceiveData</c>, <c>serialDidDisconnect</c>)
/// so callers cannot confuse Serial events with BLE characteristic events or
/// BT message events.
/// </summary>
/// <typeparam name="TPort">Platform-specific port handle, passed back to <see cref="DoConnect(TPort, SerialOpenParams)"/>.</typeparam>
internal abstract class SerialSession<TPort> : PeripheralSession<TPort, string>
    where TPort : class
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SerialSession{TPort}"/> class.
    /// </summary>
    /// <inheritdoc cref="Session.Session(IWebSocketConnection)"/>
    public SerialSession(IWebSocketConnection webSocket)
        : base(webSocket)
    {
        this.Handlers["discover"] = this.HandleDiscover;
        this.Handlers["write"] = this.HandleWrite;
        this.Handlers["disconnect"] = this.HandleDisconnect;
        this.Handlers["startReading"] = this.HandleStartReading;
        this.Handlers["stopReading"] = this.HandleStopReading;
    }

    /// <summary>
    /// Implement the JSON-RPC "discover" request. Parses the filter list and
    /// kicks off platform-specific enumeration. Discovered ports are streamed
    /// back via <see cref="OnPortDiscovered"/>.
    /// </summary>
    /// <param name="methodName">The name of the method being called ("discover").</param>
    /// <param name="args">A JSON object optionally containing a <c>filters</c> array.</param>
    /// <returns>A <see cref="Task"/> resolving to an empty result; discoveries are streamed via notifications.</returns>
    protected Task<object> HandleDiscover(string methodName, JsonElement? args)
    {
        var filters = ParseFilters(args);
        Trace.WriteLine($"received serial discover request with {filters.Count} filter(s)");

        this.ClearDiscoveredPeripherals();
        return this.DoDiscover(filters);
    }

    /// <summary>
    /// Platform-specific implementation for port discovery. Implementations
    /// should return promptly and stream results via <see cref="OnPortDiscovered"/>.
    /// </summary>
    /// <param name="filters">The filter list from the client. Empty means "match all".</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    protected abstract Task<object> DoDiscover(IReadOnlyList<SerialDiscoveryFilter> filters);

    /// <inheritdoc/>
    protected override Task<object> DoConnect(TPort port, JsonElement? args)
    {
        var openParams = ParseOpenParams(args);
        return this.DoConnect(port, openParams);
    }

    /// <summary>
    /// Platform-specific implementation for opening the given port. On success,
    /// RX should be active and incoming bytes should be reported via
    /// <see cref="DidReceiveData"/>.
    /// </summary>
    /// <param name="port">The port handle previously registered via <see cref="OnPortDiscovered"/>.</param>
    /// <param name="openParams">Open parameters extracted from the connect request.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    protected abstract Task<object> DoConnect(TPort port, SerialOpenParams openParams);

    /// <summary>
    /// Implement the JSON-RPC "write" request. Decodes the base64 payload and
    /// forwards to the platform implementation.
    /// </summary>
    /// <param name="methodName">The name of the method being called ("write").</param>
    /// <param name="args">A JSON object containing <c>message</c> and <c>encoding</c>.</param>
    /// <returns>An object with <c>sentBytes</c> equal to the number of bytes written.</returns>
    protected async Task<object> HandleWrite(string methodName, JsonElement? args)
    {
        if (args == null)
        {
            throw JsonRpc2Error.InvalidParams("write requires a message buffer").ToException();
        }

        var buffer = EncodingHelpers.DecodeBuffer(args.Value);
        var sentBytes = await this.DoWrite(buffer);

        return new Dictionary<string, int> { ["sentBytes"] = sentBytes };
    }

    /// <summary>
    /// Platform-specific implementation for sending bytes to the port.
    /// </summary>
    /// <param name="data">The bytes to send.</param>
    /// <returns>The number of bytes actually written.</returns>
    protected abstract Task<int> DoWrite(byte[] data);

    /// <summary>
    /// Implement the JSON-RPC "disconnect" request. Closes the port without
    /// firing a <c>serialDidDisconnect</c> notification (that is reserved for
    /// external-cause disconnects).
    /// </summary>
    /// <param name="methodName">The name of the method being called ("disconnect").</param>
    /// <param name="args">Unused.</param>
    /// <returns>An empty result.</returns>
    protected async Task<object> HandleDisconnect(string methodName, JsonElement? args)
    {
        await this.DoDisconnect();
        return new Dictionary<string, object>();
    }

    /// <summary>
    /// Platform-specific implementation for closing the port.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    protected abstract Task DoDisconnect();

    /// <summary>
    /// Implement the JSON-RPC "startReading" request. RX is enabled automatically
    /// on connect, so the default implementation is a no-op. Subclasses may
    /// override to re-enable RX after a <c>stopReading</c>.
    /// </summary>
    /// <param name="methodName">The name of the method being called ("startReading").</param>
    /// <param name="args">Unused.</param>
    /// <returns>An empty result.</returns>
    protected virtual Task<object> HandleStartReading(string methodName, JsonElement? args)
    {
        return Task.FromResult<object>(new Dictionary<string, object>());
    }

    /// <summary>
    /// Implement the JSON-RPC "stopReading" request. Default is a no-op.
    /// </summary>
    /// <param name="methodName">The name of the method being called ("stopReading").</param>
    /// <param name="args">Unused.</param>
    /// <returns>An empty result.</returns>
    protected virtual Task<object> HandleStopReading(string methodName, JsonElement? args)
    {
        return Task.FromResult<object>(new Dictionary<string, object>());
    }

    /// <summary>
    /// Report received bytes to the client as a <c>serialDidReceiveData</c>
    /// notification. The payload is base64-encoded.
    /// </summary>
    /// <param name="data">The bytes received.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    protected async Task DidReceiveData(byte[] data)
    {
        var encoded = EncodingHelpers.EncodeBuffer(data, "base64");
        await this.SendNotification("serialDidReceiveData", new SerialDataReceived
        {
            Encoding = "base64",
            Message = encoded,
        });
    }

    /// <summary>
    /// Report an external-cause disconnect to the client as a
    /// <c>serialDidDisconnect</c> notification. Does not fire for the
    /// client-initiated <c>disconnect</c> request.
    /// </summary>
    /// <param name="reason">One of "user", "device", "error", "shutdown".</param>
    /// <param name="message">Optional human-readable detail.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    protected async Task DidDisconnect(string reason, string message = null)
    {
        await this.SendNotification("serialDidDisconnect", new SerialDisconnectMessage
        {
            Reason = reason,
            Message = message,
        });
    }

    /// <summary>
    /// Track a discovered port and report it to the client. Uses
    /// <see cref="PeripheralSession{TPort, String}.RegisterPeripheral"/> to
    /// obtain a session-scoped peripheral ID.
    /// </summary>
    /// <param name="port">Platform-specific port handle.</param>
    /// <param name="path">OS-level port path used as the address (e.g. "COM7").</param>
    /// <param name="displayName">User-visible name, may include the path.</param>
    /// <param name="vendorIdHex">Vendor ID as a hex string (e.g. "0x1A86"), or null.</param>
    /// <param name="productIdHex">Product ID as a hex string (e.g. "0x7523"), or null.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    protected async Task OnPortDiscovered(TPort port, string path, string displayName, string vendorIdHex, string productIdHex)
    {
        var peripheralId = this.RegisterPeripheral(port, path);

        await this.SendNotification("didDiscoverPeripheral", new SerialPortDiscovered
        {
            PeripheralId = peripheralId,
            Name = displayName,
            Path = path,
            VendorId = vendorIdHex,
            ProductId = productIdHex,
            RSSI = 0,
        });
    }

    private static IReadOnlyList<SerialDiscoveryFilter> ParseFilters(JsonElement? args)
    {
        var result = new List<SerialDiscoveryFilter>();

        var filtersElement = args?.TryGetProperty("filters");
        if (filtersElement == null)
        {
            return result;
        }

        if (filtersElement.Value.ValueKind != JsonValueKind.Array)
        {
            throw JsonRpc2Error.InvalidParams("'filters' must be an array").ToException();
        }

        foreach (var item in filtersElement.Value.EnumerateArray())
        {
            result.Add(new SerialDiscoveryFilter
            {
                UsbVendorId = item.TryGetProperty("usbVendorId")?.GetInt32(),
                UsbProductId = item.TryGetProperty("usbProductId")?.GetInt32(),
                PathHint = item.TryGetProperty("pathHint")?.GetString(),
            });
        }

        return result;
    }

    private static SerialOpenParams ParseOpenParams(JsonElement? args)
    {
        var baudRate = args?.TryGetProperty("baudRate")?.GetInt32();
        if (baudRate == null)
        {
            throw JsonRpc2Error.InvalidParams("connect requires baudRate").ToException();
        }

        return new SerialOpenParams
        {
            BaudRate = baudRate.Value,
            DataBits = args?.TryGetProperty("dataBits")?.GetInt32() ?? 8,
            Parity = args?.TryGetProperty("parity")?.GetString() ?? "none",
            StopBits = args?.TryGetProperty("stopBits")?.GetString() ?? "one",
            FlowControl = args?.TryGetProperty("flowControl")?.GetString() ?? "none",
        };
    }

    /// <summary>
    /// Payload of a <c>serialDidReceiveData</c> notification.
    /// </summary>
    protected class SerialDataReceived
    {
        /// <summary>
        /// Gets or sets the encoding identifier; always "base64" for serial RX.
        /// </summary>
        [JsonPropertyName("encoding")]
        public string Encoding { get; set; }

        /// <summary>
        /// Gets or sets the encoded payload.
        /// </summary>
        [JsonPropertyName("message")]
        public string Message { get; set; }
    }

    /// <summary>
    /// Payload of a <c>serialDidDisconnect</c> notification.
    /// </summary>
    protected class SerialDisconnectMessage
    {
        /// <summary>
        /// Gets or sets the disconnect reason: "user", "device", "error", or "shutdown".
        /// </summary>
        [JsonPropertyName("reason")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Reason { get; set; }

        /// <summary>
        /// Gets or sets an optional human-readable detail message.
        /// </summary>
        [JsonPropertyName("message")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Message { get; set; }
    }

    /// <summary>
    /// Payload of a <c>didDiscoverPeripheral</c> notification on the serial transport.
    /// </summary>
    protected class SerialPortDiscovered
    {
        /// <summary>
        /// Gets or sets the session-scoped peripheral ID used by the client to connect.
        /// </summary>
        [JsonPropertyName("peripheralId")]
        public string PeripheralId { get; set; }

        /// <summary>
        /// Gets or sets the user-visible name of the port.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the OS-level port path (e.g. "COM7").
        /// </summary>
        [JsonPropertyName("path")]
        public string Path { get; set; }

        /// <summary>
        /// Gets or sets the USB vendor ID as a hex string (e.g. "0x1A86"), if known.
        /// </summary>
        [JsonPropertyName("vendorId")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string VendorId { get; set; }

        /// <summary>
        /// Gets or sets the USB product ID as a hex string (e.g. "0x7523"), if known.
        /// </summary>
        [JsonPropertyName("productId")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string ProductId { get; set; }

        /// <summary>
        /// Gets or sets a placeholder RSSI value for cross-transport message compatibility.
        /// </summary>
        [JsonPropertyName("rssi")]
        public int RSSI { get; set; }
    }
}
