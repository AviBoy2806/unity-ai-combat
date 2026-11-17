"""Separate budget ledger for the newly authorized demonstration experiment."""
import argparse
import collections
import hashlib
import json
import os
import pathlib
import shutil
import subprocess
import time

import yaml

ROOT = pathlib.Path(__file__).resolve().parents[2]
HERE = pathlib.Path(__file__).resolve().parent
OUT = ROOT / "Logs/DemoPolicy20261117"
RESULTS = ROOT / "Tools/Phase5/Combat/results"
ORIGINAL = RESULTS / "combat-stage1-bounded-20261117-23b"
RUN = RESULTS / "demo-stage1-20261117-71"
PYTHON = ROOT / "Tools/ml-venv/Scripts/python.exe"


def read(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def step(run):
    status = read(run / "run_logs/training_status.json")
    return status["CombatV1"]["final_checkpoint"]["steps"]


def prepare():
    import torch
    state = torch.load(ORIGINAL / "CombatV1/checkpoint.pt", map_location="cpu")
    assert set(["Policy", "global_step", "Optimizer:value_optimizer", "Optimizer:critic"]) <= set(state)
    assert not RUN.exists()
    shutil.copytree(ORIGINAL, RUN)
    # The trainer prunes old checkpoint paths. Redirect the cloned records so it
    # cannot prune any original-candidate artifact when resuming this isolated copy.
    for file in [RUN / "run_logs/training_status.json", RUN / "checkpoint-records.json"]:
        text = file.read_text()
        file.write_text(text.replace(str(ORIGINAL).replace("\\", "\\\\"), str(RUN).replace("\\", "\\\\")))
    assert step(RUN) == 100060
    record = dict(original=str(ORIGINAL), isolatedResume=str(RUN), inheritedStep=100060,
        checkpointSha256=sha(ORIGINAL / "CombatV1/checkpoint.pt"), optimizerReadable=True,
        configuration="Same PPO/network/reward settings; resumed under authorized fixed 2,090,000 absolute LR horizon, rather than the exhausted 100,000-step horizon. Planned blocks stop the normal controller loop without resetting this schedule.",
        rationale="Observation/action shape and public cue semantics are unchanged. Minimal planar-facing adapter fix restores the intended Normal-equivalent execution. Policy/optimizer are retained; range behavior still needs learning.")
    (OUT / "continuation-preparation.json").write_text(json.dumps(record, indent=2))
    print(json.dumps(record, indent=2))


def train(block, decisions, build="Training"):
    budget = read(OUT / "budget.json")
    assert (OUT / "prerequisites.json").exists()
    assert not (OUT / f"train-block{block}.run.json").exists()
    before = step(RUN)
    available = 2000000 - budget["additionalDecisions"]
    target = min(before + decisions, before + available - 10060, 2090000)
    assert target > before
    hard = min(600, int(1800 - budget["trainingProcessWallSeconds"]))
    assert hard >= 40
    config = yaml.safe_load((ROOT / "Tools/Phase5/Combat/stage-1.yaml").read_text())
    config["behaviors"]["CombatV1"].update(max_steps=2090000, checkpoint_interval=50000, keep_checkpoints=30)
    config["env_settings"]["env_args"] = ["--phase5-train", "--arenas", "8", "--stage", "1", "--train-seed", str(71 + (block-1)*10007), "--metrics", str(OUT / f"train-block{block}.jsonl")]
    config_path = OUT / f"train-block{block}.yaml"
    config_path.write_text(yaml.safe_dump(config, sort_keys=False))
    command = [str(PYTHON), str(HERE / "train_entry.py"), str(config_path),
        "--env=" + str(HERE / build / "Combat.exe"), "--run-id=" + RUN.name,
        "--results-dir=" + str(RESULTS), "--seed=71", "--no-graphics", "--torch-device=cpu", "--resume"]
    env = os.environ.copy()
    env.update(DEMO_STOP_STEP=str(target), DEMO_SOFT_SECONDS=str(hard-25), DEMO_PROGRESS_FILE=str(OUT / f"train-block{block}.progress.json"))
    info = dict(command=command, inheritedStep=before, targetStep=target, hardTimeoutSeconds=hard,
        softTimeoutSeconds=hard-25, configuredAbsoluteMax=2090000,
        configSha256=sha(config_path), startedUtc=time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()))
    started = time.monotonic()
    with (OUT / f"train-block{block}.console.log").open("w") as stream:
        proc = subprocess.Popen(command, cwd=ROOT, env=env, stdout=stream, stderr=subprocess.STDOUT, creationflags=subprocess.CREATE_NO_WINDOW)
        info["pid"] = proc.pid
        (OUT / f"train-block{block}.run.json").write_text(json.dumps(info, indent=2))
        try:
            info["exitCode"] = proc.wait(timeout=hard)
            info["timedOut"] = False
        except subprocess.TimeoutExpired:
            subprocess.run(["taskkill", "/PID", str(proc.pid), "/T", "/F"], capture_output=True, creationflags=subprocess.CREATE_NO_WINDOW)
            info.update(exitCode=None, timedOut=True)
    info["wallSeconds"] = time.monotonic()-started
    after = step(RUN)
    info.update(finalTrainerStep=after, additionalDecisions=after-before)
    if info["timedOut"]:
        info["lastObservedStep"] = read(OUT / f"train-block{block}.progress.json")["trainerStep"]
        info["exactDecisionsUnverified"] = True
    budget["additionalDecisions"] += max(0, after-before)
    budget["trainingProcessWallSeconds"] += info["wallSeconds"]
    budget["trainingBlocks"].append(info)
    (OUT / f"train-block{block}.run.json").write_text(json.dumps(info, indent=2))
    (OUT / "budget.json").write_text(json.dumps(budget, indent=2))
    print(json.dumps(info, indent=2), flush=True)
    assert not info["timedOut"] and info["exitCode"] == 0
    assert 0 < after-before < decisions + 10060 and budget["additionalDecisions"] < 2000000
    assert budget["trainingProcessWallSeconds"] <= 1800
    subprocess.run([str(PYTHON), str(ROOT / "Tools/Phase5/Combat/manage.py"), "metadata", str(RUN), "--stage", "1"], cwd=ROOT, check=True)
    snapshot = RUN / f"block{block}-{after}"
    snapshot.mkdir()
    for name in ["CombatV1.onnx", "metadata.json", "configuration.yaml", "run_logs/training_status.json", "CombatV1/checkpoint.pt"]:
        shutil.copy2(RUN / name, snapshot / pathlib.Path(name).name)
    print("Resumable block snapshot:", snapshot, flush=True)


