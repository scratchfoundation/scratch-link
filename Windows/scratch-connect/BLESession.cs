using System.Net.WebSockets;

namespace scratch_connect
{
    internal class BLESession: Session
    {
        internal BLESession(WebSocket webSocket) : base(webSocket)
        {
        }
    }
}
