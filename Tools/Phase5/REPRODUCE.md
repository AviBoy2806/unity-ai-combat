# Phase 5 local compatibility spike

Run from the project root in PowerShell. No cloud service is used. The smoke policy is not a combat model or a convergence result.

Source: `release_23_tag`, commit `a2777719560e4676be99e2ea128c5eb1fbeb3dbb` from https://github.com/Unity-Technologies/ml-agents. Both Python distributions and the Unity package come from this checkout. See `baseline.json`, `environment.freeze.txt`, `unity6000.6.patch`, and the Unity package lock.

## Compatibility changes

The original tag fails compilation on Unity 6000.6: Match3 uses GetInstanceID, now an obsolete-error API. The guarded patch replaces only the auto-generated Match3 seed with GetEntityId().GetHashCode() on 6000.6+. Explicit seeds retain their original behavior. Combat does not use Match3.

The installed editor's `Editor/Data/Resources/PackageManager/Editor/manifest.json` declares Inference Engine **2.6.1** as its minimum. Unity resolved the tag's 2.2.1 requirement to 2.6.1. The project explicitly pins 2.6.1 to document that requirement. The editor and existing direct dependencies were not upgraded.

The trainer imports pkg_resources. Setuptools 84.2.1 removed it; 80.9.0 restores it. No trainer source was changed. The first ONNX wheel download timed out; retrying with UV_HTTP_TIMEOUT=300 succeeded.

## Commands executed

```powershell
git clone --depth 1 --branch release_23_tag https://github.com/Unity-Technologies/ml-agents.git Tools/ml-agents-release23
python -m pip install --target Tools/bootstrap uv==0.9.9
Tools/bootstrap/bin/uv.exe python install 3.10.12 --install-dir Tools/python --cache-dir Tools/uv-cache --no-progress
Tools/bootstrap/bin/uv.exe venv Tools/ml-venv --python Tools/python/cpython-3.10.12-windows-x86_64-none/python.exe --seed --cache-dir Tools/uv-cache
$env:UV_HTTP_TIMEOUT='300'
Tools/bootstrap/bin/uv.exe pip install --python Tools/ml-venv/Scripts/python.exe --cache-dir Tools/uv-cache torch==2.1.1 ./Tools/ml-agents-release23/ml-agents-envs ./Tools/ml-agents-release23/ml-agents
Tools/bootstrap/bin/uv.exe pip install --python Tools/ml-venv/Scripts/python.exe --cache-dir Tools/uv-cache setuptools==80.9.0
Tools/ml-venv/Scripts/python.exe -m pip check
Tools/ml-venv/Scripts/python.exe -m pip freeze
```

Apply `unity6000.6.patch` inside the source checkout when recreating it. The project manifest uses the local package path. The copied Basic example also needs SharedAssets/Scripts/SensorBase.cs and ProjectSettingsOverrides.cs with its meta file. BasicController adds action callback counters for the inference probe; its gameplay/reset implementation is unchanged. The copied example's missing shared materials do not affect observations or actions.

Unity commands use `C:/Program Files/Unity/Hub/Editor/6000.6.0f1/Editor/Unity.exe`. Run only with no other Editor holding this project. The agent launches its batch processes with Start-Process -WindowStyle Hidden -PassThru and waits for their exit. Use absolute project and log paths.

```text
-batchmode -nographics -projectPath "PROJECT" -executeMethod Phase5SpikeBuild.Run -quit -logFile "PROJECT/Logs/Phase5BuildTraining2.log"
```

```powershell
Tools/ml-venv/Scripts/mlagents-learn.exe Tools/Phase5/Basic-smoke.yaml --env=Tools/Phase5/Training/Basic.exe --run-id=phase5-basic-23 --results-dir=Tools/Phase5/results --seed=23 --no-graphics --torch-device=cpu --time-scale=20 --env-args --phase5-train
Copy-Item Tools/Phase5/results/phase5-basic-23/Basic.onnx Assets/CompatibilitySpike/ExportedBasic.onnx
```

