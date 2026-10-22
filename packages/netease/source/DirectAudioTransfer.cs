// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ChillNeteasePlaylist
{
    internal sealed class DirectAudioTransfer : IDisposable
    {
        readonly CancellationTokenSource stop = new CancellationTokenSource();
        readonly object gate = new object();
        readonly Task<byte[]> task;
        long bytes;
        int disposed;
        internal Task<byte[]> Task { get { return task; } }
        internal long Bytes { get { return Interlocked.Read(ref bytes); } }
        internal DirectAudioTransfer(string url, long size)
            : this(delegate(CancellationToken token, Action<long> progress) { return AudioDownload.Read(url, token, size, progress); }) { }
        internal DirectAudioTransfer(Func<CancellationToken, Action<long>, byte[]> read)
        {
            task = System.Threading.Tasks.Task.Factory.StartNew(delegate
            {
                try { return read(stop.Token, delegate(long count) { Interlocked.Exchange(ref bytes, count); }); }
                finally { lock (gate) { stop.Dispose(); } }
            });
            task.ContinueWith(delegate(Task<byte[]> finished) { var observed = finished.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
        }
        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0) return;
            // Abort may wait on Mono's I/O lock; cancellation must never block a Unity frame.
            System.Threading.Tasks.Task.Factory.StartNew(delegate
            {
                lock (gate) { try { stop.Cancel(); } catch (ObjectDisposedException) { } }
            });
        }
    }
}
