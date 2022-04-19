// <copyright file="Session.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink;

using System.Net.WebSockets;
using System.Text.Json;
using System.Text.Json.Nodes;

using JsonRpcMethodHandler = Func<
    string, // method name
    System.Text.Json.JsonElement?, // method params / args
    Task<System.Text.Json.Nodes.JsonValue> // return value
>;

/// <summary>
/// Base class for Scratch Link sessions. One session can search for, connect to, and interact with one peripheral device.
/// </summary>
internal class Session
{
    /// <summary>
    /// Stores the mapping from method names to handlers.
    /// </summary>
    protected readonly Dictionary<string, JsonRpcMethodHandler> Handlers = new ();

    private const int MessageSizeLimit = 1024 * 1024; // 1 MiB

    private readonly WebSocketContext context;
    private readonly CancellationTokenSource cancellationTokenSource = new ();

    /// <summary>
    /// Initializes a new instance of the <see cref="Session"/> class.
    /// </summary>
    /// <param name="context">The WebSocket context which this Session will use for communication.</param>
    public Session(WebSocketContext context)
    {
        this.context = context;
        this.Handlers["getVersion"] = this.HandleGetVersion;
    }

    /// <summary>
    /// Gets a value indicating whether returns true if the backing WebSocket is open for communication or is expected to be in the future.
    /// Returns false if the backing WebSocket is closed or closing, or is in an unknown state.
    /// </summary>
    public bool IsOpen => this.context.WebSocket.State switch
    {
        WebSocketState.Connecting => true,
        WebSocketState.Open => true,
        WebSocketState.CloseSent => false,
        WebSocketState.CloseReceived => false,
        WebSocketState.Closed => false,
        WebSocketState.Aborted => false,
        WebSocketState.None => false,
        _ => false,
    };

    /// <summary>
    /// Tell the session to take ownership of the WebSocket context and begin communication.
    /// The session will do its work on a background thread.
    /// After calling this function, do not use the WebSocket context owned by this session.
    /// </summary>
    public void Start()
    {
        Task.Run(this.CommLoop);
    }

    /// <summary>
    /// Stop all communication and shut down the session. Do not use the session after this.
    /// </summary>
    public void Dispose()
    {
        this.cancellationTokenSource.Cancel();
    }

    protected Task<JsonValue> HandleGetVersion(string methodName, JsonElement? args)
    {
        return Task.FromResult(JsonValue.Create(0));
    }

    private async void CommLoop()
    {
        var cancellationToken = this.cancellationTokenSource.Token;
        var webSocket = this.context.WebSocket;
        try
        {
            var messageBuffer = new MemoryStream();
            while (this.IsOpen)
            {
                messageBuffer.SetLength(0);
                var result = await webSocket.ReceiveMessageToStream(messageBuffer, MessageSizeLimit, cancellationToken);
                if (result.CloseStatus.HasValue)
                {
                    break;
                }

                messageBuffer.Position = 0;
                var request = JsonSerializer.Deserialize<JsonRpc.Request>(messageBuffer);
                if (request != null)
                {
                    await this.HandleRequest(request, cancellationToken);
                }
            }
        }
        finally
        {
            if (this.IsOpen)
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
            }

            webSocket.Dispose();
        }
    }

    private async Task HandleRequest(JsonRpc.Request request, CancellationToken cancellationToken)
    {
        var handler = this.Handlers[request.Method];
        /*
        try
        {
            var = (handler != null)
            if (handler != null)
            {
                var result =
            }
        }
        catch (Exception)
        {

        }
        var result = handler != null ?
            await handler(request.Method, request.Params) :
            await HandleUnrecognizedMethod(request.Method, request.Params);
        */
    }
}
