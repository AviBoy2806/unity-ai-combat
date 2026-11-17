using UnityEngine;
namespace GameCombat {
[RequireComponent(typeof(Combatant))]
public class PlayerCombatAdapter : MonoBehaviour {
 readonly AttackRootMotionBuffer attackMotion=new();
 public void AccumulateAttackRootMotion(Vector3 delta){if(Actor.Runner.Action==CombatAction.Attack)attackMotion.Add(delta);}
 public Combatant Actor {get;private set;} Player player;uint sequence; CombatRequest buffered;double bufferUntil;
 public string InputDiagnostic {get;private set;}
 public void ResetEpisodeState(){attackMotion.Clear();buffered=CombatRequest.None;bufferUntil=0;sequence=0;InputDiagnostic=null;}
 void Awake(){Actor=GetComponent<Combatant>();player=GetComponent<Player>();}
 void OnEnable(){var r=GetComponent<CombatActionRunner>();r.CollectIntent+=Collect;r.ApplyMovement+=Move;r.Changed+=Changed;}
 void OnDisable(){buffered=CombatRequest.None;var r=GetComponent<CombatActionRunner>();r.CollectIntent-=Collect;r.ApplyMovement-=Move;r.Changed-=Changed;}
 void Collect(){if(!player.inputComponent)return;var input=player.inputComponent;
 var request=input.ConsumeCombat(); bool guard=input.ConsumeGuard();
 var runner=Actor.Runner;
 if(player.AttackFacingLocked)player.input.jumpTriggered=false;
 if(Actor.Dead || runner.Action==CombatAction.Stagger || runner.Action==CombatAction.Recoil){buffered=CombatRequest.None;input.ClearCombat();return;}
 if(request!=CombatRequest.None)buffered=CombatRequest.None;
 else if(buffered!=CombatRequest.None){if(Time.realtimeSinceStartupAsDouble>bufferUntil)buffered=CombatRequest.None;else request=buffered;}

 Vector3 direction=player.GetCameraRelativeMove();if(direction.sqrMagnitude<.01f)direction=-player.GetModelDirection();
 if(request!=CombatRequest.Dodge)direction=player.GetModelDirection();
 if(runner.Action==CombatAction.Free && player.AttackFacingLocked && (request==CombatRequest.Light || request==CombatRequest.Heavy)){
 if(buffered==CombatRequest.None){buffered=request;bufferUntil=Time.realtimeSinceStartupAsDouble+.12;}
 InputDiagnostic=$"{request}: waiting for previous attack pose (max 0.12s)";request=CombatRequest.None;
 }
 Actor.Runner.Submit(new CombatIntent {sequence=++sequence,guardHeld=guard,request=request},direction,player.IsGrounded());
 if(request!=CombatRequest.None){var result=runner.LastRequest;InputDiagnostic=$"{request}: {result}";
 bool transient=(request==CombatRequest.Light || request==CombatRequest.Heavy) && (result==RequestResult.Busy && runner.Action==CombatAction.Dodge && runner.Remaining<=.12f || result==RequestResult.Airborne && runner.Action==CombatAction.Free);
 if(result==RequestResult.Accepted || !transient)buffered=CombatRequest.None;
 else if(buffered==CombatRequest.None){buffered=request;bufferUntil=Time.realtimeSinceStartupAsDouble+.12;}
 }


 }
 void OnApplicationFocus(bool focus){if(!focus)buffered=CombatRequest.None;}
 void Changed(){attackMotion.Clear();player.EnforceAttackFacing();if(Actor.Runner.Action==CombatAction.Stagger || Actor.Runner.Action==CombatAction.Recoil || Actor.Dead){buffered=CombatRequest.None;player.inputComponent.ClearCombat();player.SetVelocity(x:0,z:0);}if(Actor.Runner.Action==CombatAction.Free)player.SetVelocity(x:0,z:0);if(Actor.Runner.Action==CombatAction.Attack && Actor.Runner.Attack==Actor.heavy)player.rb.AddForce(Vector3.up*Mathf.Min(player.heavyAttackForce,Actor.heavy.leapImpulse),ForceMode.Impulse);}
 void Move(){var r=Actor.Runner;if(!r.OwnsMovement){if(player.AttackFacingLocked)player.SetVelocity(x:0,z:0);return;}
 Vector3 velocity=Vector3.zero;
 if(r.Action==CombatAction.Dodge)velocity=r.Direction*player.dodgeSpeed;
 if(r.Action==CombatAction.Attack){bool animated=player.animator && player.animator.enabled && player.animator.runtimeAnimatorController && player.GetComponentInChildren<RootMotionHandler>()?.isActiveAndEnabled==true;
 velocity=animated?attackMotion.Consume(Time.fixedDeltaTime):Actor.facing.forward*r.Attack.forwardSpeed.Evaluate(r.Elapsed/r.Attack.Duration);}
 if(r.Guarding){player.input.jumpTriggered=false;velocity=player.AttackFacingLocked?Vector3.zero:player.GetCameraRelativeMove()*player.walkSpeed*Actor.tuning.guardMove;player.RotateModel(player.lockOn && player.lockOn.enabled?player.GetCameraForward():player.GetCameraRelativeMove());}
 if(velocity.sqrMagnitude>0){var delta=velocity*Time.fixedDeltaTime;Vector3 bottom=player.playerCollider.bounds.center;
 if(player.IsGrounded() && !Physics.Raycast(new Vector3(player.playerCollider.bounds.center.x,player.playerCollider.bounds.min.y+.25f,player.playerCollider.bounds.center.z)+delta,Vector3.down,.8f,1<<7,QueryTriggerInteraction.Ignore))velocity=Vector3.zero;
 if(Physics.SphereCast(bottom,.35f,delta.normalized,out _,delta.magnitude+.05f,Actor.tuning.obstacles,QueryTriggerInteraction.Ignore))velocity=Vector3.zero;
 }
 player.SetVelocity(x:velocity.x,z:velocity.z);
 }
 public void SyncState(){var r=Actor.Runner;var current=player.stateMachine.currentState.GetStateType();var desired=current;
 if(Actor.Dead)desired=PlayerState.PlayerStateType.dead;
 else if(r.Action==CombatAction.Attack)desired=r.Attack==Actor.heavy?PlayerState.PlayerStateType.heavyAttack:PlayerState.PlayerStateType.attack;
 else if(r.Action==CombatAction.Dodge)desired=PlayerState.PlayerStateType.dodge;
 else if(r.Action==CombatAction.Stagger || r.Action==CombatAction.Recoil)desired=PlayerState.PlayerStateType.stagger;
 else if(r.Guarding)desired=PlayerState.PlayerStateType.guard;
 else if(current==PlayerState.PlayerStateType.attack || current==PlayerState.PlayerStateType.heavyAttack || current==PlayerState.PlayerStateType.dodge || current==PlayerState.PlayerStateType.guard || current==PlayerState.PlayerStateType.stagger)desired=player.IsGrounded()?PlayerState.PlayerStateType.idle:PlayerState.PlayerStateType.fall;
 if(current!=desired)player.stateMachine.ForceChangeState(desired);
 }
}
}
