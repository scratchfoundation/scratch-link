using Fleck;
using System;

namespace scratch_link
{
    internal class SessionManager
    {
        public int ActiveSessionCount { get; private set; }
        public event EventHandler ActiveSessionCountChanged;

        private readonly Func<IWebSocketConnection, Session> _sessionCreationDelegate;

        internal SessionManager(Func<IWebSocketConnection, Session> sessionCreationDelegate)
        {
            ActiveSessionCount = 0;
            _sessionCreationDelegate = sessionCreationDelegate;
        }

        public void ClientDidConnect(IWebSocketConnection webSocket)
        {
            var session = _sessionCreationDelegate(webSocket);

            webSocket.OnOpen = () =>
            {
                ++ActiveSessionCount;
                ActiveSessionCountChanged(this, null);
            };
            webSocket.OnMessage = async message => await session.OnMessage(message);
            webSocket.OnBinary = async message => await session.OnBinary(message);
            webSocket.OnClose = () =>
            {
                --ActiveSessionCount;
                ActiveSessionCountChanged(this, null);
                session.Dispose();
                session = null;
            };
        }
    }
}
