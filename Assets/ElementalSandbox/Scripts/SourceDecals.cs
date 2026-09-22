using System.Collections.Generic;
using UnityEngine;
namespace ElementalSandbox
{
    // GroundDecals.js — the pooled floor marks every cast leaves: scorch,
    // ripple, crack, shockwave, dust ring, foam, frost, arc. One quad and one
    // material per live decal, aged by the app each frame, released at 1.
    public sealed class SourceDecals:System.IDisposable
    {
        public enum Kind{Scorch=0,Ripple=1,Crack=2,Shockwave=3,DustRing=4,Foam=5,Frost=6,Arc=7}
        sealed class Decal{public Transform quad;public Material material;public float age,life,radius,growth;public bool live;}
        readonly Transform root;readonly Shader shader;readonly Mesh quadMesh;
        readonly List<Decal> pool=new List<Decal>();
        // The hot kinds add light to the floor; dust, foam and frost sit on it.
        static bool Additive(Kind k)=>k==Kind.Scorch||k==Kind.Ripple||k==Kind.Crack||k==Kind.Shockwave||k==Kind.Arc;
        public SourceDecals(Transform parent)
        {
            root=new GameObject("Ground decals").transform;root.SetParent(parent,false);
            shader=Shader.Find("Elemental/GroundDecal");
            var q=GameObject.CreatePrimitive(PrimitiveType.Quad);quadMesh=q.GetComponent<MeshFilter>().sharedMesh;Object.Destroy(q);
        }
        Decal Acquire()
        {
            foreach(var d in pool)if(!d.live)return d;
            var g=new GameObject("Decal",typeof(MeshFilter),typeof(MeshRenderer));g.transform.SetParent(root,false);
            g.GetComponent<MeshFilter>().sharedMesh=quadMesh;var r=g.GetComponent<MeshRenderer>();
            var m=new Material(shader);r.sharedMaterial=m;r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;r.receiveShadows=false;
            var d2=new Decal{quad=g.transform,material=m};pool.Add(d2);return d2;
        }
        public void Spawn(Kind kind,Vector3 position,float radius=2,float life=2,Color? colorA=null,Color? colorB=null,float intensity=1,float width=.12f,float growth=0,float height=.035f)
        {
            var d=Acquire();d.live=true;d.age=0;d.life=Mathf.Max(.05f,life);d.radius=radius;d.growth=growth;
            var m=d.material;m.SetFloat("_Type",(int)kind);m.SetFloat("_Age",0);m.SetFloat("_Seed",Random.value);m.SetFloat("_Intensity",intensity);m.SetFloat("_Width",width);m.SetFloat("_Radius",radius);bool add=Additive(kind);m.SetFloat("_Additive",add?1:0);m.SetInt("_SrcBlend",add?(int)UnityEngine.Rendering.BlendMode.One:(int)UnityEngine.Rendering.BlendMode.SrcAlpha);m.SetInt("_DstBlend",add?(int)UnityEngine.Rendering.BlendMode.One:(int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if(colorA.HasValue)m.SetColor("_ColorA",colorA.Value);if(colorB.HasValue)m.SetColor("_ColorB",colorB.Value);
            // The source's PlaneGeometry is rotated flat and yawed at random.
            d.quad.position=new Vector3(position.x,height,position.z);d.quad.rotation=Quaternion.Euler(90,Random.value*360,0);d.quad.localScale=Vector3.one*radius*2;
            d.quad.gameObject.SetActive(true);
        }
        public void Tick(float dt)
        {
            foreach(var d in pool){if(!d.live)continue;d.age+=dt;float t=d.age/d.life;d.material.SetFloat("_Age",t);
                if(d.growth!=0)d.quad.localScale=Vector3.one*d.radius*2*(1+d.growth*t);
                if(t>=1){d.live=false;d.quad.gameObject.SetActive(false);}}
        }
        public void Clear(){foreach(var d in pool){d.live=false;d.quad.gameObject.SetActive(false);}}
        public void Dispose(){foreach(var d in pool)if(d.material!=null)Object.Destroy(d.material);if(root!=null)Object.Destroy(root.gameObject);}
    }
}
