using UnityEngine;
namespace ElementalSandbox
{
    // Separate suspended aerosol and ballistic spray, with the source palettes,
    // lifetimes and gravity; cement dust falls while venom gas rises.
    public sealed class SourceEruptionParticles:System.IDisposable
    {
        readonly SpellVisual spell;readonly SandboxSettings settings;readonly bool rock;
        readonly ParticleSystem smoke,drops;readonly Material smokeMaterial,dropMaterial;
        float carry;bool burst;
        float F(string key,float fallback=0)=>spell.F(key,fallback);
        float G(string key)=>settings.F("global."+key,1);
        public SourceEruptionParticles(SpellVisual spell,SandboxSettings settings)
        {
            this.spell=spell;this.settings=settings;rock=spell.Id=="quake";
            smokeMaterial=new Material(Shader.Find("Elemental/SourceSmoke"));smokeMaterial.SetFloat("_SoftFade",rock?1.6f:.6f);smokeMaterial.SetFloat("_Lit",rock?1:0);
            dropMaterial=new Material(Shader.Find("Elemental/Energy"));dropMaterial.SetFloat("_Mode",5);dropMaterial.SetFloat("_Opacity",1);dropMaterial.SetFloat("_Glow",rock?.25f:F("dropGlow",1));dropMaterial.SetColor("_BaseColor",Color.white);dropMaterial.SetColor("_HotColor",Color.white);
            smoke=Make("Source breach aerosol",smokeMaterial,true);drops=Make("Source ballistic spray",dropMaterial,false);
        }
        ParticleSystem Make(string name,Material material,bool aerosol)
        {
            var go=new GameObject(name);go.transform.SetParent(spell.Root.transform,false);var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main;main.playOnAwake=false;main.loop=false;main.duration=30;main.maxParticles=aerosol?5200:1600;main.simulationSpace=ParticleSystemSimulationSpace.World;
            var emission=ps.emission;emission.enabled=false;var shape=ps.shape;shape.enabled=false;
            var color=ps.colorOverLifetime;color.enabled=true;string prefix=aerosol?(rock?"colorDust":"colorGas"):(rock?"colorGrit":"colorDrop");var grad=new Gradient();grad.SetKeys(new[]{new GradientColorKey(spell.C(prefix+"A"),0),new GradientColorKey(spell.C(prefix+"B"),.25f),new GradientColorKey(spell.C(prefix+"C"),.65f),new GradientColorKey(spell.C(prefix+"D"),1)},new[]{new GradientAlphaKey(aerosol?0:1,0),new GradientAlphaKey(1,.12f),new GradientAlphaKey(.7f,.55f),new GradientAlphaKey(0,1)});color.color=grad;
            if(aerosol){var noise=ps.noise;noise.enabled=true;noise.strength=F(rock?"dustTurbulence":"gasTurbulence")*G("turbulence");noise.frequency=.65f;noise.scrollSpeed=.3f;noise.quality=ParticleSystemNoiseQuality.Medium;var size=ps.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,0),new Keyframe(.06f,1),new Keyframe(1,F(rock?"dustSpread":"gasSpread"))));var limit=ps.limitVelocityOverLifetime;limit.enabled=true;limit.drag=F(rock?"dustDrag":"gasDrag",rock?1.6f:.9f);limit.multiplyDragByParticleSize=false;limit.multiplyDragByParticleVelocity=false;limit.limit=1000;}
            var renderer=go.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;if(aerosol)renderer.SetActiveVertexStreams(new System.Collections.Generic.List<ParticleSystemVertexStream>{ParticleSystemVertexStream.Position,ParticleSystemVertexStream.Color,ParticleSystemVertexStream.UV,ParticleSystemVertexStream.StableRandomX});
            return ps;
        }
        public void Reset(){smoke.Clear();drops.Clear();carry=0;ringCarry=0;burst=false;}
        public void Breach(Vector3 point,float radius,float seed)
        {
            if(rock||seed<F("breachGasChance",.3f))EmitSmoke(point,rock?Mathf.RoundToInt(F("breachDust",3)):3,radius,.65f);
            EmitDrops(point,Mathf.RoundToInt(F(rock?"breachGrit":"breachDrops",3)),F(rock?"gritSpeed":"dropSpeed"));
        }

        void EmitSource(ParticleSystem ps,Vector3 position,float radius,Vector3 direction,float speed,float speedVariance,float spread,float size,float sizeVariance,float life,float lifeVariance,float spin,int count,float sizeScale,float speedScale,float lifeScale)
        {
            for(int n=0;n<count;n++)
            {
                Vector3 o=Vector3.zero;
                if(radius>0){float r=radius*Mathf.Pow(Random.value,1f/3f),theta=Random.value*Mathf.PI*2,phi=Mathf.Acos(2*Random.value-1),sn=Mathf.Sin(phi);o=new Vector3(r*sn*Mathf.Cos(theta),r*Mathf.Cos(phi),r*sn*Mathf.Sin(theta));}
                Vector3 v=direction;
                if(spread>0)v+=new Vector3((Random.value-.5f)*2*spread,(Random.value-.5f)*2*spread,(Random.value-.5f)*2*spread);
                v=v.normalized*speed*(1+(Random.value-.5f)*2*speedVariance)*speedScale;
                var e=new ParticleSystem.EmitParams{position=position+o,velocity=v,startLifetime=Mathf.Max(.05f,life*(1+(Random.value-.5f)*2*lifeVariance))*lifeScale,startSize=Mathf.Max(.001f,size*(1+(Random.value-.5f)*2*sizeVariance))*sizeScale,rotation=Random.value*360,angularVelocity=(Random.value-.5f)*2*spin*Mathf.Rad2Deg};
                ps.Emit(e,1);
            }
        }
        static float OutCubic(float t){t=1-Mathf.Clamp01(t);return 1-t*t*t;}
        float ringCarry;
        // MEASURED CORRECTION, not a look choice. The source was driven headless
        // (Chrome over CDP, Metal) and photographed at the same instants as the
        // smoke test: at 1.8 s both builds hold ~780 dust sprites of the same
        // size, lifetime and per-sprite alpha (a lone 6 m puff at opacity 1
        // renders identically in both), yet the source's cloud is ~6x denser.
        // Soft fade, drag, size curve, mask and colour space were each ruled
        // out by isolated runs. The factor's mechanism is still unknown; this
        // constant reproduces the measured density until it is found.
        const float RiftDustCalibration=6f;
        void EmitSmoke(Vector3 p,int count,float radius,float speed)
        {
            for(int i=0,n=Mathf.RoundToInt(count*G("particleCount"));i<n;i++){
                Vector2 v=Random.insideUnitCircle;var e=new ParticleSystem.EmitParams{position=p+new Vector3(v.x,.08f,v.y)*radius,velocity=new Vector3(v.x,.15f+Random.value*.6f,v.y)*speed*G("particleSpeed"),startLifetime=F(rock?"dustLifetime":"gasLifetime")*Random.Range(.7f,1.3f)*G("particleLifetime"),startSize=F(rock?"dustSize":"gasSize")*Random.Range(.6f,1.4f)*G("particleSize"),rotation=Random.value*360};smoke.Emit(e,1);
            }
        }
        void EmitDrops(Vector3 p,int count,float speed)
        {
            for(int i=0,n=Mathf.RoundToInt(count*G("particleCount"));i<n;i++){Vector2 v=Random.insideUnitCircle;var e=new ParticleSystem.EmitParams{position=p+Vector3.up*.1f,velocity=new Vector3(v.x,Random.Range(.2f,1),v.y)*speed*G("particleSpeed"),startLifetime=F(rock?"gritLifetime":"dropLifetime")*Random.Range(.6f,1.3f)*G("particleLifetime"),startSize=F(rock?"gritSize":"dropSize")*Random.Range(.5f,1.5f)*G("particleSize")};drops.Emit(e,1);}
        }
        public void Tick(float age,float travel,float dt,float retract)
        {
            smokeMaterial.SetFloat("_Opacity",F(rock?"dustOpacity":"gasOpacity")*G("opacity")*(1-retract)*(rock?RiftDustCalibration:1f));
            var main=smoke.main;main.gravityModifier=-F(rock?"dustRise":"gasRise")/9.81f;
            main=drops.main;main.gravityModifier=-F(rock?"gritGravity":"dropGravity")/9.81f;
            float t=age-travel;
            if(rock){TickRift(t,dt,retract);}
            else
            {
                if(t>=0&&!burst){burst=true;EmitSmoke(spell.Target,Mathf.RoundToInt(F("burstGas")),spell.Radius*.55f,F("gasSpeed"));EmitDrops(spell.Target,Mathf.RoundToInt(F("burstDrops")),F("dropSpeed"));}
                if(dt>0&&retract<=0){carry+=dt*F("gasRate")*(t<0?1:F("standingGas"));int n=(int)carry;carry-=n;EmitSmoke(t<0?spell.Focus:spell.Target,n,t<0?F("width")*.6f:spell.Radius,F("gasSpeed"));}
            }
            if(dt>0){smoke.Simulate(dt,false,false);drops.Simulate(dt,false,false);}
        }
        // MonolithRiftAbility: _frontFx, the impact, _rollFx and _settleFx.
        void TickRift(float t,float dt,float retract)
        {
            float sizeScale=F("dustSize",1.2f)*G("particleSize"),speedScale=F("dustSpeed",1.6f)*G("particleSpeed"),lifeScale=F("dustLifetime",3.4f)*.5f*G("particleLifetime");
            float pc=G("particleCount"),strength=1-retract;
            Vector3 target=spell.Target;
            if(t<0&&dt>0)
            {
                // Dust shed along the travelling front, leaning back down the
                // line so the cloud is left behind it — which is what makes
                // the front look fast.
                carry+=dt*F("dustRate",260)*pc;int n=(int)carry;carry-=n;
                Vector3 dir=(-spell.FrontTangent*.5f+Vector3.up).normalized;
                EmitSource(smoke,new Vector3(spell.Focus.x,.12f,spell.Focus.z),F("width",2.4f)*.5f,dir,F("dustSpeed",1.6f),.7f,.95f,.9f,.5f,F("dustLifetime",3.4f),.4f,.4f,n,sizeScale,speedScale,lifeScale);
            }
            if(t>=0&&!burst)
            {
                burst=true;ringCarry=0;
                // Layer 2 of the impact: the plume that climbs behind the ring.
                EmitSource(smoke,new Vector3(target.x,.5f,target.z),F("blastRadius",3.8f)*.75f,Vector3.up,F("plumeSpeed",4.5f),.65f,.62f,F("ringSize",1.5f)*1.5f,.5f,F("dustLifetime",3.4f)*1.7f,.45f,.5f,Mathf.RoundToInt(F("plumeDust",60)*pc),sizeScale,speedScale,lifeScale);
                EmitDrops(target,Mathf.RoundToInt(F("blastGrit",3)),F("gritSpeed"));
            }
            float ringTime=Mathf.Max(.05f,F("ringTime",.9f));
            if(t>=0&&t<ringTime&&dt>0)
            {
                // The rolling ring: twenty jets on a wobbling radius, thrown
                // outward and only slightly up. ringLift above about .5 and
                // the torus becomes a mushroom.
                float u=t/ringTime;int jets=Mathf.Max(1,Mathf.RoundToInt(F("ringJets",20)));
                ringCarry+=dt*F("ringRate",300)*pc;int perJet=Mathf.RoundToInt(ringCarry/jets);if(perJet>0){ringCarry-=perJet*jets;
                float radius=F("ringRadius",8)*G("explosionIntensity")*OutCubic(u),rise=Mathf.Lerp(1,.35f,u);
                for(int j=0;j<jets;j++)
                {
                    float bearing=(float)j/jets*Mathf.PI*2+Random.value*(Mathf.PI*2/jets),c=Mathf.Cos(bearing),sn=Mathf.Sin(bearing);
                    float reach=radius*Random.Range(.82f,1.12f);
                    Vector3 pos=new Vector3(target.x+c*reach,Random.Range(.05f,.5f)*rise,target.z+sn*reach);
                    Vector3 dir=new Vector3(c,F("ringLift",.22f),sn).normalized;
                    EmitSource(smoke,pos,F("ringThickness",.6f),dir,F("ringSpeed",7)*Mathf.Lerp(1,.35f,u),.55f,.42f,F("ringSize",1.5f)*Mathf.Lerp(.75f,1.5f,u),.45f,F("dustLifetime",3.4f)*1.25f,.4f,.35f,perJet,sizeScale,speedScale,lifeScale);
                }}
            }
            if(t>=ringTime&&dt>0&&retract<=0)
            {
                // Standing: broken stone does not stop shedding.
                carry+=dt*F("dustRate",260)*F("settleDust",.4f)*strength*pc;int n=(int)carry;carry-=n;
                EmitSource(smoke,new Vector3(target.x,.1f,target.z),F("blastRadius",3.8f)*1.25f,Vector3.up,F("dustSpeed",1.6f)*.35f,.8f,1f,F("ringSize",1.5f)*1.1f,.5f,F("dustLifetime",3.4f)*1.3f,.4f,.3f,n,sizeScale,speedScale,lifeScale);
            }
        }
        public void Dispose(){Object.Destroy(smokeMaterial);Object.Destroy(dropMaterial);}
    }
}
