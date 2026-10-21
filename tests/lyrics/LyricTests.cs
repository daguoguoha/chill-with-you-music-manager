// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Text;
using System.Threading;
using ChillNeteasePlaylist;

class LyricTests
{
    static int passed;
    static void Check(bool condition, string name) { if (!condition) throw new Exception(name); passed++; Console.WriteLine("PASS " + name); }
    static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "live")
        {
            var source = new[] { new LyricTrack { Id = "186016" }, new LyricTrack { Id = "0039MnYb0qxYhV" } };
            string[] names = { "netease", "qq" };
            for (int i = 0; i < names.Length; i++)
            {
                try { var document = LyricDocument.Parse(LyricSource.Fetch(names[i], source[i], CancellationToken.None)); Console.WriteLine(names[i] + " timed=" + document.Timed + " lines=" + document.Lines.Count); }
                catch (Exception e) { Console.WriteLine(names[i] + " failed=" + e.GetType().Name); }
            }
            return;
        }
        var lrc = LyricDocument.Parse("[ti:Synthetic test]\n[00:05.5]First synthetic line\n[00:10.25]Second synthetic line\n[00:20.125]Third synthetic line");
        Check(lrc.Timed && lrc.Lines.Count == 3, "timestamp parsing and metadata exclusion");
        Check(lrc.IndexAt(5.49) == -1 && lrc.IndexAt(5.5) == 0, "before first lyric and exact timestamp");
        Check(lrc.IndexAt(10.249) == 0 && lrc.IndexAt(10.25) == 1, "centisecond boundary");
        Check(lrc.IndexAt(20.124) == 1 && lrc.IndexAt(20.125) == 2, "millisecond boundary");
        Check(lrc.IndexAt(100) == 2 && lrc.IndexAt(6) == 0, "forward and backward seek");
        Check(lrc.IndexAt(double.NaN) == -1 && lrc.IndexAt(double.PositiveInfinity) == -1, "invalid position");
        var repeated = LyricDocument.Parse("[00:04][00:02]Repeated line\n[00:01]Earlier line");
        Check(repeated.Lines.Count == 3 && repeated.Lines[0].Time == 1 && repeated.Lines[2].Time == 4, "multiple timestamps and chronological sorting");
        var translation = LyricDocument.Parse("[00:02]Original synthetic text\n[00:02]Translated synthetic text");
        Check(translation.Lines.Count == 1 && translation.Lines[0].Text.Contains("\n"), "same-time translation merged");
        Check(LyricDocument.Parse("[offset:500]\n[00:01]Earlier").Lines[0].Time == .5, "positive LRC offset");
        Check(LyricDocument.Parse("[offset:-500]\n[00:01]Later").Lines[0].Time == 1.5, "negative LRC offset");
        Check(LyricDocument.Parse("[00:01]One\n[offset:250]").Lines[0].Time == .75, "offset tag can follow lyric");
        Check(LyricDocument.Parse("[00:00.200]One\n[offset:500]").IndexAt(0) == 0, "negative adjusted timestamp");
        var empty = LyricDocument.Parse("[00:01]One\n[00:03]\n[00:08]Two");
        Check(empty.Lines[empty.IndexAt(4)].Text == "", "empty timestamp clears lyric gap");
        Check(LyricDocument.Parse("").Lines.Count == 0, "empty source");
        var plain = LyricDocument.Parse("[ar:Synthetic artist]\nA plain line\nAnother line");
        Check(!plain.Timed && plain.Lines.Count == 2 && plain.IndexAt(400) == 0, "untimed lyrics never claim synchronized timing");
        Check(LyricDocument.Parse("\uFEFF[00:01,50]Test\r\n").Lines[0].Time == 1.5, "BOM and comma fractions");
        Check(!LyricDocument.Clean("Hello\u0000\u0001world").Contains("\0"), "control character filtering");
        Check(LyricDocument.Parse("[00:01]<00:01.00>Synthetic").Lines[0].Text == "Synthetic", "enhanced LRC word markers");
        bool rejected = false; try { LyricDocument.Parse(new string('x', LyricDocument.MaxCharacters + 1)); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "oversized lyric rejected");
        string cache = Path.Combine(Path.GetTempPath(), "chill-lyric-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(cache);
        string input = Path.Combine(cache, "synthetic.lrc");
        File.WriteAllText(input, "[00:01]Synthetic local lyric", Encoding.Unicode);
        Check(LyricSource.ReadLocal(input).Contains("Synthetic"), "UTF16 local import");
        File.WriteAllText(input, "[00:01]测试歌词", Encoding.GetEncoding(54936));
        Check(LyricSource.ReadLocal(input).Contains("测试歌词"), "GB18030 local import");
        Check(LyricSource.CacheName("netease:../danger").Length == 68 && !LyricSource.CacheName("a").Contains("/"), "stable filesystem-safe identity");
        Check(LyricSource.CacheName("netease:1") != LyricSource.CacheName("qq:1"), "provider cache separation");
        rejected = false; try { LyricSource.ReadLocal("https://example.com/test.lrc"); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "remote path not treated as local import");
        rejected = false; try { LyricSource.ReadLocal(@"\\server\share\test.lrc"); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "UNC import rejected");
        rejected = false; try { LyricSource.ReadLocal(input + ":stream.lrc"); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "alternate data stream rejected");
        var track = new LyricTrack { Key = "test:1", Id = "1", Title = "Synthetic" };
        var imported = LyricSource.Import(cache, track, null, "[00:01]Synthetic imported lyric", CancellationToken.None);
        Check(imported.Document.Timed && File.Exists(Path.Combine(cache, LyricSource.CacheName(track.Key))), "manual import persisted");
        var loaded = LyricSource.Load("bilibili", cache, track, false, CancellationToken.None);
        Check(loaded.FromLocal && loaded.Document.Lines.Count == 1, "manual cache survives offline reload");
        var local = new LyricTrack { Key = "saltplayer:1", LocalPath = Path.Combine(cache, "synthetic.mp3") };
        var sidecar = LyricSource.Load("saltplayer", cache, local, false, CancellationToken.None);
        Check(sidecar.FromLocal && sidecar.Document.Lines.Count == 1, "local same-name LRC sidecar");
        Check(LyricSource.Fetch("ximalaya", track, CancellationToken.None) == null, "no invented cloud lyric endpoint");
        var cancelled = new CancellationToken(true);
        rejected = false; try { LyricSource.Load("netease", cache, new LyricTrack { Key = "test:cancel" }, true, cancelled); } catch (OperationCanceledException) { rejected = true; }
        Check(rejected, "cancel before network");
        Check(LyricSource.DecodeBase64(Convert.ToBase64String(Encoding.UTF8.GetBytes("[00:01]Synthetic &amp; text"))).Contains("& text"), "base64 and HTML decode");
        var originalTranslation=LyricDocument.Parse("[00:01]Foreign original\n[00:02]Second original\n[00:03]\n[00:04]Same");
        var translated=LyricDocument.Parse("[00:01]中文译文\n[00:02.10]第二句\n[00:03]不应显示\n[00:04]Same");
        Check(originalTranslation.TranslationFor(translated,0)=="中文译文","exact translation timestamp");
        Check(originalTranslation.TranslationFor(translated,1)=="第二句","small timestamp drift aligned");
        Check(originalTranslation.TranslationFor(translated,2)=="","blank original clears translation");
        Check(originalTranslation.TranslationFor(translated,3)=="","duplicate original and translation hidden");
        Check(originalTranslation.TranslationFor(translated,-1)=="","no translation before first line");
        Check(originalTranslation.TranslationFor(translated,99)=="","invalid translation index safe");
        Check(originalTranslation.TranslationFor(LyricDocument.Parse("[00:00]Old verse"),0)=="","missing translation never leaks previous verse");
        Check(originalTranslation.TranslationFor(LyricDocument.Parse("[00:01]Exact\n[00:01.2]Nearby"),0)=="Exact","closest original timestamp wins");
        Check(originalTranslation.TranslationFor(LyricDocument.Parse("plain text"),0)=="","untimed translation not guessed");
        Check(originalTranslation.TranslationFor(null,0)=="","missing document safe");
        var separate=new LyricTrack {Key="translation:separate",Id="1"};
        var first=LyricSource.Import(cache,separate,null,"[00:01]Foreign original",CancellationToken.None);
        string originalCache=Path.Combine(cache,LyricSource.CacheName(separate.Key)),beforeTranslation=File.ReadAllText(originalCache);
        var paired=LyricSource.ImportTranslation(first,cache,separate,null,"[00:01]中文译文",CancellationToken.None);
        Check(paired.Document.Lines[0].Text=="Foreign original" && File.ReadAllText(originalCache)==beforeTranslation,"translation import never overwrites original");
        Check(File.Exists(Path.Combine(cache,LyricSource.TranslationCacheName(separate.Key))),"translation has separate atomic cache");
        var offlinePair=LyricSource.WithTranslation(LyricSource.Load("bilibili",cache,separate,false,CancellationToken.None),"bilibili",cache,separate,false,CancellationToken.None);
        Check(offlinePair.Document.TranslationFor(offlinePair.Translation,0)=="中文译文","all providers reload imported translation offline");
        Check(LyricSource.TranslationCacheName("netease:1")!=LyricSource.TranslationCacheName("kugou:1"),"translation cache separated across platforms");
        rejected=false;try{LyricSource.ImportTranslation(first,cache,separate,null,"Untimed translation",CancellationToken.None);}catch(InvalidOperationException){rejected=true;}
        Check(rejected,"translation import requires aligned timed LRC");
        rejected=false;try{LyricSource.ImportTranslation(first,cache,separate,null,"[00:01]Canceled",cancelled);}catch(OperationCanceledException){rejected=true;}
        Check(rejected && File.ReadAllText(originalCache)==beforeTranslation,"cancelled translation import preserves existing files");
        var absent=LyricSource.WithTranslation(new LyricResult(),"ximalaya",cache,new LyricTrack{Key="translation:absent"},false,CancellationToken.None);
        Check(absent.Translation.Lines.Count==0 && absent.Message.Contains("暂无译文"),"unsupported provider reports absence without guessed endpoint");
        Check(!LyricDocument.Parse("[00:01]\n[00:02]").HasText,"blank translation timestamps are not a real translation");
        rejected=false;try{LyricSource.ImportTranslation(first,cache,separate,null,"[00:01]\n[00:02]",CancellationToken.None);}catch(InvalidOperationException){rejected=true;}
        Check(rejected,"blank imported translation rejected");
        Check(originalTranslation.TranslationFor(LyricDocument.Parse("[00:01.1]Nearest\n[00:01.2]Farther"),0)=="Nearest","closest future timestamp wins over later nearby lines");
        Console.WriteLine("TOTAL " + passed);
    }
}
