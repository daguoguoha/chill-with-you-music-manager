// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Diagnostics;
using System.IO;
using ChillNeteasePlaylist;
using NAudio.Wave;

public static class PlaybackCompatibilityTests
{
    static int passed;
    static void Check(bool value, string name)
    { if (!value) throw new Exception("FAIL " + name); passed++; Console.WriteLine("PASS " + name); }
    // Self-generated MPEG-1 Layer III silent frames, no third-party recording.
    static byte[] Silence(int frames)
    {
        byte[] bytes = new byte[417 * frames];
        for (int i = 0; i < frames; i++)
        { int at = i * 417; bytes[at] = 0xff; bytes[at + 1] = 0xfb; bytes[at + 2] = 0x90; bytes[at + 3] = 0; }
        return bytes;
    }
    public static int Main(string[] args)
    {
        try
        {
            using (var input = new RawSourceWaveStream(new MemoryStream(new byte[] {1,0,0,255,255,127}), new WaveFormat(48000,24,1)))
            {
                var provider = AudioDecoder.FloatSamples(input); float[] precise = new float[2];
                Check(provider.Read(precise,0,2)==2, "24-bit source conversion");
                Check(precise[0]>0 && Math.Abs(precise[0]-1f/8388608)<1e-12, "24-bit low-level precision preserved");
                Check(provider.WaveFormat.SampleRate==48000 && provider.WaveFormat.Channels==1, "native sample rate and channel count preserved");
            }
            using (var pcm = AudioDecoder.Decode(Silence(40), "mp3", delegate { return false; }))
                Check(pcm.Channels == 2 && pcm.SampleRate == 44100 && pcm.Frames > 0 && pcm.Samples != null, "managed MP3 decode");
            bool mediaLoaded = false;
            foreach (ProcessModule module in Process.GetCurrentProcess().Modules)
                if (module.ModuleName.Equals("mfplat.dll", StringComparison.OrdinalIgnoreCase)) mediaLoaded = true;
            Check(!mediaLoaded, "MP3 decode did not load Windows Media Foundation");
            bool canceled = false;
            try { AudioDecoder.Decode(Silence(40), "mp3", delegate { return true; }); }
            catch (OperationCanceledException) { canceled = true; }
            Check(canceled, "cancellation before decoding");
            int calls = 0; canceled = false;
            try { AudioDecoder.Decode(Silence(8000), "mp3", delegate { return ++calls > 5; }); }
            catch (OperationCanceledException) { canceled = true; }
            Check(canceled, "cancellation during decoding");
            bool invalid = false;
            try { using (var pcm = AudioDecoder.Decode(new byte[] { 1, 2, 3 }, "mp3", delegate { return false; })) { } }
            catch (Exception) { invalid = true; }
            Check(invalid, "invalid MP3 rejected");
            using (var pcm = AudioDecoder.Decode(Silence(8000), "mp3", delegate { return false; }))
            {
                Check(pcm.IsStreaming && pcm.Samples == null, "long MP3 uses bounded PCM storage");
                float[] block = new float[1024]; pcm.Read(block); pcm.Seek(pcm.Frames / 2); pcm.Read(block);
                Check(!float.IsNaN(block[0]), "streaming read and seek");
            }
            if (args.Length > 0 && !string.IsNullOrEmpty(args[0]))
                foreach (string file in Directory.GetFiles(args[0], "audio-*.mp3"))
                    using (var pcm = AudioDecoder.Decode(File.ReadAllBytes(file), "mp3", delegate { return false; }))
                    {
                        Check(pcm.Frames > 0 && pcm.Channels > 0 && pcm.SampleRate >= 8000, "real MP3 " + Path.GetFileName(file));
                        float peak = 0;
                        if (!pcm.IsStreaming) { foreach (float x in pcm.Samples) peak = Math.Max(peak, Math.Abs(x)); }
                        else
                        {
                            float[] block = new float[65536];
                            for (long at = 0; at < (long)pcm.Frames * pcm.Channels && peak <= 0.0001f; at += block.Length)
                            { pcm.Read(block); foreach (float x in block) peak = Math.Max(peak, Math.Abs(x)); }
                        }
                        Check(peak > 0.0001f, "real MP3 has audible PCM");
                    }
            Console.WriteLine("PASS " + passed + " playback compatibility checks");
            return 0;
        }
        catch (Exception error) { Console.WriteLine(error); return 1; }
    }
}
