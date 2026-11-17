using UnityEngine;
using GameCombat;
[System.Serializable]
public class EnemyStateMachine {
 public EnemyState[] states={new EnemyDeadState(),new EnemyIdleState(),new EnemyWanderState(),new EnemyChaseState(),new EnemyCombatState(),new EnemyStunnedState()};
 public EnemyState currentState;
 readonly enemy context;
 bool transitioning; EnemyState.EnemyStateType? pending;
 public EnemyStateMachine(enemy context){this.context=context;foreach(var state in states){state.context=context;state.Init();}Transition(EnemyState.EnemyStateType.idle);}
 public void SyncCombatState(){
 var actor=context.GetComponent<Combatant>();if(!actor || !actor.Health)return;
 if(actor.Dead){ForceChangeState(EnemyState.EnemyStateType.dead);return;}
 if(actor.Runner.Broken){ForceChangeState(EnemyState.EnemyStateType.stunned);return;}
 if(currentState is EnemyStunnedState){var nav=context.GetComponent<EnemyPerceptionNav>();ForceChangeState(nav && nav.CanSeePlayer() && nav.CanEngagePlayer() && nav.PlayerDistance<=7?EnemyState.EnemyStateType.combat:nav && nav.HasPlayer && !float.IsNegativeInfinity(nav.LastSeenTime)?EnemyState.EnemyStateType.chase:EnemyState.EnemyStateType.idle);}
 }
 public void Update(){SyncCombatState();currentState?.Update();}
 public void FixedUpdate(){SyncCombatState();currentState?.FixedUpdate();}
 bool Allowed(EnemyState.EnemyStateType type){
 var actor=context.GetComponent<Combatant>();
 if(context.isDead || actor && actor.Health && actor.Dead || currentState is EnemyDeadState)return type==EnemyState.EnemyStateType.dead;
 if(actor && actor.Runner && actor.Runner.Broken)return type==EnemyState.EnemyStateType.stunned;
 return true;
 }
 public void TryChangeState(EnemyState caller,EnemyState.EnemyStateType type){if(transitioning || caller!=currentState || !Allowed(type) || !currentState.canExit || !states[(int)type].canEnter)return;Transition(type);}
 public void ForceChangeState(EnemyState.EnemyStateType type){if(!Allowed(type) || currentState==states[(int)type])return;if(transitioning){if(!pending.HasValue || type==EnemyState.EnemyStateType.dead || type==EnemyState.EnemyStateType.stunned && pending!=EnemyState.EnemyStateType.dead)pending=type;return;}Transition(type);}
 void Transition(EnemyState.EnemyStateType type){
 if(currentState==states[(int)type])return;transitioning=true;
 var previous=currentState;previous?.Exit();currentState=states[(int)type];context.SetStateName(currentState.GetType().Name);currentState.Enter();
 if(!context.standaloneControl)Debug.Log($"[ENEMY STATE TRANSITION] {previous?.GetType().Name??"NULL"} -> {currentState.GetType().Name}");
 transitioning=false;if(pending.HasValue){var next=pending.Value;pending=null;ForceChangeState(next);}
 }
}
