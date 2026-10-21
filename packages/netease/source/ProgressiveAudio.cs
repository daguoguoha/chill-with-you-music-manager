// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ChillNeteasePlaylist
{
    // Network and decoding run on a worker. Unity callbacks only read available local PCM.
    internal sealed class ProgressiveAudio : IDisposable
    {
        readonly object gate = new object();
        readonly CancellationTokenSource stop = new CancellationTokenSource();
        readonly TaskCompletionSource<ProgressiveAudio> ready = new TaskCompletionSource<ProgressiveAudio>();
        FileStream writer, reader;
        long available, position, downloaded;
        int waiting = -1;
        bool complete, disposed;
        Exception failure;
        byte[] readBuffer = new byte[65536];
        internal int Channels { get; private set; }
        internal int SampleRate { get; private set; }
        internal int Frames { get; private set; }
        internal Task<ProgressiveAudio> Ready { get { return ready.Task; } }
        internal long Downloaded { get { return Interlocked.Read(ref downloaded); } }
        internal long AvailableFrames { get { lock (gate) return Channels == 0 ? 0 : available / (Channels * 4); } }
        internal bool Complete { get { lock (gate) return complete; } }
        internal Exception Failure { get { lock (gate) return failure; } }
        internal int WaitingFrame { get { lock (gate) return waiting; } }
        internal string TemporaryPath { get; private set; }

        ProgressiveAudio()
        {
            TemporaryPath = Path.Combine(Path.GetTempPath(), AudioTemporaryFiles.Prefix + Guid.NewGuid().ToString("N") + ".pcm");
            writer = new FileStream(TemporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete, 65536, FileOptions.DeleteOnClose);
            try { reader = new FileStream(TemporaryPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 65536, FileOptions.RandomAccess | FileOptions.DeleteOnClose); }
            catch { writer.Dispose(); throw; }
        }
        internal static ProgressiveAudio Start(SongUrl first, double duration, Func<SongUrl> refresh)
        {
            var audio = new ProgressiveAudio();
            Task.Factory.StartNew(delegate
            {
                try
                {
                    SongUrl url = first;
                    for (int attempt = 0; ; attempt++)
                    {
                        try
                        {
                            audio.stop.Token.ThrowIfCancellationRequested();
                            if (attempt > 0) url = refresh();
                            if (url == null || url.isTrial || !string.Equals(url.type, "mp3", StringComparison.OrdinalIgnoreCase)) throw new PlaybackFailure("取地址", "UNSUPPORTED_FORMAT", "重试音源格式已改变，请重新点播。", false);
                            AudioDownload.Consume(url.url, audio.stop.Token, url.size, delegate(Stream input, long length, CancellationToken token) { audio.Decode(input, length, duration, token); return true; });
                            break;
                        }
                        catch (PlaybackFailure error)
                        {
                            if (!error.Retryable || attempt == 2) throw;
                            if (audio.stop.Token.WaitHandle.WaitOne((attempt + 1) * 500)) audio.stop.Token.ThrowIfCancellationRequested();
                        }
                    }
                }
                catch (OperationCanceledException) { audio.ready.TrySetCanceled(); }
                catch (Exception error)
                {
                    var safe = error as PlaybackFailure ?? PlaybackFailure.Decode(error, "mp3");
                    lock (audio.gate) audio.failure = safe;
                    audio.ready.TrySetException(safe);
                }
                finally
                {
                    lock (audio.gate) { if (audio.writer != null) { audio.writer.Dispose(); audio.writer = null; } }
                    audio.stop.Dispose();
                }
            });
            // Faults must also be observed if the selection changes before startup completes.
            audio.Ready.ContinueWith(delegate(Task<ProgressiveAudio> task) { var observed = task.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
            return audio;
        }
        // Test entry uses the same incremental decoder without any network or account.
        internal static ProgressiveAudio FromStream(Stream input, long length, double duration, CancellationToken token)
        {
            var audio = new ProgressiveAudio();
            Task.Factory.StartNew(delegate
            {
                try { audio.Decode(input, length, duration, token); }
                catch (OperationCanceledException) { audio.ready.TrySetCanceled(); }
                catch (Exception error) { lock (audio.gate) audio.failure = error; audio.ready.TrySetException(error); }
                finally { lock (audio.gate) { if (audio.writer != null) { audio.writer.Dispose(); audio.writer = null; } } }
            });
            return audio;
        }
        void Decode(Stream source, long length, double duration, CancellationToken token)
        {
            using (var input = new CountingInput(source, token, delegate(long count) { Interlocked.Exchange(ref downloaded, count); }))
            using (var decoder = new NLayer.MpegFile(input))
            {
                int channels = decoder.Channels, rate = decoder.SampleRate;
                double headerDuration = decoder.Duration.TotalSeconds;
                double seconds = headerDuration > 0 ? headerDuration : duration;
                if (channels < 1 || channels > 2 || rate < 8000 || rate > 192000 || double.IsNaN(seconds) || seconds <= 0 || seconds > 5400) throw new InvalidDataException("Invalid stream format or duration");
                lock (gate)
                {
                    if (disposed) throw new OperationCanceledException();
                    if (Channels != 0 && (Channels != channels || SampleRate != rate)) throw new InvalidDataException("Stream format changed");
                    Channels = channels; SampleRate = rate;
                    if (Frames == 0) Frames = checked((int)Math.Ceiling(seconds * rate));
                }
                float[] samples = new float[8192]; byte[] bytes = new byte[samples.Length * 4];
                long decoded = 0;
                int count;
                while ((count = decoder.ReadSamples(samples, 0, samples.Length)) > 0)
                {
                    token.ThrowIfCancellationRequested();
                    long end = decoded + count * 4L;
                    if (end > 2L * 1024 * 1024 * 1024) throw new InvalidDataException("PCM limit");
                    Buffer.BlockCopy(samples, 0, bytes, 0, count * 4);
                    lock (gate)
                    {
                        if (disposed) throw new OperationCanceledException();
                        // A fresh signed URL may restart from byte zero; retain already decoded PCM.
                        int skip = (int)Math.Min(count * 4L, Math.Max(0, available - decoded));
                        if (skip < count * 4)
                        {
                            writer.Write(bytes, skip, count * 4 - skip); writer.Flush();
                            available += count * 4 - skip;
                        }
                    }
                    decoded = end;
                    if (AvailableFrames >= (long)rate * 8) ready.TrySetResult(this);
                }
                // Read trailing tags too so a short HTTP body cannot silently look complete.
                while (input.Read(bytes, 0, bytes.Length) > 0) { }
                if (length >= 0 && input.Position != length) throw new PlaybackFailure("下载", "EMPTY_BODY", "音频下载不完整，请重试。", true);
                if (decoded == 0 || decoded < available) throw new InvalidDataException("Truncated decoded stream");
                lock (gate) complete = true;
                ready.TrySetResult(this);
            }
        }
        internal bool CanResume(int frame)
        { lock (gate) return complete || available >= ((long)frame + SampleRate * 3L) * Channels * 4; }
        internal void Read(float[] samples)
        {
            Array.Clear(samples, 0, samples.Length);
            lock (gate)
            {
                if (disposed || reader == null || Channels == 0) return;
                int needed = checked(samples.Length * 4);
                if (!complete && position + needed > available) { if (waiting < 0) waiting = (int)(position / (Channels * 4)); return; }
                if (waiting >= 0) return;
                int count = (int)Math.Min(needed, Math.Max(0, available - position));
                if (count == 0) return;
                if (readBuffer.Length < count) readBuffer = new byte[count];
                try
                {
                    reader.Position = position;
                    int received = 0;
                    while (received < count) { int n = reader.Read(readBuffer, received, count - received); if (n == 0) break; received += n; }
                    Buffer.BlockCopy(readBuffer, 0, samples, 0, received - received % 4); position += received;
                }
                catch (IOException error) { failure = PlaybackFailure.Decode(error, "mp3"); }
                catch (ObjectDisposedException) { }
            }
        }
        internal void Seek(int frame)
        {
            lock (gate)
            {
                frame = Math.Max(0, Math.Min(Frames - 1, frame)); position = (long)frame * Channels * 4;
                waiting = !complete && position >= available ? frame : -1;
            }
        }
        public void Dispose()
        {
            lock (gate)
            {
                if (disposed) return; disposed = true;
                if (reader != null) { reader.Dispose(); reader = null; }
            }
            // HttpWebRequest.Abort can wait on Mono's I/O lock. Never run it on Unity's main thread.
            Task.Factory.StartNew(delegate { try { stop.Cancel(); } catch (ObjectDisposedException) { } });
        }
        sealed class CountingInput : Stream
        {
            readonly Stream source; readonly CancellationToken token; readonly Action<long> progress;
            long count;
            internal CountingInput(Stream input, CancellationToken cancel, Action<long> report) { source = input; token = cancel; progress = report; }
            public override int Read(byte[] buffer, int offset, int size)
            {
                token.ThrowIfCancellationRequested();
                int n;
                try { n = source.Read(buffer, offset, Math.Min(size, (int)Math.Min(int.MaxValue, AudioDownload.Limit - count + 1))); }
                catch (System.Net.WebException error) { token.ThrowIfCancellationRequested(); throw AudioDownload.Failure(error); }
                catch (IOException) { token.ThrowIfCancellationRequested(); throw new PlaybackFailure("下载", "NETWORK_IO", "音频连接中断，正在等待重试。", true); }
                token.ThrowIfCancellationRequested(); count += n;
                if (count > AudioDownload.Limit) throw new PlaybackFailure("下载", "SIZE_LIMIT", "音频超过 64 MB。", false);
                progress(count); return n;
            }
            public override bool CanRead { get { return true; } }
            public override bool CanWrite { get { return false; } }
            public override bool CanSeek { get { return false; } }
            public override long Length { get { throw new NotSupportedException(); } }
            public override long Position { get { return count; } set { throw new NotSupportedException(); } }
            public override void Flush() { }
            public override long Seek(long offset, SeekOrigin origin) { throw new NotSupportedException(); }
            public override void SetLength(long value) { throw new NotSupportedException(); }
            public override void Write(byte[] buffer, int offset, int size) { throw new NotSupportedException(); }
        }
    }
}
