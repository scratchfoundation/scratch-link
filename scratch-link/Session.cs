// <copyright file="Session.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink;

using ScratchLink.JsonRpc;
using ScratchLink.JsonRpc.Converters;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text.Json;

using JsonRpcMethodHandler = Func<
    string, // method name
    object, // params / args
    Task<object> // return value - must be JSON-serializable
>;

using RequestId = UInt32;

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

    private readonly JsonSerializerOptions deserializerOptions = new ()
    {
        Converters = { new JsonRpc2MessageConverter(), new JsonRpc2ValueConverter() },
    };

    private readonly Dictionary<RequestId, TaskCompletionSource<object>> responseHandlers = new ();
    private RequestId nextId = 0;

    /// <summary>
    /// Initializes a new instance of the <see cref="Session"/> class.
    /// </summary>
    /// <param name="context">The WebSocket context which this Session will use for communication.</param>
    public Session(WebSocketContext context)
    {
        this.context = context;
        this.Handlers["getVersion"] = this.HandleGetVersion;
        this.Handlers["pingMe"] = this.HandlePingMe;
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

    /// <summary>
    /// Handle a "pingMe" request by returning "willPing" and following up with a separate "ping" request.
    /// </summary>
    /// <param name="methodName">The name of the method called (expected: "pingMe").</param>
    /// <param name="args">Any arguments passed to the method by the caller (expected: none).</param>
    /// <returns>The string "willPing".</returns>
    protected Task<object> HandlePingMe(string methodName, object args)
    {
        var cancellationToken = this.cancellationTokenSource.Token;
        Task.Run(async () =>
        {
            var pingResult = await this.SendRequest("ping", null, cancellationToken);
            Debug.Print($"Got result from ping: {pingResult}");
        });
        return Task.FromResult<object>("willPing");
    }

    /// <summary>
    /// Send a request to the client and return the result.
    /// </summary>
    /// <param name="method">The name of the method to call on the client.</param>
    /// <param name="parameters">The optional parameters to pass to the client method.</param>
    /// <param name="cancellationToken">The cancellation token to use to cancel the operation.</param>
    /// <returns>A <see cref="Task"/> resulting in the value returned by the client.</returns>
    protected async Task<object> SendRequest(string method, object parameters, CancellationToken cancellationToken)
    {
        return await this.SendNotificationOrRequest(method, parameters, true, cancellationToken);
    }

    /// <summary>
    /// Send a notification to the client. A notification has no return value.
    /// </summary>
    /// <param name="method">The name of the method to call on the client.</param>
    /// <param name="parameters">The optional parameters to pass to the client method.</param>
    /// <param name="cancellationToken">The cancellation token to use to cancel the operation.</param>
    /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
    protected async Task SendNotification(string method, object parameters, CancellationToken cancellationToken)
    {
        await this.SendNotificationOrRequest(method, parameters, false, cancellationToken);
    }

    private Task<object> HandleUnrecognizedMethod(string methodName, object args)
    {
        throw new JsonRpc2Exception(JsonRpc2Error.MethodNotFound(methodName));
    }

    private async Task HandleRequest(JsonRpc2Request request, CancellationToken cancellationToken)
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

        if (request.Id is object requestId)
        {
            await this.SendResponse(requestId, result, error, cancellationToken);
        }
    }

    private void HandleResponse(JsonRpc2Response response)
    {
        RequestId responseId;
        try
        {
            // the redundant-looking cast is for unboxing
            responseId = (RequestId)Convert.ChangeType(response.Id, typeof(RequestId));
        }
        catch (Exception)
        {
            Debug.Print($"Response appears to have invalid ID = ${response.Id}");
            return;
        }

        var responseHandler = this.responseHandlers.GetValueOrDefault(responseId, null);
        if (responseHandler == null)
        {
            Debug.Print($"Could not find handler for response with ID = ${response.Id}");
            return;
        }

        if (response.Error != null)
        {
            responseHandler.SetException(new JsonRpc2Exception(response.Error));
        }
        else
        {
            responseHandler.SetResult(response.Result);
        }
    }

    private async Task SendResponse(object id, object result, JsonRpc2Error error, CancellationToken cancellationToken)
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

    private async Task<object> SendNotificationOrRequest(string method, object parameters, bool isRequest, CancellationToken cancellationToken)
    {
        var request = new JsonRpc2Request
        {
            Method = method,
            Params = parameters,
        };

        TaskCompletionSource<object> completionSource = null;

        if (isRequest)
        {
            var requestId = this.GetNextId();
            request.Id = requestId;

            completionSource = new TaskCompletionSource<object>();
            this.responseHandlers[requestId] = completionSource;
        }

        var messageBytes = JsonSerializer.SerializeToUtf8Bytes(request);

        var webSocket = this.context.WebSocket;
        await webSocket.SendAsync(messageBytes, WebSocketMessageType.Text, true, cancellationToken);

        if (completionSource == null)
        {
            return null;
        }

        return await completionSource.Task;
    }

    private RequestId GetNextId()
    {
        return this.nextId++;
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
                    var message = JsonSerializer.Deserialize<JsonRpc2Message>(messageBuffer, this.deserializerOptions);
                    if (message is JsonRpc2Request request)
                    {
                        await this.HandleRequest(request, cancellationToken);
                    }
                    else if (message is JsonRpc2Response response)
                    {
                        this.HandleResponse(response);
                    }
                    else
                    {
                        Debug.Print("Received a message which was not recognized as a Request or Response");
                    }
                }
                else
                {
                    Debug.Print("Received an empty message");
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
