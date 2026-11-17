<proposed_plan>
# Shared combat system and switchable enemy controllers

**Writing rule:** Use simple words and short sentences in explanations and test steps. Say which object to select, which key to press, and what the player should see. Explain technical terms the first time they are needed. Keep old evidence as history; do not call a scene test passed from a small code test.

## 1. Summary and project findings

Build one shared combat system used by the player, deterministic enemies, and ML enemies. Controllers choose actions; shared gameplay code decides whether actions are legal and resolves their effects.

Preserve the existing Rigidbody movement, NavMesh pathfinding, camera, lock-on, and exploration behavior. Replace animation-dependent combat timing and direct sword-to-health damage because those prevent reliable defense mechanics and reproducible training.

**Delivery constraint:** No project files were changed. Plan Mode also prevents creating Markdown files, so this is the complete Markdown handoff in-chat. When file writing is enabled, save it as `COMBAT_IMPLEMENTATION_PLAN.md` outside `Assets`.

### Verified current implementation

| Area | Existing behavior and implication |
|---|---|
| Environment | Unity **6000.6.0f1**, AI Navigation **2.0.14**, Input System **1.20.0**, fixed timestep **0.02 seconds**. ML-Agents is not installed. |
| Enemy movement | `enemy` owns a dynamic Rigidbody and custom gravity. NavMeshAgent position/rotation updates are disabled; states consume its desired velocity. Preserve this separation. |
| Enemy decisions | `EnemyStateMachine` contains dead, idle, wander, and chase. There is no enemy gameplay attack state. |
| Perception | `EnemyPerceptionNav` uses range and obstacle linecasts, without a field-of-view cone. Detection is 15 m and visibility retention reaches 22 m in the scene. |
| Memory | Chase follows `LastSeenPosition`, replans every 0.2 s, waits 3 s after arrival, and abandons investigation after 15 s. Preserve these semantics; `memoryDuration` is not simply time since last sighting. |
| Player | Existing state machine supports movement, jump/fall, dodge, a three-hit light combo, running heavy attack, and death. Camera and lock-on are separate components. |
| Input | The player uses `Assets/Input/PlayerInputAction.inputactions`, map `Movement`, with Send Messages. The other input-actions asset is not the player’s assigned asset. RMB is available. |
| Damage | `Sword` calls `HealthComponent.takeDamage` directly. `HitDetection` filters by collider tag and deduplicates using `transform.root`, which is unsuitable for child hurtboxes or actors under training-arena parents. |
| Timing | Player attacks read Animator state names and normalized time. Dodge completion also depends on animation. `RootMotionHandler` can write player velocity separately from states. |
| Animation assets | Both characters have block clips. Enemy controller already contains attack/block states and parameters, but corresponding gameplay is absent. Player dodge uses a 1 s clip at state speed 3; enemy attack uses a 2.667 s clip at speed 2. |
| Asset wiring | Enemy navigation/perception and player health/lock-on/sword include scene-only additions. Base prefabs are not complete standalone actors. Old serialized enemy fields also remain. |
| Baseline concern | `PlayerInputComponent`’s script GUID was not found in the saved prefabs or scene. Verify and repair this before feature work; the attribute on `Player` is not evidence that existing serialized instances contain it. |

These are source/serialized-asset findings, not a verified Play Mode baseline. No automated project tests were found under `Assets`.

### Chosen scope

- One melee enemy archetype, with two attacks, blocking, deflection, dodge, posture, stagger, and deliberate spacing.
- Player gains the same defense and posture rules while retaining existing controls and movement.
- No stamina resource initially: posture, recovery, and dodge cooldown already constrain aggression and defense.
- No execution/deathblow system, unblockable attack taxonomy, learned locomotion, or coordinated enemy squads in v1.
- “Deterministic AI” means authored, reproducible decisions with a local seed where variation is needed—not cross-platform deterministic physics.

## 2. Shared architecture and combat rules

### Ownership and interfaces

Introduce these responsibilities, using ordinary C# types where a MonoBehaviour is unnecessary:

| Component/type | Responsibility |
|---|---|
| `Combatant` | Actor identity, faction, health reference, facing transform, posture, current action, and authoritative combat status. |
| `CombatActionRunner` | Accept/reject requests; advance attack, dodge, stagger, and recovery timelines; expose movement restrictions. |
| `CombatResolver` | Resolve validated contacts into damage, block, deflection, posture break, or evasion. |
| `CombatWorld` | Coordinate fixed-step action updates and contact resolution for registered actors. |
| `AttackDefinition` | ScriptableObject containing timings, damage, posture values, reach/shape, movement curve, combo link, and presentation binding. |
| `CombatTuning` | Shared defense, posture, dodge, and interruption settings. |
| `CombatPresentation` | Consume action/result events; drive existing animations and temporary visual feedback. |
| `EnemyMotor` | Sole enemy horizontal movement/rotation writer; accepts navigation or combat movement intent. Existing enemy gravity remains. |
| `EnemyBrainHost` | Select exactly one combat controller and manage switching/fallback. |
| `IEnemyCombatBrain` | Produce `CombatIntent` from a read-only `CombatObservation`; reset controller-local state. |
| `ScriptedCombatBrain` | Authored combat decisions. |
| `EnemyLearningAgent` | ML-Agents adapter producing the same intents. It does not own combat rules. |

Minimum contracts:

