using System.Collections.Generic;
using UnityEngine;
namespace ElementalSandbox
{
    // Clip a baked skinned mesh against a horizontal plane, retaining both halves.
    public static class ElementalMeshSlice
    {
        struct Vertex { public Vector3 p,n;public Vector2 uv;public static Vertex Lerp(Vertex a,Vertex b,float t)=>new Vertex{p=Vector3.Lerp(a.p,b.p,t),n=Vector3.Lerp(a.n,b.n,t).normalized,uv=Vector2.Lerp(a.uv,b.uv,t)}; }
        public static Mesh Half(Mesh source,Transform transform,float height,bool upper,Vector3 origin)
        {
            var vertices=source.vertices;var normals=source.normals;var uvs=source.uv;var triangles=source.triangles;var output=new List<Vertex>();var indices=new List<int>();var cuts=new List<Vertex>();
            for(int i=0;i<triangles.Length;i+=3){var polygon=new List<Vertex>(4);for(int j=0;j<3;j++){int k=triangles[i+j];polygon.Add(new Vertex{p=transform.TransformPoint(vertices[k]),n=normals.Length>k?transform.TransformDirection(normals[k]).normalized:Vector3.up,uv=uvs.Length>k?uvs[k]:Vector2.zero});}var clipped=new List<Vertex>(4);for(int j=0;j<polygon.Count;j++){Vertex a=polygon[j],b=polygon[(j+1)%polygon.Count];float da=(a.p.y-height)*(upper?1:-1),db=(b.p.y-height)*(upper?1:-1);if(da>=0)clipped.Add(a);if((da>=0)!=(db>=0)){Vertex intersection=Vertex.Lerp(a,b,da/(da-db));clipped.Add(intersection);bool unique=true;foreach(var c in cuts)if((c.p-intersection.p).sqrMagnitude<.000001f){unique=false;break;}if(unique)cuts.Add(intersection);}}if(clipped.Count<3)continue;int start=output.Count;output.AddRange(clipped);for(int j=1;j<clipped.Count-1;j++){indices.Add(start);indices.Add(start+j);indices.Add(start+j+1);}}
            if(cuts.Count>=3){Vector3 center=Vector3.zero;foreach(var v in cuts)center+=v.p;center/=cuts.Count;cuts.Sort((a,b)=>Mathf.Atan2(a.p.z-center.z,a.p.x-center.x).CompareTo(Mathf.Atan2(b.p.z-center.z,b.p.x-center.x)));Vector3 normal=upper?Vector3.down:Vector3.up;int start=output.Count;output.Add(new Vertex{p=center,n=normal,uv=Vector2.one*.5f});foreach(var c in cuts)output.Add(new Vertex{p=c.p,n=normal,uv=new Vector2(c.p.x-center.x,c.p.z-center.z)});for(int i=0;i<cuts.Count;i++){indices.Add(start);indices.Add(start+1+(upper?i:(i+1)%cuts.Count));indices.Add(start+1+(upper?(i+1)%cuts.Count:i));}}
            var p=new Vector3[output.Count];var n=new Vector3[output.Count];var uv=new Vector2[output.Count];for(int i=0;i<output.Count;i++){p[i]=output[i].p-origin;n[i]=output[i].n;uv[i]=output[i].uv;}var mesh=new Mesh{name=upper?"Severed upper body":"Severed lower body",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32,vertices=p,normals=n,uv=uv,triangles=indices.ToArray()};mesh.RecalculateBounds();return mesh;
        }
    }
}
