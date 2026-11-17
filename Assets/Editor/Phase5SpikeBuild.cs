using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Policies;
using Unity.InferenceEngine;

public static class Phase5SpikeBuild {
 public static void Run() {
  const string scene="Assets/CompatibilitySpike/Basic/Scenes/Basic.unity";
  EditorSceneManager.OpenScene(scene);
  bool inference=Array.IndexOf(Environment.GetCommandLineArgs(),"-spikeInference")>=0;
  foreach(var a in UnityEngine.Object.FindObjectsByType<Agent>(FindObjectsSortMode.None)) {
   var b=a.GetComponent<BehaviorParameters>();
   b.BehaviorType=inference?BehaviorType.InferenceOnly:BehaviorType.Default;
   if(inference){b.Model=AssetDatabase.LoadAssetAtPath<ModelAsset>("Assets/CompatibilitySpike/ExportedBasic.onnx");if(!b.Model)throw new Exception("Exported model missing");a.gameObject.AddComponent<Phase5InferenceProbe>();}
   PrefabUtility.RecordPrefabInstancePropertyModifications(b);EditorUtility.SetDirty(b);
  }
  EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
  var result=BuildPipeline.BuildPlayer(new[]{scene},inference?"Tools/Phase5/Inference/Basic.exe":"Tools/Phase5/Training/Basic.exe",BuildTarget.StandaloneWindows64,BuildOptions.None);
  if(result.summary.result!=BuildResult.Succeeded)throw new Exception("Spike build failed");
  Debug.Log("PHASE5_SPIKE_BUILD_PASS inference="+inference);
 }
}