```csharp
CombatIntent
    Vector2 localMove;
    bool guardHeld;
    CombatRequest request; // None, Light, Heavy, Dodge
    uint sequence;

CombatActionRunner.TryStart(request, direction)
    -> Accepted | Busy | Cooldown | Airborne | Dead | Invalid

CombatResult
    attackInstanceId, attackerId, defenderId;
    outcome; // Hit, Block, Deflect, Evade, PostureBreak
    actualHealthDamage, actualPostureChange;

EnemyBrainHost.SetMode(EnemyControlMode mode)
    -> Applied | Deferred | Unavailable
```

Use per-actor, monotonically increasing attack IDs and explicit combatant ownership. Never identify an actor using its hierarchy root.

### Separate exploration, decisions, and execution

Retain enemy idle/wander/chase/dead states and add a combat state:

- Idle/wander/chase preserve existing detection, navigation, and investigation.
- Combat state delegates decisions to the selected brain.
- Attack phases, defense, dodge, and stagger belong to the shared action runner—not duplicated enemy FSM states.
- Enter combat when the target is visible within 5 m; return to chase beyond 7 m or after losing visibility.
- On visibility loss, finish an already committed action, prohibit new target-dependent attacks, and investigate the last seen position.
- Returning from combat must reset stale navigation paths and movement intent.

Keep the existing `enemy` class name initially because `LockOn` and serialized assets reference it.

Replace direct player attack/dodge execution with calls into the shared runner. Existing player states become adapters for input, locomotion, and presentation. Add a guard state and stagger adapter; do not build a second independent player combat state machine.

### Fixed-step authority

Combat runs at the existing 50 Hz physics rate:

1. Latch player input edges; collect controller intents.
2. Apply forced outcomes and legal action requests.
3. Advance action timelines and derive movement restrictions.
4. Apply movement through each actor’s existing motor.
5. Gather attack contacts during the active interval.
6. Resolve against a stable defender-state snapshot; aggregate results.
7. Apply health/posture/interruption outcomes and publish events.

Use explicit ordering rather than unrelated `Update` methods competing for control. Input edges remain queued until consumed by a physics tick; `LateUpdate` must not erase unconsumed combat presses.

For opposing attacks contacting on the same tick, allow trades using the pre-resolution snapshot. A deflection defeats that incoming contact; it does not retroactively erase unrelated valid contacts. Death and interruptions disable future attack samples immediately.

### Hit detection

Make authored, actor-relative hit volumes authoritative for both player and enemy attacks:

- Query a moving box/capsule over the active interval using the definition’s local trajectory.
- Include an overlap sample when the active phase opens, plus swept/subdivided samples between poses.
- Use explicit hurtbox ownership, faction checks, and a wall obstruction check.
- Resolve each defender once per attack instance, including blocks, deflections, and evasions.
- Size query buffers with overflow detection; do not silently discard hits.
- Do not use the animated sword collider as a second damage source.

This moves gameplay contact away from animated weapon bones. Missing animations must not block actions, damage or completion. Animation-free movement uses authored fallback displacement; exact travel parity with script-driven visual root motion must be validated or baked for training rather than assumed. Existing weapon models remain presentation assets.

Retain `Sword`/`HitDetection` temporarily as migration adapters, then remove their direct damage path once both actors use the resolver.

### Defense and posture

All values below are initial serialized defaults, not claims about ideal balance.

| Rule | Default |
|---|---|
| Health/posture maximum | 100 each |
| Guard input | Hold RMB; release lowers guard |
| Guard coverage | 120° horizontal cone around the defender’s model forward |
| Successful block | Zero health damage; apply the attack’s block-posture damage |
| Deflection window | First 0.18 s after a legal fresh guard press |
| Deflection rearm | Guard released for at least 0.10 s and 0.30 s since the last window began |
| Deflection effect | Zero defender health/posture damage; attacker receives 25 posture and 0.30 s recoil |
| Posture break | At 100 posture, drop guard and interrupt into 2.0 s vulnerable stagger by default; enemy prefab uses a 3.5 s per-actor override to give the player a clear opening |
| After break | Reset posture to 40 at stagger completion |
| Posture recovery | After 1.5 s without posture damage: 15/s unguarded, 7.5/s guarding |
| Health scaling | Recovery multiplier `0.5 + 0.5 × healthFraction` |
| Guard movement | 40% walking speed; no sprint/jump while guard remains active |
| Normal hit reaction | 0.20 s for light, 0.35 s for heavy; recovery interruption rules below |
| Dodge | 0.34 s duration; invulnerability during `[0.06, 0.20)`; 0.65 s start-to-start cooldown |

Additional rules:

- Direction is measured from defender facing toward the attack’s sampled origin, projected horizontally. Rear attacks bypass guard.
- Deflection requires an active guard, an eligible new press, and directional coverage.
- Repeated “guard held” commands never reopen the window.
- Pressing during an uninterruptible action does not pre-bank a deflection. If still held when free, ordinary guard begins without a fresh window.
- Deflection takes precedence over block; invulnerability takes precedence over both.
- A block that fills posture causes a break but no retroactive health damage. Subsequent attacks can punish the stagger.
- Holding guard cannot prevent posture break. Successful deflection itself never breaks the defender.
- Filling posture and starting its break are one operation in `Combatant.AddPosture`. At the full threshold the shared runner interrupts immediately, before the call returns. There is no legal full-posture actor waiting for a later resolver call. Extra posture during an existing break cannot restart it.
- No separate parry button or hidden perfect-defense privilege for enemies.

### Attack commitment and interruption

Use these starting profiles:

| Attack | Frame timing (current starting settings) | Health | Posture on hit / block |
|---|---:|---:|---:|
| Player light 1 | Frames 18 / 26 / 29 at 30 FPS, 1x (hit start / hit end / action end) | 10 | 10 / 20 |
| Player light 2 | Frames 5 / 9 / 20 at 30 FPS, 1x | 10 | 10 / 20 |
| Player light 3 | User-saved frames 32 / 50 / 55 at 30 FPS, 1x | 20 | 20 / 30 |
| Player running heavy | Frames 42 / 56 / 108 at 60 FPS, 1x | 30 | 25 / 40 |
| Enemy light | Frames 38 / 45 / 80 at 30 FPS, authored 2x | 12 | 12 / 22 |
| Enemy heavy | Frames 48 / 60 / 102 at 30 FPS, authored 2x | 22 | 25 / 40 |

The latest frame-timing request replaces the older action lengths in this table. These are editable starting frames, not proof of weapon contact in the rendered scene. Charge runs before Hit Start; damage is allowed from Hit Start up to Hit End; recovery ends at Action End. Source frames convert to seconds using clip FPS and the existing state speed. The shared runner still owns these times; no animation event or Animator progress deals damage. The copied authored combo graph was checked against these schedules. Do not stretch clips.

These intentionally replace the old normalized damage windows. CombatActionRunner owns these gameplay intervals independently of visual playback; preserve authored clip speeds and controller transitions rather than retiming clips to fit these durations. See Animation ownership and preservation below.

- One fresh light click can queue the next light in the last 0.4 seconds before Action End, but never before Hit Start. This can include the late hit phase when authored recovery is short. The next strike still starts only after full recovery. Holding LMB does not queue more strikes. The window is tunable per AttackDefinition with comboInputSeconds.
- Resolve attack type from the complete fixed-step input snapshot, independent of callback ordering or stale FSM state. A transient attack retry is bounded to 0.12 seconds (below the 0.15-second ceiling), only for the final 0.12 seconds of an existing dodge or a free-but-airborne landing boundary. It never cancels commitment, extends combo windows, retries attacks blocked by guard/attack/stun, or repeats held LMB; clear on expiry, focus loss, disable, death and forced interruption. Preserve running attack selection and the heavy attack’s leap identity. Route its impulse through the player movement adapter exactly once.
- Attacks cannot cancel into guard or dodge. Recovery is a real vulnerability.
- Light attacks are interrupted by normal hits during windup/active. Heavy attacks resist ordinary hit interruption during active frames only.
- Deflection recoil, posture break, and death always interrupt, regardless of ordinary-hit resistance.
- During recovery, normal hits can stagger.
- A stagger cannot be extended indefinitely by ordinary hits: accept damage/posture while staggered without restarting its timer. Death still applies immediately.
- Player heading is strictly locked throughout gameplay attack commitment and every current/incoming/outgoing attack visual state. No input or lock-on turning in windup or after gameplay completion while attack visuals persist. Lock actor/model heading, not animated bones; visual lock never extends gameplay commitment or damage. Missing animation locks through gameplay commitment. Enemy attack turning remains limited to 180°/s in windup and frozen during active/recovery.
- Grounded starts are required for attacks and dodges. The running heavy’s authored leap may subsequently leave the ground.
- Request precedence when free: dodge, guard, attack. A previously committed action wins over new ordinary requests.

Use initial forward reach of 2 m for player light/enemy light, 2.2 m for player heavy, and 2.4 m for enemy heavy. Query widths/heights must be calibrated to the actual meshes during the collision acceptance phase; debug volumes must show the exact gameplay shape.

### Animation ownership and preservation

- Animator Controller owns normal visual transitions and blending. CombatActionRunner owns requests, legality, combo eligibility, damage windows/deduplication, commitment/recovery, interruption, death precedence and action completion. Animator state/normalized time, exit times and animation events must never decide those gameplay outcomes.
- Preserve authored combo transition conditions, exit times, durations, interruption settings and clip/state speeds, including attack 2 at 1x, plus script-driven player attack XZ root motion (including heavy). Preserve heavy foot-ground correction. Do not replace normal combo transitions with generic fixed crossfades or automatically retime clips to fit action duration.
- Requests for the player's visual sequence are queued once per accepted gameplay strike and dispatched through the existing Attack trigger in controller order. The CombatComboHold bool gates only Attack 1/2 locomotion exits while gameplay continuation remains possible/pending; all existing transition timing/interruption settings remain authored. No per-strike forced crossfade or forced Free-to-locomotion blend is used for normal player combos. Clear queued/latched attack triggers and the hold condition on interruption, death, presentation disable or unavailable animation. Explicit crossfades are reserved for justified interruption/fallback where no authored reaction state exists.
- The user-authored hit and action frames are preserved. A late combo request must not wait for a second Animator exit crossing while its game clock advances. Keep the original strike transitions; add conditional late-request transitions that copy their authored blend/interruption settings and activate only after the original exit was missed. Likewise, late recovery uses a conditional authored blend out after gameplay completion, so a held final pose cannot trap later combos. No generic per-strike crossfade, clip-speed override or automatic retiming.
- Keep only the current accepted visual request. Clear stale triggers/requests and late gates on completion, interruption, death and disable. Hold Attack 3 and heavy locomotion exits while their shared gameplay action is still running, as already done for Attack 1/2. A new chain waits for the old attack pose to leave through normal blends; the input adapter allows at most 0.12 s of startup buffering for this tail. Animator reads here only control presentation/startup input; they never open damage windows, complete recovery or apply health damage.
- Asset review: the older Light3 62/77/78 setting exceeded its 66-frame clip. The user's current saved Light3 is 32/50/55, inside that clip. Preserve it. Keep the Inspector warning and clamped end-pose preview for any future out-of-range setting. Human contact-frame review remains pending.

