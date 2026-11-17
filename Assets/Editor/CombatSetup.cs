using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using GameCombat;
public static class CombatSetup {
 public static void Run(){
 Directory.CreateDirectory("Assets/CombatData");
 var tuning=AssetDatabase.LoadAssetAtPath<CombatTuning>("Assets/CombatData/CombatTuning.asset");if(!tuning){tuning=ScriptableObject.CreateInstance<CombatTuning>();AssetDatabase.CreateAsset(tuning,"Assets/CombatData/CombatTuning.asset");}
 var l1=Attack("Light1",.2f,.16f,.34f,10,10,20,2,"Attack 1",1);
 var l2=Attack("Light2",.16f,.12f,.32f,10,10,20,2,"Attack 2",.333333f);
 var l3=Attack("Light3",.4f,.2f,.6f,20,20,30,2,"Attack 3",2.2f);
 l1.matchAnimationToAction=l2.matchAnimationToAction=l3.matchAnimationToAction=false;
 l1.nextCombo=l2;l2.nextCombo=l3;EditorUtility.SetDirty(l1);EditorUtility.SetDirty(l2);
 var heavy=Attack("RunningHeavy",.7f,.24f,.86f,30,25,40,2.2f,"Heavy Attack",2.316667f);heavy.heavy=true;heavy.matchAnimationToAction=false;heavy.leapImpulse=5;heavy.forwardSpeed=AnimationCurve.Linear(0,5,1,0);EditorUtility.SetDirty(heavy);
 Install("Assets/Prefabs/Player.prefab",true,tuning,l1,heavy);
 var enemyLight=Attack("EnemyLight",.45f,.18f,.47f,12,12,22,2,"attack",2.666667f);
 var enemyHeavy=Attack("EnemyHeavy",.75f,.22f,.73f,22,25,40,2.4f,"attack",2.666667f);enemyHeavy.heavy=true;EditorUtility.SetDirty(enemyHeavy);
 Install("Assets/Models/Enemy/Enemy.prefab",false,tuning,enemyLight,enemyHeavy);
 // Generate the assigned action wrapper through the Input System importer.
 AssetDatabase.ImportAsset("Assets/Input/PlayerInputAction.inputactions",ImportAssetOptions.ForceUpdate);
 AssetDatabase.SaveAssets();Debug.Log("COMBAT_SETUP_PASS");
 }
 static AttackDefinition Attack(string name,float w,float a,float r,float dmg,float hp,float bp,float reach,string state,float clip){
 string path="Assets/CombatData/"+name+".asset";var def=AssetDatabase.LoadAssetAtPath<AttackDefinition>(path);if(!def){def=ScriptableObject.CreateInstance<AttackDefinition>();AssetDatabase.CreateAsset(def,path);}
 if(!def.useClipFrames){def.windup=w;def.active=a;def.recovery=r;}def.damage=dmg;def.hitPosture=hp;def.blockPosture=bp;def.reach=reach;def.animationState=state;def.clipDuration=clip;EditorUtility.SetDirty(def);return def;}
 static void Install(string path,bool isPlayer,CombatTuning t,AttackDefinition light,AttackDefinition heavy){var root=PrefabUtility.LoadPrefabContents(path);try{
 var actor=root.GetComponent<Combatant>();if(!actor)actor=root.AddComponent<Combatant>();actor.faction=isPlayer?0:1;if(!isPlayer && actor.postureBreakSeconds<=0)actor.postureBreakSeconds=3.5f;actor.tuning=t;actor.light=light;actor.heavy=heavy;
 actor.facing=isPlayer?root.GetComponent<Player>().model:root.transform;
 foreach(var col in root.GetComponentsInChildren<Collider>(true)){if(col.GetComponent<HitDetection>())continue;var hurt=col.GetComponent<Hurtbox>();if(!hurt)hurt=col.gameObject.AddComponent<Hurtbox>();hurt.owner=actor;}
 if(isPlayer && !root.GetComponent<PlayerCombatAdapter>())root.AddComponent<PlayerCombatAdapter>();
 if(isPlayer && !root.GetComponent<CombatDebugControls>())root.AddComponent<CombatDebugControls>();
 if(!isPlayer && !root.GetComponent<EnemyMotor>())root.AddComponent<EnemyMotor>();
 if(!isPlayer && !root.GetComponent<EnemyBrainHost>())root.AddComponent<EnemyBrainHost>();
 if(!root.GetComponent<CombatPresentation>())root.AddComponent<CombatPresentation>();
 PrefabUtility.SaveAsPrefabAsset(root,path);
 }finally{PrefabUtility.UnloadPrefabContents(root);}}
}
