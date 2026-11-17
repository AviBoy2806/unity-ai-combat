> Current runtime handoff: [IMPLEMENTATION_PROGRESS.md](../../../IMPLEMENTATION_PROGRESS.md), [demonstration instructions](../../../Logs/RuntimeIntegration20261117/MANUAL_TESTS.md), [animation/audio assignments](../../../ANIMATIONS_AND_SOUNDS_TO_ADD.md). Normal remains default. The separately installed gameplay candidate is evaluation-only and failed quality gates. Historical claims below retain their original scope.
>
> **Ordinary CombatTraining Play Mode uses the diagnostic heuristic.** Assigning a model in the scene is not enough to evaluate it: saved-model evaluation must be explicitly selected with `--phase5-eval` in the existing evaluation executable/commands. GameScene instead selects the installed inference-only candidate through Enemy Brain Host's explicit evaluation button.

# Phase 5 combat training and evaluation

The bounded 17 November 2026 candidate experiment is recorded in `Logs/BoundedTraining20261117/COMMANDS.md` and `IMPLEMENTATION_PROGRESS.md`. Its wrapper is `bounded_task.py`. It uses the existing gameplay/trainer pipeline, records hard process limits, and does not promote or integrate a model. Keep the historical smoke results below separate from that experiment.

This is local pipeline infrastructure and a smoke policy. It is not a selected or competent combat policy. GameScene still defaults to Normal. No self-play, parameter search, cloud service or paid execution is used.

## Prerequisites and clock

The prior release-23 Basic training/export/inference spike and 135-check agent contract passed. Current-session contract and shared combat regressions passed again. Versions and installation are pinned in `../REPRODUCE.md`, `../environment.freeze.txt` and `../baseline.json`.

Retain the authored Animator, presentation and RootMotionHandler. The physics step remains **0.02 s**. Use **timeScale 4 / captureDeltaTime 0.005** (capture frame rate 200), or the measured runtime clock **1 / 0.02**. Do not use animation-free player movement. The Python startup handshake can overwrite capture settings; the arena reapplies the measured clock before preparation. Other clocks fail preparation.

### Normal-speed visual inspection — P5-ENV-VIS

User results on 17 November 2026: **P5-VIS PASS** (controls work well; agent disable/re-enable mid-attack works correctly). **ST-STATE entry into Stunned is confirmed**; no additional stun observations were reported. **P5-ENV-VIS PASS** is now user-reported; no further observations are inferred. Full current results and test fields: `Logs/VisualInspection20261117/MANUAL_TESTS.md`.

1. Stop Play Mode. Open `Assets/Scenes/CombatTraining.unity` and let scripts compile.
2. Select **Combat training session**. Keep **Default Arena Count = 1** and check **Normal Speed Visual Inspection**.
3. Select **Arena 1** and uncheck **Hide Renderers before Play Mode**.
4. Enter Play Mode. In **Scene** view select **Scripted player opponent** and press **F**. No Game camera is needed.
5. During Play Mode set Arena 1 **Stage = 3**. Use the opponent's **Family = Aggressive** for light combos, then **PredictableHeavy** for player heavy attacks. A reset may choose a different family; select the desired one again. Watch recoveries and both actors resetting. Report any unobserved death presentation separately.
6. Stop Play Mode and restore the temporary checkbox/renderer settings. Do not save them over accelerated scene defaults.

The checkbox selects **scale 1 / fixed .02 / capture .02** and paces this Editor diagnostic to at most 50 frames per wall second, including Scene view. The session's repeated Update and arena startup/re-enable clock restoration respect it. Changing global Time Scale alone is insufficient. The switch defaults off and is ignored for training/evaluation command flags; existing accelerated runs still use **4 / .005** with **.02** fixed time. The earlier bounded clock diagnostic passed. P5-ENV-VIS now has a user-reported visual PASS. The diagnostic task itself did not perform training or integration.

