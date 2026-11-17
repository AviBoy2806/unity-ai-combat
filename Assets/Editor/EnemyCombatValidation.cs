using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using GameCombat;
[InitializeOnLoad]
public static class EnemyCombatValidation {
 static bool started; static readonly List<GameObject> objects=new(); static NavMeshData mesh;
 static EnemyCombatValidation(){if(SessionState.GetBool("EnemyCombatValidation",false))EditorApplication.update+=Tick;}
 [MenuItem("Tools/Combat/Validate Phases 3 and 4")]
 public static void Run(){EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);SessionState.SetBool("EnemyCombatValidation",true);EditorApplication.update+=Tick;EditorApplication.EnterPlaymode();}
 static void Check(bool ok,string name){if(!ok)throw new Exception("FAIL: "+name);Debug.Log("PASS: "+name);}
 static GameObject Go(string name){var g=new GameObject(name);objects.Add(g);return g;}
 static void Advance(CombatActionRunner r,float seconds){for(int i=0;i<Mathf.CeilToInt(seconds/.02f);i++)r.Tick(.02f);}
 static void Tick(){if(!EditorApplication.isPlaying || started)return;started=true;SessionState.SetBool("EnemyCombatValidation",false);int exit=0;
 try{Rules();Debug.Log("ENEMY_COMBAT_VALIDATION_PASS");}catch(Exception e){Debug.LogException(e);exit=1;}
 finally{foreach(var g in objects)if(g)UnityEngine.Object.DestroyImmediate(g);if(mesh)UnityEngine.Object.DestroyImmediate(mesh);EditorApplication.Exit(exit);}}
 static void Rules(){
 var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);objects.Add(floor);floor.layer=7;floor.transform.position=new Vector3(0,-.5f,0);floor.transform.localScale=new Vector3(30,1,30);
 var settings=NavMesh.GetSettingsByIndex(0);mesh=NavMeshBuilder.BuildNavMeshData(settings,new List<NavMeshBuildSource>{new NavMeshBuildSource{shape=NavMeshBuildSourceShape.Box,size=new Vector3(30,1,30),transform=Matrix4x4.TRS(new Vector3(0,-.5f,0),Quaternion.identity,Vector3.one),area=0}},new Bounds(Vector3.zero,new Vector3(40,10,40)),Vector3.zero,Quaternion.identity);NavMesh.AddNavMeshData(mesh);
 var target=Go("Observed opponent");target.transform.position=new Vector3(0,1,2);var opponent=target.AddComponent<Combatant>();opponent.light=AssetDatabase.LoadAssetAtPath<AttackDefinition>("Assets/CombatData/Light1.asset");
 var g=Go("Enemy");g.transform.position=new Vector3(0,1,0);var agent=g.AddComponent<NavMeshAgent>();agent.Warp(g.transform.position);var nav=g.AddComponent<EnemyPerceptionNav>();nav.target=target.transform;nav.obstacleLayers=1<<6;
 var body=g.AddComponent<enemy>();var actor=g.GetComponent<Combatant>();actor.faction=1;actor.light=AssetDatabase.LoadAssetAtPath<AttackDefinition>("Assets/CombatData/EnemyLight.asset");actor.heavy=AssetDatabase.LoadAssetAtPath<AttackDefinition>("Assets/CombatData/EnemyHeavy.asset");body.enemyCollider.height=2;body.grounded=true;body.SendMessage("Start");
 var host=g.GetComponent<EnemyBrainHost>();var motor=g.GetComponent<EnemyMotor>();var r=actor.Runner;
 // Agent creation snaps its transform onto the mesh before perception disables
 // updatePosition. Restore the capsule center so this fixture has physical feet on the floor.
 motor.Teleport(new Vector3(0,1,0),Quaternion.identity);Physics.SyncTransforms();
 Check(nav.CanSeePlayer(),"injected target visible without singleton");nav.SendMessage("Update");var memory=nav.LastSeenPosition;float seen=nav.LastSeenTime;
 var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);objects.Add(wall);wall.layer=6;wall.transform.position=new Vector3(0,1,1);wall.transform.localScale=new Vector3(4,4,.2f);Physics.SyncTransforms();
 Check(!nav.CanSeePlayer(),"wall occlusion");target.transform.position=new Vector3(1,1,2);nav.SendMessage("Update");Check(nav.LastSeenPosition==memory && nav.LastSeenTime==seen,"occlusion preserves last-known position and sighting time");UnityEngine.Object.DestroyImmediate(wall);
 var stream=new EnemyObservationStream();var cue=new CombatObservation{time=0,visible=true,distance=2,opponentAction=CombatAction.Attack,opponentPhase=AttackPhase.Windup,opponentGuard=true};Check(!stream.Sample(cue).visible,"no reaction before observation delay");cue.time=.16;Check(stream.Sample(cue).opponentPhase==AttackPhase.Windup,"public cue delivered after 160 ms");cue.time=.17;cue.visible=false;var masked=stream.Sample(cue);Check(!masked.visible && masked.relativePosition==Vector3.zero && float.IsPositiveInfinity(masked.distance) && !masked.opponentGuard && masked.opponentAction==CombatAction.Free,"immediate live feature masking");cue.visible=true;cue.time=.18;Check(!stream.Sample(cue).visible,"reacquisition cannot reuse stale cues");
 actor.AddPosture(33,0);actor.Health.takeDamage(7);float health=actor.Health.Health,posture=actor.Posture;
 Check(host.SetMode(EnemyControlMode.Test)==BrainSwitchResult.Applied && host.ActiveMode==EnemyControlMode.Test,"explicit test brain selection");
 Check(actor.Health.Health==health && actor.Posture==posture,"switch preserves health and posture exactly");
 foreach(var request in new[]{CombatRequest.Light,CombatRequest.Dodge}){
 r.SetGuard(false);Check(r.TryStart(request,Vector3.forward)==RequestResult.Accepted,"start committed "+request);uint generation=host.Generation;
 Check(host.SetMode(EnemyControlMode.Normal)==BrainSwitchResult.Deferred && host.Generation==generation,"switch cannot cancel "+request);
 r.Collect();Check(host.ActiveMode==EnemyControlMode.Test && r.Action!=CombatAction.Free,"owner retained during commitment");Advance(r,request==CombatRequest.Light?actor.light.Duration:.34f);r.Collect();Check(host.ActiveMode==EnemyControlMode.Normal && host.Generation!=generation,"switch at free boundary");host.SetMode(EnemyControlMode.Test);
 }
 Check(r.TryStart(CombatRequest.Dodge,Vector3.back)==RequestResult.Cooldown,"switch preserves dodge cooldown");Check(actor.Health.Health==health && actor.Posture<=posture && nav.LastSeenPosition==memory,"resources and perception memory preserved");
 foreach(var action in new[]{CombatAction.Stagger,CombatAction.Recoil}){r.Interrupt(.3f,action);Check(host.SetMode(EnemyControlMode.Normal)==BrainSwitchResult.Deferred,"defer during "+action);Advance(r,.3f);r.Collect();Check(host.ActiveMode==EnemyControlMode.Normal,"apply after "+action);host.SetMode(EnemyControlMode.Test);}
 
 foreach(float time in new[]{0f,.46f,.64f}){r.SetGuard(false);r.TryStart(CombatRequest.Light,Vector3.forward);Advance(r,time);var phase=r.Phase;uint gen=host.Generation;Check(host.SetMode(EnemyControlMode.Normal)==BrainSwitchResult.Deferred && gen==host.Generation,"defer in attack phase "+phase);Advance(r,actor.light.Duration);r.Collect();host.SetMode(EnemyControlMode.Test);}
 host.SetMode(EnemyControlMode.Test);r.Collect();Advance(r,.18f);r.Collect();Check(r.Guarding && host.Intent.localMove==Vector2.right,$"test brain actually owns guard and movement commands; visible={nav.CanSeePlayer()} engage={nav.CanEngagePlayer()} own={g.transform.position} feet={body.enemyCollider.bounds.min.y} target={target.transform.position} native={agent.nextPosition}");host.SetMode(EnemyControlMode.Normal);Check(!r.Guarding && host.Intent.localMove==Vector2.zero,"switch clears test brain held intents");
 uint old=host.Generation;host.SetMode(EnemyControlMode.Normal);Check(!host.BufferIntent(old,0,new CombatIntent{request=CombatRequest.Heavy}),"stale generation callback rejected");Check(host.SetMode(EnemyControlMode.ML)==BrainSwitchResult.Unavailable && host.ActiveMode==EnemyControlMode.Normal,"unavailable ML retains Normal");
 var controls=target.AddComponent<CombatDebugControls>();controls.opponent=actor;controls.Schedule(CombatRequest.Light);Check(!host.DebugOwned,"default-off debug commands inert");controls.enableDefenseDebugging=true;controls.Schedule(CombatRequest.Light);Check(host.DebugOwned,"opt-in debug acquires exclusive host");controls.enabled=false;Check(!host.DebugOwned,"disabled debug cancels pending command and releases free host");
 r.SetGuard(false);Check(host.AcquireDebug(),"exclusive debug acquisition");agent.SetDestination(new Vector3(3,0,3));r.Move();Check(motor.Owner==EnemyMotor.MovementOwner.Debug && !agent.hasPath && body.rb.linearVelocity==Vector3.zero,"debug clears navigation and velocity");
 r.TryStart(CombatRequest.Light,Vector3.forward);host.ReleaseDebug();r.Collect();Check(host.DebugOwned && r.Action==CombatAction.Attack,"debug release waits through commitment");Advance(r,actor.light.Duration);r.Collect();Check(!host.DebugOwned,"debug releases at safe boundary");
 Check(host.ActiveMode==EnemyControlMode.Normal && host.mode==EnemyControlMode.ML && !string.IsNullOrEmpty(host.FallbackReason),"debug release preserves unavailable-ML Normal fallback and reason");
 target.transform.position=new Vector3(0,1,2);Physics.SyncTransforms();r.Collect();Check(host.InCombat && body.stateMachine.currentState is EnemyCombatState,"visible target enters combat state within five metres");r.Move();Check(motor.Owner==EnemyMotor.MovementOwner.Combat && !agent.hasPath,"combat sole movement owner clears stale path");
 target.transform.position=new Vector3(0,1,9);Physics.SyncTransforms();r.Collect();Check(!host.InCombat && body.stateMachine.currentState is EnemyChaseState,"seven metre exit returns to chase");

 nav.detectPlayer=false;
 var chase=(EnemyChaseState)body.stateMachine.currentState;
 typeof(EnemyChaseState).GetField("searchStarted",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).SetValue(chase,Time.time-16);
 chase.Update();Check(body.stateMachine.currentState is EnemyIdleState,"unreachable investigation times out to idle");
 body.stateMachine.TryChangeState(body.stateMachine.currentState,EnemyState.EnemyStateType.chase);chase=(EnemyChaseState)body.stateMachine.currentState;NavMesh.SamplePosition(nav.LastSeenPosition,out var arrival,2,agent.areaMask);agent.Warp(arrival.position);agent.nextPosition=arrival.position;body.rb.position=arrival.position;nav.memoryDuration=0;chase.Update();
 Check(body.stateMachine.currentState is EnemyIdleState,"arrival memory wait completes investigation");
 r.TryStart(CombatRequest.Light,Vector3.forward);actor.Health.takeDamage(1000);r.Collect();r.Move();Check(r.Action==CombatAction.Dead && motor.Owner==EnemyMotor.MovementOwner.Dead && body.horizontalVelocity==Vector3.zero,"death stops commands and movement");body.stateMachine.Update();Check(body.stateMachine.currentState is EnemyDeadState,"death enters enemy FSM terminal state");Check(r.TryStart(CombatRequest.Light,Vector3.forward)==RequestResult.Dead,"death rejects further action");
 
 // Off-mesh is a safe stop, never a teleport to sampled ground.
 agent.enabled=false;Check(!motor.CanStep(Vector3.forward),"off-mesh movement rejected");agent.enabled=true;
 var brain=new ScriptedCombatBrain();brain.Reset();var o=new CombatObservation{visible=true,grounded=true,distance=1.9f,selfAction=CombatAction.Free,time=0,heavyReady=true};Check(brain.Decide(o).request==CombatRequest.Light,"normal starts deliberate light");o.selfAction=CombatAction.Attack;brain.Decide(o);o.selfAction=CombatAction.Free;o.time=1.1;Check(brain.Decide(o).request==CombatRequest.None,"circle after full recovery");o.time=1.71;Check(brain.Decide(o).request==CombatRequest.Light,"second light after recovery and circle");o.selfAction=CombatAction.Attack;brain.Decide(o);o.selfAction=CombatAction.Free;o.time=2.81;Check(brain.Decide(o).request==CombatRequest.None,"two lights require neutral pause");o.time=3.42;Check(brain.Decide(o).request==CombatRequest.Heavy,"heavy after two completed lights");brain.Reset();o.selfPosture=80;o.distance=2;Check(brain.Decide(o).localMove==Vector2.zero,"high posture no longer orders early retreat");o.selfPosture=60;Check(brain.Decide(o).request==CombatRequest.None,"posture hysteresis avoids voluntary attacks");
 brain.Reset();o=new CombatObservation{time=.16,visible=true,grounded=true,dodgeReady=true,heavyReady=true,distance=1.9f,selfAction=CombatAction.Free,opponentAction=CombatAction.Attack,opponentPhase=AttackPhase.Windup,opponentElapsed=0,opponentWindup=.45f,opponentReach=2,opponentAttackId=1};Check(!brain.Decide(o).guardHeld,"deflection waits for public telegraph timing");o.time=.34;Check(brain.Decide(o).guardHeld,"delayed telegraph raises guard");brain.Reset();o.selfPosture=80;o.opponentElapsed=.4f;var highDefense=brain.Decide(o);Check(highDefense.request==CombatRequest.None && highDefense.guardHeld && highDefense.localMove==Vector2.zero,"high posture threat guards without automatic backward dodge");

 }
}
