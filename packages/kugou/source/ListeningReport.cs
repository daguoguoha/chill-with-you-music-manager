// SPDX-License-Identifier: GPL-3.0-or-later
// Protocol reference: MakcRe/KuGouMusicApi module/user_grade_info.js (MIT).
// Synchronizes only genuine elapsed listening seconds, once per completed session.
using System;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ChillKuGouPlaylist
{
    public static partial class Bridge
    {
        const string GradeApp="3116",GradeVersion="10597",GradeKey="LnT6xpN3khm36zse0QzvmgTZ3waWdRSA";
        const string GradeModulus="xAotDadlEfO7HMK706+9i+qDtNawW2wT64kgxT8a92ebMroNDtuEMkDvG4Nu/tPuJAc0wUwTmf1llNFq8i9SUl0U1y4BVcbcyGONT3u5TzoLH0wp+ZGXLyoWCiXrCp5yQza+f2m70xn/qxxt2EcLAh3ENPP6uon0oqAbM7290Is=";
        public static int ReportPlayback(Song song,string playlist,long seconds,long position,long startedAt,long endedAt,int kind)
        {
            if(!IsLoggedIn)return 401;
            if(kind==0)return 202; // Local timing has started; no cloud write yet.
            if(song==null || seconds<1 || seconds>86400 || kind<1 || kind>3)return 400;
            if(RetryAfterSeconds>0)return 429;
            // Query the server's current baseline every time; never cache a stale total.
            JObject baseline=SendGrade(null,0);
            if(Text(baseline["status"])!="1" || baseline["data"]==null || baseline["data"]["d_sec"]==null)return 400;
            long total=Number(baseline["data"]["d_sec"]);
            if(total<0)return 400;
            JObject result=SendGrade(total,seconds);
            return Text(result["status"])=="1" && Number(result["error_code"])==0 && Number(result["errcode"])==0?200:400;
        }
        static JObject SendGrade(long? total,long seconds)
        {
            long uid=Number(account["userid"]),now=(long)(DateTime.UtcNow-new DateTime(1970,1,1)).TotalSeconds;
            var body=new JObject{{"mid",mid},{"uuid",mid},{"type",1},{"userid",uid},{"appid",GradeApp},{"clientver",GradeVersion},{"clienttime",now},{"key",Md5(GradeApp+GradeKey+GradeVersion+now.ToString(CultureInfo.InvariantCulture))}};
            string inner;
            if(total.HasValue)
            {
                body["d_sec"]=total.Value;body["diff_sec"]=seconds;body["y_type"]=0;body["m_type"]=0;
                inner=new JObject{{"token",Text(account["token"])},{"md5",Md5(total.Value.ToString(CultureInfo.InvariantCulture)+seconds.ToString(CultureInfo.InvariantCulture)+"00")}}.ToString(Formatting.None);
            }
            else inner=new JObject{{"clienttime",now},{"userid",uid}}.ToString(Formatting.None);
            body["p"]=GradeRsa(inner);
            Throttle();
            return ListeningPost("https://gateway.kugou.com/v2/get_grade_info?dfid=-",Encoding.UTF8.GetBytes(body.ToString(Formatting.None)),"text/plain; charset=ISO-8859-1",null,"Android15-1070-10597-201-0-get_user_grade_info-wifi","https://www.kugou.com/",null,"userinfo.user.kugou.com");
        }
        public static string GradeRsa(string text)
        {
            byte[] bytes=Encoding.UTF8.GetBytes(text);
            if(bytes.Length>128)throw new InvalidOperationException("播放记录凭据格式暂不支持。");
            byte[] padded=new byte[129];Buffer.BlockCopy(bytes,0,padded,0,bytes.Length);
            Array.Reverse(padded,0,128);
            byte[] modulus=Convert.FromBase64String(GradeModulus);Array.Reverse(modulus);Array.Resize(ref modulus,129);
            byte[] result=BigInteger.ModPow(new BigInteger(padded),new BigInteger(65537),new BigInteger(modulus)).ToByteArray();
            Array.Resize(ref result,128);Array.Reverse(result);
            return string.Concat(result.Select(b=>b.ToString("X2",CultureInfo.InvariantCulture)).ToArray());
        }
    }
}