def stage_model(source, sampled=False):
    dest = ROOT / "Assets/DemoPolicyEvaluation"
    dest.mkdir(exist_ok=True)
    source = pathlib.Path(source).resolve()
    assert sha(source / "CombatV1.onnx") == read(source / "metadata.json")["exportSha256"]
    shutil.copy2(source / "CombatV1.onnx", dest / "CombatV1.onnx")
    shutil.copy2(source / "metadata.json", dest / "metadata.json")
    runtime = dict(read(source / "metadata.json"),evaluationOnly=True,
        editorTuningHash=read(ROOT / "Assets/RuntimeEvaluationCandidate/runtime-metadata.json")["editorTuningHash"],
        movementVersion="shared-enemy-motor-v1",combatMoveSpeed=2.5,decisionTicks=3,latencyTicks=1,inferenceMode="sampled" if sampled else "deterministic")
    (dest / "runtime-metadata.json").write_text(json.dumps(runtime,indent=2))
    (OUT / "staged-evaluation.json").write_text(json.dumps(dict(source=str(source),modelSha256=sha(source / "CombatV1.onnx"),metadataSha256=sha(source / "metadata.json")),indent=2))
    print("Staged evaluation model", source, flush=True)


def prepare_install(source, evaluation):
    source = pathlib.Path(source).resolve()
    metadata = read(source / "metadata.json")
    assert sha(source / "CombatV1.onnx") == metadata["exportSha256"]
    result = read(OUT / (evaluation + ".summary.json"))
    assert result["model"]["modelSha256"] == metadata["exportSha256"]
    # Preserve both the old immutable run and the exact old gameplay copies.
    old_dir = OUT / "previous-runtime-candidate"
    if old_dir.exists():
        old_dir = OUT / ("previous-runtime-candidate-install"+str(len(list(OUT.glob("previous-runtime-candidate*")))+1))
    assert not old_dir.exists()
    shutil.copytree(ROOT / "Assets/RuntimeEvaluationCandidate", old_dir)
    old = read(old_dir / "runtime-metadata.json")
    for path, expected in old["sourceHashes"].items():
        if path.startswith("Assets/CombatData/"):
            assert sha(ROOT / path) == expected
    movement_files = ["Assets/Scripts/Enemy/Combat/EnemyMotor.cs", "Assets/Scripts/Enemy/Combat/EnemyBrainHost.Agent.cs", "Assets/Scripts/Classes/RootMotionHandler.cs", "Assets/Scripts/Classes/StateRootMotion.cs", "Assets/Scripts/Combat/PlayerCombatAdapter.cs", "ProjectSettings/TimeManager.asset"]
    movement = {name:sha(ROOT / name) for name in movement_files}
    movement_hash = hashlib.sha256(json.dumps(movement,sort_keys=True,separators=(",",":")).encode()).hexdigest()
    runtime = dict(metadata, evaluationOnly=True, editorTuningHash=old["editorTuningHash"],
        movementVersion="shared-enemy-motor-v1",combatMoveSpeed=2.5,decisionTicks=3,latencyTicks=1,
        movementSourceHashes=movement,movementHash=movement_hash,
        trainingCompilationManifestSha256=sha(OUT / "training-build-source-manifest.json"),
        originalTrainingMetadataSha256=sha(source / "metadata.json"),
        stage1PromotionPassed=result["stage1GatePassed"],completeFinalMLAcceptancePassed=False,
        selectedEvaluation=str(OUT / (evaluation + ".summary.json")),additionalTrainingBudget=read(OUT / "budget.json"))
    runtime["inferenceMode"] = result.get("inferenceMode","deterministic")
    runtime["runtimeAdapterSourceSha256"] = sha(ROOT / "Assets/MLAdapter/EnemyLearningAgent.cs")
    runtime["contractSourceSha256"] = sha(ROOT / "Assets/AgentContract/AgentContract.cs")
    runtime["inferenceSeed"] = 0
    dest = ROOT / "Assets/RuntimeEvaluationCandidate"
    shutil.copy2(source / "CombatV1.onnx", dest / "CombatV1-EvaluationOnly.onnx")
    (dest / "runtime-metadata.json").write_text(json.dumps(runtime,indent=2))
    selected = dict(source=str(source),modelSha256=metadata["exportSha256"],
        runtimeMetadataSha256=sha(dest / "runtime-metadata.json"),
        trainingMetadataSha256=sha(source / "metadata.json"),
        configSha256=sha(source / "configuration.yaml"),movementHash=movement_hash,
        evaluationOnly=True,normalSavedDefault=True,evaluation=evaluation,sampledInference=runtime["inferenceMode"]=="sampled",
        contractSha256=sha(ROOT / "Assets/AgentContract/AgentContract.cs"),tuningHash=metadata["tuningHash"])
    (OUT / "selected-source.json").write_text(json.dumps(selected,indent=2))
    print(json.dumps(selected,indent=2))


