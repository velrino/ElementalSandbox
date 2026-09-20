using System;
using System.Collections;
using System.IO;
using UnityEngine;
namespace ElementalSandbox
{
    // Optional native player check, enabled only by an explicit command-line flag.
    public sealed class NativeValidation:MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install(){if(Array.IndexOf(Environment.GetCommandLineArgs(),"-elementalValidate")>=0)new GameObject("Native validation",typeof(NativeValidation));}
        IEnumerator Start()
        {
            string path=Path.Combine(Application.persistentDataPath,"NativeValidation");Directory.CreateDirectory(path);yield return null;var app=ElementalApp.Instance;int errors=0;Application.LogCallback logger=(text,trace,type)=>{if(type==LogType.Error||type==LogType.Exception){errors++;File.AppendAllText(Path.Combine(path,"errors.txt"),text+"\n");}};Application.logMessageReceived+=logger;
            for(int slot=0;slot<10;slot++){app.Clear();app.ResetTargets();app.CastAt(slot,new Vector3(0,0,8),true);yield return new WaitForSecondsRealtime(slot==3?.45f:slot==9?2.8f:1.4f);app.TogglePause();yield return null;Capture(app.View,Path.Combine(path,app.Settings.Abilities[slot].id+".png"));app.TogglePause();}
            File.WriteAllText(Path.Combine(path,"result.txt"),errors==0?"PASS — all ten abilities ran in the native player.":"FAIL — "+errors+" runtime errors.");Application.logMessageReceived-=logger;Application.Quit(errors==0?0:1);
        }
        static void Capture(Camera camera,string path){var old=camera.targetTexture;var rt=RenderTexture.GetTemporary(1100,700,24);camera.targetTexture=rt;camera.Render();var previous=RenderTexture.active;RenderTexture.active=rt;var t=new Texture2D(1100,700,TextureFormat.RGB24,false);t.ReadPixels(new Rect(0,0,1100,700),0,0);t.Apply();File.WriteAllBytes(path,t.EncodeToPNG());Destroy(t);camera.targetTexture=old;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);}
    }
}
