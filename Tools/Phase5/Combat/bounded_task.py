"""One local, bounded experiment. Gameplay and trainer code remain unchanged."""
import argparse
import collections
import hashlib
import json
import math
import pathlib
import subprocess
import time

import yaml

ROOT = pathlib.Path(__file__).resolve().parents[3]
HERE = pathlib.Path(__file__).resolve().parent
OUT = ROOT / "Logs/BoundedTraining20261117"
RUN_ID = "combat-stage1-bounded-20261117-23"
RUN = HERE / "results" / RUN_ID
PYTHON = ROOT / "Tools/ml-venv/Scripts/python.exe"


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def execute(name, command, timeout):
    OUT.mkdir(parents=True, exist_ok=True)
    record = OUT / (name + ".run.json")
    if record.exists():
        raise SystemExit("Record exists; use a new name, never overwrite evidence")
    start = time.monotonic()
    info = dict(command=list(map(str, command)), timeoutSeconds=timeout,
                startedUtc=time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()))
    with (OUT / (name + ".console.log")).open("w") as log:
        proc = subprocess.Popen(info["command"], cwd=ROOT, stdout=log,
                                stderr=subprocess.STDOUT,
                                creationflags=subprocess.CREATE_NO_WINDOW)
        info["pid"] = proc.pid
        try:
            info["exitCode"] = proc.wait(timeout=timeout)
            info["timedOut"] = False
        except subprocess.TimeoutExpired:
            subprocess.run(["taskkill", "/PID", str(proc.pid), "/T", "/F"],
                           capture_output=True, creationflags=subprocess.CREATE_NO_WINDOW)
            info.update(exitCode=None, timedOut=True)
        finally:
            info["wallSeconds"] = time.monotonic() - start
            record.write_text(json.dumps(info, indent=2))
    print(json.dumps(info, indent=2), flush=True)
    if info["timedOut"] or info["exitCode"] != 0:
        raise SystemExit("Bounded process failed; inspect saved evidence")
    return info


def prepare():
    OUT.mkdir(parents=True, exist_ok=True)
    old = json.loads((ROOT / "Logs/Phase5Environment/final-source-build-hashes.json").read_text())
    audit = [dict(path=r["path"], expected=r["sha256"], actual=sha(ROOT/r["path"])) for r in old]
    assert all(r["expected"] == r["actual"] for r in audit), "Prerequisite source/build drift"
    import torch
    import onnx
    from mlagents.plugins import trainer_type
    from mlagents.trainers.settings import RunOptions
    trainer_type.register_trainer_plugins()
    for path in HERE.glob("*.yaml"):
        RunOptions.from_dict(yaml.safe_load(path.read_text()))
    config = yaml.safe_load((HERE / "stage-1.yaml").read_text())
    config["behaviors"]["CombatV1"].update(max_steps=100000, summary_freq=10000,
        checkpoint_interval=10000, keep_checkpoints=12)
    config["env_settings"]["env_args"] += ["--metrics", str(OUT / "training.jsonl")]
    (OUT / "stage1-100k.yaml").write_text(yaml.safe_dump(config, sort_keys=False))
    continuation = yaml.safe_load(yaml.safe_dump(config))
    # Leave ample room for outstanding trajectories below the absolute 2M cap.
    continuation["behaviors"]["CombatV1"]["max_steps"] = 1900000
    (OUT / "stage1-continuation.yaml").write_text(yaml.safe_dump(continuation, sort_keys=False))
    info = dict(sourceBuildAudit=audit, python=__import__('platform').python_version(),
                torch=torch.__version__, onnx=onnx.__version__,
                configSha256=sha(OUT / "stage1-100k.yaml"),
                taskDecisionLimit=2000000, taskTrainingWallLimitSeconds=1800,
                plannedTrainingStop=100000, drainReserveDecisions=10000,
                plannedTrainingTimeoutSeconds=330,
                evaluationOnly=True, selfPlay=False,
                pendingManual=["P5-ENV-VIS", "P5-VIS", "ST-STATE", "retained earlier IDs"],
                decision="Stage 1 only until its fixed 200-episode gate passes. Stop on failed gate.")
    (OUT / "preflight.json").write_text(json.dumps(info, indent=2))
    print("PASS: 137 source/build hashes unchanged; pinned trainer parses configs; bounded stage-1 config saved")


