using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;

namespace scratch_connect
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App
    {
        const int SDMPort = 20110;

        private static class SDMPath
        {
            public const string BLE = "/scratch/ble";
            public const string BT = "/scratch/bt";
        }

        private readonly HttpListener _server;
        private readonly Dictionary<string, SessionManager> _sessionManagers;

        private App()
        {
            _sessionManagers = new Dictionary<string, SessionManager>
            {
                [SDMPath.BLE] = new SessionManager(webSocket => new BLESession(webSocket))
            };

            _server = new HttpListener();
            // "http://+:{SDMPort}" would be better but requires elevated privileges
            _server.Prefixes.Add($"http://127.0.0.1:{SDMPort}/");
            _server.Start();

            AcceptNextClient();
        }

        private void AcceptNextClient()
        {
            _server.BeginGetContext(ClientDidConnect, null);
        }

        private async void ClientDidConnect(IAsyncResult ar)
        {
            // Get ready for another connection
            AcceptNextClient();

            // Process this connection
            WebSocket webSocket;

            var listenerContext = _server.EndGetContext(ar);
            try
            {
                var websocketContext = await listenerContext.AcceptWebSocketAsync(null);
                webSocket = websocketContext.WebSocket;
            }
            catch (Exception e)
            {
                listenerContext.Response.StatusCode = 500;
                listenerContext.Response.Close();
                Debug.Print($"Exception attempting to accept a web socket connection: {e}");
                return;
            }

            var sessionManager = _sessionManagers[listenerContext.Request.Url.AbsolutePath];
            if (sessionManager != null)
            {
                sessionManager.ClientDidConnect(webSocket);
            }
            else
            {
                listenerContext.Response.StatusCode = 404;
                listenerContext.Response.Close();
                Debug.Print($"Client tried to connect to unknown path: {listenerContext.Request.Url.AbsolutePath}");
            }
        }
    }
}
