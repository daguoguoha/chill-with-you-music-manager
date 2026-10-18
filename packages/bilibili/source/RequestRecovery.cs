// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using UnityEngine;
namespace ChillBilibiliPlaylist
{
    public sealed partial class Plugin
    {
        RequestIssue requestIssue;
        void HandleRequestFailure(Exception error)
        {
            qrPolling=false;requestIssue=error as RequestIssue;
            if(requestIssue!=null)
            {
                logged=!requestIssue.LoginExpired && Bridge.IsLoggedIn;
                if(logged)ClearQr();
                status=requestIssue.Message+" ["+requestIssue.DiagnosticCode+"]";
                Logger.LogWarning("请求状态："+requestIssue.DiagnosticCode);
            }
            else {status="请求未完成："+error.Message;Logger.LogWarning("请求失败："+error.GetType().Name);}
        }
        void RetryAccount()
        {
            if(busy || !ready)return;
            if(Bridge.RetryAfterSeconds>0){status="请再等待 "+Bridge.RetryAfterSeconds+" 秒后刷新。";return;}
            requestIssue=null;
            if(Bridge.IsLoggedIn){logged=true;LoadAccount();}
            else Login();
        }
        void DrawRequestIssue()
        {
            GUI.Label(new Rect(298,103,640,38),requestIssue.RequiresVerification?"平台要求安全验证":requestIssue.LoginExpired?"需要重新登录":"连接未完成",headingText);
            GUI.Box(new Rect(298,160,656,390),"",cardStyle);
            var explanation=new GUIStyle(bodyText);explanation.wordWrap=true;explanation.clipping=TextClipping.Clip;
            GUI.Label(new Rect(320,181,610,112),requestIssue.Message,explanation);
            GUI.Label(new Rect(320,300,610,28),"步骤："+requestIssue.Operation,mutedText);
            GUI.Label(new Rect(320,334,610,28),"反馈代码："+requestIssue.DiagnosticCode,bodyText);
            string details=requestIssue.RequiresVerification?"扫码登录会保留；完成验证后再刷新。若仍被限制，请反馈上方代码。":"不需要发送账号、Cookie、Token 或原始游戏日志。";
            if(requestIssue.HasVerificationEvent)details="平台已返回验证事件"+(requestIssue.VerificationType>0?"，类型 "+requestIssue.VerificationType:"")+"。本版暂不支持完成此二次验证，请反馈上方代码。";
            GUI.Label(new Rect(320,374,610,68),details,explanation);
            GUI.enabled=ready && !busy && Bridge.RetryAfterSeconds==0;
            if(GUI.Button(new Rect(320,464,180,40),Bridge.RetryAfterSeconds>0?"等待 "+Bridge.RetryAfterSeconds+" 秒":requestIssue.LoginExpired?"重新扫码登录":"重新读取账号",primaryButton))RetryAccount();
            GUI.enabled=true;
            if(GUI.Button(new Rect(516,464,180,40),"打开B站官网",buttonStyle))Application.OpenURL("https://www.bilibili.com/");
            if(GUI.Button(new Rect(712,464,180,40),"返回已有列表",quietButton))requestIssue=null;
        }
    }
}