- Player WASD and jump movement stay blocked while any attack visual is still playing or blending out, even if shared recovery has finished. This movement-only lock does not reopen damage or extend recovery. Script-driven attack XZ motion still goes through the existing motor only during the shared attack.
- The red hit box is visible only while damage is on. CHARGE is a separate warning label, not a yellow damage box. Side sweep is opt-in; the default box stays centered. The editor frame preview shows the same box used by damage queries.
- Parry can be tuned in two places: CombatTuning sets the short window after a fresh RMB press; each attack can optionally restrict which hit frames allow a parry. Both checks must pass. A guard outside the allowed parry frames still blocks a frontal hit. Use the sampled contact time for this check. No extra collision damage path is added.

- Animation-free fallback must continue to move as authored and complete actions independently. This rule supersedes earlier recommendations to retime clips to action definitions or suppress all attack root motion; future changes must preserve this ownership split and validate sequencing separately from visual quality.

### Movement and animation integration

- Preserve player camera-relative walking/running/jumping and existing lock-on camera behavior.
- Combat actions own horizontal velocity while active. Player locomotion states must not overwrite it.
- Preserve script-controlled attack XZ root motion, including heavy attack. RootMotionHandler supplies planar animation displacement to the combat motor, which is the sole Rigidbody movement writer; do not apply both animation displacement and fallback action movement. Clear pending displacement on action changes and block outgoing attack motion after gameplay ownership ends. Preserve heavy foot-ground presentation correction and existing locomotion root-motion settings.
- The enemy motor takes either NavMesh desired velocity or combat local movement, never both.
- Combat movement uses Rigidbody motion plus collision/NavMesh boundary checks; reject movement into walls or off walkable ground rather than teleporting to sampled points.
- Guard faces the locked target when locked on; otherwise use the player’s existing facing behavior.
- Update lock-on to reject dead combatants.

Presentation subscribes to action start/phase/end and combat-result events. Animation events may trigger sound/VFX, but must not apply damage or decide gameplay completion.

Temporary feedback must work without an Animator:

- Guard: translucent forward arc.
- Deflection: role-specific bright flash and DEFLECT on defender; amber RECOIL with remaining runner duration on attacker. Forced reactions leave the active attack pose; use a temporary procedural torso reaction when no suitable clip exists. Ordinary deflection is 0.30 s recoil; only an actual posture threshold causes the full 2.0 s break. Successful deflection never breaks the defender.
- Windup: attack indicator with a distinct heavy color.
- Active attack: visible procedural weapon trail matching the query volume.
- Stagger/posture break: distinct tint and posture bar flash.
- Dodge invulnerability: brief afterimage/tint.
- Health/posture bars and a development overlay showing controller, action phase, and rejected requests.

## 3. Deterministic AI and mode switching

### Scripted combat behavior

Use an ordered decision policy with timers, not a large behavior-tree dependency.

The brain evaluates every 0.06 s. Opponent action cues arrive through a 0.16 s observation delay, shared with the ML controller. Never read player button state or queued actions.

Priority:

1. Respect death, stagger, recoil, and existing commitments.
2. React to a newly observed incoming windup if the attack is plausibly in reach.
3. If own posture exceeds 75, pause voluntary attacks until below 45. Do not automatically retreat or dodge backward merely because posture is high; this used to make the enemy escape before the full-posture opening could be earned.
4. Punish visible opponent recovery/stagger when in reach and otherwise free.
5. Approach when outside the selected attack’s reach.
6. Space, circle briefly, and initiate a committed attack.

Concrete starting behavior:

- Desired neutral spacing: 1.8–2.3 m. During a neutral circle, use sideways movement first and only a small inward/outward correction; do not replace the whole circle with forward movement just because light reach is 2 m. Use the delayed target direction for orbit and dodge directions, even if enemy facing is still catching up.
- Combat movement speed: 2.5 m/s; preserve scene chase speed 5 m/s.
- At most two light attacks in an authored sequence, each respecting full recovery, then 0.6 s neutral spacing.
- Use heavy attacks against sustained guard after 0.5 s of observed guarding, or after two completed light attacks; heavy cooldown 3 s.
- For a fresh threat, raise guard after the observation delay. Time the press from the observed telegraph’s elapsed phase and known public attack profile; never inspect future hit results.
- At high posture, raise guard on seeing the warning, rather than always scheduling a last-moment deflection. A slow warning then leads to ordinary block and can fill posture. High posture alone does not order a backward dodge. The response to repeated real health hits below remains available outside full stun.
- Circle for at most 0.6 s before reconsidering an approach/attack; schedule the neutral interval after completed recovery, not at attack start. Alternate direction reproducibly. Delayed incoming threats can preempt neutral spacing when legally free.
- Defensive responses do not cancel committed attacks.

Full posture is a guaranteed opening: the enemy prefab stays in place, unguarded and unable to dodge/counter for 3.5 seconds (Combatant.postureBreakSeconds). The shared/player default remains 2.0 seconds. Ordinary hits do not restart or shorten that stun; health damage still lands. Posture resets to 40 afterward. Strong blocking before the break is retained.

