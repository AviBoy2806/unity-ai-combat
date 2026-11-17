using UnityEngine;
namespace GameCombat {
public enum CombatRequest { None, Light, Heavy, Dodge }
public enum RequestResult { Accepted, Busy, Cooldown, Airborne, Dead, Invalid }
public enum CombatAction { Free, Attack, Dodge, Stagger, Recoil, Dead }
public enum AttackPhase { None, Windup, Active, Recovery }
public enum CombatOutcome { Hit, Block, Deflect, Evade, PostureBreak }
public struct CombatIntent { public Vector2 localMove; public bool guardHeld; public CombatRequest request; public uint sequence; }
public struct CombatResult {
 public ulong attackInstanceId; public int attackerId, defenderId;
 public CombatOutcome outcome; public float actualHealthDamage, actualPostureChange;
}
}
