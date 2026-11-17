using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using GameCombat.ML;
using GameCombat.AgentContract;
namespace GameCombat.Training {
[Serializable] public sealed class EpisodeRecord {
 public int arena,episode,seed,stage,decisions,rejected,illegal,maskViolations,guardPresses,wallAttackRequests,blocks,deflections,evades,opponentBlocks,opponentDeflections,duplicates,foreignEvents;
 public string family,outcome;public float seconds,dealt,received,reward,damageReward,defenseReward,shapingReward,livingReward,terminalReward,guardSeconds,cornerSeconds,retreatSeconds,startDistance,startAngle,movementFactor,telegraphFactor;public int wallMask;
 public int[] movement=new int[9],combat=new int[5];
 public bool evaluation,learnerOut,opponentOut,learnerDead,opponentDead;public string terminalReason;
 public int[] requestResults=new int[6];public int acceptedLight,acceptedHeavy,acceptedDodge,attackContacts,requestsBeyondAuthoredReach,facingSamples;
 public float minDistance,facingErrorSum;
}
// Sole episode owner. Agent callbacks enqueue resets; none performs a reset.
public sealed class CombatTrainingArena : MonoBehaviour {
 public Combatant learner,opponent;
 public EnemyLearningAgent agent;
 public int arenaId=1,stage=1,seed=23;
 public bool evaluation,hideRenderers=true;
 public int evaluationSeedOffset=100000;
 public OpponentFamily family;
 public int Episode {get;private set;}
 public int ResetCount {get;private set;}
 public bool ResetPending {get;private set;}=true;
 public bool Resetting {get;private set;}
 public TrainingRewards Rewards {get;private set;}
 public string BaselineTuningHash {get;private set;}
 public EpisodeRecord Record {get;private set;}
 public event Action<EpisodeRecord> EpisodeCompleted;
 public event Action ResetCompleted;
 public string outputPath="Logs/Phase5Environment/episodes.jsonl";
 public GameObject[] variedWalls;
 public Unity.AI.Navigation.NavMeshSurface surface;
 readonly Dictionary<AttackDefinition,AttackDefinition> baselineAttacks=new();
 float baseWalk,baseRun,baseEnemySpeed;
 float movementFactor=1,telegraphFactor=1;
 string learnerTuningBaseline,opponentTuningBaseline;
 EnemyBrainHost host;ScriptedPlayerOpponent scripted;System.Random random;
 readonly Dictionary<Transform,(Vector3,Quaternion)> poses=new();
 TrainingMotionEvaluation learnerMotion,opponentMotion;
 bool running,ending,hasPotential,transportReady=true;float potential;int ticks;long startDecisions;
 DecisionFrame lastFrame;int previousChoice;
 public void RequestReset(){ResetPending=true;transportReady=false;}
 void QueueAgentReset(){ResetPending=true;transportReady=true;}
 void OnEnable(){CombatWorld.BeforeStep+=BeforeStep;CombatWorld.AfterStep+=AfterStep;if(Rewards!=null){CombatTrainingSession.ApplyClock();PrepareMotion();AttachSignals();running=true;RequestReset();}}
 void OnDisable(){running=false;CombatWorld.BeforeStep-=BeforeStep;CombatWorld.AfterStep-=AfterStep;DetachSignals();learnerMotion?.Dispose();opponentMotion?.Dispose();}
 void DetachSignals(){if(learner){learner.Result-=Result;if(learner.Runner)learner.Runner.RequestEvaluated-=RequestEvaluated;}if(opponent)opponent.Result-=Result;if(agent){agent.EpisodeRequested-=QueueAgentReset;agent.DecisionRequested-=Decision;agent.ActionReturned-=ActionReturned;}}
 void AttachSignals(){DetachSignals();learner.Result+=Result;opponent.Result+=Result;agent.EpisodeRequested+=QueueAgentReset;agent.DecisionRequested+=Decision;agent.ActionReturned+=ActionReturned;learner.Runner.RequestEvaluated+=RequestEvaluated;}
 void PrepareMotion(){if(!TrainingMotionEvaluation.TryPrepare(learner,hideRenderers,out learnerMotion,out var reason))throw new InvalidOperationException(reason);if(!TrainingMotionEvaluation.TryPrepare(opponent,hideRenderers,out opponentMotion,out reason)){learnerMotion.Dispose();throw new InvalidOperationException(reason);}}
 void OnDestroy(){foreach(var copy in baselineAttacks.Keys)if(copy)Destroy(copy);}
 void Start(){
  // Academy's initial Python handshake may apply engine configuration after the
  // session Awake. Restore the measured clock before checking the motion gate.
  CombatTrainingSession.ApplyClock();
  if(!learner || !opponent || !agent || learner==opponent || learner.arenaId!=arenaId || opponent.arenaId!=arenaId)throw new InvalidOperationException("Invalid arena ownership");
  host=learner.GetComponent<EnemyBrainHost>();scripted=opponent.GetComponent<ScriptedPlayerOpponent>();scripted.target=learner;
  baseWalk=opponent.GetComponent<Player>().walkSpeed;baseRun=opponent.GetComponent<Player>().runSpeed;baseEnemySpeed=learner.GetComponent<EnemyMotor>().combatMoveSpeed;
  learnerTuningBaseline=JsonUtility.ToJson(learner.tuning);opponentTuningBaseline=JsonUtility.ToJson(opponent.tuning);
  // Per-instance copies only. Never modify the authored combat assets.
  foreach(var a in new[]{learner,opponent}){a.light=CopyAttack(a.light);a.heavy=CopyAttack(a.heavy);}
  BaselineTuningHash=host.AgentTuningHash;
  if(stage==4){var context=learner.gameObject.AddComponent<GeneralizationPolicyContext>();context.arena=this;host.agentPolicy=context;}
  if(arenaId==1){string path=Path.ChangeExtension(outputPath,"contract.json");string directory=Path.GetDirectoryName(path);if(!string.IsNullOrEmpty(directory))Directory.CreateDirectory(directory);File.WriteAllText(path,JsonUtility.ToJson(new ContractRecord{tuningHash=host.AgentTuningHash},true));}
  var perception=learner.GetComponent<EnemyPerceptionNav>();perception.requireInjectedTarget=true;perception.target=opponent.transform;
  learner.ContactsSuppressed=opponent.ContactsSuppressed=true;
  foreach(var actor in new[]{learner,opponent})foreach(var t in actor.GetComponentsInChildren<Transform>(true))if(t!=actor.transform)poses[t]=(t.localPosition,t.localRotation);
  PrepareMotion();Rewards=new TrainingRewards(learner,opponent);AttachSignals();
  running=true;
 }
 void BeforeStep(){if(running && ResetPending && !Resetting)ResetPair();}
 void ResetPair(){
  if(!running || Resetting)return;Resetting=true;ResetPending=false;ending=true;
  learner.ContactsSuppressed=opponent.ContactsSuppressed=true;
  foreach(var a in new[]{learner,opponent})a.GetComponent<Rigidbody>().detectCollisions=false;
  try {
   if(!transportReady){agent.ResetArenaTransport();transportReady=true;ResetPending=false;}
   // Purge callbacks before generation changes or action IDs restart.
   agent.ResetEpisodeState();host.ResetEpisodeState();
   foreach(var a in new[]{learner,opponent}){a.GetComponentInChildren<RootMotionHandler>()?.ResetEpisodeState();a.ResetEpisodeState();}
   foreach(var pair in poses)pair.Key.SetLocalPositionAndRotation(pair.Value.Item1,pair.Value.Item2);
   opponent.GetComponent<Player>().ResetEpisodeState();learner.GetComponent<enemy>().ResetEpisodeState();
   learner.GetComponent<EnemyPerceptionNav>().ResetEpisodeState();
   foreach(var a in new[]{learner,opponent}){var animator=a.GetComponentInChildren<Animator>();animator.Rebind();animator.Update(0);a.GetComponent<CombatPresentation>().ResetEpisodeState();CombatWorld.ForgetMotion(a);}
   int episodeSeed=evaluation?evaluationSeedOffset+(int)family*10000+Episode:seed+arenaId*1000003+Episode*7919;
   random=new System.Random(episodeSeed);
   Generalize();
   float distance=stage==4?Range(1.5f,6):Range(1.5f,3.5f);float angle=Range(-Mathf.PI,Mathf.PI);
   var axis=new Vector3(Mathf.Sin(angle),0,Mathf.Cos(angle));
   Place(learner,transform.position-axis*distance*.5f,Quaternion.LookRotation(axis));
   Place(opponent,transform.position+axis*distance*.5f,Quaternion.LookRotation(-axis));
   opponent.facing.rotation=Quaternion.Euler(0,Range(0,360),0);
   if(!evaluation){family=stage==1?OpponentFamily.Passive:stage==2?(random.Next(2)==0?OpponentFamily.PredictableLight:OpponentFamily.PredictableHeavy):(OpponentFamily)((int)OpponentFamily.Aggressive+random.Next(4));}
   scripted.family=family;scripted.ResetEpisodeState(Range(0,.8f));
   Rewards.Clear();ticks=0;startDecisions=agent.DecisionCount;hasPotential=false;lastFrame=null;previousChoice=0;
   int wallMask=0;for(int i=0;i<variedWalls.Length;i++)if(variedWalls[i].activeSelf)wallMask|=1<<i;
   Record=new EpisodeRecord{arena=arenaId,episode=Episode,seed=episodeSeed,stage=stage,family=family.ToString(),evaluation=evaluation,startDistance=distance,minDistance=distance,startAngle=angle,movementFactor=movementFactor,telegraphFactor=telegraphFactor,wallMask=wallMask};
   Physics.SyncTransforms();
   foreach(var a in new[]{learner,opponent}){var nav=a.GetComponent<NavMeshAgent>();if(nav && nav.enabled){if(!nav.Warp(a.transform.position))throw new InvalidOperationException("Reset failed NavMesh warp");nav.ResetPath();nav.velocity=Vector3.zero;nav.nextPosition=a.transform.position;}a.GetComponent<Rigidbody>().detectCollisions=true;}
   ResetCount++;Episode++;ResetCompleted?.Invoke();learner.ContactsSuppressed=opponent.ContactsSuppressed=false;ending=false;
  } catch {running=false;learner.ContactsSuppressed=opponent.ContactsSuppressed=true;throw;} finally {Resetting=false;}
 }
 float Range(float low,float high)=>low+(high-low)*(float)random.NextDouble();
 AttackDefinition CopyAttack(AttackDefinition source){if(!source)return null;var copy=Instantiate(source);baselineAttacks[copy]=source;copy.nextCombo=CopyAttack(source.nextCombo);return copy;}
 void Generalize(){
  movementFactor=stage==4?Range(.9f,1.1f):1;var player=opponent.GetComponent<Player>();player.walkSpeed=baseWalk*movementFactor;player.runSpeed=baseRun*movementFactor;learner.GetComponent<EnemyMotor>().combatMoveSpeed=baseEnemySpeed*movementFactor;
  telegraphFactor=stage==4?Range(.85f,1.15f):1;
  foreach(var pair in baselineAttacks){var a=pair.Key;var b=pair.Value;float shift=b.hitStartFrame*(telegraphFactor-1);a.hitStartFrame=b.hitStartFrame+shift;a.hitEndFrame=b.hitEndFrame+shift;a.actionEndFrame=b.actionEndFrame+shift;a.windup=b.windup*telegraphFactor;if(b.limitParryFrames){a.parryStartFrame=b.parryStartFrame+shift;a.parryEndFrame=b.parryEndFrame+shift;}}
  bool changed=false;if(variedWalls!=null)foreach(var wall in variedWalls){bool active=stage==4 && random.Next(2)==0;if(wall.activeSelf!=active){wall.SetActive(active);changed=true;}}
  if(changed && surface)surface.BuildNavMesh();
 }
 public bool ValidateGeneralization(out string reason){
  reason="Declared generalization envelope";
  var player=opponent.GetComponent<Player>();if(JsonUtility.ToJson(learner.tuning)!=learnerTuningBaseline || JsonUtility.ToJson(opponent.tuning)!=opponentTuningBaseline || Mathf.Abs(player.walkSpeed-baseWalk*movementFactor)>.0001f || Mathf.Abs(player.runSpeed-baseRun*movementFactor)>.0001f || Mathf.Abs(learner.GetComponent<EnemyMotor>().combatMoveSpeed-baseEnemySpeed*movementFactor)>.0001f)return false;
  if(movementFactor<.9f || movementFactor>1.1f || telegraphFactor<.85f || telegraphFactor>1.15f)return false;
  string Stable(AttackDefinition a){var json=JsonUtility.ToJson(a);json=System.Text.RegularExpressions.Regex.Replace(json,@"""instanceID"":-?\d+","\"instanceID\":0");return System.Text.RegularExpressions.Regex.Replace(json,@"""(windup|hitStartFrame|hitEndFrame|actionEndFrame|parryStartFrame|parryEndFrame)"":-?[0-9.]+([Ee][+-]?\d+)?","\"variant\":0");}
  foreach(var pair in baselineAttacks){var a=pair.Key;var b=pair.Value;float shift=b.limitParryFrames?b.hitStartFrame*(telegraphFactor-1):0;if(Stable(a)!=Stable(b) || Mathf.Abs(a.Windup-b.Windup*telegraphFactor)>.0001f || Mathf.Abs(a.Active-b.Active)>.0001f || Mathf.Abs(a.Recovery-b.Recovery)>.0001f || Mathf.Abs(a.parryStartFrame-b.parryStartFrame-shift)>.0001f || Mathf.Abs(a.parryEndFrame-b.parryEndFrame-shift)>.0001f){reason="Attack differs outside telegraph duration";return false;}}
  return true;
 }
 void Place(Combatant a,Vector3 ground,Quaternion rotation){
  var col=a.GetComponent<Collider>();var rb=a.GetComponent<Rigidbody>();float offset=a.transform.position.y-col.bounds.min.y;
  Vector3 point=ground+Vector3.up*(offset+.02f);rb.isKinematic=false;rb.position=point;rb.rotation=rotation;a.transform.SetPositionAndRotation(point,rotation);rb.linearVelocity=rb.angularVelocity=Vector3.zero;
 }
 void Result(CombatResult result){if(ending || Resetting || ResetPending)return;int duplicates=Rewards.Duplicates;AddReward(Rewards.Record(result));if(Rewards.Duplicates==duplicates && result.attackerId==learner.Id)Record.attackContacts++;}
 void AddReward(float value){if(value==0)return;Record.reward+=value;agent.AddReward(value);}
 void Decision(DecisionFrame frame){
  if(ending || Resetting || ResetPending)return;
  lastFrame=frame;
  if(frame.observations.Length!=66)throw new InvalidOperationException("Observation shape changed");foreach(float value in frame.observations)if(!float.IsFinite(value) || value < -1 || value > 1)throw new InvalidOperationException("Invalid observation");
  if(stage==1){float next=TrainingRewards.Potential(Vector3.Distance(learner.transform.position,opponent.transform.position),learner.light.reach);if(hasPotential)AddReward(Rewards.Shape(potential,next));potential=next;hasPotential=true;}
 }
 void ActionReturned(int move,int combat){if(ending || ResetPending)return;Record.movement[move]++;Record.combat[combat]++;if(lastFrame!=null && (!lastFrame.movementAllowed[move] || !lastFrame.combatAllowed[combat]))Record.maskViolations++;if(combat==1 && previousChoice!=1)Record.guardPresses++;previousChoice=combat;if((combat==2 || combat==3) && !learner.GetComponent<EnemyPerceptionNav>().CanSeePlayer())Record.wallAttackRequests++;}
 void RequestEvaluated(CombatRequest request,RequestResult result){if(ending || ResetPending)return;Record.requestResults[(int)result]++;if(result!=RequestResult.Accepted)Record.rejected++;else {if(request==CombatRequest.Light)Record.acceptedLight++;if(request==CombatRequest.Heavy)Record.acceptedHeavy++;if(request==CombatRequest.Dodge)Record.acceptedDodge++;}var attack=request==CombatRequest.Light?learner.light:request==CombatRequest.Heavy?learner.heavy:null;if(attack && Vector3.Distance(learner.transform.position,opponent.transform.position)>attack.reach)Record.requestsBeyondAuthoredReach++;if(learner.Runner.LastAcceptedIllegal)Record.illegal++;}
 bool Outside(Combatant a){var p=a.transform.position-transform.position;return Mathf.Abs(p.x)>8 || Mathf.Abs(p.z)>8 || p.y < -3 || p.y>12;}
 void AfterStep(){
  if(!running || ending || ResetPending)return;ticks++;Record.seconds=ticks*Time.fixedDeltaTime;
  Record.minDistance=Mathf.Min(Record.minDistance,Vector3.Distance(learner.transform.position,opponent.transform.position));if(host.ObservedTargetDirection.sqrMagnitude>.01f && learner.Runner.Action==CombatAction.Free){Record.facingErrorSum+=Vector3.Angle(Vector3.ProjectOnPlane(learner.facing.forward,Vector3.up),host.ObservedTargetDirection);Record.facingSamples++;}
  float cost=-.001f*Time.fixedDeltaTime;Record.livingReward+=cost;AddReward(cost);
  if(learner.Runner.Guarding)Record.guardSeconds+=Time.fixedDeltaTime;
  var local=learner.transform.position-transform.position;if(Mathf.Abs(local.x)>6 && Mathf.Abs(local.z)>6)Record.cornerSeconds+=Time.fixedDeltaTime;
  if(host.Intent.localMove.y<-.5f)Record.retreatSeconds+=Time.fixedDeltaTime;
  if(host.ActiveMode!=EnemyControlMode.ML && !learner.Dead)throw new InvalidOperationException("Training owner fell back: "+host.Diagnostic);
  string outcome=TrainingRewards.Terminal(learner.Dead,opponent.Dead,Outside(learner),Outside(opponent),ticks>=1500);
  if(outcome!=null)Finish(outcome);
 }
 public void Finish(string outcome){
  if(ending || Resetting || ResetPending)return;ending=true;
  if(stage==1 && hasPotential)AddReward(Rewards.Shape(potential,0));Record.terminalReward=TrainingRewards.TerminalReward(outcome);AddReward(Record.terminalReward);
  Record.outcome=outcome;Record.learnerDead=learner.Dead;Record.opponentDead=opponent.Dead;Record.learnerOut=Outside(learner);Record.opponentOut=Outside(opponent);Record.terminalReason=learner.Dead && opponent.Dead?"simultaneous_death":learner.Dead?"learner_death":opponent.Dead?"opponent_death":Record.learnerOut && Record.opponentOut?"both_out":Record.learnerOut?"learner_out":Record.opponentOut?"opponent_out":outcome=="timeout"?"timeout":"external";
  Record.decisions=(int)(agent.DecisionCount-startDecisions);Record.dealt=Rewards.Dealt;Record.received=Rewards.Received;Record.blocks=Rewards.Blocks;Record.deflections=Rewards.Deflections;Record.evades=Rewards.Evades;Record.opponentBlocks=Rewards.OpponentBlocks;Record.opponentDeflections=Rewards.OpponentDeflections;Record.duplicates=Rewards.Duplicates;Record.foreignEvents=Rewards.ForeignEvents;Record.damageReward=Rewards.DamageReward;Record.defenseReward=Rewards.DefenseReward;Record.shapingReward=Rewards.ShapingReward;
  Directory.CreateDirectory(Path.GetDirectoryName(outputPath));File.AppendAllText(outputPath,JsonUtility.ToJson(Record)+Environment.NewLine);
  agent.ResetEpisodeState();host.ResetEpisodeState();learner.ContactsSuppressed=opponent.ContactsSuppressed=true;
  EpisodeCompleted?.Invoke(Record);
  // EndEpisode calls OnEpisodeBegin synchronously. It can only enqueue one reset.
  ResetPending=true;agent.EndEpisode();
 }
}
[Serializable] public sealed class ContractRecord {public int contractVersion=1,observationSize=66,stacks=3;public string tuningHash;public float fixedDeltaTime=.02f,captureDeltaTime=.005f,timeScale=4;}
}
