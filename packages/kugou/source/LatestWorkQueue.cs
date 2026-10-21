// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Threading.Tasks;
namespace ChillKuGouPlaylist
{
    // One worker, at most one pending snapshot. No Unity objects are accessed here.
    internal sealed class LatestWorkQueue
    {
        readonly object gate=new object();
        Action pending;
        Task worker;
        bool running;
        internal bool IsBusy { get { lock(gate)return running; } }
        internal void Enqueue(Action action)
        {
            lock(gate)
            {
                pending=action;
                if(!running){running=true;worker=Task.Factory.StartNew(Drain);}
            }
        }
        void Drain()
        {
            while(true)
            {
                Action action;
                lock(gate){action=pending;pending=null;if(action==null){running=false;return;}}
                try{action();}catch(Exception){/* A later snapshot can recover after a transient disk error. */}
            }
        }
        internal bool Flush(int milliseconds)
        {
            Task current;lock(gate)current=worker;
            return current==null || current.Wait(milliseconds);
        }
    }
}
