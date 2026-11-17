using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using GameCombat;
[InitializeOnLoad]
public static class CombatRepairValidation {
 static bool started;static readonly List<GameObject> objects=new();static int assertions;
 static CombatRepairValidation(){if(SessionState.GetBool("CombatRepairValidation",false))EditorApplication.update+=Tick;}
 [MenuItem("Tools/Combat/Validate acceptance repairs (empty test scene)")]
 public static void Run(){
 var controller=AnimatorController.CreateAnimatorControllerAtPath("Assets/RepairValidation.controller");
 foreach(var name in new[]{"GroundMovement","loco","Attack 1","Attack 2","Attack 3","Heavy Attack","Locked On 0"}){var state=controller.layers[0].stateMachine.AddState(name);var clip=new AnimationClip();clip.SetCurve("",typeof(Transform),"localPosition.y",AnimationCurve.Linear(0,0,3,0));AssetDatabase.AddObjectToAsset(clip,controller);state.motion=clip;}
 foreach(var name in new[]{"Attack","Heavy Attack","Dodge","Die"})controller.AddParameter(name,AnimatorControllerParameterType.Trigger);
 foreach(var name in new[]{"CombatComboHold","Blocking","IsGrounded","Is Locked On"})controller.AddParameter(name,AnimatorControllerParameterType.Bool);
 foreach(var name in new[]{"Speed","VerticalDir"})controller.AddParameter(name,AnimatorControllerParameterType.Float);
 AssetDatabase.SaveAssets();EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);SessionState.SetBool("CombatRepairValidation",true);EditorApplication.update+=Tick;EditorApplication.EnterPlaymode();
 }
 static void Check(bool value,string label){if(!value)throw new Exception("FAIL: "+label);assertions++;Debug.Log("PASS: "+label);}
 static GameObject Go(string name){var g=new GameObject(name);objects.Add(g);return g;}
 static void Advance(CombatActionRunner r,float seconds){for(int i=0;i<Mathf.CeilToInt(seconds/.02f-1e-5f);i++)r.Tick(.02f);}
 static void InputUpdate(){typeof(InputSystem).GetMethod("Update",BindingFlags.Static|BindingFlags.NonPublic,null,new[]{typeof(InputUpdateType)},null).Invoke(null,new object[]{InputUpdateType.Dynamic});}
 static AttackDefinition Def(string name)=>AssetDatabase.LoadAssetAtPath<AttackDefinition>("Assets/CombatData/"+name+".asset");
 static Combatant Actor(string name,Vector3 position){var g=Go(name);g.transform.position=position;var a=g.AddComponent<Combatant>();a.light=Def("EnemyLight");a.heavy=Def("EnemyHeavy");g.AddComponent<BoxCollider>().center=Vector3.up*.8f;g.AddComponent<Hurtbox>().owner=a;return a;}
 static CombatOutcome Contact(Combatant a,Combatant d){var c=CombatResolver.Evaluate(a,d,a.light,a.transform.position);CombatResolver.ApplyBatch(new List<ResolvedContact>{c},d.Runner.Clock);return c.outcome;}
 static Player MakePlayer(){var g=Go("Player input/motor fixture");g.SetActive(false);g.AddComponent<Rigidbody>().useGravity=false;var capsule=g.AddComponent<CapsuleCollider>();capsule.center=Vector3.up;capsule.height=2;capsule.radius=.5f;g.AddComponent<LockOn>();var a=g.AddComponent<Combatant>();a.light=Def("Light1");a.heavy=Def("RunningHeavy");g.AddComponent<PlayerInputComponent>();var model=Go("Model");model.transform.SetParent(g.transform,false);var anim=model.AddComponent<Animator>();anim.runtimeAnimatorController=AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/RepairValidation.controller");var player=g.AddComponent<Player>();typeof(Player).GetField("<model>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(player,model.transform);g.AddComponent<PlayerCombatAdapter>();a.facing=model.transform;g.SetActive(true);anim.Update(0);return player;}
 static void Tick(){if(!EditorApplication.isPlaying || started)return;started=true;SessionState.SetBool("CombatRepairValidation",false);int code=0;
 try{Rules();Debug.Log($"COMBAT_REPAIR_VALIDATION_PASS {assertions} assertions");}catch(Exception e){Debug.LogException(e);code=1;}finally{foreach(var g in objects)if(g)UnityEngine.Object.DestroyImmediate(g);EditorApplication.Exit(code);}}
 static void Rules(){
 using var inputScope = new ValidationInputScope();
 var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);objects.Add(floor);floor.layer=7;floor.transform.position=new Vector3(0,-.5f,0);floor.transform.localScale=new Vector3(40,1,40);
 // Full runner/resolver path, tested in both actor roles with normal and heavy attacks.
 for(int role=0;role<2;role++){
 var a=Actor("Attacker "+role,new Vector3(10,0,0));var d=Actor("Defender "+role,new Vector3(10,0,1.3f));a.faction=role;d.faction=1-role;d.facing.rotation=Quaternion.Euler(0,180,0);
 CombatResult last=default;d.Result+=r=>last=r;
 a.Runner.TryStart(CombatRequest.Light,Vector3.forward);Advance(a.Runner,.46f);d.Runner.SetGuard(true);
 float health=d.Health.Health;Check(Contact(a,d)==CombatOutcome.Deflect && last.outcome==CombatOutcome.Deflect,"frontal fresh guard actually produces Deflect role "+role);
 Check(d.Health.Health==health && last.actualHealthDamage==0,"deflect zero defender health damage");Check(a.Posture==25 && a.Runner.Action==CombatAction.Recoil,"deflect attacker posture and recoil");Check(!a.Runner.SamplesActive && a.Runner.Attack==null,"recoil immediately clears future attack samples");
 for(int i=0;i<14;i++){Check(a.Runner.TryStart(CombatRequest.Light,Vector3.forward)==RequestResult.Busy,"no action during recoil");a.Runner.SetGuard(true);Check(!a.Runner.Guarding,"no guard during recoil");a.Runner.Tick(.02f);}Check(a.Runner.Remaining>.01f && a.Runner.Action==CombatAction.Recoil,"recoil remains until 0.30 seconds");a.Runner.Tick(.02f);Check(a.Runner.Action==CombatAction.Free && !a.Runner.Broken,"ordinary deflection is not posture-break stun");a.Runner.SetGuard(false);
 Advance(d.Runner,.18f);d.Runner.SetGuard(true);Check(!d.Runner.Deflecting && Contact(a,d)==CombatOutcome.Block,"late held frontal guard blocks without rearm");d.facing.rotation=Quaternion.identity;Check(Contact(a,d)==CombatOutcome.Hit,"rear bypasses guard");
 UnityEngine.Object.DestroyImmediate(a.gameObject);UnityEngine.Object.DestroyImmediate(d.gameObject);
 }
 var volumeA=Actor("volume attacker",new Vector3(-10,0,0));var volumeD=Actor("volume defender",new Vector3(-10,0,1.3f));volumeA.faction=1;volumeD.faction=0;volumeD.facing.rotation=Quaternion.Euler(0,180,0);volumeD.gameObject.AddComponent<CapsuleCollider>();var defenderPresentation=volumeD.gameObject.AddComponent<CombatPresentation>();
 volumeA.Runner.TryStart(CombatRequest.Light,Vector3.forward);Advance(volumeA.Runner,Mathf.Max(0,Mathf.CeilToInt(volumeA.light.Windup/.02f-1e-5f)*.02f-.02f));volumeD.Runner.SetGuard(true);Physics.SyncTransforms();var world=UnityEngine.Object.FindAnyObjectByType<CombatWorld>();world.Step(.02f);Check(volumeA.Runner.Action==CombatAction.Recoil && volumeA.Posture==25 && volumeD.Health.Health==100,"real volume deflect once despite multiple colliders");Check(defenderPresentation.StatusText=="DEFLECT","DEFLECT label belongs to defender");
 for(int i=0;i<10;i++)world.Step(.02f);Check(volumeD.Health.Health==100 && volumeA.Posture==25,"no continued attack samples or duplicate posture after deflection");UnityEngine.Object.DestroyImmediate(volumeA.gameObject);UnityEngine.Object.DestroyImmediate(volumeD.gameObject);
 // Deflection adds zero defender posture, even just below the break threshold.
 // An actor actually at maximum is already stunned and cannot legally guard.
 var maxA=Actor("max attacker",new Vector3(10,0,0));var maxD=Actor("max defender",new Vector3(10,0,1));maxD.facing.rotation=Quaternion.Euler(0,180,0);maxD.AddPosture(99,0);maxD.Runner.SetGuard(true);CombatResult maxResult=default;maxD.Result+=r=>maxResult=r;Contact(maxA,maxD);Check(maxResult.outcome==CombatOutcome.Deflect && maxD.Posture==99 && !maxD.Runner.Broken,"near-full-posture successful defender takes zero posture and is not broken");
 UnityEngine.Object.DestroyImmediate(maxA.gameObject);UnityEngine.Object.DestroyImmediate(maxD.gameObject);
 var player=MakePlayer();var actor=player.combat.Actor;var runner=actor.Runner;
 Physics.SyncTransforms();Debug.Log($"Ground fixture bounds {player.playerCollider.bounds} position {player.transform.position} floor {floor.transform.position}");Check(player.IsGrounded(),"synthetic player on real floor");
 // Real Input System device events and SendMessages, not direct attack requests.
 InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
 var source=player.gameObject.AddComponent<UnityEngine.InputSystem.PlayerInput>();source.notificationBehavior=PlayerNotifications.SendMessages;source.actions=inputScope.CloneActions(AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Input/PlayerInputAction.inputactions"));source.defaultActionMap="Movement";
 var keyboard=inputScope.AddDevice<Keyboard>();var mouse=inputScope.AddDevice<Mouse>();source.SwitchCurrentControlScheme(keyboard,mouse);source.ActivateInput();source.SwitchCurrentActionMap("Movement");
 foreach(int fps in new[]{30,60,144}){
 foreach(bool mouseFirst in new[]{true,false}){
 InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.QueueStateEvent(mouse,new MouseState());InputUpdate();player.inputComponent.ClearCombat();Advance(runner,3);
 // Establish running and finish the deliberate Shift dodge before first click.
 InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W,Key.LeftShift));InputUpdate();runner.Collect();Advance(runner,.34f);runner.Collect();
 Check(player.input.runPressed && player.input.movement.y>0,"actual run input established "+fps);
 InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Left));InputUpdate();
 float accumulator=0;for(int frame=0;frame<Mathf.CeilToInt(fps*.12f);frame++){
 if(mouseFirst)player.SendMessage("LateUpdate");accumulator+=1f/fps;
 while(accumulator+.000001f>=.02f){runner.Collect();runner.Tick(.02f);accumulator-=.02f;}
 if(!mouseFirst)player.SendMessage("LateUpdate");InputUpdate();
 }
 Check(runner.Action==CombatAction.Attack && runner.Attack==actor.heavy,"first running LMB accepted as heavy "+fps+" late-before-fixed "+mouseFirst);
 for(int i=0;i<5;i++)runner.Collect();Check(player.inputComponent.ConsumeCombat()==CombatRequest.None,"held LMB never repeats");Advance(runner,actor.heavy.Duration);player.rb.linearVelocity=Vector3.zero;
 }
 }
 // Reproduce callback-order classification: attack event arrives before movement update in the same batch.
 InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.QueueStateEvent(mouse,new MouseState());InputUpdate();player.inputComponent.ClearCombat();
 bool callbackSawRun=false;source.actions.FindAction("Attack").performed+=_=>callbackSawRun=player.input.runPressed && player.input.movement.sqrMagnitude>0;
 InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Left));InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W,Key.LeftShift));InputUpdate();Check(!callbackSawRun,"reproduced old callback snapshot would select Light before Run/Move");
 Check(player.inputComponent.ConsumeCombat()==CombatRequest.Heavy,"attack selection waits for final input snapshot rather than callback-order Light");player.inputComponent.ClearCombat();
 player.inputComponent.ClearCombat();runner.SetGuard(false);runner.TryStart(CombatRequest.Dodge,Vector3.forward);Advance(runner,.24f);player.inputComponent.QueueCombat(CombatRequest.Heavy);runner.Collect();Check(runner.Action==CombatAction.Dodge,"tail buffer never cancels dodge");Advance(runner,.1f);runner.Collect();Check(runner.Action==CombatAction.Attack && runner.Attack==actor.heavy,"single buffered heavy starts at dodge boundary");Advance(runner,actor.heavy.Duration);player.rb.linearVelocity=Vector3.zero;
 runner.TryStart(CombatRequest.Dodge,Vector3.forward);Advance(runner,.24f);player.inputComponent.QueueCombat(CombatRequest.Heavy);runner.Collect();player.combat.SendMessage("OnApplicationFocus",false);Advance(runner,.1f);runner.Collect();Check(runner.Action==CombatAction.Free,"focus loss clears transient attack buffer");
 Advance(runner,.7f);runner.TryStart(CombatRequest.Dodge,Vector3.forward);Advance(runner,.24f);player.inputComponent.QueueCombat(CombatRequest.Heavy);runner.Collect();typeof(PlayerCombatAdapter).GetField("bufferUntil",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(player.combat,Time.realtimeSinceStartupAsDouble-.001);Advance(runner,.1f);runner.Collect();Check(runner.Action==CombatAction.Free,"expired 120 ms buffer cannot start a later attack");
 // Heading lock covers gameplay-only, visual-only and both sides of a visual transition.
 var anim=player.animator;runner.TryStart(CombatRequest.Light,Vector3.forward);Quaternion heading=player.model.rotation;player.RotateModel(Vector3.right);Check(Quaternion.Angle(heading,player.model.rotation)<.01f,"no windup turning");Advance(runner,actor.light.Duration);anim.Play("Attack 1",0,0);anim.Update(0);player.RotateModel(Vector3.right);Check(player.AttackFacingLocked && Quaternion.Angle(heading,player.model.rotation)<.01f,"visual-only attack keeps heading after gameplay recovery");anim.CrossFadeInFixedTime("GroundMovement",.2f);anim.Update(.05f);Check(player.AttackFacingLocked,"outgoing attack transition remains locked");anim.Update(.3f);player.EnforceAttackFacing();Check(!player.AttackFacingLocked,"heading releases after visual exit");anim.CrossFadeInFixedTime("Heavy Attack",.2f);anim.Update(.02f);Check(player.AttackFacingLocked,"incoming attack transition locked");anim.enabled=false;runner.TryStart(CombatRequest.Light,Vector3.forward);Check(player.AttackFacingLocked,"animation-free commitment heading lock");Advance(runner,actor.light.Duration);Check(!player.AttackFacingLocked,"animation-free facing resumes on completion");anim.enabled=true;anim.Play("GroundMovement");anim.Update(0);
 // Exactly two seconds of shared break, with rejected actions and movement each tick.
 foreach(bool isPlayer in new[]{true,false}){
 Combatant stunned=actor;enemy enemyBody=null;
 if(!isPlayer){var g=Go("stunned enemy");g.AddComponent<NavMeshAgent>();enemyBody=g.AddComponent<enemy>();stunned=g.GetComponent<Combatant>();stunned.light=Def("EnemyLight");stunned.heavy=Def("EnemyHeavy");}
 var r=stunned.Runner;r.SetGuard(false);r.TryStart(CombatRequest.Light,Vector3.forward);stunned.AddPosture(100,r.Clock);r.Interrupt(stunned.tuning.breakDuration,CombatAction.Stagger,true);
 Check(stunned.tuning.breakDuration==2 && !r.SamplesActive && !r.Guarding,"shared break duration 2 and action samples cleared "+isPlayer);
 for(int i=0;i<99;i++){r.SetGuard(true);Check(!r.SamplesActive && r.TryStart(CombatRequest.Heavy,Vector3.forward)==RequestResult.Busy && r.TryStart(CombatRequest.Dodge,Vector3.back)==RequestResult.Busy && !r.Guarding,"stun rejects attacks guard dodge tick "+i);r.Move();var rb=stunned.GetComponent<Rigidbody>();Check(Mathf.Abs(rb.linearVelocity.x)+Mathf.Abs(rb.linearVelocity.z)<.001f,"stun stops voluntary planar movement");if(i==20){float elapsed=r.Elapsed;r.Interrupt(.2f,CombatAction.Stagger);Check(r.Elapsed==elapsed,"ordinary hit cannot restart break");}r.Tick(.02f);}
 Check(r.Broken && r.Remaining>.01f,"break lasts through 1.98 seconds");r.Tick(.02f);Check(!r.Broken && r.Action==CombatAction.Free && stunned.Posture<=40,"2-second boundary resets posture and resumes");
 }
 // Normal brain gets a real neutral interval after commitment, allowing defense.
 var brain=new ScriptedCombatBrain();brain.Reset();var o=new CombatObservation{time=0,visible=true,grounded=true,heavyReady=true,dodgeReady=true,distance=1.9f};Check(brain.Decide(o).request==CombatRequest.Light,"normal initial light");o.selfAction=CombatAction.Attack;o.time=.9;brain.Decide(o);o.selfAction=CombatAction.Free;o.time=1.1;var circle=brain.Decide(o);Check(circle.request==CombatRequest.None && Mathf.Abs(circle.localMove.x)>0,"circling begins after recovery");o.time=1.65;Check(brain.Decide(o).request==CombatRequest.None,"circle interval not consumed during attack");o.opponentAction=CombatAction.Attack;o.opponentPhase=AttackPhase.Windup;o.opponentAttackId=1;o.opponentElapsed=.16f;o.opponentWindup=.2f;o.opponentReach=2;var defense=brain.Decide(o);Check(defense.guardHeld,"delayed public windup can preempt neutral orbit with guard");
 var guardActor=Actor("normal defense execution",new Vector3(10,0,0));guardActor.Runner.Submit(defense,Vector3.forward,true);Check(guardActor.Runner.Guarding && guardActor.Runner.Deflecting,"normal defense intent reaches runner and opens legal deflection window");
 // Integrated Normal host with delayed public cues, real NavMesh clearance and motor execution.
 var mesh=NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByIndex(0),new List<NavMeshBuildSource>{new NavMeshBuildSource{shape=NavMeshBuildSourceShape.Box,size=new Vector3(40,1,40),transform=Matrix4x4.TRS(new Vector3(0,-.5f,0),Quaternion.identity,Vector3.one),area=0}},new Bounds(Vector3.zero,new Vector3(50,10,50)),Vector3.zero,Quaternion.identity);var meshInstance=NavMesh.AddNavMeshData(mesh);
 var hostTarget=Actor("public telegraph target",new Vector3(-5,1,1.9f));var enemyGo=Go("Normal host end-to-end");enemyGo.transform.position=new Vector3(-5,1,0);var agent=enemyGo.AddComponent<NavMeshAgent>();agent.Warp(enemyGo.transform.position);var perception=enemyGo.AddComponent<EnemyPerceptionNav>();perception.target=hostTarget.transform;perception.obstacleLayers=1<<6;var e=enemyGo.AddComponent<enemy>();e.enemyCollider.height=2;e.enemyCollider.radius=.5f;e.SendMessage("Start");var ec=enemyGo.GetComponent<Combatant>();ec.light=Def("EnemyLight");ec.heavy=Def("EnemyHeavy");ec.faction=1;var er=ec.Runner;var host=enemyGo.GetComponent<EnemyBrainHost>();var motor=enemyGo.GetComponent<EnemyMotor>();motor.Teleport(new Vector3(-5,1,0),Quaternion.identity);Physics.SyncTransforms();e.grounded=e.CheckGround();
 Check(host.ActiveMode==EnemyControlMode.Normal && !host.DebugOwned,"Normal host active without debug lease");
 for(int i=0;i<200;i++){er.Collect();er.Tick(.02f);er.Move();if(er.Action==CombatAction.Free && Mathf.Abs(host.Intent.localMove.x)>0 && Mathf.Abs(e.rb.linearVelocity.x)>0)break;}
 Debug.Log($"NORMAL_TRACE {er.Action}/{er.Phase} grounded {e.grounded} pos {enemyGo.transform.position} agent {agent.isOnNavMesh} {host.DecisionDiagnostic} motor {motor.MovementDiagnostic} vel {e.rb.linearVelocity}");
 Check(er.Action==CombatAction.Free && Mathf.Abs(host.Intent.localMove.x)>0 && Mathf.Abs(e.rb.linearVelocity.x)>0,"post-recovery orbit reaches actual Normal motor");
 hostTarget.Runner.TryStart(CombatRequest.Heavy,Vector3.back);for(int i=0;i<Mathf.CeilToInt(hostTarget.heavy.Windup/.02f)+1;i++){er.Collect();er.Tick(.02f);er.Move();hostTarget.Runner.Tick(.02f);if(er.Guarding && er.Deflecting)break;}
 Check(er.Guarding && er.Deflecting,"Normal delayed telegraph schedules executable fresh guard");var incoming=CombatResolver.Evaluate(hostTarget,ec,hostTarget.heavy,hostTarget.transform.position);CombatResolver.ApplyBatch(new List<ResolvedContact>{incoming},er.Clock);Check(incoming.outcome==CombatOutcome.Deflect && hostTarget.Runner.Action==CombatAction.Recoil,"Normal host can deflect a telegraphed contact");
 Advance(hostTarget.Runner,.3f);host.SetMode(EnemyControlMode.Normal);ec.AddPosture(80-ec.Posture,er.Clock);hostTarget.Runner.SetGuard(false);hostTarget.Runner.TryStart(CombatRequest.Heavy,Vector3.back);bool dodged=false;
 for(int i=0;i<40;i++){er.Collect();er.Tick(.02f);er.Move();hostTarget.Runner.Tick(.02f);if(er.Action==CombatAction.Dodge){dodged=true;Check(e.rb.linearVelocity.z<0,"posture-aware backward dodge reaches motor on clear NavMesh");break;}}
 Check(!dodged && er.Guarding && host.Intent.localMove==Vector2.zero,"Normal high-posture delayed threat guards without early escape");meshInstance.Remove();UnityEngine.Object.DestroyImmediate(mesh);UnityEngine.Object.DestroyImmediate(enemyGo);UnityEngine.Object.DestroyImmediate(hostTarget.gameObject);
 // Attacker's visual must actually leave attack, and role labels must differ.
 var reactionGo=Go("reaction enemy");reactionGo.AddComponent<Rigidbody>();reactionGo.AddComponent<CapsuleCollider>();var reactionActor=reactionGo.AddComponent<Combatant>();reactionActor.light=Def("EnemyLight");reactionActor.heavy=Def("EnemyHeavy");reactionGo.AddComponent<EnemyBrainHost>();var animator=reactionGo.AddComponent<Animator>();animator.runtimeAnimatorController=AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/RepairValidation.controller");animator.Update(0);var presentation=reactionGo.AddComponent<CombatPresentation>();
 animator.Play("Attack 1");animator.Update(.02f);reactionActor.Runner.Interrupt(.3f,CombatAction.Recoil);animator.Update(.1f);Check(!animator.GetCurrentAnimatorStateInfo(0).IsName("Attack 1") && presentation.StatusText.StartsWith("RECOIL"),"reaction interrupts current attack pose with recoil countdown");reactionActor.Runner.Interrupt(2,CombatAction.Stagger,true);Check(presentation.StatusText.StartsWith("POSTURE BREAK"),"break diagnostic distinct from ordinary recoil");
 }
}

