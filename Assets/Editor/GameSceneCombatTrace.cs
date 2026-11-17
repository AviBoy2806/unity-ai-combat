using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using GameCombat;

// Normal brain, actual scene actors, saved attack assets, real CombatWorld queries.
// Test-only player placement keeps pressure on an evasive target; it never moves the enemy.
[InitializeOnLoad]
public static class GameSceneCombatTrace {
 static Player player; static enemy body; static Combatant target,source; static double end,ready;
 static ulong queued=ulong.MaxValue; static bool started,done,wasBroken; static Vector3 anchor;
 static int contacts,breaks; static float shift,peak; static double lastSample=-1;
 static GameSceneCombatTrace(){if(SessionState.GetBool("GameSceneCombatTrace",false))EditorApplication.update+=Tick;}
 [MenuItem("Tools/Combat/Trace normal enemy under scene combo pressure")]
 public static void Run(){EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");SessionState.SetBool("GameSceneCombatTrace",true);EditorApplication.update+=Tick;EditorApplication.EnterPlaymode();}
 static void Finish(int code){done=true;SessionState.SetBool("GameSceneCombatTrace",false);EditorApplication.update-=Tick;Debug.Log($"SCENE_PRESSURE_END contacts={contacts} breaks={breaks} peak={peak:R} stunnedRootShift={shift:R}");if(Application.isBatchMode)EditorApplication.Exit(code);else EditorApplication.isPlaying=false;}
 static void Tick(){if(done || !EditorApplication.isPlaying)return;try{
 if(!started){player=UnityEngine.Object.FindFirstObjectByType<Player>();body=UnityEngine.Object.FindFirstObjectByType<enemy>();if(!player || !body)return;source=player.GetComponent<Combatant>();target=body.GetComponent<Combatant>();started=true;ready=EditorApplication.timeSinceStartup+.5;end=EditorApplication.timeSinceStartup+35;
  // Prevent test death so many normal combat exchanges can be observed. No gameplay assets are changed.
  foreach(var a in new[]{source,target}){a.Health.maxHealth=10000;typeof(HealthComponent).GetField("health",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(a.Health,10000f);}
  target.Result+=hit=>{if(hit.defenderId!=target.Id)return;contacts++;peak=Mathf.Max(peak,target.Posture);Debug.Log($"SCENE_PRESSURE_CONTACT outcome={hit.outcome} posture={target.Posture:R} broken={target.Runner.Broken} action={target.Runner.Action} state={body.CurrentStateName} move={body.horizontalVelocity} time={target.Runner.Clock:F2}");};
 }
 if(EditorApplication.timeSinceStartup<ready)return;
 var r=source.Runner;var er=target.Runner;
 if(er.Clock!=lastSample){lastSample=er.Clock;if(er.Broken){if(!wasBroken){anchor=body.transform.position;breaks++;}shift=Mathf.Max(shift,Vector3.Distance(anchor,body.transform.position));if(!body.IsStunned || !body.rb.isKinematic || shift>.001f)throw new Exception("Normal combat moved or left state during full break");}wasBroken=er.Broken;}
 if(r.Action==CombatAction.Free && !player.AttackFacingLocked || r.ComboWindowOpen && !r.ComboQueued && queued!=r.AttackId){
  if(r.Action==CombatAction.Free){var pos=body.transform.position+body.transform.forward*1.3f;pos.y=body.enemyCollider.bounds.min.y+(player.transform.position.y-player.playerCollider.bounds.min.y);player.rb.position=pos;player.transform.position=pos;player.rb.linearVelocity=Vector3.zero;player.model.rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(body.transform.position-pos,Vector3.up));Physics.SyncTransforms();}
  queued=r.AttackId;player.inputComponent.QueueCombat(CombatRequest.Light);
 }
 if(EditorApplication.timeSinceStartup>=end)Finish(0);
 }catch(Exception e){Debug.LogException(e);Finish(1);}}
}