def evaluate(name, family, episodes, stage, offset, timeout, sampled=False):
    metrics = OUT / (name + ".jsonl")
    assert not metrics.exists() and not (OUT / (name + ".run.json")).exists()
    command = [str(HERE / "Evaluation/Combat.exe"), "-batchmode", "-nographics", "--phase5-eval", "--eval-episodes", str(episodes), "--arenas", "1", "--stage", str(stage), "--eval-seed-offset", str(offset), "--metrics", str(metrics), "-logFile", str(OUT / (name + ".player.log"))]
    if family != "all":
        command += ["--eval-family", family]
    if sampled:
        command += ["--sample-policy"]
    info = dict(command=command, hardTimeoutSeconds=timeout, model=read(OUT / "staged-evaluation.json"), seedOffset=offset,inferenceMode="sampled" if sampled else "deterministic")
    start = time.monotonic()
    with (OUT / (name + ".console.log")).open("w") as stream:
        proc = subprocess.Popen(command, cwd=ROOT, stdout=stream, stderr=subprocess.STDOUT, creationflags=subprocess.CREATE_NO_WINDOW)
        info["pid"] = proc.pid
        try:
            info["exitCode"] = proc.wait(timeout=timeout)
            info["timedOut"] = False
        except subprocess.TimeoutExpired:
            subprocess.run(["taskkill", "/PID", str(proc.pid), "/T", "/F"], capture_output=True, creationflags=subprocess.CREATE_NO_WINDOW)
            info.update(exitCode=None, timedOut=True)
    info["wallSeconds"] = time.monotonic()-start
    (OUT / (name + ".run.json")).write_text(json.dumps(info, indent=2))
    assert not info["timedOut"] and info["exitCode"] == 0
    rows = [json.loads(x) for x in metrics.read_text().splitlines() if x.strip()]
    assert len(rows) == episodes * (15 if family == "all" else 1)
    assert "communicator=False" in (OUT / (name + ".player.log")).read_text()
    assert len({(r["family"],r["seed"]) for r in rows}) == len(rows)
    assert all(r["evaluation"] and r["seed"] < 1000000 and r["illegal"] == r["maskViolations"] == r["foreignEvents"] == 0 for r in rows)
    assert all(abs(r["reward"]-sum(r[k] for k in ["terminalReward","damageReward","defenseReward","shapingReward","livingReward"]))<.0005 for r in rows)
    groups = collections.defaultdict(list)
    for r in rows:
        groups[r["family"]].append(r)
    summary = dict(episodes=len(rows), model=info["model"], seedOffset=offset, scenarioStage=stage,inferenceMode=info["inferenceMode"], families={}, rewardAccountingValid=True, zeroIllegalMaskForeign=True)
    for f, rr in groups.items():
        count = collections.Counter(r["outcome"] for r in rr)
        summary["families"][f] = dict(episodes=len(rr),outcomes={k:count[k] for k in ["win","loss","draw","timeout"]},
            damageDealt=sum(r["dealt"] for r in rr)/len(rr),damageReceived=sum(r["received"] for r in rr)/len(rr),
            blocks=sum(r["blocks"] for r in rr),deflections=sum(r["deflections"] for r in rr),rejected=sum(r["rejected"] for r in rr),
            requestResults=[sum(r["requestResults"][i] for r in rr) for i in range(6)],requestResultOrder=["Accepted","Busy","Cooldown","Airborne","Dead","Invalid"],
            acceptedLight=sum(r["acceptedLight"] for r in rr),acceptedHeavy=sum(r["acceptedHeavy"] for r in rr),acceptedDodge=sum(r["acceptedDodge"] for r in rr),
            attackContacts=sum(r["attackContacts"] for r in rr),requestsBeyondAuthoredReach=sum(r["requestsBeyondAuthoredReach"] for r in rr),
            approachFromBeyond24ToWithin2=sum(r["startDistance"]>2.4 and r["minDistance"]<=2 for r in rr),startsBeyond24=sum(r["startDistance"]>2.4 for r in rr),
            meanFreeFacingError=sum(r["facingErrorSum"] for r in rr)/max(1,sum(r["facingSamples"] for r in rr)),
            combatChoices=[sum(r["combat"][i] for r in rr) for i in range(5)],terminalReasons=dict(collections.Counter(r["terminalReason"] for r in rr)))
    summary["stage1GatePassed"] = offset == 100000 and "Passive" in groups and len(groups["Passive"])>=200 and summary["families"]["Passive"]["outcomes"]["win"]/len(groups["Passive"])>=.9
    (OUT / (name + ".summary.json")).write_text(json.dumps(summary,indent=2))
    print(json.dumps(summary,indent=2),flush=True)


if __name__ == "__main__":
    p = argparse.ArgumentParser()
    p.add_argument("action",choices=["prepare","train","stage-model","evaluate","prepare-install"])
    p.add_argument("--block",type=int,default=1)
    p.add_argument("--decisions",type=int,default=200000)
    p.add_argument("--build",default="Training")
    p.add_argument("--source")
    p.add_argument("--evaluation")
    p.add_argument("--sample",action="store_true")
    p.add_argument("--name")
    p.add_argument("--family",default="Passive")
    p.add_argument("--episodes",type=int,default=50)
    p.add_argument("--stage",type=int,default=1)
    p.add_argument("--offset",type=int,default=400000)
    p.add_argument("--timeout",type=int,default=300)
    a=p.parse_args()
    if a.action=="prepare":prepare()
    elif a.action=="train":train(a.block,a.decisions,a.build)
    elif a.action=="stage-model":stage_model(a.source,a.sample)
    elif a.action=="prepare-install":prepare_install(a.source,a.evaluation)
    else:
        assert a.name and 0<a.episodes<=200 and 0<a.timeout<=900 and a.offset in [100000,400000,600000]
        evaluate(a.name,a.family,a.episodes,a.stage,a.offset,a.timeout,a.sample)
