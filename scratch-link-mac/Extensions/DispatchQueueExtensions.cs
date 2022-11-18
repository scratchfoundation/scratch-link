// <copyright file="DispatchQueueExtensions.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink.Mac.Extensions;

using System;
using System.Threading.Tasks;
using CoreFoundation;
using CoreServices;

/// <summary>
/// Extensions for <see cref="DispatchQueue"/>.
/// </summary>
public static class DispatchQueueExtensions
{
    /// <summary>
    /// Like <see cref="DispatchQueue.DispatchAsync(Action)"/>, but returns an awaitable <see cref="Task{TResult}"/>.
    /// </summary>
    /// <typeparam name="TResult">The task's return type.</typeparam>
    /// <param name="dispatchQueue">The queue to dispatch the task on.</param>
    /// <param name="func">The body of the task. Must be synchronous, or it could escape the dispatch queue.</param>
    /// <returns>A task for the value returned by <paramref name="func"/>.</returns>
    public static Task<TResult> DispatchTask<TResult>(this DispatchQueue dispatchQueue, Func<TResult> func)
    {
        var completionSource = new TaskCompletionSource<TResult>();

        dispatchQueue.DispatchAsync(() =>
        {
            try
            {
                var result = func();
                completionSource.SetResult(result);
            }
            catch (Exception e)
            {
                completionSource.SetException(e);
            }
        });

        return completionSource.Task;
    }

    /// <summary>
    /// Like <see cref="DispatchQueue.DispatchAsync(Action)"/>, but returns an awaitable <see cref="Task"/>.
    /// </summary>
    /// <param name="dispatchQueue">The queue to dispatch the task on.</param>
    /// <param name="action">The body of the task. Must be synchronous, or it could escape the dispatch queue.</param>
    /// <returns>A task which will complete after the action runs.</returns>
    public static Task DispatchTask(this DispatchQueue dispatchQueue, Action action)
    {
        return DispatchTask<bool>(dispatchQueue, () =>
        {
            action();
            return true; // ignored
        });
    }
}
