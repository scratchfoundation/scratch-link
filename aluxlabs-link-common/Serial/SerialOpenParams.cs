// <copyright file="SerialOpenParams.cs" company="ALUX">
// Copyright (c) 2026 ALUX, Inc. All rights reserved.
// Based on scratch-link by Scratch Foundation, licensed under AGPL-3.0-only.
// </copyright>

namespace AluxLabs.Link.Serial;

/// <summary>
/// Parameters extracted from a serial "connect" request.
/// </summary>
internal class SerialOpenParams
{
    /// <summary>
    /// Gets or sets the baud rate. Required.
    /// </summary>
    public int BaudRate { get; set; }

    /// <summary>
    /// Gets or sets the data bits. Defaults to 8.
    /// </summary>
    public int DataBits { get; set; }

    /// <summary>
    /// Gets or sets the parity setting: "none", "even", or "odd". Defaults to "none".
    /// </summary>
    public string Parity { get; set; }

    /// <summary>
    /// Gets or sets the stop bits: "one", "onePointFive", or "two". Defaults to "one".
    /// </summary>
    public string StopBits { get; set; }

    /// <summary>
    /// Gets or sets the flow control: "none", "rtsCts", or "xonXoff". Defaults to "none".
    /// </summary>
    public string FlowControl { get; set; }

    /// <summary>
    /// Gets or sets the client-supplied peripheral type identifier. Optional; used for diagnostic logging only.
    /// </summary>
    public string PeripheralType { get; set; }

    /// <summary>
    /// Gets or sets the keep-alive interval in milliseconds. If positive, the most recently
    /// sent TX packet is automatically re-sent at this interval while the line is idle.
    /// Null or non-positive disables the timer.
    /// </summary>
    public int? KeepAliveIntervalMs { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether wire-level TX/RX hex dumps are emitted via
    /// <see cref="System.Diagnostics.Trace"/>. Diagnostic only; off by default.
    /// </summary>
    public bool WireTrace { get; set; }
}
