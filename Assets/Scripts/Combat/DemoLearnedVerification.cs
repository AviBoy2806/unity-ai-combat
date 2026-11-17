using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using GameCombat.ML;

namespace GameCombat {
// Opt-in rendered evidence in the real GameScene. No object exists without the
// command flag. Enemy choices always come from the installed learned model.
public sealed class DemoLearnedVerification : MonoBehaviour {
 EnemyBrainHost host;EnemyLearningAgent policy;Combatant actor,target;Player player;Camera view;
 int ticks,shots,accepted,rejected,contacts,visibleFrames;float damage,startDistance,minDistance=float.PositiveInfinity;RenderTexture capture;Texture2D pixels;
 double deadline;bool done;string folder;
 [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
 static void Boot(){if(Array.IndexOf(Environment.GetCommandLineArgs(),"--demo-learned-verification")>=0)new GameObject("Opt-in actual learned verification").AddComponent<DemoLearnedVerification>();}
 string Arg(string name,string fallback){var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,name);return i>=0 && i+1<a.Length?a[i+1]:fallback;}
 void Start(){try{
  folder=Arg("--verification-output","Logs/DemoPolicy20261117/rendered");Directory.CreateDirectory(folder);
  deadline=Time.realtimeSinceStartupAsDouble+25;Application.runInBackground=true;
  Time.timeScale=1;Time.fixedDeltaTime=.02f;Time.captureDeltaTime=.02f;QualitySettings.vSyncCount=0;Application.targetFrameRate=50;
  host=FindFirstObjectByType<EnemyBrainHost>();player=FindFirstObjectByType<Player>();actor=host.GetComponent<Combatant>();target=player.GetComponent<Combatant>();policy=host.agentPolicy as EnemyLearningAgent;
  if(host.mode!=EnemyControlMode.Normal || host.ActiveMode!=EnemyControlMode.Normal)throw new Exception("Saved default was not Normal");
  player.GetComponent<UnityEngine.InputSystem.PlayerInput>().enabled=false;player.inputComponent.ResetEpisodeState();
  Vector3 direction=Vector3.ProjectOnPlane(player.model.forward,Vector3.up).normalized;
  if(direction.sqrMagnitude<.1f)direction=Vector3.forward;
  Vector3 start=default,targetStart=default;bool found=false;
  var triangles=NavMesh.CalculateTriangulation();Debug.Log("RENDER_FIXTURE original player="+player.transform.position+" enemy="+actor.transform.position+" navVertices="+triangles.vertices.Length+" onNav="+host.GetComponent<NavMeshAgent>().isOnNavMesh);
  if(triangles.vertices.Length==0)throw new Exception("GameScene has no loaded NavMesh");
  var locations=new List<Vector3>{player.transform.position,actor.transform.position};
  int stride=Mathf.Max(3,triangles.indices.Length/64/3*3);
  for(int i=0;i+2<triangles.indices.Length;i+=stride)locations.Add((triangles.vertices[triangles.indices[i]]+triangles.vertices[triangles.indices[i+1]]+triangles.vertices[triangles.indices[i+2]])/3);
  foreach(var location in locations){
   if(!NavMesh.SamplePosition(location,out var center,20,NavMesh.AllAreas))continue;
   var proposedTarget=center.position+Vector3.up*(player.transform.position.y-player.playerCollider.bounds.min.y+.02f);
   foreach(float angle in new[]{0f,90f,-90f,180f}){
    var axis=Quaternion.Euler(0,angle,0)*direction;
    if(!NavMesh.SamplePosition(center.position+axis*3.4f,out var hit,1,NavMesh.AllAreas))continue;
    var candidate=hit.position+Vector3.up*(actor.transform.position.y-actor.GetComponent<Collider>().bounds.min.y+.02f);
    float distance=Vector3.ProjectOnPlane(candidate-proposedTarget,Vector3.up).magnitude;
    if(distance<3 || distance>4.5f || Physics.Linecast(candidate+Vector3.up*.8f,proposedTarget+Vector3.up*.8f,actor.tuning.obstacles,QueryTriggerInteraction.Ignore))continue;
    start=candidate;targetStart=proposedTarget;found=true;break;
   }
   if(found)break;
  }
  if(!found)throw new Exception("No clear, beyond-contact-range starting pair on real GameScene navigation");
  // The target is placed once as part of the explicit test fixture as well.
  player.rb.position=targetStart;player.transform.position=targetStart;player.rb.linearVelocity=Vector3.zero;Physics.SyncTransforms();
  // One starting placement only. No range correction or enemy movement writes thereafter.
  host.GetComponent<EnemyMotor>().Teleport(start,Quaternion.LookRotation(Quaternion.Euler(0,90,0)*(-direction)));
  host.GetComponent<NavMeshAgent>().Warp(start);startDistance=Vector3.Distance(start,target.transform.position);
  Cursor.lockState=CursorLockMode.None;Cursor.visible=true;foreach(var c in FindObjectsByType<Camera>(FindObjectsSortMode.None))c.enabled=false;
  view=new GameObject("Verification-only camera").AddComponent<Camera>();view.fieldOfView=55;view.nearClipPlane=.1f;view.farClipPlane=200;
  // A hidden standalone window may skip the backbuffer. Explicitly render the
  // real scene into a texture; no desktop capture or gameplay changes needed.
  capture=new RenderTexture(1280,720,24);capture.Create();pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);view.targetTexture=capture;view.enabled=false;
  if(host.SetMode(EnemyControlMode.ML)!=BrainSwitchResult.Applied)throw new Exception("Learned candidate unavailable: "+host.ControllerStatus);
  if(!policy || policy.testHeuristic || policy.trainingMode || !policy.runtimeEvaluationOnly || Unity.MLAgents.Academy.Instance.IsCommunicatorOn)throw new Exception("Not offline learned inference");
  actor.Runner.RequestEvaluated+=Request;actor.Result+=Result;CombatWorld.AfterStep+=Sample;
  Debug.Log("LEARNED_RENDER_START model="+policy.modelSha256+" startDistance="+startDistance+"; actual learned model, scripted target movement, diagnostic camera");
 }catch(Exception ex){Finish(false,ex.Message);}}
 void Request(CombatRequest request,RequestResult result){if(result==RequestResult.Accepted)accepted++;else rejected++;}
 void Result(CombatResult result){if(result.attackerId==actor.Id && result.defenderId==target.Id && result.actualHealthDamage>0){damage+=result.actualHealthDamage;contacts++;}}
 void Sample(){if(done)return;try{
  ticks++;minDistance=Mathf.Min(minDistance,Vector3.Distance(actor.transform.position,target.transform.position));
  if(host.ActiveMode!=EnemyControlMode.ML || !string.IsNullOrEmpty(host.FallbackReason))throw new Exception("Hidden fallback: "+host.ControllerStatus);
  if(Unity.MLAgents.Academy.Instance.IsCommunicatorOn)throw new Exception("Communicator enabled");
  // The player target uses its existing input/motor. Enemy actions are never overridden.
  player.input.movement=ticks>=125 && ticks<145?Vector2.right:ticks>=235 && ticks<255?Vector2.left:Vector2.zero;
  if(ticks>=500)Finish(damage>0 && contacts>0 && accepted>0 && minDistance<2.4f && visibleFrames>50,"500-tick bounded rendered sample");
 }catch(Exception ex){Finish(false,ex.Message);}}
 void Update(){if(!done && Time.realtimeSinceStartupAsDouble>deadline)Finish(false,"25-second wall deadline");}
 void LateUpdate(){if(done || !view || !target || !actor)return;
  Vector3 center=(actor.transform.position+target.transform.position)*.5f+Vector3.up;
  view.transform.SetPositionAndRotation(center+new Vector3(4.5f,2.5f,-5.5f),Quaternion.LookRotation(center-(center+new Vector3(4.5f,2.5f,-5.5f))));
  if(ticks>0 && ticks%5==0){
   view.Render();var previous=RenderTexture.active;RenderTexture.active=capture;
   pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();RenderTexture.active=previous;
   var colors=pixels.GetPixels32();int lit=0;for(int i=0;i<colors.Length;i+=100)if(colors[i].r+colors[i].g+colors[i].b>24)lit++;
   if(lit>100)visibleFrames++;File.WriteAllBytes(Path.GetFullPath(folder+"/frame-"+(shots++).ToString("D4")+".png"),pixels.EncodeToPNG());
  }
 }
 void OnGUI(){if(!policy)return;GUI.Box(new Rect(12,12,650,114),"ACTUAL LEARNED MODEL — EVALUATION ONLY\nModel "+policy.modelSha256.Substring(0,12)+" | Selected "+host.mode+" / Active "+host.ActiveMode+"\n"+actor.Runner.Action+" / "+actor.Runner.Phase+" | damage "+damage.ToString("F0")+" | target health "+target.Health.Health.ToString("F0")+"\nTarget movement scripted; camera diagnostic. No enemy action override.");}
 void Finish(bool passed,string reason){if(done)return;done=true;CombatWorld.AfterStep-=Sample;
  if(!string.IsNullOrEmpty(folder)){File.WriteAllText(folder+"/verification.json",JsonUtility.ToJson(new Record{passed=passed,reason=reason,ticks=ticks,screenshots=shots,visibleFrames=visibleFrames,accepted=accepted,rejected=rejected,contacts=contacts,damage=damage,startDistance=startDistance,minDistance=minDistance,modelSha256=policy?policy.modelSha256:"",fallback=host?host.FallbackReason:"",decisions=policy?(int)policy.DecisionCount:0,communicator=Unity.MLAgents.Academy.Instance.IsCommunicatorOn,sampledInference=policy && !policy.GetComponent<Unity.MLAgents.Policies.BehaviorParameters>().DeterministicInference},true));}
  Debug.Log((passed?"LEARNED_RENDER_VERIFICATION_PASS ":"LEARNED_RENDER_VERIFICATION_FAIL ")+reason);Application.Quit(passed?0:1);
 }
 [Serializable] class Record {public bool passed,communicator,sampledInference;public string reason,modelSha256,fallback;public int ticks,screenshots,visibleFrames,accepted,rejected,contacts,decisions;public float damage,startDistance,minDistance;}
 void OnDestroy(){CombatWorld.AfterStep-=Sample;if(capture){capture.Release();Destroy(capture);}if(pixels)Destroy(pixels);}
}
}
