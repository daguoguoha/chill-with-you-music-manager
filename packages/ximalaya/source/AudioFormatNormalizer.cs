// SPDX-License-Identifier: GPL-3.0-or-later
using NAudio.Wave;
namespace ChillXimalayaPlaylist {
    internal static class AudioFormatNormalizer {
        sealed class StandardProvider : IWaveProvider {
            readonly IWaveProvider source;
            public WaveFormat WaveFormat { get; private set; }
            public StandardProvider(IWaveProvider value) { source=value; WaveFormat=value.WaveFormat.AsStandardWaveFormat(); }
            public int Read(byte[] buffer,int offset,int count) { return source.Read(buffer,offset,count); }
        }
        internal static IWaveProvider Pcm16(IWaveProvider source) { return new StandardProvider(source).ToSampleProvider().ToWaveProvider16(); }
    }
}
