"""Build the bounded experiment's reports from retained machine-readable evidence."""
import hashlib
import json
import pathlib
import shutil

ROOT = pathlib.Path(__file__).resolve().parents[2]
OUT = ROOT / "Logs/DemoPolicy20261117"

def read(p):
    return json.loads(p.read_text(encoding="utf-8-sig"))

def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()

def save(name, text):
    (OUT / name).write_text(text.strip() + "\n", encoding="utf-8")

selection = read(OUT / "selected-source.json")
installed = read(OUT / "installed-identity.json")
budget = read(OUT / "budget.json")
render = read(OUT / "sampled-texture-rendered/verification.json")
assert render["passed"] and render["visibleFrames"] == 99
assert render["modelSha256"] == installed["modelSha256"] == selection["modelSha256"]
assert sha(ROOT / "Assets/RuntimeEvaluationCandidate/CombatV1-EvaluationOnly.onnx") == selection["modelSha256"]
assert sha(ROOT / "Assets/RuntimeEvaluationCandidate/runtime-metadata.json") == installed["metadataSha256"] == selection["runtimeMetadataSha256"]
assert budget["additionalDecisions"] == 200015 and abs(budget["trainingProcessWallSeconds"] - 549.594) < .001

before = read(OUT / "start-manifest.json")
changed = [p for p, h in before.items() if (ROOT / p).exists() and sha(ROOT / p) != h]
missing = [p for p in before if not (ROOT / p).exists()]
allowed = {p.replace("/", "\\") for p in [
    "Assets/Editor/RuntimeIntegrationValidation.cs", "Assets/MLAdapter/EnemyLearningAgent.cs",
    "Assets/Scripts/Combat/CombatTrainingArena.cs", "Assets/Scripts/Combat/CombatTrainingSession.cs",
    "Assets/Scripts/Enemy/Combat/EnemyBrainHost.Agent.cs", "Assets/Scenes/CombatTraining.unity",
    "Assets/Scenes/GameScene.unity", "Assets/RuntimeEvaluationCandidate/CombatV1-EvaluationOnly.onnx",
    "Assets/RuntimeEvaluationCandidate/runtime-metadata.json"]}
assert not missing and set(changed) == allowed
original = ROOT / "Tools/Phase5/Combat/results/combat-stage1-bounded-20261117-23b"
prep = read(OUT / "continuation-preparation.json")
assert sha(original / "CombatV1/checkpoint.pt") == prep["checkpointSha256"]
assert sha(original / "CombatV1.onnx") == "566e06daf98274b96b01b1a07940262044ef69e68bbce1d51fceb4d9335eec2d"
audit = dict(passed=True, changedExistingFiles=changed, missingExistingFiles=missing,
    protectedExistingFilesUnchanged=len(before)-len(changed), originalCheckpointAndExportRetained=True,
    gameSceneChanges="Only three SHA bindings and DeterministicInference=0; model GUID/hierarchy/default Normal unchanged.",
    playerAndSharedGameplay="All starting player source, Animator controllers, prefabs, seven CombatData assets, EnemyMotor, runner, resolver and TimeManager unchanged.")
(OUT / "preservation-audit.json").write_text(json.dumps(audit, indent=2))

names = ["original-fixed-passive-200", "original-fixed-families-10", "block1-dev-passive-50",
    "block1-passive-gate-200", "block1-families-10", "block1-varied-families-5",
    "block1-sampled-dev-50", "block1-sampled-gate-200", "block1-sampled-families-10", "block1-sampled-varied-5"]
runs = {name: read(OUT / (name + ".summary.json")) for name in names}
assert all(v["rewardAccountingValid"] and v["zeroIllegalMaskForeign"] for v in runs.values())
assert runs["block1-sampled-gate-200"]["stage1GatePassed"]
family_order = list(runs["block1-sampled-families-10"]["families"])

def wldt(row):
    return "/".join(str(row["outcomes"][x]) for x in ["win", "loss", "draw", "timeout"])

comparison = ["| Family | Original greedy N=10 W/L/D/T; mean damage dealt/received | New greedy N=10 | Selected sampled N=10 | Selected sampled varied N=5 |",
              "|---|---|---|---|---|"]
detail = ["| Family (N=10 each) | Combat kills / opponent exits | Accepted light/heavy/dodge | Contacts | Rejected (Busy) | Free facing error, degrees |",
          "|---|---|---|---|---|---|"]
for family in family_order:
    cols=[]
    for name in ["original-fixed-families-10", "block1-families-10", "block1-sampled-families-10", "block1-sampled-varied-5"]:
        row=runs[name]["families"][family]
        cols.append(f"{wldt(row)}; {row['damageDealt']:.1f}/{row['damageReceived']:.1f}")
    comparison.append("| " + family + " | " + " | ".join(cols) + " |")
    row=runs["block1-sampled-families-10"]["families"][family]
    reasons=row["terminalReasons"]
    assert row["requestResults"][2:] == [0,0,0,0]
    detail.append(f"| {family} | {reasons.get('opponent_death',0)} / {reasons.get('opponent_out',0)} | {row['acceptedLight']}/{row['acceptedHeavy']}/{row['acceptedDodge']} | {row['attackContacts']} | {row['rejected']} | {row['meanFreeFacingError']:.2f} |")

evaluation_table=["| Run | Episodes | Family/scenario | Inference | Bound / actual process seconds |", "|---|---|---|---|---|"]
for name,data in runs.items():
    rr=read(OUT/(name+".run.json"))
    evaluation_table.append(f"| {name} | {data['episodes']} | {len(data['families'])} families, stage {data['scenarioStage']}, seed offset {data['seedOffset']} | {data.get('inferenceMode','deterministic')} | {rr['hardTimeoutSeconds']} / {rr['wallSeconds']:.3f} |")

hash_table=["| Identity | SHA256 / value |", "|---|---|"]
for label,key in [("Installed ONNX","modelSha256"),("Runtime metadata","runtimeMetadataSha256"),
    ("Raw training metadata","trainingMetadataSha256"),("Effective saved configuration.yaml","configSha256"),
    ("Contract source (66 × 3; branches 9/5 unchanged)","contractSha256"),("Movement source bundle","movementHash"),
    ("Standalone tuning","tuningHash")]:
    hash_table.append(f"| {label} | `{selection[key]}` |")
