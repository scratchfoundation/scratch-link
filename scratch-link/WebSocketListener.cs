// <copyright file="WebSocketListener.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink;

using System.Net;
using System.Net.WebSockets;

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
    /// Gets the mapping of path to handler.
    /// </summary>
    public Dictionary<string, Action<HttpListenerWebSocketContext>> Handlers { get; } = new ();

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
    public void Listen(IEnumerable<string> prefixes)
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
                    this.HandleWebSocketRequest(context);
                }
                else
                {
                    this.HandleOtherRequest(context);
                }
            }
        });
    }

    private async void HandleWebSocketRequest(HttpListenerContext context)
    {
        var webSocket = await context.AcceptWebSocketAsync(null);
        var handler = this.Handlers[context.Request.Url.AbsolutePath];
        if (handler != null)
        {
            handler(webSocket);
        }
        else
        {
            throw new NotImplementedException();
        }
    }

    private void HandleOtherRequest(HttpListenerContext context)
    {
        throw new NotImplementedException();
    }
}
