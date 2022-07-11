// <copyright file="MacSessionManager.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink.Mac;

using Fleck;
using ScratchLink.Mac.BLE;

/// <summary>
/// Implements the Mac-specific functionality of the SessionManager.
/// </summary>
internal class MacSessionManager : SessionManager
{
    /// <inheritdoc/>
    protected override Session MakeNewSession(IWebSocketConnection webSocket)
    {
        var requestPath = webSocket.ConnectionInfo.Path;
        return requestPath switch
        {
            "/scratch/ble" => new MacBLESession(webSocket),

            // for unrecognized paths, return a base Session for debugging
            _ => new Session(webSocket),
        };
    }
}
