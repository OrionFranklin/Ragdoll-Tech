using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RagdollTech.Editor
{
    public static class LabBuilder
    {
        [MenuItem("Ragdoll Tech/Create playable lab")]
        public static void Create()
        {
            if(!Application.isBatchMode&&!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            Directory.CreateDirectory("Assets/RagdollLab");AssetDatabase.Refresh();
            var surface=AssetDatabase.LoadAssetAtPath<Material>("Assets/RagdollLab/Surface.mat");
            if(!surface){surface=new Material(Shader.Find("Standard"));surface.SetFloat("_Glossiness",.2f);AssetDatabase.CreateAsset(surface,"Assets/RagdollLab/Surface.mat");}
            new GameObject("Ragdoll laboratory",typeof(RagdollLab)).GetComponent<RagdollLab>().surfaceMaterial=surface;
            EditorSceneManager.SaveScene(scene,"Assets/RagdollLab/Lab.unity");
            PlayerSettings.companyName="OrionFranklin";PlayerSettings.productName="Ragdoll Tech Lab";
            PlayerSettings.runInBackground=true;PlayerSettings.defaultScreenWidth=1600;PlayerSettings.defaultScreenHeight=900;
            PlayerSettings.fullScreenMode=FullScreenMode.Windowed;PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
            var settings=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            var input=settings.FindProperty("activeInputHandler");if(input!=null){input.intValue=0;settings.ApplyModifiedPropertiesWithoutUndo();}
            AssetDatabase.SaveAssets();
        }
        public static void Build()
        {
            Create();var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-labOutput");
            string output=index>=0?args[index+1]:"Builds/RagdollTechLab/RagdollTechLab.exe";
            var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/RagdollLab/Lab.unity"},locationPathName=output,target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
            if(result.summary.result!=BuildResult.Succeeded)throw new Exception("Lab build failed");
            Debug.Log("RAGDOLL_LAB_BUILD_SUCCEEDED");
        }
    }
}