Explicit enemy stun (supersedes the earlier constraint and no-retreat patches): reaching full posture enters `EnemyStunnedState` synchronously inside the posture update itself. The shared runner owns the duration and posture reset. Only runner completion or death can leave this state. Normal and forced chase/combat/wander transitions cannot interrupt it. The displayed posture number distinguishes a high yellow bar from the actual full break; full break keeps its STUN countdown visible.

StunnedState suspends brain execution, clears old requests/escape history, stops navigation and owns the motor's `Stunned` movement mode. That mode uses a stationary kinematic body until state exit, then restores normal physics with no stale velocity. It does not edit Rigidbody constraint bits or use a post-break direction clamp. Health hits still land, ordinary hits do not restart the timer, and controller changes remain deferred. The enemy's current state name is visible in its Inspector.

The enemy Animator has a separate `Stunned` state, entered as a forced reaction, with no attack/block/locomotion exits until the stun ends; death still wins. The real enemy uses a Generic rig, so the older Humanoid-only torso effect was never sufficient. Stunned uses the existing idle clip as a neutral standing pose (not the locomotion blend tree) until a dedicated stun clip is authored. No player normal combo transitions or attack root motion are changed by this state work.

After three actual health-damaging hits within 2.5 seconds outside a posture-break opening, Normal mode may try one escape after the current ordinary action/reaction is fully over. Full posture break discards that request rather than delaying it. Try a clear backward/side dodge first; if those routes are blocked, use a legal heavy counter when in reach and low enough on posture, otherwise guard/retreat. Keep a 3-second response cooldown. Count only real landed hits on self, not player inputs or blocked attempts. Full posture stun, death, shared dodge cooldown and attack commitment still win. This is an escape attempt, not immunity.

This creates punishable decisions and visible opportunities without giving the enemy perfect reactions.

### Switching

Support both Inspector selection and runtime switching through `EnemyBrainHost.SetMode`.

- Default mode: `Normal`.
- Exactly one brain may submit commands.
- A normal switch during attack/dodge/stagger is deferred until the runner becomes free.
- On switch: clear controller intent, queued combo requests, held guard, observation history, and controller-local timers.
- Preserve health, posture, action cooldowns, target, and perception memory.
- Use a controller-generation token to reject late ML callbacks from the previous owner.
- Missing/incompatible model: reject ML activation, retain normal mode, and expose a clear diagnostic.
- Inference failure while active: stop new ML requests and transition to normal at the next safe boundary.
- Switching cannot reset cooldowns, cancel attacks for free, or revive the actor.

Implementation clarification for Phases 3/4: expose a distinct Test mode exercising IEnemyCombatBrain without claiming ML availability. Default-off defense debugging takes an exclusive host lease only while free; release and pose restoration wait through commitment. Heavy policy cooldown belongs to the host so controller-local resets cannot bypass it. Clearing controller history does not clear perception memory. No ML adapter/model is available in this phase.

Both modes keep the same exploration/investigation system. ML controls combat, not navigation to a remembered target. This bounds the learning problem without making the ML enemy a separate implementation.

## 4. Machine-learning design

### Algorithm and tooling decision

| Approach | Decision |
|---|---|
| ML-Agents PPO | **Use.** Fits discrete combat choices, parallel Unity simulation, and the existing engine. |
| ML-Agents SAC | Plausible alternative when sample efficiency dominates, but replay adds complexity while opponents/curriculum change. Do not maintain two trainers initially. |
| Behavior cloning alone | Useful for demonstrations, but would mainly reproduce the authored controller and its weaknesses. Not the final solution. |
| PPO with cloning | Reserve as a measured bootstrap if basic learning stalls; not required for v1. |
| Competitive self-play from scratch | Defer. Early policies can converge on uninteresting mutual habits and exploit incomplete mechanics. |
| Custom Python/Gym/ONNX stack | Reject initially: additional environment, export, and inference integration without a demonstrated project benefit. |

