// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Net;
using System.Text;
using Newtonsoft.Json.Linq;

namespace ChillXimalayaPlaylist
{
    public static partial class Bridge
    {
        static JObject ListeningPost(string url, byte[] bytes, string contentType, CookieContainer jar, string agent, string referer, string xmSign)
        {
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "POST"; req.Timeout = 8000; req.ReadWriteTimeout = 8000; req.AllowAutoRedirect = false;
            req.UserAgent = agent; req.Referer = referer; req.CookieContainer = jar;
            req.ContentType = contentType; req.ContentLength = bytes.Length;
            req.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            if (!string.IsNullOrEmpty(xmSign)) req.Headers["xm-sign"] = xmSign;
            try
            {
                using (Stream output = req.GetRequestStream()) output.Write(bytes,0,bytes.Length);
                using (var response = (HttpWebResponse)req.GetResponse())
                using (Stream input = response.GetResponseStream())
                using (var buffer = new MemoryStream())
                {
                    if ((int)response.StatusCode != 200) throw new InvalidOperationException("播放记录 HTTP 请求未成功。");
                    byte[] block=new byte[4096]; int count;
                    while ((count=input.Read(block,0,block.Length))>0)
                    { buffer.Write(block,0,count); if(buffer.Length>65536)throw new InvalidOperationException("播放记录响应超限。"); }
                    return JObject.Parse(Encoding.UTF8.GetString(buffer.ToArray()));
                }
            }
            catch(WebException ex)
            {
                if(ex.Response!=null)ex.Response.Close();
                throw new InvalidOperationException("播放记录网络请求未完成。");
            }
        }
    }
}
