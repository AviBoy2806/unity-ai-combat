# Current assignments - 18 November 2026

The supplied assets in `Assets/Missing Clips and Sounds/` are now installed. This status supersedes the older missing-asset checklist retained below.

| Asset | Current assignment |
|---|---|
| player impact.fbx (Humanoid, .7 s) | Player HitReaction / Recoil; also full posture-break reaction |
| mutant impact.fbx (Generic, .7 s) | Enemy HitReaction / Recoil / existing Stunned state's Motion |
| Existing Player/Anims/block idle.anim | Player Guard state; holds its end pose until guard release |
| whoosh.wav | Both actors' Light Attack Sound and Dodge Sound |
| heavy whoosh.wav | Both actors' Heavy Attack Sound |
| thud.wav | Both actors' Hit Sound and Death Sound |
| metal clash.wav | Both actors' Block Sound and Posture Break Sound |
| parry.wav | Both actors' Deflect Sound |

Both actor prefabs and GameScene instances have these sound assignments and AudioSources. Evade Sound deliberately stays empty because Dodge already plays a whoosh; this avoids double playback when contact occurs during a dodge. The HUD carries action/health/posture/controller status. Debug geometry is off by default; actual collision is intact. Both reaction clips are nonlooping, 1x and imported in place; the runner owns exit timing, including a held end pose for full stun. Existing player combo states/transitions, Attack 2 speed and heavy foot/root-motion settings were preserved.

`whoosh.wav` and `thud.wav` have identical bytes, so their current sound is identical. Replace either named file if you intended different audio. Both FBX files also share raw content but retain separate Humanoid/Generic import configurations.

Remaining OPTIONAL assets: a dedicated enemy dodge clip, separate reaction variants, footsteps/landing/ambient audio. None is needed to use the newly connected presentation. User listening/animation feel acceptance remains pending; see `Logs/Presentation20261118/HANDOFF.md`.

---

# Historical assignment guide (before supplied assets)

# Animations and sounds to add

Current assignment guide — 17 November 2026. This is optional presentation work for the existing combat system, not a new gameplay requirement. The stable demonstration uses the current bindings. No assets were generated or downloaded. Exact imported inventory: [asset-audit.txt](Logs/RuntimeIntegration20261117/asset-audit.txt).

## What is already connected

| Actor / location in GameScene | Connected presentation |
|---|---|
| `Player/Model`, Animator, `Assets/Models/Player/PlayerAnimController.controller` | GroundMovement and Locked On 0 locomotion blend trees; Attack 1/2/3; Heavy Attack; dodge; Jump; Fall; death |
| `Enemy/Mutant`, Animator, `Assets/Models/Enemy/Anims/Enemy Animator Controller.controller` | loco blend tree (idle/walk/run), attack, block, dead; Stunned currently uses idle as a placeholder |
| `Player` and `Enemy`, Combat Presentation | Existing guard arc, damage-volume display, health/posture, result colors and action labels. Optional animation bool names and sound fields now live here. |
| `Player` and `Enemy`, AudioSource | Scene assignment points installed: Play On Awake off, Loop off, 3D blend 1, Doppler 0, linear rolloff, Min Distance 1, Max Distance 20. No clips assigned. Main Camera already has AudioListener. No audio mixer asset is present. |

Player connected clip paths under `Assets/Models/Player/Anims/`: `idle.anim`, directional `walk*.anim` and `run*.anim`, `attack combo 1.anim`, `attack combo 2.anim`, `attack combo 3.anim`, `heavy attack.anim`, `dodge.anim`, `idle to jump.anim`, `Fall.anim`, `death.anim`. Attack 2 and all three combo states remain **1x**. Dodge remains state speed **3**. Heavy remains **1x**, with the existing script-driven XZ root motion and foot-ground correction.

Enemy connected clips under `Assets/Models/Enemy/Anims/`: `idle.anim`, `walk.anim`, `run.anim`, `attack.anim`, `block.anim`, `dead.anim`. Both light and heavy currently share `attack` at **2x**; this is an intentional existing binding, not a missing functional asset. Stunned reuses `idle.anim` at 1x and has an existing exit when the `Stunned` Bool becomes false.

The scene actors are complete. Base prefabs are `Assets/Prefabs/Player.prefab` and `Assets/Models/Enemy/Enemy.prefab`. Their existing animation controllers are shared with the scene and training actors. AudioSources added by this task are **scene additions**, not silently applied to these prefabs. To reuse audio on new prefab instances, add an AudioSource on the prefab root, assign Combat Presentation > Combat Audio Source, then assign its clips. Do not apply all unrelated GameScene overrides to a prefab.

## Minimal animation assignment points

Combat Presentation now writes these optional **Bool** parameters only if they exist in the assigned Animator:

