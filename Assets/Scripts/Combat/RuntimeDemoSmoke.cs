using System;
using UnityEngine;
using GameCombat.ML;
// Bounded, explicitly selected validation mode in the demonstration build.
// With no --combat-runtime-smoke flag this creates no object and changes nothing.
namespace GameCombat {
public sealed class RuntimeDemoSmoke : MonoBehaviour {
 EnemyBrainHost host;EnemyLearningAgent policy;int ticks;double deadline;bool selected,done;
 [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
 static void StartIfRequested(){if(Array.IndexOf(Environment.GetCommandLineArgs(),"--combat-runtime-smoke")>=0)new GameObject("Bounded runtime smoke").AddComponent<RuntimeDemoSmoke>();}
 void Start(){deadline=Time.realtimeSinceStartupAsDouble+30;CombatWorld.AfterStep+=Sample;host=FindFirstObjectByType<EnemyBrainHost>();policy=host.agentPolicy as EnemyLearningAgent;var p=FindFirstObjectByType<Player>();p.GetComponent<UnityEngine.InputSystem.PlayerInput>().enabled=false;Time.captureDeltaTime=.02f;
  Debug.Log("DEMO_SMOKE Normal startup="+host.ActiveMode+" tuning="+host.AgentTuningHash);
  var position=p.transform.position+Vector3.forward*2.1f;position.y=host.transform.position.y;host.GetComponent<EnemyMotor>().Teleport(position,Quaternion.LookRotation(Vector3.ProjectOnPlane(p.transform.position-position,Vector3.up)));
 }
 void Update(){if(!done && Time.realtimeSinceStartupAsDouble>deadline)Finish(false,"30-second wall deadline");}
 void Sample(){if(done)return;try{ticks++;if(!selected && ticks>=30){if(host.ActiveMode!=EnemyControlMode.Normal)throw new Exception("Default was not Normal");selected=true;host.SetMode(EnemyControlMode.ML);Debug.Log("DEMO_SMOKE explicit evaluation selection "+host.ControllerStatus);}
  if(selected && !string.IsNullOrEmpty(host.FallbackReason))throw new Exception(host.FallbackReason);
  if(ticks>=180 || host.GetComponent<Combatant>().Dead){if(!selected || host.ActiveMode!=EnemyControlMode.ML || !policy || policy.DecisionCount<5)throw new Exception("Inference did not run");if(Unity.MLAgents.Academy.Instance.IsCommunicatorOn)throw new Exception("Python communicator active");Debug.Log("DEMO_SMOKE decisions="+policy.DecisionCount+" ticks="+ticks);Finish(true,"offline saved candidate");}
  if(ticks>400)throw new Exception("400 tick bound");
 }catch(Exception e){Finish(false,e.Message);}}
 void Finish(bool pass,string reason){done=true;CombatWorld.AfterStep-=Sample;Debug.Log((pass?"RUNTIME_DEMO_SMOKE_PASS ":"RUNTIME_DEMO_SMOKE_FAIL ")+reason);Application.Quit(pass?0:1);}
 void OnDestroy(){CombatWorld.AfterStep-=Sample;}
}
}