hash_table.append(f"| Imported payload | `{installed['payloadSha256']}` |")
hash_table.append("| Editor tuning | `ca234fe3a36b6d0b651dbfdf8c83a51940d982d872e6950d5a31e8bff655a697` |")

results = """
# Bounded learned-opponent improvement — 17 November 2026

Task status: **PARTIAL SUCCESS**. The immediate learned demonstration target is verified in one bounded actual GameScene rendered sample. Stage-1 promotion passes. Broader duel/defense quality fails; this is an evaluation-only candidate, not final ML acceptance. Normal remains saved default. No further training or release integration is scheduled by this report.

## Concrete diagnosis and implementation

`EnemyBrainHost.Agent` already called `motor.Face`. Normal passed a planar delayed public direction; ML passed the delayed relative position including a small vertical difference. On the actual FreezeRotation Rigidbody, constant side/rear targets stalled near 3.6° yaw, leaving 86.4°/176.4° error. Model and Rigidbody facing agreed: this was not just a renderer offset. The minimal fix passes existing `ObservedTargetDirection` to the existing motor, matching Normal. No constraints, turn speed, timing, collision, motor or gameplay tuning changed.

`diagnosis-before`, `diagnosis-planar`, and `diagnosis-comparison.json` reproduce ahead/left/right/behind/orbit, inside/outside contact, approach, free/windup/active/recovery with controlled interface policy and actual learned candidates. Trace JSONL records selected/active/fallback, delayed/masked cues, requested/buffered/applied choices, legal masks, request reasons, runner phase, motor ownership, desired direction, Rigidbody/model/Animator rotation and real resolver damage. Controlled policy is explicitly **NOT LEARNED**, never installed. After correction, stationary side/rear error is 0°, free/windup turning stays within ~3.6005°/tick (180°/s plus float error), active/recovery heading stays fixed, orbit retains ~8° public-delay lag. Controlled approach changed from 0 to 36 damage; old learned side-start 0 to 66. Initial summaries' decision counters were unfilled (0); later `selected-execution`/`block1-sampled-execution` fix counters and distinguish buffered from actual host intent. Do not interpret those early zeros as no decisions.

The old failed seed100001 was separately replayed: 18 accepted heavy requests, zero contacts/damage and essentially no approach at ~2.98 m, despite valid public cues/masks and no fallback. Repeated out-of-range commitment prevents walking during those actions; Stop selection also dominates its free choices. Acceptance does not imply contact. It is policy choice/range behavior in addition to the facing defect, not an illegal-request or stale-observation fault. Fresh cues are initially masked for the normal delay (9 ticks), not privileged.

The trained checkpoint's greedy output passed Passive but still selected Forward/None on clear open floor, only attacking after navigation masks changed near a boundary. `inference-experiment.json` tests identical learned weights using the pinned package's exported **sampled** action output. Inside/orbit damage improved 0/0 → 44/22; the distant seed replay dealt 100 with seven accepted attacks. This samples the trained PPO distribution; there are no scripted enemy attacks or uniform random action overrides. Runtime metadata now explicitly binds the inference mode and rejects a mismatching flag. Evaluation defaults remain greedy unless `--sample` is explicitly selected. Cadence (3 ticks), application latency (1 tick), .16 s public cues, legal turn/commitment rules and one owner are unchanged.

Added bounded diagnostic/build/installation helpers under `Tools/Phase6Demo`, `Assets/Editor/Demo*`, opt-in `DemoLearnedVerification`, arena request/contact telemetry and separate development evaluation seed offsets. `RuntimeIntegrationValidation` now checks the bound inference mode and tampering. GameScene diff is limited to three hash bindings plus sampled-inference flag. No presentation polish was performed.

## Before-training prerequisites — actually executed

- PASSED: pinned venv `pip check`; compatible original policy/optimizer readable; unchanged 66×3 observation and 9/5 action contract.
- PASSED: `prerequisite-agent`, 135 checks, 19.546 s; masks, delayed cues, cadence/latency and ownership.
- PASSED: isolated `training-build`, 33.939 s; `prerequisite-reset-rewards`, 19.138 s, 1,000 dirty reset checks and 1,000 per each of eight arenas, reward reconciliation/isolation.
- PASSED: fresh movement artifact, 12 parity comparisons, max .008914 m / RMS .008550 m within .02/.01 limits. Native process exit0; wrapper initially failed because this validator writes PASS to its text artifact rather than stdout. Timestamp and all comparisons independently checked; failure retained.
- PASSED: planar facing/contact trace before training. See `prerequisites.json`. Earlier user results and unreported manual subchecks were accounted for; no new human results invented.

## Separate new training expenditure

Only one training block ran: **100060 → 300075**, **200,015 additional decisions**, **549.594 s active training process wall time** (9m9.594s), exit0, no watchdog timeout. Earlier task charges are excluded. Limits were 2,000,000 new decisions / 1,800 s; remaining 1,799,985 decisions / 1,250.406 s were deliberately unused after the immediate demo target was achieved. Evaluation/build/render time is separate and explicitly logged.

Resumed policy AND optimizer into an isolated copy; original checkpoint paths were rewritten in the copy to prevent pruning originals. Stage1 remains Passive only, randomized starts; no skipped promotion or self-play. PPO/network/reward settings remain approved. The exhausted original 100k LR schedule required a documented continuation horizon: max_steps=2,090,000 fixed across blocks, not reset per block. This reserves 10,060 decisions below the task's absolute cap of 2,100,060; target stop300060 drained 15 decisions. Checkpoints every50k, keep30. CPU/local only; seed71 training range remains distinct from held-out100000-family offsets and development400000. No held-out trajectories trained.

A living-cost .001→.01 hypothesis was prepared and its path unit-tested (`reward-v2-prerequisite`, unused TrainingV2 build), but **no training used it**. The prototype was removed after sampled-output testing resolved the open-floor stall. `reward-experiment-status.json` and archived prototype retain the attempt. Actual training reward remains -.001/s; no reward search occurred.

Stop reason: immediate scoped demo outcome achieved with selected sampled configuration, not budget expiry, not training reward and not a failed first gate. Stage2 training has not begun.

## Bounded checkpoint evaluations

Every match has a 30-s simulated episode cap; each process has the explicit bound below. All exited0, communicator off, reward accounting reconciled, zero illegal/mask/foreign events, unique family/seed pairs within each run. Development offset400000 was separate. Historical 89/200 Passive remains unchanged; current planar-adapter rerun of the original is 88/200 (not a rewrite of history).

EVALUATION_TABLE

Stage1 fixed held-out gate: original greedy **88/0/0/112**, mean damage44/0; new greedy **200/0/0/0**, new selected sampled **200/0/0/0**. Selected sampled wins are all **200 actual combat kills**, no opponent exits; 1,000 contacts, 1,020 accepted attack requests (1 light/1,019 heavy), zero rejects. Far starts:92/111 reached within2 m; remaining valid heavy contacts can occur beyond2 m, so this distance proxy is not a contact test. All200 dealt100 actual health damage. Mean free facing error .067°. Greedy's gate alone did not establish open-floor competence; paired execution and final rendering decided configuration.

W/L/D/T and mean **actual health damage dealt/received** per family:

COMPARISON_TABLE

These 10/family baseline and 5/family varied samples include all planned guard holding, guard tapping, dodge spam, retreat, corners, walls, attack/combo spam and delayed-attack strategies. They are **not** the complete 200-per-family final acceptance suite. Stage3/4 describe evaluation opponent/scenario coverage, not curriculum training promotion. Samples overlap earlier fixed seeds and must not be pooled into new independent Passive200 gates. Comparing greedy and sampled on the same held-out seeds is disclosed; the inference-mode hypothesis came from separate controlled execution, not repeated acceptance-set hyperparameter search.

Selected sampled baseline telemetry:

DETAIL_TABLE

All measured learned blocks/deflections are **zero**, across original/new variants. Selected combat choices for Passive200 are None28694 / Guard0 / Light1 / Heavy1019 / Dodge0: diversity remains poor. Per-run summaries and raw JSONL retain complete movement/combat histograms, requests beyond reach, contacts, facing-error and terminal reasons. DodgeRetreat/DodgeSpam/Retreat wins are predominantly/all opponent exits and do not prove fighting skill. Sampled Heavy0/10, Balanced0/10, GuardCounter0/10 and Aggressive2/10 fail useful broader quality. Varied Passive4/5 and Corner1/5 also warn of weak robustness. Full statistical exploit acceptance is unexecuted.

## Runtime and rendered verification

- PASSED: final `sampled-runtime-integration`, 68 checks, 12.797 s, offline CPU inference, actual GameScene300ticks, Normal3/ML2 unique results, player alive; missing/invalid/payload/tuning/mode faults, safe switching, stale callback protections, resources and Normal reload. This is numerical/interface validation, not all visual quality.
- PASSED: `install-selected-sampled`, 19.021 s; exact new export plus matching metadata/payload imported. `sampled-game-build` then `capture-fix-game-build` compiled the actual GameScene; final build28.984 s under300-s bound.
- FAILED fixture attempt: `block1-rendered`, tick0 before inference, no clear start pair around original player; corrected fixture chooses one clear pair from actual GameScene NavMesh. No gameplay navigation or model rule changed.
- FAILED visual evidence attempt: `sampled-rendered` had valid telemetry but 99 black backbuffer images from the hidden window. That is **not** a visual pass. Retained unchanged.
- PASSED corrected `sampled-texture-rendered`: explicit camera RenderTexture of actual GameScene, 500ticks /10simseconds, 40-s outer /25-s internal deadline; 99 nonblack frames; 167 learned decisions, five accepted requests, zero rejects/fallback, four contacts, **88 actual damage**, distance3.400→1.250 m. Actual installed model770ddd, sampled mode, communicator off. Captured frame0/frame40 and 3×3 contact sheet were viewed by the agent: enemy turns, approaches and performs successive authored strikes with target reaction. MP4 is 99 frames at10fps. This is a short actual learned-policy sample, not a claim of reliable human-duel quality.

Fixture disclosure: target and enemy are each placed once on existing GameScene navigation; starts outside contact and side-facing. Only the player target receives two short scripted lateral input intervals through its existing input/motor. A diagnostic camera follows the pair. No enemy choices, runtime model weights, movement authority or damage are overridden; controlled diagnostic policy was separate and never installed. Texture captures omit OnGUI HUD; damage numbers come from resolver telemetry. No audio acceptance claimed. Ordinary launch creates no verification object and keeps the original cameras/input/clock/default Normal.

## Installed identity and preservation

HASH_TABLE

Checkpoint/export: `Tools/Phase5/Combat/results/demo-stage1-20261117-71/block1-300075/`; root run retains optimizer and numbered checkpoints. `training-build-source-manifest.json` plus source ZIP identify what actually compiled for training; later source metadata includes subsequent diagnostics and is not falsely called the compilation snapshot. Runtime manifest separately binds exact adapter/movement source, training provenance, mode and evaluated checkpoint.

`preservation-audit.json` verifies every starting file present, exactly nine permitted existing files changed. All player sources/controllers/prefabs, seven CombatData assets, shared runner/resolver/motor and TimeManager unchanged; root motion, foot correction, authored transitions and Attack2 speed preserved. Original candidate/checkpoint, previous runtime copies and old `Builds/CombatDemo` retained. **New** selected standalone is `Builds/DemoPolicyCandidate/CombatDemo.exe`; do not confuse the retained old executable with the new export. No release/upload performed.

## Acceptance and remaining work

| Criterion | Status | Evidence / limit |
|---|---|---|
| Concrete adapter diagnosis and legal facing/contact repair | PASS | before/after traces, controlled interface test, unchanged motor limits |
| Toolchain/reset/reward/contract/movement before training | PASS | prerequisites.json; motion marker limitation retained |
| New local budget, resumable saves, held-out separation | PASS | budget.json, train-block1 records, checkpoint and bounded commands |
| Stage1 Passive ≥180/200 | PASS | selected sampled200 combat kills/200 |
| Immediate learned turning/approach/legal attack/real damage/no hidden fallback | PASS | inspected99-frame actual GameScene sample + trace and runtime68 checks; narrow scope |
| Correct selected export/configuration installed, safe switching/offline/default Normal | PASS | installed-identity, sampled-runtime-integration, rendered model hash |
| Player animation/root motion/Attack2/shared gameplay unchanged | PASS | static audit; no new full player rendered acceptance inferred |
| Learned defense and broader basic/Balanced gates | FAIL | zero defense; Heavy0/10, Balanced0/10 baseline sample |
| Full200-per-family final statistical/exploit acceptance | NOT VERIFIED — USER TEST REQUIRED | Model-run automation remains owed; intentionally not run for weak Stage1 policy, not delegated to user |
| Human duel/input-reading/readability and unreported original manual subchecks | NOT VERIFIED — USER TEST REQUIRED | Existing manual IDs remain; short scripted-target capture does not establish these |
| Main-project clean Console/full regression | NOT VERIFIED — USER TEST REQUIRED | Lab/build verified; known historical Editor SearchDatabase exception, main-project automation still owed |
| Full ML final acceptance | FAIL | quality gates missed; evaluation-only install only |
| Runtime release integration/publication | NOT APPLICABLE | Not performed; only existing evaluation slot updated |

Manual testing required from user: **YES, only existing human acceptance remains**. No repeat of automated facing/contact/inference/reset tests is requested. `MANUAL_TESTS.md` links exact legacy procedures and current model selection. User-reported P5-ENV-VIS/P5-VIS passes and Stunned-entry-only result are preserved; no other stun observations invented.

Blocking issues for **full ML acceptance**: poor defense/Heavy/Balanced/varied contact; full final suite and human policy review absent. No concrete execution blocker found in the successful bounded selected-model tests. Validation that could reveal a blocker: original unreported human animation/stun/readability checks and main-project-specific automation. Non-blocking/deferred: broader regression, optional assets/polish, Stage2 and later curriculum, release integration. One specific next experiment: bounded approved **Stage2 predictable light/heavy mixture, retaining Passive for evaluation**, starting this optimizer/checkpoint in sampled deployment mode; first verify Passive retention on separate development seeds, then run the unchanged Stage2 promotion gate. No automatic next run is started; continuation budget must remain explicitly accounted for.

RUN MANUAL TESTS BEFORE NEXT PROMPT
"""
for token,value in [("EVALUATION_TABLE","\n".join(evaluation_table)),("COMPARISON_TABLE","\n".join(comparison)),("DETAIL_TABLE","\n".join(detail)),("HASH_TABLE","\n".join(hash_table))]:
    results=results.replace(token,value)
