// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Threading;
using ChillNeteasePlaylist;
public static class TranslationLiveProbe
{
    public static int Main(string[] args)
    {
        string cache=Path.Combine(Path.GetTempPath(),"ChillTranslation-"+Guid.NewGuid().ToString("N"));
        try
        {
            int withTranslation=0;
            foreach(string id in args)
            {
                var track=new LyricTrack{Id=id,Key="netease:"+id};
                var result=LyricSource.Load("netease",cache,track,true,CancellationToken.None);
                string original=File.ReadAllText(Path.Combine(cache,LyricSource.CacheName(track.Key)));
                result=LyricSource.WithTranslation(result,"netease",cache,track,false,CancellationToken.None);
                int aligned=0,shared=0,same=0;
                foreach(var line in result.Document.Lines)foreach(var translated in result.Translation.Lines)if(Math.Abs(line.Time-translated.Time)<0.001){shared++;if(line.Text==translated.Text)same++;}
                Console.WriteLine("timed="+result.Document.Timed+"/"+result.Translation.Timed+" shared="+shared+" same="+same);
                for(int n=0;n<result.Document.Lines.Count;n++)if(result.Document.TranslationFor(result.Translation,n).Length>0)aligned++;
                if(aligned>0)withTranslation++;
                if(File.ReadAllText(Path.Combine(cache,LyricSource.CacheName(track.Key)))!=original)throw new Exception("original changed");
                Console.WriteLine("sourceLines="+result.Document.Lines.Count+" translationLines="+result.Translation.Lines.Count+" aligned="+aligned);
            }
            if(withTranslation==0)return 2;
            Console.WriteLine("PASS official translated lyric fetch, alignment and original preservation");return 0;
        }
        catch(Exception e){Console.WriteLine("FAIL "+e.GetType().Name);return 1;}
        finally{try{if(Directory.Exists(cache))Directory.Delete(cache,true);}catch(IOException){}catch(UnauthorizedAccessException){}}
    }
}
