"""Reconcile the completed bounded experiment; never launches training/evaluation."""
import collections
import hashlib
import json
import math
import pathlib
import shutil
import zipfile

ROOT = pathlib.Path(__file__).resolve().parents[3]
OUT = ROOT / "Logs/BoundedTraining20261117"
RUN = ROOT / "Tools/Phase5/Combat/results/combat-stage1-bounded-20261117-23b"
PILOT = ROOT / "Tools/Phase5/Combat/results/phase5-combat-pilot-23b"
NAMES = ["pilot-passive-200", "pilot-families-20", "candidate-passive-200",
         "candidate-families-20", "candidate-generalization-20"]


def read(path):
    return json.loads(path.read_text())


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def outcomes(s):
    return "/".join(str(s[k]) for k in ("win", "loss", "draw", "timeout"))


reports, details, totals = {}, {}, {}
train_rows = [json.loads(x) for x in (OUT / "training.jsonl").read_text().splitlines()]
train_seeds = {r["seed"] for r in train_rows}
assert all(not r["evaluation"] and r["seed"] >= 1000000 for r in train_rows)
for name in NAMES:
    run = read(OUT / (name + ".run.json"))
    assert run["exitCode"] == 0 and not run["timedOut"] and run["wallSeconds"] < 300
    reports[name] = read(OUT / (name + ".summary.json"))
    details[name] = read(OUT / (name + ".details.json"))
    assert reports[name]["valid"] and reports[name]["fixedHeldOutSeeds"]
    rows = [json.loads(x) for x in (OUT / (name + ".jsonl")).read_text().splitlines()]
    assert not train_seeds.intersection(r["seed"] for r in rows)
    assert all(r["evaluation"] and r["seed"] < 1000000 for r in rows)
    assert len({(r["family"], r["seed"]) for r in rows}) == len(rows)
    assert all(r["illegal"] == r["maskViolations"] == r["foreignEvents"] == 0 for r in rows)
    assert all(abs(r["reward"] - sum(r[k] for k in ["terminalReward", "damageReward",
        "defenseReward", "shapingReward", "livingReward"])) < .0005 for r in rows)
    assert all(0 <= r["defenseReward"] <= .050001 for r in rows)
    log = (OUT / (name + ".player.log")).read_text()
    assert "communicator=False" in log and "PHASE5_HELD_OUT_COMPLETE" in log
    assert "InvalidOperationException" not in log and "NullReferenceException" not in log
    counts = collections.Counter(r["outcome"] for r in rows)
    choices = [sum(r["combat"][i] for r in rows) for i in range(5)]
    n = sum(choices)
    totals[name] = dict(episodes=len(rows), outcomes={k:counts[k] for k in ("win", "loss", "draw", "timeout")},
        meanDamageDealt=sum(r["dealt"] for r in rows)/len(rows),
        meanDamageReceived=sum(r["received"] for r in rows)/len(rows),
        blocks=sum(r["blocks"] for r in rows), deflections=sum(r["deflections"] for r in rows),
        rejected=sum(r["rejected"] for r in rows), illegal=0, maskViolations=0, foreignEvents=0,
        combatChoices=choices, combatChoiceOrder=["None", "Guard", "Light", "Heavy", "Dodge"],
        combatEntropyBits=-sum(c/n*math.log2(c/n) for c in choices if c),
        terminalReasons=dict(collections.Counter(r["terminalReason"] for r in rows)),
        wallMasks=sorted({r["wallMask"] for r in rows}),
        movementRange=[min(r["movementFactor"] for r in rows),max(r["movementFactor"] for r in rows)],
        telegraphRange=[min(r["telegraphFactor"] for r in rows),max(r["telegraphFactor"] for r in rows)],
        distanceRange=[min(r["startDistance"] for r in rows),max(r["startDistance"] for r in rows)],
        processWallSeconds=run["wallSeconds"], hardTimeoutSeconds=run["timeoutSeconds"])
    target = (PILOT if name.startswith("pilot") else RUN) / "evaluations/20261117"
    target.mkdir(parents=True, exist_ok=True)
    for suffix in [".jsonl", ".summary.json", ".details.json", ".identity.json", ".run.json", ".contract.json"]:
        shutil.copy2(OUT / (name + suffix), target / (name + suffix))

