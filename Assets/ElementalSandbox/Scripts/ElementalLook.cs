using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace ElementalSandbox
{
    // The look layer the first port left out.
    //
    // Everything here reads the same environment.* / post.* values the Three.js
    // build reads, all of which were already sitting unused in Defaults.json:
    //
    //   Environment.js      image-based lighting from spruit_sunrise.hdr at
    //                       environment.envIntensity, plus the ambient and
    //                       hemisphere fills folded into the same probe
    //   GradeShader.js      exposure, contrast, saturation, lift/gain,
    //                       temperature, vignette, grain, chromatic aberration
    //   DustMotes.js        2,600 additive motes on one draw call
    //   ContactShadows.js   blurred depth-from-below under the caster
    //
    // Without them the stage was a flat colour with two directional lights on
    // it: no environment specular, no air, no grade.
    public sealed class ElementalLook
    {
        readonly ElementalApp app;
        readonly SandboxSettings settings;
        readonly Volume volume;
        readonly Bloom bloom;
        readonly Tonemapping tonemapping;
        readonly Vignette vignette;
        readonly ColorAdjustments color;
        readonly LiftGammaGain liftGammaGain;
        readonly WhiteBalance whiteBalance;
        readonly FilmGrain grain;
        readonly ChromaticAberration aberration;
        readonly Cubemap probe;
        SphericalHarmonicsL2 probeSH;
        readonly bool hasProbe;
        readonly DustMotes dust;
        readonly ContactShadows contact;

        public ElementalLook(ElementalApp app,Transform parent)
        {
            this.app=app;settings=app.Settings;
            volume=new GameObject("Post processing",typeof(Volume)).GetComponent<Volume>();volume.transform.SetParent(parent);volume.isGlobal=true;volume.priority=100;
            var profile=ScriptableObject.CreateInstance<VolumeProfile>();volume.profile=profile;
            // Ordered the way the source composer runs: bloom on the linear HDR
            // buffer, ACES, then the grade in display space.
            bloom=profile.Add<Bloom>();bloom.active=true;
            tonemapping=profile.Add<Tonemapping>();tonemapping.mode.Override(TonemappingMode.ACES);
            color=profile.Add<ColorAdjustments>();
            liftGammaGain=profile.Add<LiftGammaGain>();
            whiteBalance=profile.Add<WhiteBalance>();
            vignette=profile.Add<Vignette>();
            grain=profile.Add<FilmGrain>();grain.type.Override(FilmGrainLookup.Thin1);
            aberration=profile.Add<ChromaticAberration>();

            probe=Resources.Load<Cubemap>("Elemental/hdri/spruit_sunrise");
            var skyShader=Shader.Find("Elemental/SkyProbe");
            hasProbe=probe!=null&&skyShader!=null;
            if(hasProbe)
            {
                // Diffuse: let Unity convolve the probe into SH once, keep the
                // result, then drive the live probe ourselves so the analytic
                // ambient and hemisphere fills can ride on top of it. The
                // skybox is only ever mounted for this one call — the stage
                // keeps the flat backdrop the source gives it.
                var priorMode=RenderSettings.ambientMode;var priorSkybox=RenderSettings.skybox;float priorIntensity=RenderSettings.ambientIntensity;
                var sky=new Material(skyShader){hideFlags=HideFlags.HideAndDontSave};sky.SetTexture("_Tex",probe);
                RenderSettings.skybox=sky;RenderSettings.ambientMode=AmbientMode.Skybox;RenderSettings.ambientIntensity=1;
                DynamicGI.UpdateEnvironment();
                probeSH=RenderSettings.ambientProbe;
                RenderSettings.skybox=priorSkybox;RenderSettings.ambientMode=priorMode;RenderSettings.ambientIntensity=priorIntensity;
                Object.DestroyImmediate(sky);
                // Specular: the same probe drives every Lit surface's
                // reflections, which is what the bare stone was missing.
                RenderSettings.defaultReflectionMode=DefaultReflectionMode.Custom;
                RenderSettings.customReflectionTexture=probe;
            }
            else probeSH=new SphericalHarmonicsL2();

            dust=new DustMotes(parent);
            contact=new ContactShadows(parent);
        }

        public void SetCaster(Transform caster)=>contact.SetCaster(caster);

        public void Update(Vector3 anchor)
        {
            ApplyAmbient();
            ApplyGrade();
            dust.Update(app.SimulationTime,anchor,settings.F("environment.dustAmount",.85f));
            contact.Update(settings.F("environment.contactShadow",.55f));
        }

        // Environment.js: AmbientLight + HemisphereLight + scene.environment.
        // Unity has no hemisphere light, so all three are summed into one probe.
        void ApplyAmbient()
        {
            float env=settings.F("environment.envIntensity",.32f);
            var sh=new SphericalHarmonicsL2();
            if(hasProbe){sh=probeSH;ScaleSH(ref sh,env);RenderSettings.reflectionIntensity=env;}
            // AmbientLight: uniform, so it lands entirely on the constant band.
            Color ambient=settings.C("environment.ambientColor","#8ea8d8")*settings.F("environment.ambientIntensity",.14f);
            sh.AddAmbientLight(ambient);
            // HemisphereLight: mix(ground,sky,.5+.5*N.y)*i, i.e. a constant of
            // (sky+ground)/2 plus a linear Y ramp of (sky-ground)/2. Index 1 is
            // the Y coefficient of the linear band in Unity's ordering.
            float hemi=settings.F("environment.hemiIntensity",.36f);
            Color sky=settings.C("environment.hemiSkyColor","#bdd7ff")*hemi,ground=settings.C("environment.hemiGroundColor","#3a4552")*hemi;
            sh.AddAmbientLight(new Color((sky.r+ground.r)*.5f,(sky.g+ground.g)*.5f,(sky.b+ground.b)*.5f));
            sh[0,1]+=(sky.r-ground.r)*.5f;sh[1,1]+=(sky.g-ground.g)*.5f;sh[2,1]+=(sky.b-ground.b)*.5f;
            RenderSettings.ambientMode=AmbientMode.Custom;
            RenderSettings.ambientProbe=sh;
        }

        static void ScaleSH(ref SphericalHarmonicsL2 sh,float k){for(int c=0;c<3;c++)for(int i=0;i<9;i++)sh[c,i]*=k;}

        // GradeShader.js, mapped onto URP's volume components. The source works
        // in 0..1 display space; URP's controls are percentages and EV stops, so
        // each line below carries the conversion it performs.
        void ApplyGrade()
        {
            bool on=settings.B("post.enabled",true);
            volume.weight=on?1:0;
            if(!on)return;

            // renderer.toneMappingExposure, expressed as stops.
            color.postExposure.Override(Mathf.Log(Mathf.Max(.01f,settings.F("post.exposure",1.05f)),2));
            // (c-.5)*contrast+.5  ->  URP contrast is a -100..100 percentage.
            color.contrast.Override((settings.F("post.contrast",1.12f)-1)*100);
            // mix(luma,c,saturation)
            color.saturation.Override((settings.F("post.saturation",1.08f)-1)*100);
            // c*gain + lift. PrepareLiftGammaGain turns a wheel whose rgb is
            // neutral into "add w" and "multiply by 1+w", so the source's two
            // numbers go straight into the master channel.
            liftGammaGain.lift.Override(new Vector4(0,0,0,settings.F("post.lift",-.008f)));
            liftGammaGain.gain.Override(new Vector4(0,0,0,settings.F("post.gain",1)-1));
            // r += t*.12 / b -= t*.12, positive being warm — the same sign as
            // URP's temperature, scaled so -0.03 stays the whisper it is.
            whiteBalance.temperature.Override(Mathf.Clamp(settings.F("post.temperature",-.03f)*100,-100,100));

            // 1 - v*smoothstep(.15,.72,r2*1.9): full strength in the corners,
            // untouched through the middle third.
            float v=settings.F("post.vignette",.52f);
            vignette.intensity.Override(Mathf.Clamp01(v*.62f));
            vignette.smoothness.Override(.45f);
            vignette.rounded.Override(false);

            // Source grain is a +-uGrain/2 offset on a 0..1 colour.
            float g=settings.F("post.grain",.045f);
            grain.active=g>.0005f;
            grain.intensity.Override(Mathf.Clamp01(g*5.5f));
            grain.response.Override(.8f);

            // offset = centred * r2 * uAberration * .02, so the corners shift by
            // uAberration * .01 of the frame.
            float ca=settings.F("post.chromaticAberration",.4f);
            aberration.active=ca>.001f;
            aberration.intensity.Override(Mathf.Clamp01(ca*.38f));

            // UnrealBloomPass(strength, radius, threshold) on the linear buffer.
            // The source keeps strength near zero on purpose — the glow in the
            // reference frames is emissive geometry, not the bloom chain — so
            // the port's old flat 0.45 was washing the silhouettes out.
            float strength=settings.F("post.bloomStrength",.03f);
            bloom.active=strength>.0005f;
            bloom.threshold.Override(settings.F("post.bloomThreshold",.88f));
            bloom.intensity.Override(strength*8);
            bloom.scatter.Override(Mathf.Clamp01(.45f+settings.F("post.bloomRadius",.6f)*.35f));
        }

        public void Dispose()
        {
            dust.Dispose();contact.Dispose();
            if(volume!=null){if(volume.profile!=null)Object.Destroy(volume.profile);Object.Destroy(volume.gameObject);}
            RenderSettings.customReflectionTexture=null;
            RenderSettings.ambientMode=AmbientMode.Trilight;
        }
    }
}
