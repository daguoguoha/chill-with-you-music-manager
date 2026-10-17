// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
namespace ChillSaltPlayerPlaylist
{
    public static class Bridge
    {
        static string directory;
        static Library library=new Library();
        public static string LastImportDetails {get;private set;}
        public static string LastRoot {get;private set;}
        public static int Init(string path)
        {
            directory=path;Directory.CreateDirectory(path);library=new Library();LastRoot="";
            string file=Path.Combine(path,"library.json");
            if(File.Exists(file))
            {
                if(new FileInfo(file).Length>16*1024*1024)throw new InvalidOperationException("本地歌单索引过大，请先备份 library.json 后检查。");
                try{library=JsonConvert.DeserializeObject<Library>(File.ReadAllText(file,Encoding.UTF8));}
                catch(JsonException){throw new InvalidOperationException("本地歌单索引损坏，请保留 library.json 并从备份恢复。");}
                if(library==null || library.version!=1 || library.playlists==null || library.playlists.Count>1000)throw new InvalidOperationException("本地歌单索引版本或结构无效。");
                foreach(Playlist item in library.playlists)
                    if(item==null || string.IsNullOrEmpty(item.id) || item.songs==null || item.songs.Count>10000 || item.songs.Any(s=>s==null || string.IsNullOrEmpty(s.id) || Rules.LocalPath(s.path,null)==null || !Rules.IsAudio(s.path)))throw new InvalidOperationException("本地歌单索引含无效条目，请从原歌单重新导入。");
                if(library.playlists.Count>0)LastRoot=library.playlists[library.playlists.Count-1].root ?? "";
            }
            LastImportDetails="";return 0;
        }
        static string Hash(string value){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-","").ToLowerInvariant();}
        static void Save(Library next)
        {
            string path=Path.Combine(directory,"library.json"),pending=Path.Combine(directory,"library.new");
            string data=JsonConvert.SerializeObject(next,Formatting.Indented);if(Encoding.UTF8.GetByteCount(data)>16*1024*1024)throw new InvalidOperationException("歌单索引达到容量上限。");
            File.WriteAllText(pending,data,new UTF8Encoding(false));
            if(File.Exists(path))File.Replace(pending,path,Path.Combine(directory,"library.bak"));else File.Move(pending,path);
            library=next;
        }
        public static List<Playlist> Lists(){return library.playlists.Select(p=>new Playlist{id=p.id,name=p.name,songCount=p.songs.Count,source=p.source,root=p.root}).ToList();}
        public static List<Song> Playlist(string id)
        {
            Playlist item=library.playlists.FirstOrDefault(p=>p.id==id);if(item==null)throw new InvalidOperationException("歌单不存在，请刷新或重新导入。");return item.songs.ToList();
        }
        static List<string> Scan(string root)
        {
            var files=new List<string>();var dirs=new Stack<string>();dirs.Push(root);int visited=0;
            while(dirs.Count>0)
            {
                string dir=dirs.Pop();if(++visited>10000)throw new InvalidOperationException("目录过多，请选择更具体的音乐文件夹。");
                try
                {
                    foreach(string file in Directory.EnumerateFiles(dir))if(Rules.IsAudio(file)){files.Add(file);if(files.Count>100000)throw new InvalidOperationException("文件超过 100000 个，请选择更小的音乐目录。");}
                    foreach(string sub in Directory.EnumerateDirectories(dir))if((File.GetAttributes(sub)&FileAttributes.ReparsePoint)==0)dirs.Push(sub);
                }
                catch(UnauthorizedAccessException){throw new InvalidOperationException("音乐目录中有无权读取的子目录，请选择可访问的目录。");}
            }
            return files.OrderBy(p=>p,StringComparer.OrdinalIgnoreCase).ToList();
        }
        sealed class Entry {public string path,title,artist;public double seconds;}
        static string[] Lines(string file)
        {
            if(new FileInfo(file).Length>8*1024*1024)throw new InvalidOperationException("歌单文件超过 8 MB。");
            try{using(var reader=new StreamReader(file,new UTF8Encoding(false,true),true))return reader.ReadToEnd().Split(new[]{"\r\n","\n","\r"},StringSplitOptions.None);}
            catch(DecoderFallbackException){return File.ReadAllText(file,Encoding.GetEncoding(54936)).Split(new[]{"\r\n","\n","\r"},StringSplitOptions.None);}
        }
        static List<Entry> Read(string source)
        {
            var entries=new List<Entry>();bool m3u=Path.GetExtension(source).StartsWith(".m3u",StringComparison.OrdinalIgnoreCase);
            string title=null,artist=null;double seconds=0;
            foreach(string raw in Lines(source))
            {
                string line=raw.Trim().TrimStart('\ufeff');if(line.Length==0)continue;
                if(m3u && line.StartsWith("#EXT-X-",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("这是网络 HLS 播放列表，请使用本地歌曲歌单。");
                if(m3u && line.StartsWith("#EXTINF:",StringComparison.OrdinalIgnoreCase))
                {
                    int comma=line.IndexOf(',');if(comma>8){double.TryParse(line.Substring(8,comma-8),NumberStyles.Float,CultureInfo.InvariantCulture,out seconds);title=line.Substring(comma+1);int split=title.IndexOf(" - ",StringComparison.Ordinal);if(split>0){artist=title.Substring(0,split);title=title.Substring(split+3);}}continue;
                }
                if(m3u && line.StartsWith("#",StringComparison.Ordinal))continue;
                entries.Add(new Entry{path=line,title=title,artist=artist,seconds=double.IsNaN(seconds) || double.IsInfinity(seconds)?0:Math.Max(0,Math.Min(604800,seconds))});title=artist=null;seconds=0;
                if(entries.Count>10000)throw new InvalidOperationException("歌单超过 10000 首，请拆分后导入。");
            }
            return entries;
        }
        static int SuffixScore(string a,string b)
        {
            string[] x=a.Replace('\\','/').Split('/'),y=b.Replace('\\','/').Split('/');int score=0;
            while(score<x.Length && score<y.Length && x[x.Length-1-score].Equals(y[y.Length-1-score],StringComparison.OrdinalIgnoreCase))score++;return score;
        }
        public static ImportResult Import(string input,string musicRoot)
        {
            string source=Rules.LocalPath(input,null),root=string.IsNullOrWhiteSpace(musicRoot)?null:Rules.LocalPath(musicRoot,null);
            if(source==null || (!File.Exists(source) && !Directory.Exists(source)))throw new InvalidOperationException("请选择存在的本地歌单、歌曲或文件夹。");
            if(!string.IsNullOrWhiteSpace(musicRoot) && (root==null || !Directory.Exists(root)))throw new InvalidOperationException("音乐匹配目录不存在。");
            bool folder=Directory.Exists(source);string extension=Path.GetExtension(source).ToLowerInvariant();List<Entry> entries;
            if(folder)entries=Scan(source).Select(p=>new Entry{path=p}).ToList();
            else if(Rules.IsAudio(source))entries=new List<Entry>{new Entry{path=source}};
            else if(extension==".txt" || extension==".m3u" || extension==".m3u8")entries=Read(source);
            else throw new InvalidOperationException("请选择 TXT、M3U/M3U8 歌单或支持的音频文件。");
            if(entries.Count>10000)throw new InvalidOperationException("本次超过 10000 首，请选择子文件夹或拆分歌单。");
            string playlistId=Hash(source.ToLowerInvariant());
            var item=new Playlist{id=playlistId,name=folder?new DirectoryInfo(source).Name:Path.GetFileNameWithoutExtension(source),source=source,root=root ?? ""};
            var result=new ImportResult{playlist=item};Dictionary<string,List<string>> index=null;int indexNumber=0;
            foreach(Entry entry in entries)
            {
                indexNumber++;string line=entry.path.Trim().Trim('"');
                if((line.Contains("://") && !line.StartsWith("file:",StringComparison.OrdinalIgnoreCase)) || line.StartsWith("http:",StringComparison.OrdinalIgnoreCase) || line.StartsWith("https:",StringComparison.OrdinalIgnoreCase) || line.StartsWith("content:",StringComparison.OrdinalIgnoreCase) || line.StartsWith(@"\\",StringComparison.Ordinal))
                {result.unsupported++;result.details.Add("不支持网络或设备地址："+line);continue;}
                if(!Rules.IsAudio(line)){result.unsupported++;result.details.Add("不支持的格式或非路径条目："+line);continue;}
                string full=Rules.LocalPath(line,folder?source:Path.GetDirectoryName(source));
                if(full==null || !File.Exists(full))
                {
                    full=null;
                    if(root!=null)
                    {
                        if(index==null)index=Scan(root).GroupBy(p=>Path.GetFileName(p),StringComparer.OrdinalIgnoreCase).ToDictionary(g=>g.Key,g=>g.ToList(),StringComparer.OrdinalIgnoreCase);
                        string name=line.Replace('\\','/').Split('/').Last();List<string> candidates;
                        if(index.TryGetValue(name,out candidates))
                        {
                            int best=candidates.Max(p=>SuffixScore(line,p));var matches=candidates.Where(p=>SuffixScore(line,p)==best).ToList();
                            if(matches.Count==1)full=matches[0];
                            else{result.ambiguous++;result.details.Add("重名无法确定，请补全路径："+line);continue;}
                        }
                    }
                }
                if(full==null){result.missing++;result.details.Add("未找到："+line);continue;}
                string title=entry.title,artist=entry.artist;
                if(string.IsNullOrWhiteSpace(title)){title=Path.GetFileNameWithoutExtension(full);int split=title.IndexOf(" - ",StringComparison.Ordinal);if(split>0){artist=title.Substring(0,split);title=title.Substring(split+3);}}
                var stamp=new FileInfo(full);
                item.songs.Add(new Song{id=Hash(playlistId+"|"+indexNumber+"|"+full.ToLowerInvariant()+"|"+stamp.Length+"|"+stamp.LastWriteTimeUtc.Ticks+"|"+title+"|"+artist),path=full,name=title,artists=new List<string>{string.IsNullOrWhiteSpace(artist)?"本地音乐":artist},duration=entry.seconds});
            }
            LastImportDetails=string.Join("\n",result.details.Take(200).ToArray());
            if(item.songs.Count==0)throw new InvalidOperationException("未找到可播放歌曲。缺失 "+result.missing+"，重名 "+result.ambiguous+"，不支持 "+result.unsupported+"。请指定匹配目录，确认歌单含文件路径。");
            item.songCount=item.songs.Count;
            var next=new Library{playlists=library.playlists.Where(p=>p.id!=playlistId).Concat(new[]{item}).ToList()};
            if(next.playlists.Count>1000)throw new InvalidOperationException("本地歌单超过 1000 个。");Save(next);LastRoot=root ?? "";return result;
        }
    }
}
