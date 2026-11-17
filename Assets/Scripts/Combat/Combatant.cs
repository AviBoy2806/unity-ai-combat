using System;
using UnityEngine;
namespace GameCombat {
[RequireComponent(typeof(HealthComponent),typeof(CombatActionRunner))]
public class Combatant : MonoBehaviour {
 public int faction; public int arenaId; public Transform facing; public CombatTuning tuning;
 public bool ContactsSuppressed {get;set;}
 // Called only by an arena while BOTH actors have contacts disabled.
 public void ResetEpisodeState(){if(!ContactsSuppressed)throw new InvalidOperationException("Reset requires contact suppression");Health.ResetEpisodeState();Posture=0;LastPostureTime=double.NegativeInfinity;Runner.ResetEpisodeState();}
 public AttackDefinition light, heavy;
 public HealthComponent Health {get; private set;} public CombatActionRunner Runner {get; private set;}
 [Tooltip("0 uses CombatTuning. Enemy prefab uses 3.5 seconds to give a clear opening.")]
 [Min(0)] public float postureBreakSeconds;
 public float BreakDuration => postureBreakSeconds>0?postureBreakSeconds:tuning.breakDuration;
 public float Posture {get; private set;} public double LastPostureTime {get; private set;} = double.NegativeInfinity;
 static int nextId; public int Id {get;} = System.Threading.Interlocked.Increment(ref nextId); public bool Dead => Health.Health<=0;
 public event Action<CombatResult> Result;
 void Awake() { Health=GetComponent<HealthComponent>(); Runner=GetComponent<CombatActionRunner>(); if(!facing) facing=transform;
 if(!tuning) tuning=ScriptableObject.CreateInstance<CombatTuning>(); Health.healthChanged+=HealthChanged; }
 void OnEnable() { CombatWorld.Register(this); }
 void OnDisable() { CombatWorld.Unregister(this); }
 void OnDestroy() { if(Health) Health.healthChanged-=HealthChanged; }
 void HealthChanged(float value) { if(value<=0) Runner.Die(); }
 public void AddPosture(float amount,double now) {
 if(Dead || amount<=0 || !float.IsFinite(amount)) return;
 Posture=Mathf.Min(tuning.maxPosture,Posture+amount); LastPostureTime=now;
 // Reaching full posture and entering the break are one operation. No controller or
 // caller may leave an actor at full posture waiting for a later contact to stun it.
 if(Posture>=tuning.maxPosture)Runner.Interrupt(BreakDuration,CombatAction.Stagger,true);
 }
 public void ResetBrokenPosture() { Posture=Mathf.Min(tuning.maxPosture,tuning.breakReset); }
 public void Recover(float dt,double now) { if(Dead || Runner.Action==CombatAction.Stagger || now-LastPostureTime<tuning.recoveryDelay) return;
 Posture=Mathf.Max(0,Posture-dt*(Runner.Guarding?tuning.guardRecoveryRate:tuning.recoveryRate)*(.5f+.5f*Health.Health/Health.maxHealth)); }
 [ContextMenu("Combat Debug/Light attack in 2 seconds")] void DebugLight(){GetComponent<CombatDebugControls>()?.Schedule(CombatRequest.Light);}
 [ContextMenu("Combat Debug/Heavy attack in 2 seconds")] void DebugHeavy(){GetComponent<CombatDebugControls>()?.Schedule(CombatRequest.Heavy);}
 public void Publish(CombatResult result) { Result?.Invoke(result); }
}
}

