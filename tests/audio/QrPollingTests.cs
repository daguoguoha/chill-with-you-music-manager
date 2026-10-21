// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace UnityEngine { public static class Time { public static float realtimeSinceStartup; } }
namespace PLATFORM_NAMESPACE
{
    public sealed partial class Plugin
    {
        object qrImage;
        bool qrPolling,busy;
        float nextPoll;
        string status;
        sealed class TestLogger { public string Text="";public void LogWarning(string value){Text+=value;} }
        readonly TestLogger Logger=new TestLogger();
        void ClearQr(){qrImage=null;}
        static int checks;
        static void Check(bool value){if(!value)throw new Exception("QR assertion "+checks);checks++;}
        public static void RunChecks()
        {
            var plugin=new Plugin();object image=new object();plugin.qrImage=image;plugin.qrPolling=true;plugin.qrDeadline=180;
            UnityEngine.Time.realtimeSinceStartup=10;
            Exception timeout=TRANSIENT_ERROR;
            Check(plugin.RecoverQrPoll(timeout,true));Check(plugin.qrImage==image&&plugin.qrPolling&&plugin.nextPoll==16);
            Check(plugin.RecoverQrPoll(timeout,true));Check(plugin.nextPoll==22);
            Check(plugin.RecoverQrPoll(timeout,true));Check(plugin.nextPoll==34);
            Check(!plugin.RecoverQrPoll(timeout,true));Check(plugin.qrImage==null&&!plugin.qrPolling);
            Check(!plugin.Logger.Text.Contains("SECRET"));
            plugin.qrImage=image;plugin.qrPolling=true;plugin.qrPollFailures=0;
            Check(!plugin.RecoverQrPoll(PERMANENT_ERROR,true));Check(plugin.qrImage==null&&!plugin.qrPolling);
            plugin.qrImage=image;plugin.qrPolling=true;plugin.qrPollFailures=0;
            UnityEngine.Time.realtimeSinceStartup=180;
            Check(!plugin.RecoverQrPoll(timeout,true));Check(plugin.qrImage==null);
            plugin.qrImage=image;plugin.qrPolling=true;plugin.busy=true;plugin.ExpireQrPolling();Check(plugin.qrImage==image);
            plugin.busy=false;plugin.ExpireQrPolling();Check(plugin.qrImage==null&&!plugin.qrPolling&&plugin.status.Contains("过期"));
            plugin.qrImage=image;plugin.qrPolling=true;plugin.qrPollFailures=0;
            Check(!plugin.RecoverQrPoll(timeout,false));Check(plugin.qrImage==image);
            Check(!QrPollingPolicy.Retry(true,false,1,10,180));
            Check(!QrPollingPolicy.Retry(true,true,0,10,180));
            Console.WriteLine("PASS "+checks+" QR polling recovery checks");
        }
    }
}
class QrPollingTestMain
{
    static int Main(){try{PLATFORM_NAMESPACE.Plugin.RunChecks();return 0;}catch(Exception error){Console.WriteLine("FAIL QR polling "+error.GetType().Name);return 1;}}
}
