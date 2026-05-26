// <copyright file="WinSerialPortInfo.cs" company="ALUX">
// Copyright (c) 2026 ALUX, Inc. All rights reserved.
// Based on scratch-link by Scratch Foundation, licensed under AGPL-3.0-only.
// </copyright>

namespace AluxLabs.Link.Win.Serial;

/// <summary>
/// A single port returned by <see cref="WinSerialPortEnumerator"/>.
/// </summary>
internal class WinSerialPortInfo
{
    /// <summary>
    /// Gets or sets the OS-level port path (e.g. "COM7").
    /// </summary>
    public string Path { get; set; }

    /// <summary>
    /// Gets or sets a user-visible name (typically includes the COM number).
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the USB vendor ID, or null if not parseable.
    /// </summary>
    public int? VendorId { get; set; }

    /// <summary>
    /// Gets or sets the USB product ID, or null if not parseable.
    /// </summary>
    public int? ProductId { get; set; }

    /// <summary>
    /// Gets or sets the raw PNPDeviceID, useful for surprise-removal matching.
    /// </summary>
    public string PnpDeviceId { get; set; }
}
