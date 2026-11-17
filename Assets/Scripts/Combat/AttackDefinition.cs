using UnityEngine;
namespace GameCombat {
[CreateAssetMenu(menuName="Combat/Attack")]
public class AttackDefinition : ScriptableObject {
 public float windup=.2f, active=.16f, recovery=.34f, damage=10, hitPosture=10, blockPosture=20, reach=2;
 [Header("Timing from clip frames")]
 [Tooltip("Use source-clip frames for charge, hit and recovery. The runner converts these to seconds; animation events never deal damage.")]
 public bool useClipFrames;
 public AnimationClip timingClip;
 [Min(1)] public float sourceFrameRate=30;
 [Min(.01f)] public float authoredStateSpeed=1;
 [Min(0)] public float hitStartFrame=6;
 [Min(0)] public float hitEndFrame=11;
 [Min(1)] public float actionEndFrame=27;
 [Header("Which hit frames can be parried") ]
 public bool limitParryFrames;
 public float parryStartFrame,parryEndFrame=999;
 public bool CanParryAt(float seconds)=>!useClipFrames || !limitParryFrames || FrameAt(seconds)+.0001f>=parryStartFrame && FrameAt(seconds)<parryEndFrame;
 public float Windup => useClipFrames?FrameTime(hitStartFrame):windup;
 public float Active => useClipFrames?FrameTime(hitEndFrame-hitStartFrame):active;
 public float Recovery => useClipFrames?FrameTime(actionEndFrame-hitEndFrame):recovery;
 public float FrameTime(float frame)=>frame/(Mathf.Max(1,sourceFrameRate)*Mathf.Max(.01f,authoredStateSpeed));
 public float FrameAt(float seconds)=>seconds*sourceFrameRate*authoredStateSpeed;
 [Header("Hit box")]
 [Tooltip("Off keeps the box centered. Turn on only for a deliberately authored side sweep.")]
 public bool useSideSweep;
 public float sideOffset;
 public float leapImpulse;
 public bool heavy; public string animationState="Attack 1"; public float clipDuration=1;
 public Vector3 halfExtents=new Vector3(.65f,.8f,.25f);
 public AnimationCurve lateralTrajectory=AnimationCurve.Linear(0,-.35f,1,.35f);
 public AnimationCurve forwardSpeed=AnimationCurve.Linear(0,0,1,0);
 [Tooltip("One fresh click may queue the next light during this many seconds before action end. Never before Hit Start. It still waits for recovery to finish.")]
 [Min(0)] public float comboInputSeconds=.4f;
 public AttackDefinition nextCombo;
 [Tooltip("Disable to present the clip at its authored 1x speed while gameplay timers remain independent.")]
 public bool matchAnimationToAction = false;
 public float PlaybackSpeed => matchAnimationToAction ? clipDuration/Duration : 1f;
 public float Duration => Windup+Active+Recovery;
 public Vector3 LocalCenter(float time) => new Vector3(sideOffset+(useSideSweep?lateralTrajectory.Evaluate(Mathf.Clamp01((time-Windup)/Mathf.Max(.001f,Active))):0), .8f, reach*.65f);
 public bool Valid => Windup>=0 && Active>0 && Recovery>=0 && (!useClipFrames || hitEndFrame>hitStartFrame && actionEndFrame>=hitEndFrame && sourceFrameRate>0 && authoredStateSpeed>0) && Duration>0 && damage>=0 && hitPosture>=0 && blockPosture>=0 && float.IsFinite(Duration) && float.IsFinite(damage);
}
}
