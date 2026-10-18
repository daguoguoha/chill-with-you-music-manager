// SPDX-License-Identifier: GPL-3.0-or-later
// Original procedural theme. No game artwork, fonts or sprites are redistributed.
using UnityEngine;
namespace ChillBilibiliPlaylist
{
    internal static class GameTheme
    {
        internal static readonly Color Ink=new Color(0.94f,0.92f,0.98f,1);
        internal static readonly Color Muted=new Color(0.70f,0.68f,0.78f,1);
        internal static readonly Color Accent=new Color(0.86f,0.82f,0.94f,1);
        internal static readonly Color Window=new Color(0.09f,0.09f,0.15f,0.94f);
        internal static readonly Color Sidebar=new Color(0.14f,0.13f,0.20f,0.78f);
        internal static readonly Color Card=new Color(0.16f,0.15f,0.23f,0.82f);
        internal static readonly Color Selected=new Color(0.31f,0.28f,0.40f,0.76f);
        internal static readonly Color Hover=new Color(0.30f,0.27f,0.39f,0.72f);
        internal static readonly Color Button=new Color(0.19f,0.18f,0.27f,0.66f);
        internal static readonly Color Input=new Color(0.13f,0.13f,0.21f,0.88f);
        internal static readonly Color Focused=new Color(0.23f,0.21f,0.32f,0.92f);
        internal static Texture2D Surface(Color fill,bool rounded)
        {
            int size=rounded?32:1;
            Texture2D texture=new Texture2D(size,size,TextureFormat.RGBA32,false);
            texture.hideFlags=HideFlags.HideAndDontSave;
            Color edge=new Color(0.59f,0.55f,0.70f,Mathf.Min(0.58f,fill.a));
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                Color color=fill;
                if(rounded)
                {
                    // Signed distance to a rounded 32px surface, including antialiased 1px keyline.
                    float qx=Mathf.Abs(x-15.5f)-8.5f,qy=Mathf.Abs(y-15.5f)-8.5f;
                    float distance=Mathf.Sqrt(Mathf.Max(qx,0)*Mathf.Max(qx,0)+Mathf.Max(qy,0)*Mathf.Max(qy,0))+Mathf.Min(Mathf.Max(qx,qy),0)-6.5f;
                    float coverage=Mathf.Clamp01(0.5f-distance);
                    if(distance>-1.1f && fill.a>0.01f)color=Color.Lerp(fill,edge,Mathf.Clamp01(distance+1.1f));
                    color.a*=coverage;
                }
                texture.SetPixel(x,y,color);
            }
            texture.Apply();texture.filterMode=FilterMode.Bilinear;return texture;
        }
        internal static Texture2D Emblem()
        {
            const int size=64;
            Texture2D texture=new Texture2D(size,size,TextureFormat.RGBA32,false);
            texture.hideFlags=HideFlags.HideAndDontSave;
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float dx=x-31.5f,dy=y-31.5f,d=Mathf.Sqrt(dx*dx+dy*dy);
                Color color=Window;
                if(d>27)color=dx<0?new Color(0.39f,0.78f,0.85f,0.84f):new Color(0.87f,0.36f,0.66f,0.84f);
                if(d>28.6f && d<30)color.a*=0.35f;
                color.a*=Mathf.Clamp01(31.3f-d);
                texture.SetPixel(x,y,color);
            }
            texture.Apply();texture.filterMode=FilterMode.Bilinear;return texture;
        }
    }
}
