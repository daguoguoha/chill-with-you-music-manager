using System;
using ChillNeteasePlaylist;
public static class PolicyTests {
    static int passed;
    static void Check(bool b){if(!b)throw new Exception("Assertion "+passed);passed++;}
    public static string Run(){
        Check(SongRequestPolicy.IsUnavailable("No URL available for this song (tried both APIs)"));
        Check(!SongRequestPolicy.IsUnavailable("Fallback API returned code: 403"));Check(!SongRequestPolicy.IsUnavailable(null));
        int n=0,d=0;
        Check(SongRequestPolicy.Resolve(()=>{n++;if(n==1)throw new InvalidOperationException();return 3;},()=>false,()=>d++)==3&&n==2&&d==1);
        n=0;try{SongRequestPolicy.Resolve<int>(()=>{n++;throw new InvalidOperationException();},()=>false,()=>{});}catch(InvalidOperationException){}Check(n==2);
        n=0;try{SongRequestPolicy.Resolve<int>(()=>{n++;throw new SongUnavailableException();},()=>false,()=>{});}catch(SongUnavailableException){}Check(n==1);
        n=0;try{SongRequestPolicy.Resolve<int>(()=>++n,()=>true,()=>{});}catch(OperationCanceledException){}Check(n==0);
        Check(SongRequestPolicy.NextOffset(12,11,17)==2);Check(SongRequestPolicy.NextOffset(16,11,17)==6);Check(SongRequestPolicy.NextOffset(0,16,17)==2);Check(SongRequestPolicy.NextOffset(3,-1,17)==5);
        Check(SongRequestPolicy.CanDeliver(2,2,false,false));Check(!SongRequestPolicy.CanDeliver(2,3,false,false));Check(!SongRequestPolicy.CanDeliver(2,2,false,true));Check(!SongRequestPolicy.CanDeliver(2,2,true,false));
        return "PASS "+passed+" recovery checks";
    }
}
