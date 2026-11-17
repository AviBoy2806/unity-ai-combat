using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using GameCombat;

[InitializeOnLoad]
public static class Phase5LiveAgentValidation {
 static EnemyBrainHost host;static Combatant actor;static int ticks,actions;static bool attached,done;static double deadline;
 static Phase5LiveAgentValidation(){if(SessionState.GetBool("Phase5LiveAgent",false))EditorApplication.update+=Update;}
 public static void Run(){EditorSceneManager.OpenScene("Assets/Scenes/AgentInterfaceDiagnostic.unity");SessionState.SetBool("Phase5LiveAgent",true);EditorApplication.update+=Update;EditorApplication.EnterPlaymode();}
 static void Update(){if(!EditorApplication.isPlaying || done || attached)return;try{
  Time.captureDeltaTime=.02f;host=UnityEngine.Object.FindAnyObjectByType<EnemyBrainHost>();var p=UnityEngine.Object.FindAnyObjectByType<Player>();if(!host || !p)return;
  var input=p.GetComponent<UnityEngine.InputSystem.PlayerInput>();if(input)input.enabled=false;
  var enemy=host.GetComponent<enemy>();var position=enemy.transform.position+enemy.transform.forward*2.5f;position.y=enemy.enemyCollider.bounds.min.y+(p.transform.position.y-p.playerCollider.bounds.min.y);p.rb.position=position;p.transform.position=position;p.rb.linearVelocity=Vector3.zero;
  // Isolate this interface/cadence test from duel deaths; no combat rule is replaced.
  actor=host.GetComponent<Combatant>();actor.arenaId=98341;Physics.SyncTransforms();actor.Runner.ApplyMovement+=Sample;attached=true;deadline=EditorApplication.timeSinceStartup+90;
 }catch(Exception e){Fail(e);}}
 static void Sample(){if(done)return;try{
  ticks++;if(Unity.MLAgents.Academy.IsInitialized && Unity.MLAgents.Academy.Instance.IsCommunicatorOn)throw new Exception("Unexpected Python connection");
  if(host.InCombat && host.ActiveMode!=EnemyControlMode.ML)throw new Exception("Actual runtime scheduler fell back: "+host.Diagnostic);
  var r=actor.Runner;if(host.ActiveMode==EnemyControlMode.ML && host.InCombat){if(r.Guarding)actions|=2;else if(r.Action==CombatAction.Free)actions|=1;else if(r.Action==CombatAction.Attack)actions|=r.Attack==actor.heavy?8:4;else if(r.Action==CombatAction.Dodge)actions|=16;}
  if(actions==31 && ticks>=300){Debug.Log("PHASE5_LIVE_AGENT_PASS actual CombatWorld/Academy loop, all five combat choices, ticks="+ticks+", no Python, no fallback");Finish(0);return;}
  if(ticks>2000 || EditorApplication.timeSinceStartup>deadline)throw new Exception("Live interface timeout actions="+actions+" mode="+host.ActiveMode+" combat="+host.InCombat+" "+host.Diagnostic);
 }catch(Exception e){Fail(e);}}
 static void Fail(Exception e){Debug.LogException(e);Finish(1);}
 static void Finish(int code){done=true;SessionState.SetBool("Phase5LiveAgent",false);if(actor)actor.Runner.ApplyMovement-=Sample;if(Application.isBatchMode)EditorApplication.Exit(code);else EditorApplication.isPlaying=false;}
}
