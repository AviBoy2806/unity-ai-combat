using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using GameCombat;

public static class PresentationAssetSetup {
 const string Folder="Assets/Missing Clips and Sounds/";
 static readonly List<string> report=new();
 static AnimationClip Clip(string path)=>AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview__"));
 static void Bool(AnimatorController c,string name){if(!c.parameters.Any(p=>p.name==name))c.AddParameter(name,AnimatorControllerParameterType.Bool);}
 static AnimatorState State(AnimatorController c,string name,Motion clip){var sm=c.layers[0].stateMachine;var s=sm.states.FirstOrDefault(s=>s.state.name==name).state;if(!s)s=sm.AddState(name);s.motion=clip;s.speed=1;return s;}
 static void Reaction(AnimatorController c,string name,AnimationClip clip,string parameter,string home,bool enemy){Bool(c,parameter);var sm=c.layers[0].stateMachine;var s=State(c,name,clip);if(sm.anyStateTransitions.Any(t=>t.destinationState==s))return;var entry=sm.AddAnyStateTransition(s);entry.hasExitTime=false;entry.duration=.06f;entry.hasFixedDuration=true;entry.canTransitionToSelf=false;entry.AddCondition(AnimatorConditionMode.If,0,parameter);if(enemy)entry.AddCondition(AnimatorConditionMode.IfNot,0,"Stunned");
  var exit=s.AddTransition(sm.states.First(t=>t.state.name==home).state);exit.hasExitTime=false;exit.duration=.06f;exit.hasFixedDuration=true;exit.AddCondition(AnimatorConditionMode.IfNot,0,parameter);
  if(enemy){var full=s.AddTransition(sm.states.First(t=>t.state.name=="Stunned").state);full.hasExitTime=false;full.duration=.04f;full.AddCondition(AnimatorConditionMode.If,0,"Stunned");}
 }
 static void ImportClip(string path,bool human){var importer=(ModelImporter)AssetImporter.GetAtPath(path);importer.animationType=human?ModelImporterAnimationType.Human:ModelImporterAnimationType.Generic;var clips=importer.clipAnimations.Length>0?importer.clipAnimations:importer.defaultClipAnimations;foreach(var clip in clips){clip.loopTime=false;clip.lockRootRotation=true;clip.lockRootHeightY=true;clip.lockRootPositionXZ=true;}importer.clipAnimations=clips;importer.SaveAndReimport();}
 static void Assign(CombatPresentation p){AudioClip Sound(string n)=>AssetDatabase.LoadAssetAtPath<AudioClip>(Folder+n+".wav");var source=p.GetComponent<AudioSource>();if(!source)source=p.gameObject.AddComponent<AudioSource>();p.combatAudioSource=source;source.playOnAwake=false;source.loop=false;source.spatialBlend=1;source.dopplerLevel=0;source.minDistance=1;source.maxDistance=20;source.rolloffMode=AudioRolloffMode.Linear;p.soundVolume=.65f;p.lightAttackSound=Sound("whoosh");p.heavyAttackSound=Sound("heavy whoosh");p.dodgeSound=Sound("whoosh");p.hitSound=p.deathSound=Sound("thud");p.blockSound=p.postureBreakSound=Sound("metal clash");p.deflectSound=Sound("parry");p.evadeSound=null; // Dodge already has a whoosh; avoid doubled sound on contact.
  p.showDebugGeometry=false;p.showTimingDebug=false;p.showReactionTint=false;p.useProceduralReaction=false;
 }
 public static void Install(){
  ImportClip(Folder+"player impact.fbx",true);ImportClip(Folder+"mutant impact.fbx",false);
  var player=Clip(Folder+"player impact.fbx");var mutant=Clip(Folder+"mutant impact.fbx");
  var pc=AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Models/Player/PlayerAnimController.controller");var ec=AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Models/Enemy/Anims/Enemy Animator Controller.controller");
  Reaction(pc,"HitReaction",player,"CombatStagger","GroundMovement",false);Reaction(pc,"Recoil",player,"CombatRecoil","GroundMovement",false);
  Reaction(pc,"Guard",AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Models/Player/Anims/block idle.anim"),"Blocking","GroundMovement",false);
  Reaction(ec,"HitReaction",mutant,"CombatStagger","loco",true);Reaction(ec,"Recoil",mutant,"CombatRecoil","loco",true);State(ec,"Stunned",mutant);
  foreach(var path in new[]{"Assets/Prefabs/Player.prefab","Assets/Models/Enemy/Enemy.prefab"}){var root=PrefabUtility.LoadPrefabContents(path);try{Assign(root.GetComponent<CombatPresentation>());PrefabUtility.SaveAsPrefabAsset(root,path);}finally{PrefabUtility.UnloadPrefabContents(root);}}
  EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");foreach(var p in UnityEngine.Object.FindObjectsByType<CombatPresentation>(FindObjectsSortMode.None)){Assign(p);PrefabUtility.RecordPrefabInstancePropertyModifications(p);if(p.GetComponent<Player>() && !p.GetComponent<CombatHud>())p.gameObject.AddComponent<CombatHud>();var host=p.GetComponent<EnemyBrainHost>();if(host){host.showCombatDiagnostics=false;host.mode=EnemyControlMode.Normal;PrefabUtility.RecordPrefabInstancePropertyModifications(host);}}
  EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());AssetDatabase.SaveAssets();
  foreach(var clip in new[]{player,mutant})report.Add($"CLIP {AssetDatabase.GetAssetPath(clip)} length={clip.length} human={clip.humanMotion} bindings={AnimationUtility.GetCurveBindings(clip).Length}");
  foreach(var sound in new[]{"whoosh","heavy whoosh","metal clash","parry","thud"}){var clip=AssetDatabase.LoadAssetAtPath<AudioClip>(Folder+sound+".wav");if(!clip)throw new Exception("Missing sound "+sound);report.Add($"SOUND {sound} length={clip.length} channels={clip.channels}");}
  File.WriteAllLines("Logs/Presentation20261118/installation.txt",report);Debug.Log("PRESENTATION_INSTALL_PASS");
 }
}
