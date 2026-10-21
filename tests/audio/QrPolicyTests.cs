// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using ChillNeteasePlaylist;
public static class QrPolicyTests
{
    public static string Run()
    {
        string[] input = { "Failed to get QR key, code: -462", "context deadline exceeded", "x509 certificate error", "proxyconnect tcp error", "lookup music.163.com: no such host", "connection reset by peer", "Failed to send request: EOF", "QR key not initialized", "error unmarshaling response JSON", "unknown https://music.163.com/login?codekey=SECRET COOKIE=PRIVATE" };
        string[] expected = { "-462", "超时", "安全连接", "代理", "域名解析", "连接被关闭", "连接被关闭", "已失效", "响应无效", "请求未完成" };
        int count=0;
        for (int i = 0; i < input.Length; i++)
        {
            string output = QrRequest.Describe("key", input[i]);
            if (!output.Contains(expected[i]) || output.Contains("SECRET") || output.Contains("PRIVATE") || output.Contains("https://")) throw new Exception("case " + i);
            count++;
        }
        if (!QrRequest.Describe("poll", "timeout").StartsWith("检查扫码状态失败")) throw new Exception("poll stage");
        count++;
        foreach(string value in new[]{"timeout","proxyconnect tcp error","connection reset","EOF"}) {if(!QrRequest.CanRetry(value))throw new Exception("retry transient");count++;}
        foreach(string value in new[]{"code: -462","status: 403","x509 certificate error","QR key not initialized"}) {if(QrRequest.CanRetry(value))throw new Exception("do not retry refusal");count++;}
        return "PASS "+count+" QR error and retry checks";
    }
}