Movement comparisons require matched geometry. `Logs/Phase5ArenaMotion.txt` compares saved GameScene actors with actual training-scene actors on the same flat test floor. All 12 comparisons pass the predeclared limits (max 0.02 m, RMS 0.01 m, equal samples). Maximum measured error is 0.008914 m; maximum RMS is 0.008551 m. The first unmatched-wall comparison is retained as a failed comparison, not silently discarded.

## Scene and ownership

`Assets/Scenes/CombatTraining.unity` contains eight arenas 40 m apart, with **one enabled by default**. Each owns an enemy learner, a player-archetype opponent, a floor, walls and NavMesh. Actors use the existing runners, resolver, motors, player input adapter and animation graph. There is no required camera, physical keyboard, singleton or debug controller.

The arena is the only reset owner. Agent callbacks enqueue a reset. Both actors suppress resolver contacts and physics collision detection while resetting. The reset clears gameplay resources, timers, IDs, hit history, input/guard/combo/root-motion queues, both FSMs, brain generation/history, perception memory, delayed observations, Agent transport/observation stacks, Animator state, navigation, physics and episode records. Both poses and NavMeshes synchronize before contacts resume. Scene reload is not an episode reset.

Death or leaving the 8 m arena boundary ends an episode. Simultaneous deaths and simultaneous exits draw. Timeout at 1,500 fixed ticks (30 simulated seconds) is a draw reported separately as `timeout`. Learner exit is a loss; opponent exit is a win. Records include the terminal reason, so an opponent exit is not mistaken for a kill.

Different arenas reject injected foreign targets and resolver contacts. Cross-arena actor collider pairs are explicitly ignored. Arena centers are 40 m apart; actors stop at 8 m and clearance rays are at most 10 m, below the distance to another arena's geometry.

## Exact commands

Run PowerShell from the project root. The already-created `Tools/Phase5/Lab` has separate Library/Temp/Packages/ProjectSettings, with junctions to this project's Assets, Tools and Logs. This avoids closing the user's open editor. Do not run two Unity jobs against the same Lab. Alternatively use the project root after closing its editor. No user scene is closed or saved by the Lab commands.

```powershell
$projectRoot = (Get-Location).Path
$labRoot = Join-Path $projectRoot 'Tools/Phase5/Lab'
$unityExe = 'C:/Program Files/Unity/Hub/Editor/6000.6.0f1/Editor/Unity.exe'
$pythonExe = Join-Path $projectRoot 'Tools/ml-venv/Scripts/python.exe'
function Invoke-CombatUnity($method, $logName, $quit = $false) {
  $arguments = "-batchmode -nographics -projectPath `"$labRoot`" -executeMethod $method -logFile `"$projectRoot/Logs/Phase5Environment/$logName`""
  if ($quit) { $arguments += ' -quit' }
  $job = Start-Process $unityExe -ArgumentList $arguments -WindowStyle Hidden -PassThru
  $job.WaitForExit()
  if ($job.ExitCode -ne 0) { throw "Unity failed: $logName, exit $($job.ExitCode)" }
}

