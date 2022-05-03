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
internal class Session : IDisposable
{
    /// <summary>
    /// Specifies the Scratch Link network protocol version. Note that this is not the application version.
    /// Keep this in sync with the version number in `NetworkProtocol.md`.
    /// </summary>
    protected const string NetworkProtocolVersion = "1.2";

    /// <summary>
    /// Default timeout for remote requests.
    /// </summary>
    protected static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds(3);

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

    private readonly Dictionary<RequestId, PendingRequestRecord> pendingRequests = new ();
    private RequestId nextId = 1; // some clients have trouble with ID=0

    private SemaphoreSlim websocketSendLock = new (1);

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
        this.cancellationTokenSource.Dispose();
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
            try
            {
                var pingResult = await this.SendRequest("ping", null, cancellationToken);
                Debug.Print($"Got result from ping: {pingResult}");
            }
            catch (JsonRpc2Exception e)
            {
                Debug.Print($"Got JSON-RPC error from ping: {e.Error}");
            }
            catch (Exception e)
            {
                Debug.Print($"Got unrecognized exception from ping: {e}");
            }
        });
        return Task.FromResult<object>("willPing");
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
        var request = new JsonRpc2Request
        {
            Method = method,
            Params = parameters,
        };

        var messageBytes = JsonSerializer.SerializeToUtf8Bytes(request);

        var webSocket = this.context.WebSocket;

        await this.SocketSend(messageBytes, cancellationToken);
    }

    /// <summary>
    /// Send a request to the client and return the result.
    /// Cancel the request if DefaultRequestTimeout passes before receiving a response.
    /// </summary>
    /// <param name="method">The name of the method to call on the client.</param>
    /// <param name="parameters">The optional parameters to pass to the client method.</param>
    /// <param name="cancellationToken">The cancellation token to use to cancel the operation.</param>
    /// <returns>A <see cref="Task"/> resulting in the value returned by the client.</returns>
    protected async Task<object> SendRequest(string method, object parameters, CancellationToken cancellationToken)
    {
        return await this.SendRequest(method, parameters, cancellationToken, DefaultRequestTimeout);
    }

    /// <summary>
    /// Send a request to the client and return the result.
    /// </summary>
    /// <param name="method">The name of the method to call on the client.</param>
    /// <param name="parameters">The optional parameters to pass to the client method.</param>
    /// <param name="cancellationToken">The cancellation token to use to cancel the operation.</param>
    /// <param name="timeout">Cancel the request if this much time passes before receiving a response.</param>
    /// <returns>A <see cref="Task"/> resulting in the value returned by the client.</returns>
    protected async Task<object> SendRequest(string method, object parameters, CancellationToken cancellationToken, TimeSpan timeout)
    {
        var requestId = this.GetNextId();
        var request = new JsonRpc2Request
        {
            Method = method,
            Params = parameters,
            Id = requestId,
        };

        var messageBytes = JsonSerializer.SerializeToUtf8Bytes(request);
        var webSocket = this.context.WebSocket;

        using (var pendingRequest = new PendingRequestRecord(cancellationToken, timeout))
        {
            // register the pending request BEFORE sending the request, just in case the response comes back before we get back from `await`
            this.pendingRequests[requestId] = pendingRequest;

            try
            {
                await this.SocketSend(messageBytes, cancellationToken);
                return await pendingRequest.Task;
            }
            catch (Exception e)
            {
                pendingRequest.TrySetException(e);
                throw;
            }
            finally
            {
                this.pendingRequests.Remove(requestId);
            }
        }
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
            Debug.Print($"Response appears to have invalid ID = {response.Id}");
            return;
        }

        var requestRecord = this.pendingRequests.GetValueOrDefault(responseId, null);
        if (requestRecord == null)
        {
            Debug.Print($"Could not find request record with ID = {response.Id}");
            return;
        }

        requestRecord.TrySetResult(response.Error, response.Result);
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

        await this.SocketSend(responseBytes, cancellationToken);
    }

    private RequestId GetNextId()
    {
        return this.nextId++;
    }

    private async Task SocketSend(byte[] messageBytes, CancellationToken cancellationToken)
    {
        var webSocket = this.context.WebSocket;

        await this.websocketSendLock.WaitAsync();
        try
        {
            await webSocket.SendAsync(messageBytes, WebSocketMessageType.Text, true, cancellationToken);
        }
        finally
        {
            this.websocketSendLock.Release();
        }
    }

    private async void CommLoop()
    {
        var cancellationToken = this.cancellationTokenSource.Token;
        var webSocket = this.context.WebSocket;
        try
        {
            var messageReadLock = new SemaphoreSlim(1);
            var messageBuffer = new MemoryStream();
            while (this.IsOpen)
            {
                JsonRpc2Message message;
                await messageReadLock.WaitAsync();
                try
                {
                    messageBuffer.SetLength(0);
                    var result = await webSocket.ReceiveMessageToStream(messageBuffer, MessageSizeLimit, cancellationToken);

                    if (messageBuffer.Length > 0)
                    {
                        messageBuffer.Position = 0;
                        message = JsonSerializer.Deserialize<JsonRpc2Message>(messageBuffer, this.deserializerOptions);
                    }
                    else
                    {
                        Debug.Print("Received an empty message");
                        continue;
                    }
                }
                finally
                {
                    messageReadLock.Release();
                }

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

    private class PendingRequestRecord : IDisposable
    {
        private TaskCompletionSource<object> completionSource;

        private CancellationTokenSource cancellationTokenSource;
        private CancellationTokenSource timeoutSource;

        public PendingRequestRecord(CancellationToken cancellationToken, TimeSpan timeout)
        {
            this.completionSource = new ();
            this.cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            this.cancellationTokenSource.Token.Register(() => this.completionSource.TrySetCanceled(), useSynchronizationContext: false);
            if (timeout != Timeout.InfiniteTimeSpan)
            {
                this.timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(this.cancellationTokenSource.Token);
                this.timeoutSource.Token.Register(() => this.completionSource.TrySetException(new TimeoutException()), useSynchronizationContext: false);
                this.timeoutSource.CancelAfter(timeout);
            }
        }

        public Task<object> Task => this.completionSource.Task;

        public void Dispose()
        {
            this.cancellationTokenSource.Dispose();
            this.timeoutSource?.Dispose();
        }

        public void Cancel() => this.cancellationTokenSource.Cancel();

        public void TrySetException(Exception exception) => this.completionSource.TrySetException(exception);

        public void TrySetResult(JsonRpc2Error error, object result)
        {
            if (error != null)
            {
                this.completionSource.TrySetException(new JsonRpc2Exception(error));
            }
            else
            {
                this.completionSource.TrySetResult(result);
            }
        }
    }
}
