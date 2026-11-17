using UnityEngine;
namespace GameCombat {
[RequireComponent(typeof(Combatant),typeof(EnemyMotor))]
public partial class EnemyBrainHost : MonoBehaviour {
 [Header("Escape after repeated landed hits (Normal mode)")]
 [Min(2)] public int hitsBeforeEscape=3;
 [Min(.1f)] public float hitChainSeconds=2.5f;
 [Min(.1f)] public float escapeCooldown=3;
 public int RecentHits {get;private set;}
 public bool EscapePending {get;private set;}
 double lastHit=double.NegativeInfinity,escapeReady;
 public Vector3 ObservedTargetDirection {get;private set;}
 public bool showCombatDiagnostics;
 public string DecisionDiagnostic {get;private set;}
 public EnemyControlMode mode=EnemyControlMode.Normal;
 public EnemyControlMode ActiveMode {get;private set;}
 public uint Generation {get;private set;}=1;
 public string Diagnostic {get;private set;}="Normal";
 public string FallbackReason {get;private set;}="";
 public string ControllerStatus=>$"Selected: {mode} | Active: {ActiveMode}{(HasPendingSwitch?" (switch pending)":"")}\n{(agentPolicy is GameCombat.AgentContract.ICombatPolicyDiagnostics info?info.PolicyLabel:"No learned model assigned")}\n{(string.IsNullOrEmpty(FallbackReason)?Diagnostic:"Fallback: "+FallbackReason)}";
 public bool DebugOwned {get;private set;}
 public bool InCombat {get;private set;}
 public CombatIntent Intent {get;private set;}
 public bool HasPendingSwitch=>pending.HasValue || releaseDebug;
 public void ResetEpisodeState(){ClearEscape();escapeReady=heavyCooldownUntil=0;pending=null;releaseDebug=DebugOwned=InCombat=false;mlUnavailable=false;FallbackReason="";ClearOwner();ActiveMode=mode;Diagnostic=mode.ToString();}
 Combatant actor; enemy body; EnemyPerceptionNav perception; EnemyMotor motor;
 IEnemyCombatBrain brain; readonly EnemyObservationStream stream=new();
 EnemyControlMode? pending; bool releaseDebug, mlUnavailable; double nextDecision, heavyCooldownUntil; uint sequence;
 Vector3 debugPosition; Quaternion debugRotation;
 void Awake(){actor=GetComponent<Combatant>();body=GetComponent<enemy>();perception=GetComponent<EnemyPerceptionNav>();motor=GetComponent<EnemyMotor>();brain=new ScriptedCombatBrain();brain.Reset();}
 void OnEnable(){GetComponent<CombatActionRunner>().CollectIntent+=Collect;GetComponent<Combatant>().Result+=ObserveHit;GetComponent<CombatActionRunner>().Changed+=ActionChanged;}
 void OnDisable(){GetComponent<CombatActionRunner>().CollectIntent-=Collect;GetComponent<Combatant>().Result-=ObserveHit;GetComponent<CombatActionRunner>().Changed-=ActionChanged;ClearOwner();}
 public BrainSwitchResult SetMode(EnemyControlMode requested){
 mode=requested;
 if(requested==EnemyControlMode.ML && !AgentAvailable(out var reason)){FallbackToNormal(reason);return BrainSwitchResult.Unavailable;}
 mlUnavailable=false;FallbackReason="";
 if(actor.Runner.Action!=CombatAction.Free && !actor.Dead || DebugOwned){pending=requested;return BrainSwitchResult.Deferred;}
 Apply(requested);return BrainSwitchResult.Applied;
 }
 void FallbackToNormal(string reason){
 mlUnavailable=true;FallbackReason=reason;Diagnostic="Normal fallback: "+reason;
 // Invalidate outstanding callbacks and held movement now, but never cancel commitment.
 ClearOwner();
 if(actor.Runner.Action!=CombatAction.Free && !actor.Dead || DebugOwned)pending=EnemyControlMode.Normal;
 else Apply(EnemyControlMode.Normal);
 }
 [ContextMenu("Controller/Select Normal")] void SelectNormal()=>SetMode(EnemyControlMode.Normal);
 [ContextMenu("Controller/Select ML (evaluation only)")] void SelectEvaluation()=>SetMode(EnemyControlMode.ML);
 void ClearEscape(){EscapePending=false;RecentHits=0;lastHit=double.NegativeInfinity;}
 void ActionChanged(){body=body?body:GetComponent<enemy>();body?.SyncCombatState();if(actor.Dead){ClearEscape();Intent=default;}}
 public void EnterStunned(){ClearEscape();ClearOwner();Diagnostic="Stunned - no commands";}
 public void ExitStunned(){ClearEscape();ClearOwner();Diagnostic=ActiveMode.ToString();}
 void ObserveHit(CombatResult result){
 if(result.defenderId!=actor.Id || result.actualHealthDamage<=0)return;
 // Hits during a full break are the player's opening, never a banked escape.
 if(actor.Runner.Broken || body && body.IsStunned){ClearEscape();return;}
 if(actor.Runner.Clock-lastHit>hitChainSeconds)RecentHits=0;lastHit=actor.Runner.Clock;RecentHits++;if(RecentHits>=hitsBeforeEscape)EscapePending=true;
 }
 void ClearOwner(){ResetAgentOwner();Generation++;Intent=default;ObservedTargetDirection=Vector3.zero;sequence=0;actor.Runner.ClearControllerIntent();stream.Clear();brain?.Reset();nextDecision=0;motor.Clear();}
 void Apply(EnemyControlMode requested){ClearOwner();ActiveMode=requested;pending=null;brain=requested==EnemyControlMode.Test?new TestCombatBrain():new ScriptedCombatBrain();brain.Reset();Diagnostic=requested==EnemyControlMode.ML?"ML evaluation only":requested==EnemyControlMode.Test?"Test brain (not ML)":mlUnavailable?"Normal fallback: "+FallbackReason:"Normal";}
 // Future asynchronous adapters can only buffer commands for this owner/decision; never execute callbacks directly.
 public bool BufferIntent(uint generation,uint decision,CombatIntent intent){if(generation!=Generation || decision!=sequence || DebugOwned || !InCombat || ActiveMode!=EnemyControlMode.ML || actor.Dead || body && body.IsStunned)return false;return BufferAgentIntent(generation,decision,intent);}
 public bool AcquireDebug(){if(DebugOwned)return !releaseDebug;if(actor.Dead || actor.Runner.Action!=CombatAction.Free)return false;ClearOwner();DebugOwned=true;debugPosition=transform.position;debugRotation=transform.rotation;return true;}
 public void ReleaseDebug(){if(!DebugOwned)return;releaseDebug=true;Intent=default;actor.Runner.SetGuard(false);if(actor.Runner.Action==CombatAction.Free || actor.Dead)FinishDebug();}
 void FinishDebug(){DebugOwned=false;releaseDebug=false;if(!actor.Dead){motor.Teleport(debugPosition,debugRotation);}Apply(pending??(mlUnavailable && mode==EnemyControlMode.ML?EnemyControlMode.Normal:mode));}
 void Collect(){
 body=body?body:GetComponent<enemy>();perception=perception?perception:GetComponent<EnemyPerceptionNav>();
 body?.SyncCombatState();
 if(body && body.IsStunned){Intent=default;return;}
 if(actor.Dead){if(InCombat || Intent.request!=CombatRequest.None)ClearOwner();InCombat=false;pending=null;if(DebugOwned)FinishDebug();return;}
 if(releaseDebug && actor.Runner.Action==CombatAction.Free)FinishDebug();
 if(mlUnavailable && mode!=EnemyControlMode.ML){mlUnavailable=false;FallbackReason="";Diagnostic=ActiveMode.ToString();}
 if(mode!=ActiveMode && !pending.HasValue && !(mlUnavailable && mode==EnemyControlMode.ML))SetMode(mode);
 if(pending.HasValue && !DebugOwned && actor.Runner.Action==CombatAction.Free)Apply(pending.Value);
 if(DebugOwned)return;
 if(mlUnavailable && ActiveMode==EnemyControlMode.ML){Intent=default;return;}
 bool visible=perception && perception.CanSeePlayer();
 float distance=visible?perception.PlayerDistance:float.PositiveInfinity;
 bool combat=InCombat?(visible && distance<=7 || actor.Runner.Action!=CombatAction.Free):visible && distance<=5;
 if(combat!=InCombat){InCombat=combat;ClearOwner();body?.stateMachine?.TryChangeState(body.stateMachine.currentState,combat?EnemyState.EnemyStateType.combat:EnemyState.EnemyStateType.chase);}
 if(!InCombat)return;
 if(!visible){Intent=default;actor.Runner.SetGuard(false);motor.Face(Vector3.zero);}
 var current=new CombatObservation{time=actor.Runner.Clock,visible=visible,grounded=body && body.grounded,selfAction=actor.Runner.Action,selfPosture=actor.Posture,dodgeReady=actor.Runner.DodgeReady,heavyReady=actor.Runner.Clock>=heavyCooldownUntil,distance=distance};
 var target=visible?perception.Target:null;var opponent=target?target.GetComponentInParent<Combatant>():null;
 if(opponent && !opponent.Dead){var r=opponent.Runner;current.relativePosition=opponent.transform.position-transform.position;current.opponentAction=r.Action;current.opponentPhase=r.Phase;current.opponentGuard=r.Guarding;current.opponentAttackId=r.AttackId;current.opponentElapsed=r.Elapsed;current.opponentWindup=r.Attack?r.Attack.Windup:0;current.opponentReach=r.Attack?r.Attack.reach:0;var opponentBody=opponent.GetComponent<Rigidbody>();current.opponentVelocity=(opponentBody?opponentBody.linearVelocity:Vector3.zero)-(body && body.rb?body.rb.linearVelocity:Vector3.zero);current.opponentForward=opponent.facing.forward;current.opponentHealth=opponent.Health.Health/Mathf.Max(1,opponent.Health.maxHealth);current.opponentPosture=opponent.Posture/Mathf.Max(1,opponent.tuning.maxPosture);current.opponentProgress=r.Elapsed/Mathf.Max(.02f,r.Elapsed+r.Remaining);}else current.visible=false;
 visible=current.visible;var observed=stream.Sample(current);
 ObservedTargetDirection=observed.visible?Vector3.ProjectOnPlane(observed.relativePosition,Vector3.up).normalized:Vector3.zero;
 if(ActiveMode==EnemyControlMode.ML){CollectAgent(observed,visible,current.grounded);return;}
 if(actor.Runner.Clock+1e-6<nextDecision && !(EscapePending && actor.Runner.Action==CombatAction.Free && actor.Runner.Clock>=escapeReady))return;nextDecision=actor.Runner.Clock+.06;
 Intent=brain.Decide(observed);Intent=new CombatIntent{localMove=Intent.localMove,guardHeld=Intent.guardHeld,request=Intent.request,sequence=++sequence};
 if(!visible)Intent=default;
 Vector3 toward=observed.relativePosition;toward.y=0;motor.Face(toward);
 Vector3 escapeForward=ObservedTargetDirection.sqrMagnitude>.01f?ObservedTargetDirection:actor.facing.forward;
 Vector3 direction=Intent.request==CombatRequest.Dodge?-escapeForward:actor.facing.forward;
 // Own landed hits are observable. Never cancel an attack, recoil or full posture break for an escape.
 if(ActiveMode==EnemyControlMode.Normal && EscapePending && observed.visible && actor.Runner.Action==CombatAction.Free && actor.Runner.Clock>=escapeReady){
 bool found=false;
 if(current.grounded && current.dodgeReady){foreach(var local in new[]{Vector2.down,Vector2.left,Vector2.right}){var world=Vector3.Cross(Vector3.up,escapeForward)*local.x+escapeForward*local.y;if(motor.CanStep(world*8*actor.tuning.dodgeDuration)){Intent=new CombatIntent{sequence=sequence,request=CombatRequest.Dodge,localMove=local};direction=world;found=true;break;}}}
 if(!found){if(current.grounded && current.heavyReady && actor.heavy && current.selfPosture<75 && observed.distance<=actor.heavy.reach){Intent=new CombatIntent{sequence=sequence,request=CombatRequest.Heavy};direction=actor.facing.forward;}else Intent=new CombatIntent{sequence=sequence,guardHeld=true,localMove=Vector2.down};}
 }

 if(Intent.request==CombatRequest.Dodge && !motor.CanStep(direction*8*actor.tuning.dodgeDuration))Intent=new CombatIntent{sequence=sequence,guardHeld=true};
 actor.Runner.Submit(Intent,direction,current.grounded);
 DecisionDiagnostic=$"owner {(DebugOwned?"Debug":ActiveMode.ToString())} gen {Generation} cue {(observed.visible?$"{observed.opponentAction}/{observed.opponentPhase} {observed.opponentElapsed:F2}":"masked")}\nintent {Intent.localMove} guard {Intent.guardHeld} {Intent.request}: {(Intent.request==CombatRequest.None?"none":actor.Runner.LastRequest.ToString())}\n{(brain as ScriptedCombatBrain)?.Timers(actor.Runner.Clock)} hits {RecentHits} escape {EscapePending} posture {actor.Posture:F0} dodge {current.dodgeReady} heavy {current.heavyReady}\n{motor.MovementDiagnostic}";

 if(Intent.request==CombatRequest.Heavy && actor.Runner.LastRequest==RequestResult.Accepted)heavyCooldownUntil=actor.Runner.Clock+3;
 if(EscapePending && (Intent.request==CombatRequest.Dodge || Intent.request==CombatRequest.Heavy) && actor.Runner.LastRequest==RequestResult.Accepted){EscapePending=false;RecentHits=0;escapeReady=actor.Runner.Clock+escapeCooldown;}
 // Edge requests never persist between decisions; movement/guard do.
 Intent=new CombatIntent{localMove=Intent.localMove,guardHeld=Intent.guardHeld,sequence=sequence};
 }
 void OnGUI(){if(!showCombatDiagnostics || !Debug.isDebugBuild)return;var camera=Camera.main;if(!camera)return;var p=camera.WorldToScreenPoint(transform.position+Vector3.up*3);if(p.z>0)GUI.Box(new Rect(p.x-150,Screen.height-p.y-100,420,100),$"{Diagnostic} {actor.Runner.Action} lease {DebugOwned}\n{DecisionDiagnostic}");}
}
}





