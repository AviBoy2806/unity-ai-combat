using UnityEngine;
namespace GameCombat {
public struct ResolvedContact {
 public Combatant attacker,defender; public ulong id; public CombatOutcome outcome; public float actualPosture,actualDamage,damage,posture,recoilPosture,stagger; public bool recoil;
}
public static class CombatResolver {
 public static void ApplyDamage(HealthComponent health,float damage){if(health)health.ApplyResolvedDamage(damage);}
 // Evaluation has no side effects: every contact in a tick sees the same defense state.
 public static ResolvedContact Evaluate(Combatant a,Combatant d,AttackDefinition attack,Vector3 origin,float attackTime=-1) {
 var r=new ResolvedContact {attacker=a,defender=d,id=a.Runner.AttackId,outcome=CombatOutcome.Hit};
 if(d.Runner.Invulnerable){r.outcome=CombatOutcome.Evade;return r;}
 var toward=origin-d.facing.position;toward.y=0;
 bool front=toward.sqrMagnitude<.0001f || Vector3.Angle(d.facing.forward,toward)<=d.tuning.guardAngle*.5f;
 if(d.Runner.Guarding && front){
 if(d.Runner.Deflecting && attack.CanParryAt(attackTime>=0?attackTime:a.Runner.Elapsed)){r.outcome=CombatOutcome.Deflect;r.recoil=true;r.recoilPosture=d.tuning.deflectPosture;}
 else {r.outcome=CombatOutcome.Block;r.posture=attack.blockPosture;}return r; }
 r.damage=attack.damage;r.posture=attack.hitPosture;
 if(d.Runner.Action!=CombatAction.Stagger && !(d.Runner.Action==CombatAction.Attack && d.Runner.Attack.heavy && d.Runner.Phase==AttackPhase.Active))r.stagger=attack.heavy?d.tuning.heavyStagger:d.tuning.lightStagger;
 return r;
 }
 public static void ApplyBatch(System.Collections.Generic.List<ResolvedContact> contacts,double now) {
 contacts.RemoveAll(r=>!r.attacker || !r.defender || r.attacker.ContactsSuppressed || r.defender.ContactsSuppressed || r.attacker.arenaId!=r.defender.arenaId);
 // Health for the whole batch first; deaths dominate all posture and interruption requests.
 for(int i=0;i<contacts.Count;i++){var r=contacts[i];r.actualDamage=r.defender.Health.ApplyResolvedDamage(r.damage);contacts[i]=r;}
 for(int i=0;i<contacts.Count;i++){var r=contacts[i];float before=r.defender.Posture;r.defender.AddPosture(r.posture,r.defender.Runner.Clock);r.actualPosture=r.defender.Posture-before;r.attacker.AddPosture(r.recoilPosture,r.attacker.Runner.Clock);contacts[i]=r;}
 foreach(var r in contacts) {
 // AddPosture has already entered full break synchronously for either actor.
 // Lesser reactions cannot replace it; the runner rejects them while Broken.
 if(!r.defender.Dead && r.stagger>0)r.defender.Runner.Interrupt(r.stagger,CombatAction.Stagger);
 if(!r.attacker.Dead && r.recoil)r.attacker.Runner.Interrupt(r.attacker.tuning.recoil,CombatAction.Recoil);
 var result=new CombatResult {attackInstanceId=r.id,attackerId=r.attacker.Id,defenderId=r.defender.Id,outcome=r.posture>0 && r.defender.Posture>=r.defender.tuning.maxPosture && !r.defender.Dead?CombatOutcome.PostureBreak:r.outcome,actualHealthDamage=r.actualDamage,actualPostureChange=r.actualPosture};
 r.defender.Publish(result);r.attacker.Publish(result);
 }
 }
}
}
