using System;
using System.Diagnostics;
using System.Net.WebSockets;

namespace scratch_link
{
    internal class SessionManager
    {
        public int ActiveSessionCount { get; private set; }

        private readonly Func<WebSocket, Session> _sessionCreationDelegate;

        internal SessionManager(Func<WebSocket, Session> sessionCreationDelegate)
        {
            ActiveSessionCount = 0;
            _sessionCreationDelegate = sessionCreationDelegate;
        }

        public async void ClientDidConnect(WebSocket webSocket)
        {
            Session session = null;

            try
            {
                session = _sessionCreationDelegate(webSocket);
                ++ActiveSessionCount;
                await session.Start();
            }
            catch (Exception e)
            {
                Debug.Print($"Exception causing WebSocket session to close: ${e}");
            }
            finally
            {
                --ActiveSessionCount;
                session?.Dispose();
                webSocket?.Dispose();
            }
        }
    }
}
