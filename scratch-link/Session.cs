// <copyright file="Session.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink;

using System.Net.WebSockets;

internal class Session
{
    private WebSocketContext context;

    public Session(WebSocketContext context)
    {
        this.context = context;
        var webSocket = context.WebSocket;
        Task.Run(async () =>
        {
            await webSocket.SendString("hello", true, CancellationToken.None);
            await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
        });
    }
}
