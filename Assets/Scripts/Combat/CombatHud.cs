using UnityEngine;
using UnityEngine.InputSystem;

namespace GameCombat {
// Reads combat state; controller selection is routed through the brain host.
[DefaultExecutionOrder(-120)]
public sealed class CombatHud : MonoBehaviour {
    Combatant player;
    PlayerInputComponent input;
    EnemyBrainHost[] enemies;
    float refresh;
    bool cursorReleased;
    GUIStyle title, body, small, status, button, wrap;
    Texture2D pixel;
    readonly Color ink = new(.035f, .055f, .075f, .95f), muted = new(.82f, .87f, .9f),
        teal = new(.35f, .87f, .76f), gold = new(.94f, .71f, .36f), red = new(.93f, .38f, .35f);

    void OnEnable() { player = GetComponent<Combatant>(); input = GetComponent<PlayerInputComponent>(); refresh = 0; }
    void Update() {
        if (Time.unscaledTime >= refresh || enemies == null) {
            refresh = Time.unscaledTime + .5f;
            enemies = FindObjectsByType<EnemyBrainHost>(FindObjectsSortMode.InstanceID);
        }
        var keys = Keyboard.current;
        if (keys == null) return;
        if (keys.tabKey.wasPressedThisFrame) SetCursorReleased(!cursorReleased);
        if (keys.escapeKey.wasPressedThisFrame && cursorReleased) SetCursorReleased(false);
        if (keys.f1Key.wasPressedThisFrame) ToggleController();
    }

    bool SameArena(EnemyBrainHost host) => host && host.isActiveAndEnabled && player &&
        host.GetComponent<Combatant>().arenaId == player.arenaId;

    public void ToggleController() {
        if (enemies == null) return;
        EnemyControlMode next = EnemyControlMode.ML;
        foreach (var host in enemies) if (SameArena(host)) {
            next = host.mode == EnemyControlMode.ML ? EnemyControlMode.Normal : EnemyControlMode.ML;
            break;
        }
        foreach (var host in enemies) if (SameArena(host)) host.SetModeImmediately(next);
    }

