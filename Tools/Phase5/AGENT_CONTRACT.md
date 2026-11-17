# Combat agent contract v1

The package-free assembly `GameCombat.AgentContract` contains values and a policy callback. Actor integration remains the only code that submits requests. `GameCombat.MLAdapter` references the contract and ML-Agents, and has no reference to the actor implementation. Shared combat interfaces contain no ML package types.

The vector contains **66 floats**, with **3 decision-frame stacks** configured in BehaviorParameters. `DecisionFrame.ObservationNames` lists every index in order. Position uses 10 m, planar velocity uses 20 m/s, health/posture use their maxima, action progress uses action duration, recovery uses the attack's recovery, and own cooldown/rearm use configured maxima. Values clamp to [-1,1]; booleans and categorical values use 0/1. Geometry uses 10 m clearance and a 0.15 m walkable step. Sighting age clamps at 10 seconds. The body-forward basis is assisted toward the delayed visible target, exactly as the existing enemy motor uses it. Opponent velocity is relative velocity at the delayed sample time.

| Indices | Meaning |
|---|---|
| 0–4 | Self health, posture, grounded, local velocity X/Z |
| 5–10 | Self action: Free, Attack, Dodge, Stagger, Recoil, Dead |
| 11–14 | Self phase: None, Windup, Active, Recovery |
| 15–19 | Self progress, recovery remaining, dodge cooldown, guard, deflection rearm |
| 20–29 | Opponent visible, relative position X/Z, relative velocity X/Z, forward X/Z, health, posture, guard |
| 30–35 | Opponent action, same order as self |
| 36–39 | Opponent phase, same order as self |
| 40 | Opponent progress |
| 41–56 | Eight distance/walkability pairs, forward then clockwise in 45-degree steps |
| 57–58 | Previous accepted movement request X/Z, zero during commitment |
| 59–63 | Previous accepted combat choice: neutral, guard, light, heavy, dodge |
| 64–65 | Previous decision request rejected, time since valid delayed sighting |

Opponent live values come only from EnemyObservationStream's 0.16-second delayed public snapshot. Current visibility loss immediately clears all opponent fields in the current vector, including category one-hots. Reacquisition starts a fresh delay. Older stacked frames are past observations, not new live queries. No player buttons, combo queue, hidden opponent cooldown, future outcome or RNG is observed.

Two discrete branches: movement **9**, combat **5**. Movement 0 is stationary; 1–8 are normalized forward, forward-right, right, backward-right, backward, backward-left, left, forward-left. Combat is neutral, guard, light, heavy, dodge. Neutral stays legal. Movement is masked during commitment and when the motor reports a blocked short step. Combat masks use grounded/free/dead, valid attack definitions and the shared dodge cooldown. Attacks are not masked for tactical distance. Runtime rules remain authoritative.

A snapshot is requested every three combat ticks (0.06 s). Responses buffer into the host and apply only at the next combat tick (0.02 s latency). No gameplay executes in OnActionReceived. Duplicate, late, old-generation and disabled-owner callbacks cannot submit. A missing deadline falls back to Normal at a safe boundary. Movement/guard persist between decisions; light/heavy/dodge are submitted once. Visibility loss suppresses pending attacks immediately. The existing public BufferIntent path also buffers rather than writing live intent from a callback.

Runtime policy activation requires InferenceOnly, Burst CPU, a model, matching BehaviorParameters, and metadata. Create a TextAsset JSON with these fields for a future combat export:

```json
{"contractVersion":1,"observationSize":66,"stacks":3,"tuningHash":"VALUE_FROM_EnemyBrainHost.AgentTuningHash"}
```

The tuning hash uses SHA256 over ordered tuning/light-chain/heavy serialized data, with transient Unity instance IDs removed. No final combat model or valid combat model metadata is supplied in this task. The exported Basic smoke model uses a different contract and must never be assigned as a combat policy.

`EnemyLearningAgent.testHeuristic` is an explicit test-only choice. The Agent heuristic uses these exact arrays/masks and cycles movement/combat branches. `ContractHeuristicPolicy` is a package-free boundary fixture with selectable branches and delayed-callback injection. Neither is selected in GameScene. The separate `Assets/Scenes/AgentInterfaceDiagnostic.unity` selects the real Agent heuristic, while preserving the original GameScene actor/controller assets. This is a diagnostic scene, not a training environment or curriculum.

Ordinary Editor play and script reloads disable CommunicatorFactory before Academy initialization. Standalone local training requires both `--phase5-train` and the trainer's `--mlagents-port`. Setting InferenceOnly alone was found insufficient to prevent the package's editor connection attempt.

Training motion is approved only in the measured envelope: fixedDeltaTime=0.02, captureDeltaTime=0.02, timeScale=1, original Animator graph and presentation, AlwaysAnimate, original root-motion handler. TrainingMotionEvaluation refuses an unmeasured cadence or missing animation. Accelerated/variable Animator evaluation and additional blend trajectories require new measurements before substantial training.
