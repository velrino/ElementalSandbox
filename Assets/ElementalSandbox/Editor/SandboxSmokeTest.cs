using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace ElementalSandbox.Editor
{
    [InitializeOnLoad]
    public static class SandboxSmokeTest
    {
        static int stage=-1;
        static double deadline;
        static bool waiting,ringShot;static int lateShots;
        static readonly List<string> errors=new List<string>();
        static SandboxSmokeTest(){EditorApplication.update+=Update;Application.logMessageReceived+=Log;}
        [MenuItem("Elemental Sandbox/Run visual smoke test")]
        public static void Run()
        {
            SandboxBuilder.Setup();Directory.CreateDirectory("Validation/Screenshots");SessionState.SetBool("Elemental.Smoke",true);SessionState.SetBool("Elemental.ExitSmoke",Application.isBatchMode);EditorApplication.EnterPlaymode();
        }
        static void Log(string text,string trace,LogType type){if(SessionState.GetBool("Elemental.Smoke",false)&&(type==LogType.Error||type==LogType.Exception||type==LogType.Assert))errors.Add(text);}
        static void Update()
        {
            if(!SessionState.GetBool("Elemental.Smoke",false)||!EditorApplication.isPlaying||EditorApplication.isPaused)return;var app=ElementalApp.Instance;if(app==null)return;
            try{
                if(stage<0){stage=0;deadline=EditorApplication.timeSinceStartup+1;return;}
                if(stage==6&&waiting&&!ringShot&&EditorApplication.timeSinceStartup>=deadline-1.35){ringShot=true;float d=app.Settings.F("camera.distance",11.5f);app.Settings.Set("camera.distance",22);app.ForceCamera();Capture(app,"06-quake-ring",1100,700);app.Settings.Set("camera.distance",d);app.ForceCamera();return;}
                if(stage==6&&waiting&&lateShots<2&&EditorApplication.timeSinceStartup>=deadline+(lateShots==0?1.7:3.7)){float d=app.Settings.F("camera.distance",11.5f);app.Settings.Set("camera.distance",22);app.ForceCamera();Capture(app,lateShots==0?"06-quake-late35":"06-quake-late55",1100,700);app.Settings.Set("camera.distance",d);app.ForceCamera();lateShots++;return;}
                if(EditorApplication.timeSinceStartup<deadline)return;
                if(stage==0){Capture(app,"00-idle",1100,700);stage=1;}
                if(stage>=1&&stage<=10){int slot=stage-1;if(!waiting){ringShot=false;lateShots=0;app.Clear();app.ResetTargets();if(!app.CastAt(slot,new Vector3(0,0,6),true))throw new Exception("Cast rejected "+slot);waiting=true;deadline=EditorApplication.timeSinceStartup+(slot==3?.55:slot==9?2.8:1.8);return;}if(slot==5&&lateShots<2)return;Capture(app,(slot+1).ToString("00")+"-"+app.Settings.Abilities[slot].id,1100,700);if(app.ActiveCount!=1)throw new Exception("Unexpected live effect count "+app.ActiveCount+" in "+slot);waiting=false;stage++;deadline=EditorApplication.timeSinceStartup+.1;return;}
                if(stage==11){app.Dummies[0].ResetBody();app.Dummies[0].Cut(Vector3.up);if(app.Dummies[0].SliceParts<2)throw new Exception("Body slicing failed");float largest=app.Dummies[0].LargestFragment;if(largest>3)throw new Exception("Severed piece is "+largest.ToString("0.0")+"m across; the slice is applying the rig scale twice");Capture(app,"13-slice",1100,700);app.Clear();for(int i=0;i<5;i++)app.CastAt(i,new Vector3((i-2)*2,0,6),true);if(app.ActiveCount!=4)throw new Exception("Concurrent cast cap failed");app.TogglePause();stage++;deadline=EditorApplication.timeSinceStartup+.5;return;}
                if(stage==12){if(!app.Paused)throw new Exception("Pause failed");Capture(app,"11-mobile-landscape",844,390);Capture(app,"12-mobile-portrait",390,844);app.TogglePause();app.Clear();app.ResetTargets();Finish();}
            }catch(Exception e){errors.Add(e.ToString());Finish();}
        }
        static void Capture(ElementalApp app,string name,int w,int h)
        {
            var camera=app.View;var old=camera.targetTexture;float aspect=camera.aspect;var rt=RenderTexture.GetTemporary(w,h,24,RenderTextureFormat.ARGB32);camera.targetTexture=rt;camera.aspect=(float)w/h;camera.Render();var prior=RenderTexture.active;RenderTexture.active=rt;var texture=new Texture2D(w,h,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,w,h),0,0);texture.Apply();File.WriteAllBytes("Validation/Screenshots/"+name+".png",texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);RenderTexture.active=prior;camera.targetTexture=old;camera.aspect=aspect;RenderTexture.ReleaseTemporary(rt);
        }
        static void Finish()
        {
            SessionState.SetBool("Elemental.Smoke",false);File.WriteAllText("Validation/SmokeTest.txt",errors.Count==0?"PASS: Ten abilities rendered; four-cast cap; body slicing at body scale; pause; landscape and portrait camera rendering.\n":"FAIL\n"+string.Join("\n",errors));bool exit=SessionState.GetBool("Elemental.ExitSmoke",false);Debug.Log("ELEMENTAL_SMOKE_"+(errors.Count==0?"PASS":"FAIL"));if(exit)EditorApplication.Exit(errors.Count==0?0:1);else EditorApplication.ExitPlaymode();
        }
    }
}
