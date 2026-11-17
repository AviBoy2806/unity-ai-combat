using System.Collections.Generic;
namespace GameCombat {
// Immediate visibility masking; delayed public cues. Perception memory lives outside this buffer.
public sealed class EnemyObservationStream {
 readonly Queue<CombatObservation> samples=new(); CombatObservation delayed; bool ready;
 static CombatObservation Mask(CombatObservation current){current.visible=false;current.opponentAction=CombatAction.Free;current.opponentPhase=AttackPhase.None;current.opponentGuard=false;current.relativePosition=default;current.distance=float.PositiveInfinity;current.opponentAttackId=0;current.opponentElapsed=0;current.opponentWindup=0;current.opponentReach=0;current.opponentVelocity=current.opponentForward=default;current.opponentHealth=current.opponentPosture=current.opponentProgress=0;return current;}
 public void Clear(){samples.Clear();delayed=default;ready=false;}
 public CombatObservation Sample(CombatObservation current){
 if(!current.visible){Clear();return Mask(current);}
 samples.Enqueue(current);
 while(samples.Count>0 && samples.Peek().time<=current.time-.16+1e-6){delayed=samples.Dequeue();ready=true;}
 if(!ready)return Mask(current);
 var result=delayed;result.time=current.time;result.selfAction=current.selfAction;result.selfPosture=current.selfPosture;result.grounded=current.grounded;result.dodgeReady=current.dodgeReady;result.heavyReady=current.heavyReady;return result;
 }
}
}

