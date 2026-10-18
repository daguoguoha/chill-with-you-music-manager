// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.Win32;

internal sealed class InstallerWindow : Form
{
    readonly ComboBox paths = new ComboBox();
    readonly Button browse = new Button(), install = new Button();
    readonly TextBox log = new TextBox();
    readonly BackgroundWorker worker = new BackgroundWorker();
    readonly bool disable;
    readonly string root;
    string selected;
    internal InstallerWindow(bool stop)
    {
        disable = stop; root = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        Text = "Chill With You 喜马拉雅播放器 · 一键部署";
        ClientSize = new Size(680,410); Font = new Font("Microsoft YaHei UI",9F);
        StartPosition = FormStartPosition.CenterScreen; FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false;
        Label label = new Label(); label.Text = "游戏目录（包含 Chill With You.exe）"; label.Location = new Point(18,18); label.AutoSize = true; Controls.Add(label);
        paths.Location = new Point(18,45); paths.Size = new Size(548,27); paths.DropDownStyle = ComboBoxStyle.DropDown;
        foreach(string path in FindGames()) paths.Items.Add(path);
        if(paths.Items.Count == 1) paths.SelectedIndex = 0;
        Controls.Add(paths);
        browse.Text = "选择目录"; browse.Location = new Point(574,44); browse.Size = new Size(88,29);
        browse.Click += delegate { using(FolderBrowserDialog dialog = new FolderBrowserDialog()) { dialog.Description = "选择游戏安装文件夹"; if(Directory.Exists(paths.Text)) dialog.SelectedPath = paths.Text; if(dialog.ShowDialog(this) == DialogResult.OK) paths.Text = dialog.SelectedPath; } };
        Controls.Add(browse);
        install.Text = disable ? "停用插件" : "安装 / 更新"; install.Location = new Point(18,90); install.Size = new Size(160,38); Controls.Add(install);
        log.Location = new Point(18,144); log.Size = new Size(644,248); log.Multiline = true; log.ReadOnly = true; log.ScrollBars = ScrollBars.Vertical; Controls.Add(log);
        install.Click += delegate {
            selected = paths.Text.Trim().TrimEnd('\\');
            if(!File.Exists(Path.Combine(selected,"Chill With You.exe")) || !Directory.Exists(Path.Combine(selected,@"Chill With You_Data\Managed"))) { MessageBox.Show(this,"请选择包含 Chill With You.exe 的游戏文件夹。","目录不正确"); return; }
            if(!File.Exists(Path.Combine(root,"Install.ps1"))) { MessageBox.Show(this,"请先完整解压安装包。","缺少安装文件"); return; }
            install.Enabled = browse.Enabled = paths.Enabled = false; Append(disable ? "正在停用插件…" : "开始检查并安装…"); worker.RunWorkerAsync();
        };
        worker.DoWork += RunBackend;
        worker.RunWorkerCompleted += delegate(object sender,RunWorkerCompletedEventArgs e) {
            install.Enabled = browse.Enabled = paths.Enabled = true;
            bool ok = e.Error == null && (int)e.Result == 0;
            if(e.Error != null) Append(e.Error.Message);
            MessageBox.Show(this,ok ? "操作完成，详细信息见窗口记录。" : "操作未完成，请查看窗口中的具体原因。","喜马拉雅播放器",MessageBoxButtons.OK,ok ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        };
        FormClosing += delegate(object sender,FormClosingEventArgs e) { if(worker.IsBusy) { e.Cancel = true; MessageBox.Show(this,"正在处理文件，请等操作完成后关闭。","喜马拉雅播放器"); } };
        Append(paths.Items.Count == 1 ? "已自动找到游戏目录。请退出游戏后点击安装 / 更新。" : paths.Items.Count > 1 ? "发现多个游戏目录，请从下拉列表中选择。" : "未自动找到游戏，请点击“选择目录”。");
        if(!disable) Append("已内置官方依赖，无需联网安装；校验 SHA256，并备份被更新的插件文件。");
    }
    void Append(string text)
    {
        if(IsDisposed || string.IsNullOrEmpty(text)) return;
        if(InvokeRequired) { BeginInvoke(new Action<string>(Append),text); return; }
        log.AppendText(text + Environment.NewLine);
    }
    void RunBackend(object sender,DoWorkEventArgs e)
    {
        string shell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),@"WindowsPowerShell\v1.0\powershell.exe");
        string arguments = "-NoLogo -NoProfile -ExecutionPolicy Bypass -File \""+Path.Combine(root,"Install.ps1")+"\" -Headless -GameDirectory \""+selected+"\""+(disable ? " -Disable" : "");
        ProcessStartInfo start = new ProcessStartInfo(shell,arguments);
        start.UseShellExecute = false; start.CreateNoWindow = true; start.WorkingDirectory = root;
        start.RedirectStandardOutput = true; start.RedirectStandardError = true; start.StandardOutputEncoding = Encoding.UTF8; start.StandardErrorEncoding = Encoding.UTF8;
        using(Process child = new Process()) { child.StartInfo = start; child.OutputDataReceived += delegate(object s,DataReceivedEventArgs line) { Append(line.Data); }; child.ErrorDataReceived += delegate(object s,DataReceivedEventArgs line) { Append(line.Data); }; child.Start(); child.BeginOutputReadLine(); child.BeginErrorReadLine(); child.WaitForExit(); e.Result = child.ExitCode; }
    }
    static IEnumerable<string> FindGames()
    {
        HashSet<string> roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(RegistryKey hive in new[]{Registry.CurrentUser,Registry.LocalMachine}) foreach(string keyName in new[]{@"Software\Valve\Steam",@"Software\WOW6432Node\Valve\Steam"}) {
            using(RegistryKey key = hive.OpenSubKey(keyName)) { if(key == null) continue; foreach(string name in new[]{"SteamPath","InstallPath"}) { string value = key.GetValue(name) as string; if(!string.IsNullOrEmpty(value)) roots.Add(value); } }
        }
        HashSet<string> libraries = new HashSet<string>(roots,StringComparer.OrdinalIgnoreCase);
        foreach(string steam in roots) { string file = Path.Combine(steam,@"steamapps\libraryfolders.vdf"); if(File.Exists(file)) foreach(Match m in Regex.Matches(File.ReadAllText(file),"\"path\"\\s+\"([^\"]+)\"")) libraries.Add(m.Groups[1].Value.Replace(@"\\",@"\")); }
        HashSet<string> found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(string library in libraries) {
            string folder = "Chill with You Lo-Fi Story", manifest = Path.Combine(library,@"steamapps\appmanifest_3548580.acf");
            if(File.Exists(manifest)) { Match m = Regex.Match(File.ReadAllText(manifest),"\"installdir\"\\s+\"([^\"]+)\""); if(m.Success) folder = m.Groups[1].Value; }
            string path = Path.GetFullPath(Path.Combine(library,@"steamapps\common",folder));
            if(File.Exists(Path.Combine(path,"Chill With You.exe")) && Directory.Exists(Path.Combine(path,@"Chill With You_Data\Managed"))) found.Add(path);
        }
        return found;
    }
    [STAThread] static int Main(string[] args)
    {
        try { Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false); Application.Run(new InstallerWindow(args.Length == 1 && args[0] == "--disable")); return 0; }
        catch(Exception e) { MessageBox.Show(e.Message,"喜马拉雅播放器",MessageBoxButtons.OK,MessageBoxIcon.Error); return 1; }
    }
}
