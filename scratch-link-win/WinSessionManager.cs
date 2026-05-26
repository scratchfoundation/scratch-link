// <copyright file="WinSessionManager.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace AluxLabs.Link.Win;

using Fleck;
using AluxLabs.Link.Win.BLE;
using AluxLabs.Link.Win.BT;
using AluxLabs.Link.Win.Serial;

/// <summary>
/// Implements the Windows-specific functionality of the SessionManager.
/// </summary>
internal class WinSessionManager : SessionManager
{
    /// <inheritdoc/>
    protected override Session MakeNewSession(IWebSocketConnection webSocket)
    {
        var requestPath = webSocket.ConnectionInfo.Path;
        return requestPath switch
        {
            "/scratch/ble" => new WinBLESession(webSocket),
            "/scratch/bt" => new WinBTSession(webSocket),
            "/scratch/serial" => new WinSerialSession(webSocket),

            // for unrecognized paths, return a base Session for debugging
            _ => new Session(webSocket),
        };
    }
}
