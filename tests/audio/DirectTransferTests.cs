// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Diagnostics;
using System.Net;
using System.Threading;
using ChillNeteasePlaylist;
public static class DirectTransferTests
{
    public static string Run()
    {
        IWebProxy original = WebRequest.DefaultWebProxy;
        try
        {
            var configured = new WebProxy("http://127.0.0.1:1");
            WebRequest.DefaultWebProxy = configured;
            var request = AudioDownload.CreateRequest(new Uri("https://m1.music.126.net/test.mp3"));
            if (request.Proxy != null) throw new Exception("Audio inherited the proxy");
            if (!object.ReferenceEquals(WebRequest.DefaultWebProxy, configured))
                throw new Exception("Process proxy changed");
            request.Abort();
            using (var transfer = new DirectAudioTransfer((token, progress) => { progress(4); return new byte[4]; }))
            {
                if (!transfer.Task.Wait(3000) || transfer.Task.Result.Length != 4 || transfer.Bytes != 4) throw new Exception("Transfer failed");
            }
            using (var entered = new ManualResetEventSlim())
            using (var cancelled = new ManualResetEventSlim())
            using (var release = new ManualResetEventSlim())
            using (var transfer = new DirectAudioTransfer((token, progress) =>
            {
                using (token.Register(() => { cancelled.Set(); release.Wait(3000); }))
                { entered.Set(); token.WaitHandle.WaitOne(3000); token.ThrowIfCancellationRequested(); return new byte[0]; }
            }))
            {
                if (!entered.Wait(3000)) throw new Exception("Worker did not start");
                var watch = Stopwatch.StartNew(); transfer.Dispose();
                if (watch.ElapsedMilliseconds > 500) throw new Exception("Cancellation blocked the game thread");
                if (!cancelled.Wait(3000)) throw new Exception("Worker was not cancelled");
                release.Set();
                try { transfer.Task.Wait(3000); } catch (AggregateException) { }
                if (!transfer.Task.IsCompleted) throw new Exception("Cancelled worker did not exit");
            }
            using (var transfer = new DirectAudioTransfer((token, progress) => { throw new InvalidOperationException("fixture"); }))
            {
                try { transfer.Task.Wait(3000); } catch (AggregateException) { }
                if (!transfer.Task.IsFaulted) throw new Exception("Failure was lost");
            }
            return "PASS direct proxy isolation, progress, completion, failure and nonblocking cancellation";
        }
        finally { WebRequest.DefaultWebProxy = original; }
    }
}
