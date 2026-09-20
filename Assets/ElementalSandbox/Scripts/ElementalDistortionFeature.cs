using UnityEngine;
using UnityEngine.Rendering.Universal;
namespace ElementalSandbox
{
    // URP owns the RenderGraph color copy; this feature is skipped on an idle stage.
    public sealed class ElementalDistortionFeature:FullScreenPassRendererFeature
    {
        public override void AddRenderPasses(ScriptableRenderer renderer,ref RenderingData data)
        {
            if(ElementalApp.Instance==null||ElementalApp.Instance.ActiveCount==0||data.cameraData.camera!=ElementalApp.Instance.View)return;
            base.AddRenderPasses(renderer,ref data);
        }
    }
}
