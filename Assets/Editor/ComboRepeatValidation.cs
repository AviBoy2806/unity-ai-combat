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
public static class ComboRepeatValidation {
 static bool started;static int checks;static readonly List<GameObject> objects=new();
 static ComboRepeatValidation(){if(SessionState.GetBool("ComboRepeatValidation",false))EditorApplication.update+=Tick;}
 [MenuItem("Tools/Combat/Test repeated combos with saved assets")]
 public static void Run(){foreach(var guid in AssetDatabase.FindAssets("t:AttackDefinition",new[]{"Assets/CombatData"}))AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid),ImportAssetOptions.ForceUpdate);typeof(CombatFrameValidation).GetMethod("BuildGraph",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null);EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);SessionState.SetBool("ComboRepeatValidation",true);EditorApplication.update+=Tick;EditorApplication.EnterPlaymode();}
 static void Check(bool ok,string name){if(!ok)throw new Exception("FAIL: "+name);checks++;Debug.Log("PASS: "+name);}
 static GameObject Go(string name){var g=new GameObject(name);objects.Add(g);return g;}
 static AttackDefinition Def(string name)=>AssetDatabase.LoadAssetAtPath<AttackDefinition>("Assets/CombatData/"+name+".asset");
 static Combatant Actor(string name,Vector3 pos){var g=Go(name);g.transform.position=pos;var a=g.AddComponent<Combatant>();a.light=Def("Light1");a.heavy=Def("RunningHeavy");g.AddComponent<BoxCollider>().center=Vector3.up*.8f;g.AddComponent<Hurtbox>().owner=a;return a;}
 static void InputUpdate(){typeof(InputSystem).GetMethod("Update",BindingFlags.Static|BindingFlags.NonPublic,null,new[]{typeof(InputUpdateType)},null).Invoke(null,new object[]{InputUpdateType.Dynamic});}
 static void Tick(){if(!EditorApplication.isPlaying || started)return;started=true;SessionState.SetBool("ComboRepeatValidation",false);int code=0;try{Rules();Debug.Log($"COMBO_REPEAT_VALIDATION_PASS {checks} checks");}catch(Exception e){Debug.LogException(e);code=1;}finally{foreach(var g in objects)if(g)UnityEngine.Object.DestroyImmediate(g);EditorApplication.Exit(code);}}
 static void Rules(){
 using var inputScope = new ValidationInputScope();
 var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);objects.Add(floor);floor.layer=7;floor.transform.position=Vector3.down*.5f;floor.transform.localScale=new Vector3(50,1,50);
 foreach(var n in new[]{"Light1","Light2","Light3","RunningHeavy","EnemyLight"})Check(Def(n).Valid,"saved attack data is legal: "+n);
 Check(Def("Light1").Valid && Def("Light1").useClipFrames,"current saved Light1 profile loaded without preset overrides");Check(Def("Light3").Valid && Def("Light3").useClipFrames,"current saved Light3 profile loaded without preset overrides");
 var player=(Player)typeof(CombatRepairValidation).GetMethod("MakePlayer",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null);objects.Add(player.gameObject);player.animator.runtimeAnimatorController=AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/FramePlayer.controller");player.animator.SetBool("IsGrounded",true);player.animator.Update(0);var actor=player.combat.Actor;var presentation=player.gameObject.AddComponent<CombatPresentation>();var r=actor.Runner;
 var dummy=Actor("combo target",new Vector3(0,0,1.3f));dummy.faction=1;typeof(HealthComponent).GetField("health",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(dummy.Health,10000f);dummy.Health.maxHealth=10000;dummy.gameObject.AddComponent<CapsuleCollider>();Physics.SyncTransforms();
 var world=UnityEngine.Object.FindAnyObjectByType<CombatWorld>();InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
 var input=player.gameObject.AddComponent<UnityEngine.InputSystem.PlayerInput>();input.notificationBehavior=PlayerNotifications.SendMessages;input.actions=inputScope.CloneActions(AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Input/PlayerInputAction.inputactions"));input.defaultActionMap="Movement";var mouse=inputScope.AddDevice<Mouse>();var keyboard=inputScope.AddDevice<Keyboard>();input.SwitchCurrentControlScheme(keyboard,mouse);input.ActivateInput();input.SwitchCurrentActionMap("Movement");
 var damageByAttack=new Dictionary<ulong,float>();var visualStrikes=new HashSet<string>();float maxClockGap=0;int wrongStrike=0;ulong previousQueued=0;
 dummy.Result+=hit=>{if(hit.attackerId==actor.Id && hit.actualHealthDamage>0){if(!damageByAttack.ContainsKey(hit.attackInstanceId))damageByAttack[hit.attackInstanceId]=0;damageByAttack[hit.attackInstanceId]+=hit.actualHealthDamage;}};
 foreach(int fps in new[]{30,60,144}){
 for(int chain=0;chain<5;chain++){
 bool locked=chain%2==1;player.animator.SetBool("Is Locked On",locked);float dt=1f/fps,acc=0;bool mouseHeld=false;damageByAttack.Clear();visualStrikes.Clear();previousQueued=0;float before=dummy.Health.Health;ulong startId=r.AttackId;int frame=0;
 // No Animator.Play/reset between chains. They must restart through normal recovery.
 for(;frame<fps*10;frame++){
 bool press=frame==0 || r.ComboWindowOpen && r.Elapsed>=Mathf.Max(r.Attack.Windup,r.Attack.Duration-r.Attack.comboInputSeconds)+.05f*(chain%3) && !r.ComboQueued && r.AttackId!=previousQueued;
 if(press){InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Left));InputUpdate();mouseHeld=true;previousQueued=r.AttackId;}
 else if(mouseHeld){InputSystem.QueueStateEvent(mouse,new MouseState());InputUpdate();mouseHeld=false;}
 if(chain%2==0)player.SendMessage("LateUpdate");acc+=dt;
 while(acc+.000001f>=.02f){world.Step(.02f);player.SendMessage("FixedUpdate");acc-=.02f;}
 player.SendMessage("Update");presentation.SendMessage("Update");player.animator.Update(dt);if(chain%2!=0)player.SendMessage("LateUpdate");
 var visual=player.animator.GetCurrentAnimatorStateInfo(0);var next=player.animator.IsInTransition(0)?player.animator.GetNextAnimatorStateInfo(0):default;
 foreach(var state in new[]{visual,next})foreach(var name in new[]{"Attack 1","Attack 2","Attack 3"})if(state.IsName(name))visualStrikes.Add(name);
 if(r.Action==CombatAction.Attack && r.Phase==AttackPhase.Active){var state=next.IsName(r.Attack.animationState)?next:visual;if(!state.IsName(r.Attack.animationState))wrongStrike++;else maxClockGap=Mathf.Max(maxClockGap,Mathf.Abs(r.Elapsed-state.normalizedTime*r.Attack.clipDuration));}
 if(r.AttackId>=startId+3 && r.Action==CombatAction.Free && !player.AttackFacingLocked)break;
 }
 Check(frame<fps*10,"chain returns to idle and can restart: "+fps+" FPS chain "+chain);
 Check(r.AttackId==startId+3 && visualStrikes.Count==3,"one visual/gameplay strike per combo step: "+fps+" chain "+chain);
 Check(Mathf.Abs(before-dummy.Health.Health-40)<.001f && damageByAttack.Count==3,"all three strikes hit once for 10+10+20 with multiple hurtboxes: "+fps+" chain "+chain);
 foreach(var damage in damageByAttack.Values)Check(damage==10 || damage==20,"no doubled combo damage");
 presentation.SendMessage("Update");Check(!r.ComboQueued && !player.animator.GetBool("CombatComboHold") && !player.animator.GetBool("CombatLateCombo") && !player.animator.GetBool("CombatLateRecovery"),"combo gates clear after each chain");
 }
 }
 Check(wrongStrike==0,"damage never belongs to a different visual strike or idle");Check(maxClockGap<.09f,"clock gap bounded by render/fixed-step scheduling: "+maxClockGap);Check(player.animator.speed==1,"authored playback remains 1x");
 // Stale pending requests are discarded on interruption and death.
 r.TryStart(CombatRequest.Light,Vector3.forward);presentation.SendMessage("Update");player.animator.Update(.02f);r.Interrupt(.2f,CombatAction.Stagger);for(int i=0;i<30;i++){r.Tick(.02f);presentation.SendMessage("Update");player.animator.Update(.02f);}Check(r.Action==CombatAction.Free && !player.animator.GetBool("CombatLateCombo"),"interruption clears late-request ownership");
 // Holding one press must not queue or repeat a combo, and an abandoned chain must release normally.
 InputSystem.QueueStateEvent(mouse,new MouseState());InputUpdate();float heldBefore=dummy.Health.Health;ulong heldId=r.AttackId;InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Left));InputUpdate();
 for(int i=0;i<100;i++){world.Step(.02f);player.SendMessage("Update");presentation.SendMessage("Update");player.animator.Update(.02f);}Check(r.AttackId==heldId+1 && !r.ComboQueued && r.Action==CombatAction.Free && !player.AttackFacingLocked && heldBefore-dummy.Health.Health==10,"held LMB gives one hit, abandoned chain clears, no automatic repeats");InputSystem.QueueStateEvent(mouse,new MouseState());InputUpdate();
 UnityEngine.Object.DestroyImmediate(player.gameObject);UnityEngine.Object.DestroyImmediate(dummy.gameObject);
 // Strong blocking is preserved, but breaking it gives an actual stationary 3.5-second opening.
 var mesh=NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByIndex(0),new List<NavMeshBuildSource>{new NavMeshBuildSource{shape=NavMeshBuildSourceShape.Box,size=new Vector3(50,1,50),transform=Matrix4x4.TRS(Vector3.down*.5f,Quaternion.identity,Vector3.one)}},new Bounds(Vector3.zero,new Vector3(60,10,60)),Vector3.zero,Quaternion.identity);var mi=NavMesh.AddNavMeshData(mesh);
 var source=Actor("posture attacker",new Vector3(-5,1,1.3f));var g=Go("stunned enemy");g.transform.position=new Vector3(-5,1,0);g.AddComponent<NavMeshAgent>();var nav=g.AddComponent<EnemyPerceptionNav>();nav.target=source.transform;var enemy=g.AddComponent<enemy>();enemy.enemyCollider.height=2;enemy.enemyCollider.radius=.5f;enemy.SendMessage("Start");var e=g.GetComponent<Combatant>();e.light=Def("EnemyLight");e.heavy=Def("EnemyHeavy");e.postureBreakSeconds=3.5f;var motor=g.GetComponent<EnemyMotor>();motor.Teleport(new Vector3(-5,1,0),Quaternion.identity);Physics.SyncTransforms();enemy.grounded=enemy.CheckGround();var host=g.GetComponent<EnemyBrainHost>();
 e.Runner.SetGuard(true);for(int i=0;i<10;i++)e.Runner.Tick(.02f);
 for(int i=0;i<3;i++){var block=CombatResolver.Evaluate(source,e,source.heavy,source.transform.position);CombatResolver.ApplyBatch(new List<ResolvedContact>{block},e.Runner.Clock);Check(block.outcome==CombatOutcome.Block,"ordinary guard stays strong before posture breaks");}
 Check(e.Runner.Broken && e.Runner.Remaining==3.5f && e.Health.Health==100,"full enemy posture creates a 3.5-second opening without block health damage");float start=e.Runner.Elapsed;
 for(int i=0;i<174;i++){
 e.Runner.Collect();e.Runner.Tick(.02f);e.Runner.Move();
 if(i==15 || i==65 || i==120){var hit=CombatResolver.Evaluate(source,e,source.light,source.transform.position);CombatResolver.ApplyBatch(new List<ResolvedContact>{hit},e.Runner.Clock);}
 if(i%20==0)Check(e.Runner.Broken && !e.Runner.Guarding && e.Runner.TryStart(CombatRequest.Dodge,Vector3.back)==RequestResult.Busy && enemy.horizontalVelocity.sqrMagnitude<.0001f,"no guard, escape or retreat during full opening");
 }
 Check(e.Health.Health==70 && !host.EscapePending,"three health hits land without banking a post-break escape");Check(e.Runner.Broken && e.Runner.Remaining>.01f,"ordinary hits did not restart or shorten 3.5-second stun");e.Runner.Tick(.02f);Check(!e.Runner.Broken && e.Posture<=40,"posture resets only after full opening");Check(source.BreakDuration==2,"player/shared default break remains 2 seconds");e.Runner.Interrupt(3.5f,CombatAction.Stagger,true);e.Health.takeDamage(1000);Check(e.Runner.Action==CombatAction.Dead && !e.Runner.Broken && !e.Runner.Guarding,"death clears full-stun state and wins over recovery");mi.Remove();UnityEngine.Object.DestroyImmediate(mesh);
 }
}
