using System;
namespace UnityEngine { class AudioSource { public float pitch=1; } }
class SpeedTests
{
    static int checks;
    static void Check(bool ok,string message) { if(!ok) throw new Exception(message);checks++;Console.WriteLine("PASS "+message); }
    static void Main()
    {
        var c=new ChillPlaybackSpeed.SpeedController();var a=new UnityEngine.AudioSource();var b=new UnityEngine.AudioSource();
        c.Apply(a,"netease:1",1.5f,true);Check(a.pitch==1.5f,"set speed on current music");
        a.pitch=1;c.Apply(a,"netease:1",1.5f,true);Check(a.pitch==1.5f,"reapply after player resets pitch");
        c.Apply(b,"bilibili:2",1.5f,true);Check(a.pitch==1 && b.pitch==1.5f,"track switch restores old source and applies same rate");
        c.Apply(b,"game:original",2,true);Check(b.pitch==1,"original music excluded even with reused source");
        c.Apply(a,"qqmusic:3",2,true);c.Apply(a,"qqmusic:3",2,false);Check(a.pitch==1,"disable restores source");
        a.pitch=.9f;c.Apply(a,"kugou:4",.5f,true);c.Release();Check(a.pitch==.9f,"restore original nondefault pitch");
        a.pitch=1;c.Apply(a,"ximalaya:5",1.25f,true);a.pitch=.8f;c.Release();Check(a.pitch==.8f,"release preserves later pitch set by another owner");
        a.pitch=1;c.Apply(a,"saltplayer:6",2,true);c.Apply(null,null,2,true);Check(a.pitch==1,"no source releases pitch");
        Check(ChillPlaybackSpeed.SpeedController.Rate(float.NaN)==1,"NaN resets to normal");
        Check(ChillPlaybackSpeed.SpeedController.Rate(float.PositiveInfinity)==1,"infinity resets to normal");
        Check(ChillPlaybackSpeed.SpeedController.Rate(4)==2 && ChillPlaybackSpeed.SpeedController.Rate(-1)==.5f,"speed limits");
        Check(ChillPlaybackSpeed.SpeedController.Rate(1.249f)==1.25f,"round to 0.05 step");
        Check(!ChillPlaybackSpeed.SpeedController.Supported(null)&&!ChillPlaybackSpeed.SpeedController.Supported("neteaseX:1"),"reject absent and unrelated identities");
        foreach(string prefix in new[]{"netease:","qqmusic:","qq:","kugou:","bilibili:","ximalaya:","saltplayer:"})Check(ChillPlaybackSpeed.SpeedController.Supported(prefix+"123"),"supported "+prefix);
        Console.WriteLine("TOTAL "+checks);
    }
}
