using UnityEngine;
namespace GameCombat {
[RequireComponent(typeof(Combatant))]
public class CombatPresentation : MonoBehaviour {
 [Header("Optional Animator assignment points")]
 [Tooltip("Optional Bool parameters. Add matching states/transitions in the existing Animator to use them. Missing parameters are ignored. Presentation never owns combat timing.")]
 public string guardAnimatorBool="Blocking",staggerAnimatorBool="CombatStagger",recoilAnimatorBool="CombatRecoil",dodgeAnimatorBool="CombatDodge";
 void OptionalBool(string parameter,bool value){if(!animator || !animator.enabled || !animator.runtimeAnimatorController || string.IsNullOrEmpty(parameter))return;foreach(var p in animator.parameters)if(p.name==parameter && p.type==AnimatorControllerParameterType.Bool){animator.SetBool(parameter,value);return;}}
 void AnimationParameters(){var r=actor.Runner;OptionalBool(guardAnimatorBool,r.Guarding);OptionalBool(staggerAnimatorBool,r.Action==CombatAction.Stagger);OptionalBool(recoilAnimatorBool,r.Action==CombatAction.Recoil);OptionalBool(dodgeAnimatorBool,r.Action==CombatAction.Dodge);}
 [Header("Optional combat sounds (cosmetic only)")]
 [Tooltip("Actor AudioSource: Play On Awake off, Loop off. Missing source/clips are safe.")] public AudioSource combatAudioSource;
 [Range(0,1)] public float soundVolume=1;
 [Tooltip("One-shot at accepted action start. Heavy falls back to Light if unassigned.")] public AudioClip lightAttackSound,heavyAttackSound,dodgeSound;
 [Tooltip("One-shot when this actor receives the unique resolver result, never per hurtbox.")] public AudioClip hitSound,blockSound,deflectSound,evadeSound,postureBreakSound;
 [Tooltip("One-shot on authoritative death state entry.")] public AudioClip deathSound;
 public int SoundPlayCount {get;private set;}
 readonly System.Collections.Generic.Dictionary<int,ulong> soundedResults=new();
 ulong soundedAttack;bool hasSoundedAttack;CombatAction soundedAction;
 void PlaySound(AudioClip clip){if(!clip || !combatAudioSource || !combatAudioSource.isActiveAndEnabled)return;combatAudioSource.PlayOneShot(clip,soundVolume);SoundPlayCount++;}
 void ActionSound(){var r=actor.Runner;if(r.Action==CombatAction.Attack){if(!hasSoundedAttack || soundedAttack!=r.AttackId){hasSoundedAttack=true;soundedAttack=r.AttackId;PlaySound(r.Attack.heavy && heavyAttackSound?heavyAttackSound:lightAttackSound);}}else if(r.Action!=soundedAction){if(r.Action==CombatAction.Dodge)PlaySound(dodgeSound);else if(r.Action==CombatAction.Dead)PlaySound(deathSound);}soundedAction=r.Action;}
 public bool showTimingDebug;
 [Tooltip("Opt-in geometry overlay for development only; collision remains active when hidden.")] public bool showDebugGeometry;
 public bool showReactionTint=true;
 public bool useProceduralReaction=true;
 public void ResetEpisodeState(){soundedResults.Clear();hasSoundedAttack=false;soundedAction=CombatAction.Free;RestoreBone();playerAnimation?.Clear();playerAnimation=GetComponent<Player>() && animator?new PlayerComboAnimation(animator):null;flash=0;outcome=default;defendingResult=false;foreach(var line in new[]{arc,volume,ring})if(line)line.enabled=false;for(int i=0;i<meshes.Length;i++)if(meshes[i])meshes[i].SetPropertyBlock(original[i]);}
 Renderer[] meshes; MaterialPropertyBlock[] original; MaterialPropertyBlock tint;
 PlayerComboAnimation playerAnimation;
 Combatant actor;Animator animator;LineRenderer arc,volume,ring;float flash;CombatOutcome outcome; bool defendingResult; Transform reactionBone; Quaternion boneRotation; bool boneApplied;
 public string StatusText {get {var r=actor.Runner;return r.Broken?$"POSTURE BREAK / STUN {r.Remaining:F2}s":r.Action==CombatAction.Recoil?$"RECOIL {r.Remaining:F2}s":r.Action==CombatAction.Stagger?$"HIT STAGGER {r.Remaining:F2}s":flash>0 && defendingResult?outcome==CombatOutcome.Deflect?"DEFLECT":outcome==CombatOutcome.Block?"BLOCK":outcome.ToString().ToUpperInvariant():r.Guarding?"GUARD":r.Action==CombatAction.Attack?(r.Phase==AttackPhase.Windup?"CHARGE":r.Phase==AttackPhase.Active?"ATTACK":"RECOVERY"):"";}}
 public Color ReactionColor {get {var r=actor.Runner;if(r.Broken)return new Color(1,.25f,0);if(r.Action==CombatAction.Recoil)return new Color(1,.65f,0);if(r.Action==CombatAction.Attack)return r.Phase==AttackPhase.Windup?(r.Attack.heavy?new Color(1,.3f,1):Color.yellow):r.Phase==AttackPhase.Active?Color.red:Color.gray;return defendingResult && outcome==CombatOutcome.Deflect?Color.white:outcome==CombatOutcome.Block?Color.cyan:Color.red;}}

