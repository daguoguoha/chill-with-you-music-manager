// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using PLATFORM_NAMESPACE;
class PlaylistFocusTests
{
    sealed class Row { internal string Id,Title; }
    static int checks;
    static void Check(bool ok){if(!ok)throw new Exception("Assertion "+checks);checks++;}
    static int Main()
    {
        try
        {
            var rows=new List<Row>{new Row{Id="p:1",Title="same title"},null,new Row{Id="p:2",Title="same title"}};
            Func<Row,string> key=row=>row==null?null:row.Id;
            Check(PlaylistFocusPolicy.Find(rows,key,"p:2")==2);
            Check(PlaylistFocusPolicy.Find(rows,key,"p:1")==0);
            Check(PlaylistFocusPolicy.Find(rows,key,"P:1")==-1);
            Check(PlaylistFocusPolicy.Find(rows,key,"other-playlist")==-1);
            Check(PlaylistFocusPolicy.Find(rows,key,null)==-1);
            Check(PlaylistFocusPolicy.Find(new List<Row>(),key,"p:2")==-1);
            Check(PlaylistFocusPolicy.CanFocus(2,2,"p:2","p:2",false,true));
            Check(!PlaylistFocusPolicy.CanFocus(1,2,"p:2","p:2",false,true));
            Check(!PlaylistFocusPolicy.CanFocus(2,2,"p:1","p:2",false,true));
            Check(!PlaylistFocusPolicy.CanFocus(2,2,"p:2","p:2",true,true));
            Check(!PlaylistFocusPolicy.CanFocus(2,2,"p:2","p:2",false,false));
            Check(!PlaylistFocusPolicy.CanFocus(2,2,null,null,false,true));
            Check(!PlaylistFocusPolicy.CanFocus(2,2,"p:2",null,false,true));
            Console.WriteLine("PASS "+checks+" playlist focus checks");return 0;
        }
        catch(Exception error){Console.WriteLine("FAIL focus "+error.GetType().Name);return 1;}
    }
}
