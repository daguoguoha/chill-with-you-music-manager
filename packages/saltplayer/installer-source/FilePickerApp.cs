// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Text;
using System.Drawing;
using System.Windows.Forms;
internal static class FilePickerApp
{
    [STAThread]static int Main(string[] args)
    {
        try
        {
            Application.EnableVisualStyles();string selected=null;int result=2;
            if(args.Length!=1 || (args[0]!="folder" && args[0]!="file"))return 1;
            using(var owner=new Form{Text="椒盐音乐 · 选择本地文件",StartPosition=FormStartPosition.CenterScreen,ClientSize=new Size(420,100),MinimizeBox=false,MaximizeBox=false})
            {
            owner.Controls.Add(new Label{Text="请选择歌单或音乐文件；取消后可在游戏内粘贴路径。",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter});
            owner.Shown+=(sender,e)=>{
            try{
            if(args[0]=="folder")
            {
                using(var dialog=new FolderBrowserDialog{Description="选择本地音乐文件夹",ShowNewFolderButton=false})
                {if(dialog.ShowDialog(owner)==DialogResult.OK){selected=dialog.SelectedPath;result=0;}}
            }
            else if(args[0]=="file")
            {
                using(var dialog=new OpenFileDialog{Title="选择椒盐歌单或本地歌曲",Filter="歌单或音频|*.txt;*.m3u;*.m3u8;*.mp3;*.flac;*.wav;*.m4a;*.aac;*.wma|所有文件|*.*",CheckFileExists=true,Multiselect=false,RestoreDirectory=true})
                {if(dialog.ShowDialog(owner)==DialogResult.OK){selected=dialog.FileName;result=0;}}
            }
            }catch{result=1;}finally{owner.Close();}
            };
            Application.Run(owner);
            }
            if(result==0)Console.Write(Convert.ToBase64String(Encoding.UTF8.GetBytes(selected)));return result;
        }
        catch{return 1;}
    }
}