Invoke-CombatUnity 'Phase5EnvironmentBuild.Create' 'Create.log' $true
Invoke-CombatUnity 'Phase5AgentValidation.Run' 'AgentRegression.log'
Invoke-CombatUnity 'PostureRegressionValidation.Run' 'SharedRegression.log'
Invoke-CombatUnity 'Phase5AcceleratedMotionValidation.Run' 'Acceleration.log'
Invoke-CombatUnity 'Phase5ArenaMotionValidation.Run' 'ArenaMotion.log'
Invoke-CombatUnity 'Phase5EnvironmentValidation.Run' 'ResetIsolation.log'
Invoke-CombatUnity 'Phase5EnvironmentBuild.Build' 'Build.log' $true
```

The reset validator first exercises one live arena, then 1,000 dirty resets, then 1,000 resets per arena across eight arenas. It checks atomic reset snapshots, contact suppression, callback reentry, actor/arena ownership, duplicate reward delivery and an arena disable/re-enable cycle. The contract validator separately tests masks, observation order/delay/masking, one-tick application latency, heuristic execution and stale callback rejection.

The authorized pilot was run with:

```powershell
& $pythonExe Tools/Phase5/Combat/manage.py pilot phase5-combat-pilot-23b
```

The wrapper runs `ppo-smoke.yaml` on eight arenas with seed 23, CPU PPO, stage 1, no graphics and the measured clock. It subtracts prior attempts' recorded wall time from a conservative 590-second series budget. The trainer's final drain can exceed `max_steps=4096`; this pilot ended at checkpoint step **4976**, below 30,000. Do not increase the budget or treat this policy as a quality result. A watchdog termination is a failure, not proof of an export. Do not rerun the completed pilot as an optimization search.

For a later explicitly authorized curriculum run, train and resume commands are:

```powershell
# Stage 1. Do not execute as part of the completed pipeline pilot.
Tools/ml-venv/Scripts/mlagents-learn.exe Tools/Phase5/Combat/stage-1.yaml --env=Tools/Phase5/Combat/Build/Combat.exe --run-id=combat-stage1-23 --results-dir=Tools/Phase5/Combat/results --seed=23 --no-graphics --torch-device=cpu

# Resume the same run/config and saved optimizer state.
Tools/ml-venv/Scripts/mlagents-learn.exe Tools/Phase5/Combat/stage-1.yaml --env=Tools/Phase5/Combat/Build/Combat.exe --run-id=combat-stage1-23 --results-dir=Tools/Phase5/Combat/results --seed=23 --no-graphics --torch-device=cpu --resume

& $pythonExe Tools/Phase5/Combat/manage.py metadata Tools/Phase5/Combat/results/combat-stage1-23 --stage 1
```

Use stage-2/3/4.yaml only after the preceding gate passes. New stages should have new run IDs and use `--initialize-from=PREVIOUS_RUN_ID` to keep stage records separate. Do not silently restart or alter a failed stage. The full PPO config uses 2x128 hidden units, no trainer normalization, LR 3e-4 linear, batch 256, buffer 4096, three epochs, epsilon .2, lambda .95, beta .005, gamma .995 and horizon 128. Full-run summary/checkpoint intervals are 10,000/100,000; max_steps 2 million is a starting experiment budget, not a convergence promise. Only the smoke stop/log intervals differ.

Export and evaluate a checkpoint:

```powershell
# Use the smoke run below, or substitute a later authorized recorded run.
$runRoot = Join-Path $projectRoot 'Tools/Phase5/Combat/results/phase5-combat-pilot-23b'
New-Item -ItemType Directory -Force Assets/CombatTrainingPolicy | Out-Null
Copy-Item "$runRoot/CombatV1.onnx" Assets/CombatTrainingPolicy/CombatV1.onnx
Copy-Item "$runRoot/metadata.json" Assets/CombatTrainingPolicy/metadata.json
Invoke-CombatUnity 'Phase5EnvironmentBuild.BuildEvaluation' 'BuildEvaluation.log' $true

