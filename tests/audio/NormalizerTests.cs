using System;
using System.IO;
using NAudio.Wave;
public static class NormalizerTests {
    public static string Run(){
        int count=0;
        Func<IWaveProvider,IWaveProvider>[] converters={ChillQQMusicPlaylist.AudioFormatNormalizer.Pcm16,ChillKuGouPlaylist.AudioFormatNormalizer.Pcm16,ChillBilibiliPlaylist.AudioFormatNormalizer.Pcm16,ChillXimalayaPlaylist.AudioFormatNormalizer.Pcm16,ChillSaltPlayerPlaylist.AudioFormatNormalizer.Pcm16};
        foreach(var convert in converters) {
            foreach(int bits in new[]{16,24,32}) {
                byte[] data=new byte[bits/8*4];for(int i=0;i<4;i++)data[(i+1)*(bits/8)-1]=0x40;
                using(var input=new RawSourceWaveStream(new MemoryStream(data),new WaveFormat(44100,bits,1)))Check(convert(input),ref count);
            }
            byte[] floatData=new byte[16];for(int i=0;i<4;i++)Buffer.BlockCopy(BitConverter.GetBytes(0.5f),0,floatData,i*4,4);
            using(var input=new RawSourceWaveStream(new MemoryStream(floatData),WaveFormat.CreateIeeeFloatWaveFormat(44100,1)))Check(convert(input),ref count);
            byte[] ext=new byte[12];for(int i=0;i<4;i++)ext[i*3+2]=0x40;
            using(var input=new RawSourceWaveStream(new MemoryStream(ext),new WaveFormatExtensible(44100,24,1)))Check(convert(input),ref count);
        }
        return "PASS "+count+" five-provider PCM conversion checks";
    }
    static void Check(IWaveProvider converted,ref int count){var data=new byte[8];if(converted.Read(data,0,8)!=8||Math.Abs(BitConverter.ToInt16(data,0)/32768f-0.5f)>0.001f)throw new Exception("PCM conversion failed: "+count);count++;}
}
