# Historical implementation evidence and manual procedures

Snapshot retained from before Phases 3/4. Statements about the current task, blocked gates, absent enemy AI, debug setup, and recommendations below are superseded by IMPLEMENTATION_PROGRESS.md. Historical PASS results retain their original scope. The latest combo-blending fix is now user-confirmed only; other manual results are unchanged.

# Implementation progress

## Current phase

**Current Phase 3/4 request: BLOCKED by unresolved prerequisite manual validation.** No Phase 3/4 code was implemented. The previous task's COMBO-BLEND test has no reported user result; it covers rendered combo sequencing/speed, interruption/death cleanup, heavy movement/feet and control regressions that could block subsequent enemy integration. P12-GUARD-RETEST and related earlier manual tests also remain unresolved. They are not assumed passed.

Phases 1 and 2 are implementation-complete, pending human control/presentation validation. Task status: PARTIAL SUCCESS. Enemy combat AI/Phase 3 has not started. Historical Phase 0 evidence and pending user tests are retained below; no user results have been invented.

## Latest fix — RMB defense release

- Status: PARTIAL SUCCESS (fix implemented and isolated input regression passed; in-scene human retest pending).
- Cause: PlayerInput SendMessages forwards Button performed events but not canceled events. Block had no PressAndRelease interaction, so RMB release never reached OnBlock.
- Changed: Block binding now uses Press(behavior=2), matching existing press/release button bindings; generated PlayerInputAction.cs mirrors the source asset. CombatValidation now injects mouse events and asserts guard exit without using dodge.
- PASS: isolated Unity 6000.6.0f1 compilation and Play Mode test with the actual input asset, PlayerInput SendMessages, PlayerInputComponent and CombatActionRunner: three RMB press/hold/release cycles; release clears guard and movement ownership. Logs/GuardReleaseIsolated.log: GUARD_RELEASE_TEST_PASS, exit 0. Isolated fixture retained under Logs/GuardValidationProject; no user Editor was closed.
- NOT VERIFIED: full GameScene suite after this fix. Batch attempts could not open the already-open project; no claim of a current full-scene pass.
- Acceptance: input release delivery and runner guard exit PASS; rendered GameScene defense exit NOT VERIFIED — USER TEST REQUIRED.
- Blocking issues: no confirmed remaining code defect; GameScene confirmation pending. Deferred: existing Phase 1/2 manual tests remain pending.

## Defense debugging controls

- Implemented: CombatDebugControls on Player prefab (and CombatSetup installation), default checkbox OFF. Player Inspector > Combat Debug Controls > Enable Defense Debugging enables hotkeys in Editor/development builds; release builds ignore them. No autonomous enemy combat decisions added.
- Keys (editable in Inspector): F6 schedules enemy light; F7 schedules heavy; F8 toggles opponent guard; F9 selects front/rear placement for subsequent commands. Attack Delay defaults to 2 seconds; Test Distance to 1.3 m. On-screen legend/status appears only when enabled.
- Opponent can be assigned explicitly; otherwise the nearest living opposing combatant in the same arena is selected. First command temporarily pauses its existing enemy/perception/navigation controllers, freezes Rigidbody motion, and positions it in reach. Attacks use existing definitions, shared commitment and damage rules; busy requests are rejected. Player inputs remain RMB guard/deflect, Shift dodge, LMB attack.
- Turning the checkbox off or disabling the component cancels queued debug attacks, releases opponent guard, restores its original transform/physics and paused controllers. An already committed attack still follows shared timing. Health/posture are not reset; restart Play Mode for fresh resources. Do not save Play Mode test changes.
- PASS: isolated Unity compilation and Play Mode fixture: checkbox-off commands inert; enabled command positions/freezes opponent; scheduled attack enters shared runner; component disable restores saved opponent pose/physics. Evidence: Logs/DefenseDebugValidation.log, DEFENSE_DEBUG_TEST_PASS, exit 0. Fixture: Logs/GuardValidationProject/Assets/Editor/DefenseDebugTest.cs.
- PASS: static player-prefab wiring/default-off inspection. NOT VERIFIED — USER TEST REQUIRED: physical function keys, checkbox-off restoration and overlay in GameScene. Full main-project batch testing remains unavailable while the user Editor holds it open.
- Non-blocking TODO: visual placement calibration on uneven terrain; keep test actors on clear, level floor. No enemy AI/ML phase started.

### Pending manual test: DEBUG-DEFENSE

