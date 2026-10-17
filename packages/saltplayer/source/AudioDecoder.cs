// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using NAudio.Wave;

namespace ChillSaltPlayerPlaylist
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
                int needed = checked(samples.Length * 2), received = 0;
                if (buffer.Length < needed) buffer = new byte[needed];
                try
                {
                    while (received < needed)
                    { int count = stream.Read(buffer, received, needed - received); if (count == 0) break; received += count; }
                    for (int i = 0; i < received / 2; i++) samples[i] = (short)(buffer[2*i] | (buffer[2*i+1] << 8)) / 32768f;
                }
                catch (IOException) { Array.Clear(samples, 0, samples.Length); }
                catch (ObjectDisposedException) { Array.Clear(samples, 0, samples.Length); }
            }
        }
        public void Seek(int frame)
        {
            lock (gate) { if (stream != null) stream.Position = (long)Math.Max(0, Math.Min(Frames, frame)) * Channels * 2; }
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
        const long InMemoryLimit = 128L * 1024 * 1024;
        const long PcmLimit = 1024L * 1024 * 1024;
        public static DecodedAudio DecodeFile(string path, Func<bool> canceled)
        {
            string inputPath = Rules.LocalPath(path, null);
            if (inputPath == null || !Rules.IsAudio(inputPath) || !File.Exists(inputPath)) throw new InvalidOperationException("本地音频已移动或丢失，请重新导入。");
            string pcmPath = Path.Combine(Path.GetTempPath(), "ChillSaltPlayer-" + Guid.NewGuid().ToString("N") + ".pcm");
            bool retained = false;
            try
            {
                if (canceled()) throw new OperationCanceledException();
                int channels, rate;
                long total = 0;
                // Keep Media Foundation and COM objects entirely on this worker thread.
                using (var reader = new MediaFoundationReader(inputPath))
                using (var output = new FileStream(pcmPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536))
                {
                    channels = reader.WaveFormat.Channels; rate = reader.WaveFormat.SampleRate;
                    if (channels < 1 || channels > 2 || rate < 8000 || rate > 192000 || reader.WaveFormat.BitsPerSample != 16 || reader.WaveFormat.Encoding != WaveFormatEncoding.Pcm)
                        throw new InvalidOperationException("Windows 未返回可用的 PCM 音频格式。");
                    byte[] block = new byte[65536]; int count;
                    while ((count = reader.Read(block, 0, block.Length)) > 0)
                    {
                        if (canceled()) throw new OperationCanceledException();
                        if (total + count > PcmLimit) throw new InvalidOperationException("此音频过长，请选择较短音频（48 kHz 立体声约 90 分钟以内）。");
                        output.Write(block, 0, count); total += count;
                    }
                }
                if (canceled()) throw new OperationCanceledException();
                if (total == 0 || total % (channels * 2) != 0) throw new InvalidOperationException("解码结果无效。");
                var result = new DecodedAudio { Channels = channels, SampleRate = rate, Frames = checked((int)(total / (channels * 2))) };
                if (total > InMemoryLimit)
                {
                    result.OpenPcm(pcmPath); retained = true;
                    return result;
                }
                byte[] pcm = File.ReadAllBytes(pcmPath);
                float[] samples = new float[pcm.Length / 2];
                for (int i = 0; i < samples.Length; i++)
                {
                    if ((i & 0xfffff) == 0 && canceled()) throw new OperationCanceledException();
                    samples[i] = (short)(pcm[2*i] | (pcm[2*i+1] << 8)) / 32768f;
                }
                result.Samples = samples;
                return result;
            }
            finally
            {
                if (!retained) { try { File.Delete(pcmPath); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
            }
        }
    }
}
