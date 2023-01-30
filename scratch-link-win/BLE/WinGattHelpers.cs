// <copyright file="WinGattHelpers.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink.Win.BLE;

using ScratchLink.BLE;

/// <summary>
/// Implement the Windows-specific GATT helpers.
/// </summary>
internal class WinGattHelpers : GattHelpers<Guid>
{
    /// <inheritdoc/>
    public override Guid MakeUUID(string name) =>
        new (name);

    /// <inheritdoc/>
    public override Guid CanonicalUuid(uint alias) =>
        new (alias, 0x0000, 0x1000, 0x80, 0x00, 0x00, 0x80, 0x5f, 0x9b, 0x34, 0xfb);
}
