using UnityEngine;
namespace GameCombat {
public enum EnemyControlMode { Normal, Test, ML }
public enum BrainSwitchResult { Applied, Deferred, Unavailable }
// Values only: brains cannot access inputs, opponent queues, or hidden cooldowns.
public struct CombatObservation {
 public double time; public bool visible, grounded, heavyReady, dodgeReady;
 public Vector3 relativePosition; public float distance, selfPosture;
 public CombatAction selfAction, opponentAction; public AttackPhase opponentPhase;
 public bool opponentGuard; public ulong opponentAttackId;
 public float opponentElapsed, opponentWindup, opponentReach;
 public Vector3 opponentVelocity,opponentForward; public float opponentHealth,opponentPosture,opponentProgress;
}
public interface IEnemyCombatBrain {
 CombatIntent Decide(in CombatObservation observation);
 void Reset();
}
// Explicit deterministic boundary fixture. This is never advertised as ML.
public sealed class TestCombatBrain : IEnemyCombatBrain {
 uint sequence; public void Reset(){sequence=0;}
 public CombatIntent Decide(in CombatObservation o)=>new CombatIntent {sequence=++sequence, localMove=new Vector2(1,0), guardHeld=true};
}
public sealed class ScriptedCombatBrain : IEnemyCombatBrain {
 uint sequence; int lights, circle=1; bool coolingPosture; double guardSince=-1, neutralUntil, circleUntil;
 ulong threat; double guardAt, guardUntil; bool defending;
 CombatAction previous; CombatRequest lastAttack;
 public void Reset(){sequence=0;lights=0;circle=1;coolingPosture=false;guardSince=-1;neutralUntil=circleUntil=0;threat=0;defending=false;previous=CombatAction.Free;}
 public string Timers(double now)=>$"circle {System.Math.Max(0,circleUntil-now):F2}s defense {System.Math.Max(0,guardAt-now):F2}/{System.Math.Max(0,guardUntil-now):F2}s";
 public CombatIntent Decide(in CombatObservation o){
 var intent=new CombatIntent{sequence=++sequence};
 if(previous==CombatAction.Attack && o.selfAction==CombatAction.Free){if(lastAttack==CombatRequest.Light)lights++;else lights=0;circle=-circle;circleUntil=o.time+.6;if(lights>=2)neutralUntil=circleUntil;}
 if((previous==CombatAction.Stagger || previous==CombatAction.Recoil) && o.selfAction==CombatAction.Free)circleUntil=o.time+.6;
 previous=o.selfAction;
 if(o.opponentGuard){if(guardSince<0)guardSince=o.time;}else guardSince=-1;
 if(o.selfAction!=CombatAction.Free || !o.visible)return intent;

 if(o.selfPosture>75)coolingPosture=true;else if(o.selfPosture<45)coolingPosture=false;
 if(o.opponentAction==CombatAction.Attack && o.opponentPhase==AttackPhase.Windup && o.distance<=o.opponentReach+.5f && (!defending || threat!=o.opponentAttackId)){
 threat=o.opponentAttackId;defending=true;
 // Estimate from an already observed public telegraph, never from future impact/results.
 // When posture is high, raise guard on seeing the warning instead of always
 // seeking another last-moment deflection. Slow warnings then produce ordinary
 // blocks and can fill posture, giving the player the earned stun opening.
 guardAt=coolingPosture?o.time:o.time+Mathf.Max(0,o.opponentWindup-o.opponentElapsed-.16f-.12f);
 guardUntil=o.time+Mathf.Max(.2f,o.opponentWindup-o.opponentElapsed)+.25;
 }
 if(defending && o.time<=guardUntil){if(o.time>=guardAt)intent.guardHeld=true;return intent;}
 defending=false;
 // High posture makes the enemy stop voluntary offense. It must not automatically
 // backpedal/dodge away at 75%, preventing the player from earning the full break.
 // The host still permits an escape after repeated health hits, outside full stun.
 if(coolingPosture)return intent;
 bool punish=o.opponentAction==CombatAction.Stagger || (o.opponentAction==CombatAction.Attack && o.opponentPhase==AttackPhase.Recovery);
 bool heavy=(guardSince>=0 && o.time-guardSince>=.5 || lights>=2) && o.heavyReady;
 float reach=heavy?2.4f:2f;
 if(o.time<neutralUntil && o.distance<=3){intent.localMove=new Vector2(circle,o.distance<1.8f?-.4f:o.distance>2.3f?.4f:0);return intent;}
 if(!punish && o.time<circleUntil && o.distance<=3){intent.localMove=new Vector2(circle,o.distance<1.8f?-.4f:o.distance>2.3f?.4f:0);return intent;}
 if(o.distance>reach){intent.localMove=Vector2.up;return intent;}
 intent.request=heavy?CombatRequest.Heavy:CombatRequest.Light;lastAttack=intent.request;

 return intent;
 }
}
}

