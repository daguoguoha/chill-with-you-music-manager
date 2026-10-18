using System;
using ChillBilibiliPlaylist;
class TrackerTests
{
    static int assertions;
    static void Equal(int actual, int expected, string name) { assertions++; if(actual!=expected)throw new Exception(name+": "+actual+" != "+expected); }
    static void Play(ListeningTracker t, int start, int end) { for(int i=start;i<=end;i++)t.Sample(i,i,true,1); }
    static void Main()
    {
        var t=new ListeningTracker(); t.Begin(1,2,300);Play(t,0,60);Equal(t.Seconds,60,"real play");
        t.Sample(61,60,false,1);t.Sample(180,60,false,1);t.Sample(181,60,true,1);t.Sample(182,61,true,1);Equal(t.Seconds,61,"pause excluded");
        t.Sample(183,200,true,1);Equal(t.Seconds,61,"forward seek excluded");t.Sample(184,201,true,1);Equal(t.Seconds,62,"resume after seek");
        t.Sample(185,20,true,1);Equal(t.Seconds,62,"backward seek excluded");t.Sample(186,21,true,1);Equal(t.Seconds,63,"replay actual time");
        t.Sample(400,235,true,1);Equal(t.Seconds,63,"sleep excluded");
        t.BreakContinuity();t.Sample(401,236,true,1);Equal(t.Seconds,63,"explicit seek resets baseline");
        t.Begin(2,3,20);Equal(t.Seconds,0,"track boundary");Play(t,0,50);Equal(t.Seconds,20,"duration bound");
        t.Clear();Play(t,0,20);Equal(t.Seconds,0,"disabled or logged out");
        t.Begin(3,0,300);t.Sample(0,0,true,1);t.Sample(1,0,true,1);t.Sample(2,0,true,1);Equal(t.Seconds,0,"buffering excluded");
        t.Begin(4,0,300);t.Sample(0,0,true,2);t.Sample(1,2,true,2);Equal(t.Seconds,1,"real time at double speed");
        t.Sample(double.NaN,4,true,1);t.Sample(2,4,true,1);Equal(t.Seconds,1,"invalid sample");
        t.Begin(4,0,300);t.Sample(0,0,true,1);t.Sample(1,1,false,1);Equal(t.Seconds,0,"pause transition conservative");
        t.Begin(0,0,300);Play(t,0,10);Equal(t.Seconds,0,"invalid id");
        t.Begin(5,0,double.NaN);Play(t,0,10);Equal(t.Seconds,0,"invalid duration");
        Console.WriteLine("PASS "+assertions+" playback timing assertions");
    }
}
