using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.AI;
using Unity.AI.Navigation;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.InferenceEngine;
using GameCombat;
using GameCombat.ML;
using GameCombat.Training;

public static class Phase5EnvironmentBuild {
 public const string ScenePath="Assets/Scenes/CombatTraining.unity";
 [MenuItem("Tools/Combat/Create combat training environment")]
 public static void Create(){
  EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
  var sourcePlayer=UnityEngine.Object.FindFirstObjectByType<Player>();var sourceEnemy=UnityEngine.Object.FindFirstObjectByType<enemy>();
  var scene=SceneManager.GetActiveScene();var keep=new HashSet<GameObject>();
  var session=new GameObject("Combat training session").AddComponent<CombatTrainingSession>();keep.Add(session.gameObject);session.arenas=new CombatTrainingArena[8];session.defaultArenaCount=1;
  for(int index=0;index<8;index++){
   var root=new GameObject("Arena "+(index+1));root.SetActive(false);root.transform.position=new Vector3(index%4*40,0,index/4*40);keep.Add(root);
   var arena=root.AddComponent<CombatTrainingArena>();arena.arenaId=index+1;session.arenas[index]=arena;
   var geometry=new GameObject("Owned geometry");geometry.transform.SetParent(root.transform,false);
   Box("Floor",geometry.transform,new Vector3(0,-.25f,0),new Vector3(18,.5f,18),7);
   for(int side=0;side<4;side++)Box("Boundary wall",geometry.transform,side<2?new Vector3(side==0?-9:9,1.5f,0):new Vector3(0,1.5f,side==2?-9:9),side<2?new Vector3(.4f,3,18):new Vector3(18,3,.4f),6);
   arena.variedWalls=new[]{Box("Generalization wall A",geometry.transform,new Vector3(-4,1,3),new Vector3(.4f,2,4),6),Box("Generalization wall B",geometry.transform,new Vector3(4,1,-3),new Vector3(.4f,2,4),6)};
   foreach(var wall in arena.variedWalls)wall.SetActive(false);
   arena.surface=geometry.AddComponent<NavMeshSurface>();arena.surface.collectObjects=CollectObjects.Children;arena.surface.useGeometry=NavMeshCollectGeometry.PhysicsColliders;
   var p=UnityEngine.Object.Instantiate(sourcePlayer.gameObject,root.transform);p.name="Scripted player opponent";p.transform.localPosition=new Vector3(0,1,1);p.GetComponent<Player>().standaloneControl=true;
   var physical=p.GetComponent<UnityEngine.InputSystem.PlayerInput>();if(physical)physical.enabled=false;
   p.GetComponent<LockOn>().enabled=false;var debug=p.GetComponent<CombatDebugControls>();if(debug)UnityEngine.Object.DestroyImmediate(debug);
   p.AddComponent<ScriptedPlayerOpponent>();
   var e=UnityEngine.Object.Instantiate(sourceEnemy.gameObject,root.transform);e.name="Learner";e.transform.localPosition=new Vector3(0,1,-1);e.GetComponent<enemy>().standaloneControl=true;
   var nav=e.GetComponent<EnemyPerceptionNav>();nav.requireInjectedTarget=true;nav.target=p.transform;
   debug=e.GetComponent<CombatDebugControls>();if(debug)UnityEngine.Object.DestroyImmediate(debug);
   var actor=e.GetComponent<Combatant>();var opponent=p.GetComponent<Combatant>();actor.arenaId=opponent.arenaId=arena.arenaId;actor.faction=1;opponent.faction=0;
   var child=new GameObject("Combat policy");child.SetActive(false);child.transform.SetParent(e.transform,false);
   var behavior=child.AddComponent<BehaviorParameters>();behavior.BehaviorName="CombatV1";behavior.BrainParameters.VectorObservationSize=66;behavior.BrainParameters.NumStackedVectorObservations=3;behavior.BrainParameters.ActionSpec=ActionSpec.MakeDiscrete(9,5);behavior.InferenceDevice=InferenceDevice.Burst;
   var agent=child.AddComponent<EnemyLearningAgent>();agent.testHeuristic=true;agent.MaxStep=0;child.SetActive(true);
   var host=e.GetComponent<EnemyBrainHost>();host.mode=EnemyControlMode.ML;host.agentPolicy=agent;host.showCombatDiagnostics=false;
   arena.learner=actor;arena.opponent=opponent;arena.agent=agent;p.GetComponent<ScriptedPlayerOpponent>().target=actor;
   root.SetActive(true);arena.surface.BuildNavMesh();root.SetActive(false);
  }
  foreach(var root in scene.GetRootGameObjects())if(!keep.Contains(root))UnityEngine.Object.DestroyImmediate(root);
  EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();Debug.Log("PHASE5_ENVIRONMENT_CREATED eight explicit arenas, default one");
 }
 static GameObject Box(string name,Transform parent,Vector3 position,Vector3 scale,int layer){var box=GameObject.CreatePrimitive(PrimitiveType.Cube);box.name=name;box.layer=layer;box.transform.SetParent(parent,false);box.transform.localPosition=position;box.transform.localScale=scale;return box;}
 public static void Build(){Create();BuildScene("Tools/Phase5/Combat/Build/Combat.exe");}
 public static void BuildAll(){Build();BuildEvaluation();}
 public static void BuildEvaluation(){
  EditorSceneManager.OpenScene(ScenePath);var session=UnityEngine.Object.FindFirstObjectByType<CombatTrainingSession>();
  session.evaluationModel=AssetDatabase.LoadAssetAtPath<ModelAsset>("Assets/CombatTrainingPolicy/CombatV1.onnx");session.evaluationMetadata=AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/CombatTrainingPolicy/metadata.json");
  if(!session.evaluationModel || !session.evaluationMetadata)throw new Exception("Copy exported combat ONNX and metadata before evaluation build");
  EditorSceneManager.SaveScene(SceneManager.GetActiveScene());BuildScene("Tools/Phase5/Combat/Evaluation/Combat.exe");
 }
 static void BuildScene(string path){Directory.CreateDirectory(Path.GetDirectoryName(path));var result=BuildPipeline.BuildPlayer(new[]{ScenePath},path,BuildTarget.StandaloneWindows64,BuildOptions.None);if(result.summary.result!=BuildResult.Succeeded)throw new Exception("Combat build failed");Debug.Log("PHASE5_COMBAT_BUILD_PASS "+path);}
}
