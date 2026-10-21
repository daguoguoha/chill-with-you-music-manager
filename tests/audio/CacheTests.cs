// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;

namespace PLATFORM_NAMESPACE
{
    public static class CacheTests
    {
        static int checks;
        static void Check(bool condition) { if (!condition) throw new Exception("Cache assertion " + checks); checks++; }
        public static string Run()
        {
            string root = Path.Combine(Path.GetTempPath(), "ChillCacheTest-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                string prefix = AudioTemporaryFiles.Prefix;
                string stale = Path.Combine(root, prefix + Guid.NewGuid().ToString("N") + ".pcm");
                string compressed = Path.Combine(root, prefix + Guid.NewGuid().ToString("N") + ".mp3");
                string account = Path.Combine(root, "cookie");
                string unrelated = Path.Combine(root, "Other-" + Guid.NewGuid().ToString("N") + ".pcm");
                string malformed = Path.Combine(root, prefix + "user-music.mp3");
                string unknown = Path.Combine(root, prefix + Guid.NewGuid().ToString("N") + ".json");
                string active = Path.Combine(root, prefix + Guid.NewGuid().ToString("N") + ".pcm");
                string nested = Path.Combine(root, "nested"); Directory.CreateDirectory(nested);
                string child = Path.Combine(nested, prefix + Guid.NewGuid().ToString("N") + ".pcm");
                foreach (string file in new[] {stale, compressed, account, unrelated, malformed, unknown, active, child}) File.WriteAllBytes(file, new byte[] {1, 2, 3, 4});
                using (var held = new FileStream(active, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    Check(AudioTemporaryFiles.Cleanup(root) == 2);
                    Check(File.Exists(active)); Check(!File.Exists(stale)); Check(!File.Exists(compressed));
                    Check(File.Exists(account)); Check(File.Exists(unrelated)); Check(File.Exists(malformed)); Check(File.Exists(unknown)); Check(File.Exists(child));
                }
                Check(AudioTemporaryFiles.Cleanup(root) == 1); Check(!File.Exists(active));
                Check(AudioTemporaryFiles.Cleanup(root) == 0);
                string live = Path.Combine(root, prefix + Guid.NewGuid().ToString("N") + ".pcm"); File.WriteAllBytes(live, new byte[32]);
                using (var pcm = new DecodedAudio {Frames = 4, Channels = 2, SampleRate = 44100})
                {
                    pcm.OpenPcm(live); Check(AudioTemporaryFiles.Cleanup(root) == 0);
                    pcm.Read(new float[4]); pcm.Seek(0); Check(File.Exists(live));
                }
                Check(!File.Exists(live));
                Check(AudioTemporaryFiles.Cleanup(Path.Combine(root, "absent")) == 0);
                return "PASS " + checks + " temporary audio cleanup checks";
            }
            finally { Directory.Delete(root, true); }
        }
    }
}
