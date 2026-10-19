// SPDX-License-Identifier: GPL-3.0-or-later
// Protocol adapted from MakcRe/KuGouMusicApi ba3645f (MIT). See licenses/KuGouMusicApi-LICENSE.txt.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ChillKuGouPlaylist
{
    public static partial class Bridge
    {
        const string AppId = "1005", ClientVersion = "20489";
        static string directory, mid, qrKey;
        static DateTime restrictedUntil=DateTime.MinValue;
        static readonly object requestGate=new object();
        static readonly System.Diagnostics.Stopwatch requestClock=System.Diagnostics.Stopwatch.StartNew();
        static long nextRequestAt;
        public static int RetryAfterSeconds {get{return Math.Max(0,(int)Math.Ceiling((restrictedUntil-DateTime.UtcNow).TotalSeconds));}}
        static RequestIssue Fail(RequestIssue issue){if(issue.WaitSeconds>0)restrictedUntil=DateTime.UtcNow.AddSeconds(issue.WaitSeconds);return issue;}
        static void Throttle()
        {lock(requestGate){long wait=nextRequestAt-requestClock.ElapsedMilliseconds;if(wait>0)System.Threading.Thread.Sleep((int)Math.Min(wait,400));nextRequestAt=requestClock.ElapsedMilliseconds+400;}}
        static JObject account = new JObject();
        static readonly Dictionary<string, Song> knownSongs = new Dictionary<string, Song>();
        static string Text(JToken t) { return t == null || t.Type == JTokenType.Null ? "" : t.ToString(); }
        static long Number(JToken t) { long n; return long.TryParse(Text(t), out n) ? n : 0; }
        static Dictionary<string, string> Params(params string[] values)
        {
            var p = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i < values.Length; i += 2) p[values[i]] = values[i + 1];
            return p;
        }
        static string Md5(string text)
        { using (MD5 md = MD5.Create()) return BitConverter.ToString(md.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "").ToLowerInvariant(); }
        public static bool IsLoggedIn { get { return !string.IsNullOrEmpty(Text(account["token"])) && Number(account["userid"]) > 0; } }
        public static int Init(string path)
        {
            NetworkErrors.EnableModernTls();
            account=new JObject();restrictedUntil=DateTime.MinValue;
            directory = path; Directory.CreateDirectory(path);
            string device = Path.Combine(path, "device.txt");
            mid = File.Exists(device) ? File.ReadAllText(device).Trim() : "";
            if (!System.Text.RegularExpressions.Regex.IsMatch(mid, "^[a-f0-9]{32}$")) { mid = Guid.NewGuid().ToString("N"); File.WriteAllText(device, mid); }
            string file = Path.Combine(path, "session.dat");
            if (File.Exists(file))
            {
                try { account = JObject.Parse(Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(file), Encoding.UTF8.GetBytes("ChillKuGou/1"), DataProtectionScope.CurrentUser))); }
                catch { account = new JObject(); } // A copied/expired account must be signed in again.
            }
            return 0;
        }
        static JObject Request(string path, Dictionary<string, string> extra, JObject body, string router, bool web, string host, bool post=false)
        {return RequestCore(path,extra,body,router,web,host,true,post);}
        static string VerificationEvent(JObject result,HttpWebResponse response)
        {
            JObject data=result["data"] as JObject;
            string value=response.Headers["ssa-code"] ?? Text(result["ssaCode"] ?? result["eventid"] ?? (data==null?null:data["eventid"]));
            return System.Text.RegularExpressions.Regex.IsMatch(value??"","^[A-Za-z0-9_-]{8,256}$")?value:null;
        }
        static int ReadVerificationType(string eventId)
        {
            try
            {
                JObject result=RequestCore("/verifyservice/v3/get_verify_info",null,
                    new JObject{{"eventid",eventId},{"userid",Number(account["userid"])},{"platid",2},{"rtype",1},{"wasm",1},{"i",""},{"sid",""},{"edt",""}},null,false,"https://gateway.kugou.com",false);
                JObject data=result["data"] as JObject;long kind=Number(data==null?null:data["v_type"]);
                return kind>0 && kind<1000?(int)kind:0;
            }
            catch(InvalidOperationException){return 0;}catch(JsonException){return 0;}
        }
        static JObject RequestCore(string path, Dictionary<string, string> extra, JObject body, string router, bool web, string host,bool readVerification,bool post=false)
        {
            if(!web && path!="/verifyservice/v3/get_verify_info" && RetryAfterSeconds>0)throw RequestIssue.Cooling(RetryAfterSeconds);
            var p = Params("dfid", "-", "mid", mid, "uuid", "-", "appid", AppId, "clientver", ClientVersion,
                "clienttime", ((long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds).ToString(CultureInfo.InvariantCulture));
            if (IsLoggedIn) { p["token"] = Text(account["token"]); p["userid"] = Text(account["userid"]); }
            if (extra != null) foreach (var x in extra) p[x.Key] = x.Value;
            string json = body == null ? "" : body.ToString(Formatting.None);
            string salt = web ? "NVPh5oo715z5DIWAeQlhMDsWXXQV4hwt" : "OIlwieks28dk2k092lksi2UIkp";
            p["signature"] = Md5(salt + string.Concat(p.OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => x.Key + "=" + x.Value).ToArray()) + json + salt);
            string query = string.Join("&", p.Select(x => Uri.EscapeDataString(x.Key) + "=" + Uri.EscapeDataString(x.Value)).ToArray());
            for (int attempt = 0; ; attempt++)
            {
                Throttle();
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(host + path + "?" + query);
                req.Method = body == null && !post ? "GET" : "POST"; req.Timeout = 15000; req.ReadWriteTimeout = 15000; req.AllowAutoRedirect = false;
                req.UserAgent = "Android15-1070-11083-46-0-DiscoveryDRADProtocol-wifi";
                req.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                req.Headers["mid"] = mid; req.Headers["dfid"] = "-"; req.Headers["clienttime"] = p["clienttime"];
                req.Headers["kg-rc"] = "1"; req.Headers["kg-thash"] = "5d816a0"; req.Headers["kg-rec"] = "1"; req.Headers["kg-rf"] = "B9EDA08A64250DEFFBCADDEE00F8F25F";
                if (!string.IsNullOrEmpty(router)) req.Headers["x-router"] = router;
                try
                {
                    if (body != null) { byte[] data = Encoding.UTF8.GetBytes(json); req.ContentType = "application/json;charset=utf-8"; req.ContentLength = data.Length; using (Stream output = req.GetRequestStream()) output.Write(data, 0, data.Length); }
                    using (HttpWebResponse response = (HttpWebResponse)req.GetResponse())
                    using (Stream input = response.GetResponseStream())
                    using (var buffer = new MemoryStream())
                    {
                        if (response.StatusCode != HttpStatusCode.OK) throw new InvalidOperationException("酷狗接口 HTTP " + (int)response.StatusCode + "（" + req.RequestUri.Host + "）。");
                        byte[] bytes = new byte[8192]; int count;
                        while ((count = input.Read(bytes, 0, bytes.Length)) > 0) { buffer.Write(bytes, 0, count); if (buffer.Length > 16 * 1024 * 1024) throw new InvalidOperationException("酷狗响应超过大小限制。"); }
                        JObject result = JObject.Parse(Encoding.UTF8.GetString(buffer.ToArray()));
                        if (Number(result["error_code"]) != 0 || Text(result["status"]) == "0")
                        {
                            long code=Number(result["error_code"]);string eventId=code==20028?VerificationEvent(result,response):null;
                            int kind=eventId!=null && readVerification?ReadVerificationType(eventId):0;
                            throw Fail(RequestIssue.Api(code,path,eventId!=null,kind));
                        }
                        return result;
                    }
                }
                catch (WebException ex)
                {
                    HttpWebResponse failedResponse=ex.Response as HttpWebResponse;
                    if(failedResponse!=null && ((int)failedResponse.StatusCode==412 || (int)failedResponse.StatusCode==429))
                    {int status=(int)failedResponse.StatusCode;failedResponse.Close();throw Fail(RequestIssue.Http(status,path));}
                    bool retry = body == null && attempt == 0 && NetworkErrors.CanRetry(ex);
                    string message = NetworkErrors.Describe(ex, req.RequestUri.Host);
                    if (ex.Response != null) ex.Response.Close();
                    if (retry) continue;
                    // Do not retain the original exception: it can contain a signed URL/token.
                    throw RequestIssue.Network(message,path);
                }
            }
        }
        static JObject Api(string path, Dictionary<string, string> p, JObject data, string router,bool post=false)
        { return Request(path, p, data, router, false, "https://gateway.kugou.com",post); }
        public static void CancelQr() { qrKey = null; }
        public static string QrImage(string ignored)
        {
            JObject r = Request("/v2/qrcode", Params("appid", "1001", "type", "1", "plat", "4", "qrcode_txt", "https://h5.kugou.com/apps/loginQRCode/html/index.html?appid=1005&", "srcappid", "2919"), null, null, true, "https://login-user.kugou.com");
            qrKey = Text(r["data"]["qrcode"]);
            string image = Text(r["data"]["qrcode_img"]);
            if (qrKey.Length < 10 || !image.StartsWith("data:image/png;base64,", StringComparison.Ordinal)) throw new InvalidOperationException("酷狗未返回有效二维码。");
            return image.Substring("data:image/png;base64,".Length);
        }
        public static string CheckQr()
        {
            if (string.IsNullOrEmpty(qrKey)) return JsonConvert.SerializeObject(new QrState { code = 65 });
            JObject r = Request("/v2/get_userinfo_qrcode", Params("plat", "4", "appid", AppId, "srcappid", "2919", "qrcode", qrKey), null, null, true, "https://login-user.kugou.com");
            JObject data = r["data"] as JObject;
            int state = (int)Number(data == null ? null : data["status"]);
            if (state == 4)
            {
                if (Number(data["userid"]) <= 0 || string.IsNullOrEmpty(Text(data["token"]))) throw new InvalidOperationException("扫码授权未返回账号，请重试。");
                JObject next = new JObject { { "userid", data["userid"] }, { "token", data["token"] }, { "nickname", data["nickname"] ?? data["username"] ?? "酷狗用户" } };
                byte[] protectedBytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(next.ToString(Formatting.None)), Encoding.UTF8.GetBytes("ChillKuGou/1"), DataProtectionScope.CurrentUser);
                string pending = Path.Combine(directory, "session.new"); File.WriteAllBytes(pending, protectedBytes);
                string target = Path.Combine(directory, "session.dat");
                if (File.Exists(target)) File.Replace(pending, target, null); else File.Move(pending, target);
                account = next; qrKey = null;
            }
            return JsonConvert.SerializeObject(new QrState { code = state == 4 ? 0 : state == 1 ? 66 : state == 2 ? 67 : 65 });
        }
        public static string UserInfo() { return JsonConvert.SerializeObject(new User { nickname = Text(account["nickname"]) }); }
        public static string UserPlaylists()
        {
            if (!IsLoggedIn) throw new InvalidOperationException("请先用酷狗音乐 App 扫码登录。");
            var result = new List<Playlist>(); var seen = new HashSet<string>(); int received = 0;
            for (int page = 1; page <= 100; page++)
            {
                JObject r = Api("/v7/get_all_list", Params("plat", "1", "userid", Text(account["userid"]), "token", Text(account["token"])),
                    new JObject { { "userid", account["userid"] }, { "token", account["token"] }, { "total_ver", 979 }, { "type", 2 }, { "page", page }, { "pagesize", 100 } }, "cloudlist.service.kugou.com");
                JToken data = r["data"]; JArray rows = data as JArray ?? (data == null ? null : (data["info"] as JArray ?? data["lists"] as JArray ?? data["list"] as JArray));
                if (rows == null) throw new InvalidOperationException("酷狗歌单结构已变化，请反馈版本和错误信息。");
                received += rows.Count;
                foreach (JObject row in rows.OfType<JObject>())
                {
                    string id = Text(row["listid"]); string global = Text(row["global_collection_id"]);
                    if (id.Length > 0) id = "mine:" + id + ":" + (Text(row["type"]).Length == 0 ? "0" : Text(row["type"]));
                    else id = Rules.PlaylistId(global);
                    if (!string.IsNullOrEmpty(id) && seen.Add(id)) result.Add(new Playlist { id = id, name = Text(row["name"]), songCount = (int)Number(row["count"] ?? row["song_count"]) });
                }
                long total = data is JObject ? Number(data["total"] ?? data["count"]) : 0;
                if ((total > 0 && received >= total) || rows.Count < 100) return JsonConvert.SerializeObject(new UserPlaylists { created = result });
                if (page == 100) throw new InvalidOperationException("歌单数量超过上限，请缩小收藏范围。");
            }
            throw new InvalidOperationException("歌单分页未完成。");
        }
        public static List<Song> Playlist(string id)
        {
            bool mine = id != null && System.Text.RegularExpressions.Regex.IsMatch(id, @"^mine:\d+:\d+$");
            if (!mine && Rules.PlaylistId(id) != id) throw new InvalidOperationException("歌单 ID 无效。");
            var result = new List<Song>(); var seen = new HashSet<string>(); int received = 0; long total = -1;
            for (int page = 1; page <= 100; page++)
            {
                JObject r;
                if (mine)
                {
                    if (!IsLoggedIn) throw new InvalidOperationException("读取个人歌单需要扫码登录。");
                    string[] bits = id.Split(':');
                    r = Api("/v4/get_list_all_file_v3", null, new JObject { { "listid", bits[1] }, { "userid", account["userid"] }, { "token", account["token"] }, { "type", int.Parse(bits[2]) }, { "page", page }, { "pagesize", 100 }, { "area_code", 1 }, { "allplatform", 1 }, { "show_cover", 1 } }, "cloudlist.service.kugou.com");
                }
                else r = Api("/pubsongs/v2/get_other_list_file_nofilt", Params("area_code", "1", "begin_idx", ((page - 1) * 100).ToString(), "plat", "1", "type", "1", "mode", "1", "personal_switch", "1", "pagesize", "100", "global_collection_id", id), null, null);
                JToken data = r["data"];
                JArray rows = data == null ? null : data["songs"] as JArray ?? data["info"] as JArray ?? data["files"] as JArray ?? data["list"] as JArray;
                if (rows == null && data is JObject && data["count"] != null && data["count"].Type == JTokenType.Integer && Number(data["count"]) == 0) rows = new JArray();
                if (rows == null) throw new InvalidOperationException("酷狗没有返回完整歌曲列表。");
                long reported = Number(data["count"] ?? data["total"] ?? data["total_count"]);
                if (page == 1) total = reported;
                else if (reported > 0 && reported != total) throw new InvalidOperationException("歌单在读取期间发生变化，请重新读取。");
                received += rows.Count;
                foreach (JObject row in rows.OfType<JObject>())
                {
                    string hash = Text(row["hash"]); if (hash.Length == 0) hash = Text(row["filehash"]);
                    if (!System.Text.RegularExpressions.Regex.IsMatch(hash, "^[a-fA-F0-9]{32}$")) continue;
                    string album = Text(row["album_id"]); long albumAudio = Number(row["mixsongid"] ?? row["album_audio_id"] ?? row["add_mixsongid"]);
                    string key = hash.ToLowerInvariant() + ":" + (album.Length == 0 ? "0" : album) + ":" + albumAudio;
                    var artists = new List<string>(); JArray singers = row["singerinfo"] as JArray ?? row["singerinfos"] as JArray;
                    if (singers != null) foreach (var singer in singers) { string name = Text(singer["name"]); if (name.Length > 0) artists.Add(name); }
                    string filename = Text(row["name"] ?? row["filename"]); string title = Text(row["songname"]);
                    int dash = filename.IndexOf(" - ", StringComparison.Ordinal);
                    if (title.Length == 0) title = dash >= 0 ? filename.Substring(dash + 3) : filename;
                    if (artists.Count == 0 && dash > 0) artists.Add(filename.Substring(0, dash));
                    double seconds = Number(row["timelen"]) / 1000.0; if (seconds <= 0) seconds = Number(row["duration"]);
                    Song song = new Song { id = key, hash = hash.ToLowerInvariant(), albumId = album.Length == 0 ? "0" : album, albumAudioId = albumAudio, name = title, artists = artists, duration = seconds };
                    song.fileId=Number(row["fileid"]);
                    if (seen.Add(key)) result.Add(song);
                }
                if ((total > 0 && received >= total) || rows.Count == 0 || (total <= 0 && rows.Count < 100))
                {
                    if (total > 0 && received < total) throw new InvalidOperationException("酷狗只返回部分歌曲，请重新读取。");
                    foreach (Song s in result) knownSongs[s.id] = s;
                    return result;
                }
            }
            throw new InvalidOperationException("歌单超过 10000 首，暂不支持。");
        }
        public static string SongURL(string id)
        {
            Song song; if (!knownSongs.TryGetValue(id, out song)) throw new InvalidOperationException("请重新载入歌单。");
            string uid = IsLoggedIn ? Text(account["userid"]) : "0";
            var p = Params("album_id", song.albumId, "area_code", "1", "hash", song.hash, "ssa_flag", "is_fromtrack", "version", "11430", "page_id", "151369488", "quality", "128", "album_audio_id", song.albumAudioId.ToString(), "behavior", "play", "pid", "2", "cmd", "26", "pidversion", "3001", "IsFreePart", "0", "ppage_id", "463467626,350369493,788954147", "cdnBackup", "1", "module", "", "clientver", "11430", "key", Md5(song.hash + "57ae12eb6890223e355ccfcb74edf70d" + AppId + mid + uid));
            JObject r = Api("/v5/url", p, null, "trackercdn.kugou.com");
            JArray urls = r["url"] as JArray;
            string url = urls == null ? Text(r["url"]) : urls.Select(Text).FirstOrDefault(Rules.IsMusicUrl);
            if (!Rules.IsMusicUrl(url)) throw new InvalidOperationException("此歌曲暂无可用音源，请确认账号权限。");
            double length = Number(r["timeLength"]);
            if (song.duration > 60 && length > 0 && length + 8 < song.duration) throw new InvalidOperationException("此歌曲仅返回试听片段，请确认账号权限。");
            return JsonConvert.SerializeObject(new SongUrl { url = url, type = Text(r["extName"]), size = Number(r["fileSize"]) });
        }
    }
}
