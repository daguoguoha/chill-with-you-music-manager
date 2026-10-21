// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Threading;
using PLATFORM_NAMESPACE;
public static class LatestWorkTests
{
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static void Run()
    {
        var queue=new LatestWorkQueue();var entered=new ManualResetEventSlim();var release=new ManualResetEventSlim();
        int written=-1,count=0,thread=Thread.CurrentThread.ManagedThreadId,workerThread=thread;
        queue.Enqueue(delegate{workerThread=Thread.CurrentThread.ManagedThreadId;entered.Set();release.Wait(3000);Interlocked.Increment(ref count);});
        Check(entered.Wait(3000),"worker started");
        try {
            for(int i=0;i<10000;i++){int value=i;queue.Enqueue(delegate{written=value;Interlocked.Increment(ref count);});}
            Check(queue.IsBusy && !queue.Flush(1),"disk stall stays off caller and flush is bounded");
            Check(count==0,"pending writes wait for running write");
        }finally{release.Set();}
        Check(queue.Flush(3000),"shutdown drains newest snapshot");
        Check(written==9999 && count==2,"rapid controls coalesce instead of queuing 10000 file writes");
        Check(workerThread!=thread,"serialization and disk work run off main caller");
        queue.Enqueue(delegate{throw new System.IO.IOException();});queue.Flush(3000);
        queue.Enqueue(delegate{written=10000;});Check(queue.Flush(3000)&&written==10000,"failure does not wedge future saves");
        Console.WriteLine("PASS background save isolation, coalescing, ordering, bounded exit and failure recovery");
        entered.Dispose();release.Dispose();
    }
    public static int Main(){try{Run();return 0;}catch(Exception e){Console.WriteLine("FAIL "+e.GetType().Name+" "+e.Message);return 1;}}
}
