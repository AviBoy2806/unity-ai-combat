using System;
using System.Collections.Generic;
using UnityEngine;
namespace GameCombat.Training {
// One ledger per arena. Both actor callbacks may deliver the same resolver result.
public sealed class TrainingRewards {
 readonly HashSet<(ulong,int,int)> events=new();
 readonly Combatant learner,opponent;
 public int Duplicates {get;private set;}
 public int ForeignEvents {get;private set;}
 public float Dealt {get;private set;}
 public float Received {get;private set;}
 public int Blocks,Deflections,Evades,OpponentBlocks,OpponentDeflections;
 public float DefenseReward {get;private set;}
 public float DamageReward {get;private set;}
 public float ShapingReward {get;private set;}
 public TrainingRewards(Combatant learner,Combatant opponent){this.learner=learner;this.opponent=opponent;}
 public void Clear(){events.Clear();Duplicates=ForeignEvents=Blocks=Deflections=Evades=OpponentBlocks=OpponentDeflections=0;Dealt=Received=DefenseReward=DamageReward=ShapingReward=0;}
 public float Record(CombatResult result){
  if(!float.IsFinite(result.actualHealthDamage) || result.actualHealthDamage<0)throw new InvalidOperationException("Invalid resolver damage in reward event");
  bool attacks=result.attackerId==learner.Id && result.defenderId==opponent.Id;
  bool defends=result.attackerId==opponent.Id && result.defenderId==learner.Id;
  if(!attacks && !defends || learner.arenaId!=opponent.arenaId){ForeignEvents++;return 0;}
  if(!events.Add((result.attackInstanceId,result.attackerId,result.defenderId))){Duplicates++;return 0;}
  float reward;
  if(attacks){Dealt+=result.actualHealthDamage;reward=.25f*result.actualHealthDamage/opponent.Health.maxHealth;if(result.outcome==CombatOutcome.Block)OpponentBlocks++;if(result.outcome==CombatOutcome.Deflect)OpponentDeflections++;}
  else {Received+=result.actualHealthDamage;reward=-.25f*result.actualHealthDamage/learner.Health.maxHealth;if(result.outcome==CombatOutcome.Block)Blocks++;if(result.outcome==CombatOutcome.Evade)Evades++;}
  DamageReward+=reward;
  if(defends && result.outcome==CombatOutcome.Deflect){Deflections++;float bonus=Mathf.Min(.01f,Mathf.Max(0,.05f-DefenseReward));DefenseReward+=bonus;reward+=bonus;}
  return reward;
 }
 // Bounded potential [-1,0], applied only on stage-1 decision transitions.
 public static float Potential(float distance,float reach)=>-Mathf.Clamp01(Mathf.Abs(distance-reach)/10);
 public float Shape(float previous,float next){float reward=.05f*(.995f*next-previous);ShapingReward+=reward;return reward;}
 public static string Terminal(bool learnerDead,bool opponentDead,bool learnerOut,bool opponentOut,bool timeout){
  if(learnerDead && opponentDead || learnerOut && opponentOut)return "draw";
  if(learnerDead || learnerOut)return "loss";
  if(opponentDead || opponentOut)return "win";
  return timeout?"timeout":null;
 }
 public static float TerminalReward(string outcome)=>outcome=="win"?1:outcome=="loss"?-1:0;
}
}
