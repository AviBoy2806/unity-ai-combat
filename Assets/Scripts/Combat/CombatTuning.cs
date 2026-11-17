using UnityEngine;
namespace GameCombat {
[CreateAssetMenu(menuName="Combat/Tuning")]
public class CombatTuning : ScriptableObject {
 public float maxPosture=100, guardAngle=120, deflectWindow=.18f, releaseRearm=.1f, deflectRearm=.3f;
 [Header("Parry window after a fresh guard press")]
 public bool parryWindowInFrames=false;
 [Min(1)] public float parryReferenceFps=60;
 [Tooltip("How long a new guard press can parry. Example: 10.8 frames at 60 FPS = 0.18 seconds. This is time after pressing guard, not a second damage window.")]
 [Min(.1f)] public float parryWindowFrames=10.8f;
 public float ParryWindow => parryWindowInFrames?parryWindowFrames/Mathf.Max(1,parryReferenceFps):deflectWindow;
 public float deflectPosture=25, recoil=.3f, breakDuration=2, breakReset=40;
 public float recoveryDelay=1.5f, recoveryRate=15, guardRecoveryRate=7.5f, guardMove=.4f;
 public float lightStagger=.2f, heavyStagger=.35f, dodgeDuration=.34f, dodgeStart=.06f, dodgeEnd=.2f, dodgeCooldown=.65f;
 public LayerMask obstacles=(1<<6); // Wall; exclude hurtboxes and ground.
}
}
