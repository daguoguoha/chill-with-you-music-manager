// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
namespace ChillSaltPlayerPlaylist
{
    internal static class FilePicker
    {
        static Process active;
        internal static string Select(bool folder)
        {
            string exe=Path.Combine(Path.GetDirectoryName(typeof(FilePicker).Assembly.Location),"ChillSaltPlayerFilePicker.exe");
            if(!System.IO.File.Exists(exe))throw new InvalidOperationException("文件选择器缺失，请重新安装；也可直接粘贴路径。");
            var start=new ProcessStartInfo{FileName=exe,Arguments=folder?"folder":"file",UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
            using(var process=Process.Start(start))
            {
                active=process;
                try
                {
                    string result=process.StandardOutput.ReadToEnd();process.WaitForExit();
                    if(process.ExitCode==2)return null;
                    if(process.ExitCode!=0 || result.Length>131072)throw new InvalidOperationException("文件选择未完成，请重试或直接粘贴路径。");
                    if(string.IsNullOrWhiteSpace(result))return null;
                    try{return Encoding.UTF8.GetString(Convert.FromBase64String(result.Trim()));}catch(FormatException){throw new InvalidOperationException("文件选择结果无效。");}
                }
                finally{active=null;}
            }
        }
        internal static void Cancel(){try{Process process=active;if(process!=null && !process.HasExited)process.Kill();}catch(InvalidOperationException){}catch(System.ComponentModel.Win32Exception){}}
    }
}