save("RESULTS.md",results)

save("COMMANDS.md",r"""
# Reproducible local commands and bounds

Run from `D:\Unity Projs\AI CP\My project (2)` in PowerShell. Paths below are project-relative commands; use pinned `Tools/ml-venv/Scripts/python.exe`, not system Python. Native Unity/trainer launches required local sandbox escalation for licensing/localhost; no cloud resources used. Each wrapper logs exact executable, arguments, PID, timeout, exit and elapsed time in this directory. Use fresh run names. Never run multiple Lab Unity processes concurrently; the user's main Editor is untouched.

## Executed prerequisite/diagnostic/build commands

```powershell
& Tools/ml-venv/Scripts/python.exe -m pip check
& Tools/Phase6Demo/Invoke-Unity.ps1 -Method DemoExecutionDiagnosis.Run -RunName diagnosis-before
# After the one-line planar-facing fix:
& Tools/Phase6Demo/Invoke-Unity.ps1 -Method DemoExecutionDiagnosis.Run -RunName diagnosis-planar
& Tools/Phase6Demo/Invoke-Unity.ps1 -Method Phase5AgentValidation.Run -RunName prerequisite-agent -Marker PHASE5_AGENT_VALIDATION_PASS
& Tools/Phase6Demo/Invoke-Unity.ps1 -Method DemoPolicyBuild.Training -RunName training-build -Marker DEMO_POLICY_BUILD_PASS -Quit -TimeoutSeconds 300
& Tools/Phase6Demo/Invoke-Unity.ps1 -Method Phase5EnvironmentValidation.Run -RunName prerequisite-reset-rewards -Marker PHASE5_ENVIRONMENT_VALIDATION_PASS -TimeoutSeconds 180
# Initial movement wrapper lacked MarkerFile and reported missing stdout PASS.
& Tools/Phase6Demo/Invoke-Unity.ps1 -Method Phase5ArenaMotionValidation.Run -RunName prerequisite-movement -Marker PHASE5_SAME_ANIMATION_MOTION_PASS -TimeoutSeconds 180
# Native exit0/fresh text and 12 parity=True rows were independently verified.
# For a fresh repeat, also add -MarkerFile Logs/Phase5ArenaMotion.txt.
```

Exact original argument strings, including actual validator method names/markers, are authoritative in `prerequisite-*.run.json`; text artifact is `prerequisite-movement.txt`. Diagnosis defaults150s, internal90s. Prerequisite/build records carry their actual limits. The unused living-cost prototype/build/unit run is retained under `reward-*`, `training-v2-*`; no trainer used TrainingV2. Current source removed that option.

## Actual single training block

```powershell
& Tools/ml-venv/Scripts/python.exe Tools/Phase6Demo/manage_task.py prepare
& Tools/ml-venv/Scripts/python.exe Tools/Phase6Demo/manage_task.py train --block 1 --decisions 200000
```

`prepare` was run once; do not rerun on an existing run. It copied original optimizer/policy and rewrote copied checkpoint paths, retaining originals. `train-block1.run.json`/`budget.json` contain the exact executed trainer argv, config hash and time. Block target300060, final300075 (+200015); hard600/soft575s; actual549.594s. No additional training happened.

## Bounded Stage1 continuation reference — NOT executed

```powershell
# Current-stage continuation ONLY; this does not implement a Stage2 experiment:
& Tools/ml-venv/Scripts/python.exe Tools/Phase6Demo/manage_task.py train --block 2 --decisions 200000
```

Resumes `Tools/Phase5/Combat/results/demo-stage1-20261117-71/CombatV1/checkpoint.pt` at300075 with optimizer, fixed2090000 LR horizon, existing unchanged-reward Training/Combat.exe; new training seed10078, PPOseed71, eight arenas, Stage1Passive. Helper enforces cumulative ledger, caps target below remaining decisions with10,060 drain reserve, hard min(600, remaining cumulative seconds), soft hard−25s. At this handoff planned target500075, usual drain bounded by reserve, remaining task allowance1,799,985 decisions/1,250.406s. Do not silently start this or a Stage2 run from this handoff. A future scope must explicitly retain/reconcile this ledger. Stage2 requires the approved mixture/config/promotion logic, not changing this command's label. Watchdog timeout is a failed block and exact unsaved decisions must be audited before any further run; no timeout occurred here.

## Staging, evaluation and installation

```powershell
$candidate = 'Tools/Phase5/Combat/results/demo-stage1-20261117-71/block1-300075'
& Tools/ml-venv/Scripts/python.exe Tools/Phase6Demo/manage_task.py stage-model --source $candidate --sample
& Tools/Phase6Demo/Invoke-Unity.ps1 -Method DemoPolicyBuild.Evaluation -RunName sampled-evaluation-build -Marker DEMO_POLICY_BUILD_PASS -Quit -TimeoutSeconds 300
& Tools/ml-venv/Scripts/python.exe Tools/Phase6Demo/manage_task.py evaluate --name block1-sampled-dev-50 --family Passive --episodes 50 --stage 1 --offset 400000 --timeout 180 --sample
& Tools/ml-venv/Scripts/python.exe Tools/Phase6Demo/manage_task.py evaluate --name block1-sampled-gate-200 --family Passive --episodes 200 --stage 1 --offset 100000 --timeout 300 --sample
& Tools/ml-venv/Scripts/python.exe Tools/Phase6Demo/manage_task.py evaluate --name block1-sampled-families-10 --family all --episodes 10 --stage 3 --offset 100000 --timeout 300 --sample
& Tools/ml-venv/Scripts/python.exe Tools/Phase6Demo/manage_task.py evaluate --name block1-sampled-varied-5 --family all --episodes 5 --stage 4 --offset 100000 --timeout 300 --sample
& Tools/ml-venv/Scripts/python.exe Tools/Phase6Demo/manage_task.py prepare-install --source $candidate --evaluation block1-sampled-gate-200
& Tools/Phase6Demo/Invoke-Unity.ps1 -Method DemoPolicyBuild.InstallSelected -RunName install-selected-sampled -Marker DEMO_SELECTED_INSTALL_PASS -Quit
& Tools/Phase6Demo/Invoke-Unity.ps1 -Method RuntimeIntegrationValidation.Run -RunName sampled-runtime-integration -Marker RUNTIME_INTEGRATION_VALIDATION_PASS
& Tools/Phase6Demo/Invoke-Unity.ps1 -Method DemoPolicyBuild.BuildSelectedGameScene -RunName capture-fix-game-build -Marker DEMO_SELECTED_GAME_BUILD_PASS -Quit -TimeoutSeconds 300
& Tools/Phase6Demo/Verify-Rendered.ps1 -RunName sampled-texture-rendered
```

All above named runs already exist; use NEW names for a repeat. Raw `.run.json` is exact executable argv evidence (not an invented terminal transcript). Original-fixed and new-greedy evaluations used the same commands WITHOUT `--sample`, with their respective source and names listed in RESULTS.md. Models were staged/built separately per source; don't stage a different export and assume an old executable changes. `selected-execution` used `DemoExecutionDiagnosis.Run`; paired sampled `block1-sampled-execution` used `DemoExecutionDiagnosis.RunStaged`. Both150s outer bounds. Old/provisional installs and failed render attempts are retained, not overwritten. Do not run the historical `RuntimeEvaluationSetup.Install`/`BindPayload` on the newly selected slot; those helpers bind the previous566e export.

## Render evidence

Final wrapper40s, internal25s,500ticks; real graphics (no batchmode/no nographics), hidden window, explicit Camera.Render/RenderTexture capture so hidden backbuffer cannot yield a false visual pass. First `sampled-rendered` black captures were rejected; final99 nonblack texture frames were viewed. Texture excludes OnGUI labels. The actual model's identity/damage is separately verified in JSON/log. No desktop input/focus was automated.

```powershell
& 'C:\ffmpeg\ffmpeg.exe' -y -loglevel error -framerate 10 -i 'Logs/DemoPolicy20261117/sampled-texture-rendered/frame-%04d.png' -c:v libx264 -pix_fmt yuv420p -movflags +faststart 'Logs/DemoPolicy20261117/learned-verification.mp4'
& 'C:\ffmpeg\ffmpeg.exe' -y -loglevel error -i 'Logs/DemoPolicy20261117/learned-verification.mp4' -vf 'fps=1,scale=480:-1,tile=3x3' -frames:v 1 'Logs/DemoPolicy20261117/rendered-contact-sheet.png'
```

`write_handoff.py` regenerates results from existing immutable run evidence and checks preservation/model identities; it never trains, launches Unity, or changes model assignment. Full final200-per-family acceptance has not run. STOP after handoff.
""".replace(r"\u005c", "\\"))

