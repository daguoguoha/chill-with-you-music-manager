// SPDX-License-Identifier: GPL-3.0-or-later
// Test renderer for the production IMGUI layout. Not a Unity runtime or game screenshot.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
namespace Newtonsoft.Json {public sealed class JsonPropertyAttribute:Attribute{public JsonPropertyAttribute(string name){}}}
namespace UnityEngine
{
    public class Object {public HideFlags hideFlags;public static void Destroy(Object o){var d=o as IDisposable;if(d!=null)d.Dispose();}}
    public enum HideFlags{HideAndDontSave} public enum TextureFormat{RGBA32} public enum FilterMode{Bilinear}
    public enum FontStyle{Normal,Bold} public enum TextAnchor{MiddleLeft,MiddleCenter,MiddleRight,UpperLeft}
    public enum TextClipping{Clip} public enum ScaleMode{ScaleToFit}
    public struct Color
    {
        public float r,g,b,a;public Color(float r,float g,float b,float a=1){this.r=r;this.g=g;this.b=b;this.a=a;}
        public static Color Lerp(Color x,Color y,float t){return new Color(x.r+(y.r-x.r)*t,x.g+(y.g-x.g)*t,x.b+(y.b-x.b)*t,x.a+(y.a-x.a)*t);}
        public System.Drawing.Color Drawing{get{return System.Drawing.Color.FromArgb((int)(Mathf.Clamp01(a)*255),(int)(Mathf.Clamp01(r)*255),(int)(Mathf.Clamp01(g)*255),(int)(Mathf.Clamp01(b)*255));}}
    }
    public static class Mathf
    {
        public static float Max(float a,float b){return Math.Max(a,b);}public static int Max(int a,int b){return Math.Max(a,b);}
        public static float Min(float a,float b){return Math.Min(a,b);}public static int Min(int a,int b){return Math.Min(a,b);}
        public static float Abs(float a){return Math.Abs(a);}public static float Sqrt(float a){return (float)Math.Sqrt(a);}
        public static float Clamp01(float a){return Math.Max(0,Math.Min(1,a));}public static float Clamp(float a,float l,float h){return Math.Max(l,Math.Min(h,a));}
    }
    public struct Rect{public float x,y,width,height;public Rect(float x,float y,float w,float h){this.x=x;this.y=y;width=w;height=h;}public RectangleF Drawing{get{return new RectangleF(x,y,width,height);}}}
    public class RectOffset{public int left,right,top,bottom;public RectOffset(){}public RectOffset(int l,int r,int t,int b){left=l;right=r;top=t;bottom=b;}}
    public struct Vector2{public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}public static Vector2 zero{get{return new Vector2();}}}
    public struct Vector3{public Vector3(float x,float y,float z){}public static Vector3 zero{get{return new Vector3();}}}
    public struct Quaternion{public static Quaternion identity{get{return new Quaternion();}}}
    public struct Matrix4x4{public static Matrix4x4 TRS(Vector3 a,Quaternion b,Vector3 c){return new Matrix4x4();}}
    public static class Screen{public static int width=1600,height=900;}public static class Time{public static float realtimeSinceStartup=20;}
    public class Font:Object{public static Font CreateDynamicFontFromOSFont(string[] names,int size){return new Font();}}
    public class Texture2D:Object,IDisposable
    {
        internal Bitmap Image;public FilterMode filterMode;
        public Texture2D(int w,int h,TextureFormat f,bool mip){Image=new Bitmap(w,h);}
        public void SetPixel(int x,int y,Color c){Image.SetPixel(x,Image.Height-1-y,c.Drawing);}public void Apply(){}public void Dispose(){Image.Dispose();}
    }
    public class GUIContent{public string text,tooltip;public GUIContent(string t){text=t;}public GUIContent(string t,string tip){text=t;tooltip=tip;}}
    public class GUIStyleState{public Texture2D background;public Color textColor=new Color(1,1,1);}
    public class GUIStyle
    {
        public Font font;public int fontSize=15;public FontStyle fontStyle;public TextAnchor alignment;public TextClipping clipping;public bool wordWrap;
        public float fixedWidth,fixedHeight;public RectOffset border=new RectOffset(),padding=new RectOffset(),margin=new RectOffset();
        public GUIStyleState normal=new GUIStyleState(),onNormal=new GUIStyleState(),focused=new GUIStyleState(),onFocused=new GUIStyleState(),hover=new GUIStyleState(),onHover=new GUIStyleState(),active=new GUIStyleState(),onActive=new GUIStyleState();
        public GUIStyle(){}public GUIStyle(GUIStyle s){font=s.font;fontSize=s.fontSize;}
        internal System.Drawing.Font DrawingFont(){return new System.Drawing.Font("Microsoft YaHei UI",Math.Max(1,fontSize),fontStyle==FontStyle.Bold?System.Drawing.FontStyle.Bold:System.Drawing.FontStyle.Regular,GraphicsUnit.Pixel);}
        public Vector2 CalcSize(GUIContent c){using(var f=DrawingFont()){var s=GUI.G.MeasureString(c.text??"",f,100000,StringFormat.GenericTypographic);return new Vector2(s.Width,s.Height);}}
        public float CalcHeight(GUIContent c,float width){using(var f=DrawingFont()){return GUI.G.MeasureString(c.text??"",f,(int)width,StringFormat.GenericTypographic).Height;}}
    }
    public class GUISkin:Object
    {
        public Font font;public GUIStyle textField=new GUIStyle(),verticalScrollbar,verticalScrollbarThumb,verticalScrollbarUpButton,verticalScrollbarDownButton,horizontalSlider,horizontalSliderThumb;
    }
    public class AudioSource{public object clip;public void Pause(){}public void UnPause(){}}
    public static class GUI
    {
        public static Graphics G;public static GUISkin skin=new GUISkin();public static Matrix4x4 matrix;public static int depth;public static bool enabled=true;
        static string focused="";static Stack<GraphicsState> stack=new Stack<GraphicsState>();
        public static List<Rect> Controls=new List<Rect>();
        public static void SetNextControlName(string n){}public static string GetNameOfFocusedControl(){return focused;}
        static void Background(Rect r,GUIStyle s)
        {
            if(s==null||s.normal.background==null)return;
            Bitmap b=s.normal.background.Image;int l=s.border.left,rr=s.border.right,t=s.border.top,bot=s.border.bottom;
            if(l+rr==0||t+bot==0){G.DrawImage(b,r.Drawing);return;}
            float[] sx={0,l,b.Width-rr,b.Width},sy={0,t,b.Height-bot,b.Height};
            float[] dx={r.x,r.x+l,r.x+r.width-rr,r.x+r.width},dy={r.y,r.y+t,r.y+r.height-bot,r.y+r.height};
            for(int y=0;y<3;y++)for(int x=0;x<3;x++)G.DrawImage(b,new RectangleF(dx[x],dy[y],dx[x+1]-dx[x],dy[y+1]-dy[y]),new RectangleF(sx[x],sy[y],sx[x+1]-sx[x],sy[y+1]-sy[y]),GraphicsUnit.Pixel);
        }
        public static void Box(Rect r,string text,GUIStyle s){Background(r,s);if(text.Length>0)Label(r,text,s);}
        public static void Label(Rect r,string text,GUIStyle s){Label(r,new GUIContent(text),s);}
        public static void Label(Rect r,GUIContent c,GUIStyle s)
        {
            using(var f=s.DrawingFont())using(var brush=new SolidBrush(s.normal.textColor.Drawing))using(var format=new StringFormat(StringFormat.GenericTypographic))
            {
                format.Alignment=s.alignment==TextAnchor.MiddleCenter?StringAlignment.Center:s.alignment==TextAnchor.MiddleRight?StringAlignment.Far:StringAlignment.Near;
                format.LineAlignment=s.alignment==TextAnchor.UpperLeft?StringAlignment.Near:StringAlignment.Center;
                if(!s.wordWrap)format.FormatFlags|=StringFormatFlags.NoWrap;
                format.Trimming=StringTrimming.EllipsisCharacter;
                var state=G.Save();G.SetClip(r.Drawing,CombineMode.Intersect);G.DrawString(c.text??"",f,brush,r.Drawing,format);G.Restore(state);
            }
        }
        public static bool Button(Rect r,string text,GUIStyle s){return Button(r,new GUIContent(text),s);}
        public static bool Button(Rect r,GUIContent c,GUIStyle s){Controls.Add(r);Background(r,s);Label(r,c,s);return false;}
        public static string TextField(Rect r,string value,GUIStyle s){Controls.Add(r);Background(r,s);Label(new Rect(r.x+12,r.y,r.width-24,r.height),value,s);return value;}
        public static void DrawTexture(Rect r,Texture2D t){G.DrawImage(t.Image,r.Drawing);}public static void DrawTexture(Rect r,Texture2D t,ScaleMode m){DrawTexture(r,t);}
        public static Vector2 BeginScrollView(Rect r,Vector2 scroll,Rect content,bool a=false,bool b=false){stack.Push(G.Save());G.SetClip(r.Drawing,CombineMode.Intersect);G.TranslateTransform(r.x-scroll.x,r.y-scroll.y);return scroll;}
        public static void EndScrollView(){G.Restore(stack.Pop());}
        public static Rect Window(int id,Rect r,Action<int> draw,string text,GUIStyle s){Background(r,s);draw(id);return r;}
        public static void DragWindow(Rect r){}
    }
}
