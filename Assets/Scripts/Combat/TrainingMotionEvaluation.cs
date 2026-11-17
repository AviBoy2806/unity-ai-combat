using System;
using UnityEngine;
namespace GameCombat {
// Training keeps the authored animation graph and root-motion path alive even
// without a rendered view. This lease never changes timers or clip playback.
public sealed class TrainingMotionEvaluation : IDisposable {
 readonly Animator animator; readonly AnimatorCullingMode oldCulling;
 readonly Renderer[] renderers; readonly bool[] enabled;
 bool disposed;
 TrainingMotionEvaluation(Combatant actor,bool hide){animator=actor.GetComponentInChildren<Animator>();oldCulling=animator.cullingMode;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;renderers=actor.GetComponentsInChildren<Renderer>(true);enabled=new bool[renderers.Length];for(int i=0;i<renderers.Length;i++){enabled[i]=renderers[i].enabled;if(hide)renderers[i].enabled=false;}}
 public static bool TryPrepare(Combatant actor,bool hide,out TrainingMotionEvaluation lease,out string reason){
  lease=null;var animator=actor?actor.GetComponentInChildren<Animator>():null;
  bool clock=Mathf.Abs(Time.timeScale-1)<.00001f && Mathf.Abs(Time.captureDeltaTime-.02f)<.00001f || Mathf.Abs(Time.timeScale-4)<.00001f && Mathf.Abs(Time.captureDeltaTime-.005f)<.00001f;
  if(Mathf.Abs(Time.fixedDeltaTime-.02f)>.00001f || !clock){reason="Training blocked: use measured fixed=0.02 with scale/capture 1/0.02 or 4/0.005";return false;}
  if(!animator || !animator.isActiveAndEnabled || !animator.runtimeAnimatorController){reason="Training blocked: authored Animator evaluation is required for motion equivalence";return false;}
  if(actor.GetComponent<Player>() && actor.GetComponentInChildren<RootMotionHandler>()?.isActiveAndEnabled!=true){reason="Training blocked: player root-motion handler is required";return false;}
  lease=new TrainingMotionEvaluation(actor,hide);reason="Same authored animation evaluation; rendering optional";return true;
 }
 public void Dispose(){if(disposed)return;disposed=true;if(animator)animator.cullingMode=oldCulling;for(int i=0;i<renderers.Length;i++)if(renderers[i])renderers[i].enabled=enabled[i];}
}
}