Use a new run ID on a rerun. The config is the tagged PPO Basic config with only max_steps=512 and summary_freq=128. The trainer recorded the final checkpoint at step 513. Export SHA256: `DB8826735AECDAAFF159C88CCEE55E51AB8C7A8DEDE3A73BA74A2B2A016049C3`.

```text
-batchmode -nographics -projectPath "PROJECT" -executeMethod Phase5SpikeInference.Run -logFile "PROJECT/Logs/Phase5Inference2.log"
-batchmode -nographics -projectPath "PROJECT" -executeMethod Phase5MotionValidation.Run -logFile "PROJECT/Logs/Phase5Motion2.log"
```

The final inference probe persists the newly exported model as an override in the isolated Basic scene, checks its asset path and InferenceOnly setting across every reset, uses Burst CPU, and disables the communicator. `Logs/Phase5Inference6.log` records 128 real model action callbacks and all three Basic action choices. Earlier movement-only and mixed-model attempts are not gate evidence. The source Basic example reloads its scene after goals and emits tensor/worker leak warnings; do not use that reset implementation for a combat training environment. This run does not claim that 512 steps solved Basic.

## Movement measurements

`Logs/Phase5Motion.txt` records 18 saved-GameScene trials. Each action starts after 50 fixed ticks of settling, then samples until shared action completion. Scene reloads isolate trials. Commands, fixed timestep (0.02), capture timestep (0.02), initial scene, motors and authored controllers are identical. Contacts with the other actor are excluded. The three routes are Animator+renderers, Animator without renderers, and disabled Animator fallback.

Tolerance, chosen before execution: **maximum planar error <=0.02 m, RMS planar error <=0.01 m, identical sample count**. The hidden-renderer route has zero measured error for both actors' light, heavy and dodge. Player fallback fails: light max/RMS 0.663035/0.343806 m; heavy 1.352548/0.764303 m. Dodge and all enemy cases have zero difference in this scene. Enemy attacks have zero travel on these profiles; enemy dodge travels 2.175995 m. This does not prove parity for every blend/input, wall or frame rate.

Chosen route: retain the same Animator, presentation, script root-motion handler and physics when training; set AlwaysAnimate, optionally hide renderers. TrainingMotionEvaluation provides a reversible preparation lease and rejects missing required animation or an unmeasured clock. The approved envelope uses fixedDeltaTime=0.02, captureDeltaTime=0.02 and timeScale=1. Accelerated or variable evaluation is not validated. Animation-free fallback remains useful for gameplay completion tests and is **not approved as equivalent player training movement**. Heavy leap, foot correction and independent combat timers are retained.

## Agent validation commands

Use the same Unity executable and absolute project/log paths:

```text
-batchmode -nographics -projectPath "PROJECT" -executeMethod Phase5AgentFixture.Create -quit -logFile "PROJECT/Logs/Phase5Fixture.log"
-batchmode -nographics -projectPath "PROJECT" -executeMethod Phase5AgentValidation.Run -logFile "PROJECT/Logs/Phase5AgentFinal3.log"
-batchmode -nographics -projectPath "PROJECT" -executeMethod PostureRegressionValidation.Run -logFile "PROJECT/Logs/Phase5SharedFinal.log"
```

Final results: **Logs/Phase5AgentFinal4.log**, 135 focused checks; **Logs/Phase5SharedFinal.log**, all four existing regression suites pass. The focused tests manually step Academy only inside the fixture and restore its stepping mode. **Logs/Phase5LiveAgent2.log** additionally verifies the normal CombatWorld/Academy loop for 300 physics ticks and all five combat choices, without Python or fallback. Run it with `-executeMethod Phase5LiveAgentValidation.Run`. The first live run failed to exercise heavy/dodge because the heuristic's cycling aliased with commitment; retaining each next combat choice until legal fixes that test policy. See AGENT_CONTRACT.md for the schema, metadata and action schedule. Current project script hashes are in project-source-hashes.json; this workspace has no project Git repository, so no project commit ID is invented.
