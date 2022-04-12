// <copyright file="SessionManager.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink;

using System.Net.WebSockets;

/// <summary>
/// This class connects a WebSocket to the appropriate session type and tracks the collection of active sessions.
/// </summary>
internal abstract class SessionManager
{
    /// <summary>
    /// Activated when the number of active sessions changes.
    /// </summary>
    public event EventHandler ActiveSessionCountChanged;

    /// <summary>
    /// Gets the count of active connected WebSocket sessions.
    /// </summary>
    public int ActiveSessionCount { get; private set; } = 0;

    /// <summary>
    /// Call this with a new connection context to ask the SessionManager to build and manage a session for it.
    /// </summary>
    /// <param name="webSocketContext">The WebSocket context which the SessionManager should adopt and connect to a session.</param>
    public void ClientDidConnect(WebSocketContext webSocketContext)
    {
        var session = this.MakeNewSession(webSocketContext);
    }

    /// <summary>
    /// Create a new Session object to handle a new WebSocket connection.
    /// </summary>
    /// <param name="webSocketContext">Create a Session to handle this connection.</param>
    /// <returns>A new Session object connected to the provided context.</returns>
    protected abstract Session MakeNewSession(WebSocketContext webSocketContext);
}
