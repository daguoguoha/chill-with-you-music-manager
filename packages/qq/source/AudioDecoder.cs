// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using NAudio.Wave;

namespace ChillQQMusicPlaylist
{
    public sealed class DecodedAudio
    {
        public float[] Samples;
        public int Channels, SampleRate;
    }
    public static class AudioDecoder
    {
        // NAudio wraps the codecs already supplied by Windows Media Foundation.
        // Keep the reader, COM objects, and all reads on the same worker thread.
        public static DecodedAudio Decode(byte[] bytes, string format, Func<bool> canceled)
        {
            if (bytes == null || bytes.Length == 0 || bytes.Length > 64 * 1024 * 1024) throw new InvalidOperationException("音频文件为空或超过 64 MB。");
            if (format != "mp3" && format != "m4a" && format != "aac" && format != "flac") throw new InvalidOperationException("音频格式不受支持。");
            string path = Path.Combine(Path.GetTempPath(), "ChillQQMusic-" + Guid.NewGuid().ToString("N") + "." + format);
            try
            {
                File.WriteAllBytes(path, bytes);
                using (MediaFoundationReader reader = new MediaFoundationReader(path))
                using (MemoryStream pcm = new MemoryStream())
                {
                    int channels = reader.WaveFormat.Channels, rate = reader.WaveFormat.SampleRate;
                    if (channels < 1 || channels > 2 || rate < 8000 || rate > 192000 || reader.WaveFormat.BitsPerSample != 16 || reader.WaveFormat.Encoding != WaveFormatEncoding.Pcm)
                        throw new InvalidOperationException("Windows 未返回可用的 PCM 音频格式。");
                    byte[] block = new byte[65536]; int count;
                    while ((count = reader.Read(block, 0, block.Length)) > 0)
                    {
                        if (canceled()) throw new OperationCanceledException();
                        if (pcm.Length + count > 128 * 1024 * 1024) throw new InvalidOperationException("解码音频过长，请选择较短的曲目。");
                        pcm.Write(block, 0, count);
                    }
                    if (canceled()) throw new OperationCanceledException();
                    byte[] data = pcm.ToArray();
                    if (data.Length == 0 || data.Length % (channels * 2) != 0) throw new InvalidOperationException("解码结果无效。");
                    float[] samples = new float[data.Length / 2];
                    for (int i = 0; i < samples.Length; i++) samples[i] = (short)(data[2 * i] | (data[2 * i + 1] << 8)) / 32768f;
                    return new DecodedAudio { Samples = samples, Channels = channels, SampleRate = rate };
                }
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }
    }
}
