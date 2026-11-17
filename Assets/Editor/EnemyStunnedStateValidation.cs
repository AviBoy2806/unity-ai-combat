using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using GameCombat;
[InitializeOnLoad]
public static class EnemyStunnedStateValidation {
 public sealed class ContactProbe:MonoBehaviour {public Collider target;public int contacts;void OnCollisionEnter(Collision c){if(c.collider==target)contacts++;}void OnCollisionStay(Collision c){if(c.collider==target)contacts++;}}
 sealed class CountingBrain:IEnemyCombatBrain {public int decisions;public void Reset(){}public CombatIntent Decide(in CombatObservation o){decisions++;return new CombatIntent{request=CombatRequest.Dodge,localMove=Vector2.down,guardHeld=true};}}
 static bool started;static int checks;static readonly List<GameObject> objects=new();
 static EnemyStunnedStateValidation(){if(SessionState.GetBool("EnemyStunnedStateValidation",false))EditorApplication.update+=Tick;}
 [MenuItem("Tools/Combat/Test explicit enemy StunnedState")]
 public static void Run(){EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);SessionState.SetBool("EnemyStunnedStateValidation",true);EditorApplication.update+=Tick;EditorApplication.EnterPlaymode();}
 static void Check(bool ok,string name){if(!ok)throw new Exception("FAIL: "+name);checks++;Debug.Log("PASS: "+name);}
 static GameObject Go(string name){var g=new GameObject(name);objects.Add(g);return g;}
 static AttackDefinition Def(string n)=>AssetDatabase.LoadAssetAtPath<AttackDefinition>("Assets/CombatData/"+n+".asset");
 static void Hit(Combatant a,Combatant d){var c=CombatResolver.Evaluate(a,d,a.light,a.transform.position);CombatResolver.ApplyBatch(new List<ResolvedContact>{c},d.Runner.Clock);}
 static void Tick(){if(!EditorApplication.isPlaying || started)return;started=true;SessionState.SetBool("EnemyStunnedStateValidation",false);int code=0;var old=Physics.simulationMode;try{Physics.simulationMode=SimulationMode.Script;Rules();Debug.Log($"ENEMY_STUNNED_STATE_VALIDATION_PASS {checks} checks");}catch(Exception e){Debug.LogException(e);code=1;}finally{Physics.simulationMode=old;foreach(var g in objects)if(g)UnityEngine.Object.DestroyImmediate(g);EditorApplication.Exit(code);}}
 static void Rules(){
 var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);objects.Add(floor);floor.layer=7;floor.transform.position=Vector3.down*.5f;floor.transform.localScale=new Vector3(40,1,40);
 var mesh=NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByIndex(0),new List<NavMeshBuildSource>{new NavMeshBuildSource{shape=NavMeshBuildSourceShape.Box,size=new Vector3(40,1,40),transform=Matrix4x4.TRS(Vector3.down*.5f,Quaternion.identity,Vector3.one)}},new Bounds(Vector3.zero,new Vector3(50,10,50)),Vector3.zero,Quaternion.identity);var mi=NavMesh.AddNavMeshData(mesh);
 try{
 var pusher=Go("simulated player body");pusher.transform.position=new Vector3(0,1,1.3f);var push=pusher.AddComponent<Rigidbody>();push.useGravity=false;push.mass=1000;push.constraints=RigidbodyConstraints.FreezeRotation;var col=pusher.AddComponent<CapsuleCollider>();col.height=2;col.radius=.5f;var source=pusher.AddComponent<Combatant>();source.light=Def("Light1");source.heavy=Def("RunningHeavy");
 var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Enemy/Enemy.prefab");Check(prefab,"actual saved enemy prefab available");var g=UnityEngine.Object.Instantiate(prefab);objects.Add(g);var body=g.GetComponent<enemy>();var actor=g.GetComponent<Combatant>();var r=actor.Runner;var host=g.GetComponent<EnemyBrainHost>();var motor=g.GetComponent<EnemyMotor>();var nav=g.GetComponent<EnemyPerceptionNav>();var anim=body.animator;nav.target=pusher.transform;motor.Teleport(Vector3.up,Quaternion.identity);Physics.SyncTransforms();nav.SendMessage("Update");
 Check(body.stateMachine!=null && !body.IsStunned,"actual prefab initializes state machine before first combat tick");Check(anim && anim.runtimeAnimatorController && g.GetComponentInChildren<SkinnedMeshRenderer>(),"original enemy model and Generic-rig controller imported, no replacement clips");Check(anim.HasState(0,Animator.StringToHash("Stunned")),"saved enemy controller has a dedicated Stunned animation state");
 // Every exploration/combat state must be interrupted synchronously, without Update/FixedUpdate.
 foreach(var origin in new[]{EnemyState.EnemyStateType.idle,EnemyState.EnemyStateType.wander,EnemyState.EnemyStateType.chase,EnemyState.EnemyStateType.combat}){
 body.stateMachine.ForceChangeState(origin);r.SetGuard(false);actor.AddPosture(100,r.Clock);Hit(source,actor);
 Check(body.IsStunned && body.CurrentStateName=="EnemyStunnedState" && r.Broken,"immediate StunnedState entry from "+origin);
 Check(body.rb.isKinematic && motor.Owner==EnemyMotor.MovementOwner.Stunned,"stunned state owns stationary motor immediately from "+origin);
 for(int i=0;i<175;i++)r.Tick(.02f);Check(!body.IsStunned && r.Action==CombatAction.Free,"runner alone releases StunnedState from "+origin);
 }
 // Reset test health only; production code has no health reset path here.
 typeof(HealthComponent).GetField("health",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(actor.Health,100f);actor.ResetBrokenPosture();host.SetMode(EnemyControlMode.Normal);r.SetGuard(false);r.TryStart(CombatRequest.Heavy,Vector3.forward);r.Tick(actor.heavy.Windup+.02f);r.Move();anim.Update(.02f);
 var spy=new CountingBrain();typeof(EnemyBrainHost).GetField("brain",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(host,spy);
 var constraints=body.rb.constraints;bool kinematic=body.rb.isKinematic;uint oldGeneration=host.Generation;actor.AddPosture(100,r.Clock);Hit(source,actor);
 Check(body.IsStunned && r.Attack==null && !r.SamplesActive && !r.Guarding,"full break cancels active heavy, samples and guard immediately");Check(host.Generation!=oldGeneration && host.Intent.localMove==Vector2.zero && !host.EscapePending,"state entry invalidates stale controller commands");
 anim.Update(0);Check(anim.GetCurrentAnimatorStateInfo(0).IsName("Stunned") && anim.GetFloat("Speed")==0 && !anim.GetBool("Blocking"),"original animator enters Stunned, never locomotion retreat");
 foreach(var next in new[]{EnemyState.EnemyStateType.idle,EnemyState.EnemyStateType.wander,EnemyState.EnemyStateType.chase,EnemyState.EnemyStateType.combat}){body.stateMachine.TryChangeState(body.stateMachine.currentState,next);body.stateMachine.ForceChangeState(next);Check(body.IsStunned,"state rejects normal and forced escape to "+next);}
 Check(!host.AcquireDebug() && host.SetMode(EnemyControlMode.Test)==BrainSwitchResult.Deferred,"debug and controller switching cannot take ownership during stun");Check(!host.BufferIntent(oldGeneration,1,new CombatIntent{request=CombatRequest.Heavy}),"old callbacks cannot command stunned actor");
 var probe=pusher.AddComponent<ContactProbe>();probe.target=body.enemyCollider;Vector3 start=body.rb.position;float shift=0;int healthHits=0;
 for(int i=0;i<174;i++){
 push.linearVelocity=Vector3.back*4;
 r.Collect();r.Tick(.02f);r.Move();body.stateMachine.Update();body.stateMachine.FixedUpdate();nav.MoveTo(Vector3.back*4,5,0);
 var presentation=g.GetComponent<CombatPresentation>();presentation.SendMessage("Update");anim.Update(.02f);presentation.SendMessage("LateUpdate");Physics.SyncTransforms();Physics.Simulate(.02f);shift=Mathf.Max(shift,Vector3.Distance(start,body.rb.position));
 if(i==10 || i==65 || i==120){float hp=actor.Health.Health;Hit(source,actor);if(actor.Health.Health<hp)healthHits++;}
 if(i%20==0){r.SetGuard(true);Check(body.IsStunned && r.TryStart(CombatRequest.Light,Vector3.forward)==RequestResult.Busy && r.TryStart(CombatRequest.Heavy,Vector3.forward)==RequestResult.Busy && r.TryStart(CombatRequest.Dodge,Vector3.back)==RequestResult.Busy && !r.Guarding,"cannot attack, guard or dodge while StunnedState owns actor "+i);Check(anim.GetCurrentAnimatorStateInfo(0).IsName("Stunned") && spy.decisions==0 && !host.EscapePending && !nav.Agent.hasPath,"no brain, navigation, escape or locomotion while stunned "+i);}
 }
 Check(probe.contacts>0 && shift<.001f,"real enemy prefab root stationary under physical contact: contacts "+probe.contacts+" shift "+shift);Check(healthHits==3 && actor.Health.Health==60,"real enemy hurt by three punish hits during stun, no immunity");Check(r.Broken && r.Remaining>.01f,"ordinary hits neither reset nor shorten full 3.5-second timer");Check(body.rb.constraints==constraints,"no constraint-bit hacks or added axis locks");
 push.linearVelocity=Vector3.zero;col.enabled=false;push.position=new Vector3(0,1,1.3f);pusher.transform.position=push.position;Physics.SyncTransforms();r.SetGuard(false);r.Tick(.02f);Check(!body.IsStunned && !r.Broken && r.Action==CombatAction.Free && body.rb.isKinematic==kinematic,"timer ends state cleanly and restores normal motor mode");Check(actor.Posture<=40 && !host.EscapePending && actor.Health.Health==60,"recovery preserves health and clears stun-hit escape history");r.Collect();Check(host.ActiveMode==EnemyControlMode.Test,"deferred controller change applies only after full state exit");
 // Re-enter from dodge, survive disable/enable, then die while stunned.
 host.SetMode(EnemyControlMode.Normal);r.SetGuard(false);r.TryStart(CombatRequest.Dodge,Vector3.back);actor.AddPosture(100,r.Clock);Hit(source,actor);Check(body.IsStunned && r.Action==CombatAction.Stagger && body.rb.isKinematic,"StunnedState interrupts dodge too");float remaining=r.Remaining;g.SetActive(false);g.SetActive(true);Check(body.IsStunned && body.rb.isKinematic && r.Remaining==remaining,"reenable restores state ownership without restarting timer");actor.Health.takeDamage(1000);Check(body.stateMachine.currentState is EnemyDeadState && !body.IsStunned && r.Action==CombatAction.Dead && body.rb.isKinematic==kinematic,"death immediately wins and exits stationary mode");body.stateMachine.ForceChangeState(EnemyState.EnemyStateType.idle);Check(body.stateMachine.currentState is EnemyDeadState,"dead actor cannot resume ordinary AI");
 // No Animator or NavMesh is required for state completion.
 var plain=Go("animation free enemy");var e=plain.AddComponent<enemy>();var a=plain.GetComponent<Combatant>();a.light=Def("EnemyLight");a.heavy=Def("EnemyHeavy");a.postureBreakSeconds=.12f;a.AddPosture(100,0);Hit(source,a);Check(e.IsStunned && !e.animator,"explicit stun works without animation/navigation");for(int i=0;i<6;i++)a.Runner.Tick(.02f);Check(!e.IsStunned && a.Runner.Action==CombatAction.Free,"animation-free state completes from shared timer");
 }finally{mi.Remove();UnityEngine.Object.DestroyImmediate(mesh);}
 }
}