    public void SetCursorReleased(bool released) {
        cursorReleased = released;
        if (input) input.SetHudInput(released);
        Cursor.lockState = released ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = released;
    }
    void OnDisable() {
        if (input) input.SetHudInput(false);
        if (cursorReleased) { cursorReleased = false; Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
    }

    void Init() {
        if (pixel) return;
        pixel = new Texture2D(1, 1); pixel.SetPixel(0, 0, Color.white); pixel.Apply();
        title = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
        body = new GUIStyle(GUI.skin.label) { fontSize = 16 };
        small = new GUIStyle(GUI.skin.label) { fontSize = 15 };
        status = new GUIStyle(body) { alignment = TextAnchor.MiddleRight };
        button = new GUIStyle(GUI.skin.button) { fontSize = 17, fontStyle = FontStyle.Bold };
        wrap = new GUIStyle(small) { wordWrap = true };
        foreach (var style in new[] { title, body, small, status }) style.normal.textColor = Color.white;
    }
    void Fill(Rect r, Color c) { GUI.color = c; GUI.DrawTexture(r, pixel); GUI.color = Color.white; }
    void Text(Rect r, string s, GUIStyle style, Color c) { GUI.color = c; GUI.Label(r, s, style); GUI.color = Color.white; }
    void Bar(float x, float y, float w, float value, Color c) {
        Fill(new Rect(x, y, w, 7), new Color(1, 1, 1, .09f));
        Fill(new Rect(x, y, w * Mathf.Clamp01(value), 7), c);
    }
    public static string ActionLabel(Combatant a) {
        var r = a.Runner;
        return a.Dead ? "DEFEATED" : r.Broken ? "GUARD BROKEN" : r.Action == CombatAction.Recoil ? "DEFLECTED" :
            r.Action == CombatAction.Stagger ? "STAGGERED" : r.Action == CombatAction.Dodge ? "EVADING" :
            r.Guarding ? "GUARDING" : r.Action == CombatAction.Attack ?
            (r.Phase == AttackPhase.Windup ? "WINDUP" : r.Phase == AttackPhase.Active ? "STRIKE" : "RECOVERING") : "READY";
    }
    void ActorPanel(Rect box, Combatant a, string label, Color accent) {
        Fill(box, ink); Fill(new Rect(box.x, box.y, 3, box.height), accent);
        float x = box.x + 20, y = box.y + 12, w = box.width - 40;
        Text(new Rect(x, y, w, 28), label, title, Color.white);
        Text(new Rect(x, y, w, 28), ActionLabel(a), status, a.Runner.Broken ? gold : accent);
        y += 38;
        Text(new Rect(x, y, w, 22), "VITALITY", small, muted);
        Text(new Rect(x, y, w, 22), $"{Mathf.CeilToInt(a.Health.Health)} / {a.Health.maxHealth:0}", status, Color.white);
        Bar(x, y + 26, w, a.Health.Health / Mathf.Max(1, a.Health.maxHealth), accent);
        y += 43;
        Text(new Rect(x, y, w, 22), "POSTURE", small, muted);
        Bar(x, y + 25, w, a.Posture / Mathf.Max(1, a.tuning.maxPosture), gold);
        if (a.Runner.Broken) Text(new Rect(x, y, w, 22), $"{a.Runner.Remaining:0.0}s", status, gold);
    }
    public static string ModeLabel(EnemyBrainHost h) => h.ActiveMode == EnemyControlMode.ML ? "ML  /  EVALUATION ONLY" :
        h.ActiveMode == EnemyControlMode.Test ? "TEST CONTROLLER" : "STATE-BASED AI";

    void ModeButton(Rect rect, EnemyBrainHost host, EnemyControlMode mode, string label, Color accent) {
        bool selected = host.mode == mode;
        GUI.backgroundColor = selected ? accent : new Color(.25f, .3f, .36f);
        if (GUI.Button(rect, (selected ? "●  " : "") + label, button)) host.SetModeImmediately(mode);
        GUI.backgroundColor = Color.white;
    }

    void OnGUI() {
        if (!player) return;
        Init();
        var old = GUI.matrix;
        float scale = Mathf.Min(Screen.width / 1920f, Screen.height / 1080f);
        if (scale <= 0) return;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
        float w = Screen.width / scale, h = Screen.height / scale;
        Fill(new Rect(36, 24, 300, 64), ink);
        Text(new Rect(50, 29, 270, 28), "PROVING GROUND / 01", body, Color.white);
        Text(new Rect(50, 57, 270, 24), "COMBAT DEVELOPMENT ARENA", small, teal);
        ActorPanel(new Rect(36, h - 225, 420, 174), player, "PLAYER", teal);
        Fill(new Rect(36, h - 45, w - 72, 30), ink);
        Text(new Rect(48, h - 42, w - 96, 24), "WASD  Move    SPACE  Jump    SHIFT  Dodge / run    LMB  Attack    RMB  Guard    Q / MMB  Lock on    F1  State / ML    TAB  HUD cursor", small, muted);
        if (enemies != null) {
            int row = 0;
            foreach (var host in enemies) {
                if (!SameArena(host)) continue;
                var actor = host.GetComponent<Combatant>();
                float x = w - 456, y = 36 + row++ * 400;
                ActorPanel(new Rect(x, y, 420, 174), actor, row > 1 ? "OPPONENT " + row : "OPPONENT", red);
                Fill(new Rect(x, y + 180, 420, 204), ink);
                Text(new Rect(x + 20, y + 190, 380, 24), "ENEMY CONTROLLER", body, muted);
                ModeButton(new Rect(x + 20, y + 222, 185, 40), host, EnemyControlMode.Normal, "STATE-BASED", teal);
                ModeButton(new Rect(x + 215, y + 222, 185, 40), host, EnemyControlMode.ML, "ML", gold);
                Text(new Rect(x + 20, y + 273, 380, 24), "Active: " + ModeLabel(host), body, host.ActiveMode == EnemyControlMode.ML ? gold : teal);
                string info = !string.IsNullOrEmpty(host.FallbackReason) ? "Fallback: " + host.FallbackReason :
                    host.HasPendingSwitch ? "Debug controller owns enemy; selection queued." :
                    host.ActiveMode == EnemyControlMode.ML ? "Learned candidate • evaluation only" : "Authored state-based controller";
                Text(new Rect(x + 20, y + 301, 380, 44), info, wrap, !string.IsNullOrEmpty(host.FallbackReason) ? gold : muted);
                Text(new Rect(x + 20, y + 352, 380, 24), cursorReleased ? "Click a mode  •  TAB / ESC to resume" : "F1  Switch now   •   TAB  Click HUD", small, muted);
            }
        }
        GUI.matrix = old;
    }
    void OnDestroy() { if (pixel) Destroy(pixel); }
}
}
