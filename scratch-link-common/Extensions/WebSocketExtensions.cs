// <copyright file="WebSocketExtensions.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink.Extensions;

using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// This class contains custom extensions to System.Net.WebSockets.WebSocket.
/// </summary>
internal static class WebSocketExtensions
{
    /// <summary>
    /// Sends a string over the WebSocket connection asynchronously.
    /// </summary>
    /// <param name="ws">Send a string over this WebSocket connection.</param>
    /// <param name="message">Send this string over the WebSocket connection.</param>
    /// <param name="endOfMessage">True if this string is the last part of a message; false otherwise.</param>
    /// <param name="cancellationToken">The cancellation token to use to cancel the operation.</param>
    /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
    public static Task SendString(this WebSocket ws, string message, bool endOfMessage, CancellationToken cancellationToken)
    {
        var messageBytes = Encoding.UTF8.GetBytes(message);
        var buffer = new ArraySegment<byte>(messageBytes);
        return ws.SendAsync(buffer, WebSocketMessageType.Text, endOfMessage, cancellationToken);
    }

    /// <summary>
    /// Sends JSON value over the WebSocket connection asynchronously.
    /// </summary>
    /// <typeparam name="TValue">The type of the message object.</typeparam>
    /// <param name="ws">Send a JSON value over this WebSocket connection.</param>
    /// <param name="message">Send this JSON value over the WebSocket connection.</param>
    /// <param name="endOfMessage">True if this JSON value is the last part of a message; false otherwise.</param>
    /// <param name="cancellationToken">The cancellation token to use to cancel the operation.</param>
    /// <param name="serializerOptions">Options to control JSON serialization of the message.</param>
    /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
    public static Task SendJson<TValue>(this WebSocket ws, TValue message, bool endOfMessage, CancellationToken cancellationToken, JsonSerializerOptions serializerOptions = null)
    {
        var messageBytes = JsonSerializer.SerializeToUtf8Bytes(message, serializerOptions);
        return ws.SendAsync(messageBytes, WebSocketMessageType.Text, endOfMessage, cancellationToken);
    }

    /// <summary>
    /// Reads one whole message from the WebSocket and deposit the whole message onto the provided stream.
    /// Reading a whole message may involve multiple calls to the WebSocket's Receive*() method.
    /// </summary>
    /// <param name="ws">The WebSocket from which to read a message.</param>
    /// <param name="dest">The Stream which will receive the completed message.</param>
    /// <param name="maxMessageSize">The maximum allowed message size. An attempt to receive a larger message will cause an exception.</param>
    /// <param name="cancellationToken">The cancellation token to use to cancel the operation.</param>
    /// <returns>A <see cref="Task"/> for the <see cref="WebSocketReceiveResult"/> from the last receive operation which contributing to the message.</returns>
    public static async Task<WebSocketReceiveResult> ReceiveMessageToStream(this WebSocket ws, Stream dest, int maxMessageSize, CancellationToken cancellationToken)
    {
        const int ReceiveBufferChunkSize = 4096;
        var bufferBytes = new byte[ReceiveBufferChunkSize];
        var buffer = new ArraySegment<byte>(bufferBytes);

        WebSocketReceiveResult result;
        do
        {
            result = await ws.ReceiveAsync(buffer, cancellationToken);
            if (result.Count > 0)
            {
                if (result.Count > maxMessageSize)
                {
                    throw new WebSocketException("message too large");
                }

                dest.Write(bufferBytes, 0, result.Count);
                maxMessageSize -= result.Count;
            }
        }
        while (!result.EndOfMessage && !result.CloseStatus.HasValue);

        return result;
    }
}
