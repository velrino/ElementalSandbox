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
        SandboxSettings S=>app.Settings;
        readonly List<Material> materials=new List<Material>();
        readonly List<Mesh> ownedMeshes=new List<Mesh>();
        readonly List<Transform> parts=new List<Transform>();
        readonly List<LineRenderer> strands=new List<LineRenderer>();
        readonly List<Matrix4x4>[] instances=new List<Matrix4x4>[6];
        readonly Matrix4x4[] drawBuffer=new Matrix4x4[512];
        SourceEruption sourceEruption;
        Material stone,energy,pool,volume,sparkMat;
        Transform seed,disc,core,cloud,crown,shock,pillar,halo;
        ParticleSystem particles;
        readonly List<SandboxDummy> struck=new List<SandboxDummy>();
        float nextShot,travel,previousAge;
        int shots;
        bool impacted;
        public float F(string key,float fallback=0)=>S.F(Id+"."+key,fallback);
        public Color C(string key,string fallback="#ffffff")=>S.C(Id+"."+key,fallback);
        public float Radius=>S.Abilities[Slot].zone?F("zoneRadius",4):F("burstRadius",F("blastRadius",3));
        public Vector3 Focus=>Vector3.Lerp(Origin,Target,Mathf.Clamp01(Age/Mathf.Max(.01f,travel)));
        public float Life=>Id=="cyber"?F("shatterTime",.75f)+F("fadeTime",.85f):F("lifetime",5)*S.F("global.lifetime",1);
        public float Fade=>F("fadeTime",F("sinkTime",1));
        public float Envelope {get {float a=Age-travel;return Mathf.Clamp01(a/.4f)*(1-Mathf.Clamp01((a-Life)/Mathf.Max(.01f,Fade)));}}
        float H(float n)=>Mathf.Repeat(Mathf.Sin(n*127.1f+Slot*311.7f)*43758.5453f,1);
        public SpellVisual(ElementalApp owner,int slot)
        {
            app=owner;Slot=slot;Id=S.Abilities[slot].id;Root=new GameObject(S.Abilities[slot].hint);Root.transform.SetParent(app.EffectsRoot,false);
            for(int i=0;i<6;i++)instances[i]=new List<Matrix4x4>();
            Build();if(Id=="venom"||Id=="quake")sourceEruption=new SourceEruption(this,S);Root.SetActive(false);
        }
        Material Mat(string shader,Color color,float mode=0,float glow=1)
        {
            var m=new Material(Shader.Find("Elemental/"+shader)){name=Id+" "+shader,enableInstancing=true};m.SetColor("_BaseColor",color);m.SetColor("_HotColor",Color.white);m.SetColor("_EdgeColor",S.Abilities[Slot].Color);m.SetFloat("_Mode",mode);m.SetFloat("_Glow",glow);m.SetFloat("_Opacity",1);materials.Add(m);return m;
        }
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
            if(Id=="venom"||Id=="quake"||Id=="ward"||Id=="astral"||Id=="cascade"||Id=="rend")
            {
                stone=Mat("Surface",Id=="quake"?new Color(.43f,.4f,.34f):Id=="ward"?new Color(.055f,.024f,.023f):Id=="venom"?C("colorBody","#653399"):Id=="cascade"?C("colorBladeBody","#08252f"):C("colorShardBody","#1b2436"),Id=="quake"?1:Id=="ward"?2:0,.7f);
                stone.SetColor("_HotColor",(Id=="venom"?C("colorVenom","#8aff28"):accent));stone.SetTexture("_MainTex",Resources.Load<Texture2D>("Elemental/textures/cathedral/color"));
            }
            if(Id=="venom")core=Plane("Venom core",Mat("Energy",C("colorCore","#baff4a"),5,1.4f));
            if(Id=="ward")crown=MeshPart("Runed barrier",WallMesh(),Mat("Energy",accent,3,1));
            if(Id=="acid"||Id=="astral"||Id=="quake"||Id=="venom")
            {
                volume=Mat("Volume",Id=="acid"?C("colorMistBody","#4f8f1c"):Id=="astral"?new Color(.26f,.045f,.65f):Id=="venom"?new Color(.18f,.34f,.025f):new Color(.32f,.29f,.24f),Id=="astral"?1:Id=="quake"?2:0,.8f);volume.SetColor("_HotColor",(Id=="astral"?new Color(1,.5f,.09f):accent));cloud=Primitive("Raymarched volume",PrimitiveType.Cube,volume);
            }
            if(Id=="astral")core=Plane("Event horizon",Mat("Horizon",new Color(1,.68f,.26f),0,1.8f));
            if(Id=="growth")
            {
                stone=Mat("Surface",new Color(.075f,.15f,.075f),2,.7f);stone.SetColor("_HotColor",accent);core=Primitive("Arcane heart",PrimitiveType.Sphere,energy);
                for(int i=0;i<14;i++)Line("Growing vine",stone,40,.09f);
                var leaf=PetalMesh();for(int i=0;i<72;i++)parts.Add(MeshPart(i<36?"Bloom petal":"Foliage",leaf,stone));
                Line("Target lance",Mat("Energy",accent,6,4),20,.12f);
            }
            if(Id=="cascade")
            {
                core=Primitive("Crown heart",PrimitiveType.Sphere,energy);for(int i=0;i<28;i++)Line("Rising wisp",Mat("Energy",accent,6,.7f),40,.045f);for(int i=0;i<8;i++)Line("Thrown blade trail",energy,16,.07f);
            }
            if(Id=="ink")
            {
                crown=MeshPart("Water crown",WallMesh(),Mat("Energy",accent*.65f,3,.7f));
                volume=Mat("Volume",new Color(.015f,.05f,.055f),0,.2f);volume.SetColor("_HotColor",accent*.2f);cloud=Primitive("Ink water volume",PrimitiveType.Cube,volume);for(int i=0;i<12;i++)Line("Water tendril",Mat("Energy",accent,6,.6f),40,.05f);
            }
            if(Id=="cyber")
            {
                stone=Mat("Surface",new Color(.008f,.035f,.04f),3,1.5f);stone.SetColor("_HotColor",accent);core=MeshPart("Original serpent mesh",MeshLibrary.Get("serpent"),stone);for(int i=0;i<18;i++)Line("Neon ribbon",Mat("Energy",i%3==0?new Color(1,.12f,.65f):accent,6,1),64,.035f);
            }
            if(Id=="rend")
            {
                pillar=MeshPart("Thirty metre pillar",WallMesh(),Mat("Energy",accent,4,1.2f));core=Plane("Judgment star",Mat("Energy",accent,7,2));halo=MeshPart("Orbiting halo",MeshLibrary.Ring(128,.98f),Mat("Energy",accent,6,2));for(int i=0;i<30;i++)Line("Charging tendril",Mat("Energy",i%4==0?new Color(.55f,.7f,1):accent,6,.5f),60,.012f);
            }
        }
        public void Spawn(Vector3 origin,Vector3 target)
        {
            Origin=origin;Target=target;Distance=Vector3.Distance(origin,target);Direction=(target-origin).normalized;if(Direction.sqrMagnitude<.01f)Direction=Vector3.forward;Age=previousAge=0;travel=Distance/Mathf.Max(.1f,F("speed",50)*S.F("global.speed",1));Active=true;impacted=false;shots=0;nextShot=1.7f;struck.Clear();Root.SetActive(true);particles.Clear();foreach(var l in strands)l.enabled=false;sourceEruption?.Spawn();
        }
        public void Retire(){Active=false;Root.SetActive(false);particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);}
        void Burst(Vector3 pos,int count,float speed=4)
        {
            count=Mathf.Clamp(Mathf.RoundToInt(count*S.F("global.particleCount",1)),0,1200);for(int i=0;i<count;i++){float a=H(i+Age*8)*Mathf.PI*2,r=H(i*1.7f+Age);var e=new ParticleSystem.EmitParams{position=pos,velocity=new Vector3(Mathf.Cos(a)*r,.4f+H(i*3.1f)*1.4f,Mathf.Sin(a)*r)*speed,startLifetime=.5f+H(i*1.3f)*1.5f,startSize=(.025f+H(i*2.3f)*.075f)*S.F("global.particleSize",1)};particles.Emit(e,1);}
        }
        public void Tick(float dt)
        {
            if(!Active)return;previousAge=Age;Age+=dt;float t=Age-travel,env=Envelope;
            if(t>Life+Fade){Retire();return;}
            foreach(var m in materials){m.SetFloat("_Age",Mathf.Max(0,t));m.SetFloat("_Opacity",env*S.F("global.opacity",1));}
            energy.SetFloat("_Glow",1.3f*S.F("global.glow",1));pool.SetFloat("_Glow",.9f*S.F("global.glow",1));
            Billboard(seed,Focus+Vector3.up*(Id=="astral"?F("coreHeight",3.4f):1.1f),Vector3.one*.65f);seed.gameObject.SetActive(t<0);energy.SetFloat("_Opacity",t<0?1:env);
            Floor(disc,Target,Radius*Mathf.Max(.001f,env));disc.gameObject.SetActive(t>=0&&Id!="cyber"&&Id!="quake"&&Id!="venom");
            shock.gameObject.SetActive(t>=0&&t<1.2f);shock.position=Target+Vector3.up*.07f;shock.localScale=Vector3.one*Mathf.Max(.01f,Radius*(1+t*1.7f));shock.GetComponent<Renderer>().sharedMaterial.SetFloat("_Opacity",Mathf.Clamp01(1-t/1.2f));
            if(t>=0&&!impacted){impacted=true;if(sourceEruption==null)Burst(Target+Vector3.up*.3f,180,4);app.Shake(Id=="quake"?.5f:.18f);}
            if(dt>0){particles.Simulate(dt,false,false);if(sourceEruption==null&&t>0&&t<Life&&Mathf.FloorToInt(Age*18)!=Mathf.FloorToInt(previousAge*18))Burst(Target+new Vector3((H(Age)-.5f)*Radius, .2f,(H(Age*2)-.5f)*Radius),4,.5f);}
            foreach(var list in instances)list.Clear();
            if(Id=="venom"||Id=="quake")Eruption(t,env);
            else if(Id=="ward")Ward(t,env);
            else if(Id=="acid")Acid(t,env);
            else if(Id=="growth")Growth(t,env);
            else if(Id=="cyber")Cyber(t,env);
            else if(Id=="ink")Ink(t,env,dt);
            else if(Id=="astral")Astral(t,env,dt);
            else if(Id=="cascade")Cascade(t,env);
            else if(Id=="rend")Rend(t,env);
            if(stone!=null){stone.SetFloat("_Opacity",Id=="cyber"?1-Mathf.Clamp01(t/Fade):Mathf.Max(env,Id=="venom"||Id=="quake"?Mathf.Clamp01(Age/.2f)*(1-Mathf.Clamp01((t-Life)/Fade)):0));for(int k=0;k<6;k++){int count=instances[k].Count;if(count==0)continue;instances[k].CopyTo(drawBuffer);Graphics.DrawMeshInstanced(MeshLibrary.Get((Id=="quake"||Id=="ward"?"monolith":Id=="cascade"||Id=="rend"?"shard":"crystal")+k),0,stone,drawBuffer,count,null,ShadowCastingMode.On,true);}}
            if(dt>0&&(t>=0||!S.Abilities[Slot].zone)&&t<Life&&Id!="growth"&&Id!="cascade"&&Id!="astral"&&Id!="ink"&&(Id!="rend"||t>=F("chargeTime",1.7f)))
            foreach(var d in app.Dummies){if(!d.Alive||struck.Contains(d))continue;bool hit;if(S.Abilities[Slot].zone)hit=Vector3.Distance(d.Position,Target)<Radius;else{Vector3 end=Origin+Direction*Mathf.Min(Distance,Age*F("speed",23));hit=DistanceToSegment(d.Position,Origin,end)<S.F("dummies.hit.radius",1.5f);}if(hit){d.Hit(Direction*S.F("dummies.hit.impulse",6)+Vector3.up*S.F("dummies.hit.lift",3.4f));struck.Add(d);}}
        }
        void Instance(int i,Vector3 p,Quaternion r,Vector3 scale){if(scale.y>.001f&&instances[i%6].Count<512)instances[i%6].Add(Matrix4x4.TRS(p,r,scale));}
        void Eruption(float t,float env)
        {
            bool rock=Id=="quake";if(core!=null){core.gameObject.SetActive(t>=0);Billboard(core,Target+Vector3.up*F("coreHeight",1.15f),Vector3.one*env*2.4f);}
            cloud.gameObject.SetActive(false);
            sourceEruption.Tick(Age,travel,Mathf.Clamp01((t-Life)/Mathf.Max(.01f,Fade)));
        }

        void Ward(float t,float env)
        {
            crown.gameObject.SetActive(t>=0);crown.position=Target;crown.localScale=new Vector3(Radius*env,F("height",4.4f)*env,Radius*env);float pulse=1+Mathf.Sin(t*F("bpm",72)*Mathf.PI/30)*F("beatDepth",.15f)*.1f;
            for(int i=0;i<24;i++){float a=i*Mathf.PI*2/24;float h=F("height",4.4f)*(.28f+H(i)*.65f)*env;Instance(i,Target+new Vector3(Mathf.Cos(a)*Radius,0,Mathf.Sin(a)*Radius)*env,Quaternion.Euler(0,-a*Mathf.Rad2Deg,12),new Vector3(.65f,h,.45f)*pulse);}
            pool.SetColor("_BaseColor",C("colorLava","#ff320a"));pool.SetFloat("_Glow",1.4f*S.F("global.glow",1)*pulse);
        }
        void Acid(float t,float env)
        {
            cloud.gameObject.SetActive(t>=0);float h=F("mistHeight",5.2f);cloud.position=Target+Vector3.up*h*.5f*env;cloud.localScale=new Vector3(Radius*2,h,Radius*2)*Mathf.Max(.001f,env);volume.SetFloat("_Density",F("mistDensity",2.9f));volume.SetColor("_BaseColor",C("colorMistBody","#4f8f1c"));volume.SetColor("_HotColor",C("colorMistLight","#93a862"));pool.SetColor("_BaseColor",C("colorAcid","#8fff1e"));
        }
        void Growth(float t,float env)
        {
            float grow=Mathf.Clamp01((t-F("vineDelay",.18f))/F("vineTime",1.15f))*env;float bloom=Mathf.Clamp01((t-F("bloomDelay",.85f))/F("bloomTime",1.05f))*env;Vector3 center=Target+Vector3.up*F("bloomHeight",3.2f)*grow;core.position=center;core.localScale=Vector3.one*.5f*bloom;
            for(int i=0;i<14;i++){var l=strands[i];l.enabled=t>0;l.widthMultiplier=.05f+.1f*grow;for(int j=0;j<40;j++){float u=j/39f,a=i*Mathf.PI*2/14+u*2.2f;float r=Radius*F("vineSpread",.75f)*(1-u)*grow;Vector3 p=Target+new Vector3(Mathf.Cos(a)*r,F("vineHeight",3.2f)*u*grow,Mathf.Sin(a)*r);l.SetPosition(j,p);}}
            for(int i=0;i<parts.Count;i++){float a=(i%12)*Mathf.PI*2/12+(i/12)*.35f;var p=parts[i];if(i<36){float layer=i/12;float size=F("bloomScale",2.1f)*(1-layer*.22f)*bloom;p.position=center;p.rotation=Quaternion.Euler(Mathf.Lerp(-12,60-layer*18,bloom),a*Mathf.Rad2Deg,0);p.localScale=new Vector3(.55f,1,1)*size;}else{float u=H(i),r=Radius*(1-u)*.6f;p.position=Target+new Vector3(Mathf.Cos(a)*r,F("vineHeight",3.2f)*u*grow,Mathf.Sin(a)*r);p.rotation=Quaternion.Euler(25,a*Mathf.Rad2Deg,0);p.localScale=new Vector3(.3f,1,.6f)*grow;}}
            var laser=strands[14];laser.enabled=false;if(t>nextShot&&t<Life){var d=app.Nearest(Target,Radius*1.8f);if(d!=null){nextShot=t+1.35f;shots++;shotTarget=d.Position+Vector3.up;d.Cut((d.Position-Target).normalized*7+Vector3.up*2);Burst(shotTarget,80,3);}else nextShot=t+.3f;}
            float shotAge=t-(nextShot-1.35f);if(shots>0&&shotAge>=0&&shotAge<.38f){laser.enabled=true;laser.widthMultiplier=.13f*(1-shotAge/.38f);for(int j=0;j<20;j++){float u=j/19f;laser.SetPosition(j,Vector3.Lerp(center,shotTarget,u)+Vector3.up*Mathf.Sin(u*Mathf.PI)*.15f);}}
        }
        Vector3 shotTarget;
        void Cyber(float t,float env)
        {
            float flight=Mathf.Min(Distance,Age*F("speed",22));Vector3 pos=Origin+Direction*flight+Vector3.up*F("launchHeight",1.35f);core.position=pos;core.rotation=Quaternion.LookRotation(-Direction);core.localScale=Vector3.one*F("bodyLength",5)*Mathf.Clamp01(Age/F("formTime",.22f))*(1-Mathf.Clamp01(t/F("shatterTime",.75f)));Vector3 side=Vector3.Cross(Direction,Vector3.up);
            for(int i=0;i<18;i++){var l=strands[i];l.enabled=Age>.02f&&t<F("fadeTime",.85f);l.widthMultiplier=F("trailWidth",.06f)*(1-Mathf.Clamp01(t/F("fadeTime",.85f)));for(int j=0;j<64;j++){float u=j/63f;float behind=Mathf.Min(flight,F("trailLength",9))*u;float a=u*9-Age*5+i*Mathf.PI*2/18;float r=F("trailRadius",.85f)*Mathf.Sin(u*Mathf.PI)*(.3f+H(i));l.SetPosition(j,pos-Direction*behind+side*Mathf.Cos(a)*r+Vector3.up*Mathf.Sin(a)*r);}}
        }
        void Ink(float t,float env,float dt)
        {
            float swell=.85f+.15f*Mathf.Sin(t*2)+.12f*Mathf.Sin(t*3.236f);crown.gameObject.SetActive(t>=0);crown.position=Target;crown.localScale=new Vector3(Radius,F("crownHeight",1.85f)*swell,Radius)*env;cloud.gameObject.SetActive(t>=0);cloud.position=Target+Vector3.up*F("columnHeight",3.6f)*env*.5f;cloud.localScale=new Vector3(Radius*1.5f,F("columnHeight",3.6f),Radius*1.5f)*Mathf.Max(.001f,env);volume.SetFloat("_Density",4);pool.SetColor("_BaseColor",new Color(.008f,.035f,.04f));
            for(int i=0;i<12;i++){var l=strands[i];l.enabled=t>=0;for(int j=0;j<40;j++){float u=j/39f,a=i*Mathf.PI/6+u*3+t*.35f;float r=Radius*(1-u*.7f)*env;l.SetPosition(j,Target+new Vector3(Mathf.Cos(a)*r,Mathf.Sin(u*Mathf.PI)*F("wispHeight",3.2f)*env,Mathf.Sin(a)*r));}}
            if(t>.4f&&t<Life&&dt>0)foreach(var d in app.Dummies)if(Vector3.Distance(d.Position,Target)<Radius)d.Pull(Target-Vector3.up*1.6f,dt,4,false);
        }
        void Astral(float t,float env,float dt)
        {
            Vector3 center=Target+Vector3.up*F("coreHeight",3.4f)*env;core.gameObject.SetActive(t>=0);float pulse=1+.08f*Mathf.Sin(t*4)+.06f*Mathf.Sin(t*6.47f);float radius=F("coreRadius",1.05f);Billboard(core,center,Vector3.one*radius*4.55f*env*pulse);cloud.gameObject.SetActive(t>=0);cloud.position=center;float n=F("nebulaRadius",8.5f);cloud.localScale=new Vector3(n*2,n*.75f,n*2)*Mathf.Max(.001f,env);volume.SetFloat("_Density",3.5f);volume.SetFloat("_Glow",S.F("global.glow",1));
            for(int i=0;i<56;i++){float a=H(i)*Mathf.PI*2+t*(.5f+H(i*7))/(1+Mathf.Max(0,Life-t)*.2f);float r=Mathf.Lerp(Radius*F("shardSpread",1.35f),radius*.6f,Mathf.Clamp01(t/Life))*(.4f+H(i*3));float y=(H(i*5)-.5f)*r*.9f;Vector3 p=center+new Vector3(Mathf.Cos(a)*r,y,Mathf.Sin(a)*r)*env;Instance(i,p,Quaternion.Euler(i*31+t*60,i*17+t*80,i*43),new Vector3(.14f,.3f+H(i)*.55f,.14f)*env);}
            if(t>.4f&&t<Life&&dt>0)foreach(var d in app.Dummies)if(Vector3.Distance(d.Position,Target)<Radius*1.1f||struck.Contains(d)){if(!struck.Contains(d))struck.Add(d);d.Pull(center,dt,5,true);}
        }
        void Cascade(float t,float env)
        {
            Vector3 center=Target+Vector3.up*F("crownHeight",3.1f)*env;core.position=center;core.localScale=Vector3.one*.6f*env;
            for(int i=0;i<56;i++){float a=H(i)*Mathf.PI*2,b=Mathf.Acos(H(i*1.7f)*2-1);Vector3 v=new Vector3(Mathf.Sin(b)*Mathf.Cos(a),Mathf.Cos(b),Mathf.Sin(b)*Mathf.Sin(a));float scale=F("crownScale",1.65f);float length=(.6f+H(i*3.9f)*1.2f)*scale;Instance(i,center+v*.2f,Quaternion.FromToRotation(Vector3.up,v),new Vector3(F("bladeWidth",.085f)*4,length,F("bladeWidth",.085f)*2)*env);}
            for(int i=0;i<28;i++){var l=strands[i];l.enabled=t>=0;for(int j=0;j<40;j++){float u=j/39f,a=i*Mathf.PI*2/28+u*2.3f+t*.15f;float r=Radius*(1-u)*env;l.SetPosition(j,Target+new Vector3(Mathf.Cos(a)*r,F("wispHeight",4.4f)*u*env,Mathf.Sin(a)*r));}}
            if(t>nextShot&&t<Life){var d=app.Nearest(Target,Radius*2);if(d!=null){nextShot=t+1.4f;shotTarget=d.Position+Vector3.up;shots++;d.Hit((d.Position-Target).normalized*8+Vector3.up*2);Burst(shotTarget,80,5);}else nextShot=t+.3f;}
            for(int i=28;i<36;i++){var l=strands[i];float age=t-(nextShot-1.4f)-(i-28)*.07f;l.enabled=shots>0&&age>0&&age<.45f;if(l.enabled){float f=Mathf.Clamp01(age/.34f);for(int j=0;j<16;j++){float u=Mathf.Clamp01(f-j/15f*.3f);l.SetPosition(j,Vector3.Lerp(center,shotTarget,u)+Vector3.up*Mathf.Sin(u*Mathf.PI)*.5f);}Instance(i,Vector3.Lerp(center,shotTarget,f),Quaternion.FromToRotation(Vector3.up,shotTarget-center),new Vector3(.12f,1.45f,.08f));}}
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
        public void Dispose(){sourceEruption?.Dispose();foreach(var m in materials)UnityEngine.Object.Destroy(m);foreach(var mesh in ownedMeshes)UnityEngine.Object.Destroy(mesh);UnityEngine.Object.Destroy(Root);}
    }
}
