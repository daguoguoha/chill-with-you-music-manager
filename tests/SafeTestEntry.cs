// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Reflection;
class SafeTestEntry
{
    static int Main(string[] args)
    {
        try
        {
            Type type=Assembly.GetExecutingAssembly().GetType("TEST_MAIN_TYPE",true);
            MethodInfo main=type.GetMethod("Main",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic);
            object result=main.Invoke(null,main.GetParameters().Length==0?null:new object[]{args});
            return result is int?(int)result:0;
        }
        catch(Exception error)
        {
            for(Exception current=error;current!=null;current=current.InnerException)
                Console.WriteLine("FAIL test: "+current.GetType().Name+" HRESULT="+current.HResult.ToString("X8"));
            return 1;
        }
    }
}