These are project recommendations, not a claim that PPO universally outperforms SAC. Unity documents PPO, SAC, imitation learning, and competitive self-play in its [ML-Agents overview](https://unity-technologies.github.io/ml-agents/ML-Agents-Overview/).

### Package and compatibility baseline

Use the **`release_23_tag` source snapshot** as the reproducible initial package/trainer baseline:

- Its Unity package is ML-Agents **4.0.0**, requiring Unity **6000.0** and Inference Engine **2.2.1**. The project satisfies the declared minimum, but compatibility with this exact Unity editor still needs a smoke test. [Tagged package manifest](https://raw.githubusercontent.com/Unity-Technologies/ml-agents/release_23_tag/com.unity.ml-agents/package.json)
- Install both Python packages from that same snapshot; its trainer identifies itself as **1.2.0.dev0**. Do not assume a similarly named PyPI release matches it. [Tagged trainer version](https://raw.githubusercontent.com/Unity-Technologies/ml-agents/release_23_tag/ml-agents/mlagents/trainers/__init__.py)
- Use Python **3.10.12** in an isolated environment. Begin with PyTorch **2.1.1** and the release’s declared dependency constraints; freeze the complete environment after training/export/inference succeeds. [Release setup requirements](https://raw.githubusercontent.com/Unity-Technologies/ml-agents/release_23/ml-agents/setup.py)

This is a chosen compatibility baseline, not a claim that 4.0.0 is the newest available package. Do not upgrade the editor or unrelated packages as part of this work.

Compatibility inspection on Unity 6000.6.0f1 found two necessary C# changes to that starting baseline: the editor's own package manifest enforces Inference Engine >=2.6.1, and the tagged Match3 auto-seed uses GetInstanceID, an obsolete-error API here. Explicitly pin 2.6.1 and apply the guarded EntityId hash replacement recorded in `Tools/Phase5/unity6000.6.patch`. Both Python distributions remain from the same tagged commit. Setuptools 80.9.0 is required for the trainer's pkg_resources import. Exact versions, commands and evidence live in `Tools/Phase5/REPRODUCE.md`; these changes alone do not pass the export/inference gate.

The compatibility spike must successfully train a tiny included example, export ONNX, and run inference in this project before implementing the combat agent.

### Observation contract

Use normalized vector observations with three consecutive decision-frame stacks and a two-layer feed-forward network. No camera pixels or recurrent model in v1.

Create `CombatObservationV1` with a fixed, tested field order:

- Self: health/posture fractions, grounded, local planar velocity, current action category, phase/progress, remaining recovery, dodge cooldown, guard state, deflection rearm timer.
- Opponent: visible flag, delayed relative position/velocity, facing direction, health/posture fractions, guard state, publicly visible action category and phase/progress.
- Geometry: eight evenly spaced horizontal clearance probes, each reporting normalized wall distance and whether the intended short step remains on walkable ground.
- Previous accepted movement/action and whether the latest request was rejected.
- Time since valid opponent sighting.

Rules:

- Opponent observations use the same delayed perception stream as scripted AI.
- On current line-of-sight loss, mask opponent live features immediately; do not leak current position through a singleton or physics query.
- Do not include input buttons, queued combo, RNG, future impact time, or hidden cooldowns of the opponent.
- Normalize distance by 10 m, velocity by 20 m/s, and timers by their configured maximum; clamp values.
- One-hot encode categorical features.
- The observation builder computes a constant vector size; assert the emitted size and order in tests.
- Version the observation/action contract. Model metadata records its version and the combat-tuning hash.

Unity explicitly requires consistent observation size/order and supports vector stacking and discrete action masks. [Agent design documentation](https://unity-technologies.github.io/ml-agents/Learning-Environment-Design-Agents/)

### Action contract

Two discrete branches:

- **Movement, 9 choices:** stationary and eight normalized local directions.
- **Combat, 5 choices:** neutral, guard held, light attack, heavy attack, dodge.

Facing is shared assistance toward the visible target, subject to the same turn limits as scripted AI. The model does not rotate bones or control physics directly.

- New decision every three physics ticks: **0.06 s**.
- Movement/guard persist until the next decision.
- Attack/dodge requests are submitted once per decision sequence, not every repeated action callback.
- During commitments, mask new attacks/dodges and suppress forbidden movement.
- Guard selection while already guarding holds the existing guard; neutral releases it.
- Mask impossible actions only. Do not mask attacks merely because they are tactically poor or out of range.
- Always leave neutral available.
- Runtime validation remains authoritative even if action masks are wrong.

The ML scheduling adapter must request decisions from a prepared snapshot and buffer callbacks for a designated subsequent combat tick. Do not execute gameplay directly inside `OnActionReceived`; test the scheduling explicitly so training and inference have identical latency.

### Training environment

Create a separate training scene using the same combat components, attack definitions, and motors.

- Start with one arena and one learner; scale to eight spatially separated arenas after reset correctness is proven.
- Each arena explicitly owns two combatant references and an arena ID.
- Replace `PlayerRefManager` access inside perception/combat with injected target references. Keep the singleton only as a GameScene convenience adapter.
- Use a player-archetype opponent driven by a scripted input adapter; disable physical keyboard input, camera, cursor management, and player singleton registration in training.
- Hurt queries, rays, and events must reject other arenas.
- Damage, legality and completion must not depend on rendering/animation. Keep animation-free authored movement fallback functional; validate or bake movement equivalence for training while preserving script-driven XZ root motion in the player runtime.
- Keep physics timestep 0.02 s while accelerating simulated time; do not increase timestep to gain speed.
- The Phase 5 representative scene measurements reject animation-free player attack movement as training-equivalent. Keep the same authored Animator evaluation, presentation and root-motion handler in training, with AlwaysAnimate and optional renderer hiding. `TrainingMotionEvaluation` provides this preparation route. The measured tolerance is <=0.02 m maximum XZ error and <=0.01 m RMS with equal sample counts; the recorded light/heavy/dodge cases passed with rendering hidden and the Animator retained. This is not a blanket pass for all future arena, blend and input configurations.
- Episode duration: 30 simulated seconds. Death or out-of-bounds ends it; simultaneous death is a draw. Timeout is a draw.
- Reset both actors atomically: transforms, velocities, health/posture, action IDs, queues, guard/rearm, cooldowns, hit history, brain state, delayed observations, and navigation paths.
- Disable contact processing during reset; resume after transforms and navigation are synchronized.

### Curriculum and opponents

Use separate configuration stages with unchanged observation/action shape:

1. **Contact and spacing:** passive opponent, randomized position/facing. Promote after ≥90% success across 200 fixed evaluation episodes.
2. **Defense:** predictable light/heavy attackers with randomized start timing. Promote after ≥70% survival to timeout and measurable use of both block and deflection.
3. **Full duels:** uniform mixture of aggressive combo, sustained guard/counter, dodge/retreat, and balanced scripted opponents.
4. **Generalization:** varied arena walls, starting distance 1.5–6 m, orientation, ±10% movement speed, and ±15% telegraph duration. Keep damage/rule semantics fixed.

Run earlier opponents periodically to detect forgetting. Failed promotion means continue or diagnose training, not silently weaken the acceptance test.

Do not include self-play in the first deliverable. After the scripted-mixture baseline passes, frozen prior-policy opponents are a separate improvement phase; retain scripted opponents and held-out evaluation to avoid replacing one narrow exploit with another.

### Rewards

Use actual resolver outcomes, never attempted actions:

- Win **+1**, loss **−1**, draw **0**.
- Health damage dealt: `+0.25 × actualDamage / opponentMaxHealth`.
- Health damage received: corresponding negative amount.
- Each successful deflection: **+0.01**, capped at **+0.05 per episode**.
- Living time cost: **−0.001 per simulated second**.
- Out-of-bounds learner: loss.
- No recurring reward for holding guard, being close, moving, surviving a timestep, or repeatedly applying recoverable posture damage.

During stage 1 only, use bounded potential-based distance shaping: `0.05 × (gamma × nextPotential − previousPotential)`, with potential derived from distance to attack range and terminal potential zero. Remove it from final duel training.

Compute shaping at decision transitions and health/deflection rewards once per unique combat event. Never award the same event through both attacker and defender callbacks.

### Initial PPO configuration

Use:

- Two layers × 128 hidden units; three observation stacks.
- Manual observation normalization; trainer normalization off.
- Learning rate `3e-4`, linear schedule.
- Batch size 256; buffer size 4096.
- Three epochs; epsilon 0.2; lambda 0.95; beta 0.005.
- Extrinsic gamma 0.995; time horizon 128.
- Summaries every 10,000 decisions; checkpoints every 100,000.
- Initial full-duel budget: 2 million decisions, followed by evaluation—not a promised convergence point.

These are starting experiment settings. Unity’s [training configuration reference](https://docs.unity3d.com/Packages/com.unity.ml-agents@4.0/manual/Training-Configuration-File.html) defines the configuration semantics.

Record seeds, config, source revision, package lock, combat-tuning hash, opponent mixture, and evaluation results alongside each exported model.

### Runtime and exploit controls

- Ship ONNX through ML-Agents `BehaviorParameters` in **Inference Only** mode; no Python process or online learning.
- Start with CPU inference for the small network; profile before changing execution device.
- Reject a model whose contract/tuning metadata does not match.
- Test always-guard, rapid guard tapping, repeated dodge, corner camping, retreat forever, wall attacks, combo spam, and intentionally delayed attacks.
- Judge models by held-out win rate, damage exchange, timeout rate, action diversity, defense timing, and illegal-request count—not training reward alone.
- Do not reward-limit legitimate gameplay mechanics at runtime. Reward caps belong only to training bookkeeping.

## 5. Implementation sequence and acceptance gates

Each phase must compile and remain testable before the next begins.

| Phase | Work | Acceptance gate |
|---|---|---|
| **0. Baseline and prefab repair** | Reproduce GameScene; verify missing input component; make actor prefab dependencies explicit; initialize enemy references before constructing states; retain valid scene overrides. Align NavMeshAgent radius with the physical capsule after checking scale—the saved scene radius is 2 versus capsule radius 0.5. | Fresh prefab instances work; movement, lock-on, existing attacks, death, chase, and investigation operate without exceptions. |
| **1. Shared combat foundation** | Add combatant identity, definitions, runner, resolver, fixed-step coordination, and result events. Make health damage finite/nonnegative and death idempotent; unsubscribe event handlers. | Unit tests cover damage, duplicate contacts, faction filtering, phase boundaries, and death precedence. Existing player damage uses exactly one route. |
| **2. Player migration and defense** | Add `Block` to the assigned input asset and regenerate its wrapper. Migrate light/heavy/dodge timing; add guard/posture/stagger; arbitrate root motion and state writes. | RMB press/hold/release works at different frame rates; animations can be removed without hanging actions; movement/camera regressions pass. |
| **3. Normal combat enemy** | Add enemy motor, combat FSM state, perception snapshots, scripted brain, two attacks, and procedural feedback. | Enemy approaches, spaces, attacks, defends, recovers, and can be punished; walls/occlusion/death behave correctly. |
| **4. Controller boundary** | Add Inspector/runtime mode host and a test brain exercising the shared interface. | Switching preserves resources/cooldowns, cannot cancel commitment, and never allows two command owners. |
| **5. ML compatibility and environment** | Pin package/trainer snapshot, pass export/inference spike, add agent adapter, observation tests, training scene, opponent adapters, atomic reset, and metrics. | Heuristic agent can execute every action; 1,000 resets without leaked state or cross-arena interactions; headless and rendered results match for scripted inputs. |
| **6. Training and integration** | Run curriculum, evaluate checkpoints, export selected model, configure runtime inference and fallback. | Model passes held-out tests and works in GameScene alongside a normal-mode enemy. |

### Existing modification targets

Group changes by responsibility:

- **Shared damage:** `HealthComponent`, `HitDetection`, `Sword`.
- **Player integration:** `Player`, `PlayerInputComponent`, attack/heavy/dodge states, state registration, `RootMotionHandler`, `StateRootMotion`, and `LockOn`.
- **Enemy integration:** `enemy`, `EnemyPerceptionNav`, enemy state registration and chase/wander adapters, `PlayerRefManager`.
- **Assets:** assigned input-action asset/generated wrapper, actor prefabs, GameScene wiring, and animation controller bindings.

New shared combat classes belong together under a combat namespace; enemy brains and ML adapters remain separate. Do not introduce ML package types into shared combat interfaces. Add assembly definitions when adding tests/ML separation, preserving one-way dependencies: shared gameplay → actor integration → ML adapter.

### Required regression scenarios

- Front/rear/side guard contacts; exact deflection opening/closing ticks; tap spam and held guard.
- Posture break on a block, successful deflection just below maximum defender posture, attacker reaching maximum through deflection, and health-zero/posture-break on the same tick.
- Multiple hurtboxes, nested arena parents, overlapping at active start, fast movement, walls, and simultaneous trades.
- Dodge contact at both invulnerability boundaries; no second hit from the same swing after invulnerability ends.
- Missing Animator, missing clip, interrupted animation, death during active attack, and root-motion transitions.
- Navigation unavailable/off-mesh, unreachable last seen position, visibility loss, and target destruction.
- Input at 30/60/144 FPS with unchanged fixed timestep.
- Mode changes during guard, windup, active, recovery, stagger, and pending inference.
- Training reset while a hit is pending; two arenas attacking simultaneously; observation masking through walls.
- Repeated Play Mode entry and actor enable/disable without duplicate subscriptions.

### Final ML acceptance

Evaluate the selected checkpoint over at least 200 fixed held-out episodes per opponent family:

- Zero crashes, invalid observations, cross-arena contacts, or accepted illegal actions.
- Beat passive/predictable basic opponents in at least 90% of episodes.
- Achieve at least 50% wins against the balanced held-out scripted opponent, with draws reported separately.
- Timeout rate below 15% in full-duel evaluation.
- No tested exploit strategy exceeds 80% wins against the learner.
- Human playtest confirms readable telegraphs, punishable recovery, and no input-reading behavior.

If learning fails these gates, keep normal AI as the default and report the failing metrics. Do not compensate by granting the ML enemy faster timing, privileged observations, or bypasses around shared combat rules.

## 6. Required task completion and validation protocol

Every implementation task must update IMPLEMENTATION_PROGRESS.md and end with the following completion report. Distinguish implementation completion from validation completion; writing code alone never establishes success.

### Task status

Choose exactly one: `SUCCESS`, `PARTIAL SUCCESS`, `BLOCKED`, or `FAILED`.
Use SUCCESS only when implementation is complete and all applicable acceptance criteria are verified PASS. Use PARTIAL SUCCESS when useful implementation is complete but validation or work remains; BLOCKED when a dependency prevents required progress; FAILED when execution or validation establishes the task did not meet its requirements.

### What was implemented

Briefly identify files created, files modified, and major functionality implemented. For documentation-only tasks, identify documentation changes.

### What the model verified itself

List only checks actually executed, each marked PASSED or FAILED, with its scope and useful evidence (test name, command, result/log path). Separate current-session checks from previously recorded results. Never claim compilation, Play Mode, visual inspection, control presses, training, or any other action without actually performing it. Static inspection does not establish runtime behavior; starting Play Mode does not establish successful gameplay. Unexecuted checks belong under acceptance criteria, not passed checks.

### Manual tests required from user

Explicitly state whether manual testing is needed. If none, write exactly:

`Manual testing required from user: NONE`

Otherwise give each test a stable ID and these fields: `Purpose`, `Setup`, `Steps`, `Expected result`, `Failure signs`. Specify relevant Unity scene, GameObject/prefab, Inspector settings, Play Mode requirement, exact controls, expected behavior/debug visuals; for external/ML tools include exact relevant commands/configuration/procedure. Do not delegate checks the model can reasonably execute itself. If a reasonably automatable check was not run, record that limitation and leave acceptance unverified; do not disguise it as an inherently manual requirement. User tests should address the remaining human-observed behavior.

### Acceptance criteria

List every relevant criterion separately, assigning exactly one status: `PASS`, `FAIL`, `NOT VERIFIED — USER TEST REQUIRED`, or `NOT APPLICABLE`. Link evidence for PASS/FAIL and test IDs for pending manual criteria. Do not silently pass untested criteria or infer a full gate from a narrow smoke test. Under this reporting vocabulary, an unverified automated criterion also uses `NOT VERIFIED — USER TEST REQUIRED`, with an explicit note that model-run automation is still owed rather than requesting unnecessary user testing.

### Problems and deferred work

Separate `Blocking issues` from `Non-blocking / Deferred TODOs`; state NONE where appropriate. Non-blocking polish must not delay progress. Identify unresolved validation that could reveal a blocker separately from confirmed defects.

### Recommendation

End with exactly one: `READY FOR NEXT PROMPT`, `RUN MANUAL TESTS BEFORE NEXT PROMPT`, or `FIX CURRENT TASK BEFORE CONTINUING`.
If a pending manual test could reveal a blocking problem, choose RUN MANUAL TESTS BEFORE NEXT PROMPT. Choose FIX CURRENT TASK BEFORE CONTINUING for known blocking failures or required unfinished model work; READY FOR NEXT PROMPT requires no unresolved blocking work or blocking validation.

### Persistent handoff

Keep these progress sections current: Current phase; Completed tasks; Current task; Validation performed; Pending manual tests; Manual test results; Known blockers; Deferred TODOs. Include task status, acceptance statuses, and recommendation as needed. Required manual tests remain pending until the user reports results. Record reported results under Manual test results; remove passed tests from Pending manual tests and retain failed tests as blockers where appropriate. Never invent user results. Historical findings in this plan describe the inspected pre-implementation baseline; the progress file records subsequent implementation and validation.

</proposed_plan>
