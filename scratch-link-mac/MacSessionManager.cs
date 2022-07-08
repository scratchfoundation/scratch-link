// <copyright file="MacSessionManager.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink.Platforms.MacCatalyst;

using System.Net.WebSockets;
using ScratchLink.Platforms.MacCatalyst.BLE;

/// <summary>
/// Implements the Mac-specific functionality of the SessionManager.
/// </summary>
internal class MacSessionManager : SessionManager
{
    /// <inheritdoc/>
    protected override Session MakeNewSession(WebSocketContext webSocketContext)
    {
        var requestPath = webSocketContext.RequestUri.AbsolutePath;
        return requestPath switch
        {
            "/scratch/ble" => new MacBLESession(webSocketContext),

            // for unrecognized paths, return a base Session for debugging
            _ => new Session(webSocketContext),
        };
    }
}