| Inspector field | Default parameter name | True while |
|---|---|---|
| Guard Animator Bool | `Blocking` | Shared runner is guarding |
| Stagger Animator Bool | `CombatStagger` | Shared action is Stagger (including full posture break) |
| Recoil Animator Bool | `CombatRecoil` | Shared action is Recoil |
| Dodge Animator Bool | `CombatDodge` | Shared action is Dodge |

Missing parameters/states/clips do not affect combat. These values follow gameplay, never determine it. Enemy full posture break already uses `Stunned` through EnemyStunnedState; keep that route for its dedicated stun pose. Do not add a competing enemy `CombatStagger` transition that overrides `Stunned`. Short hit reaction transitions can require `Stunned == false`.

For optional states below, use a local state speed of **1**, Animator global speed **1**, no root displacement for guard/reaction clips, and no animation events for damage, windows, commitment or completion. Preserve all existing player combo transitions, state speeds, Attack trigger handling, late-combo gates, heavy root-motion import settings and RootMotionHandler. Do not turn on automatic enemy Animator root motion.

### A1 — Player guard pose: available but unassigned

- [ ] **Optional polish.** Functional guard is already visible through GUARD/arc and shared defenses.
- Suitable existing clip: **`Assets/Models/Player/Anims/block idle.anim`**, 1.383333 seconds, 60 FPS. Currently **not looping**, and not assigned to a player Animator state. It needs a visual loop check; do not assume its seam is clean.
- Destination: **GameScene > Player/Model > Animator**; controller **`Assets/Models/Player/PlayerAnimController.controller`**, new state **`Guard` > Motion**. Root **Player > Combat Presentation > Guard Animator Bool = Blocking**. The player controller currently has no `Blocking` parameter.
- Settings: Humanoid rig compatible with Player avatar; state speed 1; loop a duplicate of the clip if desired; bake guard XZ/Y/rotation motion into the pose. Do not modify attack/heavy imports.
- Assignment:
  1. Stop Play Mode. Duplicate `block idle.anim` into a personal presentation folder (for example `Assets/Presentation/PlayerGuard.anim`). Set its Loop Time if a held cycle is desired; inspect the seam.
  2. Open the existing player controller. Add Bool `Blocking` (default false), and state `Guard`; assign the duplicate to Motion, speed 1.
  3. Add Any State -> Guard, `Blocking == true`, Has Exit Time off, short fixed blend about .08 s, Can Transition To Self off. Keep the existing Die route.
  4. Add Guard -> GroundMovement, `Blocking == false`, Has Exit Time off, short fixed blend about .08 s. Do not edit any existing combo transition.
  5. On Player > Combat Presentation leave Guard Animator Bool as `Blocking`. Save only the intended controller/clip changes.
- Verify: hold/release RMB, walk while holding, then release and attack/dodge. Guard must exit promptly, combos must restart, Attack 2 stays 1x, and guard legality/windows remain unchanged. If the stationary guard clip slides while walking, that is optional locomotion polish; a masked upper-body layer or guard blend tree is a later Animator choice, not required tomorrow.

### A2 — Enemy full posture-break pose: dedicated clip missing

- [ ] **Optional polish.** Explicit EnemyStunnedState, timer, stationary motor and STUN display already function. Current state is connected to `Assets/Models/Enemy/Anims/idle.anim`; that is a placeholder, not a dedicated stun animation.
- No suitable dedicated stun clip exists in Assets. Obtain a clip for the enemy's **Generic** Mutant skeleton; the player's Humanoid clips are not drop-in substitutes.
- Destination: GameScene > **Enemy/Mutant > Animator**; controller **`Assets/Models/Enemy/Anims/Enemy Animator Controller.controller` > Stunned > Motion**. Also affects Enemy prefab instances using this controller.
- Settings: Generic rig with matching transform paths; in-place; 1x. A held pose or loop is suitable for variable stun duration. Existing `Stunned` Bool and `Stun finished` transition (`Stunned == false`, no exit time, .1 s fixed blend) stay authoritative. Do not create an animation exit-time completion rule.
- Assignment:
  1. Import a compatible in-place clip; retain the enemy's existing avatar/skeleton setup.
  2. Open the enemy controller, select `Stunned`, drag the clip to Motion; keep speed 1.
  3. Keep the existing entry/exit parameter behavior and death route. Enemy Animator Apply Root Motion stays off.
- Verify: fill posture in Normal, observe the pose through the countdown, land ordinary hits, then watch recovery and a death case. The model must not move the enemy root or escape early. User confirmation of those subchecks is still separate from entry confirmation.

### A3 — Player hit stagger / recoil: dedicated clips missing

