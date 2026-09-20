using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ElementalSandbox
{
    [Serializable] public sealed class SettingEntry { public string key,kind,text; public float number; public bool flag; }
    [Serializable] public sealed class AbilityInfo { public string id,label,accent,key,hint; public bool zone; public Color Color {get {ColorUtility.TryParseHtmlString(accent,out var c);return c;}} }
    [Serializable] public sealed class SettingsDocument { public SettingEntry[] entries; public AbilityInfo[] abilities; }
    public sealed class SandboxSettings
    {
        readonly Dictionary<string,SettingEntry> values=new Dictionary<string,SettingEntry>();
        public System.Collections.Generic.IEnumerable<SettingEntry> Entries=>values.Values;
        public AbilityInfo[] Abilities {get;private set;}
        public string PresetPath => Path.Combine(Application.persistentDataPath,"elemental-preset.json");
        public SandboxSettings(){Reset();}
        public void Reset(){var preserved=new List<SettingEntry>();foreach(var e in values.Values)if(e.key.StartsWith("performance."))preserved.Add(e);var doc=JsonUtility.FromJson<SettingsDocument>(Resources.Load<TextAsset>("Elemental/Defaults").text);values.Clear();foreach(var e in doc.entries)values[e.key]=e;Abilities=doc.abilities;foreach(var e in preserved)values[e.key]=e;}
        public float F(string key,float fallback=0)=>values.TryGetValue(key,out var e)&&e.kind=="number"?e.number:fallback;
        public string S(string key,string fallback="")=>values.TryGetValue(key,out var e)&&e.kind=="string"?e.text:fallback;
        public bool B(string key,bool fallback=false)=>values.TryGetValue(key,out var e)&&e.kind=="boolean"?e.flag:fallback;
        public Color C(string key,string fallback="#ffffff"){ColorUtility.TryParseHtmlString(S(key,fallback),out var c);return c;}
        public void Set(string key,float value){if(!float.IsNaN(value)&&!float.IsInfinity(value)&&values.TryGetValue(key,out var e)&&e.kind=="number")e.number=Mathf.Clamp(value,-10000,10000);}
        public void SetBool(string key,bool value){if(values.TryGetValue(key,out var e)&&e.kind=="boolean")e.flag=value;}
        public void SetColor(string key,Color value){if(values.TryGetValue(key,out var e)&&e.kind=="string")e.text="#"+ColorUtility.ToHtmlStringRGB(value);}
        public string Export(){var list=new List<SettingEntry>();foreach(var e in values.Values)if(!e.key.StartsWith("performance."))list.Add(e);return JsonUtility.ToJson(new SettingsDocument{entries=list.ToArray()},true);}
        public void Save()=>File.WriteAllText(PresetPath,Export());
        public void Load()=>Import(File.ReadAllText(PresetPath));
        public void Import(string json)
        {
            if(json.Length>2*1024*1024)throw new InvalidDataException("Preset exceeds 2 MB.");
            var doc=JsonUtility.FromJson<SettingsDocument>(json);if(doc?.entries==null)throw new InvalidDataException("Invalid preset.");
            foreach(var e in doc.entries){if(e==null||string.IsNullOrEmpty(e.key)||!values.TryGetValue(e.key,out var old)||e.kind!=old.kind)throw new InvalidDataException("Unknown setting or wrong type.");if(e.kind=="number"&&(float.IsNaN(e.number)||float.IsInfinity(e.number)||Mathf.Abs(e.number)>10000))throw new InvalidDataException("Invalid number.");if(e.kind=="string"&&old.text!=null&&old.text.StartsWith("#")&&!ColorUtility.TryParseHtmlString(e.text,out _))throw new InvalidDataException("Invalid color.");}
            foreach(var e in doc.entries)if(!e.key.StartsWith("performance."))values[e.key]=e;
        }
    }
    [Serializable] public sealed class SourceMesh { public string name; public float[] positions,normals,uv,faces,cells,rands;public int[] indices; }
    [Serializable] public sealed class SourceGeometry { public SourceMesh[] meshes; }
    public static class MeshLibrary
    {
        static readonly Dictionary<string,Mesh> meshes=new Dictionary<string,Mesh>();
        public static Mesh Get(string key)
        {
            if(!meshes.ContainsKey("crystal0")){var source=JsonUtility.FromJson<SourceGeometry>(Resources.Load<TextAsset>("Elemental/Geometry").text);foreach(var g in source.meshes){int n=g.positions.Length/3;var p=new Vector3[n];var normal=new Vector3[n];var uv=new Vector2[n];for(int i=0;i<n;i++){p[i]=new Vector3(g.positions[i*3],g.positions[i*3+1],-g.positions[i*3+2]);if(g.normals.Length>=i*3+3)normal[i]=new Vector3(g.normals[i*3],g.normals[i*3+1],-g.normals[i*3+2]);uv[i]=g.uv.Length>=i*2+2?new Vector2(g.uv[i*2],g.uv[i*2+1]):new Vector2(p[i].x,p[i].y);}for(int i=0;i<g.indices.Length;i+=3){int a=g.indices[i];g.indices[i]=g.indices[i+2];g.indices[i+2]=a;}var m=new Mesh{name=g.name,indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};m.vertices=p;m.uv=uv;var face=new Vector2[n];for(int j=0;j<n;j++)face[j]=new Vector2(g.faces!=null&&j<g.faces.Length?g.faces[j]:0,p[j].y);m.uv2=face;if(g.cells!=null&&g.cells.Length==n*3){var cells=new System.Collections.Generic.List<Vector3>();var rands=new System.Collections.Generic.List<Vector3>();for(int j=0;j<n;j++){cells.Add(new Vector3(g.cells[j*3],-g.cells[j*3+1],g.cells[j*3+2]));rands.Add(new Vector3(g.rands[j*3],g.rands[j*3+1],g.rands[j*3+2]));}m.SetUVs(2,cells);m.SetUVs(3,rands);}m.triangles=g.indices;if(g.normals.Length==g.positions.Length)m.normals=normal;else m.RecalculateNormals();m.RecalculateBounds();meshes[g.name]=m;}}
            return meshes[key];
        }
        public static Mesh Ring(int segments=128,float inner=.96f)
        {
            string key="ring"+segments+"-"+inner;if(meshes.TryGetValue(key,out var old))return old;
            var p=new Vector3[(segments+1)*2];var uv=new Vector2[p.Length];var ix=new int[segments*6];for(int i=0;i<=segments;i++){float a=i*Mathf.PI*2/segments;for(int j=0;j<2;j++){p[i*2+j]=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*(j==0?inner:1);uv[i*2+j]=new Vector2((float)i/segments,j);}if(i<segments){int k=i*6,v=i*2;ix[k]=v;ix[k+1]=v+2;ix[k+2]=v+1;ix[k+3]=v+1;ix[k+4]=v+2;ix[k+5]=v+3;}}var m=new Mesh{name=key,vertices=p,uv=uv,triangles=ix};m.RecalculateNormals();meshes[key]=m;return m;
        }
    }
}
