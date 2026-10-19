// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace ChillNeteasePlaylist {
    // Draw in the icon rectangle, avoiding font baseline and glyph-size offsets.
    public sealed class NativeHeartGraphic : MaskableGraphic {
        bool filled;
        public bool Filled { get{return filled;} set{if(filled!=value){filled=value;SetVerticesDirty();}} }
        static Vector2 Curve(Vector2 a,Vector2 b,Vector2 c,Vector2 d,float t){float s=1-t;return s*s*s*a+3*s*s*t*b+3*s*t*t*c+t*t*t*d;}
        protected override void OnPopulateMesh(VertexHelper mesh){
            mesh.Clear();var points=new List<Vector2>();
            Vector2[] control={new Vector2(0,-0.48f),new Vector2(-0.16f,-0.32f),new Vector2(-0.49f,-0.07f),new Vector2(-0.47f,0.20f),new Vector2(-0.45f,0.49f),new Vector2(-0.13f,0.56f),new Vector2(0,0.28f),new Vector2(0.13f,0.56f),new Vector2(0.45f,0.49f),new Vector2(0.47f,0.20f),new Vector2(0.49f,-0.07f),new Vector2(0.16f,-0.32f),new Vector2(0,-0.48f)};
            // Native transport sprites have transparent padding around their marks.
            Rect rect=rectTransform.rect;float size=Mathf.Min(rect.width,rect.height)*0.78f;
            for(int segment=0;segment<4;segment++)for(int i=0;i<12;i++){Vector2 p=Curve(control[segment*3],control[segment*3+1],control[segment*3+2],control[segment*3+3],i/12f);points.Add(rect.center+p*size);}
            if(filled){mesh.AddVert(rect.center+new Vector2(0,-0.03f)*size,color,Vector2.zero);foreach(var point in points)mesh.AddVert(point,color,Vector2.zero);for(int i=0;i<points.Count;i++)mesh.AddTriangle(0,i+1,(i+1)%points.Count+1);}
            else{float half=Mathf.Max(0.7f,size*0.045f);for(int i=0;i<points.Count;i++){Vector2 p=points[i],q=points[(i+1)%points.Count],direction=(q-p).normalized,normal=new Vector2(-direction.y,direction.x)*half;int index=mesh.currentVertCount;mesh.AddVert(p+normal,color,Vector2.zero);mesh.AddVert(p-normal,color,Vector2.zero);mesh.AddVert(q-normal,color,Vector2.zero);mesh.AddVert(q+normal,color,Vector2.zero);mesh.AddTriangle(index,index+1,index+2);mesh.AddTriangle(index,index+2,index+3);}}
        }
    }
}
