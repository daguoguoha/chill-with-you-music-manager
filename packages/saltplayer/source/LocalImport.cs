// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using UnityEngine;
namespace ChillSaltPlayerPlaylist
{
    public sealed partial class Plugin
    {
        string musicRoot="",importReport="";
        Vector2 importScroll;
        void ImportLocal()
        {
            string source=playlistLink,root=musicRoot;status="正在导入本地音乐…";
            Run(delegate{return Bridge.Import(source,root);},delegate(object value)
            {
                var result=(ImportResult)value;playlists=Bridge.Lists();songs=result.playlist.songs;selectedPlaylistId=result.playlist.id;selectedPlaylistTitle=result.playlist.name;
                query="";songScroll=Vector2.zero;nativeMenuVersion=-1;showImport=false;importReport=Bridge.LastImportDetails;status=result.Summary;RegisterNativePlaylist();
            });
        }
        void ChooseFile()
        {status="请选择歌单或歌曲，选择窗口可取消。";Run(delegate{return FilePicker.Select(false);},delegate(object value){if(value!=null)playlistLink=(string)value;status="选择完成，可开始导入。";});}
        void ChooseFolder(bool matching)
        {status="请选择音乐目录，选择窗口可取消。";Run(delegate{return FilePicker.Select(true);},delegate(object value){if(value!=null){if(matching)musicRoot=(string)value;else playlistLink=(string)value;}status="选择完成，可开始导入。";});}
        void DrawImport()
        {
            GUI.Label(new Rect(286,100,680,36),"导入椒盐歌单和本地音乐",headingText);
            GUI.Label(new Rect(286,141,680,25),"TXT 为每行一个文件路径；也支持 M3U/M3U8。",mutedText);
            GUI.Label(new Rect(286,185,680,26),"歌单文件、单首歌曲或音乐文件夹",bodyText);
            playlistLink=Input(new Rect(286,219,684,38),playlistLink,"source-path",@"D:\Music\我的歌单.txt");
            GUI.enabled=ready && !busy;
            if(GUI.Button(new Rect(286,267,150,34),"选择歌单 / 歌曲",buttonStyle))ChooseFile();
            if(GUI.Button(new Rect(446,267,150,34),"选择音乐文件夹",buttonStyle))ChooseFolder(false);
            GUI.enabled=true;
            GUI.Label(new Rect(286,316,684,26),"路径失效时的电脑音乐目录（可选）",bodyText);
            musicRoot=Input(new Rect(286,352,524,38),musicRoot,"music-root",@"手机歌单请先把对应歌曲复制到电脑，再选择目录");
            GUI.enabled=ready && !busy;
            if(GUI.Button(new Rect(822,352,148,38),"选择匹配目录",buttonStyle))ChooseFolder(true);
            if(GUI.Button(new Rect(286,410,144,40),busy?"正在导入…":"开始导入",primaryButton))ImportLocal();
            GUI.enabled=true;
            if(GUI.Button(new Rect(445,410,100,40),"返回",quietButton))showImport=false;
            GUI.Label(new Rect(286,465,680,24),"顺序和重复曲目保留；同名无法确定时会提示。",mutedText);
            string report=string.IsNullOrEmpty(importReport)?"MP3 / FLAC / WAV / M4A / AAC / WMA；不读取网络音源。":importReport;
            float height=Mathf.Max(57,mutedText.CalcHeight(new GUIContent(report),650));
            importScroll=GUI.BeginScrollView(new Rect(286,496,684,69),importScroll,new Rect(0,0,657,height));
            GUI.Label(new Rect(0,0,650,height),report,statusText);GUI.EndScrollView();
        }
    }
}
