using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using GameCombat;
using GameCombat.Training;

[InitializeOnLoad]
public static class Phase5EnvironmentValidation {
 static CombatTrainingSession session;static CombatTrainingArena[] arenas;static int ticks,resets,actions,events;static bool attached,finished;static double deadline;static readonly List<string> lines=new();
 static Phase5EnvironmentValidation(){if(SessionState.GetBool("P5EnvironmentValidation",false))EditorApplication.update+=Update;}
 public static void Run(){SessionState.SetBool("P5EnvironmentValidation",true);EditorSceneManager.OpenScene(Phase5EnvironmentBuild.ScenePath);EditorApplication.update+=Update;EditorApplication.EnterPlaymode();}
 static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
 static void Update(){if(finished || !EditorApplication.isPlaying || attached)return;try{
  session=UnityEngine.Object.FindFirstObjectByType<CombatTrainingSession>();if(!session || !session.arenas[0].RewardsReady())return;
  deadline=EditorApplication.timeSinceStartup+300;arenas=session.arenas;CombatWorld.AfterStep+=Sample;attached=true;
  Check(!UnityEngine.Object.FindFirstObjectByType<Camera>(),"Training camera present");Check(!PlayerRefManager.Instance,"Training registered singleton");
  Check(arenas[0].learner.GetComponent<EnemyPerceptionNav>().Target==arenas[0].opponent.transform,"Injected target mismatch");
  foreach(var arena in arenas){var captured=arena;arena.ResetCompleted+=()=>VerifyAtomic(captured);}
  RewardCases(arenas[0]);lines.Add("PASS reward ledger: dual callbacks, actual damage fields, defense cap, terminal outcomes and potential transitions");
 }catch(Exception e){Fail(e);}}
 static object Field(object target,string name)=>target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(target);
 static int Count(object target)=>(int)target.GetType().GetProperty("Count").GetValue(target);
 static void VerifyAtomic(CombatTrainingArena arena){
  try{
   Check(arena.Resetting && arena.learner.ContactsSuppressed && arena.opponent.ContactsSuppressed,"Reset not atomic");
   foreach(var actor in new[]{arena.learner,arena.opponent}){var r=actor.Runner;
    Check(r.Clock==0 && r.AttackId==0 && r.Elapsed==0 && r.PreviousElapsed==0 && !r.Attack && r.Action==CombatAction.Free,"Runner timeline leaked");
    Check(!r.Guarding && !r.Deflecting && r.DeflectionRearmFraction==0 && r.DodgeCooldownFraction==0 && !r.ComboQueued && !r.Broken,"Cooldown/rearm/queue leaked");
    Check(Count(Field(r,"hits"))==0,"Hit history retained");
    var rb=actor.GetComponent<Rigidbody>();Check(!rb.isKinematic && rb.linearVelocity==Vector3.zero && rb.angularVelocity==Vector3.zero,"Physics state leaked");
    Check(actor.Health.Health==actor.Health.maxHealth && actor.Posture==0 && double.IsNegativeInfinity(actor.LastPostureTime),"Resources/recovery leaked");
   }
   var input=arena.opponent.GetComponent<PlayerInputComponent>();Check(input.ConsumeCombat()==CombatRequest.None && !input.ConsumeGuard() && input.input.movement==Vector2.zero,"Player input queue leaked");
   var host=arena.learner.GetComponent<EnemyBrainHost>();Check(!host.InCombat && !host.DebugOwned && !host.HasPendingSwitch && host.Intent.request==CombatRequest.None,"Controller state leaked");
   var stream=Field(host,"stream");Check(Count(Field(stream,"samples"))==0,"Delayed observations leaked");
   Check(Field(arena.agent,"pending")==null && Field(arena.agent,"receive")==null,"Agent callback leaked");
   var perception=arena.learner.GetComponent<EnemyPerceptionNav>();Check(float.IsNegativeInfinity(perception.LastSeenTime) && perception.LastSeenPosition==Vector3.zero,"Perception memory leaked");
   Check(arena.opponent.GetComponent<Player>().stateMachine.currentState is IdleState && !arena.learner.GetComponent<enemy>().isDead,"Actor state leaked");
   var contacts=new List<ResolvedContact>{CombatResolver.Evaluate(arena.learner,arena.opponent,arena.learner.light,arena.learner.transform.position)};CombatResolver.ApplyBatch(contacts,0);Check(contacts.Count==0,"Reset contact was processed");
  }catch(Exception e){Fail(e);}
 }
 static void RewardCases(CombatTrainingArena arena){
  Check(arena.ValidateGeneralization(out _),"Baseline generalization context");float damage=arena.learner.light.damage;arena.learner.light.damage=damage+1;Check(!arena.ValidateGeneralization(out _),"Damage variation must be rejected");arena.learner.light.damage=damage;
  var ledger=new TrainingRewards(arena.learner,arena.opponent);var r=new CombatResult{attackInstanceId=8,attackerId=arena.learner.Id,defenderId=arena.opponent.Id,outcome=CombatOutcome.Hit,actualHealthDamage=7};
  Check(Mathf.Abs(ledger.Record(r)-.0175f)<.00001f,"Actual damage reward");Check(ledger.Record(r)==0 && ledger.Dealt==7,"Duplicate paid twice");
  ledger.Clear();for(ulong i=1;i<=20;i++)ledger.Record(new CombatResult{attackInstanceId=i,attackerId=arena.opponent.Id,defenderId=arena.learner.Id,outcome=CombatOutcome.Deflect});Check(Mathf.Abs(ledger.DefenseReward-.05f)<1e-6,"Deflection reward cap");
  Check(TrainingRewards.Terminal(true,true,false,false,false)=="draw","Simultaneous death");Check(TrainingRewards.Terminal(false,false,false,false,true)=="timeout","Timeout");Check(TrainingRewards.Terminal(false,false,true,false,false)=="loss","Learner OOB");Check(TrainingRewards.Terminal(false,false,false,true,false)=="win","Opponent OOB");
  Check(Mathf.Abs(ledger.Shape(-.5f,0)-.025f)<.00001f,"Terminal potential");
 }
 static void Sample(){if(finished)return;try{
  ticks++;var first=arenas[0];Check(Mathf.Abs(Time.fixedDeltaTime-.02f)<1e-6,"Physics timestep changed");
  if(ticks<=600){var r=first.learner.Runner;if(r.Guarding)actions|=2;else if(r.Action==CombatAction.Free)actions|=1;else if(r.Action==CombatAction.Attack)actions|=r.Attack==first.learner.heavy?8:4;else if(r.Action==CombatAction.Dodge)actions|=16;
   if(ticks==300){var p=first.opponent;var point=first.learner.transform.position+first.learner.facing.forward*1.4f;p.GetComponent<Rigidbody>().position=point;p.transform.position=point;Physics.SyncTransforms();}
   events=Mathf.Max(events,first.Rewards.Duplicates);if(ticks==600){Check(actions==31,"Single arena heuristic did not exercise every action: "+actions);Check(events>0,"No actual resolver contacts in live duel");lines.Add("PASS single-arena live shared loop: all heuristic actions, actual contacts at controlled contact spacing, no camera/input/singleton");}
   return;
  }
  // Scale only after one live arena and repeated dirty resets pass.
  if(resets==1000 && !arenas[1].gameObject.activeSelf){for(int i=1;i<8;i++)arenas[i].gameObject.SetActive(true);return;}
  if(resets>=1000 && arenas.Any(a=>!a.RewardsReady()))return;
  var active=arenas.Where(a=>a.gameObject.activeSelf).ToArray();
  if(resets==1998){first.enabled=false;first.enabled=true;}
  foreach(var arena in active){
   if(resets>0 && arena.ResetCount>1){
    Check(arena.learner.Runner.AttackId==0,"Attack ID leaked");Check(arena.learner.Health.Health==arena.learner.Health.maxHealth && arena.opponent.Health.Health==arena.opponent.Health.maxHealth,"Health leaked");
    Check(arena.learner.Posture==0 && arena.opponent.Posture==0,"Posture leaked");Check(!arena.learner.Runner.Guarding && arena.learner.Runner.DodgeReady,"Guard/cooldown leaked");
    Check(arena.learner.Runner.Action==CombatAction.Free && arena.opponent.Runner.Action==CombatAction.Free,"Action leaked");
    Check(arena.learner.Runner.MarkHit(arena.opponent),"Hit history leaked");Check(arena.Record.decisions==0 && arena.Rewards.Dealt==0,"Episode ledger leaked");
    var nav=arena.learner.GetComponent<NavMeshAgent>();Check(!nav.hasPath,"Navigation leaked");Check(!arena.learner.GetComponent<EnemyBrainHost>().DebugOwned,"Controller lease leaked");
   }
   if(resets>=2000)continue;
   // Dirty action IDs, pending combo/input/guard, resource state, movement and navigation.
   foreach(var actor in new[]{arena.learner,arena.opponent}){
    var r=actor.Runner;r.SetGuard(false);
    switch(resets%6){
     case 0:r.TryStart(CombatRequest.Light,actor.facing.forward,true);if(r.Attack.nextCombo){r.Tick(r.Attack.Duration-.1f);r.TryStart(CombatRequest.Light,actor.facing.forward,true);}break;
     case 1:r.SetGuard(true);break;
     case 2:r.TryStart(CombatRequest.Dodge,actor.facing.forward,true);break;
     case 3:actor.AddPosture(actor.tuning.maxPosture,r.Clock);break;
     case 4:actor.Health.takeDamage(actor.Health.maxHealth);break;
     case 5:r.Interrupt(.3f,CombatAction.Recoil);break;
    }
    actor.AddPosture(25,r.Clock);actor.Health.takeDamage(3);var rb=actor.GetComponent<Rigidbody>();if(!rb.isKinematic){rb.linearVelocity=new Vector3(2,1,2);rb.angularVelocity=Vector3.one;}
   }
   var brain=arena.learner.GetComponent<EnemyBrainHost>();brain.GetType().GetField("heavyCooldownUntil",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(brain,99d);
   var memory=arena.learner.GetComponent<EnemyPerceptionNav>();memory.GetType().GetField("<LastSeenTime>k__BackingField",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(memory,99f);memory.GetType().GetField("<LastSeenPosition>k__BackingField",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(memory,Vector3.one*99);
   ((EnemyObservationStream)Field(brain,"stream")).Sample(new CombatObservation{time=99,visible=true,relativePosition=Vector3.one});
   var input=arena.opponent.GetComponent<PlayerInputComponent>();input.QueueCombat(CombatRequest.Heavy);input.SetBlock(true);input.input.movement=Vector2.one;
   arena.RequestReset();arena.RequestReset();
  }
  resets++;
  if(resets==1000)lines.Add("PASS one arena: 1000 repeated dirty two-actor resets, coalesced requests");
  if(resets<2001)return;
  foreach(var a in active)foreach(var b in active)if(a!=b){
   a.learner.GetComponent<EnemyPerceptionNav>().target=b.opponent.transform;Check(!a.learner.GetComponent<EnemyPerceptionNav>().Target,"Foreign target accepted");a.learner.GetComponent<EnemyPerceptionNav>().target=a.opponent.transform;
   var contacts=new List<ResolvedContact>{CombatResolver.Evaluate(a.learner,b.opponent,a.learner.light,a.learner.transform.position)};float health=b.opponent.Health.Health;CombatResolver.ApplyBatch(contacts,0);Check(contacts.Count==0 && b.opponent.Health.Health==health,"Cross-arena resolver contact");
   foreach(var ca in a.learner.GetComponentsInChildren<Collider>())foreach(var cb in b.opponent.GetComponentsInChildren<Collider>())Check(Physics.GetIgnoreCollision(ca,cb),"Cross-arena physics not excluded");
  }
  lines.Add("PASS eight arenas: 1000 repeated dirty resets each; cross-arena target, physics and resolver injection rejected");
  int callbacks=0;first.EpisodeCompleted+=record=>{callbacks++;first.RequestReset();};first.Finish("timeout");first.Finish("loss");Check(callbacks==1,"Episode callback reentry duplicated terminal/reset");
  lines.Add("PASS atomic reset snapshots across attack/combo, guard, dodge/cooldown, full posture break, death and recoil: velocities, resources, action IDs/timelines, queues, hit history, cooldown/rearm, controller, perception, delayed observations and callback invalidation; reset contact suppression; one terminal callback");
  Finish(0);
 }catch(Exception e){Fail(e);}}
 static void Fail(Exception exception){lines.Add("FAIL "+exception);Debug.LogException(exception);Finish(1);}
 static void Finish(int code){finished=true;SessionState.SetBool("P5EnvironmentValidation",false);CombatWorld.AfterStep-=Sample;Directory.CreateDirectory("Logs/Phase5Environment");File.WriteAllLines("Logs/Phase5Environment/Validation.txt",lines);Debug.Log(code==0?"PHASE5_ENVIRONMENT_VALIDATION_PASS":"PHASE5_ENVIRONMENT_VALIDATION_FAIL");if(Application.isBatchMode)EditorApplication.Exit(code);else EditorApplication.isPlaying=false;}
 static bool RewardsReady(this CombatTrainingArena arena)=>arena && arena.Rewards!=null;
}
