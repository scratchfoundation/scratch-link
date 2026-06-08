// <copyright file="WinSerialSession.cs" company="ALUX">
// Copyright (c) 2026 ALUX, Inc. All rights reserved.
// Based on scratch-link by Scratch Foundation, licensed under AGPL-3.0-only.
// </copyright>

namespace AluxLabs.Link.Win.Serial;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using Fleck;
using AluxLabs.Link.JsonRpc;
using AluxLabs.Link.Serial;

/// <summary>
/// Implements a USB Serial session on Windows using <see cref="SerialPort"/>
/// for I/O and WMI for VID/PID-aware port discovery.
/// </summary>
internal class WinSerialSession : SerialSession<WinSerialPortInfo>
{
    // Serializes Read and Write on the same handle: concurrent calls trigger a TimeoutException burst on the read side with CH340/CP210x drivers.
    private readonly object ioLock = new object();

    private SerialPort port;
    private CancellationTokenSource rxCts;
    private Task rxLoop;

    /// <summary>
    /// Initializes a new instance of the <see cref="WinSerialSession"/> class.
    /// </summary>
    /// <param name="webSocket">The WebSocket connection for this session.</param>
    public WinSerialSession(IWebSocketConnection webSocket)
        : base(webSocket)
    {
    }

    /// <inheritdoc/>
    protected override bool IsConnected => this.port != null && this.port.IsOpen;

    /// <inheritdoc/>
    protected override async Task<IReadOnlyList<EnumeratedPort>> DoEnumeratePorts(IReadOnlyList<SerialDiscoveryFilter> filters)
    {
        var ports = await Task.Run(() => WinSerialPortEnumerator.Query(filters));

        var result = new List<EnumeratedPort>(ports.Count);
        foreach (var portInfo in ports)
        {
            var vendorHex = portInfo.VendorId.HasValue
                ? $"0x{portInfo.VendorId.Value:X4}"
                : null;
            var productHex = portInfo.ProductId.HasValue
                ? $"0x{portInfo.ProductId.Value:X4}"
                : null;

            result.Add(new EnumeratedPort(portInfo, portInfo.Path, portInfo.DisplayName, vendorHex, productHex));
        }

        return result;
    }

    /// <inheritdoc/>
    protected override Task<object> DoConnect(WinSerialPortInfo info, SerialOpenParams openParams)
    {
        if (this.port != null)
        {
            throw JsonRpc2Error.InvalidRequest("already connected").ToException();
        }

        if (!string.IsNullOrEmpty(openParams.PeripheralType))
        {
            Trace.WriteLine($"Connecting to {info.Path} with peripheral type: {openParams.PeripheralType}");
        }

        try
        {
            this.port = new SerialPort(info.Path)
            {
                BaudRate = openParams.BaudRate,
                DataBits = openParams.DataBits,
                Parity = MapParity(openParams.Parity),
                StopBits = MapStopBits(openParams.StopBits),
                Handshake = MapFlowControl(openParams.FlowControl),
                ReadTimeout = 500,
                WriteTimeout = SerialPort.InfiniteTimeout,
                // CH340 + codetinker firmware treats DTR/RTS transitions as a reset signal;
                // pin them low explicitly so SerialPort.Open does not toggle them.
                DtrEnable = false,
                RtsEnable = false,
            };
            this.port.Open();
        }
        catch (Exception e)
        {
            Trace.WriteLine($"Failed to open serial port {info.Path}: {e}");
            this.CloseConnectionSilently();
            throw JsonRpc2Error.ApplicationError($"could not open serial port {info.Path}: {e.Message}").ToException();
        }

        this.rxCts = new CancellationTokenSource();
        var token = this.rxCts.Token;
        this.rxLoop = Task.Run(() => this.ReadLoop(token));

        this.StartKeepAlive(openParams.KeepAliveIntervalMs);

        return Task.FromResult<object>(new Dictionary<string, object>());
    }

    /// <inheritdoc/>
    protected override async Task<int> DoWrite(byte[] data)
    {
        var currentPort = this.port;
        if (currentPort == null || !currentPort.IsOpen)
        {
            throw JsonRpc2Error.InvalidRequest("cannot write when not connected").ToException();
        }

        // Sync Write under ioLock; see ioLock declaration for why. Task.Run keeps the async signature off the dispatcher thread.
        try
        {
            await Task.Run(() =>
            {
                lock (this.ioLock)
                {
                    if (!currentPort.IsOpen)
                    {
                        throw new InvalidOperationException("port closed");
                    }

                    currentPort.Write(data, 0, data.Length);
                }
            }).ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            throw JsonRpc2Error.InternalError("write failed: port was disposed").ToException();
        }
        catch (InvalidOperationException)
        {
            // Port was closed between the IsOpen check above and Write.
            throw JsonRpc2Error.InvalidRequest("cannot write when not connected").ToException();
        }
        catch (IOException e)
        {
            throw JsonRpc2Error.InternalError($"write failed: {e.Message}").ToException();
        }

        return data.Length;
    }

