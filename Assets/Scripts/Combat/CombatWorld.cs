using System.Collections.Generic;
using UnityEngine;
namespace GameCombat {
[DefaultExecutionOrder(-100)]
public class CombatWorld : MonoBehaviour {
 static CombatWorld instance; static readonly List<Combatant> actors=new();
 readonly List<ResolvedContact> contacts=new();
 readonly Dictionary<int,Vector3> previous=new();
 public static event System.Action BeforeStep,AfterStep;
 public static void ForgetMotion(Combatant actor){if(instance)instance.previous.Remove(actor.Id);}
 public static void Register(Combatant actor){if(!actors.Contains(actor))actors.Add(actor);if(!instance && Application.isPlaying)instance=new GameObject("CombatWorld").AddComponent<CombatWorld>();}
 public static void Unregister(Combatant actor){actors.Remove(actor);}
 [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void ResetStatics(){actors.Clear();instance=null;BeforeStep=null;AfterStep=null;}
 void FixedUpdate(){Step(Time.fixedDeltaTime);}
 public void Step(float dt){
 BeforeStep?.Invoke();actors.RemoveAll(a=>!a);var active=actors.FindAll(a=>a.isActiveAndEnabled && !a.ContactsSuppressed).ToArray();
 foreach(var a in active)a.Runner.Collect();
 foreach(var a in active)a.Runner.Tick(dt);
 foreach(var a in active)a.Runner.Move();
 Physics.SyncTransforms();contacts.Clear();
 foreach(var a in active)if(!a.Dead && a.Runner.SamplesActive)Gather(a);
 CombatResolver.ApplyBatch(contacts,Time.fixedTimeAsDouble);
 foreach(var a in active)previous[a.Id]=a.transform.position;
 AfterStep?.Invoke();
 }
 void Gather(Combatant a){
 var r=a.Runner;var attack=r.Attack;
 // Subdivide the authored sweep and actor displacement. Allocating overlap queries cannot overflow a fixed buffer.
 Vector3 origin=a.transform.position;
 Vector3 old=previous.TryGetValue(a.Id,out var oldPosition)?oldPosition:origin;
 float from=Mathf.Max(r.PreviousElapsed,attack.Windup),to=Mathf.Min(r.Elapsed,attack.Windup+attack.Active);
 int steps=Mathf.Max(1,Mathf.Max(Mathf.CeilToInt((to-from)/.005f),Mathf.CeilToInt(Vector3.Distance(old,origin)/.1f)));
 for(int i=0;i<=steps;i++){
 float sampleTime=Mathf.Lerp(from,to,(float)i/steps);
 float fraction=Mathf.InverseLerp(r.PreviousElapsed,r.Elapsed,sampleTime);
 Vector3 center=Vector3.Lerp(old,origin,fraction)+a.facing.rotation*attack.LocalCenter(sampleTime);
 foreach(var col in Physics.OverlapBox(center,attack.halfExtents,a.facing.rotation,~0,QueryTriggerInteraction.Collide)){
 var hurt=col.GetComponent<Hurtbox>();if(!hurt || !hurt.owner)continue;var d=hurt.owner;
 if(d==a || d.ContactsSuppressed || d.Dead || d.faction==a.faction || d.arenaId!=a.arenaId)continue;
 Vector3 target=col.ClosestPoint(center);
 if(Physics.Linecast(origin+Vector3.up*.8f,target,a.tuning.obstacles,QueryTriggerInteraction.Ignore))continue;
 if(!r.MarkHit(d))continue;
 contacts.Add(CombatResolver.Evaluate(a,d,attack,origin,sampleTime));
 }
 }
 }
}
}
