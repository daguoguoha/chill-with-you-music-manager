// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Runtime.CompilerServices;
using PLATFORM_NAMESPACE;
public static class EnvironmentTests
{
    static int checks;
    static void Check(bool value,string name) { if(!value)throw new Exception(name); checks++; }
    static byte[] Silence()
    {
        byte[] data=new byte[417*40];
        for(int i=0;i<40;i++){int n=i*417;data[n]=255;data[n+1]=251;data[n+2]=144;}
        return data;
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    static void Decode(string source,Func<bool> canceled)
    { using(var pcm=DECODE_EXPRESSION){} }
    sealed class DiskFull : IOException { public DiskFull(){HResult=unchecked((int)0x80070070);} }
    public static int Main(string[] args)
    {
        try
        {
            string source=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"source.mp3");
            if(args.Length>0 && args[0]=="missing-decoder")
            {
                bool missing=false;
                try { Decode(source,delegate{return false;}); }
                catch(FileNotFoundException error){missing=PlaybackFailure.Decode(error,"mp3").Message.Contains("完整包");}
                Check(missing,"missing NLayer produces repair instruction");
                Console.WriteLine("PASS missing managed decoder detected safely"); return 0;
            }
            File.WriteAllBytes(source,Silence());
            Decode(source,delegate{return false;});
            Check(File.Exists(source),"source survives valid decode");
            string oldTmp=Environment.GetEnvironmentVariable("TMP"),oldTemp=Environment.GetEnvironmentVariable("TEMP");
            try
            {
                string absent=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"missing","temp");
                Environment.SetEnvironmentVariable("TMP",absent);Environment.SetEnvironmentVariable("TEMP",absent);
                bool failed=false;
                try{Decode(source,delegate{return false;});}
                catch(DirectoryNotFoundException error){failed=PlaybackFailure.Decode(error,"mp3").Message.Contains("临时目录");}
                Check(failed,"missing temp directory returns actionable failure");
            }
            finally{Environment.SetEnvironmentVariable("TMP",oldTmp);Environment.SetEnvironmentVariable("TEMP",oldTemp);}
            Decode(source,delegate{return false;});
            Check(File.Exists(source),"source survives failed decode and retry");
            foreach(Exception error in new Exception[]{new UnauthorizedAccessException("SECRET"),new DiskFull(),new OutOfMemoryException("SECRET"),new FileNotFoundException("SECRET"),new Exception("https://private/?cookie=SECRET")})
            {
                var failure=PlaybackFailure.Decode(error,"mp3");
                Check(!failure.Message.Contains("SECRET")&&!failure.Retryable,"safe non-retryable decode failure");
                string log=PlaybackFailure.Diagnostic(DIAGNOSTIC_ID,failure);
                Check(!log.Contains("SECRET")&&!log.Contains("https://"),"safe diagnostic");
            }
            foreach(long status in new long[]{0,403,404,410,429,500,503})
            {
                int attempts=0;
                while(true){attempts++;if(attempts==3||!PlaybackFailure.RetryDownload(status==0,status))break;}
                Check(attempts==3,"transient or expired download retry is bounded");
            }
            Check(!PlaybackFailure.RetryDownload(false,422),"permanent rejection stops retry");
            for(int i=0;i<40;i++)
            {
                bool canceled=false;try{Decode(source,delegate{return true;});}catch(OperationCanceledException){canceled=true;}
                Check(canceled,"rapid canceled selections stop");
            }
            File.WriteAllBytes(source,new byte[]{1,2,3,4});bool invalid=false;
            try{Decode(source,delegate{return false;});}catch(Exception){invalid=true;}
            Check(invalid&&File.Exists(source),"corrupt audio fails without deleting source");
            Console.WriteLine("PASS "+checks+" environment and fault-injection checks");return 0;
        }
        catch(Exception error){Console.WriteLine("FAIL environment: "+error.GetType().Name+" "+error.Message);return 1;}
    }
}
