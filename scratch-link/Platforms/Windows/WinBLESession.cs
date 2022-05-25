// <copyright file="WinBLESession.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink.Platforms.Windows;

using System.Net.WebSockets;

/// <summary>
/// Implements a BLE session on Windows.
/// </summary>
internal class WinBLESession : BLESession<Guid?>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WinBLESession"/> class.
    /// </summary>
    /// <inheritdoc cref="BLESession.BLESession(WebSocketContext)"/>
    public WinBLESession(WebSocketContext context)
        : base(context)
    {
    }
}
