// <copyright file="MacSessionManager.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace AluxLabs.Link.Mac;

using Fleck;
using AluxLabs.Link.Mac.BLE;
using AluxLabs.Link.Mac.BT;

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
            "/scratch/bt" => new MacBTSession(webSocket),

            // for unrecognized paths, return a base Session for debugging
            _ => new Session(webSocket),
        };
    }
}