- [ ] **Optional polish.** Existing colors/countdowns and fallback to locomotion are functional. No dedicated matching hit-stagger or deflection-recoil clip exists.
- These are **choices**: use one suitable reaction clip for both, or obtain distinct clips. Full posture break may reuse that pose or use another held pose; every variant is not mandatory.
- Destination: GameScene > **Player/Model > Animator**, existing player controller, new `HitReaction` / `Recoil` state Motion. Player root > Combat Presentation > **Stagger Animator Bool = CombatStagger**, **Recoil Animator Bool = CombatRecoil**.
- Settings: Humanoid, Player avatar; in-place; speed 1; nonloop for short reactions (end pose may hold until the runner releases the bool), or loop a suitable held full-break pose. No clip timer drives gameplay.
- Assignment:
  1. Import a compatible reaction; add Bool `CombatStagger` and/or `CombatRecoil`, default false.
  2. Create the corresponding state(s), assign Motion at 1x. Add Any State entries for the matching true Bool, no exit time, short fixed blend, no self-transition. Retain death precedence.
  3. Exit to GroundMovement when the matching Bool is false, no exit time. Keep existing combo graph untouched. If two reaction booleans are used, only the runner's current action is true.
- Verify: receive a hit in windup and recovery; get deflected; wait for the shared countdown to finish; attack again. Death must leave the reaction. Missing/disabled animation must still allow the action timer to finish.

### A4 — Enemy ordinary hit stagger / recoil: dedicated clips missing

- [ ] **Optional polish.** Existing tint/labels and interruption out of attack work; no suitable dedicated Generic reaction clip is present.
- Destination: GameScene > **Enemy/Mutant > Animator**, enemy controller, new `HitReaction` and/or `Recoil` state Motion. Enemy root > Combat Presentation > **Stagger Animator Bool / Recoil Animator Bool**.
- Settings: Generic Mutant skeleton; in-place; speed 1; nonloop short reaction or held pose. Add Bool `CombatStagger` / `CombatRecoil` as needed. Add `Stunned == false` to ordinary stagger entry so the existing full-break state wins. Keep Any State death precedence; allow transition into existing Stunned when `Stunned == true`.
- Assignment:
  1. Import one Generic reaction clip or two alternative clips for hit/recoil.
  2. Add only the states/Bool parameters needed. Entries use true Bool, no exit time, no self-transition; ordinary stagger also requires Stunned false.
  3. Exit to loco when its Bool becomes false; permit full stun/death to interrupt. Keep the existing attack/block graph and 2x attack speed.
- Verify: normal hit interrupts eligible attack phases, recoil is distinct from full stun, Stunned cannot be overridden by the short reaction, and death wins. These transitions are presentation only.

### A5 — Enemy dodge: clip missing

- [ ] **Optional polish.** Shared dodge movement, cooldown and invulnerability already work with the existing visual feedback. No enemy dodge/roll clip exists.
- Destination: **Enemy/Mutant > Animator**, enemy controller, new `Dodge` state Motion; Enemy > Combat Presentation > **Dodge Animator Bool = CombatDodge**.
- Settings: Generic Mutant skeleton; in-place; nonloop; 1x for a clip authored around the .34 s dodge. If the clip is a different length, visual speed is an authoring choice; never change shared duration or invulnerability to fit it. EnemyMotor remains sole movement authority.
- Assignment:
  1. Import a compatible in-place dodge; add Bool `CombatDodge`, default false, and state `Dodge`.
  2. Enter when CombatDodge true, no exit time, short blend, no self-transition. Exit to loco when false; allow Stunned/death precedence.
  3. Leave enemy Apply Root Motion off. Do not add a second dodge movement component.
- Verify: a legal dodge enters/leaves the pose without sticking or extra travel; cooldown and blue invulnerability cue match shared gameplay. Numerical tests do not establish the new clip looks good.

### Existing alternatives — no new requirement

`Assets/Models/Player/Anims/attack.anim`, `Jump Forward.anim`, `Jump.anim`, `jump to fall.anim` are available alternatives, not missing mandatory bindings. `fall2.anim` and the model FBX's imported take are about one frame long and are not useful complete fall/reaction clips. A separate enemy heavy animation is an optional alternative to the currently shared `attack.anim`; separating light/heavy visual routing would require a **scoped code/Animator change**, not just dropping a clip into the shared attack state. Do not replace player attack clips or retime them before the demonstration: that requires renewed root-motion/contact/blend validation and later training-movement review.

## Sounds — no audio clips exist in Assets

All sounds below are **optional**. You still need to obtain/import audio. There are no available-but-unassigned sound assets. One clip may serve several related fields; distinct light/heavy or block/deflect variants are alternatives, not requirements.

Every row below uses **GameScene > Player or Enemy root > Combat Presentation**, with that same root's **AudioSource** assigned in **Combat Audio Source**. Apply the shared steps below separately to each actor you want to hear.

