using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using GameCombat;
using GameCombat.Training;

// Bounded clock/diagnostic check in the real scene. No trainer or model run.
[InitializeOnLoad]
public static class Phase5VisualInspectionValidation {
 const string Folder="Logs/VisualInspection20261117";
 static readonly List<string> lines=new();
 static CombatTrainingSession session;
 static CombatTrainingArena arena;
 static int phase,phaseFrame,lastFrame=-1,resetCount;
 static double deadline,wallStart,simStart;
 static bool finished;
 static readonly FieldInfo AutomatedRun=typeof(CombatTrainingSession).GetField("automatedRun",BindingFlags.Instance|BindingFlags.NonPublic);
 static Phase5VisualInspectionValidation(){if(SessionState.GetBool("P5VisualInspectionValidation",false)){deadline=EditorApplication.timeSinceStartup+45;EditorApplication.update+=Update;}}
 public static void Run(){
  Directory.CreateDirectory(Folder);
  EditorSceneManager.OpenScene(Phase5EnvironmentBuild.ScenePath);
  var source=UnityEngine.Object.FindFirstObjectByType<CombatTrainingSession>();
  Check(source && !source.normalSpeedVisualInspection,"Saved scene defaults to accelerated mode");
  source.normalSpeedVisualInspection=true;source.arenas[0].hideRenderers=false;
  // Unsaved scene overrides exercise Awake/Start with the checkbox already on.
  SessionState.SetBool("P5VisualInspectionValidation",true);
  deadline=EditorApplication.timeSinceStartup+45;EditorApplication.update+=Update;
  EditorApplication.EnterPlaymode();
 }
 static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
 static void Clock(bool normal){Check(Mathf.Abs(Time.timeScale-(normal ? 1 : 4))<.00001f && Mathf.Abs(Time.fixedDeltaTime-.02f)<.00001f && Mathf.Abs(Time.captureDeltaTime-(normal ? .02f : .005f))<.00001f,"Unexpected supported clock, phase "+phase);}
 static void Prepared(){foreach(var actor in new[]{arena.learner,arena.opponent}){Check(TrainingMotionEvaluation.TryPrepare(actor,false,out var lease,out var reason),reason);lease.Dispose();}}
 static void Next(){phase++;phaseFrame=Time.frameCount;}
 static void Update(){
  if(finished)return;
  if(EditorApplication.timeSinceStartup>deadline){Fail(new Exception("45-second validation deadline"));return;}
  if(!EditorApplication.isPlaying || Time.frameCount==lastFrame)return;
  lastFrame=Time.frameCount;
  try{
   if(phase==0){
    session=UnityEngine.Object.FindFirstObjectByType<CombatTrainingSession>();if(!session)return;
    arena=session.arenas[0];if(arena.Rewards==null || arena.ResetCount<1)return;
    foreach(var a in session.arenas)a.outputPath=Folder+"/diagnostic-episodes.jsonl";
    Check(session.NormalSpeedVisualInspectionActive,"Checkbox did not survive Play Mode startup");Clock(true);Prepared();
    Check(!Unity.MLAgents.Academy.Instance.IsCommunicatorOn && arena.agent.testHeuristic && !arena.agent.trainingMode,"Diagnostic attempted training or Python communication");
    Check(arena.learner.GetComponentsInChildren<Renderer>(true).Any(r=>r.enabled) && arena.opponent.GetComponentsInChildren<Renderer>(true).Any(r=>r.enabled),"Actors were hidden despite inspection setup");
    lines.Add("PASS normal mode survives session Awake and arena Start; measured clock accepted for both authored actors; renderers enabled; heuristic only; communicator off");
    wallStart=System.Diagnostics.Stopwatch.GetTimestamp()/(double)System.Diagnostics.Stopwatch.Frequency;simStart=Time.timeAsDouble;Next();
   }else if(phase==1){
    Clock(true);if(Time.frameCount-phaseFrame<75)return;
    double wall=System.Diagnostics.Stopwatch.GetTimestamp()/(double)System.Diagnostics.Stopwatch.Frequency-wallStart,sim=Time.timeAsDouble-simStart;
    Check(wall>=1.4 && sim/wall<=1.1,"Inspection advanced faster than wall time: sim="+sim+" wall="+wall);
    lines.Add($"PASS 75-frame wall pacing: simulated={sim:F6}s wall={wall:F6}s ratio={sim/wall:F6}; no rendered animation acceptance inferred");
    Time.timeScale=9;Time.captureDeltaTime=.001f;Next();
   }else if(phase==2 && Time.frameCount-phaseFrame>=2){
    Clock(true);lines.Add("PASS repeated session Update restores normal mode after external clock overwrite");
    resetCount=arena.ResetCount;arena.enabled=false;Time.timeScale=9;Time.captureDeltaTime=.001f;arena.enabled=true;Clock(true);Next();
   }else if(phase==3 && Time.frameCount-phaseFrame>=4){
    Clock(true);Check(arena.ResetCount>resetCount,"Arena did not reset after re-enable");Prepared();
    lines.Add("PASS arena re-enable preparation and queued reset preserve the normal clock");
    session.normalSpeedVisualInspection=false;Next();
   }else if(phase==4 && Time.frameCount-phaseFrame>=2){
    Clock(false);Prepared();lines.Add("PASS checkbox off restores original accelerated 4/0.005 clock, fixed 0.02");
    session.normalSpeedVisualInspection=true;Next();
   }else if(phase==5 && Time.frameCount-phaseFrame>=2){
    Clock(true);AutomatedRun.SetValue(session,true);CombatTrainingSession.ApplyClock();Clock(false);
    Check(!session.NormalSpeedVisualInspectionActive,"Automated-run guard permitted inspection");Next();
   }else if(phase==6 && Time.frameCount-phaseFrame>=3){
    Clock(false);lines.Add("PASS simulated latched automated-run flag ignores enabled checkbox through repeated Update; no training/evaluation process launched");
    AutomatedRun.SetValue(session,false);Next();
   }else if(phase==7 && Time.frameCount-phaseFrame>=2){
    Clock(true);UnityEngine.Object.Destroy(session);Next();
   }else if(phase==8 && Time.frameCount-phaseFrame>=2){
    CombatTrainingSession.ApplyClock();Clock(false);lines.Add("PASS destroyed session releases clock owner; static fallback remains accelerated");
    lines.Add("PHASE5_VISUAL_INSPECTION_VALIDATION_PASS");Finish(0);
   }
  }catch(Exception ex){Fail(ex);}
 }
 static void Fail(Exception ex){lines.Add(ex.ToString());Debug.LogException(ex);Finish(1);}
 static void Finish(int code){finished=true;SessionState.SetBool("P5VisualInspectionValidation",false);File.WriteAllLines(Folder+"/Validation.txt",lines);EditorApplication.update-=Update;Debug.Log(code==0?"PHASE5_VISUAL_INSPECTION_VALIDATION_PASS":"PHASE5_VISUAL_INSPECTION_VALIDATION_FAIL");if(Application.isBatchMode)EditorApplication.Exit(code);else EditorApplication.isPlaying=false;}
}
