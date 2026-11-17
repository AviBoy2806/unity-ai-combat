using System;
using System.IO;
using UnityEngine;
namespace GameCombat {
// Opt-in bounded capture for visual QA; inactive during ordinary gameplay.
public sealed class CombatHudPreview : MonoBehaviour {
 int frames;double deadline;string folder;
 [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
 static void StartRequested(){if(Array.IndexOf(Environment.GetCommandLineArgs(),"--combat-hud-preview")>=0)new GameObject("HUD capture").AddComponent<CombatHudPreview>();}
 void Start(){deadline=Time.realtimeSinceStartupAsDouble+45;folder=Path.GetFullPath("Logs/Presentation20261118");Directory.CreateDirectory(folder);Time.captureDeltaTime=.02f;}
 void Update(){frames++;if(frames==60)ScreenCapture.CaptureScreenshot(Path.Combine(folder,"hud-normal.png"));if(frames==90)FindFirstObjectByType<EnemyBrainHost>().SetMode(EnemyControlMode.ML);if(frames==130)ScreenCapture.CaptureScreenshot(Path.Combine(folder,"hud-evaluation.png"));if(frames>160 && File.Exists(Path.Combine(folder,"hud-normal.png")) && File.Exists(Path.Combine(folder,"hud-evaluation.png"))){Debug.Log("COMBAT_HUD_PREVIEW_PASS");Application.Quit(0);}else if(frames>1000 || Time.realtimeSinceStartupAsDouble>deadline){Debug.LogError("COMBAT_HUD_PREVIEW_FAIL capture timeout");Application.Quit(1);}}
}
}