 void RestoreBone(){if(boneApplied && reactionBone)reactionBone.localRotation=boneRotation;boneApplied=false;}
 void LateUpdate(){if(!useProceduralReaction)return;var r=actor.Runner;if(!r.Broken && r.Action!=CombatAction.Recoil)return;if(animator && animator.enabled && animator.isHuman){reactionBone=animator.GetBoneTransform(HumanBodyBones.Chest);if(!reactionBone)reactionBone=animator.GetBoneTransform(HumanBodyBones.Spine);if(reactionBone){boneRotation=reactionBone.localRotation;float bend=r.Broken?30: -22*Mathf.Clamp01(r.Remaining/actor.tuning.recoil);reactionBone.localRotation=boneRotation*Quaternion.Euler(bend,0,r.Broken?8:0);boneApplied=true;}}}

 void Awake(){meshes=GetComponentsInChildren<SkinnedMeshRenderer>();original=new MaterialPropertyBlock[meshes.Length];for(int i=0;i<meshes.Length;i++){original[i]=new MaterialPropertyBlock();meshes[i].GetPropertyBlock(original[i]);}tint=new MaterialPropertyBlock();actor=GetComponent<Combatant>();animator=GetComponentInChildren<Animator>();if(GetComponent<Player>() && animator)playerAnimation=new PlayerComboAnimation(animator);arc=Line("Guard",new Color(0,1,1,.5f));volume=Line("Attack volume",Color.yellow);ring=Line("Result",Color.white);}
 LineRenderer Line(string label,Color color){var go=new GameObject(label);go.transform.SetParent(transform,false);var l=go.AddComponent<LineRenderer>();l.material=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default"));l.material.SetColor("_BaseColor",Color.white);l.material.SetFloat("_Surface",1);l.material.SetFloat("_ZWrite",0);l.material.SetFloat("_SrcBlend",5);l.material.SetFloat("_DstBlend",10);l.material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");l.material.renderQueue=3000;l.startColor=l.endColor=color;l.startWidth=l.endWidth=.045f;l.useWorldSpace=true;return l;}
 void OnEnable(){soundedAction=GetComponent<CombatActionRunner>().Action;GetComponent<CombatActionRunner>().Changed+=Changed;GetComponent<Combatant>().Result+=Result;}
 void OnDisable(){RestoreBone();playerAnimation?.Clear();OptionalBool(guardAnimatorBool,false);OptionalBool(staggerAnimatorBool,false);OptionalBool(recoilAnimatorBool,false);OptionalBool(dodgeAnimatorBool,false);GetComponent<CombatActionRunner>().Changed-=Changed;GetComponent<Combatant>().Result-=Result;}
 void Result(CombatResult r){outcome=r.outcome;defendingResult=r.defenderId==actor.Id;flash=.55f;if(!defendingResult || soundedResults.TryGetValue(r.attackerId,out var id) && id==r.attackInstanceId)return;soundedResults[r.attackerId]=r.attackInstanceId;PlaySound(r.outcome==CombatOutcome.Hit?hitSound:r.outcome==CombatOutcome.Block?blockSound:r.outcome==CombatOutcome.Deflect?deflectSound:r.outcome==CombatOutcome.Evade?evadeSound:postureBreakSound);}
 void Changed(){ActionSound();AnimationParameters();if(playerAnimation!=null){playerAnimation.Changed(actor.Runner);return;}if(!animator)return;if(GetComponent<EnemyBrainHost>()){if(actor.Runner.Broken)return;if(actor.Runner.Action==CombatAction.Attack)animator.SetTrigger("Attack");else if(actor.Runner.Action!=CombatAction.Free){animator.ResetTrigger("Attack");animator.SetBool("Blocking",false);
 // Forced reactions must leave a currently playing attack; trigger reset alone cannot do this.
 string reaction=actor.Runner.Action==CombatAction.Recoil?"Recoil":"HitReaction";
 if(actor.Runner.Action!=CombatAction.Dead && !animator.HasState(0,Animator.StringToHash(reaction)) && animator.enabled && animator.runtimeAnimatorController && animator.HasState(0,Animator.StringToHash("loco")))animator.CrossFadeInFixedTime("loco",.04f);
 }return;}var r=actor.Runner;string state=r.Action==CombatAction.Attack?r.Attack.animationState:r.Action==CombatAction.Dodge?"dodge":null;
 if(state!=null && animator.HasState(0,Animator.StringToHash(state))){animator.speed=r.Action==CombatAction.Attack?r.Attack.PlaybackSpeed:1;animator.CrossFadeInFixedTime(state,.04f);}
 else {animator.speed=1;if(r.Action==CombatAction.Free && animator.HasState(0,Animator.StringToHash("GroundMovement")))animator.CrossFadeInFixedTime("GroundMovement",.08f);}
 }
 void Update(){RestoreBone();AnimationParameters();playerAnimation?.Tick(actor.Runner);var r=actor.Runner;flash=Mathf.Max(0,flash-Time.deltaTime);
 bool colored=showReactionTint && (r.Invulnerable || r.Action==CombatAction.Stagger || r.Action==CombatAction.Recoil || flash>0);
 tint.SetColor("_BaseColor",r.Invulnerable?new Color(.2f,.5f,1):r.Action==CombatAction.Stagger && !r.Broken?Color.magenta:ReactionColor);
 for(int i=0;i<meshes.Length;i++)if(meshes[i])meshes[i].SetPropertyBlock(colored?tint:original[i]);
 arc.enabled=showDebugGeometry && r.Guarding;arc.positionCount=25;for(int i=0;i<25;i++){float angle=Mathf.Lerp(-60,60,i/24f);arc.SetPosition(i,actor.facing.position+Vector3.up+Quaternion.AngleAxis(angle,Vector3.up)*actor.facing.forward*.8f);}
 volume.enabled=showDebugGeometry && r.Action==CombatAction.Attack && r.Phase==AttackPhase.Active;
 if(volume.enabled){var a=r.Attack;var center=transform.position+actor.facing.rotation*a.LocalCenter(r.Elapsed);volume.startColor=volume.endColor=r.Phase==AttackPhase.Active?Color.red:a.heavy?new Color(1,.3f,1):Color.yellow;var e=a.halfExtents;int[] corners={0,1,3,2,0,4,5,1,5,7,3,7,6,2,6,4};volume.positionCount=corners.Length;for(int i=0;i<corners.Length;i++){int k=corners[i];volume.SetPosition(i,center+actor.facing.rotation*new Vector3((k&1)==0?-e.x:e.x,(k&2)==0?-e.y:e.y,(k&4)==0?-e.z:e.z));}}
 ring.enabled=showDebugGeometry && (flash>0 || r.Action==CombatAction.Stagger || r.Action==CombatAction.Recoil || r.Invulnerable);ring.startColor=ring.endColor=r.Invulnerable?Color.blue:ReactionColor;ring.positionCount=33;for(int i=0;i<33;i++)ring.SetPosition(i,transform.position+Vector3.up*.1f+new Vector3(Mathf.Sin(i*Mathf.PI/16),0,Mathf.Cos(i*Mathf.PI/16))*.8f);
 }
 void OnGUI(){if(showTimingDebug && actor.Runner.Action==CombatAction.Attack){var a=actor.Runner.Attack;float visual=-1;bool heldPose=false;if(animator && animator.enabled){var state=animator.GetCurrentAnimatorStateInfo(0);if(animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsName(a.animationState))state=animator.GetNextAnimatorStateInfo(0);if(state.IsName(a.animationState)){visual=state.normalizedTime*a.clipDuration*a.sourceFrameRate;float end=a.clipDuration*a.sourceFrameRate;heldPose=(!a.timingClip || !a.timingClip.isLooping) && visual>end;if(heldPose)visual=end;}}GUI.Box(new Rect(12,Screen.height-100,600,85),$"{name}: {actor.Runner.Phase} | game frame {a.FrameAt(actor.Runner.Elapsed):F1} | clip frame {(visual<0?"waiting":visual.ToString("F1")+(heldPose?" (end pose)":""))}\nHit frames {a.hitStartFrame:F1} to {a.hitEndFrame:F1} | combo {(actor.Runner.ComboQueued?"queued":actor.Runner.ComboWindowOpen?"CLICK NOW":"closed")} | parry lasts {actor.tuning.ParryWindow:F3}s after guard press\nRed box = damage ON. CHARGE = warning only.");}
}

 void OnDestroy(){foreach(var line in new[]{arc,volume,ring})if(line && line.sharedMaterial)Destroy(line.sharedMaterial);}
}
}

