// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using UnityEngine;
namespace ChillKuGouPlaylist
{
    public sealed partial class Plugin
    {
        bool qrPendingPoll;
        int qrPollFailures;
        float qrDeadline;
        bool RecoverQrPoll(Exception error,bool isPoll)
        {
            if(!isPoll) return false;
            bool transient=error is RequestIssue && ((RequestIssue)error).CanRetry;
            qrPollFailures++;
            if(QrPollingPolicy.Retry(transient,qrImage!=null,qrPollFailures,Time.realtimeSinceStartup,qrDeadline))
            {
                qrPolling=true;
                nextPoll=Time.realtimeSinceStartup+QrPollingPolicy.Delay(qrPollFailures);
                status="扫码状态连接暂时中断，保留当前二维码，正在重试（"+qrPollFailures+"/3）。";
                Logger.LogWarning("扫码状态临时连接失败；限次重试 "+qrPollFailures+"/3。");
                return true;
            }
            qrPolling=false;ClearQr();
            return false;
        }
        void ExpireQrPolling()
        {
            if(qrPolling && !busy && Time.realtimeSinceStartup>=qrDeadline)
            { qrPolling=false;ClearQr();status="二维码已过期，请重新生成。"; }
        }
    }
}