- Purpose: Verify the checkbox, hotkeys and feedback in the actual scene.
- Setup: Stop Play Mode and allow import/compilation. Open Assets/Scenes/GameScene.unity; enter Play Mode. Select Player and tick Enable Defense Debugging on Combat Debug Controls. Optionally assign Enemy to Opponent. Click Game view.
- Steps: Press F6, hold RMB early for block; press F6 again and time RMB just before the red active volume for deflection. Press F7 for heavy. Press F9 then F6 to test rear coverage. Press F8 to toggle enemy guard and attack it with LMB. Untick Enable Defense Debugging, return to Game view and press F6/F7/F8/F9 again.
- Expected result: Key legend/countdown while enabled; selected enemy moves into reach and stops exploration; attacks execute after 2 s using normal windup/active/recovery; guard/deflect/dodge indicators remain visible. F8 gives a defensive opponent for attack tests. Checkbox off removes legend, cancels pending attacks, restores opponent position/exploration/physics, and hotkeys have no effect.
- Failure signs: Inputs work with debugging off; pending strike fires after disabling; opponent never restores; missing legend or attacks despite a living/free target; Console exceptions. Restart Play Mode if either actor dies.
- Manual results: none reported for DEBUG-DEFENSE. Existing P12 and guard-release retests remain pending. Recommendation: RUN MANUAL TESTS BEFORE NEXT PROMPT.

## Latest fix — post-attack drift and sword collider audit

- Task status: PARTIAL SUCCESS; code fix and isolated damage regression complete, in-scene movement retest pending.
- RootMotionHandler now rejects attack/dodge clip root motion even after shared gameplay ownership ends (including outgoing/incoming blend states), and rejects idle-state root motion. PlayerCombatAdapter clears stale root-motion flags on action changes; completion still zeroes horizontal velocity. Authored running-heavy movement remains during its committed action.
- Found swordCollider beneath the player armature bone. It was already runtime-disabled and Sword.OnHit was inert, so it was not a second damage source. Also disabled the BoxCollider in the saved Player prefab; retained inert legacy binding compatibility rather than deleting referenced hierarchy objects.
- PASS: isolated Unity compilation/Play Mode regression with actual Sword/HitDetection/CombatWorld code: collider disabled on startup, EnableHitbox cannot enable it, Sword.OnHit does no damage, authoritative light deals exactly 10 health damage across its full duration with the legacy sword and multiple defender colliders present. Evidence: Logs/SwordDamageValidation.log, SWORD_DAMAGE_TEST_PASS, exit 0; reproducible fixture under Logs/GuardValidationProject/Assets/Editor/SwordDamageTest.cs.
- PASS: static damage-path audit; health mutation calls remain only in CombatResolver. Static inspection confirms blend/idle root-motion gates and serialized sword collider disabled.
- NOT VERIFIED — USER TEST REQUIRED: main GameScene post-attack drift and visual recovery. Added horizontal-stop assertions after light/heavy completion in CombatValidation, but did not rerun the main suite while the user's Editor holds the project open. No claim of main-scene compilation/Play Mode verification for this fix.
- Blocking issues: no confirmed remaining duplicate-damage defect; drift fix awaits in-scene confirmation. Deferred: visual attack blending calibration if needed after feedback.

### Pending manual test: ATTACK-STOP

- Purpose: Confirm attacks do not leave residual player movement.
- Setup: Stop Play Mode, allow import/recompile, open GameScene and enter Play Mode. Use a clear floor area away from enemy physical contact. Select Player to observe root Transform if needed.
- Steps: Stand still, left-click once, then release movement controls through recovery. Repeat the three-hit light combo. Run then heavy-attack, release movement controls and wait for landing/recovery. Finally walk immediately after a light attack.
- Expected result: No horizontal drift after recovery with movement input released; heavy retains its deliberate committed leap/lunge; walking resumes normally. Player root Transform should stop, independently of any remaining visual weapon animation. Existing damage test already verified one light hit removes 10 health, not 20.
- Failure signs: Player root continues translating after recovery without input/contact, walking becomes stuck, or Console errors. Distinguish model-only animation movement from root position changes in the report.
- Manual results: user reported post-attack movement; no passing retest received. Previous pending tests remain pending. Recommendation: RUN MANUAL TESTS BEFORE NEXT PROMPT.

## Latest correction — attack XZ root motion and heavy feet

- Status: PARTIAL SUCCESS; implementation and isolated compilation/buffer tests PASS, rendered heavy-foot placement pending.
- User correction supersedes the earlier attack root-motion suppression: attack XZ motion is again enabled by StateRootMotion. RootMotionHandler forwards planar animation displacement into AttackRootMotionBuffer; PlayerCombatAdapter consumes it once per physics tick as the sole Rigidbody movement writer. Authored movement remains the fallback when animation/handler is unavailable. Damage windows/completion still use shared gameplay timers, not Animator normalized time.
- Fixed StateRootMotion blend ownership so an outgoing state cannot clear the incoming state's settings. Removed action-change clearing of these script-driven settings. Buffered displacement clears on action changes; outgoing combat clip root motion cannot resume after shared recovery ends.
- RootMotionHandler now lifts the visible humanoid model only when foot/toe samples penetrate Ground during the Heavy Attack clip/blends. Uses humanoid sole heights plus Sole Clearance (default 0.035 m); correction does not move the physics capsule, override physics Y, or force airborne feet down. Removes its own offset when the heavy visual ends or handler disables.
- Changed: RootMotionHandler, StateRootMotion, PlayerCombatAdapter, Player root-motion ownership field; added AttackRootMotionBuffer. Sword damage behavior unchanged.
- PASS: isolated Unity 6000.6.0f1 compilation of runtime scripts relevant to this change (CameraLook excluded from minimal fixture because it needs Cinemachine). RootMotionBufferTest.Run verifies planar distance conservation at simulated 30/60/144 FPS, once-only consumption, rejection of vertical animation displacement and clearing stale movement. Logs/RootMotionValidation.log: ROOT_MOTION_TEST_PASS, exit 0.
- NOT VERIFIED — USER TEST REQUIRED: rendered feet/sole clearance, attack animation XZ travel and final recovery stop in GameScene. No main-scene visual test claimed. No confirmed blocking compile defect remains. Earlier drift suppression is superseded by this user-requested root-motion arbitration.
- Deferred: baked-motion parity for future animation-free ML training; visual slope/foot clearance calibration if needed. No enemy combat AI work performed.

