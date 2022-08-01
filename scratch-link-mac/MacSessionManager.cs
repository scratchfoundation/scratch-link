// <copyright file="MacSessionManager.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink.Mac;

using Fleck;
using Foundation;
using ScratchLink.Mac.BLE;
using ScratchLink.Mac.BT;

/// <summary>
/// Implements the Mac-specific functionality of the SessionManager.
/// </summary>
internal class MacSessionManager : SessionManager
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MacSessionManager"/> class.
    /// This is only present to told the [Preserve] attribute, which tells the Xamarin linker that
    /// this method will be used in some way that it otherwise would miss.
    /// </summary>
    /// <seealso cref="ScratchLinkApp.Builder.Build"/>
    [Preserve]
    public MacSessionManager()
    {
    }

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