| Checklist | Action/outcome | Exact field | Playback trigger |
|---|---|---|---|
| [ ] S1 | Light attack whoosh/grunt, optional; missing asset | Light Attack Sound | Once per accepted shared attack ID at action start; not each damage frame |
| [ ] S2 | Heavy attack, optional alternate to S1; missing asset | Heavy Attack Sound | Accepted heavy start; falls back to Light Attack Sound when absent |
| [ ] S3 | Dodge swish/effort, optional; missing asset | Dodge Sound | Authoritative Dodge action starts |
| [ ] S4 | Health-hit impact, optional; missing asset | Hit Sound | This actor is defender of unique Hit resolver result |
| [ ] S5 | Guard impact, optional; missing asset | Block Sound | This actor receives unique Block result |
| [ ] S6 | Deflection ring, optional alternate metal sound; missing asset | Deflect Sound | Defending actor receives unique Deflect result; attacker callback does not duplicate playback |
| [ ] S7 | Evaded contact, optional; missing asset | Evade Sound | Defender receives unique Evade result, if an attack volume actually contacts it during invulnerability |
| [ ] S8 | Posture break impact, optional; missing asset | Posture Break Sound | Unique PostureBreak resolver result; raw debug posture additions have no resolver result and do not play it |
| [ ] S9 | Death effort/fall, optional; missing asset | Death Sound | Authoritative Dead action entry; repeated death does not restart it |

**AudioSource / mixer settings for S1–S9:** scene sources are already assigned. Keep AudioSource Clip empty (PlayOneShot uses the fields), Play On Awake off, Loop off, Pitch 1, Volume 1, Spatial Blend 1, Doppler 0, Linear Rolloff, Min Distance 1, Max Distance 20. Combat Presentation > Sound Volume controls one-shot gain (default 1). The source uses the default output; **no mixer asset/group is required or currently connected**. You may later route it to a mixer group you create. Keep only the existing Main Camera AudioListener for this scene. Adjust gain while listening; do not change combat timing to synchronize audio.

For **each** remaining sound row:

1. Stop Play Mode. Import a short WAV/OGG/MP3 into a folder such as `Assets/Audio/Combat/`. For a small 3D one-shot, mono/Force To Mono and a non-streaming short-clip load type are reasonable; audition gain/clipping first.
2. Select the intended actor root (`Player` or `Enemy`) in **Assets/Scenes/GameScene.unity**. In Combat Presentation, drag the imported clip into the row's exact field. Confirm Combat Audio Source references its root AudioSource. A field may deliberately stay empty.
3. Save the intended scene assignment. For reusable spawned actors, repeat in the appropriate prefab root and add/assign an AudioSource there, then save that prefab only.
4. Enter Play Mode and cause the row's event: LMB attack; running LMB heavy; Shift dodge; receive a hit; hold RMB for Block; fresh RMB for Deflect; contact during dodge for Evade; fill posture for PostureBreak; health zero for Death. Listen once from near the actor, then at a distance.
5. Confirm no per-frame or multiple-hurtbox duplicates, correct actor position/attenuation, no startup sound, and no gameplay stall if the field is cleared. Stop Play Mode before editing model/component assets.

Whoosh S1/S2 currently plays at **action start**, so choose a short windup/effort clip. A swing-apex whoosh or footsteps are optional cosmetic timing improvements. They currently have **no animation-event receiver**; adding one would require a small code change and a suitable event on the model Animator GameObject. Do not add those events to control hit windows/damage or also play the same whoosh from two routes. Footsteps, jump/landing, ambient music and UI sounds are deferred choices, not missing requirements for this task.

## Repeatable no-code import / assign / test workflow

**One new sound:** 1. Import it under Assets/Audio/Combat. 2. Drag it into Player or Enemy > Combat Presentation > the matching Sound field. 3. Check its already-linked AudioSource. 4. Play the corresponding action/result once and repeat with two hurtboxes/reenable if relevant. 5. Save the scene/prefab outside Play Mode. **No code or Animator editing required.**

**One new reaction animation:** 1. Import for the correct rig (Player Humanoid / Enemy Generic). 2. Create the optional reaction state and matching Bool in the existing controller using A3/A4. 3. Assign Motion and bool-conditioned entry/exit; keep existing attack graph. 4. Match Combat Presentation's parameter-name field. 5. Trigger the reaction and confirm it releases on the shared timer and death wins. **Animator editing required; no gameplay code required for the supplied optional bool hooks.** For A2, replacing Motion in the already-existing Stunned state needs no new parameters or code.

Obtain only the optional assets you want for the demonstration. Preserve a copy of the working scene/controller before personal presentation edits. Re-run the relevant combo/root-motion/movement checks after animation graph edits before claiming the changed assets are validated. New user-assigned audio/animation has not been visually/audibly verified by the automated synthetic-hook tests.
