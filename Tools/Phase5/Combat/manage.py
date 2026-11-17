"""Local experiment records and held-out gates. No automatic promotion or search."""
import argparse
import collections
import hashlib
import json
import pathlib
import platform
import subprocess
import sys
import time

ROOT = pathlib.Path(__file__).resolve().parents[3]
HERE = pathlib.Path(__file__).resolve().parent

def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def metadata(run, stage=1, pilot_only=False):
    import torch
    import mlagents.trainers
    import onnx
    import yaml
    path = pathlib.Path(run).resolve()
    model = path / "CombatV1.onnx"
    if not model.is_file():
        raise SystemExit("Export missing; no checkpoint record created")
    onnx.checker.check_model(onnx.load(str(model)))
    # Runtime hash comes from the actual instantiated host, not Python reserialization.
    config=yaml.safe_load((path/"configuration.yaml").read_text())
    env_args=config["env_settings"].get("env_args") or []
    def arg(name, default): return env_args[env_args.index(name)+1] if name in env_args else default
    stage=int(arg("--stage",stage))
    seed=int(arg("--train-seed",23))
    metrics=pathlib.Path(arg("--metrics",str(ROOT/"Logs/Phase5Environment/episodes.jsonl")))
    contract_path=metrics.with_suffix(".contract.json")
    if contract_path.exists(): contract=json.loads(contract_path.read_text())
    elif (path/"metadata.json").exists():
        previous=json.loads((path/"metadata.json").read_text())
        if previous["exportSha256"]!=digest(model): raise SystemExit("Model changed; matching runtime contract required")
        contract={k:previous[k] for k in ("contractVersion","observationSize","stacks","tuningHash","fixedDeltaTime","captureDeltaTime","timeScale")}
    else: raise SystemExit("Matching runtime metrics.contract.json required; do not use an Editor-generated hash")
    files = {}
    for pattern in ("Assets/**/*.cs", "Assets/**/*.controller", "Assets/CombatData/*.asset", "Packages/*.json", "ProjectSettings/TimeManager.asset", "Tools/Phase5/Combat/*.yaml"):
        for p in ROOT.glob(pattern):
            files[p.relative_to(ROOT).as_posix()] = digest(p)
    record = dict(contract, python=platform.python_version(), torch=torch.__version__,
                  trainer=mlagents.trainers.__version__, onnx=onnx.__version__,
                  sourceRevision=None, sourceIdentity="source SHA256 manifest (workspace has no Git revision)",
                  sourceHashes=files, exportSha256=digest(model),
                  checkpoints={str(p.relative_to(path)): digest(p) for p in path.rglob("*.pt")},
                  run=str(path), createdUtc=time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
                  trainerSeed=config["env_settings"]["seed"],trainingSeed=seed, trainingSeedFormula="masterSeed + arenaId*1000003 + episode*7919",
                  heldOutSeedFormula="100000 + familyIndex*10000 + episode",
                  stage=stage, opponentMixture=({"Passive":1.0} if stage==1 else {"PredictableLight":.5,"PredictableHeavy":.5} if stage==2 else {k:.25 for k in ("Aggressive","GuardCounter","DodgeRetreat","Balanced")}), pilotOnly=pilot_only)
    (path / "metadata.json").write_text(json.dumps(record, indent=2))
    print(json.dumps({"export":str(model), "sha256":record["exportSha256"], "checkpoints":len(record["checkpoints"])}))

