using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
public static class RecommendationTests {
    sealed class Item { public string Id; }
    static int checks;
    static void Check(bool condition){if(!condition)throw new Exception("Recommendation assertion "+checks);checks++;}
    static void Throws(Action action){bool failed=false;try{action();}catch(InvalidOperationException){failed=true;}Check(failed);}
    public static string Run(){
        Func<IEnumerable<Item>,Func<Item,string>,List<Item>>[] prepare={ChillNeteasePlaylist.RecommendationPolicy.Prepare<Item>,ChillKuGouPlaylist.RecommendationPolicy.Prepare<Item>};
        foreach(var policy in prepare){
            var input=new[]{new Item{Id="1"},null,new Item{Id="1"},new Item{Id=""},new Item{Id="0"},new Item{Id="2"}};
            var output=policy(input,i=>i.Id);Check(output.Count==2&&output[0].Id=="1"&&output[1].Id=="2");
            Throws(()=>policy(null,i=>i.Id));Throws(()=>policy(new Item[0],i=>i.Id));
            Throws(()=>policy(new[]{new Item{Id="0"}},i=>i.Id));
            var tooMany=new List<Item>();for(int i=0;i<2001;i++)tooMany.Add(new Item{Id=i.ToString()});Throws(()=>policy(tooMany,i=>i.Id));
        }
        string hash="0123456789abcdef0123456789abcdef";
        foreach(string key in new[]{"song_list","songs","list"}){
            var response=JObject.Parse("{\"data\":{\""+key+"\":[{\"hash\":\""+hash+"\",\"filename\":\"Artist - Title\",\"timelen\":180000,\"mixsongid\":42},{\"hash\":\""+hash+"\",\"mixsongid\":42},{\"hash\":\"bad\"}]}}");
            var songs=ChillKuGouPlaylist.Bridge.ParseRecommendations(response);
            Check(songs.Count==1&&songs[0].name=="Title"&&songs[0].artists[0]=="Artist"&&songs[0].duration==180&&songs[0].albumAudioId==42);
        }
        Throws(()=>ChillKuGouPlaylist.Bridge.ParseRecommendations(JObject.Parse("{\"data\":{}}")));
        Throws(()=>ChillKuGouPlaylist.Bridge.ParseRecommendations(JObject.Parse("{\"data\":{\"songs\":[]}}")));
        var saved=new ChillKuGouPlaylist.Song{hash=hash,albumId="0",albumAudioId=42,id=hash+":0:42"};
        var actualShape=JObject.Parse("{\"data\":{\"song_list\":[{\"hash\":\""+hash+"\",\"songname\":\"Title\",\"author_name\":\"Artist\",\"time_length\":245,\"album_audio_id\":42}]}}");
        Check(ChillKuGouPlaylist.Bridge.ParseRecommendations(actualShape)[0].duration==245);
        ChillKuGouPlaylist.Bridge.RememberSong(saved);Check(true);
        Throws(()=>ChillKuGouPlaylist.Bridge.RememberSong(null));
        saved.id="other";Throws(()=>ChillKuGouPlaylist.Bridge.RememberSong(saved));
        saved.id=hash+":0:42";saved.hash="invalid";Throws(()=>ChillKuGouPlaylist.Bridge.RememberSong(saved));
        return "PASS "+checks+" recommendation checks";
    }
}
