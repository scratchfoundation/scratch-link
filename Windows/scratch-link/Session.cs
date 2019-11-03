using Fleck;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace scratch_link
{
    using CompletionHandler = Func<JToken /*result*/, JsonRpcException /*error*/, Task>;
    using RequestId = UInt32;

    internal abstract class Session : IDisposable
    {
        // Keep this in sync with the version number in `NetworkProtocol.md`
        private const string NetworkProtocolVersion = "1.2";

        private static readonly Encoding Encoding = Encoding.UTF8;

        private readonly IWebSocketConnection _webSocket;
        private readonly ArraySegment<byte> _readBuffer;
        private readonly char[] _decodeBuffer;
        private readonly int _maxMessageSize;

        private readonly SemaphoreSlim _sessionLock;

        private RequestId _nextId;
        private readonly Dictionary<RequestId, CompletionHandler> _completionHandlers;

        protected Session(IWebSocketConnection webSocket, int bufferSize = 4096, int maxMessageSize = 1024 * 1024)
        {
            _sessionLock = new SemaphoreSlim(1);
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
                if (_webSocket != null)
                {
                    _webSocket.Close();
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        // Override this to handle received RPC requests & notifications.
        // Call this method with `await super.DidReceiveCall(...)` to implement default calls like `getVersion`.
        // Call the completion handler when done with a request:
        // - pass your call's "return value" (or null) as `result` on success
        // - pass an instance of `JsonRpcException` for `error` on failure
        // You may also throw a `JsonRpcException` (or any other `Exception`) to signal failure.
        // Exceptions are caught even when thrown in an `async` method after `await`:
        // http://www.interact-sw.co.uk/iangblog/2010/11/01/csharp5-async-exceptions
        protected virtual async Task DidReceiveCall(string method, JObject parameters, CompletionHandler completion)
        {
            switch (method)
            {
                case "pingMe":
                    await completion("willPing", null);
                    SendRemoteRequest("ping", null, (result, error) =>
                    {
                        Debug.Print($"Got result from ping: {result}");
                        return Task.CompletedTask;
                    });
                    break;
                case "getVersion":
                    await completion(GetVersion(), null);
                    break;
                default:
                    // unrecognized method
                    throw JsonRpcException.MethodNotFound(method);
            }
        }

        // Create a `JObject` containing version information. All version values must be strings.
        // This base version puts the network protocol version in a property called `protocol`.
        // Subclasses may choose to override this method to add more info; the recommended pattern is:
        //   var versionInfo = base.GetVersion();
        //   versionInfo.Add("mySpecialVersion", someValue);
        //   return versionInfo;
        protected virtual JObject GetVersion()
        {
            return new JObject(
                new JProperty("protocol", NetworkProtocolVersion)
            );
        }

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
                await _webSocket.Send(requestText);
            }
            catch (Exception e)
            {
                Debug.Print($"Error serializing or sending request: {e}");
                Debug.Print($"Request was: {request}");
            }
        }

        protected async Task SendErrorNotification(JsonRpcException error)
        {
            try
            {
                var message = MakeResponse(null, null, error);
                var messageText = JsonConvert.SerializeObject(message);

                await _webSocket.Send(messageText);
            }
            catch (Exception e)
            {
                // This probably means the WebSocket is closed, causing Send to throw. Since SendErrorNotification
                // is often called in response to an exception -- possibly one that caused the WebSocket to close --
                // reporting this secondary exception won't help anyone and may itself cause a crash. Instead, print
                // this secondary exception and move on...
                Debug.Print($"Error serializing or sending error notification: {e}");
                Debug.Print($"Original error notification was: {error}");
            }
        }

        public async Task OnMessage(string message)
        {
            await DidReceiveMessage(message, async response =>
            {
                await _webSocket.Send(response);
            });
        }

        public async Task OnBinary(byte[] messageBytes)
        {
            // the message MUST be a string encoded in UTF-8 format
            var message = Encoding.UTF8.GetString(messageBytes);
            await DidReceiveMessage(message, async response =>
            {
                var responseBytes = Encoding.UTF8.GetBytes(response);
                await _webSocket.Send(responseBytes);
            });
        }

        private JObject MakeResponse(JToken responseId, JToken result, JsonRpcException error)
        {
            return new JObject(
                new JProperty("jsonrpc", "2.0"),
                new JProperty("id", responseId),
                error == null ?
                    new JProperty("result", result) :
                    new JProperty("error", JObject.FromObject(error))
            );
        }

        private async Task DidReceiveMessage(string message, Func<string, Task> sendResponseText)
        {
            var encoding = Encoding.UTF8;
            JToken responseId = null;

            async Task SendResponseInternal(JToken result, JsonRpcException error)
            {
                var response = MakeResponse(responseId, result, error);

                var responseText = JsonConvert.SerializeObject(response);

                await sendResponseText(responseText);
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

            async Task resultHandler(JToken result, JsonRpcException error)
            {
                if (error != null) throw error;
                await sendResult(result);
            };

            _sessionLock.Wait();
            try
            {
                await DidReceiveCall(method, parameters, resultHandler);
            }
            finally
            {
                _sessionLock.Release();
            }
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
