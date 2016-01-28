// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DotNet.CodeFormatting
{
    /// <summary>
    /// IDisposable wrapper to use with SemaphoreSlim
    /// </summary>
    internal sealed class SemaphoreLock : IDisposable
    {
        private SemaphoreSlim _semaphore;

        private SemaphoreLock(SemaphoreSlim semaphore)
        {
            _semaphore = semaphore;
        }

        public void Dispose()
        {
            var semaphore = Interlocked.Exchange(ref _semaphore, null);
            if (semaphore != null)
            {
                semaphore.Release();
            }
        }

        /// <summary>
        /// Wait for the semaphore, and return a lock that, when diposed, will release it
        /// </summary>
        /// <param name="semaphore">Semphore to lock</param>
        /// <returns>Lock that can be Disposed to release the semaphore</returns>
        public static async Task<SemaphoreLock> GetAsync(SemaphoreSlim semaphore)
        {
            await semaphore.WaitAsync();
            return new SemaphoreLock(semaphore);
        }
    }
}
