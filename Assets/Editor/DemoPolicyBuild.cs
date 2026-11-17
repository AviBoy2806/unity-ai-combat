using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using GameCombat.Training;
using GameCombat.ML;
using GameCombat;
using Unity.MLAgents.Policies;

// Reuses the existing isolated environment. Prior frozen binaries are never overwritten.
public static class DemoPolicyBuild {
 public static void Training(){
  Phase5EnvironmentBuild.Create();
  // GameScene now contains an optional runtime adapter. Only the explicitly
  // owned training adapter belongs in each copied arena; no unused runtime agents.
  foreach(var a in UnityEngine.Object.FindObjectsByType<EnemyLearningAgent>(FindObjectsInactive.Include,FindObjectsSortMode.None))if(a.runtimeEvaluationOnly)UnityEngine.Object.DestroyImmediate(a.gameObject);
  EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
  Build(Environment.GetEnvironmentVariable("DEMO_TRAIN_BUILD_PATH")??"Tools/Phase6Demo/Training/Combat.exe");
 }
 public static void Evaluation(){
  AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
  EditorSceneManager.OpenScene(Phase5EnvironmentBuild.ScenePath);
  var session=UnityEngine.Object.FindFirstObjectByType<CombatTrainingSession>();
  session.evaluationModel=AssetDatabase.LoadAssetAtPath<Unity.InferenceEngine.ModelAsset>("Assets/DemoPolicyEvaluation/CombatV1.onnx");
  session.evaluationMetadata=AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/DemoPolicyEvaluation/metadata.json");
  if(!session.evaluationModel || !session.evaluationMetadata)throw new Exception("Evaluation staging export/metadata missing");
  EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());Build("Tools/Phase6Demo/Evaluation/Combat.exe");
 }
 static void Build(string path){Directory.CreateDirectory(Path.GetDirectoryName(path));var report=BuildPipeline.BuildPlayer(new[]{Phase5EnvironmentBuild.ScenePath},path,BuildTarget.StandaloneWindows64,BuildOptions.None);if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Isolated demo-policy build failed");Debug.Log("DEMO_POLICY_BUILD_PASS "+path);}
 [Serializable] class Selection {public string modelSha256,runtimeMetadataSha256;public bool sampledInference;}
 [Serializable] class Installed {public string modelSha256,metadataSha256,payloadSha256;public bool normalSavedDefault;public string modelPath,metadataPath;}
 public static void InstallSelected(){
  var selected=JsonUtility.FromJson<Selection>(File.ReadAllText("Logs/DemoPolicy20261117/selected-source.json"));
  if(RuntimeEvaluationSetup.Hash(File.ReadAllBytes(RuntimeEvaluationSetup.ModelPath))!=selected.modelSha256 || RuntimeEvaluationSetup.Hash(File.ReadAllBytes(RuntimeEvaluationSetup.MetadataPath))!=selected.runtimeMetadataSha256)throw new Exception("Selected source identity mismatch");
  AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);AssetDatabase.ImportAsset(RuntimeEvaluationSetup.ModelPath,ImportAssetOptions.ForceSynchronousImport|ImportAssetOptions.ForceUpdate);
  EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");int count=0;
  foreach(var host in UnityEngine.Object.FindObjectsByType<EnemyBrainHost>(FindObjectsSortMode.None)){
   var agent=host.agentPolicy as EnemyLearningAgent;if(!agent || !agent.runtimeEvaluationOnly)throw new Exception("Expected existing evaluation-only adapter; refusing hierarchy changes");
   var behavior=agent.GetComponent<BehaviorParameters>();behavior.Model=AssetDatabase.LoadAssetAtPath<Unity.InferenceEngine.ModelAsset>(RuntimeEvaluationSetup.ModelPath);behavior.DeterministicInference=!selected.sampledInference;
   agent.compatibleModel=behavior.Model;agent.modelMetadata=AssetDatabase.LoadAssetAtPath<TextAsset>(RuntimeEvaluationSetup.MetadataPath);
   agent.compatibleMetadataSha256=RuntimeEvaluationSetup.Hash(System.Text.Encoding.UTF8.GetBytes(agent.modelMetadata.text));agent.modelSha256=selected.modelSha256;agent.compatiblePayloadSha256=EnemyLearningAgent.PayloadHash(behavior.Model);
   if(agent.trainingMode || agent.testHeuristic || behavior.BehaviorType!=BehaviorType.InferenceOnly || behavior.InferenceDevice!=InferenceDevice.Burst)throw new Exception("Gameplay adapter configuration unexpectedly changed");
   host.mode=EnemyControlMode.Normal;EditorUtility.SetDirty(agent);EditorUtility.SetDirty(behavior);EditorUtility.SetDirty(host);count++;
   File.WriteAllText("Logs/DemoPolicy20261117/installed-identity.json",JsonUtility.ToJson(new Installed{modelSha256=agent.modelSha256,metadataSha256=agent.compatibleMetadataSha256,payloadSha256=agent.compatiblePayloadSha256,normalSavedDefault=true,modelPath=RuntimeEvaluationSetup.ModelPath,metadataPath=RuntimeEvaluationSetup.MetadataPath},true));
  }
  if(count!=1)throw new Exception("Expected exactly one existing enemy host");
  EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());AssetDatabase.SaveAssets();Debug.Log("DEMO_SELECTED_INSTALL_PASS model="+selected.modelSha256+" Normal saved default");
 }
 public static void BuildSelectedGameScene(){
  string root=File.Exists("COMBAT_IMPLEMENTATION_PLAN.md")?Directory.GetCurrentDirectory():Path.GetFullPath("../../..");
  string output=Path.Combine(root,"Builds/DemoPolicyCandidate/CombatDemo.exe");Directory.CreateDirectory(Path.GetDirectoryName(output));
  var report=BuildPipeline.BuildPlayer(new[]{"Assets/Scenes/GameScene.unity"},output,BuildTarget.StandaloneWindows64,BuildOptions.Development);
  if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Selected GameScene build failed");Debug.Log("DEMO_SELECTED_GAME_BUILD_PASS "+output);
 }
}
