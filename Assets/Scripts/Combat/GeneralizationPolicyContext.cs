using UnityEngine;
using GameCombat.AgentContract;
namespace GameCombat.Training {
// Scoped to the isolated training arena. Runtime GameScene still checks the exact
// tuning hash. Evaluation checks the model's baseline AND the declared variants.
public sealed class GeneralizationPolicyContext : MonoBehaviour,ICombatPolicy {
 public CombatTrainingArena arena;
 string checkedHash;bool valid;
 public bool CanActivate(string tuningHash,out string reason){
  reason="Generalization context unavailable";
  if(!arena || arena.stage!=4)return false;
  if(checkedHash!=tuningHash){checkedHash=tuningHash;valid=arena.ValidateGeneralization(out reason);}
  if(!valid){reason="Generalization exceeded declared tuning envelope";return false;}
  return arena.agent.CanActivate(arena.BaselineTuningHash,out reason);
 }
 public void Decide(DecisionFrame frame,System.Action<uint,uint,int,int> receive)=>arena.agent.Decide(frame,receive);
 public void Deactivate(){checkedHash=null;valid=false;if(arena && arena.agent)arena.agent.Deactivate();}
}
}
