using System;
using System.Diagnostics;
using System.Net.WebSockets;

namespace scratch_connect
{

    internal class SessionManager
    {
        private readonly Func<WebSocket, Session> _sessionCreationDelegate;

        internal SessionManager(Func<WebSocket, Session> sessionCreationDelegate)
        {
            _sessionCreationDelegate = sessionCreationDelegate;
        }

        public async void ClientDidConnect(WebSocket webSocket)
        {
            Session session = null;

            try
            {
                session = _sessionCreationDelegate(webSocket);
                await session.Start();
            }
            catch (Exception e)
            {
                Debug.Print($"Exception causing WebSocket session to close: ${e}");
            }
            finally
            {
                session?.Dispose();
                webSocket?.Dispose();
            }
        }
    }
}
