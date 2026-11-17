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

Unity caches, executable builds, local Python installations, training checkpoints, and logs are excluded from Git. Trained model assets already installed under `Assets` are included. Recreate the Python environment using the reproduction instructions before running training scripts. Some historical evidence links point to local logs that are excluded from this repository.
