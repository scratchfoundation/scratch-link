// <copyright file="Session.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Fleck;
using ScratchLink.Extensions;
using ScratchLink.JsonRpc;
using ScratchLink.JsonRpc.Converters;

using JsonRpcMethodHandler = System.Func<
    string, // method name
    System.Text.Json.JsonElement?, // params / args
    System.Threading.Tasks.Task<object> // return value - must be JSON-serializable
>;

using RequestId = System.UInt32;

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

    private const int MessageSizeLimit = 1024 * 1024; // 1 MiB

    private readonly IWebSocketConnection webSocket;
    private readonly CancellationTokenSource cancellationTokenSource = new ();

    private readonly JsonSerializerOptions deserializerOptions = new ()
    {
        Converters = { new JsonRpc2MessageConverter(), new JsonRpc2ValueConverter() },
    };

    private readonly SemaphoreSlim websocketSendLock = new (1);

    private readonly ConcurrentDictionary<RequestId, PendingRequestRecord> pendingRequests = new ();
    private RequestId nextId = 1; // some clients have trouble with ID=0

    /// <summary>
    /// Initializes a new instance of the <see cref="Session"/> class.
    /// </summary>
    /// <param name="webSocket">The WebSocket which this session will use for communication.</param>
    public Session(IWebSocketConnection webSocket)
    {
        this.webSocket = webSocket;
        this.Handlers["getVersion"] = this.HandleGetVersion;
        this.Handlers["pingMe"] = this.HandlePingMe;
    }

    /// <summary>
    /// Gets a value indicating whether returns true if the backing WebSocket is open for communication.
    /// Returns false if the backing WebSocket is closed or closing, or is in an unknown state.
    /// </summary>
    public bool IsOpen => this.webSocket.IsAvailable;

    /// <summary>
    /// Gets a value indicating whether <see cref="Dispose(bool)"/> has already been called and completed on this session.
    /// </summary>
    protected bool DisposedValue { get; private set; }

    /// <summary>
    /// Gets the cancellation token for this session. Use this for long-running operations anywhere in the session.
    /// </summary>
    protected CancellationToken CancellationToken => this.cancellationTokenSource.Token;

    /// <summary>
    /// Gets the mapping from method names to handlers.
    /// </summary>
    protected Dictionary<string, JsonRpcMethodHandler> Handlers { get; } = new ();

    /// <summary>
    /// Tell the session to take ownership of the WebSocket context and begin communication.
    /// The session will do its work on a background thread.
    /// After calling this function, do not use the WebSocket context owned by this session.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public Task Run()
    {
        var runCompletion = new TaskCompletionSource<bool>();
        this.webSocket.OnClose = () =>
        {
            runCompletion.TrySetResult(true);
        };
        this.webSocket.OnMessage = async message =>
        {
            var jsonMessage = JsonSerializer.Deserialize<JsonRpc2Message>(message, this.deserializerOptions);
            await this.HandleMessage(jsonMessage, this.CancellationToken);
        };

        return runCompletion.Task;
    }

    /// <summary>
    /// Stop all communication and shut down the session. Do not use the session after this.
    /// </summary>
    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        this.Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Implement the Disposable pattern for this session.
    /// If <paramref name="disposing"/> is true and <see cref="DisposedValue"/> is false, free any managed resources.
    /// In all cases, call <see cref="Dispose(bool)"/> on <c>base</c> as the last step.
    /// </summary>
    /// <param name="disposing">True if called from <see cref="Dispose()"/>.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!this.DisposedValue)
        {
            if (disposing)
            {
                this.webSocket.Close();
                this.cancellationTokenSource.Cancel();
                this.cancellationTokenSource.Dispose();
            }

            this.DisposedValue = true;
        }
    }

    /// <summary>
    /// In DEBUG builds, wrap an event handler with a try/catch which reports the exception using SendErrorNotifiation.
    /// Otherwise, return the original event handler without modification.
    /// </summary>
    /// <remarks>
    /// This could theoretically leak sensitive information. Use only for debugging.
    /// </remarks>
    /// <param name="original">The original event handler, to be wrapped.</param>
    /// <returns>The wrapped event handler.</returns>
    protected EventHandler WrapEventHandler(EventHandler original)
    {
#if DEBUG
        return (object sender, EventArgs args) =>
        {
            try
            {
                original(sender, args);
            }
            catch (Exception e)
            {
                this.SendEventExceptionNotification(e);
            }
        };
#else
        return original;
#endif
    }

    /// <summary>
    /// In DEBUG builds, wrap an event handler with a try/catch which reports the exception using SendErrorNotifiation.
    /// Otherwise, return the original event handler without modification.
    /// </summary>
    /// <remarks>
    /// This could theoretically leak sensitive information. Use only for debugging.
    /// </remarks>
    /// <typeparam name="T">The type of event args associated with the event handler.</typeparam>
    /// <param name="original">The original event handler, to be wrapped.</param>
    /// <returns>The wrapped event handler.</returns>
    protected EventHandler<T> WrapEventHandler<T>(EventHandler<T> original)
    {
#if DEBUG
        return (object sender, T args) =>
        {
            try
            {
                original(sender, args);
            }
            catch (Exception e)
            {
                this.SendEventExceptionNotification(e);
            }
        };
#else
        return original;
#endif
    }

    /// <summary>
    /// In DEBUG builds, wrap an event handler with a try/catch which reports the exception using SendErrorNotifiation.
    /// Otherwise, return the original event handler without modification.
    /// This version wraps an async handler. Make sure any async handler returns <see cref="Task"/>.
    /// </summary>
    /// <remarks>
    /// This could theoretically leak sensitive information. Use only for debugging.
    /// </remarks>
    /// <typeparam name="T">The type of event args associated with the event handler.</typeparam>
    /// <param name="original">The original event handler, to be wrapped.</param>
    /// <returns>The wrapped event handler.</returns>
    protected EventHandler<T> WrapEventHandler<T>(Func<object, T, Task> original)
    {
#if DEBUG
        return async (object sender, T args) =>
        {
            try
            {
                await original(sender, args);
            }
            catch (Exception e)
            {
                this.SendEventExceptionNotification(e);
            }
        };
#else
        return (object sender, T args) => { original(sender, args); };
#endif
    }

    /// <summary>
    /// Handle a "getVersion" request.
    /// </summary>
    /// <param name="methodName">The name of the method called (expected: "getVersion").</param>
    /// <param name="args">Any arguments passed to the method by the caller (expected: none).</param>
    /// <returns>A string representing the protocol version.</returns>
    protected Task<object> HandleGetVersion(string methodName, JsonElement? args)
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
    protected Task<object> HandlePingMe(string methodName, JsonElement? args)
    {
        var cancellationToken = this.CancellationToken;
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

        var message = JsonSerializer.Serialize(request);

        await this.SocketSend(message, cancellationToken);
    }

    /// <summary>
    /// Inform the client of an error. This is sent as an independent notification, not in response to a request.
    /// </summary>
    /// <param name="error">An object containing information about the error.</param>
    /// <param name="cancellationToken">The cancellation token to use to cancel the operation.</param>
    /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
    protected async Task SendErrorNotification(JsonRpc2Error error, CancellationToken cancellationToken)
    {
        await this.SendResponse(null, null, error, cancellationToken);
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
        return await this.SendRequest(method, parameters, DefaultRequestTimeout, cancellationToken);
    }

    /// <summary>
    /// Send a request to the client and return the result.
    /// </summary>
    /// <param name="method">The name of the method to call on the client.</param>
    /// <param name="parameters">The optional parameters to pass to the client method.</param>
    /// <param name="timeout">Cancel the request if this much time passes before receiving a response.</param>
    /// <param name="cancellationToken">The cancellation token to use to cancel the operation.</param>
    /// <returns>A <see cref="Task"/> resulting in the value returned by the client.</returns>
    protected async Task<object> SendRequest(string method, object parameters, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var requestId = this.GetNextId();
        var request = new JsonRpc2Request
        {
            Method = method,
            Params = parameters,
            Id = requestId,
        };

        var message = JsonSerializer.Serialize(request);

        using var pendingRequest = new PendingRequestRecord(timeout, cancellationToken);

        // register the pending request BEFORE sending the request, just in case the response comes back before we get back from awaiting `SocketSend`
        this.pendingRequests[requestId] = pendingRequest;

        try
        {
            await this.SocketSend(message, cancellationToken);
            return await pendingRequest.Task;
        }
        catch (Exception e)
        {
            pendingRequest.TrySetException(e);
            throw;
        }
        finally
        {
            this.pendingRequests.TryRemove(requestId, out _);
        }
    }