def train(attempt=1):
    run_id = RUN_ID if attempt == 1 else RUN_ID + "b"
    run = HERE / "results" / run_id
    name = "training" if attempt == 1 else "training-retry"
    assert (OUT / "preflight.json").exists()
    assert not run.exists(), "Never overwrite a run"
    prior = sum(json.loads(p.read_text())["wallSeconds"] for p in OUT.glob("training*.run.json"))
    assert prior + 330 < 1800
    command = [ROOT / "Tools/ml-venv/Scripts/mlagents-learn.exe", OUT / "stage1-100k.yaml",
               "--env=" + str(HERE / "Build/Combat.exe"), "--run-id=" + run_id,
               "--results-dir=" + str(HERE / "results"), "--seed=23", "--no-graphics", "--torch-device=cpu"]
    execute(name, command, 330)
    status = json.loads((run / "run_logs/training_status.json").read_text())
    steps = status["CombatV1"]["final_checkpoint"]["steps"]
    assert steps < 110000 < 2000000
    record = json.loads((OUT / (name + ".run.json")).read_text())
    record.update(actualTrainerDecisions=steps, allTaskTrainingWallSeconds=prior + record["wallSeconds"])
    (OUT / (name + ".run.json")).write_text(json.dumps(record, indent=2))
    execute("metadata" + str(attempt), [PYTHON, HERE / "manage.py", "metadata", run, "--stage", "1"], 60)
    (run / "checkpoint-records.json").write_text(json.dumps(status, indent=2))


def resume():
    """Explicit future invocation only; never called automatically by evaluation."""
    run = HERE / "results" / (RUN_ID + "b")
    prior = sum(json.loads(p.read_text())["wallSeconds"] for p in OUT.glob("training*.run.json"))
    remaining = int(1800 - prior - 5)
    assert remaining > 0
    config = yaml.safe_load((OUT / "stage1-continuation.yaml").read_text())
    assert config["behaviors"]["CombatV1"]["max_steps"] == 1900000
    before = json.loads((run / "run_logs/training_status.json").read_text())["CombatV1"]["final_checkpoint"]["steps"]
    assert before < 1900000
    command = [ROOT / "Tools/ml-venv/Scripts/mlagents-learn.exe", OUT / "stage1-continuation.yaml",
        "--env=" + str(HERE / "Build/Combat.exe"), "--run-id=" + RUN_ID + "b",
        "--results-dir=" + str(HERE / "results"), "--seed=23", "--no-graphics",
        "--torch-device=cpu", "--resume"]
    execute("training-continuation", command, remaining)


def bind_evaluation_build():
    build = HERE / "Evaluation"
    model = ROOT / "Assets/CombatTrainingPolicy/CombatV1.onnx"
    metadata = ROOT / "Assets/CombatTrainingPolicy/metadata.json"
    source = json.loads(metadata.read_text())
    assert source["exportSha256"] == sha(model)
    files = {str(p.relative_to(build)): sha(p) for p in (build / "Combat_Data").rglob("*")
             if p.is_file() and "ML-Agents" not in p.parts
             and (p.suffix in [".assets", ".resS", ".resource", ".json", ".dll"]
                  or p.name.startswith("globalgamemanagers"))}
    (build / "evaluation-identity.json").write_text(json.dumps(dict(
        exportSha256=sha(model), metadataSha256=sha(metadata), tuningHash=source["tuningHash"],
        sourceRun=source["run"], contractVersion=source["contractVersion"],
        evaluationOnly=True, buildFiles=files), indent=2))


