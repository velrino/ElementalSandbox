using System.Collections.Generic;
using UnityEngine;
namespace ElementalSandbox
{
    // Clip a baked skinned mesh against a horizontal plane, retaining both halves.
    public static class ElementalMeshSlice
    {
        struct Vertex { public Vector3 p,n;public Vector2 uv;public static Vertex Lerp(Vertex a,Vertex b,float t)=>new Vertex{p=Vector3.Lerp(a.p,b.p,t),n=Vector3.Lerp(a.n,b.n,t).normalized,uv=Vector2.Lerp(a.uv,b.uv,t)}; }

        const float WELD=.0001f;

        // Chain the cut segments into closed rings.
        //
        // A humanoid cut at the waist does not leave one loop: the torso is one,
        // each arm crossing the plane is another, and the clothing shells are
        // more. Fanning every intersection point around a single shared centroid
        // — which is what sorting them all by angle amounts to — stitches those
        // rings to each other, and the triangles that span the gaps between them
        // are metres long. One of them is the bright wedge that has been filling
        // these frames. Each ring has to be closed and capped on its own.
        static List<List<Vertex>> Rings(List<Vertex> a,List<Vertex> b)
        {
            var rings=new List<List<Vertex>>();
            var used=new bool[a.Count];
            for(int s=0;s<a.Count;s++)
            {
                if(used[s])continue;
                used[s]=true;
                var ring=new List<Vertex>{a[s],b[s]};
                // Walk forward from the open end until the ring closes or runs out.
                for(int guard=0;guard<a.Count;guard++)
                {
                    Vector3 tail=ring[ring.Count-1].p;
                    if((tail-ring[0].p).sqrMagnitude<WELD)break;
                    int next=-1;bool flip=false;
                    for(int i=0;i<a.Count;i++)
                    {
                        if(used[i])continue;
                        if((a[i].p-tail).sqrMagnitude<WELD){next=i;flip=false;break;}
                        if((b[i].p-tail).sqrMagnitude<WELD){next=i;flip=true;break;}
                    }
                    if(next<0)break;
                    used[next]=true;
                    ring.Add(flip?a[next]:b[next]);
                }
                if(ring.Count>=3)rings.Add(ring);
            }
            return rings;
        }

        public static Mesh Half(Mesh source,Transform transform,float height,bool upper,Vector3 origin)
        {
            // `source` is a BakeMesh snapshot, which already carries the rig's
            // scale: this dummy's skin has a lossyScale of ~92, its shared mesh
            // is 0.02 units tall, and the snapshot comes back 1.7 units tall —
            // world size. Running it back through TransformPoint applied that
            // ~92 a second time, which is what turned a severed body into a
            // 145-metre slab lying across the stage. The snapshot therefore
            // needs the renderer's rotation and position and nothing else.
            var toWorld=Matrix4x4.TRS(transform.position,transform.rotation,Vector3.one);
            var vertices=source.vertices;var normals=source.normals;var uvs=source.uv;var triangles=source.triangles;
            var output=new List<Vertex>();var indices=new List<int>();
            // Cut segments, one per crossing triangle, kept as ordered pairs so
            // the rings can be chained afterwards.
            var cutA=new List<Vertex>();var cutB=new List<Vertex>();
            for(int i=0;i<triangles.Length;i+=3)
            {
                var polygon=new List<Vertex>(4);
                for(int j=0;j<3;j++){int k=triangles[i+j];polygon.Add(new Vertex{p=toWorld.MultiplyPoint3x4(vertices[k]),n=normals.Length>k?toWorld.MultiplyVector(normals[k]).normalized:Vector3.up,uv=uvs.Length>k?uvs[k]:Vector2.zero});}
                var clipped=new List<Vertex>(4);var crossing=new List<Vertex>(2);
                for(int j=0;j<polygon.Count;j++)
                {
                    Vertex a=polygon[j],b=polygon[(j+1)%polygon.Count];
                    float da=(a.p.y-height)*(upper?1:-1),db=(b.p.y-height)*(upper?1:-1);
                    if(da>=0)clipped.Add(a);
                    if((da>=0)!=(db>=0)){Vertex intersection=Vertex.Lerp(a,b,da/(da-db));clipped.Add(intersection);crossing.Add(intersection);}
                }
                if(crossing.Count==2){cutA.Add(crossing[0]);cutB.Add(crossing[1]);}
                if(clipped.Count<3)continue;
                int start=output.Count;output.AddRange(clipped);
                for(int j=1;j<clipped.Count-1;j++){indices.Add(start);indices.Add(start+j);indices.Add(start+j+1);}
            }

            // Cap each ring on its own centroid.
            Vector3 normal=upper?Vector3.down:Vector3.up;
            foreach(var ring in Rings(cutA,cutB))
            {
                Vector3 center=Vector3.zero;foreach(var v in ring)center+=v.p;center/=ring.Count;
                int start=output.Count;
                output.Add(new Vertex{p=center,n=normal,uv=Vector2.one*.5f});
                foreach(var c in ring)output.Add(new Vertex{p=c.p,n=normal,uv=new Vector2(c.p.x-center.x,c.p.z-center.z)});
                for(int i=0;i<ring.Count;i++){indices.Add(start);indices.Add(start+1+(upper?i:(i+1)%ring.Count));indices.Add(start+1+(upper?(i+1)%ring.Count:i));}
            }

            var p=new Vector3[output.Count];var n=new Vector3[output.Count];var uv=new Vector2[output.Count];
            for(int i=0;i<output.Count;i++){p[i]=output[i].p-origin;n[i]=output[i].n;uv[i]=output[i].uv;}
            var mesh=new Mesh{name=upper?"Severed upper body":"Severed lower body",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32,vertices=p,normals=n,uv=uv,triangles=indices.ToArray()};
            mesh.RecalculateBounds();return mesh;
        }
    }
}
