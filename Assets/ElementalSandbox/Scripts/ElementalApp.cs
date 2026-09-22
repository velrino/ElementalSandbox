using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace ElementalSandbox
{
    [DefaultExecutionOrder(-100)]
    public sealed class ElementalApp : MonoBehaviour
    {
        public static ElementalApp Instance {get;private set;}
        public SandboxSettings Settings {get;private set;}
        public Camera View {get;private set;}
        public Transform EffectsRoot {get;private set;}
        public readonly List<SandboxDummy> Dummies=new List<SandboxDummy>();
        readonly List<SpellVisual> effects=new List<SpellVisual>();
        readonly float[] cooldowns=new float[10];
        readonly Vector4[] distortCenters=new Vector4[4],distortParams=new Vector4[4];
        // LightPool.js: a fixed set of point lights created once and parked at
        // zero, so a cast never adds a light to the scene. The first port fed
        // the floor shader an ad-hoc term in the ability's accent colour at a
        // flat intensity; the source lights floor, stone, character and
        // targets alike, in lightColor at lightIntensity, and that floor glow
        // is most of what read as "atmosphere" in its frames.
        readonly Light[] castLights=new Light[4];
        public SourceDecals Decals {get;private set;}
        public int Selected {get;private set;}=0;
        public bool Armed {get;private set;}
        public bool Paused {get;private set;}
        public Vector3 AimPoint {get;private set;}=new Vector3(0,0,6);
        public float SimulationTime {get;private set;}
        public int ActiveCount {get{int n=0;foreach(var e in effects)if(e.Active)n++;return n;}}
        public SandboxHUD HUD {get;private set;}
        public string Status="Select an ability, aim, click or tap to cast.";
        Transform actor,model;
        Animation animation;
        string castName;
        float animationRemaining;
        Material floorMaterial,aimMaterial;
        Transform zone;
        LineRenderer arrow;
        Light sun,rim;
        float yaw=-34.4f,pitch=24.5f,distance=11.5f,shake;
        Vector3 cameraTarget=new Vector3(0,1.35f,0),focus;
        float focusWeight;
        Vector2 pointer;
        bool pointerValid;
        ElementalLook look;
        float fpsElapsed;int fpsFrames;
        public float FPS {get;private set;}=60;
        public float CpuMilliseconds {get;private set;}
        public float ResolutionScale {get;private set;}=1;
        float budgetElapsed;
        readonly Key[] keys={Key.Q,Key.E,Key.R,Key.F,Key.V,Key.X,Key.B,Key.Z,Key.N,Key.K};
        readonly Key[] numbers={Key.Digit1,Key.Digit2,Key.Digit3,Key.Digit4,Key.Digit5,Key.Digit6,Key.Digit7,Key.Digit8,Key.Digit9,Key.Digit0};
        void Awake()
        {
            Instance=this;Settings=new SandboxSettings();Application.targetFrameRate=60;QualitySettings.vSyncCount=0;Time.timeScale=1;
            EffectsRoot=new GameObject("Abilities (pooled)").transform;EffectsRoot.SetParent(transform);
            CreateStage();CreateCharacter();look.SetCaster(model);CreateAim();ResetTargets();HUD=gameObject.AddComponent<SandboxHUD>();HUD.App=this;
            float pref=PlayerPrefs.GetFloat("Elemental.Quality",1);SetQuality(pref<.5f);
            pointer=new Vector2(Screen.width*.6f,Screen.height*.45f);UpdateAim();UpdateCamera(1);
        }
        void CreateStage()
        {
            foreach(var camera in FindObjectsByType<Camera>())camera.gameObject.SetActive(false);
            var cameraObject=new GameObject("Sandbox Camera",typeof(Camera),typeof(AudioListener));cameraObject.transform.SetParent(transform);View=cameraObject.GetComponent<Camera>();View.tag="MainCamera";View.nearClipPlane=.1f;View.farClipPlane=400;View.allowHDR=true;View.clearFlags=CameraClearFlags.SolidColor;var data=View.GetUniversalAdditionalCameraData();data.renderPostProcessing=true;data.requiresDepthTexture=true;data.requiresColorTexture=true;data.antialiasing=AntialiasingMode.FastApproximateAntialiasing;
            var floor=GameObject.CreatePrimitive(PrimitiveType.Plane);floor.name="Ground - same 400 metre plane";floor.transform.SetParent(transform);floor.transform.localScale=Vector3.one*40;
            floorMaterial=new Material(Shader.Find("Elemental/Ground"));floorMaterial.SetTexture("_MainTex",Resources.Load<Texture2D>("Elemental/textures/cathedral/color"));floorMaterial.SetTexture("_NormalTex",Resources.Load<Texture2D>("Elemental/textures/cathedral/normal"));floorMaterial.SetTexture("_RoughTex",Resources.Load<Texture2D>("Elemental/textures/cathedral/roughness"));floorMaterial.SetTexture("_AoTex",Resources.Load<Texture2D>("Elemental/textures/cathedral/ao"));floor.GetComponent<Renderer>().sharedMaterial=floorMaterial;
            sun=new GameObject("Key light",typeof(Light)).GetComponent<Light>();sun.transform.SetParent(transform);sun.type=LightType.Directional;sun.shadows=LightShadows.Soft;sun.shadowBias=.025f;sun.shadowNormalBias=.12f;
            rim=new GameObject("Cool rim",typeof(Light)).GetComponent<Light>();rim.transform.SetParent(transform);rim.type=LightType.Directional;rim.shadows=LightShadows.None;
            look=new ElementalLook(this,transform);
            Decals=new SourceDecals(transform);
            for(int i=0;i<castLights.Length;i++){var l=new GameObject("Cast light "+(i+1),typeof(Light)).GetComponent<Light>();l.transform.SetParent(transform);l.type=LightType.Point;l.shadows=LightShadows.None;l.useColorTemperature=false;l.intensity=0;l.range=1;castLights[i]=l;}
        }
        void CreateCharacter()
        {
            actor=new GameObject("Caster").transform;actor.SetParent(transform);var prefab=Resources.Load<GameObject>("Elemental/Models/Idle");if(prefab==null)throw new InvalidOperationException("Original character FBX is missing.");model=Instantiate(prefab,actor).transform;model.name="Original character";NormalizeModel(model,1.8f);
            var skin=new Material(Shader.Find("Universal Render Pipeline/Lit"));skin.SetTexture("_BaseMap",Resources.Load<Texture2D>("Elemental/Models/diffuse"));skin.SetFloat("_Smoothness",.15f);foreach(var r in model.GetComponentsInChildren<Renderer>()){var mats=r.sharedMaterials;for(int i=0;i<mats.Length;i++)mats[i]=skin;r.sharedMaterials=mats;r.shadowCastingMode=ShadowCastingMode.On;r.receiveShadows=true;}
            animation=model.GetComponent<Animation>()??model.gameObject.AddComponent<Animation>();animation.playAutomatically=false;var names=new[]{"Idle","cast1","cast2","cast3"};foreach(var name in names){var clips=new[]{Resources.Load<AnimationClip>("Elemental/Animations/"+name)};foreach(var clip in clips){if(clip==null)throw new InvalidOperationException("Missing bound animation "+name);var copy=Instantiate(clip);copy.legacy=true;copy.wrapMode=name=="Idle"?WrapMode.Loop:WrapMode.Once;animation.AddClip(copy,name);break;}}
            if(animation["Idle"]!=null)animation.Play("Idle");
        }
        public static void NormalizeModel(Transform t,float height)
        {
            var rs=t.GetComponentsInChildren<Renderer>();if(rs.Length==0)return;Bounds b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);t.localScale*=height/Mathf.Max(.001f,b.size.y);b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);t.position+=new Vector3(t.parent.position.x-b.center.x,t.parent.position.y-b.min.y,t.parent.position.z-b.center.z);
        }
        void CreateAim()
        {
            aimMaterial=new Material(Shader.Find("Elemental/Energy"));aimMaterial.SetFloat("_Mode",0);aimMaterial.SetFloat("_Glow",1);aimMaterial.SetFloat("_Opacity",1);
            zone=GameObject.CreatePrimitive(PrimitiveType.Quad).transform;zone.name="Zone aim";zone.SetParent(transform);Destroy(zone.GetComponent<Collider>());zone.GetComponent<Renderer>().sharedMaterial=aimMaterial;
            var g=new GameObject("Line skillshot aim",typeof(LineRenderer));g.transform.SetParent(transform);arrow=g.GetComponent<LineRenderer>();arrow.sharedMaterial=new Material(Shader.Find("Elemental/Energy"));arrow.sharedMaterial.SetFloat("_Mode",6);arrow.sharedMaterial.SetFloat("_Glow",1);arrow.sharedMaterial.SetFloat("_Opacity",1);arrow.useWorldSpace=true;arrow.positionCount=8;arrow.widthMultiplier=.06f;arrow.numCornerVertices=2;
        }
        public float Cooldown(int i)=>Mathf.Max(0,cooldowns[i]-SimulationTime);
        public void Select(int slot){Selected=Mathf.Clamp(slot,0,9);Armed=true;Status=Settings.Abilities[Selected].hint+" — aim, then click or tap";UpdateAim();}
        public void Cancel(){Armed=false;Status="Cast cancelled.";}
        public void TogglePause(){Paused=!Paused;Time.timeScale=Paused?0:Mathf.Clamp(Settings.F("global.timeScale",1),0,4);}
        public void Clear(){foreach(var e in effects)e.Retire();Decals.Clear();Armed=false;shake=0;Status="Effects cleared.";}
        public void ResetTargets(){foreach(var d in Dummies)if(d!=null)Destroy(d.gameObject);Dummies.Clear();int count=Mathf.Clamp((int)Settings.F("dummies.count",6),0,32);if(!Settings.B("dummies.enabled",true))count=0;for(int i=0;i<count;i++){var g=new GameObject("Target "+(i+1));g.transform.SetParent(transform);var d=g.AddComponent<SandboxDummy>();d.Initialize(this,i,count);Dummies.Add(d);}}
        public SandboxDummy Nearest(Vector3 center,float range){SandboxDummy found=null;float best=range;foreach(var d in Dummies)if(d.Alive){float dist=Vector3.Distance(d.Position,center);if(dist<best){best=dist;found=d;}}return found;}
        public bool CastAt(int slot,Vector3 point,bool bypassCooldown=false)
        {
            if(Paused)return false;if(!bypassCooldown&&Cooldown(slot)>0){Status="Ability cooling down.";return false;}
            var meta=Settings.Abilities[slot];float range=Settings.F(meta.id+".range",20),minimum=Settings.F(meta.id+".minRange",0);point.y=0;Vector3 offset=point-actor.position;float dist=Mathf.Clamp(offset.magnitude,minimum,range);Vector3 dir=offset.sqrMagnitude>.001f?offset.normalized:Vector3.forward;point=actor.position+dir*dist;
            if(ActiveCount>=4){SpellVisual oldest=null;foreach(var e in effects)if(e.Active&&(oldest==null||e.Age>oldest.Age))oldest=e;oldest?.Retire();}
            SpellVisual spell=null;foreach(var e in effects)if(!e.Active&&e.Slot==slot){spell=e;break;}if(spell==null){spell=new SpellVisual(this,slot);effects.Add(spell);}spell.Spawn(actor.position,point);cooldowns[slot]=SimulationTime+Settings.F(meta.id+".cooldown",2);actor.rotation=Quaternion.LookRotation(dir);castName=Settings.S(meta.id+".castAnim","cast2");if(animation[castName]!=null){animation[castName].time=0;animation.CrossFade(castName,Settings.F("character.castBlendIn",.12f));animationRemaining=animation[castName].length;}
            focus=point+Vector3.up;focusWeight=1;Armed=false;Status=meta.hint;return true;
        }
        public void ForceCamera(){distance=Settings.F("camera.distance",11.5f);UpdateCamera(0);}
        public void Shake(float amount){shake=Mathf.Max(shake,amount*Settings.F("global.cameraShake",1));}
        void Update()
        {
            double start=Time.realtimeSinceStartupAsDouble;float real=Mathf.Min(Time.unscaledDeltaTime,.1f);float scale=Paused?0:Mathf.Clamp(Settings.F("global.timeScale",1),0,4);Time.timeScale=scale;float dt=real*scale;SimulationTime+=dt;Shader.SetGlobalFloat("_SandboxTime",SimulationTime);HandleInput();UpdateAim();UpdateEnvironment();look.Update(actor.position);Decals.Tick(dt);
            int li=0;float damp=1-Mathf.Pow(.0005f,Mathf.Max(dt,1e-4f)),gi=Settings.F("global.lightIntensity",1),gr=Settings.F("global.lightRadius",1);
            foreach(var e in effects){e.Tick(dt);if(e.Active&&li<4){
                // Ability._updateLight + LightPool.set, per active cast.
                var l=castLights[li];l.transform.position=e.Focus+Vector3.up*e.LightHeight();l.color=e.C("lightColor","#ffffff");
                float target=(e.F("lightIntensity",10)*e.LightScale()*e.LightShimmer()+e.LightBoost)*gi;
                l.intensity=Mathf.Lerp(l.intensity,target,damp);l.range=Mathf.Max(.5f,e.F("lightRadius",12)*(1+e.LightBoost*.02f)*gr);
                Vector3 dc=e.Target+Vector3.up*(e.Id=="astral"?e.F("coreHeight",3.4f):1);distortCenters[li]=new Vector4(dc.x,dc.y,dc.z,e.Id=="astral"?e.F("coreRadius",1.05f)*2:e.Radius);distortParams[li]=new Vector4(e.Id=="astral"?1:0,e.Age,e.Envelope,Settings.F("global.distortion",1));li++;}}
            for(int k=li;k<4;k++){castLights[k].intensity=Mathf.Lerp(castLights[k].intensity,0,1-Mathf.Pow(.0001f,Mathf.Max(dt,1e-4f)));distortCenters[k]=Vector4.zero;distortParams[k]=Vector4.zero;}
            Shader.SetGlobalVectorArray("_DistortCenters",distortCenters);Shader.SetGlobalVectorArray("_DistortParams",distortParams);
            foreach(var d in Dummies)d.Tick(dt);if(animationRemaining>0){animationRemaining-=dt*Settings.F("global.animationSpeed",1);if(animationRemaining<=0&&animation["Idle"]!=null)animation.CrossFade("Idle",Settings.F("character.castBlendOut",.3f));}foreach(AnimationState state in animation)state.speed=Settings.F("global.animationSpeed",1);
            UpdateCamera(real);Application.targetFrameRate=ActiveCount>0||Armed?(int)Settings.F("performance.maxFps",60):(int)Settings.F("performance.idleFps",30);fpsElapsed+=Time.unscaledDeltaTime;fpsFrames++;if(fpsElapsed>.5f){FPS=fpsFrames/fpsElapsed;fpsElapsed=0;fpsFrames=0;}
            CpuMilliseconds=(float)((Time.realtimeSinceStartupAsDouble-start)*1000);
            // Adapt only from measured work, never from an intentionally throttled idle interval.
            if(Settings.B("performance.dynamicResolution",true)&&ActiveCount>0){budgetElapsed+=real;if(budgetElapsed>3){float target=1000/Mathf.Min(60,Settings.F("performance.maxFps",60));if(CpuMilliseconds>target*1.2f)ResolutionScale=Mathf.Max(.6f,ResolutionScale-.1f);else if(CpuMilliseconds<target*.6f)ResolutionScale=Mathf.Min(1,ResolutionScale+.05f);ApplyResolution();budgetElapsed=0;}}
        }
        void HandleInput()
        {
            var kb=Keyboard.current;if(kb!=null&&!HUD.EditingText){for(int i=0;i<10;i++)if(kb[keys[i]].wasPressedThisFrame||kb[numbers[i]].wasPressedThisFrame)Select(i);if(kb.escapeKey.wasPressedThisFrame)Cancel();if(kb.gKey.wasPressedThisFrame)HUD.ShowEditor=!HUD.ShowEditor;if(kb.hKey.wasPressedThisFrame)HUD.ShowHelp=!HUD.ShowHelp;if(kb.pKey.wasPressedThisFrame)TogglePause();if(kb.cKey.wasPressedThisFrame)Clear();if(kb.tKey.wasPressedThisFrame)ResetTargets();}
            var ts=Touchscreen.current;int touches=0;UnityEngine.InputSystem.Controls.TouchControl first=null,second=null;if(ts!=null)foreach(var touch in ts.touches)if(touch.press.isPressed){if(first==null)first=touch;else if(second==null)second=touch;touches++;}
            if(touches>=2){Vector2 a=first.position.ReadValue(),b=second.position.ReadValue(),da=first.delta.ReadValue(),db=second.delta.ReadValue();float now=(a-b).magnitude,before=(a-da-b+db).magnitude;yaw+=(da.x+db.x)*.08f;pitch-=(da.y+db.y)*.08f;if(now>1&&before>1)Settings.Set("camera.distance",Mathf.Clamp(Settings.F("camera.distance",11.5f)*before/now,Settings.F("camera.minDistance",3.5f),Settings.F("camera.maxDistance",30)));return;}
            if(touches==1){pointer=first.position.ReadValue();if(first.press.wasPressedThisFrame&&!HUD.Blocks(pointer)){UpdateAim();if(Armed&&pointerValid)CastAt(Selected,AimPoint);}return;}
            var mouse=Mouse.current;if(mouse==null)return;pointer=mouse.position.ReadValue();if(HUD.Blocks(pointer))return;if(mouse.rightButton.wasPressedThisFrame)Cancel();if(mouse.rightButton.isPressed){var d=mouse.delta.ReadValue();yaw+=d.x*.18f;pitch-=d.y*.18f;}float scroll=mouse.scroll.ReadValue().y;if(Mathf.Abs(scroll)>.01f)Settings.Set("camera.distance",Mathf.Clamp(Settings.F("camera.distance",11.5f)*Mathf.Exp(-scroll*.0012f),Settings.F("camera.minDistance",3.5f),Settings.F("camera.maxDistance",30)));if(mouse.leftButton.wasPressedThisFrame){UpdateAim();if(Armed&&pointerValid)CastAt(Selected,AimPoint);}
        }
        void UpdateAim()
        {
            Ray ray=View.ScreenPointToRay(pointer);pointerValid=new Plane(Vector3.up,Vector3.zero).Raycast(ray,out float enter)&&enter>0&&enter<500;if(pointerValid){Vector3 p=ray.GetPoint(enter);var id=Settings.Abilities[Selected].id;Vector3 delta=p-actor.position;float range=Mathf.Clamp(delta.magnitude,Settings.F(id+".minRange",0),Settings.F(id+".range",20));AimPoint=actor.position+delta.normalized*range;if(Armed&&Settings.B("character.turnToAim",true)&&delta.sqrMagnitude>.01f)actor.rotation=Quaternion.Slerp(actor.rotation,Quaternion.LookRotation(delta.normalized),1-Mathf.Exp(-10*Time.unscaledDeltaTime));}
            bool show=Armed&&pointerValid;var meta=Settings.Abilities[Selected];zone.gameObject.SetActive(show&&meta.zone);arrow.enabled=show&&!meta.zone;aimMaterial.SetColor("_BaseColor",meta.Color);arrow.sharedMaterial.SetColor("_BaseColor",meta.Color);zone.position=AimPoint+Vector3.up*.05f;zone.rotation=Quaternion.Euler(90,0,0);zone.localScale=Vector3.one*Settings.F(meta.id+".zoneRadius",4)*2;
            if(show&&!meta.zone){Vector3 a=actor.position+Vector3.up*.05f,b=AimPoint+Vector3.up*.05f,d=(b-a).normalized,s=Vector3.Cross(Vector3.up,d)*.45f;arrow.SetPositions(new[]{a+s,b-d*.7f+s,b-d*.7f+s*2,b,b-d*.7f-s*2,b-d*.7f-s,a-s,a+s});}
        }
        void UpdateCamera(float dt)
        {
            pitch=Mathf.Clamp(pitch,90-Settings.F("camera.maxPolar",1.32f)*Mathf.Rad2Deg,90-Settings.F("camera.minPolar",.35f)*Mathf.Rad2Deg);float targetDistance=Settings.F("camera.distance",11.5f);distance=Mathf.Lerp(distance,targetDistance,1-Mathf.Exp(-6*dt));Vector3 target=Vector3.Lerp(new Vector3(0,Settings.F("camera.targetHeight",1.35f),0),focus,focusWeight*Settings.F("camera.autoFrame",.35f));cameraTarget=Vector3.Lerp(cameraTarget,target,1-Mathf.Exp(-4*dt));focusWeight=Mathf.Max(0,focusWeight-dt*.08f);float y=pitch*Mathf.Deg2Rad,a=yaw*Mathf.Deg2Rad;Vector3 offset=new Vector3(Mathf.Sin(a)*Mathf.Cos(y),Mathf.Sin(y),-Mathf.Cos(a)*Mathf.Cos(y))*distance;View.transform.position=cameraTarget+offset;View.transform.LookAt(cameraTarget);if(shake>.001f){View.transform.position+=new Vector3(Mathf.Sin(SimulationTime*73),Mathf.Sin(SimulationTime*91),0)*shake*.07f;shake=Mathf.Max(0,shake-dt*1.6f);}View.fieldOfView=Settings.F("camera.fov",46);
        }
        void UpdateEnvironment()
        {
            View.backgroundColor=Settings.C("environment.backgroundColor");RenderSettings.fog=Settings.B("environment.fogEnabled",true);RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogColor=Settings.C("environment.fogColor");RenderSettings.fogStartDistance=Settings.F("environment.fogNear",26);RenderSettings.fogEndDistance=Settings.F("environment.fogFar",135);
            sun.color=Settings.C("environment.sunColor");sun.intensity=Settings.F("environment.sunIntensity",2.6f);sun.transform.rotation=SourceLightRotation(Settings.F("environment.sunAzimuth",2.95f),Settings.F("environment.sunElevation",.6f));Shader.SetGlobalVector("_ElementalLightDir",-sun.transform.forward);rim.color=Settings.C("environment.rimColor");rim.intensity=Settings.F("environment.rimIntensity",1.1f);rim.transform.rotation=SourceLightRotation(Settings.F("environment.rimAzimuth",5.45f),Settings.F("environment.rimElevation",.35f));floorMaterial.SetColor("_BaseColor",Settings.C("environment.floorColor"));floorMaterial.SetColor("_Tint",Settings.C("environment.floorTint"));floorMaterial.SetFloat("_Pool",Settings.F("environment.floorPool",.8f));floorMaterial.SetFloat("_Textured",Settings.B("environment.floorTexture")?1:0);floorMaterial.SetFloat("_Sheen",Settings.F("environment.floorSheen",.34f));floorMaterial.SetFloat("_Roughness",Settings.F("environment.floorRoughness",.88f));floorMaterial.SetFloat("_NormalScale",Settings.F("environment.floorNormalScale",.85f));floorMaterial.SetFloat("_TexTint",Settings.F("environment.floorTexTint",.4f));floorMaterial.SetFloat("_TexScale",Settings.F("environment.floorTextureScale",12));
        }
        // Three.js Environment._computeLightDirection, with source Z reflected.
        static Quaternion SourceLightRotation(float azimuth,float elevation)=>Quaternion.LookRotation(new Vector3(-Mathf.Cos(azimuth)*Mathf.Cos(elevation),-Mathf.Sin(elevation),Mathf.Sin(azimuth)*Mathf.Cos(elevation)));
        public void SetQuality(bool economy){Settings.Set("performance.maxFps",economy?30:60);Settings.Set("performance.idleFps",economy?15:30);Settings.Set("performance.shadowResolution",economy?1024:2048);PlayerPrefs.SetFloat("Elemental.Quality",economy?0:1);ResolutionScale=1;QualitySettings.shadows=UnityEngine.ShadowQuality.All;QualitySettings.shadowResolution=economy?UnityEngine.ShadowResolution.Medium:UnityEngine.ShadowResolution.High;ApplyResolution();}
        void ApplyResolution(){if(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset rp)rp.renderScale=ResolutionScale;}
        void OnApplicationFocus(bool focusState){if(!focusState)Armed=false;}
        void OnDestroy(){Time.timeScale=1;foreach(var e in effects)e.Dispose();if(floorMaterial!=null)Destroy(floorMaterial);if(aimMaterial!=null)Destroy(aimMaterial);look?.Dispose();Decals?.Dispose();Instance=null;}
    }
}