#if DEBUG
    private void SendEventExceptionNotification(Exception e)
    {
        _ = this.SendErrorNotification(JsonRpc2Error.InternalError(e.ToString()), this.CancellationToken);
    }
#endif

    private Task<object> HandleUnrecognizedMethod(string methodName, JsonElement? args)
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
            result = await handler(request.Method, request.Params as JsonElement?);
        }
        catch (JsonRpc2Exception e)
        {
            error = e.Error;
        }
        catch (OperationCanceledException)
        {
            error = JsonRpc2Error.ApplicationError("operation canceled");
        }
        catch (TimeoutException)
        {
            error = JsonRpc2Error.ApplicationError("timeout");
        }
        catch (Exception e)
        {
#if DEBUG
            error = JsonRpc2Error.ApplicationError($"unhandled error encountered during call: {e}");
#else
            error = JsonRpc2Error.ApplicationError($"unhandled error encountered during call");
#endif
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
        var response = new JsonRpc2Message
        {
            Id = id,
            ExtraProperties = new (),
        };

        // handling "result" this way, instead of using JsonRpc2Response, means we can send "result: null" iff both result and error are null
        if (error == null)
        {
            response.ExtraProperties["result"] = result;
        }
        else
        {
            response.ExtraProperties["error"] = error;
        }

        try
        {
            var responseString = JsonSerializer.Serialize(response);

            try
            {
                await this.SocketSend(responseString, cancellationToken);
            }
            catch (Exception e)
            {
                Debug.Print($"Failed to send serialized response due to {e}");
            }
        }
        catch (Exception e)
        {
            Debug.Print($"Failed to serialize response: {response} due to {e}");
        }
    }

    private RequestId GetNextId()
    {
        return this.nextId++;
    }

    private async Task SocketSend(string message, CancellationToken cancellationToken)
    {
        await this.websocketSendLock.WaitAsync(cancellationToken);
        try
        {
            await this.webSocket.Send(message);
        }
        finally
        {
            this.websocketSendLock.Release();
        }
    }

    private async Task HandleMessage(JsonRpc2Message message, CancellationToken cancellationToken)
    {
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

    private class PendingRequestRecord : IDisposable
    {
        private readonly TaskCompletionSource<object> completionSource;

        private readonly CancellationTokenSource cancellationTokenSource;
        private readonly CancellationTokenSource timeoutSource;

        public PendingRequestRecord(TimeSpan timeout, CancellationToken cancellationToken)
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