save("MANUAL_TESTS.md", """
# Selected learned candidate: current Editor usage and pending human acceptance

Current candidate770ddd31a64d, checkpoint300075, **sampled inference**, evaluation-only. Stage1Passive200/200 actual kills passed; broader defense/duel acceptance fails. Normal remains saved default. No trainer/Python is needed for play.

1. Stop Play Mode. Open **Assets/Scenes/GameScene.unity** from Project (reopen from disk if an already-open scene still shows the old566e binding; preserve any personal unsaved edits separately). Wait for compilation/import. This task used a separate Lab; it did not reload or save your open Editor.
2. Select **Enemy**. Enemy Brain Host **Mode=Normal** by default; Agent Policy remains assigned to **Enemy/Evaluation-only combat policy**. Player > Combat Debug Controls > Enable Defense Debugging stays off.
3. Select that child to inspect: Behavior Parameters **Inference Only / CPU Burst / Deterministic Inference OFF**. Enemy Learning Agent Runtime Evaluation Only ON, training/test heuristic OFF. Installed model `Assets/RuntimeEvaluationCandidate/CombatV1-EvaluationOnly.onnx`, metadata beside it; model SHA starts **770ddd31a64d**, exact metadata SHA starts **5c786e4c2561**. Do not change inference mode independently: metadata mismatch safely rejects it. No need to edit these installed settings.
4. Enter Play Mode. Select **Enemy > Enemy Brain Host > Select ML — evaluation only** (or component context menu Controller > Select ML). Confirm Selected ML / Active ML and no Fallback. Switching during commitment waits for Free. The existing failed-training-gates warning means complete final gates remain unmet; Stage1 has now passed.
5. Click Game view. Existing controls: WASD move, mouse look, Q/middle mouse lock-on, Space jump, Shift press dodge/hold run, LMB light/fresh combo clicks, running LMB heavy, RMB guard/fresh deflection. The learned model should legally turn, close space and use mainly heavy attacks; it has no demonstrated defense and can lose badly to heavy/balanced opposition. This is actual learned inference, not the controlled diagnostic interface policy.
6. Click **Select Normal** to return; current action completes first and resources persist. Stop/re-enter to restore saved Normal. No release integration or new player hotkeys were added.

New selected local standalone: **Builds/DemoPolicyCandidate/CombatDemo.exe** (keep its complete directory). Ordinary launch remains Normal. The retained **Builds/CombatDemo/CombatDemo.exe** is the older export; do not use it to judge the new model. Editor Inspector buttons provide interactive policy selection. `Tools/Phase6Demo/Verify-Rendered.ps1 -RunName NEW-NAME` is the bounded actual-model/scripted-target test, not ordinary interactive play.

## Human results retained exactly

| ID | Result | Scope |
|---|---|---|
| P5-ENV-VIS | PASS — prior user report | Visual-test pass; no added observations invented |
| P5-VIS | PASS — user report | Controls work well; disabling/re-enabling agent mid-attack works correctly |
| ST-STATE entry | PASS — user report | Enemy successfully enters Stunned only |
| ST-STATE other subchecks | NOT VERIFIED — USER TEST REQUIRED | Duration/stationary/hit-timer/repeat/death observations still unreported |

No repeat of automated facing/range/contact/switch/fallback tests is requested. Agent inspected actual rendered frames and numerical records for those narrow checks. Current original human **DEMO-REHEARSAL**, **ST-STATE-REMAINING**, and the human readability/input-reading portion of **DEMO-ML** remain pending; short scripted-target evidence does not prove them. Purpose, Setup, numbered Steps, Expected result and Failure signs remain in [prior manual guide](../RuntimeIntegration20261117/MANUAL_TESTS.md#pending-human-checks), with model/mode/build instructions above superseding its old566e/deterministic instructions. Other legacy IDs and optional ASSET-PREVIEW remain as recorded there; no new assets were assigned. Full statistical suite is owed by automation, not by manual user execution.

For a human policy review use existing DEMO-ML procedure with the new model settings above; report whether delayed turns/telegraphs/recovery are readable and punishable and whether any response appears to read hidden inputs. This is genuinely human assessment. Remaining broader player animation and stun procedures keep their original expected behavior; don't infer them from enemy capture. No new mandatory manual test was added by this task.

RUN MANUAL TESTS BEFORE NEXT PROMPT
""")