### Pending manual test: HEAVY-FEET-ROOT

- Purpose: Verify restored animation travel and no visible floor penetration.
- Setup: Stop Play Mode, allow compilation, then enter GameScene on clear level Ground. Use Scene view side-on to see soles; RootMotionHandler is on the player model. Keep its Ground mask and default Sole Clearance initially.
- Steps: Perform lights/combo; while already running, left-click for heavy, then release movement keys and observe jump, landing and recovery. Repeat locked/unlocked. Inspect Player root position versus model feet. Disable Animator temporarily and repeat an attack to check fallback completion, then restore it.
- Expected result: Animated attacks retain script-driven XZ travel; heavy soles stay above the floor when landing/crouching; jump remains airborne naturally. After recovery, horizontal movement stops without input. Animation-free attacks still finish. No Console errors.
- Failure signs: Soles sink below Ground, model accumulates upward offset between attacks, no animated XZ travel, doubled travel, lingering root drift, or exceptions. Report whether only the mesh moves or the Player root moves.
- Manual results: user reported heavy feet clipping and requested restoration of XZ root motion; no passing retest yet. Existing pending tests remain pending. Recommendation: RUN MANUAL TESTS BEFORE NEXT PROMPT.

## Latest correction — heavy XZ flag and combo attack 2 speed

- Status: PARTIAL SUCCESS; changes implemented and isolated tests PASS, GameScene visual retest pending.
- Corrected the actual Heavy Attack Animator state's StateRootMotion behaviour (fileID 3997525512411490275) from xzPlayer=0 to xzPlayer=1. Heavy animation now supplies planar displacement through the existing buffer/motor; no second displacement source added. Vertical motion/foot correction unchanged.
- AttackDefinition now exposes matchAnimationToAction (default true) and PlaybackSpeed. Light2 opts out so the authored 0.333 s clip plays at 1x instead of 0.333/0.6 (~0.56x). Its windup/active/recovery remain 0.16/0.12/0.32 s. CombatPresentation uses PlaybackSpeed; CombatSetup preserves the Light2 override.
- PASS: actual asset static checks for heavy XZ enabled and Light2 override. PASS: isolated Unity compilation and AttackPlaybackTest.Run: 1x second-combo playback with unchanged 0.6 s action; root displacement conservation/once-only consumption/clearing at simulated 30/60/144 FPS. Logs/AttackPlaybackValidation.log: ATTACK_PLAYBACK_TEST_PASS, exit 0.
- Acceptance: asset configuration and timing/speed calculations PASS; rendered heavy XZ and second-combo speed NOT VERIFIED — USER TEST REQUIRED. No confirmed compile blocker remains. Full main-scene visual validation not performed.

### Pending manual test: HEAVY-XZ-COMBO2

- Purpose: Confirm actual heavy travel and normal second-strike speed.
- Setup: Stop Play Mode, allow Unity imports/compilation, then enter GameScene on clear level ground.
- Steps: While already running, left-click for heavy and observe horizontal root travel. Then perform a light attack and click again near the end of recovery (~0.55 s) to queue attack 2. Release movement through completion; repeat locked/unlocked.
- Expected result: Heavy retains its animation-driven XZ travel. Attack 2 plays at its original 1x speed; remaining gameplay recovery still completes before another action. No doubled displacement, stuck action or new errors.
- Failure signs: Stationary heavy animation, second strike still slow, doubled heavy travel, lingering drift or Console errors. No passing user retest reported yet; earlier pending tests remain pending.
- Deferred: further combat-feel tuning. Recommendation: RUN MANUAL TESTS BEFORE NEXT PROMPT.

## Latest fix — authored combo blending and preservation rule

