// <copyright file="MacGattHelpers.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink.Platforms.MacCatalyst;

using System;
using CoreBluetooth;

/// <summary>
/// Implement the MacOS-specific GATT helpers.
/// </summary>
internal class MacGattHelpers : GattHelpers<CBUUID>
{
    /// <inheritdoc/>
    public override CBUUID MakeUUID(string name) =>
        CBUUID.FromString(name);

    /// <inheritdoc/>
    public override CBUUID CanonicalUuid(uint alias) =>
        CBUUID.FromBytes(new[]
        {
            (byte)((alias >> 24) & 0xFF),
            (byte)((alias >> 16) & 0xFF),
            (byte)((alias >> 8) & 0xFF),
            (byte)((alias >> 0) & 0xFF),
        });
}
