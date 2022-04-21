// <copyright file="Session.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink;

using ScratchLink.JsonRpc;
using System.Net.WebSockets;
using System.Text.Json;

using JsonRpcMethodHandler = Func<
    string, // method name
    object, // params / args
    Task<object> // return value - must be JSON-serializable
>;

/// <summary>
/// Base class for Scratch Link sessions. One session can search for, connect to, and interact with one peripheral device.
/// </summary>
internal class Session
{
    /// <summary>
    /// Specifies the Scratch Link network protocol version. Note that this is not the application version.
    /// Keep this in sync with the version number in `NetworkProtocol.md`.
    /// </summary>
    protected const string NetworkProtocolVersion = "1.2";

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

    /// <summary>
    /// Handle a "getVersion" request.
    /// </summary>
    /// <param name="methodName">The name of the method called (expected: "getVersion").</param>
    /// <param name="args">Any arguments passed to the method by the caller (expected: none).</param>
    /// <returns>A string representing the protocol version.</returns>
    protected Task<object> HandleGetVersion(string methodName, object args)
    {
        return Task.FromResult<object>(new Dictionary<string, string>
        {
            { "protocol", NetworkProtocolVersion },
        });
    }

    private Task<object> HandleUnrecognizedMethod(string methodName, object args)
    {
        throw new JsonRpc2Exception(JsonRpc2Error.MethodNotFound(methodName));
    }

    private async Task HandleRequest(JsonRpc.JsonRpc2Request request, CancellationToken cancellationToken)
    {
        var handler = this.Handlers.GetValueOrDefault(request.Method, this.HandleUnrecognizedMethod);

        object result = null;
        JsonRpc2Error error = null;

        try
        {
            result = await handler(request.Method, request.Params);
        }
        catch (JsonRpc2Exception e)
        {
            error = e.Error;
        }
        catch (Exception e)
        {
            error = JsonRpc2Error.ApplicationError($"Unhandled error encountered during call: {e}");
        }

        if (request.Id is JsonElement requestId)
        {
            await this.SendResponse(requestId, result, error, cancellationToken);
        }
    }

    private async Task SendResponse(JsonElement id, object result, JsonRpc2Error error, CancellationToken cancellationToken)
    {
        var response = new JsonRpc2Response
        {
            Id = id,
            Result = (error == null) ? result : null,
            Error = error,
        };
        var responseBytes = JsonSerializer.SerializeToUtf8Bytes(response);

        var webSocket = this.context.WebSocket;
        await webSocket.SendAsync(responseBytes, WebSocketMessageType.Text, true, cancellationToken);
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

                if (messageBuffer.Length > 0)
                {
                    messageBuffer.Position = 0;
                    var request = JsonSerializer.Deserialize<JsonRpc.JsonRpc2Request>(messageBuffer);
                    if (request != null)
                    {
                        await this.HandleRequest(request, cancellationToken);
                    }
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
}
