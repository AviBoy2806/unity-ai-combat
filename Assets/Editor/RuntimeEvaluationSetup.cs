using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.InferenceEngine;
using GameCombat;
using GameCombat.ML;

// Installs only the optional adapter in the existing scene; never rebuilds gameplay.
public static class RuntimeEvaluationSetup {
 public const string ModelPath="Assets/RuntimeEvaluationCandidate/CombatV1-EvaluationOnly.onnx";
 public const string MetadataPath="Assets/RuntimeEvaluationCandidate/runtime-metadata.json";
 public const string ModelHash="566e06daf98274b96b01b1a07940262044ef69e68bbce1d51fceb4d9335eec2d";
 public static string Hash(byte[] bytes){using var sha=SHA256.Create();return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
 public static void Install(){
  if(Hash(File.ReadAllBytes(ModelPath))!=ModelHash)throw new Exception("Candidate ONNX identity mismatch");
  AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
  EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
  foreach(var host in UnityEngine.Object.FindObjectsByType<EnemyBrainHost>(FindObjectsSortMode.None)){
   if(host.agentPolicy)throw new Exception("Existing adapter requires inspection; do not overwrite it");
   var child=new GameObject("Evaluation-only combat policy");child.SetActive(false);child.transform.SetParent(host.transform,false);
   var b=child.AddComponent<BehaviorParameters>();b.BehaviorName="CombatV1";b.BrainParameters.VectorObservationSize=66;b.BrainParameters.NumStackedVectorObservations=3;b.BrainParameters.ActionSpec=ActionSpec.MakeDiscrete(9,5);b.BehaviorType=BehaviorType.InferenceOnly;b.InferenceDevice=InferenceDevice.Burst;b.DeterministicInference=true;b.Model=AssetDatabase.LoadAssetAtPath<ModelAsset>(ModelPath);
   if(!b.Model)throw new Exception("Candidate import failed");
   var agent=child.AddComponent<EnemyLearningAgent>();agent.runtimeEvaluationOnly=true;agent.compatibleModel=b.Model;agent.modelMetadata=AssetDatabase.LoadAssetAtPath<TextAsset>(MetadataPath);agent.compatibleMetadataSha256=Hash(System.Text.Encoding.UTF8.GetBytes(agent.modelMetadata.text));agent.modelSha256=ModelHash;agent.compatiblePayloadSha256=EnemyLearningAgent.PayloadHash(b.Model);agent.MaxStep=0;child.SetActive(true);
   host.agentPolicy=agent;host.mode=EnemyControlMode.Normal;PrefabUtility.RecordPrefabInstancePropertyModifications(host);
  }
  foreach(var p in UnityEngine.Object.FindObjectsByType<CombatPresentation>(FindObjectsSortMode.None)){
   if(!p.combatAudioSource){var source=p.gameObject.AddComponent<AudioSource>();source.playOnAwake=false;source.loop=false;source.spatialBlend=1;source.dopplerLevel=0;source.minDistance=1;source.maxDistance=20;source.rolloffMode=AudioRolloffMode.Linear;p.combatAudioSource=source;PrefabUtility.RecordPrefabInstancePropertyModifications(p);}
  }
  EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());AssetDatabase.SaveAssets();
  Debug.Log("RUNTIME_EVALUATION_INSTALL_PASS Normal default; candidate explicitly evaluation-only");
 }
 public static void Audit(){
  EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
  var lines=new System.Collections.Generic.List<string>();
  foreach(var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())foreach(var c in root.GetComponentsInChildren<Combatant>(true)){
   string PathOf(Transform t)=>t.parent?PathOf(t.parent)+"/"+t.name:t.name;
   var anim=c.GetComponentInChildren<Animator>();var presentation=c.GetComponent<CombatPresentation>();
   lines.Add($"ACTOR {PathOf(c.transform)} faction={c.faction} arena={c.arenaId} animatorObject={(anim?PathOf(anim.transform):"none")} controller={AssetDatabase.GetAssetPath(anim?anim.runtimeAnimatorController:null)} audio={presentation?.combatAudioSource} light={AssetDatabase.GetAssetPath(c.light)} heavy={AssetDatabase.GetAssetPath(c.heavy)}");
   if(anim && anim.runtimeAnimatorController is UnityEditor.Animations.AnimatorController controller)foreach(var state in controller.layers[0].stateMachine.states)lines.Add($"STATE {state.state.name} speed={state.state.speed} motion={AssetDatabase.GetAssetPath(state.state.motion)} loop={(state.state.motion is AnimationClip clip?clip.isLooping:false)}");
  }
  foreach(var guid in AssetDatabase.FindAssets("t:AudioClip",new[]{"Assets"}))lines.Add("AUDIO "+AssetDatabase.GUIDToAssetPath(guid));
  foreach(var guid in AssetDatabase.FindAssets("t:AnimationClip",new[]{"Assets/Models"})){var path=AssetDatabase.GUIDToAssetPath(guid);foreach(var clip in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>())lines.Add($"CLIP {path}::{clip.name} length={clip.length} fps={clip.frameRate} loop={clip.isLooping}");}
  Directory.CreateDirectory("Logs/RuntimeIntegration20261117");File.WriteAllLines("Logs/RuntimeIntegration20261117/asset-audit.txt",lines);
  Debug.Log("RUNTIME_ASSET_AUDIT_PASS");
 }
 public static void BindPayload(){
  if(Hash(File.ReadAllBytes(ModelPath))!=ModelHash)throw new Exception("ONNX mismatch");
  EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
  foreach(var policy in UnityEngine.Object.FindObjectsByType<EnemyLearningAgent>(FindObjectsSortMode.None)){policy.compatiblePayloadSha256=EnemyLearningAgent.PayloadHash(policy.compatibleModel);policy.compatibleMetadataSha256=Hash(System.Text.Encoding.UTF8.GetBytes(policy.modelMetadata.text));}
  EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());Debug.Log("RUNTIME_PAYLOAD_BIND_PASS");
 }
 public static void BuildDemo(){
  string output=Path.GetFullPath((File.Exists("COMBAT_IMPLEMENTATION_PLAN.md")?"":"../../../")+"Builds/CombatDemo/CombatDemo.exe");
  var result=UnityEditor.BuildPipeline.BuildPlayer(new[]{"Assets/Scenes/GameScene.unity"},output,UnityEditor.BuildTarget.StandaloneWindows64,UnityEditor.BuildOptions.None);
  if(result.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception("Demo build failed");Debug.Log("RUNTIME_DEMO_BUILD_PASS");
 }
}

[CustomEditor(typeof(EnemyBrainHost))]
public class RuntimeControllerInspector : Editor {
 public override void OnInspectorGUI(){DrawDefaultInspector();var host=(EnemyBrainHost)target;EditorGUILayout.HelpBox(host.ControllerStatus,MessageType.Info);if(!Application.isPlaying)return;
  if(GUILayout.Button("Select Normal"))host.SetMode(EnemyControlMode.Normal);
  if(GUILayout.Button("Select ML — evaluation only"))host.SetMode(EnemyControlMode.ML);
  Repaint();
 }
}
