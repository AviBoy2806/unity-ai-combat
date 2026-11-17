# Unity AI Combat

A Unity melee combat project with shared combat rules, scripted enemy AI, and an experimental ML-Agents training and evaluation pipeline.

## Open the project

1. Clone this repository.
2. In Unity Hub, add the repository folder and open it with **Unity 6000.6.0f1**.
3. Allow Unity to import assets and resolve packages.
4. Open `Assets/Scenes/GameScene.unity` for gameplay or `Assets/Scenes/CombatTraining.unity` for the training scene.

The ML-Agents release 23 source is included under `Tools/ml-agents-release23`. The package manifest points to this local dependency, which includes the Unity 6000.6 compatibility change. Its upstream license and notices remain with the source.

## Project notes

- [Current implementation status](IMPLEMENTATION_PROGRESS.md)
- [Combat implementation plan](COMBAT_IMPLEMENTATION_PLAN.md)
- [Animation and sound assignments](ANIMATIONS_AND_SOUNDS_TO_ADD.md)
- [ML-Agents environment reproduction](Tools/Phase5/REPRODUCE.md)
- [Combat training and evaluation](Tools/Phase5/Combat/README.md)
- [Agent contract](Tools/Phase5/AGENT_CONTRACT.md)

The authored enemy controller remains the default. Experimental learned policies retain the limitations recorded in the implementation notes.

## Proving Ground level and HUD

`GameScene` contains a compact grid-textured arena with an open combat court, four cover piers, side lanes, and two raised decks with ramps. Floor and wall grid lines use world-space meters, with heavier lines every four meters. Materials are under `Assets/Level/ProvingGround`.

- **F1** immediately toggles the enemy between the state-based controller and the existing ML evaluation policy.
- **Tab** releases the cursor so you can click **STATE-BASED / ML** in the HUD. Tab or Escape returns to gameplay. Player movement, camera look, and combat input are blocked while the HUD cursor is open.
- Controller selection preserves health, posture, current attacks, stuns, death, and dodge cooldowns. The new controller owns subsequent decisions immediately. An unavailable policy displays its fallback reason.

The Editor menu **Combat > Level > Rebuild Proving Ground** regenerates the environment and bakes its NavMesh. This replaces the saved gameplay scene's environment and resets its spawn positions. **Validate Proving Ground** runs bounded route, input, switching, and live inference checks. Normal play never starts training.

Enemy pursuit projects an airborne player's position onto the ground below them and follows ramps before entering melee on a different elevation. The Humanoid navigation bake uses the enemy capsule's 0.5-meter radius and a 0.1-meter step height, so routes enter ramps rather than their vertical sides. **Combat > Level > Validate Jump and Elevation Pursuit** exercises moving airborne targets, ramp ascent/descent, and nearby raised targets with both controller selections.

Unity caches, executable builds, local Python installations, training checkpoints, and logs are excluded from Git. Trained model assets already installed under `Assets` are included. Recreate the Python environment using the reproduction instructions before running training scripts. Some historical evidence links point to local logs that are excluded from this repository.