assert not reports["candidate-passive-200"]["promotionEligible"]
assert all(not reports[name]["promotionEligible"] for name in NAMES)
training = read(OUT / "training-retry.run.json")
assert training["actualTrainerDecisions"] == 100060
assert training["allTaskTrainingWallSeconds"] < 1800
metadata = read(RUN / "metadata.json")
selection = dict(evaluationOnly=True, runtimeIntegrationEligible=False,
    selectedRun=str(RUN), model="CombatV1.onnx", exportSha256=sha(RUN / "CombatV1.onnx"),
    metadataSha256=sha(RUN / "metadata.json"), configSha256=sha(OUT / "stage1-100k.yaml"),
    trainerConfigurationSha256=sha(RUN / "configuration.yaml"),
    agentContractSourceSha256=sha(ROOT / "Assets/AgentContract/AgentContract.cs"),
    runtimeContractSha256=sha(OUT / "training.contract.json"), tuningHash=metadata["tuningHash"],
    packageManifestSha256=sha(ROOT / "Packages/manifest.json"),
    packageLockSha256=sha(ROOT / "Packages/packages-lock.json"), trainerStep=100060,
    totalTrainingProcessWallSeconds=training["allTaskTrainingWallSeconds"],
    rationale="Best measured Stage-1 contact candidate among the pilot and final export: 89/200 Passive wins versus 0/200. Not an overall accepted combat policy; defensive and predictable-heavy regressions are retained explicitly.",
    failedGate="Stage 1 needs >=180/200 Passive wins; measured 89 wins and 111 timeouts.",
    nextInvestigation="Trace public distance, movement mask and attack commitment in failed Passive seed 100001: 481/500 Stop choices, 18 heavy choices, zero damage. Determine why the policy repeatedly commits outside contact range. Do not change shared timing or movement.",
    pendingManual=["P5-ENV-VIS", "P5-VIS", "ST-STATE", "retained earlier IDs"],
    continuationCommand="Tools/ml-venv/Scripts/python.exe Tools/Phase5/Combat/bounded_task.py resume",
    continuationExecuted=False, pilotRetained=str(PILOT))
(RUN / "evaluation-only-selection.json").write_text(json.dumps(selection, indent=2))
(OUT / "selection.json").write_text(json.dumps(selection, indent=2))
(OUT / "measured-results.json").write_text(json.dumps(dict(training=training, evaluations=totals), indent=2))

lines = ["# Bounded training and evaluation — 17 November 2026", "",
    "Task status: **PARTIAL SUCCESS**. The bounded experiment is complete. The policy failed promotion; Normal stays default. No runtime release integration was attempted.", "",
    "## Budget and provenance", "",
    f"Local CPU only. Actual final trainer decisions: **100,060**. Successful training process: **{training['wallSeconds']:.3f} s**. Including the zero-collection sandbox startup failure: **{training['allTaskTrainingWallSeconds']:.3f} s** (4 min 20.890 s), charged conservatively against 1,800 s. Last trainer-reported elapsed time: 244.007 s. Stop target 100,000; trajectory drain added 60. No second training candidate, hyperparameter search, stage promotion or self-play followed. The two-million-decision / 30-minute limits were not exhausted; training stopped on the predeclared failed Stage-1 gate.", "",
    "The approved Stage-1 mixture was Passive only, seed 23, eight arenas. All other families were evaluation opponents. PPO hyperparameters, observation/action shape, shared gameplay and authored assets were preserved. Stop/log/checkpoint settings changed; linear LR decay follows the shortened stop horizon. All six existing configs parsed in the pinned trainer. Historical numeric prerequisites were re-read and bound to 137 unchanged source/build hashes; those Unity suites were not rerun. pip check passed. No manual test was claimed passed.", "",
    "## Bounded evaluation runs", "",
    "Each process had a **300-second hard timeout**; each episode had the existing 1,500-tick / 30-s simulated timeout. Stage-1 Passive and Stage-3 Passive samples overlap; they are never pooled. Fixed held-out seeds are separate from actual training seeds. All evaluations had communicator=False and deterministic local Burst CPU inference.", "",
    "| Run | Episodes | W/L/D/T | Mean actual dealt/received | Blocks/deflections | Rejected | Process seconds |",
    "|---|---:|---|---|---|---:|---:|"]
