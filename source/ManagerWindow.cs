// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

internal sealed class ManagerWindow : Form
{
    readonly string root = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
    readonly ComboBox paths = new ComboBox();
    readonly Button browse = new Button(), scan = new Button(), install = new Button(), enable = new Button(), disable = new Button(), undo = new Button(), verify = new Button();
    readonly DataGridView table = new DataGridView();
    readonly Label summary = new Label(), detailTitle = new Label(), footer = new Label();
    readonly TextBox details = new TextBox(), output = new TextBox();
    readonly BackgroundWorker worker = new BackgroundWorker();
    readonly JavaScriptSerializer serializer = new JavaScriptSerializer();
    readonly Color ink=Color.FromArgb(37,53,47), green=Color.FromArgb(50,100,79), muted=Color.FromArgb(105,115,107), paper=Color.FromArgb(245,245,239);
    string scannedPath="", jobPath="", jobId="", jobAction="", previousId="", lastMessage="";
    bool busy, blocked=true;
    Dictionary<string,object> currentStatus;
    internal ManagerWindow(string initialPath, bool render)
    {
        Text="Chill 音乐插件管理器"; ClientSize=new Size(1100,790); MinimumSize=new Size(1000,760);
        StartPosition=FormStartPosition.CenterScreen; Font=new Font("Microsoft YaHei UI",9F); BackColor=paper; ForeColor=ink;
        AutoScaleMode=AutoScaleMode.Dpi;
        Panel header=new Panel {Dock=DockStyle.Top,Height=105,BackColor=ink}; Controls.Add(header);
        header.Controls.Add(new Label {Text="CHILL / MUSIC TOOLS",Location=new Point(26,15),Size=new Size(600,22),Font=new Font(Font.FontFamily,9F,FontStyle.Bold),ForeColor=Color.FromArgb(183,208,190)});
        header.Controls.Add(new Label {Text="音乐插件，一个地方管好",Location=new Point(24,39),Size=new Size(850,40),Font=new Font(Font.FontFamily,23F,FontStyle.Bold),ForeColor=Color.White});
        Label badge=new Label {Text="v1.0.0  ·  离线安装",Location=new Point(865,47),Size=new Size(204,30),TextAlign=ContentAlignment.MiddleCenter,ForeColor=Color.White,BackColor=green}; header.Controls.Add(badge);
        Panel content=new Panel {Dock=DockStyle.Fill,Padding=new Padding(24,14,24,16)}; Controls.Add(content); content.BringToFront();
        TableLayoutPanel layout=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=1,RowCount=7};
        foreach(int height in new[]{62,36,225,43,120,112}) layout.RowStyles.Add(new RowStyle(SizeType.Absolute,height));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent,100)); content.Controls.Add(layout);
        TableLayoutPanel directory=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=3,RowCount=2};
        directory.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));directory.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,104));directory.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,110));
        directory.RowStyles.Add(new RowStyle(SizeType.Absolute,24)); directory.RowStyles.Add(new RowStyle(SizeType.Absolute,34));
        directory.Controls.Add(new Label {Text="游戏目录",AutoSize=true,ForeColor=muted},0,0);
        paths.Dock=DockStyle.Fill; paths.DropDownStyle=ComboBoxStyle.DropDown; paths.Margin=new Padding(0,0,10,0); directory.Controls.Add(paths,0,1);
        StyleButton(browse,"选择目录",false); browse.Dock=DockStyle.Fill; browse.Margin=new Padding(0,0,8,0); directory.Controls.Add(browse,1,1);
        StyleButton(scan,"扫描 / 刷新",true); scan.Dock=DockStyle.Fill; scan.Margin=Padding.Empty; directory.Controls.Add(scan,2,1); layout.Controls.Add(directory,0,0);
        summary.Dock=DockStyle.Fill;summary.Text="请选择游戏目录，再扫描插件状态。";summary.TextAlign=ContentAlignment.MiddleLeft;summary.ForeColor=muted; layout.Controls.Add(summary,0,1);
        table.Dock=DockStyle.Fill;table.ReadOnly=true;table.AllowUserToAddRows=false;table.AllowUserToDeleteRows=false;table.AllowUserToResizeRows=false;table.MultiSelect=false;table.SelectionMode=DataGridViewSelectionMode.FullRowSelect;table.RowHeadersVisible=false;table.BackgroundColor=Color.White;table.BorderStyle=BorderStyle.None;table.CellBorderStyle=DataGridViewCellBorderStyle.SingleHorizontal;table.GridColor=paper;table.AutoGenerateColumns=false;table.EnableHeadersVisualStyles=false;
        table.ColumnHeadersDefaultCellStyle=new DataGridViewCellStyle {BackColor=Color.FromArgb(228,233,224),ForeColor=ink,Font=new Font(Font,FontStyle.Bold),SelectionBackColor=Color.FromArgb(228,233,224)};
        table.DefaultCellStyle=new DataGridViewCellStyle {BackColor=Color.White,ForeColor=ink,SelectionBackColor=Color.FromArgb(219,235,222),SelectionForeColor=ink,Padding=new Padding(8,0,0,0)};
        table.RowTemplate.Height=30;table.ColumnHeadersHeight=34;table.ColumnHeadersHeightSizeMode=DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        foreach(string[] col in new[]{new[]{"平台","22"},new[]{"随包版本","15"},new[]{"已装版本","15"},new[]{"文件状态","36"},new[]{"快捷键","12"}}) {
            table.Columns.Add(new DataGridViewTextBoxColumn {HeaderText=col[0],AutoSizeMode=DataGridViewAutoSizeColumnMode.Fill,FillWeight=int.Parse(col[1]),SortMode=DataGridViewColumnSortMode.NotSortable});
        }
        table.SelectionChanged+=delegate{ShowSelected();};layout.Controls.Add(table,0,2);
        FlowLayoutPanel actions=new FlowLayoutPanel {Dock=DockStyle.Fill,WrapContents=false,Padding=new Padding(0,6,0,0)};
        foreach(Button b in new[]{install,enable,disable,undo,verify}) {b.Size=new Size(b==install?180:b==undo?160:126,31);b.Margin=new Padding(0,0,8,0);actions.Controls.Add(b);}
        StyleButton(install,"安装 / 修复至随包版本",true);StyleButton(enable,"重新启用",false);StyleButton(disable,"停用",false);StyleButton(undo,"撤回上次操作",false);StyleButton(verify,"校验安装包",false);layout.Controls.Add(actions,0,3);
        TableLayoutPanel selectedPanel=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,BackColor=Color.White,Padding=new Padding(12,8,12,5)};
        selectedPanel.RowStyles.Add(new RowStyle(SizeType.Absolute,24));selectedPanel.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        detailTitle.Dock=DockStyle.Fill;detailTitle.Font=new Font(Font,FontStyle.Bold);selectedPanel.Controls.Add(detailTitle,0,0);
        details.Dock=DockStyle.Fill;details.Multiline=true;details.ReadOnly=true;details.BorderStyle=BorderStyle.None;details.ScrollBars=ScrollBars.Vertical;details.BackColor=Color.White;details.ForeColor=muted;selectedPanel.Controls.Add(details,0,1);layout.Controls.Add(selectedPanel,0,4);
        output.Dock=DockStyle.Fill;output.Multiline=true;output.ReadOnly=true;output.ScrollBars=ScrollBars.Vertical;output.BorderStyle=BorderStyle.FixedSingle;output.BackColor=Color.FromArgb(237,240,232);output.ForeColor=ink;output.Margin=new Padding(0,10,0,0);layout.Controls.Add(output,0,5);
        footer.Text="修改前请退出游戏。账号、歌单与存档保留。随包版本不代表线上最新版本。";footer.Dock=DockStyle.Fill;footer.ForeColor=muted;footer.TextAlign=ContentAlignment.MiddleLeft;layout.Controls.Add(footer,0,6);
        foreach(string p in FindGames()) paths.Items.Add(p);
        if(!string.IsNullOrEmpty(initialPath)) paths.Text=initialPath; else if(paths.Items.Count==1) paths.SelectedIndex=0;
        paths.TextChanged+=delegate { if(!busy) {scannedPath="";summary.Text="目录已变化，请点击扫描 / 刷新。";UpdateButtons();} };
        browse.Click+=delegate {using(FolderBrowserDialog dialog=new FolderBrowserDialog()) {dialog.Description="选择包含 Chill With You.exe 的文件夹";if(Directory.Exists(paths.Text))dialog.SelectedPath=paths.Text;if(dialog.ShowDialog(this)==DialogResult.OK)paths.Text=dialog.SelectedPath;}};
        scan.Click+=delegate {Start("Status");}; install.Click+=delegate {Start("Install");};enable.Click+=delegate {Start("Enable");};disable.Click+=delegate {Start("Disable");};undo.Click+=delegate {Start("Undo");};verify.Click+=delegate {Start("Verify");};
        worker.DoWork+=delegate(object sender,DoWorkEventArgs e) {e.Result=RunBackend(jobAction,jobPath,jobId);};
        worker.RunWorkerCompleted+=delegate(object sender,RunWorkerCompletedEventArgs e) {
            SetBusy(false);
            if(e.Error!=null) {Log("未完成："+e.Error.Message);return;}
            Dictionary<string,object> result=(Dictionary<string,object>)e.Result;
            if(jobAction=="Status" && Flag(result,"Ok")) {scannedPath=jobPath;currentStatus=result;LoadStatus(result);}
            else {lastMessage=(Flag(result,"Ok")?"完成：":"未完成：")+Value(result,"Message");Log(lastMessage); if(jobAction!="Verify")Start("Status");}
        };
        FormClosing+=delegate(object sender,FormClosingEventArgs e) {if(busy){e.Cancel=true;MessageBox.Show(this,"正在处理文件，请等操作完成。","音乐插件管理器");}};
        UpdateButtons();
        if(render) {currentStatus=RunBackend("Status",paths.Text,"");scannedPath=paths.Text;LoadStatus(currentStatus);}
        else Shown+=delegate{Start("Status");};
    }
    void StyleButton(Button b,string text,bool accent) {b.Text=text;b.FlatStyle=FlatStyle.Flat;b.FlatAppearance.BorderColor=Color.FromArgb(191,203,190);b.BackColor=accent?green:Color.White;b.ForeColor=accent?Color.White:ink;b.Cursor=Cursors.Hand;}
    static string Value(Dictionary<string,object> obj,string key) {object value;return obj!=null&&obj.TryGetValue(key,out value)&&value!=null?Convert.ToString(value):"";}
    static bool Flag(Dictionary<string,object> obj,string key) {object value;return obj!=null&&obj.TryGetValue(key,out value)&&value is bool&&(bool)value;}
    static IEnumerable<object> Items(Dictionary<string,object> obj,string key) {object value;if(obj!=null&&obj.TryGetValue(key,out value)&&value is IEnumerable&&!(value is string)) foreach(object x in (IEnumerable)value) yield return x;}
    Dictionary<string,object> Selected() {return table.CurrentRow==null?null:table.CurrentRow.Tag as Dictionary<string,object>;}
    void LoadStatus(Dictionary<string,object> status) {
        if(!Flag(status,"Ok")) {Log(Value(status,"Message"));return;}
        previousId=Selected()==null?previousId:Value(Selected(),"Id");table.Rows.Clear();int active=0,stopped=0;
        foreach(object item in Items(status,"Plugins")) {
            Dictionary<string,object> p=(Dictionary<string,object>)item;
            int n=table.Rows.Add(Value(p,"Name"),Value(p,"Version"),string.IsNullOrEmpty(Value(p,"InstalledVersion"))?"—":Value(p,"InstalledVersion"),Value(p,"State"),Value(p,"Hotkey"));table.Rows[n].Tag=p;
            if(Flag(p,"Enabled")) active++;else if(Flag(p,"Disabled")) stopped++;
            if(Flag(p,"NeedsRepair")||!Flag(p,"PackageOk"))table.Rows[n].DefaultCellStyle.ForeColor=Color.FromArgb(160,90,48);
        }
        blocked=true;object diagObject;
        if(status.TryGetValue("Diagnostics",out diagObject)) {
            Dictionary<string,object> diag=(Dictionary<string,object>)diagObject;StringBuilder diagText=new StringBuilder();int count=0;
            foreach(object item in Items(diag,"Blockers")){diagText.AppendLine("需处理 · "+item);count++;}
            foreach(object item in Items(diag,"Messages"))diagText.AppendLine(Convert.ToString(item));blocked=count>0;
            output.Text=diagText.ToString();
            if(!string.IsNullOrEmpty(lastMessage))Log(lastMessage);
        }
        summary.Text="6 个音乐平台  ·  "+active+" 个启用  ·  "+stopped+" 个停用"+(blocked?"  ·  请查看下方检查结果":"  ·  可以管理插件");
        foreach(DataGridViewRow row in table.Rows)if(Value((Dictionary<string,object>)row.Tag,"Id")==previousId){table.CurrentCell=row.Cells[0];break;}
        ShowSelected();
    }
    void ShowSelected() {Dictionary<string,object> p=Selected();if(p==null){detailTitle.Text="选择一个音乐平台";details.Clear();UpdateButtons();return;}
        detailTitle.Text=Value(p,"Name")+"  /  "+Value(p,"Hotkey");StringBuilder text=new StringBuilder(Value(p,"Hint"));
        foreach(object note in Items(p,"Notes"))text.AppendLine().Append(Convert.ToString(note));
        if(Flag(p,"CanUndo"))text.AppendLine().Append("可撤回该平台最近一次由本管理器完成的操作；共享框架保留。");details.Text=text.ToString();UpdateButtons();}
    void UpdateButtons() {Dictionary<string,object> p=Selected();bool ready=!busy&&p!=null&&scannedPath==paths.Text.Trim().TrimEnd('\\')&&!string.IsNullOrEmpty(scannedPath);
        install.Enabled=ready&&!blocked&&Flag(p,"PackageOk")&&!Flag(p,"NewerInstalled");enable.Enabled=ready&&!blocked&&Flag(p,"Disabled")&&!Flag(p,"Enabled")&&!Flag(p,"NeedsRepair")&&Flag(p,"PackageOk")&&!Flag(p,"NewerInstalled");disable.Enabled=ready&&Flag(p,"Enabled");undo.Enabled=ready&&Flag(p,"CanUndo");verify.Enabled=!busy&&p!=null;scan.Enabled=browse.Enabled=paths.Enabled=!busy;}
    void SetBusy(bool value) {busy=value;UseWaitCursor=value;UpdateButtons();}
    void Log(string message) {output.AppendText(Environment.NewLine+message+Environment.NewLine);output.SelectionStart=output.TextLength;output.ScrollToCaret();}
    void Start(string action) {
        if(busy)return;Dictionary<string,object> p=Selected();jobPath=paths.Text.Trim().TrimEnd('\\');
        if(action!="Status"&&p==null)return;
        if(action!="Status"&&action!="Verify"&&(jobPath!=scannedPath||string.IsNullOrEmpty(jobPath))){Log("请先扫描当前游戏目录。");return;}
        jobAction=action;jobId=p==null?"":Value(p,"Id");SetBusy(true);summary.Text=action=="Status"?"正在扫描插件文件与共享框架…":"正在处理，请稍候…";worker.RunWorkerAsync();
    }
    static string Quote(string arg) {
        StringBuilder result=new StringBuilder("\"");int slashes=0;
        foreach(char c in arg){if(c=='\\'){slashes++;continue;}if(c=='\"'){result.Append('\\',slashes*2+1);result.Append(c);}else{result.Append('\\',slashes);result.Append(c);}slashes=0;}
        result.Append('\\',slashes*2);return result.Append('"').ToString();
    }
    Dictionary<string,object> RunBackend(string action,string game,string id) {
        string engine=Path.Combine(root,"Manager.ps1");if(!File.Exists(engine))throw new IOException("缺少 Manager.ps1，请完整解压管理器包。");
        string shell=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),@"System32\WindowsPowerShell\v1.0\powershell.exe");
        string arguments="-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "+Quote(engine)+" -Action "+Quote(action);
        if(!string.IsNullOrEmpty(game))arguments+=" -GameDirectory "+Quote(game);if(!string.IsNullOrEmpty(id))arguments+=" -Id "+Quote(id);
        ProcessStartInfo info=new ProcessStartInfo(shell,arguments){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=root,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8};
        info.EnvironmentVariables["PSModulePath"]=Path.Combine(Path.GetDirectoryName(shell),"Modules");
        using(Process process=new Process()){process.StartInfo=info;process.Start();System.Threading.Tasks.Task<string> err=process.StandardError.ReadToEndAsync();string text=process.StandardOutput.ReadToEnd();process.WaitForExit();string error=err.Result;
            try {return serializer.Deserialize<Dictionary<string,object>>(text.Trim());}catch{throw new IOException(string.IsNullOrWhiteSpace(error)?"管理器未返回有效结果，请检查安装包是否完整。":error);}}
    }
    internal static IEnumerable<string> FindGames() {
        HashSet<string> roots=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(RegistryKey hive in new[]{Registry.CurrentUser,Registry.LocalMachine})foreach(string name in new[]{@"Software\Valve\Steam",@"Software\WOW6432Node\Valve\Steam"})
            using(RegistryKey key=hive.OpenSubKey(name)){if(key==null)continue;foreach(string valueName in new[]{"SteamPath","InstallPath"}){string value=key.GetValue(valueName) as string;if(!string.IsNullOrEmpty(value))roots.Add(value);}}
        HashSet<string> libraries=new HashSet<string>(roots,StringComparer.OrdinalIgnoreCase);
        foreach(string steam in roots){string file=Path.Combine(steam,@"steamapps\libraryfolders.vdf");if(File.Exists(file))foreach(Match m in Regex.Matches(File.ReadAllText(file),"\"path\"\\s+\"([^\"]+)\""))libraries.Add(m.Groups[1].Value.Replace(@"\\",@"\"));}
        HashSet<string> found=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(string library in libraries){string folder="Chill with You Lo-Fi Story",manifest=Path.Combine(library,@"steamapps\appmanifest_3548580.acf");if(File.Exists(manifest)){Match m=Regex.Match(File.ReadAllText(manifest),"\"installdir\"\\s+\"([^\"]+)\"");if(m.Success)folder=m.Groups[1].Value;}
            string path=Path.GetFullPath(Path.Combine(library,@"steamapps\common",folder));if(File.Exists(Path.Combine(path,"Chill With You.exe"))&&Directory.Exists(Path.Combine(path,@"Chill With You_Data\Managed")))found.Add(path);}
        return found;
    }
    [STAThread] static int Main(string[] args) {
        try {string game="",render="";for(int i=0;i<args.Length;i++){if(args[i]=="--game"&&i+1<args.Length)game=args[++i];else if(args[i]=="--render"&&i+1<args.Length)render=args[++i];else throw new ArgumentException("不支持的启动参数。");}
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            using(ManagerWindow window=new ManagerWindow(game,!string.IsNullOrEmpty(render))){if(!string.IsNullOrEmpty(render)){window.ShowInTaskbar=false;window.Opacity=0;window.Show();Application.DoEvents();using(Bitmap bitmap=new Bitmap(window.Width,window.Height)){window.DrawToBitmap(bitmap,new Rectangle(0,0,bitmap.Width,bitmap.Height));bitmap.Save(render,ImageFormat.Png);}File.WriteAllText(render+".json",window.serializer.Serialize(new {Status=window.currentStatus,InstallEnabled=window.install.Enabled,DisableEnabled=window.disable.Enabled,UndoEnabled=window.undo.Enabled}),Encoding.UTF8);window.Close();}else Application.Run(window);}return 0;
        }catch(Exception e){if(Array.IndexOf(args,"--render")>=0){Console.Error.WriteLine(e.ToString());return 1;}MessageBox.Show(e.Message,"Chill 音乐插件管理器",MessageBoxButtons.OK,MessageBoxIcon.Error);return 1;}
    }
}
