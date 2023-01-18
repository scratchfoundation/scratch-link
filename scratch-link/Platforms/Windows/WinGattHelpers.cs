// <copyright file="WinGattHelpers.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink.Platforms.Windows;

using System;

/// <summary>
/// Implement the Windows-specific GATT helpers.
/// </summary>
internal class WinGattHelpers : GattHelpers<Guid>
{
    /// <inheritdoc/>
    public override Guid MakeUUID(string name) =>
        new Guid(name);

    /// <inheritdoc/>
    public override Guid CanonicalUuid(uint alias) =>
        new Guid(alias, 0x0000, 0x1000, 0x80, 0x00, 0x00, 0x80, 0x5f, 0x9b, 0x34, 0xfb);
}
