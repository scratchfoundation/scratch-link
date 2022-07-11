// <copyright file="WebSocketListener.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Fleck;

/// <summary>
/// Listen for WebSocket connections and direct them to service handlers.
/// </summary>
internal class WebSocketListener
{
    private readonly CancellationTokenSource cts = new ();

    private WebSocketServer server;

    /// <summary>
    /// Gets or sets the action which will be called when the listener receives a WebSocket connection.
    /// </summary>
    public Action<IWebSocketConnection> OnWebSocketConnection { get; set; }

    /// <summary>
    /// Start listening for connections. If already listening, stop and restart with the new prefix list.
    /// </summary>
    /// <param name="location">
    /// The list of WS URL to listen on.
    /// <example><code>
    /// ws://0.0.0.0:1234/
    /// ws://127.0.0.1/
    /// </code></example>
    /// </param>
    public void Start(string location)
    {
        if (this.server != null)
        {
            this.server.ListenerSocket.Close();
            this.server.Dispose();
        }

        this.server = new WebSocketServer(location);
        this.server.ListenerSocket.NoDelay = true; // disable Nagle's algorithm

        this.server.Start(socket =>
        {
            if (this.cts.IsCancellationRequested)
            {
                socket.Close(503); // Service Unavailable: the server is stopping
                return;
            }

            socket.OnOpen = () => this.OnWebSocketConnection(socket);
        });
    }

    /// <summary>
    /// Stop listening for connections and terminate processing of all ongoing requests.
    /// </summary>
    public void Stop()
    {
        this.cts.Cancel();
        this.server.RestartAfterListenError = false; // work around statianzo/Fleck#325
        this.server.ListenerSocket.Close();
        this.server.Dispose();
    }
}
