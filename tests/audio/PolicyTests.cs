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
        foreach(long code in new long[]{401,403,404,408,410,429,500,502,503,504}) Check(PlaybackFailure.RetryDownload(false,code));
        foreach(long code in new long[]{200,400,405,422,600}) Check(!PlaybackFailure.RetryDownload(false,code));
        Check(PlaybackFailure.RetryDownload(true,0));
        n=0;try{SongRequestPolicy.Resolve<int>(()=>{n++;throw PlaybackFailure.FromSource("Song URL API returned code: 403 SECRET https://private/");},()=>false,()=>{});}catch(PlaybackFailure){}Check(n==1);
        n=0;bool canceled=false;try{SongRequestPolicy.Resolve<int>(()=>{n++;throw PlaybackFailure.FromSource("timeout SECRET");},()=>canceled,()=>{canceled=true;});}catch(OperationCanceledException){}Check(n==1);
        string diagnostic=PlaybackFailure.Diagnostic(123,PlaybackFailure.FromSource("timeout COOKIE=SECRET https://private/?token=SECRET"));
        Check(diagnostic.Contains("songId=123")&&diagnostic.Contains("stage=取地址")&&!diagnostic.Contains("SECRET")&&!diagnostic.Contains("https://"));
        Check(PlaybackFailure.Decode(new UnauthorizedAccessException("SECRET"),"mp3").Message.Contains("不可写"));
        Check(PlaybackFailure.Decode(new System.IO.FileNotFoundException("SECRET"),"mp3").Message.Contains("完整包"));
        Check(PlaybackFailure.Decode(new OutOfMemoryException("SECRET"),"mp3").Message.Contains("内存不足"));
        Check(!PlaybackFailure.Decode(new Exception("https://private/?token=SECRET"),"mp3").Message.Contains("SECRET"));
        foreach(string kind in new[]{"PROXY","TLS","DNS","TIMEOUT","REFUSED","RESET","IO"}) {
            var error=PlaybackFailure.FromSource("Song request failed: NETWORK_"+kind);
            Check(error.Code=="NETWORK_"+kind&&error.Retryable&&PlaybackFailure.StopQueue(error));
            Check(!error.Message.Contains("Song request failed"));
        }
        foreach(string reason in new[]{"Fallback API returned code: 520","Song URL API returned code: 401","Song URL API returned code: 429","Failed to parse JSON","Song request failed: RESPONSE"})
            Check(PlaybackFailure.StopQueue(PlaybackFailure.FromSource(reason)));
        Check(!PlaybackFailure.StopQueue(new SongUnavailableException()));
        Check(!PlaybackFailure.StopQueue(PlaybackFailure.FromSource("Fallback API returned code: 404")));
        Check(PlaybackFailure.StopQueue(new InvalidOperationException("SECRET")));
        Check(PlaybackFailure.StopQueue(new PlaybackFailure("下载","ConnectionError_HTTP_0","",true)));
        Check(PlaybackFailure.StopQueue(new PlaybackFailure("下载","ProtocolError_HTTP_403","",true)));
        Check(PlaybackFailure.StopQueue(new PlaybackFailure("下载","ProtocolError_HTTP_503","",true)));
        Check(!PlaybackFailure.StopQueue(new PlaybackFailure("下载","ProtocolError_HTTP_404","",true)));
        Check(!PlaybackFailure.StopQueue(new PlaybackFailure("下载","SIZE_LIMIT","",false)));
        Check(!PlaybackFailure.StopQueue(PlaybackFailure.Decode(new System.IO.InvalidDataException(),"mp3")));
        Check(PlaybackFailure.StopQueue(PlaybackFailure.Decode(new System.IO.FileNotFoundException(),"mp3")));
        return "PASS "+passed+" recovery checks";
    }
}
