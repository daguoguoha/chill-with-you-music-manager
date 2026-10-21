// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using NAudio.Wave;

namespace ChillNeteasePlaylist
{
    public sealed class DecodedAudio : IDisposable
    {
        public float[] Samples;
        public int Channels, SampleRate, Frames;
        readonly object gate = new object();
        FileStream stream;
        string pcmPath;
        byte[] buffer = new byte[0];
        public bool IsStreaming { get { return pcmPath != null; } }
        internal void OpenPcm(string path)
        {
            stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.RandomAccess);
            pcmPath = path;
        }
        // Unity's audio callbacks only read an already decoded, local PCM file.
        // No network, codec/COM calls or Unity APIs run on the audio thread.
        public void Read(float[] samples)
        {
            lock (gate)
            {
                Array.Clear(samples, 0, samples.Length);
                if (stream == null) return;
                int needed = checked(samples.Length * 4), received = 0;
                if (buffer.Length < needed) buffer = new byte[needed];
                try
                {
                    while (received < needed)
                    { int count = stream.Read(buffer, received, needed - received); if (count == 0) break; received += count; }
                    Buffer.BlockCopy(buffer, 0, samples, 0, received - received % 4);
                }
                catch (IOException) { Array.Clear(samples, 0, samples.Length); }
                catch (ObjectDisposedException) { Array.Clear(samples, 0, samples.Length); }
            }
        }
        public void Seek(int frame)
        {
            lock (gate) { if (stream != null) stream.Position = (long)Math.Max(0, Math.Min(Frames, frame)) * Channels * 4; }
        }
        public void Dispose()
        {
            lock (gate)
            {
                if (stream != null) { stream.Dispose(); stream = null; }
                if (pcmPath != null)
                { try { File.Delete(pcmPath); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
            }
        }
    }
    public static class AudioDecoder
    {
        const long InMemoryLimit = 32L * 1024 * 1024;
        const long PcmLimit = 2L * 1024 * 1024 * 1024;
        sealed class StandardProvider : IWaveProvider
        {
            readonly IWaveProvider source;
            public WaveFormat WaveFormat { get; private set; }
            public StandardProvider(IWaveProvider value) { source = value; WaveFormat = value.WaveFormat.AsStandardWaveFormat(); }
            public int Read(byte[] buffer, int offset, int count) { return source.Read(buffer, offset, count); }
        }
        // Lossless tracks can decode to 24/32-bit PCM. Normalize on the same worker
        // to float without quantizing to 16-bit, then retain native rate and channels.
        internal static ISampleProvider FloatSamples(IWaveProvider source)
        { return new StandardProvider(source).ToSampleProvider(); }
        sealed class ManagedMp3 : ISampleProvider, IDisposable
        {
            readonly NLayer.MpegFile reader;
            public WaveFormat WaveFormat { get; private set; }
            public ManagedMp3(string path)
            {
                reader = new NLayer.MpegFile(path);
                WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(reader.SampleRate, reader.Channels);
            }
            public int Read(float[] buffer, int offset, int count) { return reader.ReadSamples(buffer, offset, count); }
            public void Dispose() { reader.Dispose(); }
        }
        sealed class Input : IDisposable
        {
            internal IWaveProvider Provider;
            readonly IDisposable owner;
            internal Input(IWaveProvider provider, IDisposable resource) { Provider = provider; owner = resource; }
            public void Dispose() { owner.Dispose(); }
        }
        static Input Open(string path, string format)
        {
            // MP3 never calls Media Foundation/ACM or requires Windows media features.
            if (format == "mp3")
            {
                var managed = new ManagedMp3(path);
                return new Input(managed.ToWaveProvider(), managed);
            }
            var windows = new MediaFoundationReader(path, new MediaFoundationReader.MediaFoundationReaderSettings { RequestFloatOutput = true });
            return new Input(windows, windows);
        }
        public static DecodedAudio Decode(byte[] bytes, string format, Func<bool> canceled)
        {
            if (bytes == null || bytes.Length == 0 || bytes.Length > 64 * 1024 * 1024) throw new InvalidOperationException("音频文件为空或超过 64 MB，请选较短曲目。");
            if (format != "mp3" && format != "m4a" && format != "aac" && format != "flac") throw new InvalidOperationException("音频格式不受支持。");
            string inputPath = Path.Combine(Path.GetTempPath(), "ChillNetease-" + Guid.NewGuid().ToString("N") + "." + format);
            string pcmPath = Path.ChangeExtension(inputPath, ".pcm");
            bool retained = false;
            try
            {
                if (canceled()) throw new OperationCanceledException();
                File.WriteAllBytes(inputPath, bytes);
                int channels, rate;
                long total = 0;
                // Non-MP3 codecs and COM objects remain entirely on the worker thread.
                using (var reader = Open(inputPath, format))
                using (var output = new FileStream(pcmPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536))
                {
                    ISampleProvider normalized = FloatSamples(reader.Provider);
                    channels = normalized.WaveFormat.Channels; rate = normalized.WaveFormat.SampleRate;
                    if (channels < 1 || channels > 2 || rate < 8000 || rate > 192000)
                        throw new InvalidOperationException("Windows 未返回可用的 PCM 音频格式。");
                    float[] block = new float[16384]; byte[] outputBytes = new byte[65536]; int count;
                    while ((count = normalized.Read(block, 0, block.Length)) > 0)
                    {
                        if (canceled()) throw new OperationCanceledException();
                        if (total + count * 4L > PcmLimit) throw new InvalidOperationException("此音频过长，请选较短曲目（48 kHz 立体声约 90 分钟以内）。");
                        Buffer.BlockCopy(block, 0, outputBytes, 0, count * 4);
                        output.Write(outputBytes, 0, count * 4); total += count * 4L;
                    }
                }
                if (canceled()) throw new OperationCanceledException();
                if (total == 0 || total % (channels * 4) != 0) throw new InvalidOperationException("解码结果无效。");
                var result = new DecodedAudio { Channels = channels, SampleRate = rate, Frames = checked((int)(total / (channels * 4))) };
                if (total > InMemoryLimit)
                {
                    result.OpenPcm(pcmPath); retained = true;
                    return result;
                }
                byte[] pcm = File.ReadAllBytes(pcmPath);
                float[] samples = new float[pcm.Length / 4];
                if (canceled()) throw new OperationCanceledException();
                Buffer.BlockCopy(pcm, 0, samples, 0, pcm.Length);
                result.Samples = samples;
                return result;
            }
            finally
            {
                try { File.Delete(inputPath); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                if (!retained) { try { File.Delete(pcmPath); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
            }
        }
    }
}
