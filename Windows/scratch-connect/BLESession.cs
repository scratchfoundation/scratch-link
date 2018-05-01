using System;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace scratch_connect
{
    internal class BLESession : Session
    {
        internal BLESession(WebSocket webSocket) : base(webSocket)
        {
        }

        protected override async Task DidReceiveCall(string method, JObject parameters,
            Func<JToken, JsonRpcException, Task> completion)
        {
            switch (method)
            {
                case "discover":
                    throw new NotImplementedException("not implemented yet");
                case "pingMe":
                    await completion("willPing", null);
                    SendRemoteRequest("ping", null, (result, error) =>
                    {
                        Debug.Print($"Got result from ping: {result}");
                        return Task.CompletedTask;
                    });
                    break;
                default:
                    throw JsonRpcException.MethodNotFound(method);
            }
        }
    }
}
