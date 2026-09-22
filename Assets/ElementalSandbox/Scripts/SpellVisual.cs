using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace ElementalSandbox
{
    public sealed class SpellVisual
    {
        public readonly int Slot;
        public readonly string Id;
        public GameObject Root {get;private set;}
        public Vector3 Origin,Target,Direction;
        public float Age,Distance;
        public bool Active {get;private set;}
        readonly ElementalApp app;
        public ElementalApp App=>app;
        SandboxSettings S=>app.Settings;
        readonly List<Material> materials=new List<Material>();
        readonly Vector4[] clears=new Vector4[4];
        Material ringMat;Transform ring;
        readonly List<Mesh> ownedMeshes=new List<Mesh>();
        readonly List<Transform> parts=new List<Transform>();
        readonly List<LineRenderer> strands=new List<LineRenderer>();
        readonly List<Matrix4x4>[] instances=new List<Matrix4x4>[6];
        readonly Matrix4x4[] drawBuffer=new Matrix4x4[512];
        SourceEruption sourceEruption;
        SourceCascade sourceCascade;
        SourceGrowth sourceGrowth;
        Material stone,energy,pool,volume,sparkMat;
        Transform seed,disc,core,cloud,crown,shock,pillar,halo;
        ParticleSystem particles;
        readonly List<SandboxDummy> struck=new List<SandboxDummy>();
        float nextShot,travel,previousAge;
        int shots;
        bool impacted;
        public float F(string key,float fallback=0)=>S.F(Id+"."+key,fallback);
        public Color C(string key,string fallback="#ffffff")=>S.C(Id+"."+key,fallback);
        public float G(string key)=>S.F("global."+key,1);
        public float Radius=>S.Abilities[Slot].zone?F("zoneRadius",4):F("burstRadius",F("blastRadius",3));
        public Vector3 Focus=>Vector3.Lerp(Origin,Target,Mathf.Clamp01(Age/Mathf.Max(.01f,travel)));
        public float Life=>Id=="cyber"?F("shatterTime",.75f)+F("fadeTime",.85f):F("lifetime",5)*S.F("global.lifetime",1);
        public float Fade=>F("fadeTime",F("sinkTime",1));
        public float Envelope {get {float a=Age-travel;return Mathf.Clamp01(a/.4f)*(1-Mathf.Clamp01((a-Life)/Mathf.Max(.01f,Fade)));}}
        public float Travel=>travel;
        // Ability.js: the transient additive punch an impact puts into the
        // light, decaying on its own. Each ability sets its own factor at the
        // moment the front lands.
        public float LightBoost;
        // Where the source parks the light for this ability; most lift it a
        // fraction (lightHeight) of their tallest element, a few leave it on
        // the floor. The height stays clamped away from zero for those that
        // lift, so the floor never sees the light exactly edge-on.
        public float LightHeight()
        {
            float lh=F("lightHeight",0);
            switch(Id)
            {
                case "ward":return Mathf.Max(.3f,F("height",4.4f)*Mathf.Clamp01(lh));
                case "acid":return Mathf.Max(.25f,F("mistHeight",5.2f)*Mathf.Clamp01(lh));
                case "growth":return Mathf.Clamp01(lh)*F("bloomHeight",3)*Envelope;
                case "cyber":return F("flightHeight",1.6f);
                case "ink":return Mathf.Max(.2f,F("crownHeight",1.85f)*Mathf.Clamp01(lh));
                case "cascade":return Mathf.Lerp(.35f,F("crownHeight",3)*Envelope,Mathf.Clamp01(lh));
                case "rend":return F("pillarHeight",30)*lh;
                default:return 0;
            }
        }
        // Ability.update: 1 while travelling, easing down 45% over the stand,
        // then the last 35% draining through the fade.
        public float LightScale()
        {
            float t=Age-travel;
            if(t<0)return 1;
            if(t<Life){float u=Mathf.Clamp01(t/Mathf.Max(.01f,Life));return 1-u*u*.45f;}
            float v=Mathf.Clamp01((t-Life)/Mathf.Max(.01f,Fade));return (1-v)*.35f;
        }
        public float LightShimmer()=>.9f+.1f*Mathf.Sin(Age*9.3f)*Mathf.Sin(Age*3.7f);
        // MonolithRiftAbility: the marks the rift leaves on the floor. widthNear
        // to width along the line on the widthCurve power, and one DUSTRING per
        // 1/scarRate metres, scattered across that half-width.
        float scarDistance;
        float HalfWidth(float s)=>Mathf.Lerp(F("widthNear",.5f),F("width",1.5f),Mathf.Pow(Mathf.Clamp01(s),F("widthCurve",.85f)));
        void RiftDecals(float previousT,float t)
        {
            var decals=app.Decals;if(decals==null)return;float ex=G("explosionIntensity");
            if(previousAge<=0&&Age>0){scarDistance=0;decals.Spawn(SourceDecals.Kind.DustRing,Origin,F("widthNear",.5f)*3f,F("scarLife",8)*.4f,C("colorDustCoat","#cfc6b3"),C("colorScarA","#231f1a"),.5f,.4f);}
            float front=Distance*Mathf.Clamp01(Age/Mathf.Max(.01f,travel)),step=1/Mathf.Max(.05f,F("scarRate",1.4f));
            Vector3 side=Vector3.Cross(Direction,Vector3.up).normalized;
            while(front-scarDistance>=step){scarDistance+=step;float s=Mathf.Clamp01(scarDistance/Mathf.Max(.1f,Distance));float w=HalfWidth(s);
                Vector3 p=Origin+Direction*(s*Distance)+side*UnityEngine.Random.Range(-.7f,.7f)*w;
                decals.Spawn(SourceDecals.Kind.DustRing,p,w*F("scarSpread",1.6f)*UnityEngine.Random.Range(.6f,1.2f),F("scarLife",8),C("colorScarA","#231f1a"),C("colorScarB","#3e3931"),F("scarIntensity",.22f),F("scarWidth",.45f));}
            if(previousT<0&&t>=0){
                decals.Spawn(SourceDecals.Kind.DustRing,Target,F("ringRadius",8)*ex*.8f,F("scarLife",8)*.8f,C("colorScarA","#231f1a"),C("colorDustCoat","#cfc6b3"),.32f,.55f);
                decals.Spawn(SourceDecals.Kind.Shockwave,Target,F("shockRadius",6.5f)*ex,.55f,C("colorShockA","#d8cfbd"),C("colorShockB","#8d8578"),.5f,.05f);}
        }
        float H(float n)=>Mathf.Repeat(Mathf.Sin(n*127.1f+Slot*311.7f)*43758.5453f,1);
        public SpellVisual(ElementalApp owner,int slot)
        {
            app=owner;Slot=slot;Id=S.Abilities[slot].id;Root=new GameObject(S.Abilities[slot].hint);Root.transform.SetParent(app.EffectsRoot,false);
            for(int i=0;i<6;i++)instances[i]=new List<Matrix4x4>();
            Build();if(Id=="venom"||Id=="quake")sourceEruption=new SourceEruption(this,S);if(Id=="cascade")sourceCascade=new SourceCascade(this,S);if(Id=="growth")sourceGrowth=new SourceGrowth(this,S);Root.SetActive(false);
        }
        Material Mat(string shader,Color color,float mode=0,float glow=1)
        {
            var m=new Material(Shader.Find("Elemental/"+shader)){name=Id+" "+shader,enableInstancing=true};m.SetColor("_BaseColor",color);m.SetColor("_HotColor",Color.white);m.SetColor("_EdgeColor",S.Abilities[Slot].Color);m.SetFloat("_Mode",mode);m.SetFloat("_Glow",glow);m.SetFloat("_Opacity",1);materials.Add(m);return m;
        }
        // A per-ability volume, ported from the source material of the same
        // name. The shared Elemental/Volume read two of each block's shape
        // parameters; these read all of them.
        Material Dedicated(string shader,bool instanced=false)
        {
            var m=new Material(Shader.Find("Elemental/"+shader)){name=Id+" "+shader,enableInstancing=instanced};materials.Add(m);return m;
        }
        // The widest the chimney ever gets — the proxy only has to contain it,
        // because every metre of the shape is resolved in the fragment stage.
        float MistReach()=>1+Mathf.Max(F("mistFlare",.4f),0)+Mathf.Max(F("mistSkirt",.22f),0)+Mathf.Max(F("mistLobe",.32f),0);
        // The MeshStandardMaterial base under each source look: Obsidian .24/0
        // flat, VoidShard .34/.2 flat, RendShard .22/.1 flat, ShatterStone .88/0
        // flat, GrowthVine .82/0. The serpent is a ShaderMaterial in the source
        // and has no base, so it gets a neutral one.
        Material Solid(Material m,float roughness,float metallic,bool flat){m.SetFloat("_Roughness",roughness);m.SetFloat("_Metallic",metallic);m.SetFloat("_Flat",flat?1:0);return m;}
        Transform Primitive(string name,PrimitiveType type,Material mat)
        {
            var g=GameObject.CreatePrimitive(type);g.name=name;g.transform.SetParent(Root.transform,false);UnityEngine.Object.Destroy(g.GetComponent<Collider>());g.GetComponent<Renderer>().sharedMaterial=mat;g.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;return g.transform;
        }
        Transform MeshPart(string name,Mesh mesh,Material mat)
        {
            var g=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));g.transform.SetParent(Root.transform,false);g.GetComponent<MeshFilter>().sharedMesh=mesh;g.GetComponent<MeshRenderer>().sharedMaterial=mat;return g.transform;
        }
        LineRenderer Line(string name,Material m,int points,float width)
        {
            var g=new GameObject(name,typeof(LineRenderer));g.transform.SetParent(Root.transform,false);var l=g.GetComponent<LineRenderer>();l.sharedMaterial=m;l.useWorldSpace=true;l.positionCount=points;l.widthMultiplier=width;l.numCornerVertices=2;l.numCapVertices=2;l.shadowCastingMode=ShadowCastingMode.Off;strands.Add(l);return l;
        }
        Transform Plane(string name,Material m)=>Primitive(name,PrimitiveType.Quad,m);
        void Floor(Transform t,Vector3 pos,float radius){t.position=pos+Vector3.up*.035f;t.rotation=Quaternion.Euler(90,0,0);t.localScale=Vector3.one*radius*2;}
        void Billboard(Transform t,Vector3 pos,Vector3 scale){t.position=pos;t.rotation=app.View.transform.rotation;t.localScale=scale;}
        void Build()
        {
            var accent=S.Abilities[Slot].Color;
            energy=Mat("Energy",accent,5,1.3f);pool=Mat("Energy",accent,Id=="acid"||Id=="ward"||Id=="ink"?1:2,.9f);
            seed=Plane("Travelling seed",energy);disc=Plane("Ground field",pool);shock=MeshPart("Shock ring",MeshLibrary.Ring(128,.975f),Mat("Energy",accent,6,1));
            sparkMat=Mat("Energy",accent,5,2);var pg=new GameObject("Particles");pg.transform.SetParent(Root.transform,false);particles=pg.AddComponent<ParticleSystem>();particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);var main=particles.main;main.playOnAwake=false;main.loop=false;main.duration=20;main.startLifetime=1.5f;main.startSize=.075f;main.startSpeed=2;main.maxParticles=2400;main.simulationSpace=ParticleSystemSimulationSpace.World;var emission=particles.emission;emission.enabled=false;var shape=particles.shape;shape.enabled=false;var col=particles.colorOverLifetime;col.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(accent,.2f),new GradientColorKey(accent,1)},new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(1,.2f),new GradientAlphaKey(0,1)});col.color=gradient;var size=particles.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,.3f),new Keyframe(.1f,1),new Keyframe(1,0)));pg.GetComponent<ParticleSystemRenderer>().sharedMaterial=sparkMat;
            if(Id=="venom"||Id=="quake"||Id=="ward"||Id=="astral"||Id=="rend")
            {
                stone=Mat("Surface",Id=="quake"?new Color(.43f,.4f,.34f):Id=="ward"?new Color(.055f,.024f,.023f):Id=="venom"?C("colorBody","#653399"):Id=="cascade"?C("colorBladeBody","#08252f"):C("colorShardBody","#1b2436"),Id=="quake"?1:Id=="ward"?2:0,.7f);
                stone.SetColor("_HotColor",(Id=="venom"?C("colorVenom","#8aff28"):accent));stone.SetTexture("_MainTex",Resources.Load<Texture2D>("Elemental/textures/cathedral/color"));Solid(stone,Id=="astral"?.34f:Id=="rend"?.22f:Id=="venom"?.88f:.95f,Id=="astral"?.2f:Id=="rend"?.1f:0,Id!="quake");
                // ObsidianMaterial.js: the ward's glass is its own material.
                if(Id=="ward"){materials.Remove(stone);UnityEngine.Object.Destroy(stone);stone=Dedicated("Obsidian",true);}
            }
            if(Id=="venom")core=Plane("Venom core",Mat("Energy",C("colorCore","#baff4a"),5,1.4f));
            if(Id=="acid"){ringMat=Dedicated("AcidRing");ring=Plane("Pool boundary",ringMat);}
            if(Id=="ward")crown=MeshPart("Runed barrier",WallMesh(),Mat("Energy",accent,3,1));
            // Only acid and astral carry a volume. Venom's gas and the rift's
            // dust are ParticleEngine emitters in the source — VenomSurgeAbility
            // and MonolithRiftAbility import no volume material at all — so the
            // generic march the port hung on them was an invention, and a body
            // of flat haze standing where the source has smoke rolling off the
            // stone.
            if(Id=="acid"||Id=="astral")
            {
                volume=Id=="acid"?Dedicated("AcidMist"):Dedicated("Nebula");
                cloud=Primitive("Raymarched volume",PrimitiveType.Cube,volume);
            }
            if(Id=="astral")core=Plane("Event horizon",Mat("Horizon",new Color(1,.68f,.26f),0,1.8f));
            if(Id=="growth")
            {
                stone=Mat("Surface",new Color(.075f,.15f,.075f),2,.7f);stone.SetColor("_HotColor",accent);Solid(stone,.82f,0,false);core=Primitive("Arcane heart",PrimitiveType.Sphere,energy);
                                Line("Target lance",Mat("Energy",accent,6,4),20,.12f);
            }
            if(Id=="ink")
            {
                crown=MeshPart("Water crown",WallMesh(),Mat("Energy",accent*.65f,3,.7f));
                volume=Dedicated("InkVolume");cloud=Primitive("Ink water volume",PrimitiveType.Cube,volume);for(int i=0;i<12;i++)Line("Water tendril",Mat("Energy",accent,6,.6f),40,.05f);
            }
            if(Id=="cyber")
            {
                stone=Mat("Surface",new Color(.008f,.035f,.04f),3,1.5f);stone.SetColor("_HotColor",accent);Solid(stone,.5f,0,false);core=MeshPart("Original serpent mesh",MeshLibrary.Get("serpent"),stone);for(int i=0;i<18;i++)Line("Neon ribbon",Mat("Energy",i%3==0?new Color(1,.12f,.65f):accent,6,1),64,.035f);
            }
            if(Id=="rend")
            {
                pillar=MeshPart("Thirty metre pillar",WallMesh(),Mat("Energy",accent,4,1.2f));core=Plane("Judgment star",Mat("Energy",accent,7,2));halo=MeshPart("Orbiting halo",MeshLibrary.Ring(128,.98f),Mat("Energy",accent,6,2));for(int i=0;i<30;i++)Line("Charging tendril",Mat("Energy",i%4==0?new Color(.55f,.7f,1):accent,6,.5f),60,.012f);
            }
        }
        public void Spawn(Vector3 origin,Vector3 target)
        {
            Origin=origin;Target=target;Distance=Vector3.Distance(origin,target);Direction=(target-origin).normalized;if(Direction.sqrMagnitude<.01f)Direction=Vector3.forward;Age=previousAge=0;travel=Distance/Mathf.Max(.1f,F("speed",50)*S.F("global.speed",1));Active=true;impacted=false;shots=0;nextShot=1.7f;struck.Clear();Root.SetActive(true);particles.Clear();foreach(var l in strands)l.enabled=false;sourceEruption?.Spawn();sourceCascade?.Spawn();sourceGrowth?.Spawn();
        }
        public void Retire(){Active=false;Root.SetActive(false);particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);}
        void Burst(Vector3 pos,int count,float speed=4)
        {
            count=Mathf.Clamp(Mathf.RoundToInt(count*S.F("global.particleCount",1)),0,1200);for(int i=0;i<count;i++){float a=H(i+Age*8)*Mathf.PI*2,r=H(i*1.7f+Age);var e=new ParticleSystem.EmitParams{position=pos,velocity=new Vector3(Mathf.Cos(a)*r,.4f+H(i*3.1f)*1.4f,Mathf.Sin(a)*r)*speed,startLifetime=.5f+H(i*1.3f)*1.5f,startSize=(.025f+H(i*2.3f)*.075f)*S.F("global.particleSize",1)};particles.Emit(e,1);}
        }
        public void Tick(float dt)
        {
            if(!Active)return;previousAge=Age;Age+=dt;float t=Age-travel,env=Envelope;
            if(previousAge<travel&&Age>=travel){float k=Id=="acid"||Id=="astral"?1.3f:Id=="growth"?1.1f:1.2f;LightBoost=Mathf.Max(LightBoost,F("lightIntensity",10)*k*G("explosionIntensity"));}
            LightBoost=Mathf.Max(0,LightBoost-LightBoost*4.5f*dt-.5f*dt);
            if(Id=="quake")RiftDecals(previousAge-travel,t);
            if(t>Life+Fade){Retire();return;}
            foreach(var m in materials){m.SetFloat("_Age",Mathf.Max(0,t));m.SetFloat("_Opacity",env*S.F("global.opacity",1));}
            energy.SetFloat("_Glow",1.3f*S.F("global.glow",1));pool.SetFloat("_Glow",.9f*S.F("global.glow",1));
            Billboard(seed,Focus+Vector3.up*(Id=="astral"?F("coreHeight",3.4f):1.1f),Vector3.one*.65f);seed.gameObject.SetActive(t<0);energy.SetFloat("_Opacity",t<0?1:env);
            Floor(disc,Target,Radius*Mathf.Max(.001f,env));disc.gameObject.SetActive(t>=0&&Id!="cyber"&&Id!="quake"&&Id!="venom"&&Id!="cascade"&&Id!="growth");
            shock.gameObject.SetActive(t>=0&&t<1.2f);shock.position=Target+Vector3.up*.07f;shock.localScale=Vector3.one*Mathf.Max(.01f,Radius*(1+t*1.7f));shock.GetComponent<Renderer>().sharedMaterial.SetFloat("_Opacity",Mathf.Clamp01(1-t/1.2f));
            if(t>=0&&!impacted){impacted=true;if(sourceEruption==null&&sourceCascade==null)Burst(Target+Vector3.up*.3f,180,4);app.Shake(Id=="quake"?.5f:.18f);}
            if(dt>0){particles.Simulate(dt,false,false);if(sourceEruption==null&&sourceCascade==null&&t>0&&t<Life&&Mathf.FloorToInt(Age*18)!=Mathf.FloorToInt(previousAge*18))Burst(Target+new Vector3((H(Age)-.5f)*Radius, .2f,(H(Age*2)-.5f)*Radius),4,.5f);}
            foreach(var list in instances)list.Clear();
            if(Id=="venom"||Id=="quake")Eruption(t,env);
            else if(Id=="ward")Ward(t,env);
            else if(Id=="acid")Acid(t,env);
            else if(Id=="growth"){sourceGrowth.Tick(Age,travel,dt,Mathf.Clamp01((t-Life)/Mathf.Max(.01f,Fade)));Growth(t,env);}
            else if(Id=="cyber")Cyber(t,env);
            else if(Id=="ink")Ink(t,env,dt);
            else if(Id=="astral")Astral(t,env,dt);
            else if(Id=="cascade")sourceCascade.Tick(Age,travel,dt,Mathf.Clamp01((t-Life)/Mathf.Max(.01f,Fade)));
            else if(Id=="rend")Rend(t,env);
            if(stone!=null){stone.SetFloat("_Opacity",Id=="cyber"?1-Mathf.Clamp01(t/Fade):Mathf.Max(env,Id=="venom"||Id=="quake"?Mathf.Clamp01(Age/.2f)*(1-Mathf.Clamp01((t-Life)/Fade)):0));for(int k=0;k<6;k++){int count=instances[k].Count;if(count==0)continue;instances[k].CopyTo(drawBuffer);Graphics.DrawMeshInstanced(MeshLibrary.Get((Id=="quake"||Id=="ward"?"monolith":Id=="cascade"||Id=="rend"?"shard":"crystal")+k),0,stone,drawBuffer,count,null,ShadowCastingMode.On,true);}}
            if(dt>0&&(t>=0||!S.Abilities[Slot].zone)&&t<Life&&Id!="growth"&&Id!="cascade"&&Id!="astral"&&Id!="ink"&&(Id!="rend"||t>=F("chargeTime",1.7f)))
            foreach(var d in app.Dummies){if(!d.Alive||struck.Contains(d))continue;bool hit;if(S.Abilities[Slot].zone)hit=Vector3.Distance(d.Position,Target)<Radius;else{Vector3 end=Origin+Direction*Mathf.Min(Distance,Age*F("speed",23));hit=DistanceToSegment(d.Position,Origin,end)<S.F("dummies.hit.radius",1.5f);}if(hit){d.Hit(Direction*S.F("dummies.hit.impulse",6)+Vector3.up*S.F("dummies.hit.lift",3.4f));struck.Add(d);}}
        }
        void Instance(int i,Vector3 p,Quaternion r,Vector3 scale){if(scale.y>.001f&&instances[i%6].Count<512)instances[i%6].Add(Matrix4x4.TRS(p,r,scale));}
        void Eruption(float t,float env)
        {
            bool rock=Id=="quake";if(core!=null){core.gameObject.SetActive(t>=0);Billboard(core,Target+Vector3.up*F("coreHeight",1.15f),Vector3.one*env*2.4f);}
            // The gas and the dust are SourceEruptionParticles, as they are
            // ParticleEngine emitters in the source; there is no volume here.
            sourceEruption.Tick(Age,travel,Mathf.Clamp01((t-Life)/Mathf.Max(.01f,Fade)));
        }

        void Ward(float t,float env)
        {
            crown.gameObject.SetActive(t>=0);crown.position=Target;crown.localScale=new Vector3(Radius*env,F("height",4.4f)*env,Radius*env);float pulse=1+Mathf.Sin(t*F("bpm",72)*Mathf.PI/30)*F("beatDepth",.15f)*.1f;
            for(int i=0;i<24;i++){float a=i*Mathf.PI*2/24;float h=F("height",4.4f)*(.28f+H(i)*.65f)*env;Instance(i,Target+new Vector3(Mathf.Cos(a)*Radius,0,Mathf.Sin(a)*Radius)*env,Quaternion.Euler(0,-a*Mathf.Rad2Deg,12),new Vector3(.65f,h,.45f)*pulse);}
            pool.SetColor("_BaseColor",C("colorLava","#ff320a"));pool.SetFloat("_Glow",1.4f*S.F("global.glow",1)*pulse);
            // The obsidian's look, from settings.ward every frame. flashY is the
            // source's beat wave: beatPhase * (60/bpm) * flashSpeed.
            float bpm=Mathf.Max(1,F("bpm",58)),beat=Mathf.Repeat(Mathf.Max(0,t)*bpm/60,1);
            stone.SetColor("_ColorRock",C("colorObsidian","#1c1416"));stone.SetColor("_ColorChar",C("colorObsidianChar","#070406"));
            stone.SetColor("_ColorVein",C("colorVein","#ff2f10"));stone.SetColor("_ColorVeinCore",C("colorVeinCore","#ffdca6"));
            stone.SetFloat("_VeinScale",F("veinScale",1.4f)*G("noiseFrequency"));stone.SetFloat("_VeinWidth",F("veinWidth",.06f));stone.SetFloat("_VeinBranches",F("veinBranches",.35f));
            stone.SetFloat("_VeinDepth",F("veinDepth",.28f));stone.SetFloat("_VeinGlow",F("veinGlow",.35f)*G("shaderIntensity"));stone.SetFloat("_VeinFlow",F("veinFlow",.8f));
            stone.SetFloat("_VeinFlowSpeed",F("veinFlowSpeed",.9f)*G("noiseSpeed"));stone.SetFloat("_FlashY",t<0?-10:beat*(60/bpm)*F("flashSpeed",3.4f));stone.SetFloat("_FlashWidth",F("flashWidth",1));
            stone.SetFloat("_FlashGain",F("veinFlash",.7f));stone.SetFloat("_FacetTint",F("facetTint",.42f)*G("randomness"));stone.SetFloat("_Cavity",F("cavity",.4f));
            stone.SetFloat("_RimLight",F("rimLight",.3f)*G("fresnel"));stone.SetFloat("_Fade",env);stone.SetFloat("_Glow",F("obsidianGlow",1)*G("glow"));stone.SetFloat("_Roughness",F("glassRough",.42f));
        }
        void Acid(float t,float env)
        {
            cloud.gameObject.SetActive(t>=0);
            float h=Mathf.Max(.001f,F("mistHeight",5.2f)*env),reach=Radius*MistReach();
            // The proxy is only a scissor, so it is sized to the reach rather
            // than the radius — at the source's defaults the chimney is nearly
            // twice as wide as the pool, and a tighter box would shear it off.
            cloud.position=Target+Vector3.up*h*.5f;cloud.localScale=new Vector3(reach*2,h,reach*2);
            volume.SetVector("_Centre",Target);volume.SetFloat("_Radius",Radius);volume.SetFloat("_Height",h);volume.SetFloat("_Fade",env);
            volume.SetFloat("_Steps",F("mistSteps",26));volume.SetFloat("_Density",F("mistDensity",2.9f));volume.SetFloat("_Absorb",F("mistAbsorb",1.45f));
            volume.SetFloat("_Scale",F("mistScale",.5f)*G("noiseFrequency"));volume.SetFloat("_Detail",F("mistDetail",1.9f));volume.SetFloat("_Filament",F("mistFilament",.5f));
            volume.SetFloat("_Threshold",F("mistThreshold",.52f));volume.SetFloat("_Rise",F("mistRise",.5f)*G("noiseSpeed"));volume.SetFloat("_Stretch",F("mistStretch",.32f));
            volume.SetFloat("_Twist",F("mistTwist",1.2f));volume.SetFloat("_Spin",F("mistSpin",.02f)*Mathf.PI*2);volume.SetFloat("_Edge",F("mistEdge",.45f));
            volume.SetFloat("_Flare",F("mistFlare",.4f));volume.SetFloat("_Falloff",F("mistFalloff",1.5f));volume.SetFloat("_Skirt",F("mistSkirt",.22f));
            volume.SetFloat("_Lobe",F("mistLobe",.32f)*G("noiseStrength"));volume.SetFloat("_Tear",F("mistTear",.24f));
            volume.SetFloat("_GroundGlow",F("mistGroundGlow",1.15f)*G("shaderIntensity"));volume.SetFloat("_GroundFalloff",F("mistGroundFalloff",.85f));
            volume.SetFloat("_Shadow",F("mistShadow",2.2f));volume.SetFloat("_ShadowStep",F("mistShadowStep",.9f));
            volume.SetFloat("_Ambient",F("mistAmbient",.05f));volume.SetFloat("_Saturate",F("mistSaturate",2.2f));
            volume.SetFloat("_Boil",F("boilDepth",.9f));volume.SetFloat("_Seed",Slot*7.3f);
            volume.SetFloat("_Opacity",F("mistOpacity",1)*G("opacity"));volume.SetFloat("_Glow",F("mistGlow",.85f)*G("glow"));
            volume.SetColor("_ColorDeep",C("colorMistDeep","#0a1e05"));volume.SetColor("_ColorBody",C("colorMistBody","#4f8f1c"));
            volume.SetColor("_ColorEdge",C("colorMistEdge","#b7f25a"));volume.SetColor("_ColorGlow",C("colorAcid","#8fff1e"));volume.SetColor("_ColorLight",C("colorMistLight","#93a862"));
            pool.SetColor("_BaseColor",C("colorAcid","#8fff1e"));
            // The ring. Its quad is wider than the footprint because the halo
            // and the wobble both reach past R, and every threshold in the
            // shader is in metres so the band keeps its width when the zone is
            // re-scaled.
            float rr=Radius*Mathf.Max(.001f,env),quad=rr*1.35f;
            Floor(ring,Target,quad);ring.gameObject.SetActive(t>=0);
            ringMat.SetFloat("_QuadSize",quad*2);ringMat.SetFloat("_Radius",rr);ringMat.SetFloat("_Fade",env);ringMat.SetFloat("_Seed",Slot*7.3f);
            ringMat.SetFloat("_Boil",F("boilDepth",.9f));
            ringMat.SetFloat("_Width",F("ringWidth",.085f));ringMat.SetFloat("_Core",F("ringCore",1.5f)*G("shaderIntensity"));
            ringMat.SetFloat("_Halo",F("ringHalo",.4f));ringMat.SetFloat("_HaloWidth",F("ringHaloWidth",.4f));ringMat.SetFloat("_Spill",F("ringSpill",.1f));
            ringMat.SetFloat("_Wobble",F("ringWobble",.008f)*G("noiseStrength"));ringMat.SetFloat("_WobbleScale",F("ringWobbleScale",2.6f)*G("noiseFrequency"));
            ringMat.SetFloat("_Chevrons",F("ringChevrons",34));ringMat.SetFloat("_ChevronDepth",F("ringChevronDepth",.32f));ringMat.SetFloat("_Scroll",F("ringScroll",.05f));
            ringMat.SetFloat("_Sweep",F("ringSweep",1.1f));ringMat.SetFloat("_SweepSpeed",F("ringSweepSpeed",.2f));ringMat.SetFloat("_SweepWidth",F("ringSweepWidth",.12f));
            ringMat.SetFloat("_Ticks",F("ringTicks",6));ringMat.SetFloat("_Opacity",F("ringOpacity",1)*G("opacity"));ringMat.SetFloat("_Glow",F("ringGlow",.95f)*G("glow"));
            ringMat.SetColor("_ColorRing",C("colorRing","#9dff2b"));ringMat.SetColor("_ColorCore",C("colorRingCore","#f4ffd6"));
        }
        // The nest, the foliage and the wither now come from SourceGrowth.
        // What is left here is the bloom heart and the lance it shoots, which
        // still ride the generic layers.
        void Growth(float t,float env)
        {
            float grow=Mathf.Clamp01((t-F("vineDelay",.18f))/F("vineTime",1.15f))*env;
            float bloom=Mathf.Clamp01((t-F("bloomDelay",.85f))/F("bloomTime",1.05f))*env;
            Vector3 center=Target+Vector3.up*F("bloomHeight",3.2f)*grow;
            core.position=center;core.localScale=Vector3.one*.5f*bloom;
            var laser=strands[0];laser.enabled=false;
            if(t>nextShot&&t<Life){var d=app.Nearest(Target,Radius*1.8f);if(d!=null){nextShot=t+1.35f;shots++;shotTarget=d.Position+Vector3.up;d.Cut((d.Position-Target).normalized*7+Vector3.up*2);Burst(shotTarget,80,3);}else nextShot=t+.3f;}
            float shotAge=t-(nextShot-1.35f);
            if(shots>0&&shotAge>=0&&shotAge<.38f){laser.enabled=true;laser.widthMultiplier=.13f*(1-shotAge/.38f);for(int j=0;j<20;j++){float u=j/19f;laser.SetPosition(j,Vector3.Lerp(center,shotTarget,u)+Vector3.up*Mathf.Sin(u*Mathf.PI)*.15f);}}
        }
        Vector3 shotTarget;
        void Cyber(float t,float env)
        {
            float flight=Mathf.Min(Distance,Age*F("speed",22));Vector3 pos=Origin+Direction*flight+Vector3.up*F("launchHeight",1.35f);core.position=pos;core.rotation=Quaternion.LookRotation(-Direction);core.localScale=Vector3.one*F("bodyLength",5)*Mathf.Clamp01(Age/F("formTime",.22f))*(1-Mathf.Clamp01(t/F("shatterTime",.75f)));Vector3 side=Vector3.Cross(Direction,Vector3.up);
            for(int i=0;i<18;i++){var l=strands[i];l.enabled=Age>.02f&&t<F("fadeTime",.85f);l.widthMultiplier=F("trailWidth",.06f)*(1-Mathf.Clamp01(t/F("fadeTime",.85f)));for(int j=0;j<64;j++){float u=j/63f;float behind=Mathf.Min(flight,F("trailLength",9))*u;float a=u*9-Age*5+i*Mathf.PI*2/18;float r=F("trailRadius",.85f)*Mathf.Sin(u*Mathf.PI)*(.3f+H(i));l.SetPosition(j,pos-Direction*behind+side*Mathf.Cos(a)*r+Vector3.up*Mathf.Sin(a)*r);}}
        }
        void Ink(float t,float env,float dt)
        {
            float swell=.85f+.15f*Mathf.Sin(t*2)+.12f*Mathf.Sin(t*3.236f);crown.gameObject.SetActive(t>=0);crown.position=Target;crown.localScale=new Vector3(Radius,F("crownHeight",1.85f)*swell,Radius)*env;cloud.gameObject.SetActive(t>=0);
            float ih=Mathf.Max(.001f,F("columnHeight",3.6f)*env),ir=Radius*1.5f;
            float ireach=ir*(1+Mathf.Max(F("wispFlare",.3f),0)+Mathf.Max(F("wispSkirt",.12f),0)+Mathf.Max(F("wispLobe",.3f),0));
            cloud.position=Target+Vector3.up*ih*.5f;cloud.localScale=new Vector3(ireach*2,ih,ireach*2);
            volume.SetVector("_Centre",Target);volume.SetFloat("_Radius",ir);volume.SetFloat("_Height",ih);volume.SetFloat("_Fade",env);volume.SetFloat("_Seed",Slot*3.7f);
            volume.SetFloat("_Steps",Mathf.Round(F("wispSteps",24)));volume.SetFloat("_Density",F("wispDensity",3.4f)*G("shaderIntensity"));volume.SetFloat("_Absorb",F("wispAbsorb",1.75f));
            volume.SetFloat("_Scale",F("wispScale",.62f)*G("noiseFrequency"));volume.SetFloat("_Detail",F("wispDetail",2.2f));volume.SetFloat("_Filament",F("wispFilament",.62f));
            volume.SetFloat("_Threshold",F("wispThreshold",.48f));volume.SetFloat("_Rise",F("wispRise",.35f)*G("noiseSpeed"));volume.SetFloat("_Stretch",F("wispStretch",.55f));
            volume.SetFloat("_Twist",F("wispTwist",2.4f)*G("turbulence"));volume.SetFloat("_Spin",F("wispSpin",.14f));volume.SetFloat("_Wind",F("wispWind",1.6f)*G("turbulence"));
            volume.SetFloat("_Funnel",F("wispFunnel",.55f));volume.SetFloat("_Edge",F("wispEdge",.5f));volume.SetFloat("_Flare",F("wispFlare",.3f));
            volume.SetFloat("_Skirt",F("wispSkirt",.12f));volume.SetFloat("_Falloff",F("wispFalloff",1.35f));volume.SetFloat("_Lobe",F("wispLobe",.3f)*G("noiseStrength"));volume.SetFloat("_Tear",F("wispTear",.3f));
            volume.SetFloat("_Light",F("wispLight",.85f));volume.SetFloat("_Shadow",F("wispShadow",2.6f));volume.SetFloat("_ShadowStep",F("wispShadowStep",.8f));
            volume.SetFloat("_Ambient",F("wispAmbient",.08f));volume.SetFloat("_Saturate",F("wispSaturate",2.4f));
            volume.SetFloat("_Swell",(swell-.85f)*F("swellDepth",.75f));volume.SetFloat("_Drain",0);volume.SetFloat("_Opacity",F("wispOpacity",1)*G("opacity"));
            volume.SetColor("_ColorDeep",C("colorWispDeep","#04070a"));volume.SetColor("_ColorBody",C("colorWispBody","#12363d"));
            volume.SetColor("_ColorEdge",C("colorWispEdge","#4e8f92"));volume.SetColor("_ColorLight",C("colorWispLight","#bfe6e0"));
            // The bodies the tide has hold of, so the ink parts in front of them.
            int held=0;foreach(var d in app.Dummies){if(held>=4)break;if(Vector3.Distance(d.Position,Target)<Radius){clears[held++]=new Vector4(d.Position.x,d.Position.y+.9f,d.Position.z,1.1f);}}
            for(int k=held;k<4;k++)clears[k]=Vector4.zero;
            volume.SetVectorArray("_Clears",clears);volume.SetFloat("_ClearCount",held);
            volume.SetFloat("_Clear",held>0?F("wispClear",.85f):0);volume.SetFloat("_ClearFade",Mathf.Max(.05f,F("wispClearFade",1.1f)));
            pool.SetColor("_BaseColor",new Color(.008f,.035f,.04f));
            for(int i=0;i<12;i++){var l=strands[i];l.enabled=t>=0;for(int j=0;j<40;j++){float u=j/39f,a=i*Mathf.PI/6+u*3+t*.35f;float r=Radius*(1-u*.7f)*env;l.SetPosition(j,Target+new Vector3(Mathf.Cos(a)*r,Mathf.Sin(u*Mathf.PI)*F("wispHeight",3.2f)*env,Mathf.Sin(a)*r));}}
            if(t>.4f&&t<Life&&dt>0)foreach(var d in app.Dummies)if(Vector3.Distance(d.Position,Target)<Radius)d.Pull(Target-Vector3.up*1.6f,dt,4,false);
        }
        void Astral(float t,float env,float dt)
        {
            Vector3 center=Target+Vector3.up*F("coreHeight",3.4f)*env;core.gameObject.SetActive(t>=0);float pulse=1+.08f*Mathf.Sin(t*4)+.06f*Mathf.Sin(t*6.47f);float radius=F("coreRadius",1.05f);Billboard(core,center,Vector3.one*radius*4.55f*env*pulse);cloud.gameObject.SetActive(t>=0);cloud.position=center;
            float n=F("nebulaRadius",8.5f)*Mathf.Max(.001f,env);
            // The proxy only has to contain the analytic sphere the march bounds
            // itself with, which REACH_MARGIN widens past the radius.
            cloud.localScale=Vector3.one*n*2*1.18f;
            volume.SetVector("_Centre",center);volume.SetFloat("_Radius",n);volume.SetFloat("_Hole",radius*env);
            // nebulaCavity is a multiplier on the horizon, not the eye itself:
            // saturate(horizon * nebulaCavity / reach). Passing the 2.5 straight
            // through opened an eye wider than the cloud and erased all of it.
            volume.SetFloat("_Cavity",Mathf.Clamp01(radius*env*F("nebulaCavity",2.5f)/Mathf.Max(.2f,n)));
            volume.SetFloat("_Churn",0);volume.SetFloat("_Drain",0);volume.SetFloat("_Fade",env);volume.SetFloat("_Seed",Slot*5.1f);
            volume.SetFloat("_Steps",Mathf.Round(F("nebulaSteps",34)));volume.SetFloat("_Density",F("nebulaDensity",2.4f)*G("shaderIntensity"));
            volume.SetFloat("_Absorb",F("nebulaAbsorb",.5f));volume.SetFloat("_Emissive",F("nebulaGlow",1.15f)*G("glow"));
            volume.SetFloat("_Scale",F("nebulaScale",.34f)*G("noiseFrequency"));volume.SetFloat("_Detail",F("nebulaDetail",2.3f));volume.SetFloat("_Filament",F("nebulaFilament",.62f));
            volume.SetFloat("_Threshold",F("nebulaThreshold",.5f));volume.SetFloat("_Edge",F("nebulaEdge",.7f));volume.SetFloat("_Flatten",F("nebulaFlatten",.6f));
            volume.SetFloat("_Arms",Mathf.Round(F("nebulaArms",3)));volume.SetFloat("_ArmSharp",F("nebulaArmSharp",1.5f));volume.SetFloat("_ArmWeight",F("nebulaArmWeight",.68f));
            volume.SetFloat("_Wind",F("nebulaWind",2.4f)*G("turbulence"));volume.SetFloat("_Twist",F("nebulaTwist",1.6f)*G("turbulence"));
            volume.SetFloat("_Spin",F("nebulaSpin",.2f)*G("noiseSpeed"));volume.SetFloat("_Rise",F("nebulaRise",.35f)*G("noiseSpeed"));
            volume.SetFloat("_Spikes",Mathf.Round(F("nebulaSpikes",9)));volume.SetFloat("_SpikeSharp",F("nebulaSpikeSharp",9));
            volume.SetFloat("_SpikeReach",F("nebulaSpikeReach",.95f));volume.SetFloat("_SpikeGlow",F("nebulaSpikeGlow",.7f));
            volume.SetFloat("_HeatFalloff",F("nebulaHeatFalloff",2.1f));volume.SetFloat("_Beam",F("nebulaBeam",.3f));volume.SetFloat("_Opacity",F("nebulaOpacity",1)*G("opacity"));
            volume.SetColor("_ColorEdge",C("colorNebulaEdge","#2e1558"));volume.SetColor("_ColorBody",C("colorNebulaBody","#8b46f0"));
            volume.SetColor("_ColorHot",C("colorNebulaHot","#ffb03a"));volume.SetColor("_ColorCore",C("colorNebulaCore","#fff3d0"));
            for(int i=0;i<56;i++){float a=H(i)*Mathf.PI*2+t*(.5f+H(i*7))/(1+Mathf.Max(0,Life-t)*.2f);float r=Mathf.Lerp(Radius*F("shardSpread",1.35f),radius*.6f,Mathf.Clamp01(t/Life))*(.4f+H(i*3));float y=(H(i*5)-.5f)*r*.9f;Vector3 p=center+new Vector3(Mathf.Cos(a)*r,y,Mathf.Sin(a)*r)*env;Instance(i,p,Quaternion.Euler(i*31+t*60,i*17+t*80,i*43),new Vector3(.14f,.3f+H(i)*.55f,.14f)*env);}
            if(t>.4f&&t<Life&&dt>0)foreach(var d in app.Dummies)if(Vector3.Distance(d.Position,Target)<Radius*1.1f||struck.Contains(d)){if(!struck.Contains(d))struck.Add(d);d.Pull(center,dt,5,true);}
        }
        void Rend(float t,float env)
        {
            float charge=F("chargeTime",1.7f),blast=Mathf.Clamp01((t-charge)/F("pillarRise",.34f))*env;float height=F("pillarHeight",30);pillar.gameObject.SetActive(t>charge);pillar.position=Target;pillar.localScale=new Vector3(F("pillarRadius",.28f)*3,height,F("pillarRadius",.28f)*3)*Mathf.Max(.001f,blast);core.gameObject.SetActive(t>charge);Billboard(core,Target+Vector3.up*height*F("starSeat",.42f),Vector3.one*F("starSize",2)*blast*2);halo.gameObject.SetActive(t>charge);halo.position=Target+Vector3.up*height*F("haloSeat",.87f)*blast;halo.rotation=Quaternion.Euler(15,t*20,10);halo.localScale=Vector3.one*F("haloRadius",1.5f)*blast;
            for(int i=0;i<30;i++){var l=strands[i];l.enabled=t>0;for(int j=0;j<60;j++){float u=j/59f,a=i*Mathf.PI*2/30+u*F("tendrilWind",1.7f)+t*.2f;float r=Radius*(1-u)*env;l.SetPosition(j,Target+new Vector3(Mathf.Cos(a)*r,u*height*.6f*Mathf.Clamp01(t/charge)*env,Mathf.Sin(a)*r));}}
            for(int i=0;i<40;i++){float f=Mathf.Repeat(t*.5f+H(i),1),a=H(i*3)*Mathf.PI*2;float r=Radius*(1-f);Vector3 p=Target+new Vector3(Mathf.Cos(a)*r,(1-f)*F("shardReach",3.4f)*2,Mathf.Sin(a)*r);Instance(i,p,Quaternion.Euler(160,a*Mathf.Rad2Deg,20),new Vector3(.2f,F("shardSize",1.7f),.15f)*env);}
            if(previousAge-travel<charge&&t>=charge){app.Shake(.7f);Burst(Target,420,8);}
        }
        public static float DistanceToSegment(Vector3 p,Vector3 a,Vector3 b){Vector3 v=b-a;float f=Mathf.Clamp01(Vector3.Dot(p-a,v)/Mathf.Max(.00001f,v.sqrMagnitude));return Vector3.Distance(p,a+v*f);}
        Mesh WallMesh()
        {
            const int sides=128,rows=16;var p=new Vector3[(sides+1)*(rows+1)];var uv=new Vector2[p.Length];var ix=new List<int>();for(int y=0;y<=rows;y++)for(int x=0;x<=sides;x++){int i=y*(sides+1)+x;float u=(float)x/sides,v=(float)y/rows,a=u*Mathf.PI*2;p[i]=new Vector3(Mathf.Cos(a),v,Mathf.Sin(a));uv[i]=new Vector2(u,v);if(x<sides&&y<rows){ix.AddRange(new[]{i,i+sides+1,i+1,i+1,i+sides+1,i+sides+2});}}var m=new Mesh{name="Cylindrical field",vertices=p,uv=uv,triangles=ix.ToArray()};m.RecalculateNormals();ownedMeshes.Add(m);return m;
        }
        Mesh PetalMesh()
        {
            const int rows=18,cols=6;var p=new Vector3[(rows+1)*(cols+1)];var uv=new Vector2[p.Length];var ix=new List<int>();for(int y=0;y<=rows;y++)for(int x=0;x<=cols;x++){int i=y*(cols+1)+x;float u=(float)y/rows,v=(float)x/cols*2-1;p[i]=new Vector3(v*Mathf.Sin(u*Mathf.PI)*.5f,.2f*Mathf.Sin(u*Mathf.PI)-v*v*.08f,u);uv[i]=new Vector2((float)x/cols,u);if(y<rows&&x<cols)ix.AddRange(new[]{i,i+1,i+cols+1,i+1,i+cols+2,i+cols+1});}var m=new Mesh{name="Petal",vertices=p,uv=uv,triangles=ix.ToArray()};m.RecalculateNormals();ownedMeshes.Add(m);return m;
        }
        public void Dispose(){sourceEruption?.Dispose();sourceCascade?.Dispose();sourceGrowth?.Dispose();foreach(var m in materials)UnityEngine.Object.Destroy(m);foreach(var mesh in ownedMeshes)UnityEngine.Object.Destroy(mesh);UnityEngine.Object.Destroy(Root);}
    }
}
