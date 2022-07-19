// <copyright file="SemaphoreSlimExtensions.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink.Extensions;

using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Extensions for <see cref="SemaphoreSlim"/>.
/// </summary>
public static class SemaphoreSlimExtensions
{
    /// <summary>
    /// Like <see cref="SemaphoreSlim.WaitAsync(CancellationToken)"/>, but generates a <see cref="DisposableSemaphoreLock"/> which will release the semaphore on <see cref="DisposableSemaphoreLock.Dispose"/>.
    /// </summary>
    /// <param name="semaphore">The semaphore on which to call <see cref="SemaphoreSlim.WaitAsync(CancellationToken)"/>.</param>
    /// <param name="cancellationToken"><inheritdoc cref="SemaphoreSlim.WaitAsync(CancellationToken)"/></param>
    /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
    public static async Task<IDisposable> WaitDisposableAsync(
        this SemaphoreSlim semaphore,
        CancellationToken cancellationToken = default(CancellationToken))
    {
        var disposableLock = new DisposableSemaphoreLock(semaphore);
        await semaphore.WaitAsync(cancellationToken);
        return disposableLock;
    }

    private class DisposableSemaphoreLock : IDisposable
    {
        private SemaphoreSlim semaphore;

        public DisposableSemaphoreLock(SemaphoreSlim semaphore)
        {
            this.semaphore = semaphore;
        }

        public void Dispose()
        {
            if (this.semaphore == null)
            {
                return;
            }

            this.semaphore.Release();
            this.semaphore = null;
        }
    }
}
