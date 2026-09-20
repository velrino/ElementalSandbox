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
            smokeMaterial=new Material(Shader.Find("Elemental/SourceSmoke"));
            dropMaterial=new Material(Shader.Find("Elemental/Energy"));dropMaterial.SetFloat("_Mode",5);dropMaterial.SetFloat("_Opacity",1);dropMaterial.SetFloat("_Glow",rock?.25f:F("dropGlow",1));dropMaterial.SetColor("_BaseColor",Color.white);dropMaterial.SetColor("_HotColor",Color.white);
            smoke=Make("Source breach aerosol",smokeMaterial,true);drops=Make("Source ballistic spray",dropMaterial,false);
        }
        ParticleSystem Make(string name,Material material,bool aerosol)
        {
            var go=new GameObject(name);go.transform.SetParent(spell.Root.transform,false);var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main;main.playOnAwake=false;main.loop=false;main.duration=30;main.maxParticles=aerosol?2400:1600;main.simulationSpace=ParticleSystemSimulationSpace.World;
            var emission=ps.emission;emission.enabled=false;var shape=ps.shape;shape.enabled=false;
            var color=ps.colorOverLifetime;color.enabled=true;string prefix=aerosol?(rock?"colorDust":"colorGas"):(rock?"colorGrit":"colorDrop");var grad=new Gradient();grad.SetKeys(new[]{new GradientColorKey(spell.C(prefix+"A"),0),new GradientColorKey(spell.C(prefix+"B"),.25f),new GradientColorKey(spell.C(prefix+"C"),.65f),new GradientColorKey(spell.C(prefix+"D"),1)},new[]{new GradientAlphaKey(aerosol?0:1,0),new GradientAlphaKey(1,.12f),new GradientAlphaKey(.7f,.55f),new GradientAlphaKey(0,1)});color.color=grad;
            if(aerosol){var noise=ps.noise;noise.enabled=true;noise.strength=F(rock?"dustTurbulence":"gasTurbulence")*G("turbulence");noise.frequency=.65f;noise.scrollSpeed=.3f;noise.quality=ParticleSystemNoiseQuality.Medium;var size=ps.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,.3f,1,F(rock?"dustSpread":"gasSpread")));}
            var renderer=go.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            return ps;
        }
        public void Reset(){smoke.Clear();drops.Clear();carry=0;burst=false;}
        public void Breach(Vector3 point,float radius,float seed)
        {
            if(rock||seed<F("breachGasChance",.3f))EmitSmoke(point,rock?Mathf.RoundToInt(F("breachDust",3)):3,radius,.65f);
            EmitDrops(point,Mathf.RoundToInt(F(rock?"breachGrit":"breachDrops",3)),F(rock?"gritSpeed":"dropSpeed"));
        }
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
            smokeMaterial.SetFloat("_Opacity",F(rock?"dustOpacity":"gasOpacity")*G("opacity")*(1-retract));
            var main=smoke.main;main.gravityModifier=-F(rock?"dustRise":"gasRise")/9.81f;
            main=drops.main;main.gravityModifier=-F(rock?"gritGravity":"dropGravity")/9.81f;
            float t=age-travel;
            if(t>=0&&!burst){burst=true;EmitSmoke(spell.Target,Mathf.RoundToInt(F(rock?"plumeDust":"burstGas")),spell.Radius*.55f,F(rock?"plumeSpeed":"gasSpeed"));EmitDrops(spell.Target,Mathf.RoundToInt(F(rock?"blastGrit":"burstDrops")),F(rock?"gritSpeed":"dropSpeed"));}
            if(dt>0&&retract<=0){carry+=dt*F(rock?"dustRate":"gasRate")*(t<0?1:F(rock?"settleDust":"standingGas"));int n=(int)carry;carry-=n;EmitSmoke(t<0?spell.Focus:spell.Target,n,t<0?F("width")*.6f:spell.Radius,F(rock?"dustSpeed":"gasSpeed"));
                if(rock&&t>0&&t<F("ringTime")){int jets=Mathf.Max(1,(int)F("ringJets"));for(int j=0;j<jets;j++){float a=j*Mathf.PI*2/jets;Vector3 radial=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));EmitSmoke(spell.Target+radial*Mathf.Min(F("ringRadius"),t*F("ringSpeed")),1,F("ringThickness"),.3f);}}
            }
            if(dt>0){smoke.Simulate(dt,false,false);drops.Simulate(dt,false,false);}
        }
        public void Dispose(){Object.Destroy(smokeMaterial);Object.Destroy(dropMaterial);}
    }
}
