using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using GameCombat.AgentContract;
namespace GameCombat {
public partial class EnemyBrainHost {
 [Tooltip("Optional ML adapter. Shared gameplay has no ML package dependency.")] public MonoBehaviour agentPolicy;
 ICombatPolicy Policy=>agentPolicy as ICombatPolicy;
 long agentTick,applyTick,nextAgentTick;bool awaiting,received;int pendingMove,pendingCombat;uint pendingSequence;float lastSeen=10;
 Vector2 previousMove;int previousCombat;bool rejected;
 public static Vector2 AgentMovement(int choice){if(choice==0)return Vector2.zero;float angle=(choice-1)*Mathf.PI/4;return new Vector2(Mathf.Sin(angle),Mathf.Cos(angle));}
 public string AgentTuningHash {get {var builder=new StringBuilder(JsonUtility.ToJson(actor.tuning));var seen=new HashSet<AttackDefinition>();for(var attack=actor.light;attack && seen.Add(attack);attack=attack.nextCombo)builder.Append(JsonUtility.ToJson(attack));builder.Append(JsonUtility.ToJson(actor.heavy));using var sha=SHA256.Create();return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(System.Text.RegularExpressions.Regex.Replace(builder.ToString(),@"""instanceID"":-?\d+","\"instanceID\":0")))).Replace("-","").ToLowerInvariant();}}
 bool AgentAvailable(out string reason){reason="ML adapter missing";try{return Policy!=null && Policy.CanActivate(AgentTuningHash,out reason);}catch(Exception e){reason=e.Message;return false;}}
 void ResetAgentOwner(){Policy?.Deactivate();awaiting=received=false;agentTick=nextAgentTick=0;previousMove=default;previousCombat=0;rejected=false;lastSeen=10;}
 void ReceiveAgent(uint generation,uint decision,int move,int combat){if(!awaiting || received || generation!=Generation || decision!=pendingSequence || agentTick>=applyTick || move<0 || move>=9 || combat<0 || combat>=5)return;pendingMove=move;pendingCombat=combat;received=true;}
 bool BufferAgentIntent(uint generation,uint decision,CombatIntent intent){if(!awaiting || received || generation!=Generation || decision!=pendingSequence || agentTick>=applyTick)return false;int move=-1;for(int i=0;i<9;i++)if((AgentMovement(i)-intent.localMove).sqrMagnitude<.0001f){move=i;break;}if(move<0)return false;int combat=intent.request==CombatRequest.Light?2:intent.request==CombatRequest.Heavy?3:intent.request==CombatRequest.Dodge?4:intent.guardHeld?1:0;ReceiveAgent(generation,decision,move,combat);return received;}
 void CollectAgent(CombatObservation observed,bool liveVisible,bool grounded){
  agentTick++;
  if(!AgentAvailable(out var reason)){FallbackToNormal(reason);return;}
  if(awaiting && agentTick>=applyTick){awaiting=false;Policy.Deactivate();if(!received){FallbackToNormal("ML decision missed designated tick");return;}
   if(liveVisible){var request=pendingCombat==2?CombatRequest.Light:pendingCombat==3?CombatRequest.Heavy:pendingCombat==4?CombatRequest.Dodge:CombatRequest.None;var move=AgentMovement(pendingMove);var forward=ObservedTargetDirection.sqrMagnitude>.01f?ObservedTargetDirection:actor.facing.forward;var direction=request==CombatRequest.Dodge && move.sqrMagnitude>.01f?Vector3.Cross(Vector3.up,forward)*move.x+forward*move.y:actor.facing.forward;
    Intent=new CombatIntent{sequence=pendingSequence,localMove=move,guardHeld=pendingCombat==1,request=request};actor.Runner.Submit(Intent,direction,grounded);rejected=request!=CombatRequest.None && actor.Runner.LastRequest!=RequestResult.Accepted;previousMove=!rejected && actor.Runner.Action==CombatAction.Free?move:Vector2.zero;previousCombat=rejected?0:pendingCombat;Intent=new CombatIntent{localMove=move,guardHeld=pendingCombat==1,sequence=pendingSequence};motor.Face(ObservedTargetDirection);
   }else {Intent=default;actor.Runner.SetGuard(false);previousMove=default;previousCombat=0;}
   received=false;
  }
  if(!liveVisible){Intent=default;actor.Runner.SetGuard(false);}
  if(agentTick<nextAgentTick || awaiting)return;nextAgentTick=agentTick+3;
  try{var frame=BuildAgentFrame(observed,grounded);frame.generation=Generation;frame.sequence=++sequence;pendingSequence=sequence;applyTick=agentTick+1;awaiting=true;received=false;
   Policy.Decide(frame,ReceiveAgent);}catch(Exception ex){awaiting=false;FallbackToNormal("ML inference failure: "+ex.Message);}
 }
 DecisionFrame BuildAgentFrame(CombatObservation o,bool grounded){
  var r=actor.Runner;var values=new List<float>(66);
  void Add(float value){if(!float.IsFinite(value))throw new InvalidOperationException("Nonfinite combat observation");values.Add(Mathf.Clamp(value,-1,1));}
  void One(int value,int size){for(int i=0;i<size;i++)Add(i==value?1:0);}
  var forward=ObservedTargetDirection.sqrMagnitude>.01f?ObservedTargetDirection:actor.facing.forward;var right=Vector3.Cross(Vector3.up,forward);Vector3 velocity=body.rb.linearVelocity;
  Add(actor.Health.Health/Mathf.Max(1,actor.Health.maxHealth));Add(actor.Posture/Mathf.Max(1,actor.tuning.maxPosture));Add(grounded?1:0);Add(Vector3.Dot(velocity,right)/20);Add(Vector3.Dot(velocity,forward)/20);One((int)r.Action,6);One((int)r.Phase,4);Add(r.Elapsed/Mathf.Max(.02f,r.Elapsed+r.Remaining));Add(r.Phase==AttackPhase.Recovery?r.Remaining/Mathf.Max(.02f,r.Attack.Recovery):0);Add(r.DodgeCooldownFraction);Add(r.Guarding?1:0);Add(r.DeflectionRearmFraction);
  Add(o.visible?1:0);Add(o.visible?Vector3.Dot(o.relativePosition,right)/10:0);Add(o.visible?Vector3.Dot(o.relativePosition,forward)/10:0);Add(o.visible?Vector3.Dot(o.opponentVelocity,right)/20:0);Add(o.visible?Vector3.Dot(o.opponentVelocity,forward)/20:0);Add(o.visible?Vector3.Dot(o.opponentForward,right):0);Add(o.visible?Vector3.Dot(o.opponentForward,forward):0);Add(o.visible?o.opponentHealth:0);Add(o.visible?o.opponentPosture:0);Add(o.visible && o.opponentGuard?1:0);
  if(o.visible){One((int)o.opponentAction,6);One((int)o.opponentPhase,4);}else for(int i=0;i<10;i++)Add(0);Add(o.visible?o.opponentProgress:0);
  var movement=new bool[9];movement[0]=true;
  for(int i=0;i<8;i++){var local=AgentMovement(i+1);var direction=right*local.x+forward*local.y;float clearance=Physics.Raycast(body.enemyCollider.bounds.center,direction,out var hit,10,actor.tuning.obstacles,QueryTriggerInteraction.Ignore)?hit.distance:10;bool step=motor.CanStep(direction*.15f);Add(clearance/10);Add(step?1:0);movement[i+1]=r.Action==CombatAction.Free && step;}
  Add(previousMove.x);Add(previousMove.y);One(previousCombat,5);Add(rejected?1:0);lastSeen=o.visible?0:Mathf.Min(10,lastSeen+.06f);Add(lastSeen/10);
  if(values.Count!=66)throw new InvalidOperationException("CombatObservationV1 size "+values.Count);
  bool free=r.Action==CombatAction.Free && !actor.Dead;
  return new DecisionFrame{observations=values.ToArray(),movementAllowed=movement,combatAllowed=new[]{true,free && grounded,free && grounded && actor.light && actor.light.Valid,free && grounded && actor.heavy && actor.heavy.Valid,free && grounded && r.DodgeReady}};
 }
}
}


