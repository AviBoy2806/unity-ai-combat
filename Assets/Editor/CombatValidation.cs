using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using GameCombat;

[InitializeOnLoad]
public static class CombatValidation {
 static bool started,failed;static int stage;static double until;static Vector3 position;static Player freshPlayer;static enemy freshEnemy;
 static CombatValidation(){if(SessionState.GetBool("CombatValidation",false))Hook();}
 static void Hook(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;}
 public static void Run(){EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");SessionState.SetBool("CombatValidation",true);Hook();EditorApplication.EnterPlaymode();}
 static void Log(string text,string stack,LogType type){if(type==LogType.Exception || type==LogType.Error || type==LogType.Assert)failed=true;}
 static void Assert(bool value,string name){if(!value)throw new Exception("FAIL: "+name);Debug.Log("PASS: "+name);}
 static Combatant Actor(string name,Vector3 pos,int faction){var go=new GameObject(name);go.transform.position=pos;go.AddComponent<CapsuleCollider>().center=Vector3.up;var a=go.AddComponent<Combatant>();a.faction=faction;var hurt=go.AddComponent<Hurtbox>();hurt.owner=a;a.light=AssetDatabase.LoadAssetAtPath<AttackDefinition>("Assets/CombatData/Light1.asset");a.heavy=AssetDatabase.LoadAssetAtPath<AttackDefinition>("Assets/CombatData/RunningHeavy.asset");return a;}
 static void Advance(CombatActionRunner r,float seconds){for(int i=0;i<Mathf.RoundToInt(seconds/.02f);i++)r.Tick(.02f);}
 static CombatOutcome Contact(Combatant a,Combatant d){var list=new List<ResolvedContact>{CombatResolver.Evaluate(a,d,a.light,a.transform.position)};var outcome=list[0].outcome;CombatResolver.ApplyBatch(list,d.Runner.Clock);return outcome;}
 static void Rules(){
 var a=Actor("Test attacker",new Vector3(10000,0,0),0);var d=Actor("Test defender",new Vector3(10000,0,1),1);d.facing.rotation=Quaternion.Euler(0,180,0);
 Assert(a.Runner.TryStart(CombatRequest.Light,Vector3.forward)==RequestResult.Accepted,"attack accepted without Animator");
 Assert(a.Runner.TryStart(CombatRequest.Dodge,Vector3.forward)==RequestResult.Busy,"commitment rejects dodge");
 Assert(a.Runner.MarkHit(d) && !a.Runner.MarkHit(d),"per-attack duplicate suppression");
 d.Runner.SetGuard(true);Assert(Contact(a,d)==CombatOutcome.Deflect && d.Health.Health==100 && a.Posture==25,"fresh directional deflection and attacker posture");
 Advance(d.Runner,.18f);Assert(!d.Runner.Deflecting,"deflection closing boundary");d.Runner.SetGuard(true);Assert(!d.Runner.Deflecting,"held guard never rearms");
 Assert(Contact(a,d)==CombatOutcome.Block && d.Health.Health==100 && d.Posture==20,"block health and posture");
 d.facing.rotation=Quaternion.identity;Assert(Contact(a,d)==CombatOutcome.Hit && d.Health.Health==90,"rear attack bypasses guard");
 Advance(d.Runner,.2f);d.facing.rotation=Quaternion.Euler(0,180,0);d.Runner.SetGuard(false);Advance(d.Runner,.1f);d.Runner.SetGuard(true);Assert(d.Runner.Deflecting,"release and cooldown rearm");
 d.Runner.SetGuard(false);Assert(d.Runner.TryStart(CombatRequest.Dodge,Vector3.right)==RequestResult.Accepted,"dodge accepted");Advance(d.Runner,.04f);Assert(!d.Runner.Invulnerable,"before dodge iframe");Advance(d.Runner,.02f);Assert(d.Runner.Invulnerable && Contact(a,d)==CombatOutcome.Evade,"dodge opening boundary");Advance(d.Runner,.14f);Assert(!d.Runner.Invulnerable,"dodge closing boundary");Advance(d.Runner,.14f);Assert(d.Runner.TryStart(CombatRequest.Dodge,Vector3.right)==RequestResult.Cooldown,"dodge cooldown");
 d.AddPosture(100,d.Runner.Clock);Contact(a,d);Assert(d.Runner.Broken,"posture break forced");Advance(d.Runner,.5f);float elapsed=d.Runner.Elapsed;Contact(a,d);Assert(d.Runner.Elapsed==elapsed,"hits do not extend broken stagger");Advance(d.Runner,d.tuning.breakDuration-.5f);Assert(!d.Runner.Broken && d.Posture<=40,"break completion reset");
 float health=d.Health.Health;d.Health.takeDamage(float.NaN);d.Health.takeDamage(-1);Assert(d.Health.Health==health,"invalid damage rejected");d.Health.takeDamage(1000);Assert(d.Runner.Action==CombatAction.Dead,"death immediately dominates");d.Runner.Interrupt(1,CombatAction.Stagger,true);Assert(d.Runner.Action==CombatAction.Dead,"death cannot be interrupted");
 UnityEngine.Object.DestroyImmediate(a.gameObject);UnityEngine.Object.DestroyImmediate(d.gameObject);
 a=Actor("Combo",new Vector3(10000,0,0),0);a.Runner.TryStart(CombatRequest.Light,Vector3.forward);float comboOpens=Mathf.Max(a.light.Windup,a.light.Duration-a.light.comboInputSeconds);a.Runner.Tick(comboOpens-.02f);Assert(a.Runner.TryStart(CombatRequest.Light,Vector3.forward)==RequestResult.Busy,"early combo rejected");a.Runner.Tick(.02f);Assert(a.Runner.TryStart(CombatRequest.Light,Vector3.forward)==RequestResult.Accepted,"combo window opening from current saved frames");a.Runner.Tick(a.Runner.Remaining-.001f);Assert(a.Runner.Attack==a.light,"queued combo waits for full recovery");a.Runner.Tick(.001f);Assert(a.Runner.Attack==a.light.nextCombo,"combo starts only after full saved recovery");UnityEngine.Object.DestroyImmediate(a.gameObject);
 a=Actor("Volumes",new Vector3(10000,0,0),0);d=Actor("Hurtboxes",new Vector3(10000,0,1.3f),1);d.gameObject.AddComponent<BoxCollider>();var world=UnityEngine.Object.FindFirstObjectByType<CombatWorld>();
 a.Runner.TryStart(CombatRequest.Light,Vector3.forward);for(int i=0;i<Mathf.CeilToInt(a.light.Duration/.02f);i++)world.Step(.02f);Assert(d.Health.Health==90,"authoritative volume hits once across active ticks/multiple colliders");
 UnityEngine.Object.DestroyImmediate(a.gameObject);UnityEngine.Object.DestroyImmediate(d.gameObject);
 a=Actor("Faction",new Vector3(10000,0,0),0);d=Actor("Ally",new Vector3(10000,0,1.3f),0);a.Runner.TryStart(CombatRequest.Light,Vector3.forward);for(int i=0;i<Mathf.CeilToInt(a.light.Duration/.02f);i++)world.Step(.02f);Assert(d.Health.Health==100,"faction filter");UnityEngine.Object.DestroyImmediate(a.gameObject);UnityEngine.Object.DestroyImmediate(d.gameObject);
 a=Actor("Obstruction",new Vector3(10000,0,0),0);d=Actor("Behind wall",new Vector3(10000,0,1.3f),1);var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.layer=6;wall.transform.position=new Vector3(10000,1,.65f);wall.transform.localScale=new Vector3(4,3,.2f);a.Runner.TryStart(CombatRequest.Light,Vector3.forward);for(int i=0;i<Mathf.CeilToInt(a.light.Duration/.02f);i++)world.Step(.02f);Assert(d.Health.Health==100,"wall obstruction");UnityEngine.Object.DestroyImmediate(wall);UnityEngine.Object.DestroyImmediate(a.gameObject);UnityEngine.Object.DestroyImmediate(d.gameObject);

 a=Actor("Trade A",new Vector3(10000,0,0),0);d=Actor("Trade B",new Vector3(10000,0,1),1);
 a.Health.takeDamage(95);d.Health.takeDamage(95);
 var trade=new List<ResolvedContact>{CombatResolver.Evaluate(a,d,a.light,a.transform.position),CombatResolver.Evaluate(d,a,d.light,d.transform.position)};
 CombatResolver.ApplyBatch(trade,0);Assert(a.Dead && d.Dead && a.Runner.Action==CombatAction.Dead && d.Runner.Action==CombatAction.Dead,"simultaneous lethal trade and death precedence");
 UnityEngine.Object.DestroyImmediate(a.gameObject);UnityEngine.Object.DestroyImmediate(d.gameObject);
 a=Actor("Armor attacker",new Vector3(10000,0,0),0);d=Actor("Armor defender",new Vector3(10000,0,1),1);
 d.Runner.TryStart(CombatRequest.Heavy,Vector3.back);d.Runner.Tick(d.heavy.Windup+.02f);Contact(a,d);Assert(d.Runner.Action==CombatAction.Attack,"heavy active resists normal hit stagger");d.Runner.Tick(d.heavy.Active);Contact(a,d);Assert(d.Runner.Action==CombatAction.Stagger,"heavy recovery is punishable");
 UnityEngine.Object.DestroyImmediate(a.gameObject);UnityEngine.Object.DestroyImmediate(d.gameObject);
 a=Actor("Guard banking",new Vector3(10000,0,0),0);a.Runner.TryStart(CombatRequest.Light,Vector3.forward);a.Runner.SetGuard(true);a.Runner.Tick(a.light.Duration);Assert(a.Runner.Guarding && !a.Runner.Deflecting,"guard pressed during commitment cannot bank deflection");
 a.Runner.SetGuard(false);a.AddPosture(50,a.Runner.Clock);Advance(a.Runner,1.4f);Assert(a.Posture==50,"posture recovery delay");Advance(a.Runner,.2f);Assert(a.Posture<50,"posture recovery resumes");UnityEngine.Object.DestroyImmediate(a.gameObject);
 foreach(int fps in new[]{30,60,144}){a=Actor("Rate"+fps,new Vector3(10000,0,0),0);a.Runner.TryStart(CombatRequest.Light,Vector3.forward);double accumulator=0;int ticks=0;for(int frame=0;frame<fps;frame++){accumulator+=1.0/fps;while(accumulator+1e-9>=.02){a.Runner.Tick(.02f);accumulator-=.02;ticks++;}}Assert(ticks==50 && a.Runner.Action==CombatAction.Free,"50 fixed ticks independent of render schedule "+fps);UnityEngine.Object.DestroyImmediate(a.gameObject);}
 Debug.Log("COMBAT_RULES_PASS");
 }
 static void Tick(){if(!EditorApplication.isPlaying || EditorApplication.timeSinceStartup<until)return;try{
 var p=UnityEngine.Object.FindFirstObjectByType<Player>();
 if(!started){started=true;Rules();until=EditorApplication.timeSinceStartup+2;return;}
 switch(stage++){
 case 0: position=p.transform.position;p.input.movement=Vector2.up;until=EditorApplication.timeSinceStartup+1;break;
 case 1: Assert(Vector3.Distance(position,p.transform.position)>.2f,"scene locomotion");p.input.movement=Vector2.zero;p.inputComponent.QueueCombat(CombatRequest.Light);until=EditorApplication.timeSinceStartup+1;break;
 case 2: Assert(p.combat.Actor.Runner.Action==CombatAction.Free,"scene light ends by timer");Assert(new Vector2(p.rb.linearVelocity.x,p.rb.linearVelocity.z).magnitude<.05f,"no horizontal drift after light recovery");
 using(var inputScope = new ValidationInputScope(p.GetComponent<UnityEngine.InputSystem.PlayerInput>())){
 UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior=UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
 UnityEngine.InputSystem.InputSystem.settings.editorInputBehaviorInPlayMode=UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
 var mouse=inputScope.AddDevice<UnityEngine.InputSystem.Mouse>();
 p.GetComponent<UnityEngine.InputSystem.PlayerInput>().SwitchCurrentControlScheme(mouse);
 UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse,new UnityEngine.InputSystem.LowLevel.MouseState().WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Right));
 typeof(UnityEngine.InputSystem.InputSystem).GetMethod("Update",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic,null,new[]{typeof(UnityEngine.InputSystem.LowLevel.InputUpdateType)},null).Invoke(null,new object[]{UnityEngine.InputSystem.LowLevel.InputUpdateType.Dynamic});
 Assert(p.inputComponent.ConsumeGuard(),"actual mouse press reaches SendMessages guard input");
 UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse,new UnityEngine.InputSystem.LowLevel.MouseState());
 typeof(UnityEngine.InputSystem.InputSystem).GetMethod("Update",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic,null,new[]{typeof(UnityEngine.InputSystem.LowLevel.InputUpdateType)},null).Invoke(null,new object[]{UnityEngine.InputSystem.LowLevel.InputUpdateType.Dynamic});
 Assert(!p.inputComponent.ConsumeGuard(),"actual mouse release reaches SendMessages guard input");
 }
p.inputComponent.SetBlock(true);until=EditorApplication.timeSinceStartup+.5;break;
 case 3: Assert(p.combat.Actor.Runner.Guarding && !p.combat.Actor.Runner.Deflecting,"RMB adapter hold without repeat window");p.inputComponent.SetBlock(false);until=EditorApplication.timeSinceStartup+1;break;
 case 4: Assert(!p.combat.Actor.Runner.Guarding && p.stateMachine.currentState.GetStateType()!=PlayerState.PlayerStateType.guard,"guard release exits defense state without dodge");p.inputComponent.QueueCombat(CombatRequest.Dodge);until=EditorApplication.timeSinceStartup+1;break;
 case 5: Assert(p.combat.Actor.Runner.Action==CombatAction.Free,"scene dodge ends");p.inputComponent.QueueCombat(CombatRequest.Heavy);until=EditorApplication.timeSinceStartup+3;break;
 case 6: Assert(p.combat.Actor.Runner.Action==CombatAction.Free,"scene heavy ends");Assert(new Vector2(p.rb.linearVelocity.x,p.rb.linearVelocity.z).magnitude<.05f,"no horizontal drift after heavy recovery");p.animator.enabled=false;p.inputComponent.QueueCombat(CombatRequest.Light);until=EditorApplication.timeSinceStartup+1;break;
 case 7: Assert(p.combat.Actor.Runner.Action==CombatAction.Free,"disabled Animator action completion");
 freshPlayer=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab"),p.transform.position+Vector3.right*3,Quaternion.identity).GetComponent<Player>();
 freshEnemy=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Enemy/Enemy.prefab"),p.transform.position+Vector3.forward*3,Quaternion.identity).GetComponent<enemy>();
 until=EditorApplication.timeSinceStartup+1;break;
 case 8: freshPlayer.inputComponent.QueueCombat(CombatRequest.Light);until=EditorApplication.timeSinceStartup+1;break;
 default: Assert(freshPlayer.combat.Actor.Runner.Action==CombatAction.Free && freshEnemy.GetComponent<Combatant>() && freshEnemy.stateMachine!=null,"fresh prefab runtime initialization and player attack completion");
 UnityEngine.Object.DestroyImmediate(freshPlayer.gameObject);UnityEngine.Object.DestroyImmediate(freshEnemy.gameObject);PlayerRefManager.Instance.SetPlayer(p);Debug.Log(failed?"COMBAT_VALIDATION_FAIL":"COMBAT_VALIDATION_PASS");Finish();break;
 }
 }catch(Exception e){failed=true;Debug.LogException(e);Finish();}}
 static void Finish(){SessionState.SetBool("CombatValidation",false);EditorApplication.Exit(failed?1:0);}
}


