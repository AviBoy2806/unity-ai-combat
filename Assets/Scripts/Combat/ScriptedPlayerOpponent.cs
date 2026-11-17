using UnityEngine;
namespace GameCombat.Training {
public enum OpponentFamily { Passive,PredictableLight,PredictableHeavy,Aggressive,GuardCounter,DodgeRetreat,Balanced,AlwaysGuard,GuardTap,DodgeSpam,CornerCamp,Retreat,WallAttack,ComboSpam,DelayedAttack }
// Sends input through the existing player adapter. It never resolves hits or moves a body.
[DefaultExecutionOrder(-110)]
public sealed class ScriptedPlayerOpponent : MonoBehaviour {
 public Combatant target;
 public OpponentFamily family;
 Player player;Combatant actor;EnemyObservationStream stream=new();ScriptedCombatBrain balanced=new();
 double next,ready;bool guard;uint choices;
 public float StartDelay {get;private set;}
 void Awake(){player=GetComponent<Player>();actor=GetComponent<Combatant>();}
 void OnEnable(){CombatWorld.BeforeStep+=Tick;}
 void OnDisable(){CombatWorld.BeforeStep-=Tick;}
 public void ResetEpisodeState(float delay){stream.Clear();balanced.Reset();next=choices=0;ready=delay;StartDelay=delay;guard=false;player.inputComponent.ResetEpisodeState();}
 void Tick(){
  if(!target || actor.ContactsSuppressed || target.ContactsSuppressed || actor.arenaId!=target.arenaId || actor.Dead)return;
  var r=actor.Runner;var other=target.Runner;Vector3 delta=target.transform.position-transform.position;delta.y=0;
  bool visible=!target.Dead && delta.magnitude<=22 && !Physics.Linecast(transform.position+Vector3.up*.5f,target.transform.position+Vector3.up*.5f,actor.tuning.obstacles,QueryTriggerInteraction.Ignore);
  var observed=stream.Sample(new CombatObservation{time=r.Clock,visible=visible,relativePosition=delta,distance=delta.magnitude,selfAction=r.Action,selfPosture=actor.Posture,grounded=player.IsGrounded(),heavyReady=true,dodgeReady=r.DodgeReady,opponentAction=other.Action,opponentPhase=other.Phase,opponentGuard=other.Guarding,opponentAttackId=other.AttackId,opponentElapsed=other.Elapsed,opponentWindup=other.Attack?other.Attack.Windup:0,opponentReach=other.Attack?other.Attack.reach:0});
  if(r.Clock+1e-6<next)return;next=r.Clock+.06;
  CombatIntent intent=default;Vector3? directedMove=null;
  if(observed.visible && r.Clock>=ready){
   bool free=r.Action==CombatAction.Free;float distance=observed.distance;
   switch(family){
    case OpponentFamily.Passive:break;
    case OpponentFamily.Balanced:intent=balanced.Decide(observed);break;
    case OpponentFamily.AlwaysGuard:intent.guardHeld=true;break;
    case OpponentFamily.GuardTap:intent.guardHeld=(choices%4)<2;break;
    case OpponentFamily.GuardCounter:intent.guardHeld=observed.opponentAction!=CombatAction.Stagger && !(observed.opponentAction==CombatAction.Attack && observed.opponentPhase==AttackPhase.Recovery);if(!intent.guardHeld && free)intent.request=CombatRequest.Heavy;break;
    case OpponentFamily.DodgeSpam:intent.localMove=Vector2.left;if(free && r.DodgeReady)intent.request=CombatRequest.Dodge;break;
    case OpponentFamily.DodgeRetreat:intent.localMove=distance<3?Vector2.down:Vector2.up;if(free && observed.opponentAction==CombatAction.Attack && r.DodgeReady)intent.request=CombatRequest.Dodge;else if(free && distance<2)intent.request=CombatRequest.Light;break;
    case OpponentFamily.Retreat:intent.localMove=Vector2.down;break;
    case OpponentFamily.CornerCamp:var corner=transform.parent.TransformPoint(new Vector3(7,0,7));var toCorner=Vector3.ProjectOnPlane(corner-transform.position,Vector3.up);directedMove=toCorner.magnitude>.5f?toCorner.normalized:Vector3.zero;intent.guardHeld=true;break;
    default:
     if(distance>1.9f)intent.localMove=Vector2.up;
     else if(free || (family==OpponentFamily.ComboSpam || family==OpponentFamily.Aggressive) && r.ComboWindowOpen){intent.request=family==OpponentFamily.PredictableHeavy?CombatRequest.Heavy:CombatRequest.Light;ready=r.Clock+(family==OpponentFamily.DelayedAttack?2.0:family==OpponentFamily.PredictableLight || family==OpponentFamily.PredictableHeavy?.8:0);}
     break;
   }
  }
  if(family==OpponentFamily.WallAttack && r.Action==CombatAction.Free && r.Clock>=ready && Physics.Raycast(actor.facing.position+Vector3.up*.8f,actor.facing.forward,2,actor.tuning.obstacles,QueryTriggerInteraction.Ignore)){intent.request=CombatRequest.Light;ready=r.Clock+1.2;}
  choices++;
  // Training has no camera: root transform defines the input basis. Facing remains
  // under Player.RotateModel and its authored attack heading lock.
  Vector3 toward=observed.visible?Vector3.ProjectOnPlane(observed.relativePosition,Vector3.up).normalized:actor.facing.forward;
  Vector3 world=directedMove??Vector3.Cross(Vector3.up,toward)*intent.localMove.x+toward*intent.localMove.y;
  player.input.movement=new Vector2(Vector3.Dot(world,transform.right),Vector3.Dot(world,transform.forward));
  player.RotateModel(toward);
  if(intent.guardHeld!=guard){guard=intent.guardHeld;player.inputComponent.SetBlock(guard);}
  if(intent.request!=CombatRequest.None)player.inputComponent.QueueCombat(intent.request);
 }
}
}
