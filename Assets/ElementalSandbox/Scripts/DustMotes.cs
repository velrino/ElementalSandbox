using UnityEngine;
namespace ElementalSandbox
{
    // DustMotes.js — 2,600 motes of ambient dust on a single draw call.
    //
    // The source seeds them once into a 46 x 14 x 46 volume and never touches
    // the positions again; drift, curl, twinkle and wrapping all live in the
    // vertex shader. This keeps that split: the mesh is built once, and the
    // only per-frame work is moving the volume onto the action.
    public sealed class DustMotes
    {
        const int Count=2600;
        static readonly Vector3 Volume=new Vector3(46,14,46);
        readonly Transform root;
        readonly Material material;
        readonly Mesh mesh;
        readonly MeshRenderer renderer;

        public DustMotes(Transform parent)
        {
            var go=new GameObject("Dust motes");root=go.transform;root.SetParent(parent);
            mesh=Build();
            material=new Material(Shader.Find("Elemental/Dust")){name="Dust motes"};
            go.AddComponent<MeshFilter>().sharedMesh=mesh;
            renderer=go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial=material;
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows=false;
            renderer.lightProbeUsage=UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage=UnityEngine.Rendering.ReflectionProbeUsage.Off;
        }

        // Four verts per mote: the shader turns each quad into a camera-facing
        // billboard. uv is the corner, texcoord1 carries (seed, volume height).
        static Mesh Build()
        {
            var positions=new Vector3[Count*4];var uv=new Vector2[Count*4];var seeds=new Vector2[Count*4];var indices=new int[Count*6];
            var random=new System.Random(20260921);
            float R()=>(float)random.NextDouble();
            for(int i=0;i<Count;i++)
            {
                var p=new Vector3((R()-.5f)*Volume.x,R()*Volume.y,(R()-.5f)*Volume.z);
                float seed=R();int v=i*4,t=i*6;
                for(int c=0;c<4;c++){positions[v+c]=p;seeds[v+c]=new Vector2(seed,Volume.y);}
                uv[v]=new Vector2(0,0);uv[v+1]=new Vector2(1,0);uv[v+2]=new Vector2(1,1);uv[v+3]=new Vector2(0,1);
                indices[t]=v;indices[t+1]=v+1;indices[t+2]=v+2;indices[t+3]=v;indices[t+4]=v+2;indices[t+5]=v+3;
            }
            var m=new Mesh{name="Dust motes",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
            m.vertices=positions;m.uv=uv;m.uv2=seeds;m.triangles=indices;
            // The vertex shader displaces well outside the seeded box, and the
            // volume follows the action, so culling is not worth its risk here.
            m.bounds=new Bounds(Vector3.zero,Volume*3);
            return m;
        }

        public void Update(float time,Vector3 anchor,float amount)
        {
            bool visible=amount>.001f;
            renderer.enabled=visible;
            if(!visible)return;
            root.position=new Vector3(anchor.x,0,anchor.z);
            material.SetFloat("_Amount",amount);
            // gl_PointSize is in pixels; this is the pixels-to-world-units
            // factor at one metre, so the motes keep the source's apparent size
            // whatever the window is doing.
            var camera=Camera.main;
            float height=Mathf.Max(1,camera!=null?camera.pixelHeight:Screen.height);
            float fov=camera!=null?camera.fieldOfView:46;
            material.SetFloat("_Size",2*Mathf.Tan(fov*.5f*Mathf.Deg2Rad)/height);
        }

        public void Dispose()
        {
            if(material!=null)Object.Destroy(material);
            if(mesh!=null)Object.Destroy(mesh);
            if(root!=null)Object.Destroy(root.gameObject);
        }
    }
}