progress = """
# Implementation progress

## Current phase

Handoff updated 18 November 2026 (local time); run identifiers retain 20261117 UTC experiment date. Bounded execution diagnosis and learned-policy improvement, 17 November 2026. **Normal remains the saved default.** New candidate770ddd31a64d is installed in the existing evaluation-only slot, sampled inference. The immediate learned demonstration target is verified in a bounded real GameScene rendered sample; complete ML acceptance is not achieved. No further training or runtime release integration follows this handoff.

## Current task

Task status: **PARTIAL SUCCESS**. Fixed a concrete ML facing adapter mismatch, resumed approved Stage1 locally, selected the trained sampled policy using contact/held-out/rendered evidence, and updated its exact model/metadata binding. **200,015 additional decisions /549.594 seconds active training** were spent separately from earlier tasks (caps2m/1800s). Only one block ran. Stopped early after the immediate demo target was achieved; unused budget was not silently spent. No cloud/self-play/reward search/gameplay redesign.

Current authoritative report: [RESULTS.md](Logs/DemoPolicy20261117/RESULTS.md), [commands and continuation](Logs/DemoPolicy20261117/COMMANDS.md), [exact Editor selection/manual guide](Logs/DemoPolicy20261117/MANUAL_TESTS.md). Previous entire progress is retained at [BeforeProgress.md](Logs/DemoPolicy20261117/BeforeProgress.md); its old installed566e model/89-200 failure/default inference/build instructions are historical, superseded below. User results are retained exactly.

## Completed tasks / diagnosis and implementation

- ML already called motor.Face; its nonplanar delayed vector stalled constant side/rear turning on the actual FreezeRotation Rigidbody. Passed existing delayed planar ObservedTargetDirection instead, matching Normal. Side/rear error86.4°/176.4° →0°, turn cap180°/s and active/recovery commitment preserved. No motor/constraints/timing change.
- Controlled legal interface tests (explicitly NOT LEARNED, never installed) and original learned tests trace cue/mask/action/request/runner/motor/rotation/contact paths across ahead/sides/rear/orbit and range/phase boundaries. Old seed100001 still made18 accepted out-of-range heavy requests/zero damage after facing repair: a separate policy failure, not fallback or stale cues.
- Resumed readable original policy AND optimizer into isolated run, fixed documented continuation LR horizon2090000 with drain reserve, approved PPO/network/rewards and Stage1Passive. One200k block ended300075. No curriculum promotion skipped; Stage2 has not trained.
- New greedy model passed Passive but stalled on open floor. Same learned sampled output resolved controlled inside/orbit stalls (0/0 →44/22 damage). Pinned package supported output, no scripted enemy override. Runtime metadata binds sampled mode, tests reject mismatches. Default harness stays deterministic unless explicitly requested.
- Added bounded execution/build/install/render helpers, telemetry and separate development seed offset. Changed GameScene only three identity hashes and inference flag. Existing evaluation-only slot reused; no new gameplay flow.
- New selected build **Builds/DemoPolicyCandidate/CombatDemo.exe**; old candidate/checkpoints/runtime copies/**Builds/CombatDemo** retained. Player source/controllers, root motion/foot correction, authored combos/Attack2 1x, shared motor/runner/resolver/CombatData/TimeManager unchanged per audit.

## Current installed model / configuration identity

HASH_TABLE

Source checkpoint/export: **Tools/Phase5/Combat/results/demo-stage1-20261117-71/block1-300075/**. Resumable root **.../CombatV1/checkpoint.pt** and numbered300075 checkpoint retain optimizer state. `configuration.yaml` beside snapshot and exact `train-block1.yaml`/argv/budget are archived. Runtime manifest includes evaluated mode, movement/contract/tuning hashes and training compilation provenance. SourceZIP/manifest captures the actual training build before later diagnostics; no provenance rewrite of the old run. Workspace has no Git revision.

## Validation performed — current task

| Check | Result / evidence |
|---|---|
| Toolchain/agent/reset/reward/movement before training | PASSED; pip check, agent135, 1,000 dirty resets plus1,000×8 arenas, 12 movement parity runs (.008914 max/.008550 RMS), actual planar contact trace; prerequisites.json |
| Training block1 | PASSED exit0, inherited100060 →300075 (+200015),549.594s; hard600/soft575s; no timeout; original checkpoints retained |
| Current original Passive gate | FAILED88/200,112timeouts,44 mean damage; historical frozen89/200 preserved |
| Selected sampled Stage1 Passive gate | PASSED200/200 actual combat kills,100 damage each,1,000 contacts,zero rejects; fixed held-out100000 offset; process300s bound |
| Selected representative baseline families | PASSED collection/legality, FAILED broad quality;150episodes,10/family,300s bound; Heavy0/10,Balanced0/10,zero learned defense |
| Selected varied families/exploits | PASSED collection/legality, FAILED robustness;75episodes,5/family,300s bound; Passive4/5,Corner1/5; full200/family NOT run |
| Final runtime integration | PASSED68 checks,12.797s; offline CPU300ticks, Normal3/ML2 unique results, faults/mode tamper/switch/fallback/resources/stale callbacks/default Normal |
| Final selected GameScene build | PASSED28.984s under300s; source and new model compiled |
| Actual rendered selected policy | PASSED sampled-texture-rendered:500ticks,99 nonblack frames inspected,167 decisions,5accepted/0rejected,4contacts/88damage,3.400→1.250m,no fallback,offline;40s outer/25s internal bounds |
| Preservation and identity audit | PASSED nine permitted existing files changed,none missing; all protected player/shared assets/source unchanged; original export/checkpoint retained |

Per-family W/L/D/T, damage exchange, accepted/rejected reasons, contacts, action diversity, facing and kill/exit counts are in RESULTS.md and raw summaries. AlwaysGuard/GuardTap/DodgeSpam/CornerCamp/Retreat/WallAttack/ComboSpam/DelayedAttack were all sampled. Retreat/dodge opponent-exit wins are explicitly not combat competence. None of these small family samples is the complete final acceptance suite. Fixed acceptance seeds never trained; sampled-vs-greedy held-out comparison is disclosed, hypothesis came from separate controlled execution.

Retained failures: initial movement wrapper stdout-marker error despite fresh passing text; unused reward-cost prototype/build/unit-check (NOT TRAINED, code removed); initial render fixture no valid starting pair before inference; hidden-backbuffer99 black frames rejected as visual proof. Corrected explicit camera texture capture passes and frames were viewed. Earlier SearchDatabase Editor indexing exception remains historical, not a new clean-main-Editor claim. No new user control/animation acceptance inferred.

## Manual test results — user reported

| ID | Result | Exact scope |
|---|---|---|
| P5-ENV-VIS | PASS | Prior user-reported visual pass; no additional observations invented |
| P5-VIS | PASS | Controls work well; disabling/re-enabling agent mid-attack works correctly |
| ST-STATE entry | PASS | Enemy successfully enters Stunned only |
| ST-STATE other subchecks | NOT VERIFIED — USER TEST REQUIRED | Original duration/stationary/hit-timer/repeat/death observations remain unreported |

## Pending manual tests

**Manual testing required from user: YES — existing human acceptance only.** Current numbered model selection steps plus links to original Purpose/Setup/Steps/Expected/Failure fields: [MANUAL_TESTS.md](Logs/DemoPolicy20261117/MANUAL_TESTS.md). No repeat of automated facing/contact/reset/fallback checks is requested.

- DEMO-REHEARSAL: original human player controls/combos/feet/camera/audibility on personal display/input setup; narrow enemy rendered capture does not pass this whole procedure.
- DEMO-ML: remaining human telegraph/recovery/input-reading/readability judgment with770ddd sampled candidate; numerical switching/fallback/identity were automated and passed.
- ST-STATE-REMAINING: only original unreported duration/stationary/hit-timer/repeat/death checks; entry already passed.
- Other original unreported manual IDs/conditional ASSET-PREVIEW retained in prior guide/history. No new assets assigned.

Unexecuted **automation**, not delegated to user: full200-per-family statistical/exploit suite and main-project-specific regression/clean Console. Lab/runtime/build tests do not establish these. Training no later than Stage1 means broad policy quality cannot be claimed.

## Individual acceptance criteria

| Criterion | Status | Evidence / remaining limit |
|---|---|---|
| Concrete execution diagnosis and legal facing repair | PASS | before/planar traces; matched delayed public planar direction and turn/phase limits |
| Toolchain/reset/reward/observation/action/movement prerequisites | PASS | prerequisites.json before any training |
| New local cumulative budget and held-out separation | PASS | budget.json:200015/2m;549.594/1800s; separate training/development/eval seeds |
| Resumable checkpoints and bounded commands | PASS | snapshot/root optimizer, config/argv, COMMANDS.md |
| Approved Stage1 curriculum, no self-play/privileges | PASS | Passive stage1 config; no later training |
| Stage1 promotion threshold ≥180/200 | PASS | selected sampled200 kills/200 |
| Immediate actual learned visible facing/approach/attacks/damage | PASS | inspected actual GameScene render plus167 decisions/four contacts/88 damage; narrow sample |
| Offline compatible new model/mode identity and safe switching/fallback | PASS | installed hashes + runtime68 + rendered selected hash |
| Normal saved default/evaluation-only retention | PASS | saved scene/reload/manifest; old model/checkpoint retained |
| Protected player/shared gameplay preserved | PASS | preservation-audit; no broadened player animation acceptance |
| PredictableHeavy/Balanced/defense quality | FAIL | baseline0/10 Heavy,0/10 Balanced,zero learned blocks/deflections |
| Complete final200-per-family/exploit acceptance | NOT VERIFIED — USER TEST REQUIRED | Automated suite still owed; partial sample clearly labelled, not user-run task |
| Human telegraph/recovery/input-reading and original manual subchecks | NOT VERIFIED — USER TEST REQUIRED | Existing DEMO-ML/REHEARSAL/ST-STATE-REMAINING procedures |
| Main-project clean Console/full regression | NOT VERIFIED — USER TEST REQUIRED | Automation still owed; historical SearchDatabase exception retained |
| Complete final ML success | FAIL | Broader gates missed; selected candidate evaluation-only |
| Runtime release integration/publication | NOT APPLICABLE | Not performed; stop at authorized evaluation-only model assignment |

## Known blockers / problems

Blocking for **full ML acceptance**: zero defense, failed Heavy/Balanced and varied contact/spacing, incomplete statistical/human acceptance. No concrete execution blocker found in successful bounded selected-model checks. Pending human animation/stun/readability and main-project regression could still reveal blockers; they are not silently passed. Native startup/indexing/licensing warnings remain in retained logs; no entirely clean Console claim.

## Deferred TODOs / specific next experiment

One next experiment: approved **Stage2 predictable light/heavy mixture, retaining Passive for evaluation**, resume300075 optimizer, retain sampled deployment, evaluate Passive retention on separate development seeds then unchanged Stage2 gate. No experiment is started automatically. Stage1-only exact bounded continuation command is in COMMANDS.md; it is not mislabeled Stage2. Full final suite only when justified. Optional asset/presentation work, broad release integration and unrelated legacy validation remain deferred; they did not consume this training scope.

## Exact demonstration steps

1. Stop Play Mode; open/reopen **Assets/Scenes/GameScene.unity** from disk, preserving personal unsaved edits separately. Wait for import/compile. User's already-open Editor was not reloaded by Lab changes.
2. Confirm Enemy > Enemy Brain Host saved **Normal**. Child Evaluation-only combat policy already binds770ddd model, metadata5c786e, **Deterministic Inference OFF**, InferenceOnly/CPU Burst. Leave training/test heuristic/debug off.
3. Enter Play; Enemy > Enemy Brain Host > **Select ML — evaluation only**. Confirm Selected/ActiveML, no fallback and770ddd hash. Play with existing controls; return with Select Normal. Commitment completes normally.
4. New local standalone **Builds/DemoPolicyCandidate/CombatDemo.exe** starts Normal; use Editor for interactive Inspector candidate selection. Old Builds/CombatDemo is retained previous export. Short actual-model evidence: [video](Logs/DemoPolicy20261117/learned-verification.mp4), [capture summary](Logs/DemoPolicy20261117/sampled-texture-rendered/verification.json), [contact sheet](Logs/DemoPolicy20261117/rendered-contact-sheet.png).

## Recommendation

RUN MANUAL TESTS BEFORE NEXT PROMPT
""".replace("HASH_TABLE", "\n".join(hash_table))
(ROOT / "IMPLEMENTATION_PROGRESS.md").write_text(progress.strip()+"\n",encoding="utf-8")

