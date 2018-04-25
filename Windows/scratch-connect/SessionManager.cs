using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Threading;

namespace scratch_connect
{
    internal class SessionManager
    {
        private readonly Dictionary<WebSocket, Session> _sessions = new Dictionary<WebSocket, Session>();
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
                session = GetSessionFor(webSocket);
                var utf8 = new UTF8Encoding();

                var receiveBuffer = new byte[session.ReceiveBufferSize];
                var textBuffer = "";
                var binaryBuffer = new List<byte>();

                while (webSocket.State == WebSocketState.Open)
                {
                    // ReceiveAsync may receive a message in fragments. If so we'll need to glue it back together before trying to parse JSON...
                    var receiveResult = await webSocket.ReceiveAsync(new ArraySegment<byte>(receiveBuffer), CancellationToken.None);

                    switch (receiveResult.MessageType)
                    {
                        case WebSocketMessageType.Text:

                            textBuffer += utf8.GetString(receiveBuffer, 0, receiveResult.Count);
                            if (receiveResult.EndOfMessage)
                            {
                                var textMessage = textBuffer;
                                textBuffer = "";
                                DidReceiveText(session, textMessage);
                            }
                            break;
                        case WebSocketMessageType.Binary:
                            binaryBuffer.AddRange(new ArraySegment<byte>(receiveBuffer, 0, receiveResult.Count));
                            if (receiveResult.EndOfMessage)
                            {
                                var binaryMessage = binaryBuffer;
                                binaryBuffer = new List<byte>();
                                DidReceiveBinary(session, binaryMessage);
                            }
                            break;
                        case WebSocketMessageType.Close:
                            await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
            }
            catch (Exception e)
            {
                Debug.Print($"Exception causing web socket to close: ${e}");
            }
            finally
            {
                session?.Dispose();
                if (webSocket != null)
                {
                    webSocket.Dispose();
                    _sessions.Remove(webSocket);
                }
            }
        }

        private void DidReceiveText(Session session, string textBuffer)
        {
            throw new NotImplementedException();
        }

        private void DidReceiveBinary(Session session, List<byte> binaryBuffer)
        {
            throw new NotImplementedException();
        }

        private Session GetSessionFor(WebSocket webSocket)
        {
            if (_sessions.TryGetValue(webSocket, out var session))
            {
                return session;
            }

            session = _sessionCreationDelegate(webSocket);
            _sessions[webSocket] = session;
            return session;
        }
    }
}
