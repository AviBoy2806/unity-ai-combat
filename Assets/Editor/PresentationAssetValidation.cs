using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using GameCombat;

[InitializeOnLoad]
public static class PresentationAssetValidation {
 static bool done;static int checks,frames,lastFrame=-1;static double deadline;
 static PresentationAssetValidation(){if(SessionState.GetBool("Presentation18",false)){deadline=EditorApplication.timeSinceStartup+60;EditorApplication.update+=Tick;}}
 public static void Run(){SessionState.SetBool("Presentation18",true);EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");deadline=EditorApplication.timeSinceStartup+60;EditorApplication.update+=Tick;EditorApplication.EnterPlaymode();}
 public static void Preview(){SessionState.SetBool("PresentationPreview18",true);Run();}
 static void Check(bool value,string message){if(!value)throw new Exception(message);checks++;Debug.Log("PRESENTATION_CHECK "+message);}
 static void Tick(){if(done)return;try{if(EditorApplication.timeSinceStartup>deadline)throw new Exception("60-second deadline");if(!EditorApplication.isPlaying)return;
  if(SessionState.GetBool("PresentationPreview18",false)){PreviewTick();return;}
  done=true;Rules();Debug.Log("PRESENTATION_VALIDATION_PASS checks="+checks);Finish(0);
 }catch(Exception e){Debug.LogException(e);Finish(1);}}
 static void Rules(){var world=UnityEngine.Object.FindFirstObjectByType<CombatWorld>();world.enabled=false;
  var player=UnityEngine.Object.FindFirstObjectByType<Player>();player.GetComponent<UnityEngine.InputSystem.PlayerInput>().enabled=false;Check(player.GetComponent<CombatHud>(),"HUD installed on gameplay player");
  foreach(var host in UnityEngine.Object.FindObjectsByType<EnemyBrainHost>(FindObjectsSortMode.None)){Check(host.mode==EnemyControlMode.Normal && !host.showCombatDiagnostics,"Normal default and raw diagnostic overlay off");host.enabled=false;}
  foreach(var actor in UnityEngine.Object.FindObjectsByType<Combatant>(FindObjectsSortMode.None)){
   var p=actor.GetComponent<CombatPresentation>();var anim=actor.GetComponentInChildren<Animator>();anim.cullingMode=AnimatorCullingMode.AlwaysAnimate;Check(!p.showDebugGeometry && !p.showTimingDebug && !p.showReactionTint,"technical geometry/tint default off for "+actor.name);
   Check(p.combatAudioSource && !p.combatAudioSource.playOnAwake && !p.combatAudioSource.loop && p.lightAttackSound && p.heavyAttackSound && p.hitSound && p.blockSound && p.deflectSound && p.deathSound,"real sound assignments for "+actor.name);
   if(actor.GetComponent<Player>()){var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.layer=7;floor.transform.position=new Vector3(actor.transform.position.x,actor.GetComponent<Collider>().bounds.min.y-.06f,actor.transform.position.z);floor.transform.localScale=new Vector3(10,.1f,10);Physics.SyncTransforms();Check(actor.GetComponent<Player>().IsGrounded(),"isolated player fixture grounded");}
   string home=actor.GetComponent<Player>()?"GroundMovement":"loco";if(actor.GetComponent<Player>()){anim.SetBool("IsGrounded",true);anim.SetFloat("VerticalDir",0);}
   void Step(int n){for(int i=0;i<n;i++){actor.Runner.Tick(.02f);p.SendMessage("Update");anim.Update(.02f);}}
   actor.Runner.SetGuard(false);Step(200);
   foreach(var action in new[]{CombatAction.Stagger,CombatAction.Recoil}){actor.Runner.Interrupt(.3f,action);Step(4);Check(anim.GetCurrentAnimatorStateInfo(0).IsName(action==CombatAction.Stagger?"HitReaction":"Recoil") || anim.IsInTransition(0),"reaction animation entered "+actor.name+" "+action);Step(80);Check(actor.Runner.Action==CombatAction.Free && (anim.GetCurrentAnimatorStateInfo(0).IsName(home) || home=="GroundMovement" && anim.GetCurrentAnimatorStateInfo(0).IsName("Locked On 0")),"reaction exits by shared state "+actor.name+" action="+actor.Runner.Action+" state="+anim.GetCurrentAnimatorStateInfo(0).fullPathHash+" clips="+string.Join(",",anim.GetCurrentAnimatorClipInfo(0).Select(c=>c.clip.name)));}
   if(actor.GetComponent<Player>()){actor.Runner.SetGuard(true);Step(100);Check(anim.GetCurrentAnimatorStateInfo(0).IsName("Guard"),"existing player block clip holds guard pose");actor.Runner.SetGuard(false);Step(20);Check(anim.GetCurrentAnimatorStateInfo(0).IsName(home),"guard release exits pose");}
   actor.AddPosture(100,actor.Runner.Clock);Step(4);Check(anim.GetCurrentAnimatorStateInfo(0).IsName(actor.GetComponent<Player>()?"HitReaction":"Stunned") || anim.IsInTransition(0),"full break uses supplied reaction "+actor.name);Step(200);Check(!actor.Runner.Broken,"full break releases without animation authority");
   int sounds=p.SoundPlayCount;actor.Runner.TryStart(CombatRequest.Light,actor.facing.forward);p.SendMessage("Changed");Check(p.SoundPlayCount==sounds+1,"real attack sound once "+actor.name);Step(200);
   foreach(var line in actor.GetComponentsInChildren<LineRenderer>())Check(!line.enabled,"debug line hidden "+line.name);
   if(!anim.isHuman){var clip=anim.runtimeAnimatorController.animationClips.First(c=>AssetDatabase.GetAssetPath(c).Contains("mutant impact"));var bindings=AnimationUtility.GetCurveBindings(clip);int match=bindings.Count(b=>b.type==typeof(Transform) && anim.transform.Find(b.path));Check(match>100,"Generic reaction matches actual Mutant hierarchy: "+match);}
  }
 }
 static void PreviewTick(){if(Time.frameCount==lastFrame)return;lastFrame=Time.frameCount;frames++;Time.captureDeltaTime=.02f;
  if(frames==60 || frames==130)ScreenCapture.CaptureScreenshot(Path.GetFullPath("Logs/Presentation20261118/"+(frames==60?"hud-normal.png":"hud-evaluation.png")));
  if(frames==90)UnityEngine.Object.FindFirstObjectByType<EnemyBrainHost>().SetMode(EnemyControlMode.ML);
  if(frames>160){Check(File.Exists("Logs/Presentation20261118/hud-normal.png") && File.Exists("Logs/Presentation20261118/hud-evaluation.png"),"rendered screenshots saved");Debug.Log("PRESENTATION_PREVIEW_PASS");Finish(0);}
 }
 static void Finish(int code){done=true;SessionState.SetBool("Presentation18",false);SessionState.SetBool("PresentationPreview18",false);if(Application.isBatchMode)EditorApplication.Exit(code);else EditorApplication.isPlaying=false;}
}
