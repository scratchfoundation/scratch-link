using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace scratch_link
{
    using RequestId = UInt32;

    using CompletionHandler = Func<JToken /*result*/, JsonRpcException /*error*/, Task>;

    internal abstract class Session: IDisposable
    {
        private static readonly Encoding Encoding = Encoding.UTF8;

        private readonly WebSocket _webSocket;
        private readonly ArraySegment<byte> _readBuffer;
        private readonly char[] _decodeBuffer;
        private readonly int _maxMessageSize;

        private RequestId _nextId;
        private readonly Dictionary<RequestId, CompletionHandler> _completionHandlers;

        protected Session(WebSocket webSocket, int bufferSize = 4096, int maxMessageSize = 1024 * 1024)
        {
            _webSocket = webSocket;
            _readBuffer = new ArraySegment<byte>(new byte[bufferSize]);
            _decodeBuffer = new char[bufferSize];
            _maxMessageSize = maxMessageSize;
            _nextId = 0;
            _completionHandlers = new Dictionary<RequestId, CompletionHandler>();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _webSocket?.Dispose();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        // Override this to handle received RPC requests & notifications.
        // Call the completion handler when done with a request:
        // - pass your call's "return value" (or null) as `result` on success
        // - pass an instance of `JsonRpcException` for `error` on failure
        // You may also throw a `JsonRpcException` (or any other `Exception`) to signal failure.
        // Exceptions are caught even when thrown in an `async` method after `await`:
        // http://www.interact-sw.co.uk/iangblog/2010/11/01/csharp5-async-exceptions
        protected abstract Task DidReceiveCall(string method, JObject parameters, CompletionHandler completion);

        // Omit (or pass null for) the completion handler to send a Notification.
        // Completion handlers may be async. If your completion handler is not async, return `Task.CompletedTask`.
        protected async void SendRemoteRequest(string method, JObject parameters, CompletionHandler completion = null)
        {
            var request = new JObject(
                new JProperty("jsonrpc", "2.0"),
                new JProperty("method", method)
            );

            if (parameters != null)
            {
                request.Add("params", parameters);
            }

            if (completion != null)
            {
                var requestId = GetNextId();
                request.Add("id", requestId);
                _completionHandlers.Add(requestId, completion);
            }

            try
            {
                var requestText = JsonConvert.SerializeObject(request);
                var requestData = Encoding.GetBytes(requestText);
                await _webSocket.SendAsync(
                    new ArraySegment<byte>(requestData), WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch (Exception e)
            {
                Debug.Print($"Error serializing or sending request: {e}");
                Debug.Print($"Request was: {request}");
            }
        }

        public async Task Start()
        {
            // Suppress warning about .Array potentially being null
            if (_readBuffer.Array == null) throw new NullReferenceException();

            for (;;)
            {
                var receiveResult = await _webSocket.ReceiveAsync(_readBuffer, CancellationToken.None);
                if (receiveResult.CloseStatus.HasValue)
                {
                    await _webSocket.CloseAsync(receiveResult.CloseStatus.Value, receiveResult.CloseStatusDescription,
                        CancellationToken.None);
                    return;
                }

                if (receiveResult.EndOfMessage)
                {
                    var message = Encoding.GetString(_readBuffer.Array, 0, receiveResult.Count);
                    DidReceiveMessage(message, receiveResult.MessageType);
                }
                else // fragmented message
                {
                    var decoder = Encoding.GetDecoder();
                    var messageBuilder = new StringBuilder();

                    void DecodeFragment()
                    {
                        var numDecodedCharacters = decoder.GetChars(
                            _readBuffer.Array, 0, receiveResult.Count,
                            _decodeBuffer, 0, receiveResult.EndOfMessage);
                        if (messageBuilder.Length + numDecodedCharacters > _maxMessageSize)
                        {
                            throw new ApplicationException("Incoming message too big");
                        }

                        messageBuilder.Append(_decodeBuffer, 0, numDecodedCharacters);
                    }

                    DecodeFragment();
                    while (!receiveResult.EndOfMessage)
                    {
                        receiveResult = await _webSocket.ReceiveAsync(_readBuffer, CancellationToken.None);
                        if (receiveResult.CloseStatus.HasValue)
                        {
                            await _webSocket.CloseAsync(receiveResult.CloseStatus.Value,
                                receiveResult.CloseStatusDescription, CancellationToken.None);
                            Debug.Print("Socket closed before end of fragmented message");
                            return;
                        }
                        DecodeFragment();
                    }

                    var message = messageBuilder.ToString();
                    DidReceiveMessage(message, receiveResult.MessageType);
                }
            }
        }

        private async void DidReceiveMessage(string message, WebSocketMessageType messageType)
        {
            Debug.Assert(messageType != WebSocketMessageType.Close);

            var encoding = Encoding.UTF8;
            JToken responseId = null;

            async Task SendResponseInternal(JToken result, JsonRpcException error)
            {
                var response = new JObject(
                    new JProperty("jsonrpc", "2.0"),
                    new JProperty("id", responseId),
                    error == null ? new JProperty("result", result) : new JProperty("error", JObject.FromObject(error))
                );

                var responseText = JsonConvert.SerializeObject(response);
                var responseBytes = encoding.GetBytes(responseText);

                await _webSocket.SendAsync(
                    new ArraySegment<byte>(responseBytes), messageType, true, CancellationToken.None);
            }

            async Task SendResponse(JToken result, JsonRpcException error)
            {
                try
                {
                    await SendResponseInternal(result, error);
                }
                catch (Exception firstError)
                {
                    try
                    {
                        Debug.Print($"Could not encode response: {firstError}");
                        await SendResponseInternal(null,
                            JsonRpcException.ApplicationError("Could not encode response"));
                    }
                    catch (Exception secondError)
                    {
                        Debug.Print($"Could not report response encoding failure: {secondError}");
                    }
                }
            }

            try
            {
                var json = JObject.Parse(message);

                // do this as early as possible so that error responses can include it.
                responseId = json["id"];

                // property "jsonrpc" must be exactly "2.0"
                if ((string)json["jsonrpc"] != "2.0")
                {
                    throw JsonRpcException.InvalidRequest("unrecognized JSON-RPC version string");
                }

                if (json["method"] != null)
                {
                    await DidReceiveRequest(json, async result => await SendResponse(result, null));
                }
                else if (json["result"] != null || json["error"] != null)
                {
                    await DidReceiveResponse(json);
                }
                else
                {
                    throw JsonRpcException.InvalidRequest("message is neither request nor response");
                }
            }
            catch (JsonRpcException jsonRpcException)
            {
                await SendResponse(null, jsonRpcException);
            }
            catch (Exception e)
            {
                var jsonRpcException =
                    JsonRpcException.ApplicationError($"Unhandled error encountered during call: {e}");
                await SendResponse(null, jsonRpcException);
            }
        }

        private async Task DidReceiveRequest(JObject request, Func<JToken, Task> sendResult)
        {
            var method = request["method"]?.ToObject<string>();
            if (string.IsNullOrWhiteSpace(method))
            {
                throw JsonRpcException.InvalidRequest("method value missing or not a string");
            }

            // optional: dictionary of parameters by name
            var parameters = request["params"]?.ToObject<JObject>() ?? new JObject();

            await DidReceiveCall(method, parameters, async (result, error) =>
            {
                if (error != null) throw error;
                await sendResult(result);
            });
        }

        private async Task DidReceiveResponse(JObject response)
        {
            var requestId = response["id"]?.ToObject<RequestId?>();
            if (!requestId.HasValue)
            {
                throw JsonRpcException.InvalidRequest("response ID value missing or wrong type");
            }

            if (!_completionHandlers.TryGetValue(requestId.Value, out var completionHandler))
            {
                throw JsonRpcException.InvalidRequest("response ID does not correspond to any open request");
            }

            var error = response["error"]?.ToObject<JsonRpcException>();
            try
            {
                if (error != null)
                {
                    await completionHandler(null, error);
                }
                else
                {
                    var result = response["result"];
                    await completionHandler(result, null);
                }
            }
            catch (Exception e)
            {
                var remoteMessage = $"exception encountered while handling response {requestId}";
                Debug.Print(remoteMessage);
                Debug.Print($"The exception was: {e}");
                throw JsonRpcException.ApplicationError(remoteMessage);
            }
        }

        private RequestId GetNextId()
        {
            return _nextId++;
        }
    }
}