- Task status: PARTIAL SUCCESS; controller sequencing tests PASS; original humanoid visual inspection remains pending. Enemy AI not started.
- Inspected actual controller: Attack trigger drives Ground/Locked On → Attack 1 (0.1 s blend), 1 → 2 (exit 0.89667684, 0.1 s blend), 2 → 3 (exit 1, 0.1745399 s blend). Attack-state speeds are 1; transitions use fixed durations, interruption source None and ordered interruption. Locomotion exits use authored exit times/blends (Attack 1 0.24/0.25 s; others 0.25 s).
- Added PlayerComboAnimation visual queue, used by CombatPresentation for player actions. One visual request per new AttackId; existing Attack/Heavy Attack/Dodge triggers used; normal Free changes no longer force GroundMovement. Interrupted visuals alone may explicitly blend to GroundMovement because there are no authored stagger/recoil clips. Death clears attack queues/trigger/hold; existing Die remains responsible for death visuals. Disabled/unavailable Animator clears visual queues without affecting gameplay.
- Controller modification: added CombatComboHold bool and false conditions only on Attack 1/2 Ground/Locked On exits. Authored strike transitions, all exit times, blend durations, interruption fields and state speeds unchanged; heavy XZ flag remains enabled. Gameplay runner and damage resolver unchanged.
- Explicit schedule conflict resolved: gameplay action lengths 0.70/0.60/1.20 s differ from authored clips 1.0/0.333/2.2 s. Visual requests wait for controller source states; continuation hold avoids intervening locomotion. Visual completion may lag gameplay, but cannot extend hit windows, recovery or movement. No clips stretched and no recovery shortened. Player Animator global speed stays 1; attack 2 retains state speed 1. Player definitions/setup no longer request automatic retiming; AttackDefinition defaults to preserving authored speed.
- Preservation requirement: COMBAT_IMPLEMENTATION_PLAN.md now authoritatively assigns normal blends to Animator Controller and gameplay timing/legality to CombatActionRunner; preserves clip speeds and script-driven attack XZ/heavy foot correction. Conflicting retiming, root-motion suppression and assumed headless displacement-parity passages revised; remaining architecture unchanged.

### Current validation — combo ownership fix

- PASS: isolated Unity compilation of relevant runtime scripts (minimal fixture excludes unrelated CameraLook/Cinemachine).
- PASS: ComboControllerTest.Run copied the actual authored graph and used generic clips matching the original durations, then manually stepped Animator and runner. Verified all three strike entries, no locomotion between strikes, second strike at 1x, authored return to locomotion, locked-on three-strike sequencing, interruption without stale attack trigger, death clearing pending attack/hold, and animation-free gameplay completion. Logs/ComboControllerValidation.log: COMBO_CONTROLLER_TEST_PASS, exit 0. These are sequencing tests, NOT visual verification of the humanoid clips.
- PASS: root-motion buffer tests within that run preserve planar displacement/once-only consumption/clearing at simulated 30/60/144 FPS. Static inspection confirms Heavy Attack StateRootMotion xzPlayer=1; foot-ground correction and single motor remain in place.
- PASS: static audit confirms health mutation only through CombatResolver and animated Sword remains inert. No gameplay normalizedTime usage. Controller timing/interruption fields unchanged by the edit.
- NOT VERIFIED — USER TEST REQUIRED: original rendered combo blends, actual heavy XZ/feet, camera/control regressions and alignment between visual contact and gameplay windows. Existing numerical damage tests are historical; not represented as a new complete combat-suite run.

### Pending manual test: COMBO-BLEND

- Purpose: Validate authored visual blending, speeds and control preservation with the original model/clips.
- Setup: Stop Play Mode, let Unity import/compile, open GameScene and enter Play Mode on clear Ground; keep Animator enabled. No temporary fixed crossfade or playback-speed override should be added.
- Steps: Left-click, click again about 0.55 s later, then about 0.6 s later; repeat near the end of each legal queue window. Watch all three strikes, the Inspector Animator graph, and eventual return to idle. Repeat with Q lock-on and with movement held through recovery. Observe attack 2 at normal speed. Perform running heavy and watch XZ travel/feet; dodge, guard with RMB and release. Use F6/F7 defense debug attacks to interrupt a queued combo; take damage until death and confirm no delayed strike. Restart, disable player Animator in Play Mode, perform attacks and confirm gameplay recovery/control still complete; restore Animator.
- Expected result: Controller-authored transitions blend the combo without locomotion between strikes, skipped/duplicate strikes or stale triggers. Strike 2 is 1x. Normal visual return waits for authored exit; gameplay damage/recovery remains timer-driven. Interruptions/death discard queued visuals. Heavy keeps script-driven XZ movement/foot correction; guard release, dodge, movement/camera/lock-on remain usable. No residual root travel after shared movement ownership ends.
- Failure signs: Snaps/fixed-looking replacement blends, intermediate locomotion, slowed strike 2, stale attack after interruption/death, doubled/stale movement, foot clipping or new Console errors. Report visual-contact mismatch separately from health damage counts.
- Acceptance: controller-driven sequence / 1x speed / interruption-death queue cleanup / animation-free completion / single damage path PASS for executed scope; rendered blending, heavy travel/feet and gameplay-camera visual regressions NOT VERIFIED — USER TEST REQUIRED. Enemy AI NOT APPLICABLE.
- Blockers: no confirmed failing executed check. Deferred: visual-contact calibration if the two independent schedules look misaligned; do not solve by silently retiming clips or changing recovery. Historical pending user tests remain pending. Recommendation: RUN MANUAL TESTS BEFORE NEXT PROMPT.