def evaluate(name, family, episodes, stage, timeout):
    metrics = OUT / (name + ".jsonl")
    assert not metrics.exists(), "Evaluation must use a fresh metrics path"
    build_identity = HERE / "Evaluation/evaluation-identity.json"
    if build_identity.exists():
        identity = json.loads(build_identity.read_text())
        assert all(sha(HERE / "Evaluation" / p) == h for p, h in identity["buildFiles"].items())
    else:
        model = ROOT / "Assets/CombatTrainingPolicy/CombatV1.onnx"
        metadata = ROOT / "Assets/CombatTrainingPolicy/metadata.json"
        source = json.loads(metadata.read_text())
        assert source["exportSha256"] == sha(model)
        identity = dict(exportSha256=sha(model), metadataSha256=sha(metadata),
            tuningHash=source["tuningHash"], contractVersion=source["contractVersion"],
            sourceRun=source["run"], provenance="Pre-existing verified pilot evaluation build")
    identity["executableSha256"] = sha(HERE / "Evaluation/Combat.exe")
    (OUT / (name + ".identity.json")).write_text(json.dumps(identity, indent=2))
    command = [HERE / "Evaluation/Combat.exe", "-batchmode", "-nographics", "--phase5-eval",
               "--eval-episodes", str(episodes), "--arenas", "1", "--stage", str(stage),
               "--metrics", metrics, "-logFile", OUT / (name + ".player.log")]
    if family != "all":
        command += ["--eval-family", family]
    execute(name, command, timeout)
    rows = [json.loads(line) for line in metrics.read_text().splitlines() if line.strip()]
    assert len(rows) == episodes * (15 if family == "all" else 1)
    assert "communicator=False" in (OUT / (name + ".player.log")).read_text()
    execute(name + "-summary", [PYTHON, HERE / "manage.py", "summarize", metrics,
                              "--stage", str(stage)], 30)
    groups = collections.defaultdict(list)
    for row in rows:
        groups[row["family"]].append(row)
    result = {}
    for key, records in groups.items():
        choices = [sum(r["combat"][i] for r in records) for i in range(5)]
        total = sum(choices)
        result[key] = dict(episodes=len(records),
            terminalReasons=dict(collections.Counter(r["terminalReason"] for r in records)),
            maskViolations=sum(r["maskViolations"] for r in records),
            rejected=sum(r["rejected"] for r in records), illegal=sum(r["illegal"] for r in records),
            opponentBlocks=sum(r["opponentBlocks"] for r in records),
            opponentDeflections=sum(r["opponentDeflections"] for r in records),
            wallAttackRequests=sum(r["wallAttackRequests"] for r in records),
            combatChoices=choices, combatChoiceOrder=["None", "Guard", "Light", "Heavy", "Dodge"],
            combatEntropyBits=-sum((n/total)*math.log2(n/total) for n in choices if n),
            movementChoices=[sum(r["movement"][i] for r in records) for i in range(9)])
    (OUT / (name + ".details.json")).write_text(json.dumps(result, indent=2))


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("action", choices=["prepare", "train", "resume", "eval", "build-eval"])
    parser.add_argument("--name")
    parser.add_argument("--family", default="all")
    parser.add_argument("--episodes", type=int, default=20)
    parser.add_argument("--stage", type=int, default=3)
    parser.add_argument("--timeout", type=int, default=300)
    parser.add_argument("--attempt", type=int, choices=[1, 2], default=1)
    args = parser.parse_args()
    if args.action == "prepare":
        prepare()
    elif args.action == "train":
        train(args.attempt)
    elif args.action == "resume":
        resume()
    elif args.action == "eval":
        assert args.name and 0 < args.episodes <= 200 and 0 < args.timeout <= 900
        evaluate(args.name, args.family, args.episodes, args.stage, args.timeout)
    else:
        assert args.name
        execute(args.name, ["C:/Program Files/Unity/Hub/Editor/6000.6.0f1/Editor/Unity.exe",
            "-batchmode", "-nographics", "-quit", "-projectPath", ROOT / "Tools/Phase5/Lab",
            "-executeMethod", "Phase5EnvironmentBuild.BuildEvaluation", "-logFile", OUT / (args.name + ".unity.log")], 300)
        assert "PHASE5_COMBAT_BUILD_PASS" in (OUT / (args.name + ".unity.log")).read_text()
        bind_evaluation_build()
