using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using GameCombat;

// Measures the real saved scene loop. No motor, timer, controller or clip is patched.
[InitializeOnLoad]
public static class Phase5ArenaMotionValidation {
 static bool waitingLoad; static int trial, ticks; static bool submitted, finished; static Combatant actor;
 static Vector3 origin; static Quaternion heading; static uint queued;
 static readonly List<Vector3[]> paths=new(); static readonly List<Vector3> path=new();
 static readonly List<string> lines=new();
 static Phase5ArenaMotionValidation(){if(SessionState.GetBool("Phase5ArenaMotion",false))EditorApplication.update+=Update;}
 public static void Run(){SessionState.SetBool("Phase5ArenaMotion",true);EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");EditorApplication.update+=Update;EditorApplication.EnterPlaymode();}
 static void Update(){if(!EditorApplication.isPlaying || finished || waitingLoad)return;try{
  Time.timeScale=trial%3==0?1:4;Time.captureDeltaTime=.02f/Time.timeScale;
  if(actor)return; var session=UnityEngine.Object.FindFirstObjectByType<GameCombat.Training.CombatTrainingSession>(); if(session){session.enabled=false;foreach(var arena in session.arenas)if(arena.gameObject.activeSelf){arena.enabled=false;arena.agent.enabled=false;arena.opponent.GetComponent<GameCombat.Training.ScriptedPlayerOpponent>().enabled=false;}}
  bool playerTrial=trial<9;var p=UnityEngine.Object.FindFirstObjectByType<Player>();var e=UnityEngine.Object.FindFirstObjectByType<enemy>();if(!p || !e)return;
  if(playerTrial){e.gameObject.SetActive(false);actor=p.GetComponent<Combatant>();var input=p.GetComponent<UnityEngine.InputSystem.PlayerInput>();if(input)input.enabled=false;}
  else {e.GetComponent<EnemyBrainHost>().enabled=false;actor=e.GetComponent<Combatant>();p.gameObject.SetActive(false);}
  var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Matched motion fixture floor";floor.layer=7;floor.transform.position=new Vector3(1000,-.25f,1000);floor.transform.localScale=new Vector3(40,.5f,40);var surface=floor.AddComponent<Unity.AI.Navigation.NavMeshSurface>();surface.collectObjects=Unity.AI.Navigation.CollectObjects.Children;surface.useGeometry=UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;surface.BuildNavMesh();var body=actor.GetComponent<Rigidbody>();var collider=actor.GetComponent<Collider>();float pivot=actor.transform.position.y-collider.bounds.min.y;var location=new Vector3(1000,pivot+.02f,1000);body.position=location;actor.transform.position=location;body.linearVelocity=body.angularVelocity=Vector3.zero;Physics.SyncTransforms();var nav=actor.GetComponent<UnityEngine.AI.NavMeshAgent>();if(nav)nav.Warp(location);actor.arenaId=97531; // Exclude unrelated scene contacts, unchanged motor/ground/walls.
  int route=trial%3;var anim=actor.GetComponentInChildren<Animator>();if(anim){anim.cullingMode=AnimatorCullingMode.AlwaysAnimate;}
  if(route==1)foreach(var renderer in actor.GetComponentsInChildren<Renderer>())renderer.enabled=false;
  ticks=0;submitted=false;queued=0;path.Clear();actor.Runner.ApplyMovement+=Sample;
 }catch(Exception ex){Fail(ex);}}
 static void Sample(){try{
  var r=actor.Runner;var p=actor.GetComponent<Player>();var e=actor.GetComponent<enemy>();
  if(!submitted){if(++ticks<50)return;if(!(p?p.IsGrounded():e.CheckGround()))throw new Exception("Actor failed to ground");origin=actor.transform.position;heading=actor.facing.rotation;
   int action=(trial%9)/3;var request=action==0?CombatRequest.Light:action==1?CombatRequest.Heavy:CombatRequest.Dodge;
   // Both routes submit the same legal request. Player presentation still follows its normal runner events.
   var result=r.TryStart(request,actor.facing.forward,true);if(result!=RequestResult.Accepted)throw new Exception("Start rejected "+result);
   submitted=true;ticks=0;path.Add(Vector3.zero);return;
  }
  path.Add(Quaternion.Inverse(heading)*(actor.transform.position-origin));ticks++;
  if(ticks>200)throw new Exception("Action did not complete independently of animation");
  if(r.Action!=CombatAction.Free)return;
  actor.Runner.ApplyMovement-=Sample;paths.Add(path.ToArray());
  var endpoint=path[path.Count-1];lines.Add($"trial={trial} actor={(trial<9?"player":"enemy")} action={(trial%9)/3} route={trial%3} ticks={ticks} end={endpoint.ToString("F6")}");
  if(++trial<18){actor=null;waitingLoad=true;EditorApplication.delayCall+=()=>{EditorSceneManager.LoadSceneInPlayMode(trial%3==0?"Assets/Scenes/GameScene.unity":"Assets/Scenes/CombatTraining.unity",new LoadSceneParameters(LoadSceneMode.Single));waitingLoad=false;};return;}
  for(int index=0;index<18;index+=3){var baseline=paths[index];foreach(int offset in new[]{1,2}){var other=paths[index+offset];float max=0,sum=0;int n=Math.Min(baseline.Length,other.Length);for(int i=0;i<n;i++){var delta=baseline[i]-other[i];delta.y=0;max=Mathf.Max(max,delta.magnitude);sum+=delta.sqrMagnitude;}float rms=Mathf.Sqrt(sum/n);bool pass=baseline.Length==other.Length && max<=.02f && rms<=.01f;lines.Add($"COMPARE trial={index} route={offset} samples={baseline.Length}/{other.Length} maxXZ={max:R} rmsXZ={rms:R} parity={pass}");if(!pass)throw new Exception("Same-animation hidden-renderer parity failed");}}
  lines.Add("PHASE5_SAME_ANIMATION_MOTION_PASS maxXZ<=0.02m rmsXZ<=0.01m; runtime GameScene versus training arena, hidden and rendered, scale=4");File.WriteAllLines("Logs/Phase5ArenaMotion.txt",lines);Finish(0);
 }catch(Exception ex){Fail(ex);}}
 static void Fail(Exception ex){lines.Add(ex.ToString());File.WriteAllLines("Logs/Phase5ArenaMotion.txt",lines);Debug.LogException(ex);Finish(1);}
 static void Finish(int code){finished=true;SessionState.SetBool("Phase5ArenaMotion",false);if(Application.isBatchMode)EditorApplication.Exit(code);else EditorApplication.isPlaying=false;}
}


