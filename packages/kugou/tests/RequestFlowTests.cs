// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Net;
using Newtonsoft.Json.Linq;
#if BILI
using ChillBilibiliPlaylist;
#else
using ChillKuGouPlaylist;
#endif
class RequestFlowTests
{
 static void Check(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS "+message);}
 static int Main()
 {
  try {
   var http=RequestIssue.Http(412,"/x/web-interface/nav");
   Check(http.RequiresVerification && !http.LoginExpired && http.WaitSeconds==60,"HTTP 412 preserves login and stops rapid retries");
   Check(RequestIssue.Http(429,"/v7/get_all_list").WaitSeconds==60,"HTTP rate limit pauses requests");
   Check(!RequestIssue.Http(500,"/v7/get_all_list").LoginExpired,"server failure does not pretend session expired");
   string privateText="PRIVATE_TOKEN_MUST_NOT_LEAK";
   var other=RequestIssue.Api(12345,"/unknown?token="+privateText,true,999999);
   Check(!other.Message.Contains(privateText) && !other.DiagnosticCode.Contains(privateText) && other.VerificationType==0,"feedback code excludes paths, tokens and unbounded type fields");
   Check(RequestIssue.Cooling(500).WaitSeconds==60 && RequestIssue.Cooling(-3).WaitSeconds==1,"cooldown hint remains bounded");
#if BILI
   Check(RequestIssue.Api(-101,"/x/web-interface/nav",false,0).LoginExpired,"only recognized authentication failure asks for another scan");
   Check(RequestIssue.Api(-352,"/x/v3/fav/folder/created/list-all",false,0).RequiresVerification,"Bilibili security rejection is distinct from expired login");
   var jar=new CookieContainer();jar.Add(new Cookie("SESSDATA",privateText,"/",".bilibili.com"){Secure=true});
   var data=new JObject{{"b_3","A123456789-DEVICEinfoc"},{"b_4","B123456789-DEVICE=="}};
   Check(DeviceCookies.Apply(jar,data) && DeviceCookies.Complete(jar),"server-provided device cookies are installed including base64 padding");
   Check(jar.GetCookies(DeviceCookies.Origin)["SESSDATA"].Value==privateText,"device initialization preserves authenticated cookie");
   Check(!DeviceCookies.Apply(jar,new JObject{{"b_3","DIFFERENT_DEVICE_VALUE"}}) && jar.GetCookies(DeviceCookies.Origin)["buvid3"].Value=="A123456789-DEVICEinfoc","existing device identity is not rotated");
   var qr=new CookieContainer();DeviceCookies.Copy(jar,qr);
   Check(DeviceCookies.Complete(qr) && qr.GetCookies(DeviceCookies.Origin)["SESSDATA"]==null,"new QR flow inherits device identity without copying account cookies");
   var bad=new CookieContainer();DeviceCookies.Apply(bad,new JObject{{"b_3","bad\r\nHeader: injection"},{"b_4",new string('x',300)}});
   Check(!DeviceCookies.Complete(bad) && bad.Count==0,"invalid device values cannot inject cookies");
#else
   var verification=RequestIssue.Api(20028,"/v7/get_all_list",true,23);
   Check(verification.RequiresVerification && !verification.LoginExpired && verification.HasVerificationEvent && verification.VerificationType==23,"Kugou 20028 retains login and identifies human verification");
   Check(verification.Message.Contains("暂不支持") && !verification.Message.Contains("重新扫码登录"),"unsupported challenge is disclosed instead of claiming automatic recovery");
   Check(!RequestIssue.Api(10000,"/v7/get_all_list",false,0).LoginExpired,"unknown Kugou failure does not discard session");
#endif
   return 0;
  }catch(Exception error){Console.WriteLine("FAIL "+error.Message);return 1;}
 }
}
