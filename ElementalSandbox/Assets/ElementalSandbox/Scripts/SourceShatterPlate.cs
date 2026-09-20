using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace ElementalSandbox
{
    // Source Voronoi cells with the centroid shrink/heave/tilt equations from
    // ShatterStoneMaterial.js. CPU deformation also keeps normals correct.
    public sealed class SourceShatterPlate:System.IDisposable
    {
        readonly SpellVisual spell;readonly bool rock;readonly Mesh mesh;readonly Material material;
        readonly Vector3[] original,normals,positions,deformedNormals;
        readonly List<Vector3> cells=new List<Vector3>(),dice=new List<Vector3>();
        public SourceShatterPlate(SpellVisual spell,SandboxSettings settings)
        {
            this.spell=spell;rock=spell.Id=="quake";mesh=Object.Instantiate(MeshLibrary.Get(spell.Id+"Plate"));mesh.MarkDynamic();original=mesh.vertices;normals=mesh.normals;positions=new Vector3[original.Length];deformedNormals=new Vector3[original.Length];mesh.GetUVs(2,cells);mesh.GetUVs(3,dice);
            material=new Material(Shader.Find("Elemental/QuakeSource"));foreach(var e in settings.Entries){if(!e.key.StartsWith("quake."))continue;string key="_"+e.key.Substring(6);if(!material.HasProperty(key))continue;if(e.kind=="number")material.SetFloat(key,e.number);else if(ColorUtility.TryParseHtmlString(e.text,out var color))material.SetColor(key,color);}
            string[] maps={"Albedo","Normal","Rough","AO"},files={"color","normal","roughness","ao"};for(int i=0;i<4;i++)material.SetTexture("_"+maps[i]+"Map",Resources.Load<Texture2D>("Elemental/textures/cathedral/"+files[i]));material.SetFloat("_damp",0);material.SetFloat("_grime",0);
        }
        float F(string venom,string quake)=>spell.F(rock?quake:venom);
        static float Smooth(float a,float b,float x){float u=Mathf.InverseLerp(a,b,x);return u*u*(3-2*u);}
        public void Tick(float age,float sink)
        {
            if(age<0)return;float radius=Mathf.Max(.2f,F("plateRadius","craterRadius")),grown=Mathf.Clamp01(age*F("slabGrowth","plateGrowth")/radius);
            for(int i=0;i<positions.Length;i++){
                var c=cells[i];var d=dice[i];float radial=new Vector2(c.x,c.y).magnitude,open=Smooth(radial-.26f,radial+.05f,grown),profile=1-Smooth(.1f,1,radial),yaw=d.z*Mathf.PI*2;
                var q=Quaternion.AngleAxis(-F("slabTilt","plateTilt")*(d.y*2-1)*open*profile*Mathf.Rad2Deg,new Vector3(Mathf.Cos(yaw),0,-Mathf.Sin(yaw)));
                float gap=1-F("slabGap","plateGap")*(.5f+.95f*d.x);
                Vector3 local=new Vector3((original[i].x-c.x)*gap,original[i].y,(original[i].z-c.y)*gap);
                Vector3 v=q*local;float lift=F("slabHeave","plateHeave")*(.2f+.8f*d.x)*open*profile;
                positions[i]=new Vector3(c.x+v.x,v.y+lift-Mathf.Pow(sink,3)*(F("slabDepth","plateDepth")*4+.35f),c.y+v.z);deformedNormals[i]=q*normals[i];
            }
            mesh.vertices=positions;mesh.normals=deformedNormals;mesh.RecalculateBounds();material.SetFloat("_Age",age);material.SetFloat("_Opacity",1);
            Graphics.DrawMesh(mesh,Matrix4x4.TRS(spell.Target+Vector3.up*.018f,Quaternion.identity,Vector3.one*radius),material,0,null,0,null,ShadowCastingMode.Off,true);
        }
        public void Dispose(){Object.Destroy(mesh);Object.Destroy(material);}
    }
}
