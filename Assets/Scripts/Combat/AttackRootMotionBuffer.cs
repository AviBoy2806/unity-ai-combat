using UnityEngine;
namespace GameCombat {
// Animation supplies displacement; the combat motor remains the sole Rigidbody writer.
public sealed class AttackRootMotionBuffer {
    Vector3 pending;
    public void Add(Vector3 delta) { if(float.IsFinite(delta.x) && float.IsFinite(delta.z)) pending += new Vector3(delta.x,0,delta.z); }
    public Vector3 Consume(float fixedDelta) { var delta=pending;pending=Vector3.zero;return fixedDelta>0?delta/fixedDelta:Vector3.zero; }
    public void Clear() { pending=Vector3.zero; }
}
}