# Archive the comparison/provenance beside this selected run without touching originals.
run = pathlib.Path(selection["source"]).parent
archive = run / "evaluations/DemoPolicy20261117"
archive.mkdir(parents=True, exist_ok=True)
for pattern in ["*.summary.json", "*.jsonl"]:
    for file in OUT.glob(pattern):
        shutil.copy2(file, archive / file.name)
for name in ["RESULTS.md", "COMMANDS.md", "MANUAL_TESTS.md", "budget.json", "selected-source.json", "installed-identity.json", "prerequisites.json", "preservation-audit.json", "training-build-source-manifest.json", "checkpoint-validation.json", "run-summary.json"]:
    shutil.copy2(OUT / name, archive / name)

paths = list(ROOT.glob("Assets/Editor/Demo*")) + list(ROOT.glob("Assets/Scripts/Combat/DemoLearnedVerification*")) + list(ROOT.glob("Tools/Phase6Demo/*.py")) + list(ROOT.glob("Tools/Phase6Demo/*.ps1"))
paths += [ROOT / p for p in changed]
paths += list(pathlib.Path(selection["source"]).glob("*"))
paths += [OUT / "selected-source.json", OUT / "installed-identity.json", OUT / "budget.json", OUT / "checkpoint-validation.json", OUT / "run-summary.json", ROOT / "Logs/RuntimeIntegration20261117/MANUAL_TESTS.md"]
paths += [ROOT / "IMPLEMENTATION_PROGRESS.md", OUT / "RESULTS.md", OUT / "COMMANDS.md", OUT / "MANUAL_TESTS.md", ROOT / "Builds/DemoPolicyCandidate/CombatDemo.exe", ROOT / "Builds/DemoPolicyCandidate/CombatDemo_Data/Managed/Assembly-CSharp.dll", OUT / "learned-verification.mp4"]
(OUT / "final-manifest.json").write_text(json.dumps({str(p.relative_to(ROOT)):sha(p) for p in paths if p.is_file()},indent=2))
print(json.dumps(dict(passed=True, additionalDecisions=budget["additionalDecisions"], activeTrainingSeconds=round(budget["trainingProcessWallSeconds"],3), model=selection["modelSha256"], protectedExistingFilesUnchanged=audit["protectedExistingFilesUnchanged"], evaluationEpisodes=sum(x["episodes"] for x in runs.values()), archive=str(archive)),indent=2))
