// Legacy presentation adapter: animated colliders never resolve gameplay damage.
public class Sword : HitDetection { public float damageModifier=1; public override void OnHit(UnityEngine.GameObject obj) {} }
