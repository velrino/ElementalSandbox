using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace ElementalSandbox
{
    // Resolves the same three populations and emergence equations as
    // VenomSurgeAbility.js / MonolithRiftAbility.js. Dice stay fixed per cast.
    public sealed class SourceEruption : IDisposable
    {
        readonly SpellVisual spell;
        readonly SandboxSettings settings;
        readonly bool rock;
        readonly Material material;
        readonly Light localLight;
        readonly Matrix4x4[][] matrices = new Matrix4x4[6][];
        readonly Vector4[][] states = new Vector4[6][];
        readonly int[] counts = new int[6];
        readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        readonly Record[] records = new Record[600];
        readonly SourceEruptionParticles particles;
        readonly SourceShatterPlate plate;
        readonly Vector3[] debrisPosition=new Vector3[200],debrisVelocity=new Vector3[200];
        readonly float[] debrisSize=new float[200];
        readonly Matrix4x4[] debrisMatrices=new Matrix4x4[200];
        bool launched;int debrisCount;
        int activeCount;
        float previousAge;
        struct Record { public float along,lateral,radial,angle,yaw,stagger,height,radius,lean,scatter,bearing,seed; public int tier,variant; public bool cluster,breached; }
        float F(string key,float fallback=0)=>spell.F(key,fallback);
        float G(string key)=>settings.F("global."+key,1);
        static float Smooth(float a,float b,float x){float u=Mathf.InverseLerp(a,b,x);return u*u*(3-2*u);}
        public SourceEruption(SpellVisual spell,SandboxSettings settings)
        {
            this.spell=spell;this.settings=settings;rock=spell.Id=="quake";
            material=new Material(Shader.Find("Elemental/"+(rock?"QuakeSource":"VenomSource"))){enableInstancing=true};
            if(rock){string[] maps={"Albedo","Normal","Rough","AO"},files={"color","normal","roughness","ao"};for(int i=0;i<4;i++)material.SetTexture("_"+maps[i]+"Map",Resources.Load<Texture2D>("Elemental/textures/cathedral/"+files[i]));}
            for(int i=0;i<6;i++){matrices[i]=new Matrix4x4[600];states[i]=new Vector4[600];}
            var lightObject=new GameObject("Source eruption light");lightObject.transform.SetParent(spell.Root.transform,false);localLight=lightObject.AddComponent<Light>();localLight.type=LightType.Point;localLight.shadows=LightShadows.None;
            particles=new SourceEruptionParticles(spell,settings);plate=new SourceShatterPlate(spell,settings);
        }
        public void Spawn()
        {
            var random=new System.Random(127+spell.Slot*7919);
            Func<float> dice=()=> (float)random.NextDouble();
            Func<float> signed=()=>dice()*2-1;
            activeCount=Mathf.Clamp(Mathf.RoundToInt(F(rock?"stoneCount":"gemCount")*F("density",1)),1,600);
            int spine=activeCount-Mathf.RoundToInt(activeCount*Mathf.Clamp01(F(rock?"blastShare":"burstShare")));
            for(int i=0;i<activeCount;i++)
            {
                var r=new Record {cluster=i>=spine,yaw=dice()*Mathf.PI*2,stagger=dice(),height=signed(),radius=signed(),lean=signed(),scatter=signed(),bearing=signed(),seed=dice(),variant=i%4};
                if(r.cluster)
                {
                    r.angle=dice()*Mathf.PI*2;r.along=1;float roll=dice(),hero=F(rock?"monolithShare":"spearShare"),small=F(rock?"blockShare":"shardShare");
                    r.tier=roll<hero?0:roll<hero+small?2:1;
                    r.radial=r.tier==0?Mathf.Lerp(rock?.05f:.04f,rock?.5f:.42f,dice()):r.tier==2?Mathf.Lerp(rock?.5f:.45f,rock?1.15f:1,Mathf.Sqrt(dice())):Mathf.Sqrt(dice())*(rock?.95f:.88f);
                }
                else {r.tier=dice()<F("rubble")?2:1;r.along=Mathf.Pow((i+dice())/Mathf.Max(1,spine),F("frontBias",1));r.lateral=signed();}
                if(rock)r.variant=r.tier==0?(dice()<.7f?0:1):r.tier==2?(dice()<.5f?3:4):1+Mathf.Min(2,(int)(dice()*3));
                records[i]=r;
            }
            previousAge=0;launched=false;particles.Reset();
        }
        public void Tick(float age,float travel,float retract)
        {
            Array.Clear(counts,0,6);
            localLight.transform.position=(age<travel?spell.Focus:spell.Target)+Vector3.up*(rock?1.5f:F("coreHeight",1.15f));localLight.color=spell.C("lightColor");localLight.range=F("lightRadius",15);localLight.intensity=F("lightIntensity")*G("lightIntensity")*(1-retract)*(rock?Mathf.Lerp(1,F("lightSettle",.5f),Mathf.Clamp01(age-travel)):1);
            float dt=Mathf.Max(0,age-previousAge),randomness=G("randomness");
            Vector3 side=Vector3.Cross(spell.Direction,Vector3.up);
            foreach(var e in settings.Entries)
            {
                string prefix=spell.Id+".";if(!e.key.StartsWith(prefix))continue;
                string key="_"+e.key.Substring(prefix.Length);if(!material.HasProperty(key))continue;
                if(e.kind=="number")material.SetFloat(key,e.number);else if(e.kind=="string"&&ColorUtility.TryParseHtmlString(e.text,out var color))material.SetColor(key,color);
            }
            material.SetFloat("_Age",Mathf.Max(0,age-travel));material.SetFloat("_Opacity",G("opacity"));material.SetFloat("_GlobalGlow",G("glow"));material.SetVector("_Core",spell.Target+Vector3.up*F("coreHeight",1.15f));
            for(int i=0;i<activeCount;i++)
            {
                var r=records[i];float elapsed=age-travel*r.along-r.stagger*F("riseStagger")-(r.cluster?r.radial*F(rock?"blastStagger":"burstStagger"):0);if(elapsed<0)continue;
                float lateral=Mathf.Sign(r.lateral)*Mathf.Pow(Mathf.Abs(r.lateral),F("clumping",1))+r.scatter*F("scatter");
                Vector3 radial=new Vector3(Mathf.Cos(r.angle),0,Mathf.Sin(r.angle));
                Vector3 tang=spell.Direction,outw=side;Vector3 p=r.cluster?spell.Target+radial*(spell.Ring?spell.RingRadius*(.75f+.45f*r.radial):spell.Radius*r.radial):spell.PathPoint(r.along,lateral,Mathf.Lerp(F("widthNear"),F("width"),Mathf.Pow(r.along,F("widthCurve",1))),out tang,out outw);
                float h=r.cluster?F(rock?"blastHeight":"burstHeight")*Mathf.LerpUnclamped(1,1-Mathf.Clamp01(F("crown")),Mathf.Pow(r.radial,rock?1.25f:1.3f)):Mathf.Lerp(F("heightNear"),F("height"),Mathf.Pow(r.along,F("heightCurve",1)))*(1+(F("peak",1)-1)*Smooth(1-F("peakWidth"),1,r.along))*Mathf.Lerp(1,1-Mathf.Clamp01(F("crown")),Mathf.Pow(Mathf.Clamp01(Mathf.Abs(lateral)),1.4f));
                if(r.cluster&&r.tier==0)h*=F(rock?"monolithScale":"spearScale",1);else if(r.tier==2)h*=F(rock?"blockScale":"shardScale",1);
                h=Mathf.Max(.02f,h*(1+r.height*F("heightJitter")*randomness));
                float radius=F("radius")*(r.cluster?Mathf.LerpUnclamped(rock?1.2f:1.15f,rock?.72f:.7f,r.radial):Mathf.Lerp(.7f,rock?1.15f:1.1f,Mathf.Pow(r.along,.6f)));
                if(r.cluster&&r.tier==0)radius*=F(rock?"monolithGirth":"spearSlim",1);else if(r.tier==2)radius*=r.cluster?(rock?1.55f:1.5f):(rock?1.5f:1.45f);
                radius=Mathf.Max(.01f,radius*(1+r.radius*F("radiusJitter")*randomness));
                float bearing=r.angle+(rock?r.bearing*F("blastLeanScatter"):0);
                Vector3 lean=r.cluster?new Vector3(Mathf.Cos(bearing),0,Mathf.Sin(bearing)):(tang*(rock?-.55f:.7f)+outw*lateral*(rock?1:.9f)).normalized;
                float outward=r.radial*(r.tier==0?(rock?.5f:.55f):1);
                float angle=r.cluster?F(rock?"blastLean":"burstLean")*Mathf.Pow(rock?Mathf.Clamp01(outward):outward,F(rock?"blastLeanCurve":"burstLeanCurve",1)):F("lean")*Mathf.Lerp(rock?.35f:.3f,1,r.along);
                angle*=1+r.lean*F("leanJitter")*randomness;
                float rise=Mathf.Max(.02f,F("riseTime")),overshoot=F("riseOvershoot"),drop=Mathf.Clamp01((elapsed-rise)/Mathf.Max(.05f,F("settle")));
                float emerge=elapsed<=rise?(1-Mathf.Pow(1-elapsed/rise,5))*(1+overshoot):1+overshoot-overshoot*drop*drop;
                if(!r.breached&&emerge>.22f){r.breached=true;records[i]=r;particles.Breach(p,radius,r.seed);}
                p.y=(emerge-1)*h*(rock?.92f:.85f)-Mathf.Pow(retract,3)*(h+radius+(rock?.6f:.5f));
                Quaternion q=Quaternion.AngleAxis(angle*Mathf.Rad2Deg,Vector3.Cross(Vector3.up,lean).normalized)*Quaternion.AngleAxis(r.yaw*F("twist",1)*Mathf.Rad2Deg,Vector3.up);
                int v=r.variant,k=counts[v]++;matrices[v][k]=Matrix4x4.TRS(p,q,new Vector3(radius,h,radius)*Mathf.Lerp(rock?.9f:.84f,1,Mathf.Min(1,emerge)));
                states[v][k]=new Vector4(r.seed,Mathf.Clamp01(1-elapsed/Mathf.Max(.02f,F("birthFade",.22f))),r.seed*3,0);
            }
            for(int v=0;v<6;v++)if(counts[v]>0){block.SetVectorArray("_EruptionState",states[v]);Graphics.DrawMeshInstanced(MeshLibrary.Get((rock?"monolith":"crystal")+v),0,material,matrices[v],counts[v],block,ShadowCastingMode.On,true);}
            plate.Tick(age-travel,retract);
            if(rock&&age>=travel){
                if(!launched){launched=true;debrisCount=Mathf.Clamp((int)F("shrapnelCount",70),0,200);for(int i=0;i<debrisCount;i++){float a=i*2.399963f;float dice=records[i%activeCount].seed;debrisPosition[i]=spell.Target+Vector3.up*.2f;debrisVelocity[i]=new Vector3(Mathf.Cos(a),F("shrapnelLift")*Mathf.Lerp(.5f,1.5f,dice),Mathf.Sin(a))*F("shrapnelSpeed")*Mathf.Lerp(.35f,1,dice);debrisSize[i]=F("shrapnelSize")*(1+(dice*2-1)*F("shrapnelSizeJitter"));}}
                for(int i=0;i<debrisCount;i++){float remaining=Mathf.Min(dt,.1f);while(remaining>0){float step=Mathf.Min(remaining,.016f);remaining-=step;debrisVelocity[i].y+=F("shrapnelGravity")*step;debrisPosition[i]+=debrisVelocity[i]*step;if(debrisPosition[i].y<debrisSize[i]*.3f){debrisPosition[i].y=debrisSize[i]*.3f;debrisVelocity[i].y=Mathf.Abs(debrisVelocity[i].y)*F("shrapnelBounce");debrisVelocity[i].x*=F("shrapnelFriction");debrisVelocity[i].z*=F("shrapnelFriction");}}
                    float spin=debrisVelocity[i].sqrMagnitude>.5f?(age-travel)*F("shrapnelSpin")*Mathf.Rad2Deg:0;debrisMatrices[i]=Matrix4x4.TRS(debrisPosition[i]-Vector3.up*retract,Quaternion.Euler(i*17+spin,i*43+spin*.7f,i*61),Vector3.one*debrisSize[i]);}
                if(debrisCount>0)Graphics.DrawMeshInstanced(MeshLibrary.Get("debris0"),0,material,debrisMatrices,debrisCount,null,ShadowCastingMode.On,true);
            }
            particles.Tick(age,travel,dt,retract);previousAge=age;
        }
        public void Dispose(){particles.Dispose();plate.Dispose();UnityEngine.Object.Destroy(material);}
    }
}
