// <copyright file="EventAwaiter.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink;

using System;

/// <summary>
/// Adaptor to use "await" with events.
/// </summary>
public static class EventAwaiter
{
    /// <summary>
    /// Make an awaitable Task which will resolve the next time this event is triggered.
    /// This version only supports hooking an event in the calling class, since that's the only class which can access the implicit event delegate.
    /// See <a href="https://stackoverflow.com/q/2560258">here</a> for more information and alternative solutions.
    /// </summary>
    /// <typeparam name="T">The type of EventArgs.</typeparam>
    /// <param name="targetEvent">The event to wait for.</param>
    /// <param name="timeout">How long to wait for the event. If the timeout expires the Task will throw a <see cref="TimeoutException"/>.</param>
    /// <param name="cancellationToken">The cancellation token to use to cancel the operation.</param>
    /// <returns>A Task which will return the args passed to the event when it triggers.</returns>
    public static Task<T> MakeTask<T>(EventHandler<T> targetEvent, TimeSpan timeout, CancellationToken cancellationToken)
    {
        return MakeTask<T>(
            h => targetEvent += h,
            h => targetEvent -= h,
            timeout,
            cancellationToken);
    }

    /// <summary>
    /// Make an awaitable Task which will resolve the next time this event is triggered.
    /// This version supports hooking an event in another class by providing <c>addHandler</c> and <c>removeHandler</c> actions.
    /// See <a href="https://stackoverflow.com/q/2560258">here</a> for more information and alternative solutions.
    /// </summary>
    /// <typeparam name="T">The type of EventArgs.</typeparam>
    /// <param name="addHandler">An action to add a handler to the event, like <c>handler => foo.MyEvent += handler</c>.</param>
    /// <param name="removeHandler">An action to remove a handler from the event, like <c>handler => foo.MyEvent += handler</c>.</param>
    /// <param name="timeout">How long to wait for the event. If the timeout expires the Task will throw a <see cref="TimeoutException"/>.</param>
    /// <param name="cancellationToken">The cancellation token to use to cancel the operation.</param>
    /// <returns>A Task which will return the args passed to the event when it triggers.</returns>
    public static Task<T> MakeTask<T>(Action<EventHandler<T>> addHandler, Action<EventHandler<T>> removeHandler, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var completionSource = new TaskCompletionSource<T>();

        // localCancellationSource is canceled on cleanup and should not affect the task results.
        // If cancellationToken is cancelled it happed externally and should mark the task as canceled.
        var localCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var localCancellationToken = localCancellationSource.Token;

        EventHandler<T> handler = null;
        CancellationTokenRegistration? tokenRegistration = null;
        var delayTimer = Task.Delay(timeout, localCancellationToken);

        // make this safe to call multiple times in case (for example) a timeout goes into the event queue before a success finishes processing
        var cleanup = () =>
        {
            if (localCancellationSource?.IsCancellationRequested == false)
            {
                localCancellationSource.Cancel();
                localCancellationSource = null;
            }

            if (handler != null)
            {
                removeHandler(handler);
                handler = null;
            }

            if (delayTimer != null)
            {
                delayTimer.Dispose();
                delayTimer = null;
            }

            if (tokenRegistration != null)
            {
                tokenRegistration?.Dispose();
                tokenRegistration = null;
            }
        };

        // success
        handler = (object sender, T args) =>
        {
            cleanup();
            completionSource.TrySetResult(args);
        };
        addHandler(handler);

        // timeout
        delayTimer.ContinueWith(
            _ =>
            {
                cleanup();
                completionSource.TrySetException(new TimeoutException());
            },
            localCancellationToken);

        // cancel
        tokenRegistration = cancellationToken.Register(() =>
        {
            cleanup();
            completionSource.TrySetCanceled();
        });

        return completionSource.Task;
    }
}