## Completed tasks

- Saved the approved Astra plan; subsequently appended the required completion/validation protocol without redesigning the architecture.
- Phase 0 record: added PlayerInputComponent; promoted player health/lock-on/sword and enemy eye/navigation/perception into prefabs; preserved scene camera bindings; aligned agent radius to capsule (0.5); delayed enemy state construction to Start; fixed child Animator lookup/off-mesh synchronization; added player main-camera fallback.
- Prior documentation task: SUCCESS (documentation only).
- Phases 1/2: added GameCombat identity/intents/results, data assets, explicit hurtboxes, fixed-step world, runner, resolver, swept hit queries, faction/arena filtering, wall obstruction, deduplication, health/posture/defense/interruption/death rules.
- Migrated player attack/heavy/dodge states to shared-runner adapters; added guard/stagger states, fixed-step input queues and generated Block action/wrapper; preserved locomotion/camera/jump/fall; gated root motion and movement ownership. Disabled legacy sword damage.
- Added CombatPresentation arcs, exact attack-volume lines, result rings/tints, bars/status; debug attack commands on Combatant. Updated player/enemy prefabs and authored CombatData assets; enemy remains passive except existing chase/investigation and manually invoked debug actions.
- Added reproducible Editor utilities CombatSetup.Run and CombatValidation.Run. No packages or editor version changed.

## Current task

Resolve the previous task's COMBO-BLEND manual gate before implementing Phases 3/4. Request user PASS/FAIL results for combo/locomotion return, heavy XZ/feet, interrupted combo/death cleanup and guard release. Use the existing debug checkbox/hotkeys for incoming attacks; do not begin enemy AI or controller switching until prerequisite blocking validation is resolved. Other pending test IDs remain pending until reported or explicitly superseded with equivalent verified coverage.

## Validation performed

Current Phases 1/2 checks:

- PASS — Unity 6000.6.0f1 compilation and CombatSetup.Run: Logs/CombatSetup.log, COMBAT_SETUP_PASS; input wrapper regenerated with Block. Initial compile/test defects were fixed before final validation.
- PASS — CombatValidation.Run, batch Play Mode assertions in Logs/CombatValidation.log: COMBAT_RULES_PASS / COMBAT_VALIDATION_PASS, exit 0. Tests cover attack commitment, per-swing duplicates, directional deflection/block/rear hits, exact deflection close, guard rearm/hold/no banking, dodge opening/closing/cooldown, posture break/reset/no stagger extension, invalid damage, immediate death and lethal trades, heavy active armor/recovery vulnerability, combo early rejection/opening/full recovery, multi-collider volume deduplication, factions, walls, posture recovery delay/resumption, and 50 Hz timing under simulated 30/60/144 FPS schedules.
- PASS — GameScene Play Mode smoke: locomotion displacement; queued light/heavy/dodge completion; held guard without repeat deflection; disabled-Animator action completion. These inject input through the adapter rather than physically pressing controls.
- PASS — Static search: no gameplay normalizedTime usage; health mutation calls only in CombatResolver; legacy sword trigger cannot apply damage or enable a second hit volume.
- PASS — fresh prefab runtime integration: instantiated player/enemy prefabs in Play Mode, initialized their combat dependencies and completed a queued player attack without errors. This is a current integration check, not a retroactive Phase 0 claim.
- NOT VERIFIED — rendered camera/animation feedback, physical RMB/WASD/combo controls, actual display-rate playtests. Mathematical scheduling tests are not rendered FPS tests. See manual tests below.

### Phase 1/2 acceptance

| Criterion | Status | Evidence / pending test |
|---|---|---|
| Phase 1 damage, deduplication, factions, phase boundaries, death precedence | PASS | Executed Play Mode rule assertions. |
| Existing player damage has exactly one authoritative route | PASS | CombatWorld → CombatResolver → HealthComponent; Sword is inert. |
| Shared action timing works without animations | PASS | Bare test actors plus disabled player Animator. |
| Phase 2 block/deflect/posture/dodge/interruption rules | PASS | Executed rule assertions; rendered feedback is separate below. |
| Fixed-step timing independent of rendering cadence | PASS | Simulated 30/60/144 frame schedules with 50 fixed ticks, not actual rendered FPS tests. |
| Physical RMB press/hold/release at different render rates | NOT VERIFIED — USER TEST REQUIRED | P12-M2; physical/display-rate validation not performed. |
| Player locomotion/camera/lock-on/jump/fall regressions | NOT VERIFIED — USER TEST REQUIRED | Automated movement smoke passed; P12-M1 covers human-observed integration. |
| Combo/heavy/dodge presentation and temporary visuals readable | NOT VERIFIED — USER TEST REQUIRED | P12-M1–M3. |
| Full Phase 2 gate | NOT VERIFIED — USER TEST REQUIRED | Pending physical controls and rendered presentation. |
| Enemy combat AI / ML | NOT APPLICABLE | Explicitly outside this task. |



