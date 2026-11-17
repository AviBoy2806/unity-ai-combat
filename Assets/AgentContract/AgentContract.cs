using System;
namespace GameCombat.AgentContract {
 public sealed class DecisionFrame {
  public const int Version=1,Size=66,Stacks=3,MovementChoices=9,CombatChoices=5;
  public static readonly string[] ObservationNames={"self.health","self.posture","self.grounded","self.velocity.x","self.velocity.z","self.free","self.attack","self.dodge","self.stagger","self.recoil","self.dead","self.phase.none","self.phase.windup","self.phase.active","self.phase.recovery","self.progress","self.recovery","self.dodgeCooldown","self.guard","self.deflectionRearm","opponent.visible","opponent.position.x","opponent.position.z","opponent.velocity.x","opponent.velocity.z","opponent.forward.x","opponent.forward.z","opponent.health","opponent.posture","opponent.guard","opponent.free","opponent.attack","opponent.dodge","opponent.stagger","opponent.recoil","opponent.dead","opponent.phase.none","opponent.phase.windup","opponent.phase.active","opponent.phase.recovery","opponent.progress","geometry.0.distance","geometry.0.walkable","geometry.1.distance","geometry.1.walkable","geometry.2.distance","geometry.2.walkable","geometry.3.distance","geometry.3.walkable","geometry.4.distance","geometry.4.walkable","geometry.5.distance","geometry.5.walkable","geometry.6.distance","geometry.6.walkable","geometry.7.distance","geometry.7.walkable","previous.move.x","previous.move.z","previous.neutral","previous.guard","previous.light","previous.heavy","previous.dodge","previous.rejected","opponent.sightingAge"};
  public uint generation,sequence;
  public float[] observations;
  public bool[] movementAllowed,combatAllowed;
 }
 // This assembly has no dependency on gameplay implementations or ML packages.
 public interface ICombatPolicyDiagnostics { string PolicyLabel {get;} }
 public interface ICombatMovementMetadata { float CombatMoveSpeed {get;} }
 public interface ICombatPolicy {
  bool CanActivate(string tuningHash,out string reason);
  void Decide(DecisionFrame frame,Action<uint,uint,int,int> receive);
  void Deactivate();
 }
}