def summarize(path, stage):
    rows = [json.loads(line) for line in pathlib.Path(path).read_text().splitlines() if line.strip()]
    if not rows:
        raise SystemExit("No episode records")
    groups = collections.defaultdict(list)
    for row in rows:
        groups[row["family"]].append(row)
    report = {"episodes":len(rows), "stage":stage, "families":{}}
    held_out=all(r.get("evaluation",100000<=r["seed"]<1000000) for r in rows)
    unique=len({(r["family"],r["seed"]) for r in rows})==len(rows)
    report["fixedHeldOutSeeds"]=held_out and unique
    report["rewardAccountingValid"]=all(abs(r["reward"]-sum(r[k] for k in ("terminalReward","damageReward","defenseReward","shapingReward","livingReward")))<.0005 and 0<=r["defenseReward"]<=.050001 for r in rows)
    for family, records in sorted(groups.items()):
        n = len(records)
        counts = collections.Counter(r["outcome"] for r in records)
        report["families"][family] = dict(episodes=n, outcomes={k:counts[k] for k in ("win","loss","draw","timeout")},
            winRate=counts["win"]/n, lossRate=counts["loss"]/n, timeoutRate=counts["timeout"]/n,
            meanDamageDealt=sum(r["dealt"] for r in records)/n,
            meanDamageReceived=sum(r["received"] for r in records)/n,
            meanReward=sum(r["reward"] for r in records)/n,
            blocks=sum(r["blocks"] for r in records),deflections=sum(r["deflections"] for r in records),
            evades=sum(r["evades"] for r in records),rejected=sum(r["rejected"] for r in records),
            illegal=sum(r["illegal"] for r in records), foreignEvents=sum(r["foreignEvents"] for r in records),
            duplicateDeliveries=sum(r["duplicates"] for r in records),
            guardSeconds=sum(r["guardSeconds"] for r in records),cornerSeconds=sum(r["cornerSeconds"] for r in records),retreatSeconds=sum(r["retreatSeconds"] for r in records),
            combatChoices=[sum(r["combat"][i] for r in records) for i in range(5)])
    g=report["families"]
    if stage==1:
        report["promotionEligible"] = "Passive" in g and g["Passive"]["episodes"]>=200 and g["Passive"]["winRate"]>=.9
        report["gate"]="200 fixed held-out Passive episodes, >=90% wins"
    elif stage==2:
        report["promotionEligible"] = all(f in g and g[f]["episodes"]>=200 and g[f]["timeoutRate"]>=.7 and g[f]["blocks"]>0 and g[f]["deflections"]>0 for f in ("PredictableLight","PredictableHeavy"))
        report["gate"]="200 per predictable family; >=70% survive to timeout; blocks and deflections each observed"
    else:
        basic=("Passive","PredictableLight","PredictableHeavy")
        duel=("Aggressive","GuardCounter","DodgeRetreat","Balanced")
        exploits=("AlwaysGuard","GuardTap","DodgeSpam","CornerCamp","Retreat","WallAttack","ComboSpam","DelayedAttack")
        enough=all(f in g and g[f]["episodes"]>=200 for f in basic+duel+exploits)
        report["statisticalGates"]={"200PerFamily":enough,
            "basic90Percent":all(f in g and g[f]["winRate"]>=.9 for f in basic),
            "balanced50Percent":"Balanced" in g and g["Balanced"]["winRate"]>=.5,
            "duelTimeoutBelow15Percent":all(f in g and g[f]["timeoutRate"]<.15 for f in duel),
            "exploitWinRateAtMost80Percent":all(f in g and g[f]["lossRate"]<=.8 for f in exploits)}
        report["promotionEligible"]=enough and all(report["statisticalGates"].values())
        report["humanReviewRequired"]=True
        report["gate"]="Fixed held-out statistical gates plus separate human readability review; never auto-promote"
    report["promotionEligible"] &= report["fixedHeldOutSeeds"] and report["rewardAccountingValid"]
    report["valid"]=report["rewardAccountingValid"] and all(v["foreignEvents"]==0 and v["illegal"]==0 for v in g.values())
    output=pathlib.Path(path).with_suffix(".summary.json")
    output.write_text(json.dumps(report,indent=2))
    print(json.dumps(report,indent=2))

def pilot(run_id, resume):
    # Trainer drains outstanding trajectories after max_steps (observed final
    # checkpoint 4976 for max_steps 4096), still well below 30,000 decisions.
    # Same planned PPO settings;
    # only stop/summary/checkpoint frequencies differ in the bounded pipeline test.
    config=HERE/"ppo-smoke.yaml"
    command=[str(ROOT/"Tools/ml-venv/Scripts/mlagents-learn.exe"),str(config),
        "--env="+str(HERE/"Build/Combat.exe"),"--run-id="+run_id,
        "--results-dir="+str(HERE/"results"),"--seed=23","--no-graphics","--torch-device=cpu","--time-scale=4"]
    if resume: command.append("--resume")
    command += ["--env-args","--phase5-train","--arenas","8","--stage","1","--train-seed","23","--metrics",str(ROOT/"Logs/Phase5Environment/pilot-episodes.jsonl")]
    log=ROOT/"Logs/Phase5Environment"/(run_id+("-resume" if resume else "")+".log")
    used=sum(json.loads(p.read_text()).get("wallSeconds",0) for p in log.parent.glob("*.run.json"))
    budget=590-used
    if budget<30: raise SystemExit("This pilot series has exhausted its conservative wall-time budget")
    start=time.monotonic()
    with log.open("w") as stream:
        proc=subprocess.Popen(command,cwd=ROOT,stdout=stream,stderr=subprocess.STDOUT)
        try: code=proc.wait(timeout=budget)
        except subprocess.TimeoutExpired:
            # Bound includes Unity startup; terminate only this owned process tree.
            subprocess.run(["taskkill","/PID",str(proc.pid),"/T","/F"],capture_output=True)
            raise SystemExit("Pilot hard stop before 10 minutes; checkpoint/export not assumed. Inspect log.")
    result={"command":command,"exitCode":code,"wallSeconds":time.monotonic()-start,"maxConfiguredDecisions":4096,"hardLimitSeconds":budget,"priorPilotWallSeconds":used}
    (log.with_suffix(".run.json")).write_text(json.dumps(result,indent=2))
    print(json.dumps(result,indent=2))
    if code: raise SystemExit(code)
    metadata(HERE/"results"/run_id, pilot_only=True)

if __name__=="__main__":
    parser=argparse.ArgumentParser()
    parser.add_argument("action",choices=["metadata","summarize","pilot"])
    parser.add_argument("path")
    parser.add_argument("--stage",type=int,default=1)
    parser.add_argument("--resume",action="store_true")
    args=parser.parse_args()
    if args.action=="metadata":metadata(args.path,args.stage)
    elif args.action=="summarize":summarize(args.path,args.stage)
    else:pilot(args.path,args.resume)