Previously recorded Phase 0 results (not rerun):

- PASSED — Unity 6000.6.0f1 compilation.
- PASSED — prefab dependency structural checks, Logs/Phase0Repair.log: PHASE0_STRUCTURAL_PASS. This loaded prefab contents; it did not prove fresh instances work in Play Mode.
- PASSED — automated Play Mode smoke, Logs/Phase0Smoke.log: PHASE0_SMOKE_PASS, exit 0: movement displacement, light/heavy attack completion, dodge completion, visible-target chase acquisition, lock-on acquisition, investigation exit after detection loss, and player/enemy death state checks without reported runtime errors.
- PASSED — recorded camera existence/reference checks. Rendered framing and physical input were not verified.

Phase 0 acceptance accounting:

| Criterion | Status | Evidence / remaining coverage |
|---|---|---|
| Explicit prefab dependencies | PASS | Recorded structural check; runtime is separate below. |
| Fresh prefab instances initialize and complete player actions | PASS | Current CombatValidation fresh-prefab runtime assertion; rendered behavior remains pending. |
| Player movement and camera preserved | NOT VERIFIED — USER TEST REQUIRED | Movement displacement passed; physical controls, jump/run and rendered camera remain P0-M1. |
| Lock-on preserved | NOT VERIFIED — USER TEST REQUIRED | Automated target acquisition passed; physical toggle and rendered framing remain P0-M1. |
| Existing attacks preserved | NOT VERIFIED — USER TEST REQUIRED | First light/heavy completion passed; physical combo, damage and visual behavior remain P0-M2. |
| Death operates without exceptions | PASS | Recorded smoke state checks for both actors; no claim of visual death review. |
| Enemy chase operates without exceptions | PASS | Recorded visible-target acquisition; rendered navigation remains P0-M1/P0-M3. |
| Investigation / last-known-position preserved | NOT VERIFIED — USER TEST REQUIRED | Recorded investigation exit passed; arrival at remembered position and no tracking through occlusion remain P0-M3. |
| Full Phase 0 gate | NOT VERIFIED — USER TEST REQUIRED | Limited automated coverage plus pending tests and fresh-instance runtime automation. |

Current documentation checks are recorded after editing; no Unity or external-tool testing is claimed for this task.

## Pending manual tests

**Phase 3/4 prerequisite gate:** COMBO-BLEND has no reported result and must be resolved first. The detailed instructions above remain authoritative; the existing defense debug checkbox (Player > Combat Debug Controls > Enable Defense Debugging) provides F6/F7 incoming attacks and F8 opponent guard. Confirm guard release as well (P12-GUARD-RETEST). Do not mark these passed from previous isolated controller/buffer tests; those did not visually inspect the original humanoid in GameScene.

Required current tests (perform Play Mode-only changes; do not save/apply them). Report PASS/FAIL for each ID with Console errors and symptoms. No user results received.

### P12-M1 — Player loop and animation ownership

- Purpose: Verify physical controls, camera, combo identity, heavy leap, dodge and action recovery.
- Setup: Open Assets/Scenes/GameScene.unity in Unity 6000.6.0f1. Clear Console, enter Play Mode and click Game view. Keep original Inspector settings. Scene enemy still only chases; combat AI is intentionally absent.
- Steps: Mouse-look, WASD walk, W+Shift run (wait for initial dodge to finish), Space jump/land. Press Q or middle mouse near a visible enemy; strafe then unlock. Face the enemy within sword reach; left-click, then click again about 0.55 s later and again about 0.6 s later to queue all three light strikes. While already running, left-click for heavy. Tap Shift with/without movement to dodge. Try RMB/Shift during attack recovery; wait until free before retrying. Inspect enemy HealthComponent after contacts. Repeat after disabling the player child Animator in Play Mode; restore it afterward.
- Expected result: Controls/camera remain usable; three light actions progress only after recovery; heavy leaps once and lands; dodge completes; actions cannot cancel recovery. Yellow/magenta windup boxes turn red while active, and clear contacts reduce health once per swing. Actions complete with Animator disabled and procedural feedback remains. No new errors or competing root-motion snaps.
- Failure signs: Missed physical controls, stuck action, early cancel, lost camera/lock-on, multiple damage ticks per swing, no damage on clear contacts, root-motion fighting, or exceptions.

### P12-M2 — Guard, deflect, posture, iframe feedback

