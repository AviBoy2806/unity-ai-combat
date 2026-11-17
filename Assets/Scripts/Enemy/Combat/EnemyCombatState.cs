using UnityEngine;
public class EnemyCombatState : EnemyState {
 public override void Enter(){context.GetComponent<EnemyPerceptionNav>()?.StopMoving();context.GetComponent<GameCombat.EnemyMotor>()?.Clear();}
 public override void Exit(){context.GetComponent<GameCombat.EnemyMotor>()?.Clear();}
 public override void Update(){SetDampedFloat(SpeedHash,context.horizontalVelocity.magnitude/5,.1f);}
}
