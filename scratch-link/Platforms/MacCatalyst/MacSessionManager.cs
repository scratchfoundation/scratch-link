// <copyright file="MacSessionManager.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink.Platforms.MacCatalyst;

using System.Net.WebSockets;

/// <summary>
/// Implements the Mac-specific functionality of the SessionManager.
/// </summary>
internal class MacSessionManager : SessionManager
{
    /// <inheritdoc/>
    protected override Session MakeNewSession(WebSocketContext webSocketContext)
    {
        return new Session(webSocketContext);
    }
}
