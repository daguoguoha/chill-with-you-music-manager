using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Salt=ChillSaltPlayerPlaylist;
using Bili=ChillBilibiliPlaylist;
using Xm=ChillXimalayaPlaylist;
class FourHeartTests {
    static int count;
    static void Check(bool ok,string name){if(!ok)throw new Exception(name);count++;}
    static void Reject(Action action,string name){bool threw=false;try{action();}catch(InvalidOperationException){threw=true;}Check(threw,name);}
    static void Main(string[] args) {
        string root=Path.GetFullPath(args[0]),store=Path.Combine(root,"library"),music=Path.Combine(root,"song.wav");Directory.CreateDirectory(root);File.WriteAllBytes(music,new byte[]{1,2,3});
        Salt.Bridge.Init(store);var song=new Salt.Song{id="original",path=music,name="Fixture",artists=new List<string>{"Test"},duration=120};
        Check(Salt.Bridge.Lists().Single().id==Salt.Bridge.FavoritesPlaylist,"empty favorites playlist available");
        Check(!Salt.Bridge.IsFavorite(song),"new local file unliked");
        Salt.Bridge.SetFavorite(song,true);Check(Salt.Bridge.IsFavorite(song),"local like persisted");
        string saved=File.ReadAllText(Path.Combine(store,"favorites.json"));Salt.Bridge.SetFavorite(song,true);Check(saved==File.ReadAllText(Path.Combine(store,"favorites.json")),"idempotent add preserves file");
        var same=new Salt.Song{id="different playlist ID",path=music.ToUpperInvariant(),name="same"};
        Check(Salt.Bridge.IsFavorite(same),"same path across playlists and case shares state");
        Salt.Bridge.SetFavorite(same,true);Check(Salt.Bridge.Playlist(Salt.Bridge.FavoritesPlaylist).Count==1,"same file not duplicated");
        Check(Salt.Bridge.Playlist(Salt.Bridge.FavoritesPlaylist)[0].id!="original","favorite queue gets stable own identifier");
        Salt.Bridge.Init(store);Check(Salt.Bridge.IsFavorite(song),"restart restores favorite");
        var imported=Salt.Bridge.Import(music,"");Check(Salt.Bridge.IsFavorite(imported.playlist.songs[0]),"reimport keeps favorite by path");
        string library=File.ReadAllText(Path.Combine(store,"library.json"));File.Delete(music);Salt.Bridge.SetFavorite(song,false);Check(!Salt.Bridge.IsFavorite(song),"missing file can be removed");
        Check(File.ReadAllText(Path.Combine(store,"library.json"))==library,"favorite write preserves imported playlists");
        Reject(()=>Salt.Bridge.SetFavorite(song,true),"missing file add rejected");
        Reject(()=>Salt.Bridge.SetFavorite(new Salt.Song{id="bad",path="https://example.invalid/a.mp3"},true),"remote path rejected");
        File.WriteAllText(Path.Combine(store,"favorites.json"),"{");Reject(()=>Salt.Bridge.Init(store),"corrupt favorite file rejected");
        Check(File.ReadAllText(Path.Combine(store,"favorites.json"))=="{","corrupt file preserved for recovery");
        File.WriteAllText(Path.Combine(store,"favorites.json"),"{\"version\":2,\"songs\":[]}");Reject(()=>Salt.Bridge.Init(store),"future version rejected");
        Check(Bili.Bridge.FavoriteKey(new Bili.Song{bvid="BV1uJ411r7hL",cid=1})==Bili.Bridge.FavoriteKey(new Bili.Song{bvid="BV1uJ411r7hL",cid=2}),"Bilibili parts share whole-video like");
        Check(Bili.Bridge.ParseFavoriteState(JObject.Parse("{data:{like:true}}")),"Bilibili liked response");
        Check(!Bili.Bridge.ParseFavoriteState(JObject.Parse("{data:{like:false}}")),"Bilibili unliked response");
        Reject(()=>Bili.Bridge.ParseFavoriteState(JObject.Parse("{data:{}}")),"Bilibili absent state stays unknown");
        Check(Xm.Bridge.ParseFavoriteState(JObject.Parse("{data:{trackInfo:{isLike:true}}}")),"Ximalaya liked response");
        Reject(()=>Xm.Bridge.ParseFavoriteState(JObject.Parse("{data:{trackInfo:{isLike:null}}}")),"Ximalaya absent state stays unknown");
        Check(Xm.Bridge.FavoriteKey(new Xm.Song{id="00025202"})=="25202","canonical track ID");
        int writes=0,reads=0;bool state=false;
        var result=Bili.FavoriteOperation.Run(()=>{reads++;return state;},v=>{writes++;state=v;},null);
        Check(!result.Liked && writes==0 && reads==1,"unknown-state read never writes");
        result=Bili.FavoriteOperation.Run(()=>state,v=>{writes++;state=v;},true);Check(result.Liked && writes==1,"write confirmed by readback");
        result=Bili.FavoriteOperation.Run(()=>state,v=>{writes++;state=v;},true);Check(result.Liked && writes==1,"already desired does not rewrite");
        var xmResult=Xm.FavoriteOperation.Run(()=>false,v=>{writes++;throw new IOException("offline");},true);Check(!xmResult.Liked && xmResult.Unconfirmed && writes==2,"failed write not marked liked or retried");
        var saltResult=Salt.FavoriteOperation.Run(()=>false,v=>{},true);Check(!saltResult.Liked,"platform readback wins over successful response");
        Reject(()=>{Bili.FavoriteOperation.Run(()=>{throw new InvalidOperationException();},v=>writes++,true);},"failed initial read prevents write");Check(writes==2,"no write after unknown state");
        Console.WriteLine("PASS "+count+" favorite behavior checks");
    }
}