for name, total in totals.items():
    lines.append(f"| {name} | {total['episodes']} | {outcomes(total['outcomes'])} | {total['meanDamageDealt']:.3f}/{total['meanDamageReceived']:.3f} | {total['blocks']}/{total['deflections']} | {total['rejected']} | {total['processWallSeconds']:.3f} |")
lines += ["", "All runs: zero accepted illegal actions, mask violations and foreign events; reward components and defense caps reconcile; no observed gameplay exceptions/crashes or invalid-observation aborts. Native Unity telemetry DNS failures are present in sandboxed logs; a clean Console/network log is not claimed. No cloud training was used.", "",
    "## Per-family comparison — diagnostic samples", "",
    "**N=20 per family per column**, not the final acceptance suite. Damage columns are per-episode means. W/L/D/T is from the learner's perspective. Stage 3 is fixed baseline tuning; Stage 4 is the approved varied scenario, not additional training.", "",
    "| Family | Pilot W/L/D/T | New W/L/D/T | Pilot dealt/received | New dealt/received | New stage-4 W/L/D/T | Stage-4 dealt/received |",
    "|---|---|---|---|---|---|---|"]
families = list(details["candidate-families-20"])
for family in families:
    p, c, g = [reports[name]["families"][family] for name in ["pilot-families-20", "candidate-families-20", "candidate-generalization-20"]]
    lines.append(f"| {family} | {outcomes(p['outcomes'])} | {outcomes(c['outcomes'])} | {p['meanDamageDealt']:.1f}/{p['meanDamageReceived']:.1f} | {c['meanDamageDealt']:.1f}/{c['meanDamageReceived']:.1f} | {outcomes(g['outcomes'])} | {g['meanDamageDealt']:.1f}/{g['meanDamageReceived']:.1f} |")
lines += ["", "## Defense, action diversity and exploit interpretation", "",
    "The new candidate used **zero blocks and zero deflections in every held-out run**. The pilot's 300-episode baseline sample had 552 blocks and 283 deflections. This is a measured defensive regression, not an accepted tradeoff. PredictableHeavy changed from pilot 8/20 wins to new 0/20. GuardCounter changed from 20/20 timeouts to new 20/20 losses. Balanced stayed at zero wins and worsened from 18/20 to 20/20 losses. Earlier opponents/checkpoints are retained; the newer export is not described as uniformly stronger.", "",
    "Raw callback counts (None/Guard/Light/Heavy/Dodge; these include forced/masked None and are not conditional action preferences):", ""]
for name in ["pilot-families-20", "candidate-families-20", "candidate-generalization-20"]:
    t = totals[name]
    lines.append(f"- {name}: {t['combatChoices']}; raw combat entropy {t['combatEntropyBits']:.4f} bits. Terminal causes: {t['terminalReasons']}.")