- Purpose: Visually validate defense and physical RMB behavior against manually commanded incoming attacks, without adding enemy AI.
- Setup: In GameScene Play Mode, let actors land. Select Enemy, disable its enemy script, EnemyPerceptionNav and NavMeshAgent; set its Rigidbody Is Kinematic=true. Keep Combatant/CombatActionRunner/CombatPresentation enabled. Place Enemy 1.3 m directly in front of the player on clear floor, retaining grounded Y. Set the player model world yaw to 0 and Enemy root yaw to 180 so they face one another. No production settings should be saved. Enemy Combatant has EnemyLight/EnemyHeavy definitions.
- Steps: On Enemy Combatant component context menu choose Combat Debug > Light attack in 2 seconds. Return to Game view and hold RMB well before impact: observe normal block. Repeat, pressing RMB near the end of the yellow windup just before red active volume: observe deflection. Keep RMB held for a subsequent delayed strike: it must block, not reopen deflection. Release for at least 0.1 s before retrying (at least 0.3 s between window starts). Repeat delayed strikes/holds until posture fills and breaks; stop attacks to observe recovery. Use Shift near impact to observe the brief blue iframe tint. Restart Play Mode as needed to reset health/posture. Repeat RMB press/hold/release at the different frame-rate limits available in your Game view/editor setup; report the actual rates tested (if no limiter is available, report display rate and leave other rates unverified).
- Expected result: Cyan guard arc while held; ordinary block prevents health loss but raises posture. Timed deflect flashes cyan/white with a ring and recoils the attacker. Holding never repeats deflection. Full posture drops guard and produces a magenta 1 s stagger; recovery follows. Blue dodge tint is brief. No new errors.
- Failure signs: Persistent invulnerability while holding, repeated deflections without releases, block health damage from front, missing feedback, posture never recovering/breaking, or exceptions. A missed parry alone is not a failure; retry timing.

### P12-M3 — Rear defense and death cleanup

- Purpose: Check visible directional coverage and death presentation after integration.
- Setup: Use P12-M2's stationary debug attacker setup and fresh Play Mode health. Keep Console open.
- Steps: Hold RMB but turn the player model away from Enemy. Schedule Enemy light attacks and return to Game view, holding RMB. Verify health loss on rear contact. Turn to face Enemy and repeat to confirm front block. Release guard and repeat delayed heavy/light attacks until player health reaches zero (restart if needed). Separately, in a fresh run, attack Enemy until its health reaches zero; inspect remaining arcs/attack boxes and try locking onto it.
- Expected result: Rear hits bypass guard; front hits block. Death stops gameplay actions and clears guard/attack feedback; dead enemy cannot be locked on. No new exceptions. Existing death clips may play, but gameplay completion does not depend on them.
- Failure signs: Rear immunity, actors attacking/guarding after death, stale damaging volumes, reacquiring dead targets, or exceptions.

### Retained Phase 0 tests


Use Unity 6000.6.0f1. These tests concern existing behavior; blocking/parrying/posture/debug combat visuals are not implemented yet and are not expected. Do not save Play Mode changes. Report each ID as PASS or FAIL with symptoms/Console errors.

### P0-M1 — Physical controls, camera and lock-on

- Purpose: Verify the human-controlled, rendered behavior omitted by the smoke test.
- Setup: Open Assets/Scenes/GameScene.unity. Open Console and Game view; clear Console. Keep saved Inspector settings unchanged. Enter Play Mode and click Game view to capture input.
- Steps: Move mouse to orbit; press W/A/S/D individually. Hold W+Shift to run, release Shift to walk, and press Space to jump. Tap Shift while standing to dodge. Approach the enemy with clear line of sight within 10 m; press Q to acquire lock-on. Move sideways with A/D and move mouse; press Q to release. Repeat acquisition/release using middle mouse. Watch the enemy approach while you move around a wall.
- Expected result: Movement follows the camera, run is faster than walk, jumping lands normally, dodge completes, camera remains usable without snapping/disappearing. Lock-on acquires the visible enemy, maintains useful framing, and releases with the same toggle. Enemy moves around obstacles. No new Console errors/exceptions. No new debug visualization is required.
- Failure signs: Ignored controls, stuck action, camera loss/jitter/severe clipping, failed toggles, enemy stuck against reachable walls, or Console exceptions.

### P0-M2 — Physical attack chain, damage and visible death

- Purpose: Verify combo input and visible weapon contact, beyond forced-state completion.
- Setup: Re-enter GameScene Play Mode for fresh health; keep saved settings. Select Enemy and expand HealthComponent in Inspector to watch health. Click Game view before each input sequence.
- Steps: Stand in sword reach facing the enemy. Left-click for light attack; click again during the swing/recovery to chain, and again for the third strike. If timing is missed, wait for idle and retry. Hold W+Shift and left-click to perform the running heavy attack. Inspect health after completed contacts. Repeat valid hits until enemy health reaches zero. Stop/re-enter Play Mode to restore the scene.
- Expected result: Light attacks chain through the existing three animations when inputs land in their windows; running heavy performs its existing leap/attack and returns control. Visible contacts reduce health; enemy dies and stops chasing at zero. No stuck hitbox/action or new Console exceptions. No new debug visualization is expected.
- Failure signs: Inputs never chain despite repeated timing attempts, animation remains stuck, no damage on clear contacts, enemy keeps chasing at zero health, or new exceptions. Record observed health values and attack used; do not infer a PASS from animation alone.

