// <copyright file="WebSocketListener.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink;

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Listen for WebSocket connections and direct them to service handlers.
/// </summary>
internal class WebSocketListener
{
    private readonly HttpListener listener = new ();

    private readonly CancellationTokenSource cts = new ();

    /// <summary>
    /// Gets a value indicating whether WebSocketListener is supported in the current environment.
    /// </summary>
    public static bool IsSupported => HttpListener.IsSupported;

    /// <summary>
    /// Gets or sets the action which will be called when the listener receives a WebSocket connection.
    /// </summary>
    public Action<WebSocketContext> OnWebSocketConnection { get; set; }

    /// <summary>
    /// Gets or sets the action which will be called when the listener receives a non-WebSocket connection.
    /// </summary>
    public Action<HttpListenerContext> OnOtherConnection { get; set; }

    /// <summary>
    /// Start listening for connections. If already listening, stop and restart with the new prefix list.
    /// </summary>
    /// <param name="prefixes">
    /// The list of HTTP(S) URL prefixes to listen on.
    /// Use "http" instead of "ws" or "https" instead of "wss".
    /// <example><code>
    /// http://locahost:1234/
    /// https://127.0.0.1/
    /// </code></example>
    /// </param>
    public void Start(IEnumerable<string> prefixes)
    {
        if (this.listener.IsListening)
        {
            throw new InvalidOperationException();
        }

        foreach (var prefix in prefixes)
        {
            this.listener.Prefixes.Add(prefix);
        }

        this.listener.Start();
        Task.Run(async () =>
        {
            CancellationToken token = this.cts.Token;
            while (!token.IsCancellationRequested)
            {
                var context = await this.listener.GetContextAsync();
                if (context.Request.IsWebSocketRequest)
                {
                    var webSocketContext = await context.AcceptWebSocketAsync(null);
                    this.OnWebSocketConnection(webSocketContext);
                }
                else
                {
                    this.OnOtherConnection(context);
                }
            }
        });
    }

    /// <summary>
    /// Stop listening for connections and terminate processing of all ongoing requests.
    /// </summary>
    public void Stop()
    {
        this.cts.Cancel();
        this.listener.Stop();
    }
}
