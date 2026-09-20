using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
namespace ElementalSandbox.Editor
{
    public static class CharacterClipBuilder
    {
        static string Canonical(string name)=>Regex.Replace(name,@"mixamorig\d*:?","",RegexOptions.IgnoreCase);
        public static void Build()
        {
            string dir="Assets/ElementalSandbox/Resources/Elemental/Animations";Directory.CreateDirectory(dir);var target=Resources.Load<GameObject>("Elemental/Models/Idle");var paths=new Dictionary<string,string>();var names=new Dictionary<string,string>();foreach(var t in target.GetComponentsInChildren<Transform>()){string path=AnimationUtility.CalculateTransformPath(t,target.transform);paths[Canonical(path)]=path;names[Canonical(t.name)]=path;}
            var report=new List<string>();foreach(string name in new[]{"Idle","cast1","cast2","cast3"}){var clips=Resources.LoadAll<AnimationClip>("Elemental/Models/"+name);AnimationClip source=null;foreach(var c in clips)if(!c.name.StartsWith("__preview")){source=c;break;}if(source==null)throw new System.Exception("Missing animation "+name);string outPath=dir+"/"+name+".anim";var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(outPath);if(clip==null){clip=new AnimationClip();AssetDatabase.CreateAsset(clip,outPath);}clip.ClearCurves();clip.name=name;clip.legacy=true;clip.frameRate=source.frameRate;clip.wrapMode=name=="Idle"?WrapMode.Loop:WrapMode.Once;int bound=0,missing=0;foreach(var binding in AnimationUtility.GetCurveBindings(source)){if(binding.path.Length==0)continue;string canonical=Canonical(binding.path);if(!paths.TryGetValue(canonical,out string dest)&&!names.TryGetValue(Canonical(Path.GetFileName(binding.path)),out dest)){missing++;continue;}var output=binding;output.path=dest;AnimationUtility.SetEditorCurve(clip,output,AnimationUtility.GetEditorCurve(source,binding));bound++;}clip.EnsureQuaternionContinuity();EditorUtility.SetDirty(clip);report.Add(name+": "+bound+" bound curves, "+missing+" unmatched curves, "+clip.length+" seconds");if(bound==0||missing>0)throw new System.Exception("Animation curve binding failed: "+name);}
            Directory.CreateDirectory("Validation");File.WriteAllLines("Validation/AnimationBindings.txt",report);
        }
    }
}
