// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
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
    readonly ComboBox paths = new NightComboBox();
    readonly Button browse = new NightButton(), scan = new NightButton(), install = new NightButton(), enable = new NightButton(), disable = new NightButton(), undo = new NightButton(), verify = new NightButton();
    readonly DataGridView table = new DataGridView();
    readonly Label summary = new Label(), detailTitle = new Label(), footer = new Label();
    readonly TextBox details = new TextBox(), output = new TextBox();
    readonly BackgroundWorker worker = new BackgroundWorker();
    readonly JavaScriptSerializer serializer = new JavaScriptSerializer();
    readonly Color ink=Color.FromArgb(238,235,247), green=Color.FromArgb(71,67,94), muted=Color.FromArgb(172,168,190), paper=Color.FromArgb(25,25,42);
    string scannedPath="", jobPath="", jobId="", jobAction="", previousId="", lastMessage="";
    bool busy, blocked=true;
    Dictionary<string,object> currentStatus;
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);
    [DllImport("uxtheme.dll",CharSet=CharSet.Unicode)] static extern int SetWindowTheme(IntPtr window,string subApp,string subId);
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        try {int dark=1;if(DwmSetWindowAttribute(Handle,20,ref dark,4)!=0)DwmSetWindowAttribute(Handle,19,ref dark,4);}catch(DllNotFoundException){}catch(EntryPointNotFoundException){}
    }
    internal ManagerWindow(string initialPath, bool render)
    {
        Text="Chill with You · 音乐插件"; ClientSize=new Size(1120,880); MinimumSize=new Size(1040,860);
        StartPosition=FormStartPosition.CenterScreen; Font=new Font("Microsoft YaHei UI",9.5F); BackColor=paper; ForeColor=ink;
        AutoScaleMode=AutoScaleMode.Dpi;
        Panel header=new NightHeader {Dock=DockStyle.Top,Height=112,BackColor=paper}; Controls.Add(header);
        header.Controls.Add(new Label {Text="CHILL WITH YOU  /  MUSIC",Location=new Point(108,20),Size=new Size(600,22),Font=new Font(Font.FontFamily,9F),ForeColor=muted,BackColor=Color.Transparent});
        header.Controls.Add(new Label {Text="放松时光 · 音乐插件",Location=new Point(104,42),Size=new Size(680,42),Font=new Font(Font.FontFamily,24F),ForeColor=ink,BackColor=Color.Transparent});
        Label badge=new Label {Text="v1.6.0-rc.2 / 红心更新",Location=new Point(865,42),Size=new Size(220,32),TextAlign=ContentAlignment.MiddleRight,ForeColor=muted,BackColor=Color.Transparent,Anchor=AnchorStyles.Top|AnchorStyles.Right}; header.Controls.Add(badge);
        Panel content=new Panel {Dock=DockStyle.Fill,Padding=new Padding(26,16,26,14),BackColor=paper}; Controls.Add(content); content.BringToFront();
        TableLayoutPanel layout=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=1,RowCount=7};
        foreach(int height in new[]{68,34,324,48,112,80}) layout.RowStyles.Add(new RowStyle(SizeType.Absolute,height));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent,100)); content.Controls.Add(layout);
        TableLayoutPanel directory=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=3,RowCount=2};
        directory.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));directory.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,104));directory.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,110));
        directory.RowStyles.Add(new RowStyle(SizeType.Absolute,24)); directory.RowStyles.Add(new RowStyle(SizeType.Absolute,34));
        directory.Controls.Add(new Label {Text="游戏目录",AutoSize=true,ForeColor=muted},0,0);
        paths.Dock=DockStyle.Fill; paths.DropDownStyle=ComboBoxStyle.DropDown; paths.Margin=new Padding(0,0,10,0); paths.BackColor=Color.FromArgb(38,38,58);paths.ForeColor=ink;directory.Controls.Add(paths,0,1);
        StyleButton(browse,"选择目录",false); browse.Dock=DockStyle.Fill; browse.Margin=new Padding(0,0,8,0); directory.Controls.Add(browse,1,1);
        StyleButton(scan,"扫描 / 刷新",true); scan.Dock=DockStyle.Fill; scan.Margin=Padding.Empty; directory.Controls.Add(scan,2,1); layout.Controls.Add(directory,0,0);
        summary.Dock=DockStyle.Fill;summary.Text="请选择游戏目录，再扫描插件状态。";summary.TextAlign=ContentAlignment.MiddleLeft;summary.ForeColor=muted; layout.Controls.Add(summary,0,1);
        table.Dock=DockStyle.Fill;table.ReadOnly=true;table.AllowUserToAddRows=false;table.AllowUserToDeleteRows=false;table.AllowUserToResizeRows=false;table.MultiSelect=false;table.SelectionMode=DataGridViewSelectionMode.FullRowSelect;table.RowHeadersVisible=false;table.BackgroundColor=Color.FromArgb(33,33,52);table.BorderStyle=BorderStyle.None;table.CellBorderStyle=DataGridViewCellBorderStyle.SingleHorizontal;table.GridColor=Color.FromArgb(49,47,69);table.AutoGenerateColumns=false;table.EnableHeadersVisualStyles=false;
        table.ColumnHeadersBorderStyle=DataGridViewHeaderBorderStyle.None;
        table.ColumnHeadersDefaultCellStyle=new DataGridViewCellStyle {BackColor=Color.FromArgb(40,39,60),ForeColor=muted,Font=Font,SelectionBackColor=Color.FromArgb(40,39,60),Padding=new Padding(12,0,0,0)};
        table.DefaultCellStyle=new DataGridViewCellStyle {BackColor=Color.FromArgb(33,33,52),ForeColor=ink,SelectionBackColor=Color.FromArgb(64,60,84),SelectionForeColor=Color.FromArgb(250,244,253),Padding=new Padding(12,0,0,0)};
        table.AlternatingRowsDefaultCellStyle.BackColor=Color.FromArgb(35,34,54);
        table.RowTemplate.Height=35;table.ColumnHeadersHeight=35;table.ColumnHeadersHeightSizeMode=DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        foreach(string[] col in new[]{new[]{"平台","22"},new[]{"随包版本","15"},new[]{"已装版本","15"},new[]{"文件状态","36"},new[]{"快捷键","12"}}) {
            table.Columns.Add(new DataGridViewTextBoxColumn {HeaderText=col[0],AutoSizeMode=DataGridViewAutoSizeColumnMode.Fill,FillWeight=int.Parse(col[1]),SortMode=DataGridViewColumnSortMode.NotSortable});
        }
        table.SelectionChanged+=delegate{ShowSelected();};NightPanel tableFrame=new NightPanel{Dock=DockStyle.Fill,Padding=new Padding(1),BackColor=Color.FromArgb(33,33,52),Margin=Padding.Empty};tableFrame.Controls.Add(table);layout.Controls.Add(tableFrame,0,2);
        FlowLayoutPanel actions=new FlowLayoutPanel {Dock=DockStyle.Fill,WrapContents=false,Padding=new Padding(0,6,0,0)};
        foreach(Button b in new[]{install,enable,disable,undo,verify}) {b.Size=new Size(b==install?196:b==undo?162:130,34);b.Margin=new Padding(0,0,10,0);actions.Controls.Add(b);}
        StyleButton(install,"安装 / 修复至随包版本",true);StyleButton(enable,"重新启用",false);StyleButton(disable,"停用",false);StyleButton(undo,"撤回上次操作",false);StyleButton(verify,"校验安装包",false);layout.Controls.Add(actions,0,3);
        TableLayoutPanel selectedPanel=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,BackColor=Color.FromArgb(36,35,55),Padding=new Padding(16,10,16,8)};
        selectedPanel.RowStyles.Add(new RowStyle(SizeType.Absolute,24));selectedPanel.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        detailTitle.Dock=DockStyle.Fill;detailTitle.Font=new Font(Font.FontFamily,11F);selectedPanel.Controls.Add(detailTitle,0,0);
        details.Dock=DockStyle.Fill;details.Multiline=true;details.ReadOnly=true;details.BorderStyle=BorderStyle.None;details.ScrollBars=ScrollBars.Vertical;details.BackColor=selectedPanel.BackColor;details.ForeColor=muted;selectedPanel.Controls.Add(details,0,1);layout.Controls.Add(selectedPanel,0,4);
        output.Dock=DockStyle.Fill;output.Multiline=true;output.ReadOnly=true;output.ScrollBars=ScrollBars.Vertical;output.BorderStyle=BorderStyle.None;output.BackColor=Color.FromArgb(30,30,48);output.ForeColor=muted;output.Margin=new Padding(0,12,0,0);layout.Controls.Add(output,0,5);
        footer.Text="修改前请退出游戏。本候选包暂不含 QQ；酷狗、喜马拉雅红心受平台验证限制。账号、存档与本地进度保留。";footer.Dock=DockStyle.Fill;footer.ForeColor=muted;footer.TextAlign=ContentAlignment.MiddleLeft;layout.Controls.Add(footer,0,6);
        foreach(Control control in new Control[]{paths,details,output,table})control.HandleCreated+=delegate(object sender,EventArgs e){try{SetWindowTheme(((Control)sender).Handle,"DarkMode_Explorer",null);}catch(DllNotFoundException){}catch(EntryPointNotFoundException){}};
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
    void StyleButton(Button b,string text,bool accent) {b.Text=text;b.FlatStyle=FlatStyle.Flat;b.FlatAppearance.BorderSize=0;b.BackColor=accent?green:Color.FromArgb(39,38,58);b.ForeColor=ink;b.Cursor=Cursors.Hand;b.UseVisualStyleBackColor=false;((NightButton)b).Primary=accent;}
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
            table.Rows[n].Cells[3].Style.ForeColor=Flag(p,"Enabled")?Color.FromArgb(172,217,215):muted;
            if(Flag(p,"NeedsRepair")||!Flag(p,"PackageOk"))table.Rows[n].DefaultCellStyle.ForeColor=Color.FromArgb(237,185,183);
        }
        blocked=true;object diagObject;
        if(status.TryGetValue("Diagnostics",out diagObject)) {
            Dictionary<string,object> diag=(Dictionary<string,object>)diagObject;StringBuilder diagText=new StringBuilder();int count=0;
            foreach(object item in Items(diag,"Blockers")){diagText.AppendLine("需处理 · "+item);count++;}
            foreach(object item in Items(diag,"Messages"))diagText.AppendLine(Convert.ToString(item));blocked=count>0;
            output.Text=diagText.ToString();
            if(!string.IsNullOrEmpty(lastMessage))Log(lastMessage);
        }
        summary.Text=table.Rows.Count+" 个插件（含进度、倍速）  ·  "+active+" 个启用  ·  "+stopped+" 个停用"+(blocked?"  ·  请查看下方检查结果":"  ·  可以管理插件");
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
        if(!string.IsNullOrEmpty(game))arguments+=" -GameDirectory "+Quote(Path.GetFullPath(game));if(!string.IsNullOrEmpty(id))arguments+=" -Id "+Quote(id);
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
            using(ManagerWindow window=new ManagerWindow(game,!string.IsNullOrEmpty(render)))
            {
                if(!string.IsNullOrEmpty(render))
                {
                    // Render the verified fixture state without exposing the operator's local path.
                    window.paths.Text=@"C:\Games\Chill with You Lo-Fi Story";
                    window.scannedPath=window.paths.Text;
                    window.LoadStatus(window.currentStatus);
                    window.ShowInTaskbar=false;window.Opacity=0;window.Show();Application.DoEvents();
                    window.ActiveControl=window.table;window.paths.SelectionLength=0;Application.DoEvents();
                    using(Bitmap bitmap=new Bitmap(window.Width,window.Height))
                    {window.DrawToBitmap(bitmap,new Rectangle(0,0,bitmap.Width,bitmap.Height));bitmap.Save(render,ImageFormat.Png);}
                    File.WriteAllText(render+".json",window.serializer.Serialize(new {Status=window.currentStatus,InstallEnabled=window.install.Enabled,DisableEnabled=window.disable.Enabled,UndoEnabled=window.undo.Enabled}),Encoding.UTF8);
                    window.Close();
                }
                else Application.Run(window);
            }
            return 0;
        }catch(Exception e){if(Array.IndexOf(args,"--render")>=0){Console.Error.WriteLine(e.ToString());return 1;}MessageBox.Show(e.Message,"Chill 音乐插件管理器",MessageBoxButtons.OK,MessageBoxIcon.Error);return 1;}
    }
}

