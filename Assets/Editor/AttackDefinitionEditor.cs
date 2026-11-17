using UnityEditor;
using UnityEngine;
using GameCombat;
[CustomEditor(typeof(AttackDefinition))]
public class AttackDefinitionEditor : Editor {
 GameObject previewActor;float previewFrame;bool ownsPreview;
 void Field(string name)=>EditorGUILayout.PropertyField(serializedObject.FindProperty(name),true);
 public override void OnInspectorGUI(){
 serializedObject.Update();var attack=(AttackDefinition)target;
 EditorGUILayout.HelpBox("Charge = warning only. Hit start/end = damage on/off. Frames are from the original clip, before its Animator speed is applied. Damage runs at 50 ticks per second (20 ms steps). Authored State Speed must match the Animator state speed. This field does not change playback speed. Normal blends and clip speeds are kept.",MessageType.Info);
 Field("useClipFrames");
 if(serializedObject.FindProperty("useClipFrames").boolValue){foreach(var name in new[]{"timingClip","sourceFrameRate","authoredStateSpeed","hitStartFrame","hitEndFrame","actionEndFrame","limitParryFrames","parryStartFrame","parryEndFrame"})Field(name);}
 else foreach(var name in new[]{"windup","active","recovery"})Field(name);
 serializedObject.ApplyModifiedProperties();
 EditorGUILayout.LabelField($"Charge ends {attack.Windup:F3}s | Damage ends {attack.Windup+attack.Active:F3}s | Action ends {attack.Duration:F3}s");
 if(attack.timingClip && GUILayout.Button("Read clip FPS and length")){serializedObject.FindProperty("sourceFrameRate").floatValue=attack.timingClip.frameRate;serializedObject.FindProperty("clipDuration").floatValue=attack.timingClip.length;serializedObject.ApplyModifiedProperties();}
 EditorGUILayout.Space();EditorGUILayout.LabelField("Look at a frame (outside Play Mode)",EditorStyles.boldLabel);
 previewActor=(GameObject)EditorGUILayout.ObjectField("Actor to preview",previewActor,typeof(GameObject),true);
 using(new EditorGUI.DisabledScope(Application.isPlaying || !attack.timingClip || !previewActor)){
 EditorGUI.BeginChangeCheck();previewFrame=EditorGUILayout.Slider("Preview frame",previewFrame,0,attack.timingClip?attack.timingClip.length*attack.sourceFrameRate:1);
 if(EditorGUI.EndChangeCheck())Sample(attack);
 EditorGUILayout.BeginHorizontal();if(GUILayout.Button("Hit starts here"))Set("hitStartFrame");if(GUILayout.Button("Hit ends here"))Set("hitEndFrame");if(GUILayout.Button("Action ends here"))Set("actionEndFrame");EditorGUILayout.EndHorizontal();
 }
 if(GUILayout.Button("Stop preview / restore pose"))StopPreview();
 EditorGUILayout.HelpBox("Choose a scene actor above, then drag Preview frame. Put Hit start on the first frame where the weapon should hurt. Put Hit end after the last such frame. Keep Action end after Hit end. For combos, Action end also sets when a queued next strike starts. It can be beyond the clip end to keep recovery. Changing it can shift the controller exit: check Show Timing Debug afterward. Limit Parry Frames lets you choose which damaging frames accept a parry. Outside that range a fresh guard still blocks. Combat Tuning controls how long RMB stays a fresh parry press. Change timing between attacks, not during a swing. Preview does no damage.",MessageType.None);
 serializedObject.Update();DrawPropertiesExcluding(serializedObject,"m_Script","useClipFrames","timingClip","sourceFrameRate","authoredStateSpeed","hitStartFrame","hitEndFrame","actionEndFrame","limitParryFrames","parryStartFrame","parryEndFrame","windup","active","recovery");serializedObject.ApplyModifiedProperties();
 if(attack.useClipFrames && attack.timingClip && attack.hitEndFrame>attack.timingClip.length*attack.sourceFrameRate+.01f)EditorGUILayout.HelpBox($"Hit End is past the clip end ({attack.timingClip.length*attack.sourceFrameRate:F1} frames). That part of the hit window uses the final held pose. Your saved value has not been changed.",MessageType.Warning);
 if(attack.nextCombo)EditorGUILayout.LabelField($"Next-click window: {Mathf.Max(attack.Windup,attack.Duration-attack.comboInputSeconds):F2}s to {attack.Duration:F2}s; queued strike waits for recovery.");
 if(!attack.Valid)EditorGUILayout.HelpBox("Timing is not valid. Hit end must be after Hit start. Action end must be at or after Hit end.",MessageType.Error);
 }
 void Set(string field){serializedObject.Update();serializedObject.FindProperty(field).floatValue=previewFrame;serializedObject.ApplyModifiedProperties();}
 void Sample(AttackDefinition a){if(!a.timingClip || !previewActor || Application.isPlaying)return;if(AnimationMode.InAnimationMode() && !ownsPreview)return;if(!ownsPreview){AnimationMode.StartAnimationMode();ownsPreview=true;}var animator=previewActor.GetComponentInChildren<Animator>();if(!animator)return;AnimationMode.BeginSampling();AnimationMode.SampleAnimationClip(animator.gameObject,a.timingClip,previewFrame/Mathf.Max(1,a.sourceFrameRate));AnimationMode.EndSampling();SceneView.RepaintAll();}
 void StopPreview(){if(ownsPreview && AnimationMode.InAnimationMode())AnimationMode.StopAnimationMode();ownsPreview=false;}
 void OnEnable(){SceneView.duringSceneGui+=DrawBox;EditorApplication.playModeStateChanged+=PlayChanged;}
 void PlayChanged(PlayModeStateChange state){StopPreview();}
 void OnDisable(){SceneView.duringSceneGui-=DrawBox;EditorApplication.playModeStateChanged-=PlayChanged;StopPreview();}
 void DrawBox(SceneView view){if(!ownsPreview || !previewActor)return;var a=(AttackDefinition)target;var actor=previewActor.GetComponentInParent<Combatant>();var root=actor?actor.transform:previewActor.transform;var facing=actor && actor.facing?actor.facing:root;var old=Handles.matrix;Handles.matrix=Matrix4x4.TRS(root.position,facing.rotation,Vector3.one);Handles.color=previewFrame>=a.hitStartFrame && previewFrame<a.hitEndFrame?Color.red:Color.yellow;Handles.DrawWireCube(a.LocalCenter(a.FrameTime(previewFrame)),a.halfExtents*2);Handles.matrix=old;}
}

// New fields can otherwise keep an old import cache until the user manually reimports.
public static class CombatFrameAssetImport {
 [InitializeOnLoadMethod] static void RefreshAfterCompile(){EditorApplication.delayCall+=()=>{const string key="CombatFrameFieldsImported.v2";if(SessionState.GetBool(key,false))return;SessionState.SetBool(key,true);foreach(var name in new[]{"Light1","Light2","Light3","RunningHeavy","EnemyLight","EnemyHeavy","CombatTuning"}){var path="Assets/CombatData/"+name+".asset";if(System.IO.File.Exists(path))AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);}};}
}
