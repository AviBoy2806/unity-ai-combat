
using UnityEngine;

public class PlayerRefManager : MonoBehaviour
{
    public static PlayerRefManager Instance { get; private set; }

    public Player PlayerRef { get; private set; }

    public static void CreateManager(Player player)
    {
        if (Instance == null)
        {
            GameObject managerObject = new GameObject("PlayerRefManager");
            Instance = managerObject.AddComponent<PlayerRefManager>();
        }

        Instance.SetPlayer(player);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void SetPlayer(Player player)
    {
        PlayerRef = player;
    }

    public bool HasPlayer()
    {
        return PlayerRef != null;
    }

    public Vector3 GetPlayerPosition()
    {
        return HasPlayer() ? PlayerRef.transform.position : Vector3.zero;
    }

    public float GetDistanceToPlayer(Vector3 position)
    {
        if (!HasPlayer())
            return Mathf.Infinity;

        return Vector3.Distance(position, PlayerRef.transform.position);
    }

    public Vector3 GetDirectionToPlayer(Vector3 position)
    {
        if (!HasPlayer())
            return Vector3.zero;

        return (PlayerRef.transform.position - position).normalized;
    }

    public Vector3 GetDirectionAwayFromPlayer(Vector3 position)
    {
        return -GetDirectionToPlayer(position);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
