using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Policies;
using Unity.InferenceEngine;

[InitializeOnLoad]
public static class Phase5SpikeInference {
 static double started; static int steps; static bool moved; static float oldX; static bool found;
 static Phase5SpikeInference(){if(SessionState.GetBool("Phase5Inference",false))EditorApplication.update+=Tick;}
 public static void Run(){EditorSceneManager.OpenScene("Assets/CompatibilitySpike/Basic/Scenes/Basic.unity");
  var model=AssetDatabase.LoadAssetAtPath<ModelAsset>("Assets/CompatibilitySpike/ExportedBasic.onnx");if(!model)throw new Exception("Missing newly exported Basic model");
  foreach(var agent in UnityEngine.Object.FindObjectsByType<Agent>()){var b=agent.GetComponent<BehaviorParameters>();b.Model=model;b.BehaviorType=BehaviorType.InferenceOnly;b.InferenceDevice=InferenceDevice.Burst;PrefabUtility.RecordPrefabInstancePropertyModifications(b);EditorUtility.SetDirty(b);}
  // Persist only the isolated example so its own scene-reload reset keeps the exported model.
  EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
  SessionState.SetBool("Phase5Inference",true);EditorApplication.update+=Tick;EditorApplication.EnterPlaymode();
 }
 static void Tick(){if(!EditorApplication.isPlaying)return;if(started==0)started=EditorApplication.timeSinceStartup;
  try{if(Academy.Instance.IsCommunicatorOn || CommunicatorFactory.Enabled)throw new Exception("Unwanted Python communicator");var controller=UnityEngine.Object.FindAnyObjectByType<BasicController>();if(controller){var behavior=controller.GetComponent<BehaviorParameters>();if(!behavior.Model || AssetDatabase.GetAssetPath(behavior.Model)!="Assets/CompatibilitySpike/ExportedBasic.onnx" || behavior.BehaviorType!=BehaviorType.InferenceOnly)throw new Exception("Exported model/config changed across reset: path="+AssetDatabase.GetAssetPath(behavior.Model)+" type="+behavior.BehaviorType);if(found)moved|=Math.Abs(controller.transform.position.x-oldX)>.1f;oldX=controller.transform.position.x;found=true;steps++;}
   if(BasicController.actionCount>=128){Debug.Log("PHASE5_EXPORTED_INFERENCE_PASS CPU, 128 model action callbacks, actionBits="+BasicController.actionBits+", communicator disabled; movement="+moved);Finish(0);}
   if(EditorApplication.timeSinceStartup-started>90)throw new Exception("Inference timeout");
  }catch(Exception e){Debug.LogException(e);Finish(1);}
 }
 static void Finish(int code){SessionState.SetBool("Phase5Inference",false);EditorApplication.update-=Tick;if(Application.isBatchMode)EditorApplication.Exit(code);else EditorApplication.isPlaying=false;}
}





