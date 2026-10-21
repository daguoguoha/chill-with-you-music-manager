// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Net;
using System.Threading;
using ChillNeteasePlaylist;
public static class AudioDownloadTests
{
    static int checks;
    static void Check(bool value) {if(!value)throw new Exception("Audio download assertion "+checks);checks++;}
    static void Failure(Action action,string code)
    {try{action();throw new Exception("Expected failure");}catch(PlaybackFailure error){Check(error.Code==code);}}
    sealed class CancelStream : MemoryStream
    {
        readonly CancellationTokenSource cancel;
        public CancelStream(CancellationTokenSource value):base(new byte[]{1,2,3}){cancel=value;}
        public override int Read(byte[] buffer,int offset,int count){int read=base.Read(buffer,offset,count);cancel.Cancel();return read;}
    }
    public static string Run()
    {
        Check(AudioDownload.DeadlineSeconds(-1)==120);
        Check(AudioDownload.DeadlineSeconds(1024*1024)==120);
        Check(AudioDownload.DeadlineSeconds(11L*1024*1024)==600);
        Check(AudioDownload.DeadlineSeconds(long.MaxValue)==600);
        long progress=0;
        Check(AudioDownload.ReadBounded(new MemoryStream(new byte[]{1,2,3,4}),4,CancellationToken.None,4,n=>progress=n).Length==4&&progress==4);
        foreach(string url in new[]{"https://m1.music.126.net/audio.mp3?token=fixture","http://m1.music.126.net/audio.mp3","https://m1.music.163.com/audio.mp3"})
            Check(AudioDownload.Target(url).Scheme=="https");
        foreach(string url in new[]{"https://music.126.net.evil.invalid/a","https://evil.invalid/a","ftp://m1.music.126.net/a","https://secret@m1.music.126.net/a","https://m1.music.126.net:8443/a","file:///C:/test","https://127.0.0.1/a"})
            Failure(()=>AudioDownload.Target(url),"INVALID_URL");
        Check(AudioDownload.ReadBounded(new MemoryStream(new byte[]{1,2,3,4}),4,CancellationToken.None,4).Length==4);
        Check(AudioDownload.ReadBounded(new MemoryStream(new byte[]{1,2,3}),-1,CancellationToken.None,4).Length==3);
        Failure(()=>AudioDownload.ReadBounded(new MemoryStream(new byte[]{1,2,3,4,5}),-1,CancellationToken.None,4),"SIZE_LIMIT");
        Failure(()=>AudioDownload.ReadBounded(new MemoryStream(new byte[]{1}),5,CancellationToken.None,4),"SIZE_LIMIT");
        Failure(()=>AudioDownload.ReadBounded(new MemoryStream(),0,CancellationToken.None,4),"EMPTY_BODY");
        Failure(()=>AudioDownload.ReadBounded(new MemoryStream(new byte[]{1,2}),3,CancellationToken.None,4),"EMPTY_BODY");
        using(var cancel=new CancellationTokenSource())
        {
            cancel.Cancel();try{AudioDownload.ReadBounded(new MemoryStream(new byte[]{1}),1,cancel.Token,4);throw new Exception("Expected cancellation");}catch(OperationCanceledException){Check(true);}
        }
        using(var cancel=new CancellationTokenSource())
        using(var stream=new CancelStream(cancel))
        {try{AudioDownload.ReadBounded(stream,3,cancel.Token,4);throw new Exception("Expected cancellation");}catch(OperationCanceledException){Check(true);}}
        foreach(WebExceptionStatus status in new[]{WebExceptionStatus.ProxyNameResolutionFailure,WebExceptionStatus.NameResolutionFailure,WebExceptionStatus.TrustFailure,WebExceptionStatus.SecureChannelFailure,WebExceptionStatus.Timeout,WebExceptionStatus.ConnectFailure,WebExceptionStatus.ConnectionClosed,WebExceptionStatus.KeepAliveFailure,WebExceptionStatus.ReceiveFailure})
        {
            var error=AudioDownload.Failure(new WebException("SECRET https://private.invalid/?cookie=SECRET",status));
            Check(error.Code.StartsWith("NETWORK_")&&error.Retryable&&PlaybackFailure.StopQueue(error));
            Check(!error.Message.Contains("SECRET"));
        }
        return "PASS "+checks+" system audio download checks";
    }
}
