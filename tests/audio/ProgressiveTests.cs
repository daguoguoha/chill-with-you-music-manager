// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using ChillNeteasePlaylist;
public static class ProgressiveTests
{
    static int checks;
    static void Check(bool value) { if (!value) throw new Exception("Progressive assertion " + checks); checks++; }
    static void Until(Func<bool> condition) { var watch = Stopwatch.StartNew(); while (!condition()) { if(watch.ElapsedMilliseconds>5000) throw new Exception("Worker did not finish"); Thread.Sleep(10); } }
    sealed class Gated : MemoryStream
    {
        internal readonly ManualResetEventSlim Release = new ManualResetEventSlim();
        readonly CancellationToken cancel;
        internal Gated(byte[] value, CancellationToken token) : base(value) { cancel = token; }
        public override int Read(byte[] buffer, int offset, int count)
        { if (Position >= 417*500) Release.Wait(cancel); return base.Read(buffer, offset, Math.Min(97,count)); }
    }
    static byte[] Silence(int frames)
    {
        byte[] bytes = new byte[417*frames];
        for(int i=0;i<frames;i++){int at=417*i;bytes[at]=255;bytes[at+1]=251;bytes[at+2]=144;}
        return bytes;
    }
    public static string Run(string audioDirectory)
    {
        byte[] encoded=Silence(1600);
        using(var cancel=new CancellationTokenSource())
        using(var input=new Gated(encoded,cancel.Token))
        using(var audio=ProgressiveAudio.FromStream(input,encoded.Length,1600*1152d/44100,cancel.Token))
        {
            Until(()=>audio.Ready.IsCompleted); audio.Ready.GetAwaiter().GetResult();
            Check(!audio.Complete); Check(audio.Downloaded<encoded.Length); Check(audio.AvailableFrames>=8*44100);
            Check(audio.SampleRate==44100&&audio.Channels==2); Check(File.Exists(audio.TemporaryPath));
            bool locked=false; try { using(var exclusive=new FileStream(audio.TemporaryPath,FileMode.Open,FileAccess.Read,FileShare.None)){} } catch(IOException) {locked=true;} Check(locked);
            float[] block=new float[1024]; audio.Read(block); Check(audio.WaitingFrame<0);
            int target=(int)audio.AvailableFrames+44100*5; audio.Seek(target);audio.Read(block);Check(audio.WaitingFrame==target);Check(!audio.CanResume(target));
            input.Release.Set();Until(()=>audio.Complete||audio.Failure!=null);Check(audio.Failure==null);Check(audio.Complete);Check(audio.CanResume(target));
            audio.Seek(0);audio.Read(block);Check(audio.WaitingFrame<0);Check(audio.AvailableFrames>44100*30);
            string path=audio.TemporaryPath;audio.Dispose();Until(()=>!File.Exists(path));Check(!File.Exists(path));
        }
        using(var cancel=new CancellationTokenSource())
        using(var input=new Gated(encoded,cancel.Token))
        using(var audio=ProgressiveAudio.FromStream(input,encoded.Length,42,cancel.Token))
        {
            Until(()=>audio.Ready.IsCompleted); audio.Ready.GetAwaiter().GetResult();
            string path=audio.TemporaryPath;cancel.Cancel();audio.Dispose();Until(()=>!File.Exists(path));Check(!File.Exists(path));
        }
        using(var input=new MemoryStream(new byte[]{1,2,3}))
        using(var audio=ProgressiveAudio.FromStream(input,3,42,CancellationToken.None))
        { Until(()=>audio.Ready.IsCompleted);Check(audio.Ready.IsFaulted);var observed=audio.Ready.Exception; }
        using(var cancel=new CancellationTokenSource())
        using(var input=new Gated(encoded,cancel.Token))
        using(var audio=ProgressiveAudio.FromStream(input,encoded.Length+1,42,cancel.Token))
        {
            Until(()=>audio.Ready.IsCompleted);audio.Ready.GetAwaiter().GetResult();input.Release.Set();Until(()=>audio.Failure!=null);
            Check(!audio.Complete);Check(audio.Failure is PlaybackFailure&&((PlaybackFailure)audio.Failure).Code=="EMPTY_BODY");
        }
        int real=0;
        if(!string.IsNullOrEmpty(audioDirectory))
            foreach(string file in Directory.GetFiles(audioDirectory,"audio-*.mp3"))
            using(var input=new MemoryStream(File.ReadAllBytes(file)))
            using(var audio=ProgressiveAudio.FromStream(input,input.Length,300,CancellationToken.None))
            {
                Until(()=>audio.Complete||audio.Failure!=null);Check(audio.Failure==null&&audio.AvailableFrames>0);
                audio.Seek(0);float[] samples=new float[audio.SampleRate*audio.Channels];float peak=0;
                for(int second=0;second<15&&peak<=0.0001f;second++){audio.Read(samples);foreach(float value in samples)peak=Math.Max(peak,Math.Abs(value));}
                Check(peak>0.0001f);real++;
            }
        if(real>0)Console.WriteLine("PASS "+real+" real MP3 streams with audible PCM");
        return "PASS "+checks+" progressive playback checks";
    }
}
