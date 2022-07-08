// <copyright file="EventAwaiter.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink;

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

/// <summary>
/// Adaptor to use "await" with events.
/// Events which fire after construction but before calling <see cref="MakeTask"/> will be queued and can be retrieved later.
/// </summary>
/// <typeparam name="T">The type of EventArgs returned by this event.</typeparam>
public class EventAwaiter<T> : IDisposable
{
    private readonly Action<EventHandler<T>> removeHandler;
    private readonly Channel<T> events;
    private bool disposedValue;

    /// <summary>
    /// Initializes a new instance of the <see cref="EventAwaiter{T}"/> class.
    /// This version only supports hooking an event in the calling class, since that's the only class which can access the implicit event delegate.
    /// </summary>
    /// <param name="targetEvent">The event to hook.</param>
    public EventAwaiter(EventHandler<T> targetEvent)
        : this(h => targetEvent += h, h => targetEvent -= h)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="EventAwaiter{T}"/> class.
    /// This version supports hooking an event in another class by providing <c>addHandler</c> and <c>removeHandler</c> actions.
    /// </summary>
    /// <param name="addHandler">An action to add a handler to the event, like <c>handler => foo.MyEvent += handler</c>.</param>
    /// <param name="removeHandler">An action to remove a handler from the event, like <c>handler => foo.MyEvent += handler</c>.</param>
    public EventAwaiter(Action<EventHandler<T>> addHandler, Action<EventHandler<T>> removeHandler)
    {
        this.removeHandler = removeHandler;
        this.events = Channel.CreateUnbounded<T>();
        addHandler(this.EventHandler);
    }

    /// <summary>
    /// Dispose of this <see cref="EventAwaiter{T}"/> instance, including ensuring that the event is no longer hooked.
    /// </summary>
    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        this.Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Make an awaitable Task which will resolve the next time this event is triggered.
    /// See <a href="https://stackoverflow.com/q/2560258">here</a> for more information and alternative solutions.
    /// </summary>
    /// <param name="timeout">How long to wait for the event. If the timeout expires the Task will throw a <see cref="TimeoutException"/>.</param>
    /// <param name="cancellationToken">The cancellation token to use to cancel the operation.</param>
    /// <returns>A Task which will return the args passed to the event when it triggers.</returns>
    public ValueTask<T> MakeTask(TimeSpan timeout, CancellationToken cancellationToken)
    {
        // localCancellationSource is canceled on cleanup and should not affect the task results.
        // If cancellationToken is cancelled it happed externally and should mark the task as canceled.
        var localCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        localCancellationSource.CancelAfter(timeout);

        var localCancellationToken = localCancellationSource.Token;

        return this.events.Reader.ReadAsync(localCancellationToken);
    }

    /// <summary>
    /// Implements the Dispose pattern for <see cref="EventAwaiter{T}"/>.
    /// </summary>
    /// <param name="disposing">True if called from <see cref="Dispose()"/>.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!this.disposedValue)
        {
            try
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                    this.removeHandler(this.EventHandler);
                }
            }
            finally
            {
                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                this.disposedValue = true;
            }
        }
    }

    private void EventHandler(object sender, T args)
    {
        if (!this.events.Writer.TryWrite(args))
        {
            Debug.Print("EventAwaiter failed to write an event!");
        }
    }
}