g = totals["candidate-generalization-20"]
lines += ["", "AlwaysGuard, GuardTap, DodgeSpam, CornerCamp, Retreat, WallAttack, ComboSpam and DelayedAttack each ran 20 baseline episodes for both candidates and 20 varied episodes for the new candidate. The new baseline candidate lost 13/20 to WallAttack, 12/20 to DelayedAttack and 3/20 to ComboSpam. No sampled exploit family exceeded 80% opponent wins, but N=20 cannot establish the complete planned exploit gate. Guard holding/tapping and corners still produced many timeouts. All new DodgeSpam baseline wins and most Retreat wins were opponent exits, not combat kills; zero-damage wins must not be presented as learned punishment.", "",
    f"Varied scenario sampled wall masks {g['wallMasks']}, movement factors {g['movementRange']}, telegraph factors {g['telegraphRange']}, starting distances {g['distanceRange']}. Both actors use the approved symmetric variants. This is inference/reset evidence, not full movement equivalence for every variant. The existing wallAttackRequests metric counts the learner's hidden-target requests, not the opponent's wall-trigger branch; it was zero. Exact opponent attack-against-wall branch coverage is uninstrumented and remains unverified. Running the WallAttack family is not evidence that every wall exploit trigger occurred.", "",
    "18 runner rejections occurred in the new baseline sample and 29 in the varied sample; their reasons are not retained by the existing episode schema. They are distinct from accepted illegal actions (zero) and mask violations (zero). Per-family counts, opponent defenses, guard/corner/retreat duration and action histograms are in each .summary.json and .details.json. No new request-timing advantage was added.", "",
    "## Selection and exact failed criteria", "",
    selection["rationale"], "",
    "- Stage-1 promotion **FAIL**: 89/200 = 44.5%; need >=180/200 = 90%.",
    "- Basic-opponent 90% quality **FAIL** in the baseline sample: Passive 9/20, PredictableLight 4/20, PredictableHeavy 0/20. Passive's separate complete stage gate is 89/200.",
    "- Balanced >=50% quality **FAIL** in both new samples: 0/20 each.",
    "- Duel timeout threshold was met in the samples, but 200-per-family coverage was not run. Sample compliance is not final acceptance.",
    "- Final suite **NOT VERIFIED**: only the Stage-1 Passive gate has 200 episodes; other families have 20 per scenario. Full automatic acceptance was intentionally deferred after promotion failed, not delegated to the user.",
    "- Human readability/input-reading/animation/stun acceptance **NOT VERIFIED — USER TEST REQUIRED**. Existing pending test IDs are retained below in the progress file.", "",
    "## Model and continuation", "",
    f"Evaluation-only model: `{RUN.relative_to(ROOT).as_posix()}/CombatV1.onnx`.", "",
    f"Model SHA256 `{selection['exportSha256']}`; config SHA256 `{selection['configSha256']}`; contract source SHA256 `{selection['agentContractSourceSha256']}`; tuning hash `{selection['tuningHash']}`. Full metadata/runtime-contract/package/config hashes are in selection.json. These identify an unsuccessful evaluation candidate, not a runtime integration approval.", "",
    "Resumable policy/optimizer checkpoint: CombatV1/checkpoint.pt and CombatV1-100060.pt. Exact commands, timeout procedure, failures, source-asset restoration and future continuation are in COMMANDS.md. The continuation command was saved but **not executed**. CPU checkpoint loading passed; an end-to-end resume training round trip is not claimed. Original source evaluation model/metadata and CombatTraining scene were restored. The retained standalone evaluation build embeds the new candidate and has a static payload identity record. GameScene, authored controllers, Attack 2 speed, combo transitions, player root motion and shared combat assets remain byte-identical to the baseline manifest.", "",
    "## Problems and next investigation", "",
    "Blocking issues for promotion/integration: failed Passive/basic/Balanced gates, absent learned defense, incomplete final acceptance and unresolved human validation. No confirmed automated prerequisite defect remains after the local-process permission fixes. The first sandbox trainer collected zero decisions; its 11.203 s is charged. The sandbox build stalled in licensing; its one lock-conflicted retry failed; only the owned stalled batch process was stopped. The successful build completed with its PASS marker. Generalization's first prelaunch hash audit included a runtime timer JSON; no evaluation launched, and only that timer differed. The corrected audit verifies 179 static payload files. These were infrastructure/environment diagnoses, not gameplay changes.", "",
    "Non-blocking / Deferred TODOs: later curriculum stages, complete 200-per-family suite, end-to-end resume check, opponent wall-trigger telemetry and runtime release integration. No further work was silently scheduled.", "",
    "One concrete next investigation: " + selection["nextInvestigation"], "",
    "RUN MANUAL TESTS BEFORE NEXT PROMPT", ""]
report = "\n".join(lines)
(OUT / "RESULTS.md").write_text(report, encoding="utf-8")
shutil.copy2(OUT / "RESULTS.md", RUN / "BOUNDED_RESULTS.md")
shutil.copy2(OUT / "COMMANDS.md", RUN / "CONTINUATION_AND_COMMANDS.md")

