// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Threading;
using Newtonsoft.Json.Linq;
using ChillNeteasePlaylist;
public static class NeteaseRequestTests
{
    public static void Run()
    {
        string directory=Path.Combine(Path.GetTempPath(),"ChillLyricRequest-"+Guid.NewGuid().ToString("N"));
        int requests=0,checks=0;
        Action<bool> check=delegate(bool value){if(!value)throw new Exception("lyric request check "+checks);checks++;};
        LyricSource.NeteaseRequest=delegate(long id,CancellationToken cancel){cancel.ThrowIfCancellationRequested();requests++;return JObject.Parse("{\"code\":200,\"lrc\":{\"lyric\":\"[00:01]Original\"},\"tlyric\":{\"lyric\":\"[00:01]Translation\"}}");};
        var track=new LyricTrack{Key="netease:42",Id="42"};
        var original=LyricSource.Load("netease",directory,track,false,CancellationToken.None);
        check(requests==1 && original.Document.Lines[0].Text=="Original");
        var translated=LyricSource.WithTranslation(original,"netease",directory,track,false,CancellationToken.None);
        check(requests==1 && translated.Translation.Lines[0].Text=="Translation");
        LyricSource.NeteaseRequest=delegate(long id,CancellationToken token){throw new InvalidOperationException("fixture network failure");};
        var cached=LyricSource.Load("netease",directory,track,false,CancellationToken.None);check(cached.FromLocal && cached.Document.Lines.Count==1);
        check(LyricSource.WithTranslation(cached,"netease",directory,track,false,CancellationToken.None).Translation.HasText);
        bool rejected=false;try{LyricSource.Load("netease",directory,track,true,CancellationToken.None);}catch(InvalidOperationException){rejected=true;}check(rejected);
        check(LyricSource.Load("netease",directory,track,false,CancellationToken.None).Document.HasText);
        rejected=false;try{LyricSource.Load("netease",directory,new LyricTrack{Key="netease:43",Id="43"},true,new CancellationToken(true));}catch(OperationCanceledException){rejected=true;}check(rejected);
        LyricSource.NeteaseRequest=null;
        Console.WriteLine("PASS "+checks+" native lyric routing, translation reuse and offline recovery checks");
    }
}