### P0-M3 — Remembered position and fresh-prefab visual parity

- Purpose: Verify occluded pursuit and visually compare newly instantiated prefab actors with the saved scene actors.
- Setup: First use GameScene unchanged. Select Enemy/EnemyPerceptionNav; retain detectPlayer=true, memoryDuration=3 and investigationTimeout=15. Enter Play Mode with Console open.
- Steps: Get visibly detected, then run behind an existing wall and continue sideways while remaining occluded. Watch in Scene view without moving actor transforms; inspect LastSeenPosition/LastSeenTime in Debug Inspector if available. Enemy should go toward the last visible location rather than your hidden current position, wait approximately 3 s on arrival, then leave chase (15 s investigation timeout handles unreachable arrival). Stop Play Mode. For fresh-instance comparison, create an unsaved copy via File > Save As to a temporary scene outside the production scene path; remove its existing player/enemy instances and drag in Assets/Prefabs/Player.prefab and Assets/Models/Enemy/Enemy.prefab. Position the player on the existing floor and the enemy on the baked NavMesh, with both capsule bottoms just above the floor and 3–5 m apart with clear sight. Keep the existing camera/Cinemachine objects; inspect their tracking targets and reassign references that point to deleted actors to the corresponding new player/model/CameraTarget transforms. Enter Play Mode; repeat mouse/WASD/Q and one left-click attack, watching actor grounding, camera and enemy approach. Do not apply these temporary scene settings to the production prefabs. Delete/discard the temporary scene after testing.
- Expected result: Enemy investigates its remembered location and does not continuously track hidden movement. Fresh actors render, ground, move, acquire lock-on and show attack/chase behavior without missing-component exceptions; camera works with the explicitly rebound scene camera targets. No new combat debug visuals are required.
- Failure signs: Enemy tracks through the wall, never leaves chase, new actors fail initialization/fall through reachable ground, lost camera targets, missing sword/controls, or exceptions. Scene-specific camera rebinding is expected and is not itself a prefab defect.

### P12-GUARD-RETEST — Exit defense on release

- Purpose: Confirm the reported stuck-defense symptom is gone in the running scene.
- Setup: Stop Play Mode, let Unity finish importing/recompiling, then open Assets/Scenes/GameScene.unity and enter Play Mode. Click Game view.
- Steps: Hold RMB for one second, release it without pressing Shift, then walk with WASD and left-click. Repeat three times.
- Expected result: Guard arc/defense state clears on release; normal movement and attacks resume. Holding still opens only one deflection window.
- Failure signs: Guard stays active after release, attacks remain blocked, movement stays at guard speed, or new Console errors.

## Manual test results

User reported FAIL: character stuck in defense. Root cause fixed as recorded above; P12-GUARD-RETEST remains pending. No passing retest has been reported. Other pending tests remain pending. Move passed test IDs here only after the user supplies results; record failed results here and under Known blockers where appropriate.

## Known blockers

- Phase 3/4 start is blocked by the user's explicit prerequisite gate: unresolved COMBO-BLEND and related human gameplay validation could reveal blocking defects. No new code failure is asserted; the limitation is missing evidence. Only IMPLEMENTATION_PROGRESS.md changed in this request.
- Current checks: PASS — read both planning documents and inspected pending/manual-result entries; PASS — found no recorded passing user result for the previous task. No Unity tests, builds or gameplay inspections were executed this request.
- Phase 3/4 acceptance: normal enemy approach/spacing/attacks/defense/recovery and occlusion/death integration = NOT VERIFIED — USER TEST REQUIRED (not implemented in this blocked request); controller ownership/switch preservation/cooldown/commitment invariants = NOT VERIFIED — USER TEST REQUIRED (not implemented). ML-Agents setup = NOT APPLICABLE.

No confirmed blocking defect remains from executed tests. Human gameplay/presentation validation P12-M1–M3 (and retained Phase 0 tests) could expose blocking regressions, so the full gate remains unverified. Do not treat the automated passes as visual approval.

## Deferred TODOs

- Existing unused RootMotionHandler field and state serialization warnings.
- Broader collision/damage edge cases and frame-rate/input-device coverage during later combat validation; basic existing attack correctness remains pending above, not deferred polish.
- CLI update advisory previously recorded; no tooling/package upgrade performed.
- Phases 3+ remain unstarted. Heavy leap impulse is authored at 5 (capped by existing heavyAttackForce) so it lands for the new 0.70 s windup; retuning beyond correctness is deferred.
- Allocating overlap queries avoid silent fixed-buffer overflow; optimize after profiling. Assembly-definition migration and formal NUnit/Test Runner packaging can follow if needed; current tests are executable Editor-driven Play Mode assertions.
- Phase0Smoke force-state test is historical and no longer exercises migrated combat requests; use CombatValidation.Run.
- Debug tint/line quality and exhaustive contact geometry calibration await visual feedback; do not polish before user results.

## Recommendation

RUN MANUAL TESTS BEFORE NEXT PROMPT
