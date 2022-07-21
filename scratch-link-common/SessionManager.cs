// <copyright file="SessionManager.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink;

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Fleck;

/// <summary>
/// This class connects a WebSocket to the appropriate session type and tracks the collection of active sessions.
/// </summary>
internal abstract class SessionManager
{
    /// <summary>
    /// Stores the set of active sessions. Implemented as a dictionary because ConcurrentHashSet doesn't exist.
    /// </summary>
    private readonly ConcurrentDictionary<Session, bool> sessions = new ();

    /// <summary>
    /// Activated when the number of active sessions changes.
    /// </summary>
    public event EventHandler ActiveSessionCountChanged;

    /// <summary>
    /// Gets the count of active connected WebSocket sessions.
    /// </summary>
    public int ActiveSessionCount { get => this.sessions.Count; }

    /// <summary>
    /// Close all currently-active sessions.
    /// </summary>
    public void EndAllSessions()
    {
        // shallow-copy the session list since it will change as sessions close
        var allCurrentSessions = this.sessions.Keys.Select(session => session);

        foreach (var session in allCurrentSessions)
        {
            session.EndSession();
        }
    }

    /// <summary>
    /// Call this with a new connection context to ask the SessionManager to build and manage a session for it.
    /// </summary>
    /// <param name="webSocket">The WebSocket which the SessionManager should adopt and connect to a session.</param>
    public async void ClientDidConnect(IWebSocketConnection webSocket)
    {
        using var session = this.MakeNewSession(webSocket);
        if (!this.sessions.TryAdd(session, true))
        {
            throw new ApplicationException("Failed to add session to session manager.");
        }

        this.ActiveSessionCountChanged?.Invoke(this, EventArgs.Empty);
        try
        {
            await session.Run();
        }
        finally
        {
            if (this.sessions.TryRemove(session, out _))
            {
                this.ActiveSessionCountChanged?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                Debug.Print("Failed to remove session from session manager");
            }
        }
    }

    /// <summary>
    /// Create a new Session object to handle a new WebSocket connection.
    /// </summary>
    /// <param name="webSocket">Create a Session to handle this connection.</param>
    /// <returns>A new Session object connected to the provided context.</returns>
    protected abstract Session MakeNewSession(IWebSocketConnection webSocket);
}