$evalExe = Join-Path $projectRoot 'Tools/Phase5/Combat/Evaluation/Combat.exe'
$evalArgs = "-batchmode -nographics --phase5-eval --eval-episodes 2 --arenas 1 --stage 3 --metrics `"$projectRoot/Logs/Phase5Environment/held-out-new.jsonl`" -logFile `"$projectRoot/Logs/Phase5Environment/HeldOutNew.log`""
$job = Start-Process $evalExe -ArgumentList $evalArgs -WindowStyle Hidden -PassThru
$job.WaitForExit()
if ($job.ExitCode -ne 0) { throw 'Held-out evaluation failed' }
& $pythonExe Tools/Phase5/Combat/manage.py summarize Logs/Phase5Environment/held-out-new.jsonl --stage 3
```

Evaluation uses deterministic Burst CPU inference and explicitly checks that Python communication is off. It runs all 15 opponent families unless `--eval-family Passive` (or another exact enum family name) is supplied. Use a **new metrics filename per evaluation**, since records append. For generalization evaluation substitute `--stage 4`. Final acceptance requires **200 fixed episodes per family**, not two. A bounded stage-1 promotion check uses `--eval-family Passive --eval-episodes 200 --stage 1`. None of those larger runs was performed in this task.

## Curriculum, rewards and records

Stage 1 uses Passive and randomized starts; promotion needs >=90% wins in 200 unique fixed held-out episodes. Stage 2 mixes PredictableLight/PredictableHeavy equally; promotion needs >=70% survival to timeout and observed blocks and deflections in 200 held-out episodes per family. Stage 3 uniformly mixes Aggressive, GuardCounter, DodgeRetreat and Balanced. Earlier families remain in evaluation. Stage 4 adds walls, starts 1.5-6 m apart, random facing, movement x0.9-1.1 and telegraph x0.85-1.15. Active/recovery lengths and damage/defense semantics remain fixed; authored assets are never edited. The arena-scoped generalization policy validates variants against the model's baseline. GameScene retains its exact metadata/tuning check.

Exploit families are AlwaysGuard, GuardTap, DodgeSpam, CornerCamp, Retreat, WallAttack, ComboSpam and DelayedAttack. Final statistical checks also require basic wins >=90%, Balanced wins >=50%, duel timeout rates <15%, exploit opponent wins <=80%, 200 episodes per family, and zero integrity errors. Human readability/telegraph review remains a separate gate. Nothing promotes automatically.

Rewards follow the plan: +1/-1/0 terminal; +/-0.25 x actual health damage/max health; +.01 per real deflection capped at +.05/episode; -.001/s living cost. Only stage 1 uses .05 x (.995 x nextPotential - previousPotential), bounded potential [-1,0], terminal potential zero. No recurring guard, proximity or posture reward exists. The ledger deduplicates `(attackInstanceId, attackerId, defenderId)` within an episode across both actor callbacks.

Metrics include win/loss/draw/timeout, terminal reason, actual damage, defense results, requests rejected by the runner, accepted illegal requests, mask violations, action counts, guard presses/time, retreat/corner time, wall attack requests, foreign events, duplicate deliveries and reward components. Duplicate deliveries are expected from two callbacks; duplicate **payouts** are forbidden. The summarizer checks reward reconciliation and caps, unique evaluation seeds and training/evaluation separation before reporting promotion eligibility.

Training episode seeds are `masterSeed + arenaId*1000003 + episode*7919`; held-out seeds are `100000 + familyIndex*10000 + episode`. Evaluation is fixed, independent of the training RNG. Per-episode records include seed and generalization parameters. Reproducibility does not imply cross-platform deterministic physics.

The metadata command reads the matching runtime `METRICS.contract.json`, never an Editor-generated tuning hash. It records contract, tuning hash, export/checkpoint hashes, versions, stage, mixture, config and source SHA256s. The workspace has no project Git commit. The pilot additionally retains its source snapshot, exact managed assemblies and trainer checkpoint records. Keep evaluation summaries beside each export. End-to-end resume was documented, not run; the saved policy/optimizer checkpoint was loaded successfully for readability validation.

## Scope limits

Numerical checks do not establish human-visible blending, attack-2 speed, heavy feet/XZ motion, telegraph readability or the prior reported full-stun issue. Those manual tests remain in IMPLEMENTATION_PROGRESS.md. Generalization inference/reset checks are a pipeline test, not a movement-equivalence claim for every varied telegraph, combo, wall contact or frame rate. No final policy acceptance or final-policy selection was attempted.
