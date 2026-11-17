
using UnityEngine;
using System.Collections.Generic;

public class HitDetection : MonoBehaviour
{
    [SerializeField] private BoxCollider Col;
    public string hitTag = "Enemy";

    private readonly HashSet<GameObject> hits = new();

    private void Awake()
    {
        if (Col == null)
            Col = GetComponent<BoxCollider>();

        if (Col != null)
        {
            Col.isTrigger = true;
            DisableHitbox();
        }
    }

    public virtual void OnHit(GameObject obj) { }

    public void EnableHitbox()
    {
        if (Col == null)
            return;

        // Kept for old animation bindings; authoritative volumes live in CombatWorld.
        Col.enabled = false;
    }

    public void DisableHitbox()
    {
        if (Col == null)
            return;

        Col.enabled = false;
    }
}