internal static class NightDrawing
{
    internal static GraphicsPath Rounded(RectangleF rect,float radius)
    {
        GraphicsPath path=new GraphicsPath();float d=Math.Min(radius*2,Math.Min(rect.Width,rect.Height));
        path.AddArc(rect.X,rect.Y,d,d,180,90);path.AddArc(rect.Right-d,rect.Y,d,d,270,90);
        path.AddArc(rect.Right-d,rect.Bottom-d,d,d,0,90);path.AddArc(rect.X,rect.Bottom-d,d,d,90,90);path.CloseFigure();return path;
    }
}
internal sealed class NightButton : Button
{
    internal bool Primary;
    bool hover,down;
    internal NightButton(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);}
    protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
    protected override void OnMouseLeave(EventArgs e){hover=down=false;Invalidate();base.OnMouseLeave(e);}
    protected override void OnMouseDown(MouseEventArgs e){down=true;Invalidate();base.OnMouseDown(e);}
    protected override void OnMouseUp(MouseEventArgs e){down=false;Invalidate();base.OnMouseUp(e);}
    protected override void OnEnabledChanged(EventArgs e){Invalidate();base.OnEnabledChanged(e);}
    protected override void OnGotFocus(EventArgs e){Invalidate();base.OnGotFocus(e);}
    protected override void OnLostFocus(EventArgs e){Invalidate();base.OnLostFocus(e);}
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(Parent==null?Color.FromArgb(25,25,42):Parent.BackColor);
        Color fill=!Enabled?Color.FromArgb(31,31,47):down?Color.FromArgb(78,71,104):hover?Color.FromArgb(63,59,83):BackColor;
        Color edge=!Enabled?Color.FromArgb(65,62,81):Primary?Color.FromArgb(177,164,201):Color.FromArgb(106,101,129);
        using(GraphicsPath shape=NightDrawing.Rounded(new RectangleF(0.75f,0.75f,Width-1.5f,Height-1.5f),Height/2f-1))
        using(Brush brush=new SolidBrush(fill))using(Pen pen=new Pen(edge,1f)){g.FillPath(brush,shape);g.DrawPath(pen,shape);}
        if(Primary&&Enabled){using(Pen cyan=new Pen(Color.FromArgb(115,207,219),1.1f))g.DrawArc(cyan,2,2,Height-5,Height-5,140,100);using(Pen pink=new Pen(Color.FromArgb(223,113,181),1.1f))g.DrawArc(pink,Width-Height+2,2,Height-5,Height-5,-40,100);}
        TextRenderer.DrawText(g,Text,Font,ClientRectangle,Enabled?ForeColor:Color.FromArgb(119,116,139),TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine);
        if(Focused&&ShowFocusCues){using(GraphicsPath shape=NightDrawing.Rounded(new RectangleF(4,4,Width-8,Height-8),Height/2f-4))using(Pen pen=new Pen(Color.FromArgb(207,204,228),1)){pen.DashStyle=DashStyle.Dot;g.DrawPath(pen,shape);}}
    }
}
internal sealed class NightPanel : Panel
{
    internal NightPanel(){DoubleBuffered=true;}
    protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);using(Pen edge=new Pen(Color.FromArgb(86,81,111)))e.Graphics.DrawRectangle(edge,0,0,Width-1,Height-1);}
}
internal sealed class NightHeader : Panel
{
    internal NightHeader(){DoubleBuffered=true;}
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
        using(LinearGradientBrush wash=new LinearGradientBrush(ClientRectangle,Color.FromArgb(32,34,55),Color.FromArgb(43,31,55),0f))g.FillRectangle(wash,ClientRectangle);
        Rectangle ring=new Rectangle(28,24,62,62);
        using(Brush fill=new SolidBrush(Color.FromArgb(32,31,51)))g.FillEllipse(fill,ring);
        using(Pen line=new Pen(Color.FromArgb(124,116,154),1.4f))g.DrawEllipse(line,ring);
        using(Pen cyan=new Pen(Color.FromArgb(105,205,220),1.5f))g.DrawArc(cyan,26,22,66,66,125,150);
        using(Pen pink=new Pen(Color.FromArgb(222,100,175),1.5f))g.DrawArc(pink,26,22,66,66,-55,150);
        using(Pen icon=new Pen(Color.FromArgb(218,210,237),2.5f))
        {g.DrawArc(icon,44,39,30,30,180,180);g.DrawLine(icon,44,53,44,65);g.DrawLine(icon,74,53,74,65);g.DrawRectangle(icon,44,55,5,12);g.DrawRectangle(icon,69,55,5,12);}
        for(int y=Height-18;y<Height;y+=4)for(int x=(y%8);x<Width;x+=8)
        {int alpha=8+(Height-y);using(Brush dot=new SolidBrush(Color.FromArgb(alpha,195,180,220)))g.FillEllipse(dot,x,y,1.4f,1.4f);}
        using(Pen edge=new Pen(Color.FromArgb(62,57,82)))g.DrawLine(edge,0,Height-1,Width,Height-1);
        base.OnPaint(e);
    }
}
internal sealed class NightComboBox : ComboBox
{
    internal NightComboBox(){FlatStyle=FlatStyle.Flat;DrawMode=DrawMode.OwnerDrawFixed;ItemHeight=24;}
    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        bool selected=(e.State&DrawItemState.Selected)!=0;
        using(Brush fill=new SolidBrush(selected?Color.FromArgb(69,62,92):Color.FromArgb(38,38,58)))e.Graphics.FillRectangle(fill,e.Bounds);
        if(e.Index>=0)TextRenderer.DrawText(e.Graphics,GetItemText(Items[e.Index]),Font,e.Bounds,ForeColor,TextFormatFlags.VerticalCenter|TextFormatFlags.Left|TextFormatFlags.EndEllipsis);
        e.DrawFocusRectangle();
    }
}
