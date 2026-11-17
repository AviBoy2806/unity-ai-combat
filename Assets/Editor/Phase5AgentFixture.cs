using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using GameCombat;
using GameCombat.ML;

public static class Phase5AgentFixture {
 [MenuItem("Tools/Combat/Create agent interface diagnostic scene")]
 public static void Create(){
  EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
  var enemy=UnityEngine.Object.FindAnyObjectByType<EnemyBrainHost>();
  var child=new GameObject("Contract heuristic (diagnostic scene only)");child.SetActive(false);child.transform.SetParent(enemy.transform,false);
  var parameters=child.AddComponent<BehaviorParameters>();parameters.BehaviorType=BehaviorType.HeuristicOnly;parameters.BrainParameters.VectorObservationSize=66;parameters.BrainParameters.NumStackedVectorObservations=3;parameters.BrainParameters.ActionSpec=ActionSpec.MakeDiscrete(9,5);
  var agent=child.AddComponent<EnemyLearningAgent>();agent.testHeuristic=true;child.SetActive(true);enemy.agentPolicy=agent;enemy.mode=EnemyControlMode.ML;enemy.showCombatDiagnostics=true;
  PrefabUtility.RecordPrefabInstancePropertyModifications(enemy);
  EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),"Assets/Scenes/AgentInterfaceDiagnostic.unity");Debug.Log("PHASE5_AGENT_FIXTURE_CREATED");
 }
}
