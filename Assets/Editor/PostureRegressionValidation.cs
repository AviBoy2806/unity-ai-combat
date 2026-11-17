using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class PostureRegressionValidation {
 static bool started;
 static PostureRegressionValidation(){if(SessionState.GetBool("PostureRegressionValidation",false))EditorApplication.update+=Tick;}
 public static void Run(){EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);SessionState.SetBool("PostureRegressionValidation",true);EditorApplication.update+=Tick;EditorApplication.EnterPlaymode();}
 static void Tick(){if(started || !EditorApplication.isPlaying)return;started=true;SessionState.SetBool("PostureRegressionValidation",false);int code=0;
 try{foreach(var type in new[]{typeof(PostureThresholdValidation),typeof(CombatValidation),typeof(EnemyStunnedStateValidation),typeof(EnemyCombatValidation)}){
  var simulation=Physics.simulationMode;
  try{Physics.simulationMode=SimulationMode.Script;type.GetMethod("Rules",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null);Debug.Log("POSTURE_REGRESSION_PASS "+type.Name);}
  finally{Physics.simulationMode=simulation;foreach(var root in SceneManager.GetActiveScene().GetRootGameObjects())UnityEngine.Object.DestroyImmediate(root);NavMesh.RemoveAllNavMeshData();}
 }Debug.Log("POSTURE_REGRESSION_VALIDATION_PASS");}catch(Exception e){Debug.LogException(e);code=1;}finally{if(Application.isBatchMode)EditorApplication.Exit(code);else EditorApplication.isPlaying=false;}}
}
