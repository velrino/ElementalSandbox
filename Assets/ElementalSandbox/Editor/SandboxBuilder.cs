using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace ElementalSandbox.Editor
{
    public sealed class SandboxAssetImporter:AssetPostprocessor
    {
        void OnPreprocessModel(){if(!assetPath.StartsWith("Assets/ElementalSandbox/Resources/Elemental/Models/",StringComparison.OrdinalIgnoreCase))return;var m=(ModelImporter)assetImporter;m.animationType=ModelImporterAnimationType.Legacy;m.importAnimation=true;m.materialImportMode=ModelImporterMaterialImportMode.None;m.isReadable=true;}
        void OnPreprocessTexture(){if(!assetPath.Contains("ElementalSandbox"))return;var t=(TextureImporter)assetImporter;t.maxTextureSize=2048;t.mipmapEnabled=true;t.anisoLevel=4;if(assetPath.EndsWith("normal.jpg")){t.textureType=TextureImporterType.NormalMap;t.sRGBTexture=false;}if(assetPath.EndsWith("roughness.jpg")||assetPath.EndsWith("ao.jpg"))t.sRGBTexture=false;}
    }
    public static class SandboxBuilder
    {
        public const string ScenePath="Assets/ElementalSandbox/Scenes/ElementalSandbox.unity";
        [MenuItem("Elemental Sandbox/Create or open sandbox scene")]
        public static void Setup()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach(var guid in AssetDatabase.FindAssets("t:Model",new[]{"Assets/ElementalSandbox"})){var path=AssetDatabase.GUIDToAssetPath(guid);if(!path.EndsWith(".fbx",StringComparison.OrdinalIgnoreCase))continue;var importer=(ModelImporter)AssetImporter.GetAtPath(path);if(importer.animationType!=ModelImporterAnimationType.Legacy||importer.materialImportMode!=ModelImporterMaterialImportMode.None){importer.animationType=ModelImporterAnimationType.Legacy;importer.importAnimation=true;importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.SaveAndReimport();}}
            CharacterClipBuilder.Build();
            Directory.CreateDirectory("Assets/ElementalSandbox/Scenes");
            if(File.Exists(ScenePath))EditorSceneManager.OpenScene(ScenePath);else{EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);new GameObject("Elemental Sandbox",typeof(ElementalApp));EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),ScenePath);}
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)};
            PlayerSettings.productName="Elemental Sandbox";PlayerSettings.companyName="LinearAbilityCasting";PlayerSettings.colorSpace=ColorSpace.Linear;PlayerSettings.defaultScreenWidth=1440;PlayerSettings.defaultScreenHeight=900;PlayerSettings.defaultInterfaceOrientation=UIOrientation.AutoRotation;PlayerSettings.allowedAutorotateToLandscapeLeft=true;PlayerSettings.allowedAutorotateToLandscapeRight=true;PlayerSettings.allowedAutorotateToPortrait=true;PlayerSettings.allowedAutorotateToPortraitUpsideDown=true;
            foreach(var guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset",new[]{"Assets/Settings"})){var rp=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetDatabase.GUIDToAssetPath(guid));rp.supportsCameraDepthTexture=true;rp.supportsCameraOpaqueTexture=true;rp.supportsHDR=true;rp.msaaSampleCount=1;EditorUtility.SetDirty(rp);}
            Directory.CreateDirectory("Assets/ElementalSandbox/Materials");
            const string warpPath="Assets/ElementalSandbox/Materials/FrameDistortion.mat";
            var warp=AssetDatabase.LoadAssetAtPath<Material>(warpPath);if(warp==null){warp=new Material(Shader.Find("Elemental/Distortion"));AssetDatabase.CreateAsset(warp,warpPath);}
            foreach(var guid in AssetDatabase.FindAssets("t:UniversalRendererData",new[]{"Assets/Settings"})){var path=AssetDatabase.GUIDToAssetPath(guid);var data=AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);var feature=data.rendererFeatures.OfType<ElementalDistortionFeature>().FirstOrDefault();if(feature==null){feature=ScriptableObject.CreateInstance<ElementalDistortionFeature>();feature.name="Elemental frame distortion";AssetDatabase.AddObjectToAsset(feature,data);data.rendererFeatures.Add(feature);}feature.passMaterial=warp;feature.injectionPoint=FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingPostProcessing;feature.fetchColorBuffer=true;feature.Create();EditorUtility.SetDirty(feature);EditorUtility.SetDirty(data);}
            AssetDatabase.SaveAssets();Validate();Debug.Log("ELEMENTAL_SETUP_COMPLETE");
        }
        [MenuItem("Elemental Sandbox/Validate port assets")]
        public static void Validate()
        {
            var settings=new SandboxSettings();if(settings.Abilities.Length!=10)throw new Exception("Expected ten abilities.");foreach(string shader in new[]{"Surface","Energy","Ground","Volume","Horizon","VenomSource","QuakeSource","SourceSmoke","Dust","Contact","SkyProbe","AcidMist","Nebula","InkVolume","AcidRing","Obsidian","GroundDecal"}){var s=Shader.Find("Elemental/"+shader);if(s==null)throw new Exception("Missing shader "+shader);if(ShaderUtil.ShaderHasError(s))throw new Exception("Shader error "+shader);}
            foreach(string name in new[]{"Idle","cast1","cast2","cast3","dummy"})if(Resources.Load<GameObject>("Elemental/Models/"+name)==null)throw new Exception("Missing model: "+name);
            for(int i=0;i<6;i++){if(MeshLibrary.Get("crystal"+i).vertexCount==0||MeshLibrary.Get("monolith"+i).vertexCount==0)throw new Exception("Missing geometry");}if(MeshLibrary.Get("serpent").vertexCount==0)throw new Exception("Missing serpent");
            float old=settings.F("ward.range");try{settings.Import("{\"entries\":[{\"key\":\"ward.range\",\"kind\":\"number\",\"number\":12},{\"key\":\"unknown\",\"kind\":\"number\",\"number\":3}]}");throw new Exception("Invalid preset accepted");}catch(InvalidDataException){}if(settings.F("ward.range")!=old)throw new Exception("Preset validation is not atomic");
            File.WriteAllText("PortValidation.json","{\"assets\":\"passed\",\"abilities\":10,\"originalMeshes\":27,\"presetValidation\":\"passed\"}");Debug.Log("ELEMENTAL_ASSET_VALIDATION_PASSED");
        }
        [MenuItem("Elemental Sandbox/Build macOS")]
        public static void BuildMac()=>Build(BuildTarget.StandaloneOSX,"Builds/macOS/ElementalSandbox.app");
        [MenuItem("Elemental Sandbox/Build Windows")]
        public static void BuildWindows()=>Build(BuildTarget.StandaloneWindows64,"Builds/Windows/ElementalSandbox.exe");
        [MenuItem("Elemental Sandbox/Build Android")]
        public static void BuildAndroid()=>Build(BuildTarget.Android,"Builds/Android/ElementalSandbox.apk");
        [MenuItem("Elemental Sandbox/Build iOS Xcode project")]
        public static void BuildIOS()=>Build(BuildTarget.iOS,"Builds/iOS");
        static void Build(BuildTarget target,string path){if(!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Unknown,target))throw new Exception("Install the Unity "+target+" build support module first.");Directory.CreateDirectory(Path.GetDirectoryName(path));var result=BuildPipeline.BuildPlayer(new[]{ScenePath},path,target,BuildOptions.None);if(result.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception("Build failed: "+result.summary.result);}
    }
}