    /// <inheritdoc/>
    protected override async Task DoDisconnect()
    {
        this.StopKeepAlive();
        var loop = this.rxLoop;
        this.CloseConnectionSilently();

        if (loop != null)
        {
            try
            {
                await loop;
            }
            catch
            {
                // swallow: the loop's own error path already reported anything client-visible
            }
        }
    }

    /// <inheritdoc/>
    protected override async Task DoTriggerDTRReset()
    {
        var currentPort = this.port;
        if (currentPort == null || !currentPort.IsOpen)
        {
            throw JsonRpc2Error.InternalError("No connected peripheral").ToException();
        }

        try
        {
            currentPort.DtrEnable = true;
            await Task.Delay(50).ConfigureAwait(false);
            currentPort.DtrEnable = false;
        }
        catch (Exception e) when (e is ObjectDisposedException || e is InvalidOperationException || e is IOException)
        {
            throw JsonRpc2Error.InternalError($"setSignals failed: {e.Message}").ToException();
        }
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        this.CloseConnectionSilently();
    }

    private static Parity MapParity(string parity) =>
        (parity ?? "none").ToLowerInvariant() switch
        {
            "none" => Parity.None,
            "even" => Parity.Even,
            "odd" => Parity.Odd,
            "mark" => Parity.Mark,
            "space" => Parity.Space,
            _ => throw JsonRpc2Error.InvalidParams($"unsupported parity: {parity}").ToException(),
        };

    private static StopBits MapStopBits(string stopBits) =>
        (stopBits ?? "one").ToLowerInvariant() switch
        {
            "one" => StopBits.One,
            "onepointfive" => StopBits.OnePointFive,
            "two" => StopBits.Two,
            _ => throw JsonRpc2Error.InvalidParams($"unsupported stopBits: {stopBits}").ToException(),
        };

    private static Handshake MapFlowControl(string flow) =>
        (flow ?? "none").ToLowerInvariant() switch
        {
            "none" => Handshake.None,
            "rtscts" => Handshake.RequestToSend,
            "xonxoff" => Handshake.XOnXOff,
            _ => throw JsonRpc2Error.InvalidParams($"unsupported flowControl: {flow}").ToException(),
        };

    private void ReadLoop(CancellationToken ct)
    {
        var buf = new byte[4096];

        while (!ct.IsCancellationRequested)
        {
            var currentPort = this.port;
            if (currentPort == null || !currentPort.IsOpen)
            {
                break;
            }

            // Poll BytesToRead so Read is only called when there's data to drain — keeps ioLock hold time minimal.
            int available;
            try
            {
                available = currentPort.BytesToRead;
            }
            catch (ObjectDisposedException)
            {
                // Derives from InvalidOperationException; catch first.
                break;
            }
            catch (InvalidOperationException)
            {
                break;
            }
            catch (IOException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (IOException e)
            {
                Trace.WriteLine($"Serial BytesToRead IOException on {currentPort.PortName}: {e.Message}");
                _ = this.DidDisconnect("device", e.Message);
                this.CloseConnectionSilently();
                break;
            }

            if (available <= 0)
            {
                // Wait on ct.WaitHandle so cancellation wakes the loop immediately; otherwise sleep 10ms.
                try
                {
                    if (ct.WaitHandle.WaitOne(10))
                    {
                        break;
                    }
                }
                catch (ObjectDisposedException)
                {
                    break;
                }

                continue;
            }

            int n;
            try
            {
                lock (this.ioLock)
                {
                    if (!currentPort.IsOpen)
                    {
                        break;
                    }

                    n = currentPort.Read(buf, 0, Math.Min(available, buf.Length));
                }
            }
            catch (TimeoutException)
            {
                // Defensive: BytesToRead gate should prevent this.
                continue;
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (IOException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (IOException e)
            {
                Trace.WriteLine($"Serial read IOException on {currentPort.PortName}: {e.Message}");
                _ = this.DidDisconnect("device", e.Message);
                this.CloseConnectionSilently();
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (InvalidOperationException)
            {
                break;
            }
            catch (Exception e)
            {
                Trace.WriteLine($"Unexpected serial read error: {e}");
                _ = this.DidDisconnect("error", e.Message);
                this.CloseConnectionSilently();
                break;
            }

            if (n <= 0)
            {
                continue;
            }

            var data = new byte[n];
            Buffer.BlockCopy(buf, 0, data, 0, n);
            _ = this.DidReceiveData(data);
        }
    }

    private void CloseConnectionSilently()
    {
        var localCts = this.rxCts;
        this.rxCts = null;

        try
        {
            localCts?.Cancel();
        }
        catch
        {
            // ignored
        }

        var localPort = this.port;
        this.port = null;

        if (localPort != null)
        {
            try
            {
                if (localPort.IsOpen)
                {
                    localPort.Close();
                }
            }
            catch (Exception e)
            {
                Trace.WriteLine($"Error closing serial port: {e}");
            }

            try
            {
                localPort.Dispose();
            }
            catch
            {
                // ignored
            }
        }

        try
        {
            localCts?.Dispose();
        }
        catch
        {
            // ignored
        }
    }
}
