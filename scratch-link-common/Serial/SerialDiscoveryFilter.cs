// <copyright file="SerialDiscoveryFilter.cs" company="ALUX">
// Copyright (c) 2026 ALUX, Inc. All rights reserved.
// Based on scratch-link by Scratch Foundation, licensed under AGPL-3.0-only.
// </copyright>

namespace AluxLabs.Link.Serial;

/// <summary>
/// A single filter entry passed by the client in a serial "discover" request.
/// A port is reported when it matches any filter in the list.
/// </summary>
internal class SerialDiscoveryFilter
{
    /// <summary>
    /// Gets or sets the USB vendor ID to match, as a decimal integer.
    /// </summary>
    public int? UsbVendorId { get; set; }

    /// <summary>
    /// Gets or sets the USB product ID to match, as a decimal integer.
    /// </summary>
    public int? UsbProductId { get; set; }

    /// <summary>
    /// Gets or sets an optional port path substring to match (e.g. "COM7").
    /// </summary>
    public string PathHint { get; set; }
}
