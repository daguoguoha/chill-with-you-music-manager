// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Net;
using Newtonsoft.Json.Linq;
namespace ChillBilibiliPlaylist
{
    internal static class DeviceCookies
    {
        internal static readonly Uri Origin=new Uri("https://www.bilibili.com/");
        internal static bool Complete(CookieContainer jar){var c=jar.GetCookies(Origin);return c["buvid3"]!=null && c["buvid4"]!=null;}
        static bool Valid(string value)
        {
            if(string.IsNullOrEmpty(value) || value.Length<10 || value.Length>256)return false;
            foreach(char c in value)if(c<33 || c>126 || c==';' || c==',' || c=='"' || c=='\\')return false;
            return true;
        }
        internal static bool Apply(CookieContainer jar,JObject data)
        {
            if(data==null)return false;bool changed=false;
            foreach(string name in new[]{"buvid3","buvid4"})
            {
                if(jar.GetCookies(Origin)[name]!=null)continue;
                JToken item=data[name=="buvid3"?"b_3":"b_4"];
                string value=item!=null && item.Type==JTokenType.String?(string)item:"";
                if(!Valid(value))continue;
                jar.Add(new Cookie(name,value,"/",".bilibili.com"){Secure=true});changed=true;
            }
            return changed;
        }
        internal static void Copy(CookieContainer from,CookieContainer to)
        {foreach(string name in new[]{"buvid3","buvid4"}){Cookie item=from.GetCookies(Origin)[name];if(item!=null && Valid(item.Value) && to.GetCookies(Origin)[name]==null)to.Add(new Cookie(name,item.Value,"/",".bilibili.com"){Secure=true});}}
    }
}
