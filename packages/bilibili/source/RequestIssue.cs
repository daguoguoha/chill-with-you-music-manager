// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace ChillBilibiliPlaylist
{
    public sealed class RequestIssue : InvalidOperationException
    {
        public readonly bool RequiresVerification, LoginExpired;
        public readonly string DiagnosticCode, Operation;
        public readonly int WaitSeconds, VerificationType;
        public readonly bool HasVerificationEvent;
        RequestIssue(string message,string code,string operation,bool verify,bool expired,int wait,bool hasEvent,int verificationType)
            :base(message){DiagnosticCode=code;Operation=operation;RequiresVerification=verify;LoginExpired=expired;WaitSeconds=wait;HasVerificationEvent=hasEvent;VerificationType=verificationType;}
        static string OperationCode(string path)
        {
            if(path=="/x/web-interface/nav")return "ACCOUNT";
            if(path=="/x/frontend/finger/spi")return "DEVICE";
            if(path=="/x/v3/fav/folder/created/list-all")return "FOLDERS";
            if(path=="/x/v3/fav/resource/list")return "FAVORITES";
            if(path=="/x/web-interface/view")return "VIDEO";
            if(path=="/x/player/playurl" || path=="/x/player/wbi/playurl")return "AUDIO";
            if(path=="/v7/get_all_list")return "PLAYLISTS";
            if(path=="/verifyservice/v3/get_verify_info")return "VERIFY_INFO";
            if(path=="/v2/get_userinfo_qrcode" || path=="/x/passport-login/web/qrcode/poll")return "QR_POLL";
            if(path=="/v2/qrcode" || path=="/x/passport-login/web/qrcode/generate")return "QR_CREATE";
            return "OTHER";
        }
        static string Title(string operation)
        {
            switch(operation){case "ACCOUNT":return "读取账号";case "DEVICE":return "初始化设备信息";case "FOLDERS":return "读取收藏夹";case "FAVORITES":return "读取收藏夹内容";case "VIDEO":return "读取视频信息";case "AUDIO":return "读取音频地址";case "PLAYLISTS":return "读取歌单";case "VERIFY_INFO":return "读取验证类型";case "QR_POLL":return "确认扫码结果";case "QR_CREATE":return "获取二维码";default:return "平台请求";}
        }
        public static RequestIssue Http(int status,string path)
        {
            string op=OperationCode(path);bool restricted=status==412 || status==429;
            string message=restricted?"B站暂时限制了当前请求。请在官方客户端正常登录并完成要求的验证，稍后再刷新；无需反复扫码。":"B站接口暂不可用，请稍后重试。";
            return new RequestIssue(message,"BILI-HTTP"+status+"-"+op,Title(op),restricted,false,restricted?60:0,false,0);
        }
        public static RequestIssue Api(long code,string path,bool hasEvent,int verificationType)
        {
            string op=OperationCode(path);bool restricted=code==-412 || code==-352;bool expired=code==-101;
            int type=verificationType>0 && verificationType<1000?verificationType:0;
            string message=expired?"B站登录已失效，请重新扫码；原登录文件会保留。":restricted?"B站暂时限制了当前请求。请在官方客户端正常登录并完成要求的验证，稍后再刷新；无需反复扫码。":"B站未接受当前请求，请稍后重试或检查内容权限。";
            return new RequestIssue(message,"BILI-API"+code+"-"+op,Title(op),restricted,expired,restricted?60:0,hasEvent,type);
        }
        public static RequestIssue Cooling(int seconds)
        {return new RequestIssue("当前请求仍在冷却，请等待后再刷新。","BILI-COOLDOWN","等待平台限制解除",true,false,Math.Max(1,Math.Min(seconds,60)),false,0);}
        public static RequestIssue Network(string safeDescription,string path)
        {string op=OperationCode(path);return new RequestIssue(safeDescription,"BILI-NETWORK-"+op,Title(op),false,false,0,false,0);}
    }
}
