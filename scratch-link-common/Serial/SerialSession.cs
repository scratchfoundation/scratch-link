// <copyright file="SerialSession.cs" company="ALUX">
// Copyright (c) 2026 ALUX, Inc. All rights reserved.
// Based on scratch-link by Scratch Foundation, licensed under AGPL-3.0-only.
// </copyright>

namespace ScratchLink.Serial;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
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
    // Yield keep-alive while RX is active so the peripheral can finish unsolicited notifications (e.g. BLE link-loss).
    private const int KeepAliveRxYieldMs = 200;

    // Force keep-alive once the client has been idle this long, even if RX is still flowing, to stay under the device's 1 s RX timeout.
    private const int KeepAliveTxIdleMs = 900;

    // Pause keep-alive once RX has stalled this long. Dongles defer BLE link-loss notifications while servicing PC commands;
    // pausing gives the dongle the idle window it needs to push the notification.
    private const int KeepAliveRxStallMs = 500;

    // Serializes DoWrite calls so two writes never overlap and corrupt the stream.
    private readonly SemaphoreSlim writeSemaphore = new SemaphoreSlim(1, 1);

    // Guards keep-alive lifecycle fields shared with the timer callback.
    private readonly object stateLock = new object();

    private byte[] lastSentData;
    private Timer keepAliveTimer;
    private int keepAliveIntervalMs;
    private bool keepAliveActive;

    // 64-bit timestamps in DateTime.UtcNow.Ticks; accessed via Interlocked to keep reads/writes atomic on 32-bit runtimes.
    private long lastRxTicks;
    private long lastClientTxTicks;
    private long lastKeepAliveSentTicks;

    // volatile so threads outside stateLock see the latest value on the hot path.
    private volatile bool wireTrace;

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
        this.Handlers["setKeepAlive"] = this.HandleSetKeepAlive;
        this.Handlers["triggerDTRReset"] = this.HandleTriggerDTRReset;
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
        this.wireTrace = openParams.WireTrace;
        if (this.wireTrace)
        {
            Trace.WriteLine("wire-trace: enabled for this session");
        }

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
    /// JSON-RPC <c>write</c> handler. Caches the payload as the most recent TX packet
    /// and resets the keep-alive timer so resends are suppressed during active bursts.
    /// </summary>
    /// <param name="methodName">Dispatched method name.</param>
    /// <param name="args">Decoded request params.</param>
    /// <returns><c>sentBytes</c> wrapper.</returns>
    protected async Task<object> HandleWrite(string methodName, JsonElement? args)
    {
        if (args == null)
        {
            throw JsonRpc2Error.InvalidParams("write requires a message buffer").ToException();
        }

        var buffer = EncodingHelpers.DecodeBuffer(args.Value);

        await this.writeSemaphore.WaitAsync().ConfigureAwait(false);
        int sentBytes;
        try
        {
            lock (this.stateLock)
            {
                this.lastSentData = buffer;
            }

            Interlocked.Exchange(ref this.lastClientTxTicks, DateTime.UtcNow.Ticks);
            this.ResetKeepAliveTimer();

            if (this.wireTrace)
            {
                Trace.WriteLine($"wire-trace TX {buffer.Length}B {FormatHex(buffer)}");
            }

            sentBytes = await this.DoWrite(buffer).ConfigureAwait(false);
        }
        finally
        {
            this.writeSemaphore.Release();
        }

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
    /// JSON-RPC <c>setKeepAlive</c> handler. <c>intervalMs</c>: positive (re)starts, null/0/negative disables.
    /// Idempotent: stop-then-start so repeated calls leave only one timer alive.
    /// Response echoes the applied interval (null when disabled).
    /// </summary>
    /// <param name="methodName">Dispatched method name.</param>
    /// <param name="args">Decoded request params.</param>
    /// <returns>Echo of the applied interval.</returns>
    protected Task<object> HandleSetKeepAlive(string methodName, JsonElement? args)
    {
        int? requested = null;
        if (args != null)
        {
            var prop = args.Value.TryGetProperty("intervalMs");
            if (prop.HasValue && prop.Value.ValueKind != JsonValueKind.Null)
            {
                requested = prop.Value.GetInt32();
            }
        }

        // Stop-then-start makes the call idempotent regardless of current state.
        this.StopKeepAlive();

        int? applied = null;
        if (requested.HasValue && requested.Value > 0)
        {
            this.StartKeepAlive(requested.Value);
            applied = requested.Value;
        }

        var appliedText = applied?.ToString() ?? "null";
        Trace.WriteLine($"keep-alive: setKeepAlive applied intervalMs={appliedText}");
        return Task.FromResult<object>(new Dictionary<string, object> { ["intervalMs"] = applied });
    }

    /// <summary>
    /// JSON-RPC <c>triggerDTRReset</c> handler. Acquires the write semaphore so no
    /// write overlaps the DTR pulse sequence, then delegates to <see cref="DoTriggerDTRReset"/>.
    /// </summary>
    /// <param name="methodName">Dispatched method name.</param>
    /// <param name="args">Unused.</param>
    /// <returns>An empty result, returned after the DTR pulse sequence completes.</returns>
    protected async Task<object> HandleTriggerDTRReset(string methodName, JsonElement? args)
    {
        Trace.WriteLine("triggerDTRReset: executing DTR pulse");

        await this.writeSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            await this.DoTriggerDTRReset().ConfigureAwait(false);
        }
        finally
        {
            this.writeSemaphore.Release();
        }

        return new Dictionary<string, object>();
    }

    /// <summary>
    /// Platform-specific implementation for the DTR reset pulse. Asserts DTR for 50 ms
    /// then releases it. Must throw <see cref="JsonRpc2Exception"/> on failure so the
    /// caller receives a well-formed JSON-RPC error response.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    protected abstract Task DoTriggerDTRReset();

    /// <summary>
    /// Report received bytes to the client as a <c>serialDidReceiveData</c>
    /// notification. The payload is base64-encoded.
    /// </summary>
    /// <param name="data">The bytes received.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    protected async Task DidReceiveData(byte[] data)
    {
        Interlocked.Exchange(ref this.lastRxTicks, DateTime.UtcNow.Ticks);

        if (this.wireTrace)
        {
            Trace.WriteLine($"wire-trace RX {data.Length}B {FormatHex(data)}");
        }

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

    /// <summary>
    /// Start the keep-alive timer at <paramref name="keepAliveIntervalMs"/>. Null or non-positive disables.
    /// No-op if already running.
    /// </summary>
    /// <param name="keepAliveIntervalMs">Interval in milliseconds; null/non-positive disables.</param>
    protected void StartKeepAlive(int? keepAliveIntervalMs)
    {
        if (keepAliveIntervalMs == null || keepAliveIntervalMs.Value <= 0)
        {
            return;
        }

        var interval = keepAliveIntervalMs.Value;

        // Seed timestamps to "now" so the first tick doesn't see zero-initialized fields as ancient activity
        // (which would otherwise mis-trigger the RX-stall guard before any RX has arrived).
        var nowTicks = DateTime.UtcNow.Ticks;
        Interlocked.Exchange(ref this.lastRxTicks, nowTicks);
        Interlocked.Exchange(ref this.lastClientTxTicks, nowTicks);
        Interlocked.Exchange(ref this.lastKeepAliveSentTicks, nowTicks);

        lock (this.stateLock)
        {
            if (this.keepAliveActive)
            {
                Trace.WriteLine("keep-alive: StartKeepAlive called while already active; ignoring");
                return;
            }

            this.keepAliveIntervalMs = interval;
            this.keepAliveActive = true;
            this.keepAliveTimer = new Timer(this.OnKeepAliveTick, null, interval, interval);
        }

        Trace.WriteLine($"keep-alive: started ({interval}ms)");
    }

    /// <summary>
    /// Stop the keep-alive timer and block until any in-flight tick finishes.
    /// Safe to call repeatedly.
    /// </summary>
    protected void StopKeepAlive()
    {
        Timer toDispose;
        lock (this.stateLock)
        {
            if (!this.keepAliveActive)
            {
                return;
            }

            this.keepAliveActive = false;
            toDispose = this.keepAliveTimer;
            this.keepAliveTimer = null;
        }

        if (toDispose != null)
        {
            // Block so no resend races a subsequent disconnect or port disposal.
            using var waitHandle = new ManualResetEvent(false);
            if (toDispose.Dispose(waitHandle))
            {
                waitHandle.WaitOne();
            }
        }

        Trace.WriteLine("keep-alive: stopped");
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing && !this.DisposedValue)
        {
            // Order matters: StopKeepAlive waits for in-flight ticks before we dispose the semaphore they use.
            this.StopKeepAlive();
            this.writeSemaphore.Dispose();
        }

        base.Dispose(disposing);
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
            PeripheralType = args?.TryGetProperty("peripheralType")?.GetString(),
            KeepAliveIntervalMs = args?.TryGetProperty("keepAliveIntervalMs")?.GetInt32(),
            WireTrace = args?.TryGetProperty("wireTrace")?.GetBoolean() ?? false,
        };
    }

    /// <summary>
    /// Hex preview for diagnostic logs, capped at <paramref name="maxBytes"/> with a tail marker.
    /// </summary>
    private static string FormatHex(byte[] data, int maxBytes = 256)
    {
        if (data == null || data.Length == 0)
        {
            return string.Empty;
        }

        var take = data.Length <= maxBytes ? data.Length : maxBytes;
        var sb = new System.Text.StringBuilder(take * 3);
        for (var i = 0; i < take; i++)
        {
            if (i > 0)
            {
                sb.Append(' ');
            }

            sb.Append(data[i].ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
        }

        if (data.Length > take)
        {
            sb.Append($" …(+{data.Length - take}B)");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Push the next tick one full interval forward so that ongoing write bursts suppress the resend.
    /// </summary>
    private void ResetKeepAliveTimer()
    {
        Timer timer;
        int interval;
        lock (this.stateLock)
        {
            if (!this.keepAliveActive || this.keepAliveTimer == null)
            {
                return;
            }

            timer = this.keepAliveTimer;
            interval = this.keepAliveIntervalMs;
        }

        try
        {
            timer.Change(interval, interval);
        }
        catch (ObjectDisposedException)
        {
            // Raced with StopKeepAlive.
        }
    }

    private async void OnKeepAliveTick(object state)
    {
        byte[] data;
        lock (this.stateLock)
        {
            if (!this.keepAliveActive)
            {
                return;
            }

            data = this.lastSentData;
        }

        if (data == null || data.Length == 0 || !this.IsConnected)
        {
            return;
        }

        // msSinceAnyTx tracks "any TX we issued recently" — client write OR previous keep-alive — so a single forced
        // keep-alive resets the budget instead of unlocking the 33 ms cadence permanently.
        var nowTicks = DateTime.UtcNow.Ticks;
        var msSinceRx = (nowTicks - Interlocked.Read(ref this.lastRxTicks)) / TimeSpan.TicksPerMillisecond;
        var msSinceClientTx = (nowTicks - Interlocked.Read(ref this.lastClientTxTicks)) / TimeSpan.TicksPerMillisecond;
        var msSinceKeepAlive = (nowTicks - Interlocked.Read(ref this.lastKeepAliveSentTicks)) / TimeSpan.TicksPerMillisecond;
        var msSinceAnyTx = Math.Min(msSinceClientTx, msSinceKeepAlive);

        // RX stalled too long: peripheral is silent (likely BLE link loss). Pause so the dongle can push its notification.
        if (msSinceRx > KeepAliveRxStallMs)
        {
            return;
        }

        // Yield to in-flight RX so we don't trample its TX window; capped by KeepAliveTxIdleMs to stay under the ~1 s device timeout.
        if (msSinceRx < KeepAliveRxYieldMs && msSinceAnyTx < KeepAliveTxIdleMs)
        {
            return;
        }

        // WaitAsync(0) makes the tick idle-only: during a write burst the semaphore is busy and we no-op.
        if (!await this.writeSemaphore.WaitAsync(0).ConfigureAwait(false))
        {
            return;
        }

        try
        {
            if (!this.keepAliveActive || !this.IsConnected)
            {
                return;
            }

            if (this.wireTrace)
            {
                Trace.WriteLine($"wire-trace TX(keep-alive) {data.Length}B {FormatHex(data)}");
            }

            await this.DoWrite(data).ConfigureAwait(false);
            Interlocked.Exchange(ref this.lastKeepAliveSentTicks, DateTime.UtcNow.Ticks);
        }
        catch (ObjectDisposedException)
        {
            // Session disposed mid-tick.
        }
        catch (Exception e)
        {
            // async-void Timer callback: an escaped exception terminates the process, so log and move on.
            Trace.WriteLine($"keep-alive: resend failed: {e.GetType().Name}: {e.Message}");
        }
        finally
        {
            try
            {
                this.writeSemaphore.Release();
            }
            catch (ObjectDisposedException)
            {
                // Semaphore disposed during shutdown.
            }
        }
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
