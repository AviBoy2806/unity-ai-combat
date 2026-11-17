using System;
using System.Collections.Generic;
using UnityEngine;
namespace GameCombat {
public class CombatActionRunner : MonoBehaviour {
 Combatant owner; public Combatant Owner => owner ? owner : owner=GetComponent<Combatant>();
 public CombatAction Action {get; private set;}
 public AttackDefinition Attack {get; private set;}
 public AttackPhase Phase => Action!=CombatAction.Attack?AttackPhase.None:Elapsed+1e-5f<Attack.Windup?AttackPhase.Windup:Elapsed+1e-5f<Attack.Windup+Attack.Active?AttackPhase.Active:AttackPhase.Recovery;
 public float Elapsed {get; private set;} public float PreviousElapsed {get; private set;}
 public bool Guarding {get; private set;} public bool Deflecting => Guarding && Clock+1e-6<deflectUntil;
 public bool Invulnerable => Action==CombatAction.Dodge && Elapsed+0.00001f>=Owner.tuning.dodgeStart && Elapsed<Owner.tuning.dodgeEnd-0.00001f;
 public bool OwnsMovement => Action!=CombatAction.Free || Guarding;
 public bool Broken => broken;
 public bool ComboQueued => combo;
 public bool ComboWindowOpen => Action==CombatAction.Attack && Attack.nextCombo && Elapsed+1e-5f>=Mathf.Max(Attack.Windup,Attack.Duration-Attack.comboInputSeconds);
 public float Remaining => Action==CombatAction.Free || Action==CombatAction.Dead?0:Mathf.Max(0,duration-Elapsed);
 public ulong AttackId {get; private set;}
 public Vector3 Direction {get; private set;} public double Clock {get; private set;}
 public RequestResult LastRequest {get; private set;}
 public event Action Changed;
 public event Action CollectIntent;
 public event Action ApplyMovement;
 public event Action<CombatRequest,RequestResult> RequestEvaluated;
 public bool LastAcceptedIllegal {get;private set;}
 readonly HashSet<int> hits=new();
 double dodgeReady, deflectUntil=double.NegativeInfinity, lastDeflect=double.NegativeInfinity, releasedAt=double.NegativeInfinity;
 float duration; bool held, combo, broken; uint sequence; bool hasSequence;
 void Awake(){ owner=GetComponent<Combatant>(); }
 public float DodgeCooldownFraction => Mathf.Clamp01((float)(dodgeReady-Clock)/Mathf.Max(.02f,Owner.tuning.dodgeCooldown));
 public float DeflectionRearmFraction => Mathf.Clamp01((float)System.Math.Max(Owner.tuning.releaseRearm-(Clock-releasedAt),Owner.tuning.deflectRearm-(Clock-lastDeflect))/Mathf.Max(.02f,System.Math.Max(Owner.tuning.releaseRearm,Owner.tuning.deflectRearm)));
 public bool DodgeReady => Clock+1e-6>=dodgeReady;
 public void ClearControllerIntent(){combo=false;hasSequence=false;SetGuard(false);}
 internal void ResetEpisodeState(){Action=CombatAction.Free;Attack=null;Elapsed=PreviousElapsed=duration=0;Clock=0;AttackId=0;Direction=Vector3.zero;LastRequest=RequestResult.Accepted;LastAcceptedIllegal=false;Guarding=held=combo=broken=hasSequence=false;sequence=0;hits.Clear();dodgeReady=0;deflectUntil=lastDeflect=releasedAt=double.NegativeInfinity;}
 public void Collect(){ CollectIntent?.Invoke(); }
 public void Move(){ ApplyMovement?.Invoke(); }
 public void Submit(CombatIntent intent,Vector3 direction,bool grounded) {
 if(hasSequence && intent.sequence==sequence)return; sequence=intent.sequence; hasSequence=true;
 bool legal=!Owner.Dead && (intent.request==CombatRequest.Light && ComboWindowOpen || grounded && Action==CombatAction.Free && (intent.request!=CombatRequest.Dodge || DodgeReady));
 LastAcceptedIllegal=false;
 if(intent.request==CombatRequest.Dodge && Action==CombatAction.Free) { SetGuard(false); LastRequest=TryStart(intent.request,direction,grounded);LastAcceptedIllegal=LastRequest==RequestResult.Accepted && !legal;RequestEvaluated?.Invoke(intent.request,LastRequest); return; }
 SetGuard(intent.guardHeld,grounded);
 if(intent.request!=CombatRequest.None){LastRequest=TryStart(intent.request,direction,grounded);LastAcceptedIllegal=LastRequest==RequestResult.Accepted && !legal;RequestEvaluated?.Invoke(intent.request,LastRequest);}
 }
 public void SetGuard(bool value,bool eligible=true) {
 bool fresh=value&&!held;
 if(!value && held) releasedAt=Clock;
 held=value; bool next=value && eligible && Action==CombatAction.Free && !Owner.Dead;
 if(fresh && next && Clock-releasedAt+1e-6>=Owner.tuning.releaseRearm && Clock-lastDeflect+1e-6>=Owner.tuning.deflectRearm) { lastDeflect=Clock; deflectUntil=Clock+Owner.tuning.ParryWindow; }
 Guarding=next;
 }
 public RequestResult TryStart(CombatRequest request,Vector3 direction,bool grounded=true) {
 if(Owner.Dead) return RequestResult.Dead;
 if(request==CombatRequest.None)return RequestResult.Invalid;
 if(request==CombatRequest.Light && ComboWindowOpen) { combo=true;return RequestResult.Accepted; }
 if(Action!=CombatAction.Free || Guarding)return RequestResult.Busy;
 if(!grounded)return RequestResult.Airborne;
 if(request==CombatRequest.Dodge && Clock+1e-6<dodgeReady)return RequestResult.Cooldown;
 var attack=request==CombatRequest.Heavy?Owner.heavy:Owner.light;
 if(request!=CombatRequest.Dodge && (!attack || !attack.Valid))return RequestResult.Invalid;
 Direction=direction.sqrMagnitude>.001f?direction.normalized:Owner.facing.forward;
 if(request==CombatRequest.Dodge) { Action=CombatAction.Dodge;duration=Owner.tuning.dodgeDuration;dodgeReady=Clock+Owner.tuning.dodgeCooldown; Begin(); }
 else StartAttack(attack);
 return RequestResult.Accepted;
 }
 void Begin(){ Elapsed=PreviousElapsed=0; Guarding=false; combo=false; Changed?.Invoke(); }
 void StartAttack(AttackDefinition attack){ Attack=attack;Action=CombatAction.Attack;duration=attack.Duration;AttackId++;hits.Clear();Begin(); }
 public void Tick(float dt) {
 Clock+=dt; if(Owner.Dead){Die();return;}
 PreviousElapsed=Elapsed;
 if(Action!=CombatAction.Free){ Elapsed+=dt;
 if(Elapsed+1e-5f>=duration) {
 if(Action==CombatAction.Attack && combo && Attack.nextCombo) { StartAttack(Attack.nextCombo); }
 else { if(broken)Owner.ResetBrokenPosture();broken=false;Action=CombatAction.Free;Attack=null;Elapsed=PreviousElapsed=0;Guarding=held;Changed?.Invoke(); }
 }}
 Owner.Recover(dt,Clock);
 }
 public bool MarkHit(Combatant target)=>hits.Add(target.Id);
 public bool SamplesActive => Action==CombatAction.Attack && PreviousElapsed<Attack.Windup+Attack.Active && Elapsed+1e-5f>=Attack.Windup;
 public void Interrupt(float seconds,CombatAction action,bool postureBreak=false) {
 if(Owner.Dead){Die();return;}
 if(broken || (Action==CombatAction.Stagger && !postureBreak) || (Action==CombatAction.Recoil && action==CombatAction.Stagger && !postureBreak))return;
 if(postureBreak)seconds=Mathf.Max(seconds,Owner.BreakDuration);
 held=false;deflectUntil=double.NegativeInfinity;broken=postureBreak;Action=action;Attack=null;duration=seconds;Begin();
 }
 public void Die(){ if(Action==CombatAction.Dead)return;Action=CombatAction.Dead;Attack=null;Guarding=false;held=false;combo=false;broken=false;Changed?.Invoke(); }
}
}



