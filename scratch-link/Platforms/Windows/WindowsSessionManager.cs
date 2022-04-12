// <copyright file="WindowsSessionManager.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink.Platforms.Windows;

using System.Net.WebSockets;

/// <summary>
/// Implements the Windows-specific functionality of the SessionManager.
/// </summary>
internal class WindowsSessionManager : SessionManager
{
    /// <inheritdoc/>
    protected override Session MakeNewSession(WebSocketContext webSocketContext)
    {
        return new Session(webSocketContext);
    }
}
