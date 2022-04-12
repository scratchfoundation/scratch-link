// <copyright file="WebSocketExtensions.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;

/// <summary>
/// This class contains custom extensions to System.Net.WebSockets.WebSocket.
/// </summary>
internal static class WebSocketExtensions
{
    /// <summary>
    /// Sends a string over the WebSocket connection asynchronously.
    /// </summary>
    /// <param name="ws"></param>
    /// <param name="message"></param>
    /// <param name="endOfMessage"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
    public static Task SendString(this WebSocket ws, string message, bool endOfMessage, CancellationToken cancellationToken)
    {
        var messageBytes = Encoding.UTF8.GetBytes(message);
        var buffer = new ArraySegment<byte>(messageBytes);
        return ws.SendAsync(buffer, WebSocketMessageType.Text, endOfMessage, cancellationToken);
    }
}
