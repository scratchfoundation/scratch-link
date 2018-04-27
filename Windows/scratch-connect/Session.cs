using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace scratch_connect
{
    internal class Session: IDisposable
    {
        private readonly WebSocket _webSocket;
        private readonly ArraySegment<byte> _readBuffer;
        private readonly char[] _decodeBuffer;
        private readonly int _maxMessageSize;

        protected Session(WebSocket webSocket, int bufferSize = 4096, int maxMessageSize = 1024 * 1024)
        {
            _webSocket = webSocket;
            _readBuffer = new ArraySegment<byte>(new byte[bufferSize]);
            _decodeBuffer = new char[bufferSize];
            _maxMessageSize = maxMessageSize;
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

        public async Task Start()
        {
            // Suppress warning about .Array potentially being null
            if (_readBuffer.Array == null) throw new NullReferenceException();

            var encoding = Encoding.UTF8;

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
                    var message = encoding.GetString(_readBuffer.Array, 0, receiveResult.Count);
                    DidReceiveMessage(message, receiveResult.MessageType == WebSocketMessageType.Binary);
                }
                else // fragmented message
                {
                    var decoder = encoding.GetDecoder();
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
                    DidReceiveMessage(message, receiveResult.MessageType == WebSocketMessageType.Binary);
                }
            }
        }

        private void DidReceiveMessage(string message, bool isBinary)
        {
            var messageType = isBinary ? "binary" : "text";
            var json = JsonConvert.DeserializeObject(message);
            Debug.Print($"Received {messageType} message and deserialized it to: {json}");
        }
    }
}
