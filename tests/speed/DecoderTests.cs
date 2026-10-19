using System;
using System.IO;
using NAudio.Wave;
using ChillNeteasePlaylist;
public static class DecoderTests {
    public static string Run() {
        int count=0;
        foreach(int bits in new[]{16,24,32}) {
            byte[] bytes=new byte[bits/8*4];
            for(int i=0;i<4;i++)bytes[i*(bits/8)+(bits/8)-1]=0x40;
            using(var input=new RawSourceWaveStream(new MemoryStream(bytes),new WaveFormat(44100,bits,1))) {
                var converted=AudioDecoder.Pcm16(input);var output=new byte[8];
                if(converted.Read(output,0,8)!=8 || Math.Abs(BitConverter.ToInt16(output,0)/32768f-0.5f)>0.001f)throw new Exception("PCM conversion failed: "+bits);
                count++;
            }
        }
        byte[] floating=new byte[16];for(int i=0;i<4;i++)Buffer.BlockCopy(BitConverter.GetBytes(0.5f),0,floating,i*4,4);
        using(var input=new RawSourceWaveStream(new MemoryStream(floating),WaveFormat.CreateIeeeFloatWaveFormat(44100,1))) {
            var output=new byte[8];if(AudioDecoder.Pcm16(input).Read(output,0,8)!=8 || Math.Abs(BitConverter.ToInt16(output,0)/32768f-0.5f)>0.001f)throw new Exception("float conversion failed");count++;
        }
        try{AudioDecoder.Decode(new byte[0],"flac",()=>false);throw new Exception("empty accepted");}catch(InvalidOperationException){count++;}
        try{AudioDecoder.Decode(new byte[]{1},"flac",()=>true);throw new Exception("cancel ignored");}catch(OperationCanceledException){count++;}
        return "PASS "+count+" decoder checks";
    }
}
