using System;
using System.Net.WebSockets;

namespace scratch_connect
{
    internal class Session: IDisposable
    {
        private readonly WebSocket _webSocket;

        public virtual int ReceiveBufferSize => 1024;
        protected virtual int MaxMessageSize => 1024 * 1024;

        protected Session(WebSocket webSocket)
        {
            _webSocket = webSocket;
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
    }
}
