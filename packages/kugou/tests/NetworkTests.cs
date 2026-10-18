using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using Newtonsoft.Json.Linq;
using ChillKuGouPlaylist;

class NetworkTests
{
    static void Check(bool pass, string name) { if (!pass) throw new Exception(name); Console.WriteLine("PASS " + name); }
    static object Call(string host, bool post)
    {
        try { return typeof(Bridge).GetMethod("Request", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null,
            new object[] { "/probe", new Dictionary<string,string> { { "token", "PRIVATE_QUERY_SENTINEL" } }, post ? new JObject() : null, null, false, host }); }
        catch (TargetInvocationException error) { throw error.InnerException; }
    }
    static void ServerCase(int[] codes, bool post, bool success, string errorCode)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port, received = 0;
        Exception serverError = null;
        var server = new Thread(delegate() {
            try {
                foreach (int code in codes) {
                    DateTime until = DateTime.UtcNow.AddSeconds(8);
                    while (!listener.Pending() && DateTime.UtcNow < until) Thread.Sleep(10);
                    if (!listener.Pending()) throw new Exception("Expected retry missing");
                    using (TcpClient client = listener.AcceptTcpClient())
                    using (NetworkStream stream = client.GetStream()) {
                        stream.ReadTimeout = 5000;
                        string headers = ""; int next;
                        while (!headers.EndsWith("\r\n\r\n") && (next = stream.ReadByte()) >= 0) headers += (char)next;
                        int contentLength = 0;
                        foreach (string line in headers.Split(new[] { "\r\n" }, StringSplitOptions.None))
                            if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)) contentLength = int.Parse(line.Substring(15).Trim());
                        if (headers.IndexOf("100-continue", StringComparison.OrdinalIgnoreCase) >= 0) {
                            byte[] ready = Encoding.ASCII.GetBytes("HTTP/1.1 100 Continue\r\n\r\n"); stream.Write(ready, 0, ready.Length);
                        }
                        for (int i=0; i<contentLength; i++) stream.ReadByte();
                        received++;
                        string body = code == 200 ? "{\"status\":1,\"error_code\":0}" : "PRIVATE_RESPONSE_SENTINEL";
                        byte[] reply = Encoding.ASCII.GetBytes("HTTP/1.1 " + code + " Test\r\nContent-Length: " + body.Length + "\r\nContent-Type: application/json\r\nConnection: close\r\n\r\n" + body);
                        stream.Write(reply, 0, reply.Length);
                    }
                }
            } catch (Exception error) { serverError = error; }
        });
        server.IsBackground = true; server.Start();
        Exception failure = null;
        try { Call("http://127.0.0.1:" + port, post); } catch(Exception error) { failure = error; }
        bool joined = server.Join(10000); listener.Stop();
        Check(joined && serverError == null && received == codes.Length, "HTTP " + codes[0] + " request count=" + received + (post ? " POST" : " GET"));
        if (success) Check(failure == null, "transient GET recovers");
        else {
            Check(failure is InvalidOperationException && failure.Message.Contains(errorCode), "safe HTTP status diagnostic " + errorCode);
            Check(!failure.ToString().Contains("PRIVATE_") && !failure.ToString().Contains("token="), "request and response secrets excluded");
        }
    }
    static int Main(string[] args)
    {
        try {
            if (args.Length > 0 && args[0] == "live") {
                var certCallback = ServicePointManager.ServerCertificateValidationCallback;
                Bridge.Init(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test-account"));
                byte[] qr = Convert.FromBase64String(Bridge.QrImage("kugou"));
                Check(qr.Length > 100 && qr[0] == 137 && qr[1] == 80, "real HTTPS QR PNG bytes=" + qr.Length);
                string state = (string)JObject.Parse(Bridge.CheckQr())["code"];
                Check(state == "66" || state == "67", "real QR polling state=" + state);
                Check(object.ReferenceEquals(certCallback, ServicePointManager.ServerCertificateValidationCallback), "certificate validation unchanged");
                Console.WriteLine("Live QR test passed; no login or account data collected."); return 0;
            }
            SecurityProtocolType original = ServicePointManager.SecurityProtocol;
            ServicePointManager.SecurityProtocol = (SecurityProtocolType)0;
            NetworkErrors.EnableModernTls(); Check((int)ServicePointManager.SecurityProtocol == 0, "OS TLS default preserved");
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls;
            NetworkErrors.EnableModernTls(); Check(ServicePointManager.SecurityProtocol == (SecurityProtocolType.Tls | SecurityProtocolType.Tls12), "TLS 1.2 added to legacy policy");
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            NetworkErrors.EnableModernTls(); Check(ServicePointManager.SecurityProtocol == SecurityProtocolType.Tls12, "existing modern TLS preserved");
            ServicePointManager.SecurityProtocol = original;
            foreach (WebExceptionStatus status in Enum.GetValues(typeof(WebExceptionStatus))) {
                var error = new WebException("PRIVATE_QUERY_SENTINEL", null, status, null);
                string message = NetworkErrors.Describe(error, "login-user.kugou.com");
                Check(message.Contains(status.ToString()) && !message.Contains("PRIVATE_"), "safe diagnostic " + status);
            }
            Check(!NetworkErrors.CanRetry(new WebException("", null, WebExceptionStatus.TrustFailure, null)), "certificate failure not retried");
            WebRequest.DefaultWebProxy = null;
            Bridge.Init(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test-account"));
            ServerCase(new[] { 503, 200 }, false, true, "");
            ServerCase(new[] { 503, 503 }, false, false, "503");
            ServerCase(new[] { 503 }, true, false, "503");
            ServerCase(new[] { 403 }, false, false, "403");
            ServerCase(new[] { 407 }, false, false, "407");
            Console.WriteLine("All connection regression checks passed."); return 0;
        } catch (Exception error) { Console.WriteLine("FAIL " + error.GetType().Name + ": " + error.Message); return 1; }
    }
}
