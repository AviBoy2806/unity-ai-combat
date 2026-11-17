using UnityEngine;
using UnityEngine.AI;
namespace GameCombat {
[RequireComponent(typeof(Rigidbody))]
public class EnemyMotor : MonoBehaviour,GameCombat.AgentContract.ICombatMovementMetadata {
 public float CombatMoveSpeed=>combatMoveSpeed;
 public float combatMoveSpeed=2.5f;
 enemy body; Combatant actor; EnemyBrainHost host; NavMeshAgent agent; Vector3 facing; float jumpImpulse;
 bool stunned, savedKinematic, jumping;
 public enum MovementOwner { Navigation, Combat, Debug, Dead, Stunned }
 public MovementOwner Owner {get;private set;}
 public string MovementDiagnostic {get;private set;}
 public string StepBlockReason {get;private set;}
 NavMeshPath stepPath;
 void Awake(){body=GetComponent<enemy>();actor=GetComponent<Combatant>();agent=GetComponent<NavMeshAgent>();}
 void OnEnable(){GetComponent<CombatActionRunner>().ApplyMovement+=Move;GetComponent<CombatActionRunner>().Changed+=Changed;}
 void OnDisable(){GetComponent<CombatActionRunner>().ApplyMovement-=Move;GetComponent<CombatActionRunner>().Changed-=Changed;SetStunned(false);Clear();}
 void Changed(){var r=GetComponent<CombatActionRunner>();body=body?body:GetComponent<enemy>();body?.SyncCombatState();if(r.Action==CombatAction.Stagger || r.Action==CombatAction.Recoil || r.Action==CombatAction.Dead)Clear();}
 public void SetStunned(bool value){
 body=body?body:GetComponent<enemy>();if(!body || !body.rb || stunned==value)return;
 Clear();stunned=value;
 if(value){savedKinematic=body.rb.isKinematic;if(!savedKinematic){body.rb.linearVelocity=Vector3.zero;body.rb.angularVelocity=Vector3.zero;}body.rb.isKinematic=true;Owner=MovementOwner.Stunned;}
 else {body.rb.isKinematic=savedKinematic;if(!savedKinematic){body.rb.linearVelocity=Vector3.zero;body.rb.angularVelocity=Vector3.zero;}body.verticalVelocity=0;}
 }
 public void QueueJump(float impulse){if(!stunned && body && body.grounded && !actor.Dead)jumpImpulse=impulse;}
 public void Clear(){body=body?body:GetComponent<enemy>();jumpImpulse=0;if(body){body.horizontalVelocity=Vector3.zero;if(body.rb && !body.rb.isKinematic)body.rb.linearVelocity=new Vector3(0,body.rb.linearVelocity.y,0);}facing=Vector3.zero;if(agent && agent.enabled && agent.isOnNavMesh){agent.ResetPath();agent.velocity=Vector3.zero;agent.nextPosition=transform.position;}}
 public void Face(Vector3 direction){facing=direction;}
 public void Teleport(Vector3 position,Quaternion rotation){Clear();body.rb.position=position;body.rb.rotation=rotation;transform.SetPositionAndRotation(position,rotation);if(agent && agent.enabled && agent.isOnNavMesh)agent.nextPosition=position;}
 bool BlockStep(string reason){StepBlockReason=reason;return false;}
 public bool CanStep(Vector3 delta){StepBlockReason="";if(delta.sqrMagnitude<.00001f)return true;
 if(!agent || !agent.enabled || !agent.isOnNavMesh)return BlockStep("agent off mesh");
 var bounds=body.enemyCollider.bounds;
 var feet=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
 if(!NavMesh.SamplePosition(feet,out var start,.6f,agent.areaMask))return BlockStep("feet off mesh");
 if(!NavMesh.SamplePosition(feet+delta,out var end,.6f,agent.areaMask))return BlockStep("destination off mesh");
 // Use the agent's attached polygon. Re-mapping a capsule foot with the static
 // Raycast can select the wrong polygon at a sloped triangle seam.
 if(agent.Raycast(end.position,out _)){
 // Heightfield seams can report a hit at the ray origin on an otherwise
 // continuous ramp. Accept only a complete local path with no meaningful detour;
 // walls, disconnected surfaces and ledges still fail this and the physics casts.
 stepPath??=new NavMeshPath();
 if(!NavMesh.CalculatePath(start.position,end.position,agent.areaMask,stepPath) || stepPath.status!=NavMeshPathStatus.PathComplete)return BlockStep("disconnected mesh edge");
 var corners=stepPath.corners;float length=0;
 for(int i=1;i<corners.Length;i++)length+=Vector3.Distance(corners[i-1],corners[i]);
 if(length>delta.magnitude+.15f)return BlockStep("mesh edge requires detour");
 }
 if(Physics.SphereCast(bounds.center,Mathf.Min(bounds.extents.x,bounds.extents.z)*.9f,delta.normalized,out var wall,delta.magnitude+.05f,actor.tuning.obstacles,QueryTriggerInteraction.Ignore))return BlockStep("wall: "+wall.collider.name);
 return Physics.Raycast(new Vector3(bounds.center.x,bounds.min.y+.25f,bounds.center.z)+delta,Vector3.down,.8f,body.GroundMask,QueryTriggerInteraction.Ignore) || BlockStep("no ground ahead");
 }
 bool TryNavigationSlide(Vector3 wanted,out Vector3 velocity){
 velocity=Vector3.zero;if(wanted.sqrMagnitude<.00001f || !agent || !agent.isOnNavMesh || !agent.hasPath)return false;
 // desiredVelocity smooths corners in the agent's simulation. A rigidbody can
 // still be behind that corner at a different render/physics rate. Aim at the
 // actual path corner first, then try either unobstructed movement component.
 var corner=Vector3.ProjectOnPlane(agent.steeringTarget-body.rb.position,Vector3.up);
 if(corner.sqrMagnitude>.0025f){var toward=corner.normalized*wanted.magnitude;if(CanStep(toward*Time.fixedDeltaTime)){velocity=toward;return true;}}
 var x=new Vector3(wanted.x,0,0);var z=new Vector3(0,0,wanted.z);
 var first=Mathf.Abs(wanted.x)>Mathf.Abs(wanted.z)?x:z;var second=first==x?z:x;
 if(first.sqrMagnitude>.00001f && CanStep(first*Time.fixedDeltaTime)){velocity=first;return true;}
 if(second.sqrMagnitude>.00001f && CanStep(second*Time.fixedDeltaTime)){velocity=second;return true;}
 return false;
 }
 void Move(){body=body?body:GetComponent<enemy>();if(!body || !body.enabled)return;host=host?host:GetComponent<EnemyBrainHost>();var r=actor.Runner;body.SyncCombatState();
 if(body.IsStunned){Owner=MovementOwner.Stunned;Clear();MovementDiagnostic="Stunned: no movement";return;}
 var next=actor.Dead?MovementOwner.Dead:host && host.DebugOwned?MovementOwner.Debug:host && host.InCombat || r.OwnsMovement?MovementOwner.Combat:MovementOwner.Navigation;
 if(next!=Owner){Clear();Owner=next;}
 body.verticalVelocity=body.rb.linearVelocity.y;body.grounded=body.CheckGround();
 if(jumpImpulse>0){body.verticalVelocity=jumpImpulse;body.grounded=false;jumping=true;jumpImpulse=0;}
 Vector3 velocity=Vector3.zero;
 if(Owner==MovementOwner.Navigation){body.stateMachine?.FixedUpdate();velocity=body.horizontalVelocity;}
 if(Owner==MovementOwner.Combat){
 if(r.Action==CombatAction.Free){var move=host?host.Intent.localMove:Vector2.zero;var forward=host && host.ObservedTargetDirection.sqrMagnitude>.01f?host.ObservedTargetDirection:actor.facing.forward;var right=Vector3.Cross(Vector3.up,forward);velocity=Vector3.ClampMagnitude(right*move.x+forward*move.y,1)*combatMoveSpeed;if(r.Guarding)velocity*=actor.tuning.guardMove;}
 else if(r.Action==CombatAction.Attack)velocity=actor.facing.forward*r.Attack.forwardSpeed.Evaluate(r.Elapsed/r.Attack.Duration);
 else if(r.Action==CombatAction.Dodge)velocity=r.Direction*8;
 }
 MovementDiagnostic=$"{Owner} wanted {velocity.magnitude:F1}";if(Owner!=MovementOwner.Debug && !CanStep(velocity*Time.fixedDeltaTime)){
 string blocked=StepBlockReason;
 if(Owner==MovementOwner.Navigation && TryNavigationSlide(velocity,out var slide)){velocity=slide;facing=slide;MovementDiagnostic+=" following corner / sliding past "+blocked;}
 else{MovementDiagnostic+=" blocked: "+blocked;velocity=Vector3.zero;}
 }
 bool turn=r.Action==CombatAction.Free || r.Action==CombatAction.Attack && r.Phase==AttackPhase.Windup;
 if(turn && facing.sqrMagnitude>.001f && Owner!=MovementOwner.Dead && Owner!=MovementOwner.Debug)body.rb.MoveRotation(Quaternion.RotateTowards(body.rb.rotation,Quaternion.LookRotation(facing),180*Time.fixedDeltaTime));
 if(body.grounded && (!jumping || body.verticalVelocity<=0)){
 jumping=false;
 body.verticalVelocity=-2;
 // Follow the actual support plane, rather than pushing horizontally into it.
 var bounds=body.enemyCollider.bounds;
 if(velocity.sqrMagnitude>.00001f && Physics.Raycast(bounds.center,Vector3.down,out var support,bounds.extents.y+.3f,body.GroundMask,QueryTriggerInteraction.Ignore) && support.normal.y>.5f && support.normal.y<.999f){
 velocity=Vector3.ProjectOnPlane(velocity,support.normal);body.verticalVelocity=velocity.y-.2f;
 }
 }else{body.ApplyAirGravity();body.ClampFallVelocity();}
 body.horizontalVelocity=velocity;
 if(!body.rb.isKinematic)body.rb.linearVelocity=new Vector3(velocity.x,Owner==MovementOwner.Debug?0:body.verticalVelocity,velocity.z);
 if(agent && agent.enabled && agent.isOnNavMesh)agent.nextPosition=body.rb.position;
 }
}
}