old = (ROOT / "Logs/BeforeBoundedTraining20261117.md").read_text(encoding="utf-8-sig")
manual = old[old.index("## Manual test results"):old.index("## Individual acceptance criteria")]
progress = """# Implementation progress

## Current phase

Phase 6 bounded local training/evaluation experiment complete. Stage 1 did not pass promotion. Normal remains default. The best measured Stage-1 candidate is retained for evaluation only. No runtime release integration occurred.

## Current task

Task status: **PARTIAL SUCCESS**. The bounded experiment, evaluation records and handoff are complete. ML quality and human acceptance are incomplete. Training stopped after the fixed failed gate, without consuming the remaining budget or changing gameplay.

Previous progress is preserved verbatim in Logs/BeforeBoundedTraining20261117.md. Earlier implementation history remains there and in Logs/BeforePhase5Environment.md / Logs/BeforePhase5.md.

## Completed tasks / what was implemented

- Added Tools/Phase5/Combat/bounded_task.py: local bounded process wrapper, exact command/time/exit records, isolated metrics, pinned-config preparation, candidate metadata, static evaluation payload identity, richer action/terminal summaries and a saved explicit continuation command. No automatic search, promotion or integration.
- Added Tools/Phase5/Combat/report_bounded_task.py: reproducible reconciliation/report generation from completed records only.
- Added Logs/BoundedTraining20261117: configs, prerequisite and preservation audits, raw results, per-family summaries, commands and selection hashes. Updated Tools/Phase5/Combat/README.md to point to this experiment.
- Saved numbered and latest policy/optimizer checkpoints, ONNX, metadata, source snapshot, exact managed assemblies and evaluation records beside the candidate. Retained the pilot and its new evaluation records.
- No shared C# gameplay, observation/action contract, authored animation/controller/attack, GameScene or movement changes. Temporary source evaluation assets and the training scene were restored; all 137 baseline source/build files match again.

## Validation performed — current session versus historical prerequisites

Current: pip check; pinned config parsing; audit of 137 source/build hashes; bounded CPU PPO/export; checkpoint CPU loading including optimizer state; successful local evaluation build; five bounded held-out processes; reward/cap, seed partition, action/mask/foreign checks; preservation re-audit; Python syntax compilation. All completed runs are recorded below.

Historical evidence re-read, not rerun: 135-check observation/action regression, shared/posture regression, 1,000 one-arena resets then 1,000 per eight arenas, reward/reset/isolation fixtures, 18 accelerated motion trials and 12 matched-geometry movement comparisons. Source/build hashes are unchanged. Matched motion maximum .008914 m and RMS .008551 m remain within .02/.01 limits. The failed animation-free player route remains excluded. Existing root-motion/Animator route was used. Static/numeric evidence never closes manual presentation acceptance.

""" + report.replace("# Bounded training and evaluation — 17 November 2026", "## Measured experiment report", 1) + "\n" + manual + """
## Individual acceptance criteria

| Criterion | Status | Evidence / scope |
|---|---|---|
| Confirm prerequisite evidence against unchanged toolchain/build/source | PASS | prerequisites.json, preflight.json; historical Unity suites explicitly not rerun |
| Local-only resources; bounded training decisions/time | PASS | 100,060 trainer decisions; 260.890 s including failed startup; training*.run.json |
| Approved curriculum/opponents; no self-play; no unauthorized promotion | PASS | Stage 1 Passive only; pinned configuration.yaml; stage gate failed and training stopped |
| Held-out seed/opponent separation and earlier-family retention | PASS | actual training seeds above one million, evaluation below; five fixed evaluations; pilot retained |
| Bounded evaluations with per-family outcomes/damage/defense/actions/errors | PASS | 200 Passive plus 20 per 15 families for each candidate, plus new 20 per family varied; 300-s limits |
| All documented exploit families executed | PASS | AlwaysGuard/GuardTap/DodgeSpam/CornerCamp/Retreat/WallAttack/ComboSpam/DelayedAttack records |
| Exact opponent wall-trigger branch coverage | NOT VERIFIED — USER TEST REQUIRED | Existing telemetry lacks opponent wall-trigger counts; automated instrumentation is deferred, not a request for user testing |
| Reward reconciliation, observations/masks, accepted legality and arena integrity | PASS | All completed evaluations valid, zero illegal/mask/foreign counts; baseline/varied rejected requests 18/29 reported separately |
| Checkpoints, model/config/contract/tuning hashes and exact continuation command | PASS | selection.json, checkpoint-records.json, checkpoint CPU read, COMMANDS.md; continuation not executed |
| Actual end-to-end resume round trip | NOT VERIFIED — USER TEST REQUIRED | Automation deferred after failed promotion; not delegated to user |
| No privilege, timing, defense, damage, collision or movement concessions | PASS | 137 baseline files unchanged; same compiled gameplay/contract |
| Authored root-motion/combo/Attack 2 preservation in files | PASS | preservation-final.json; source evaluation assets restored |
| Stage-1 >=90% over 200 Passive episodes | FAIL | 89 wins, 111 timeouts; candidate-passive-200.summary.json |
| Basic opponents >=90% wins | FAIL | New baseline sample Passive 45%, PredictableLight 20%, PredictableHeavy 0%; complete Passive gate 44.5% |
| Balanced >=50% wins | FAIL | 0/20 in both new samples |
| Full-duel timeout <15% with complete planned sample | NOT VERIFIED — USER TEST REQUIRED | Samples meet timeout threshold; only 20/family, full automation deferred after failed gate |
| No exploit >80% opponent wins with complete planned sample | NOT VERIFIED — USER TEST REQUIRED | Samples meet threshold; only 20/family, full automation deferred after failed gate |
| Complete >=200 held-out episodes per family | NOT VERIFIED — USER TEST REQUIRED | Not run after failed Stage-1 gate; no complete final acceptance claim; automation deferred |
| Learned block/deflection and broad action use ready for final duels | FAIL | Zero new held-out blocks/deflections; only None/Heavy combat callbacks |
| Human-visible training/reset and existing control/animation acceptance | NOT VERIFIED — USER TEST REQUIRED | P5-ENV-VIS, P5-VIS and retained prior IDs below |
| Previous reported full-stun visual failure closed | NOT VERIFIED — USER TEST REQUIRED | ST-STATE; no new user result |
| Stage 2–4 training / runtime release integration | NOT APPLICABLE | Failed Stage-1 gate; integration outside this bounded task |

## Known blockers / problems

Blocking issues for promotion/integration: failed quality gates, missing learned defense, incomplete final suite and pending human validation. No confirmed automated infrastructure defect remains. Rejected requests have counts but no retained per-reason trace. Wall-trigger branch coverage is uninstrumented.

Unresolved validation that could reveal a blocker: P5-ENV-VIS, P5-VIS, ST-STATE and retained prior tests. No user result was invented. Unity SearchDatabase/indexing and native licensing/telemetry warnings are separate from standalone gameplay outcomes; a clean Editor Console is not claimed.

## Deferred TODOs / next concrete investigation

Trace failed Passive seed 100001 through public distance, movement masks and shared attack commitment: 481/500 Stop choices, 18 heavy choices, zero damage. Establish why attacks repeatedly begin outside contact range before proposing more training. Preserve gameplay and held-out separation. This investigation was recommended, not executed.

Later stages, full 200-per-family acceptance, resume round trip, opponent wall-trigger telemetry and runtime integration remain deferred. No further training/evaluation is running or scheduled by this task.

## Recommendation

RUN MANUAL TESTS BEFORE NEXT PROMPT
"""
(ROOT / "IMPLEMENTATION_PROGRESS.md").write_text(progress, encoding="utf-8")

with zipfile.ZipFile(RUN / "source-snapshot.zip", "w", zipfile.ZIP_DEFLATED) as archive:
    for name, expected in metadata["sourceHashes"].items():
        p = ROOT / name
        assert sha(p) == expected
        archive.write(p, name)
    for p in [OUT / "stage1-100k.yaml", OUT / "stage1-continuation.yaml",
              ROOT / "Tools/Phase5/Combat/bounded_task.py", pathlib.Path(__file__)]:
        archive.write(p, p.relative_to(ROOT))
print(json.dumps(dict(selection=selection, totals=totals), indent=2))
