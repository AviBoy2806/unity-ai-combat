using UnityEngine;
using UnityEngine.InputSystem;

namespace GameCombat {
/// <summary>Manual test fixture only. Never supplies autonomous combat decisions.</summary>
[DefaultExecutionOrder(-110)]
[RequireComponent(typeof(Combatant))]
public class CombatDebugControls : MonoBehaviour {
    [Tooltip("Development-only defense test controls. Off by default; ignored in release builds.")]
    public bool enableDefenseDebugging;
    public Combatant opponent;
    [Min(.1f)] public float attackDelay = 2;
    [Min(.5f)] public float testDistance = 1.3f;
    public Key lightKey = Key.F6, heavyKey = Key.F7, guardKey = Key.F8, sideKey = Key.F9;

    Combatant player, captured;
    Rigidbody body;
    bool rear, wasEnabled;
    CombatRequest pending;
    double due;
    string message = "Ready";

    void Awake() { player = GetComponent<Combatant>(); }
    void Update() {
        bool allowed = enableDefenseDebugging && Debug.isDebugBuild;
        if (!allowed) { if (wasEnabled) ReleaseOpponent(); wasEnabled = false; return; }
        wasEnabled = true;
        var keyboard = Keyboard.current;
        if (keyboard == null) return;
        if (keyboard[sideKey].wasPressedThisFrame) {
            rear = !rear;
            message = rear ? "Next attack: rear" : "Next attack: front";
        }
        if (keyboard[lightKey].wasPressedThisFrame) Schedule(CombatRequest.Light);
        if (keyboard[heavyKey].wasPressedThisFrame) Schedule(CombatRequest.Heavy);
        if (keyboard[guardKey].wasPressedThisFrame && PrepareOpponent()) {
            pending = CombatRequest.None;
            if(captured.Runner.Action!=CombatAction.Free){message="Opponent busy";return;}
            PlaceOpponent();
            captured.Runner.SetGuard(!captured.Runner.Guarding);
            message = captured.Runner.Guarding ? "Opponent guarding" : "Opponent guard released";
        }
    }
    // Execute queued commands at physics boundaries, before CombatWorld collects contacts.
    void FixedUpdate() {
        if (!enableDefenseDebugging || !Debug.isDebugBuild || pending == CombatRequest.None || Time.fixedTimeAsDouble < due) return;
        var request = pending; pending = CombatRequest.None;
        if (!captured || captured.Dead || player.Dead) { message = "Actor dead: restart Play Mode"; return; }
        if(captured.Runner.Action!=CombatAction.Free){message="Opponent busy";return;}
        PlaceOpponent();
        captured.Runner.SetGuard(false);
        var result = captured.Runner.TryStart(request, captured.facing.forward);
        message = $"{request}: {result}";
    }
    public void Schedule(CombatRequest request) {
        if (!enableDefenseDebugging || !Debug.isDebugBuild || !PrepareOpponent()) return;
        if (request != CombatRequest.Light && request != CombatRequest.Heavy) return;
        if (captured.Runner.Action != CombatAction.Free) { message = "Opponent busy; wait for recovery"; return; }
        PlaceOpponent(); captured.Runner.SetGuard(false);
        pending = request; due = Time.fixedTimeAsDouble + attackDelay;
        message = $"{request} in {attackDelay:0.0}s";
    }
    bool PrepareOpponent() {
        if (captured && captured == opponent && !captured.Dead) return captured.GetComponent<EnemyBrainHost>().DebugOwned;
        ReleaseOpponent();
        if (!player || player.Dead) { message = "Player dead: restart Play Mode"; return false; }
        if (!opponent) {
            float nearest = float.PositiveInfinity;
            foreach (var candidate in FindObjectsByType<Combatant>()) {
                if (candidate == player || candidate.Dead || candidate.faction == player.faction || candidate.arenaId != player.arenaId) continue;
                float distance = (candidate.transform.position - transform.position).sqrMagnitude;
                if (distance < nearest) { opponent = candidate; nearest = distance; }
            }
        }
        if (!opponent || opponent == player || opponent.Dead || opponent.faction == player.faction || opponent.arenaId != player.arenaId) {
            message = "No living opposing combatant"; return false;
        }
        var host=opponent.GetComponent<EnemyBrainHost>();
        if(!host || !host.AcquireDebug()){message="Opponent committed or has no brain host; wait for recovery";return false;}
        captured=opponent;body=captured.GetComponent<Rigidbody>();
        return true;
    }
    void PlaceOpponent() {
        Vector3 forward = player.facing.forward; forward.y = 0; forward.Normalize();
        Vector3 position = transform.position + forward * testDistance * (rear ? -1 : 1);
        // Match capsule bottoms rather than assuming both actors have the same pivot.
        var playerCollider = GetComponent<Collider>(); var targetCollider = captured.GetComponent<Collider>();
        if (playerCollider && targetCollider) position.y = playerCollider.bounds.min.y + (captured.transform.position.y - targetCollider.bounds.min.y);
        Vector3 look = transform.position-position; look.y=0;
        captured.GetComponent<EnemyMotor>().Teleport(position,Quaternion.LookRotation(look.normalized,Vector3.up));
        Physics.SyncTransforms();
    }
    void ReleaseOpponent() {
        pending=CombatRequest.None;
        if(captured)captured.GetComponent<EnemyBrainHost>()?.ReleaseDebug();
        captured=null;body=null;
    }
    void OnDisable() { ReleaseOpponent(); wasEnabled = false; }
    void OnGUI() {
        if (!enableDefenseDebugging || !Debug.isDebugBuild) return;
        GUI.Box(new Rect(12,12,510,108), $"DEFENSE DEBUG — {lightKey}: incoming light | {heavyKey}: incoming heavy\n{guardKey}: opponent guard | {sideKey}: front/rear ({(rear?"rear":"front")})\nRMB: block/deflect | Shift: dodge | LMB: attack\n{message}");
    }
}
}

