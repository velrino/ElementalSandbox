using UnityEngine;
using UnityEngine.Rendering;
namespace ElementalSandbox
{
    // ContactShadows.js — the tight darkening under the caster's feet that a
    // shadow map cannot resolve.
    //
    // Same trick and same numbers as the source: render the character's depth
    // from below into a 256px target, blur it twice, project it on the floor.
    // The source pins its ortho camera to a dedicated layer so the floor cannot
    // occlude itself; here the draw list is explicit instead — the caster's own
    // renderers, submitted through a command buffer — which needs no layer and
    // cannot pick anything else up.
    public sealed class ContactShadows
    {
        const int Resolution=256;
        const float Size=5.5f,Height=3.2f,BlurAmount=2.4f;
        readonly Transform root;
        readonly Material material;
        readonly Mesh quad;
        readonly MeshRenderer catcher;
        readonly RenderTexture target,scratch;
        readonly Camera shadowCamera;
        readonly CommandBuffer commands=new CommandBuffer{name="Contact shadow"};
        Renderer[] casters=System.Array.Empty<Renderer>();
        Transform caster;
        float refreshed=-1;

        public ContactShadows(Transform parent)
        {
            var go=new GameObject("Contact shadow");root=go.transform;root.SetParent(parent);root.position=new Vector3(0,.015f,0);

            target=New();scratch=New();
            quad=BuildQuad();
            material=new Material(Shader.Find("Elemental/Contact")){name="Contact shadow"};
            material.SetTexture("_MainTex",target);
            material.SetFloat("_Height",Height);
            material.SetFloat("_Darkness",.8f);

            go.AddComponent<MeshFilter>().sharedMesh=quad;
            catcher=go.AddComponent<MeshRenderer>();
            catcher.sharedMaterial=material;
            catcher.shadowCastingMode=ShadowCastingMode.Off;
            catcher.receiveShadows=false;
            catcher.lightProbeUsage=LightProbeUsage.Off;
            catcher.reflectionProbeUsage=ReflectionProbeUsage.Off;

            var cameraObject=new GameObject("Contact shadow camera");cameraObject.transform.SetParent(root);
            // Looking straight up from the floor, over a 0..Height slab.
            cameraObject.transform.localPosition=Vector3.zero;cameraObject.transform.localRotation=Quaternion.Euler(-90,0,0);
            shadowCamera=cameraObject.AddComponent<Camera>();
            shadowCamera.orthographic=true;shadowCamera.orthographicSize=Size*.5f;
            shadowCamera.nearClipPlane=0;shadowCamera.farClipPlane=Height;shadowCamera.aspect=1;
            // Never rendered by the pipeline: it exists only for its matrices.
            shadowCamera.enabled=false;
        }

        static RenderTexture New()
        {
            var rt=new RenderTexture(Resolution,Resolution,0,RenderTextureFormat.ARGB32){useMipMap=false,autoGenerateMips=false,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            rt.Create();return rt;
        }

        // The camera's screen X is world +X and its screen Y is world -Z, so the
        // catcher samples the target with that mapping rather than a flip.
        static Mesh BuildQuad()
        {
            float h=Size*.5f;
            var m=new Mesh{name="Contact catcher"};
            m.vertices=new[]{new Vector3(-h,0,-h),new Vector3(h,0,-h),new Vector3(h,0,h),new Vector3(-h,0,h)};
            m.uv=new[]{new Vector2(0,1),new Vector2(1,1),new Vector2(1,0),new Vector2(0,0)};
            m.triangles=new[]{0,1,2,0,2,3};
            m.RecalculateNormals();m.bounds=new Bounds(Vector3.zero,new Vector3(Size,.1f,Size));
            return m;
        }

        public void SetCaster(Transform value)
        {
            caster=value;
            casters=value==null?System.Array.Empty<Renderer>():value.GetComponentsInChildren<Renderer>();
            refreshed=-1;
        }

        public void Update(float strength)
        {
            // Opacity is not throttled — the editor slider has to answer at once.
            material.SetFloat("_Opacity",strength);
            catcher.enabled=strength>.001f&&casters.Length>0;
            if(!catcher.enabled)return;
            if(caster!=null)root.position=new Vector3(caster.position.x,.015f,caster.position.z);

            // Four target binds for a blob under an idle loop, blurred twice
            // before anyone sees it: the source refreshes it at shadowFps and so
            // does this.
            float fps=Mathf.Max(1,ElementalApp.Instance!=null?ElementalApp.Instance.Settings.F("performance.shadowFps",30):30);
            float now=Time.unscaledTime;
            if(refreshed>=0&&now-refreshed<1/fps)return;
            refreshed=now;
            Render();
        }

        void Render()
        {
            commands.Clear();
            commands.SetRenderTarget(target);
            commands.ClearRenderTarget(true,true,Color.clear);
            commands.SetViewProjectionMatrices(shadowCamera.worldToCameraMatrix,GL.GetGPUProjectionMatrix(shadowCamera.projectionMatrix,true));
            foreach(var r in casters)
            {
                if(r==null||!r.enabled||!r.gameObject.activeInHierarchy)continue;
                int count=r is SkinnedMeshRenderer skinned&&skinned.sharedMesh!=null?skinned.sharedMesh.subMeshCount:1;
                for(int i=0;i<count;i++)commands.DrawRenderer(r,material,i,0);
            }
            Graphics.ExecuteCommandBuffer(commands);
            Blur(BlurAmount);
            Blur(BlurAmount*.35f);
        }

        // Blit assigns the source to _MainTex itself, so the catcher's binding
        // has to be restored once the pair is done.
        void Blur(float amount)
        {
            material.SetFloat("_Blur",amount/Resolution);
            Graphics.Blit(target,scratch,material,1);
            Graphics.Blit(scratch,target,material,2);
            material.SetTexture("_MainTex",target);
        }

        public void Dispose()
        {
            commands.Release();
            if(target!=null)target.Release();
            if(scratch!=null)scratch.Release();
            if(material!=null)Object.Destroy(material);
            if(quad!=null)Object.Destroy(quad);
            if(root!=null)Object.Destroy(root.gameObject);
        }
    }
}
