using Fleck;
using System;
using System.Windows;

namespace scratch_link
{
    internal class SessionManager
    {
        public int ActiveSessionCount { get; private set; }

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
                ((App)(Application.Current)).UpdateIconText();
            };
            webSocket.OnMessage = async message => await session.OnMessage(message);
            webSocket.OnBinary = async message => await session.OnBinary(message);
            webSocket.OnClose = () =>
            {
                --ActiveSessionCount;
                ((App)(Application.Current)).UpdateIconText();
                session.Dispose();
                session = null;
            };
        }
    }
}
