using System;
using System.IO;
using GameCombat;
using GameCombat.ML;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

// Actual rigidbody/agent movement on the saved arena; no window capture or trainer.
[InitializeOnLoad]
public static class EnemyElevationValidation {
    const string Pending="EnemyElevationValidation";
    static bool started,done;
    static double deadline;
    static int scenario,ticks,checks,lastFrame=-1;
    static EnemyBrainHost host;
    static enemy body;
    static EnemyPerceptionNav nav;
    static Player player;
    static float enemyPivot,playerPivot;
    static Vector3 start;
    static int lastPhysicsStep=-1;
    static bool HighRate=>SessionState.GetBool("EnemyElevationHighRate",false);
    static bool LoadedScene=>SessionState.GetBool("EnemyElevationLoadedScene",false);
    static int Kind=>LoadedScene?3:scenario%4;
    static EnemyElevationValidation() {if(SessionState.GetBool(Pending,false)){deadline=EditorApplication.timeSinceStartup+100;EditorApplication.update+=Update;}}
    [MenuItem("Combat/Level/Validate Jump and Elevation Pursuit")]
    public static void Run() {
        SessionState.SetBool("EnemyElevationLoadedScene",false);
        EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");SessionState.SetBool(Pending,true);
        deadline=EditorApplication.timeSinceStartup+100;EditorApplication.update+=Update;EditorApplication.EnterPlaymode();
    }
    [MenuItem("Combat/Level/Validate Both Ramps at High Frame Rate")]
    public static void RunHighRate() {SessionState.SetBool("EnemyElevationHighRate",true);Run();}
    [MenuItem("Combat/Level/Verify Loaded Ramp Entrances")]
    public static void RunLoadedHighRate() {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play mode before the loaded-scene check.");
        SessionState.SetBool("EnemyElevationHighRate",true);SessionState.SetBool("EnemyElevationLoadedScene",true);SessionState.SetBool(Pending,true);
        deadline=EditorApplication.timeSinceStartup+100;EditorApplication.update-=Update;EditorApplication.update+=Update;
        EditorApplication.EnterPlaymode();
    }
    public static void CancelLoadedCheck(){if(LoadedScene)Finish(2);}
    static void Check(bool value,string message) {if(!value)throw new Exception(message);checks++;Debug.Log("ELEVATION_CHECK "+message);}
    static void Update() {
        if(done)return;
        try {
            if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Elevation validation deadline");
            if(!EditorApplication.isPlaying || Time.time<.5f || lastFrame==Time.frameCount)return;
            lastFrame=Time.frameCount;
            if(!started) {started=true;Initialize();Begin();return;}
            if(HighRate){int step=Mathf.FloorToInt(Time.fixedTime/Time.fixedDeltaTime);if(step==lastPhysicsStep)return;lastPhysicsStep=step;}
            ticks++;
            int kind=Kind;
            if(kind==0) {
                // The target stays beyond the old 2m NavMesh sampling radius while
                // moving horizontally. Its support point is still on the arena floor.
                PlacePlayer(new Vector3(Mathf.Sin(ticks*.03f),4.5f+Mathf.Sin(ticks*.025f)*1.5f,-12-ticks*.012f));
                if(ticks<140)return;
                Debug.Log($"ELEVATION_JUMP p={host.transform.position} native={nav.Agent.nextPosition} onMesh={nav.CanNavigate} path={nav.Agent.hasPath}/{nav.Agent.pathStatus} destination={nav.Agent.destination} state={body.CurrentStateName} visible={nav.CanSeePlayer()} {host.GetComponent<EnemyMotor>().MovementDiagnostic}");
                bool underneath=Vector3.ProjectOnPlane(player.transform.position-host.transform.position,Vector3.up).magnitude<1.25f;
                Check(nav.Agent.hasPath && nav.Agent.pathStatus==NavMeshPathStatus.PathComplete || underneath,"airborne moving target is pursued to its support point: "+host.ActiveMode);
                Check(nav.Agent.destination.y<.8f && Vector3.Distance(start,host.transform.position)>4,"enemy keeps moving toward ground below jump: "+host.ActiveMode);
                nav.MoveTo(player.transform.position+Vector3.back*4,5,.8f);
                Check(nav.Agent.hasPath,"fixture has an outstanding route before unsupported observation");
                Vector3 destination=nav.Agent.destination;
                nav.MoveTo(new Vector3(100,80,100),5,.8f);
                Check(nav.Agent.hasPath && Vector3.Distance(destination,nav.Agent.destination)<.01f,"unsupported observation retains last valid route");
            } else {
                float feet=body.enemyCollider.bounds.min.y;
                bool reached=kind==2 ? feet<.8f && host.transform.position.z<5 :
                    feet>1.4f && host.transform.position.z>11.25f;
                if(ticks<4)Check(!host.InCombat,"different elevation uses pathfinding before melee: "+host.ActiveMode);
                if(!reached) {
                    if(ticks%100==0)Debug.Log($"ELEVATION_TRACE case={scenario} mode={host.ActiveMode} p={host.transform.position} feet={feet:F2} combat={host.InCombat} state={body.CurrentStateName} target={player.transform.position} lastSeen={nav.LastSeenPosition} visible={nav.CanSeePlayer()} destination={nav.Agent.destination} desired={nav.Agent.desiredVelocity} path={nav.Agent.pathStatus} {nav.NavigationDiagnostic} {host.GetComponent<EnemyMotor>().MovementDiagnostic}");
                    if(ticks>550)throw new Exception($"Elevation case {scenario} stalled: position={host.transform.position} feet={feet:F2} combat={host.InCombat} {host.GetComponent<EnemyMotor>().MovementDiagnostic}");
                    return;
                }
                Check(true,(kind==2?"enemy descends ramp":kind==3?"nearby raised target routes around deck side":"enemy climbs ramp toward raised target")+": "+host.ActiveMode);
            }
            Check(host.mode==host.ActiveMode && string.IsNullOrEmpty(host.FallbackReason),"selected controller stays active without fallback");
            if(++scenario<(LoadedScene?4:HighRate?16:8)) {Begin();return;}
            Debug.Log("ENEMY_ELEVATION_VALIDATION_PASS checks="+checks+" scenarios="+scenario);Finish(0);
        }catch(Exception e){Debug.LogException(e);Finish(1);}
    }
    static void Initialize() {
        Time.captureDeltaTime=HighRate?1f/400:.02f;
        Debug.Log($"ELEVATION_SETUP renderDelta={Time.captureDeltaTime} fixedDelta={Time.fixedDeltaTime} triangles={NavMesh.CalculateTriangulation().indices.Length}");
        host=UnityEngine.Object.FindFirstObjectByType<EnemyBrainHost>();body=host.GetComponent<enemy>();nav=host.GetComponent<EnemyPerceptionNav>();
        player=UnityEngine.Object.FindFirstObjectByType<Player>();
        player.GetComponent<UnityEngine.InputSystem.PlayerInput>().enabled=false;
        player.GetComponent<CombatHud>().enabled=false;player.GetComponent<PlayerCombatAdapter>().enabled=false;player.enabled=false;
        player.rb.linearVelocity=Vector3.zero;player.rb.isKinematic=true;
        enemyPivot=host.transform.position.y-body.enemyCollider.bounds.min.y;
        playerPivot=player.transform.position.y-player.playerCollider.bounds.min.y;
        nav.target=player.transform;
        Check(nav.CanNavigate,"saved scene navigation available");
    }
    static void Begin() {
        ticks=0;int kind=Kind;
        var actor=host.GetComponent<Combatant>();actor.ContactsSuppressed=true;actor.ResetEpisodeState();
        var policy=host.agentPolicy as EnemyLearningAgent;policy?.ResetEpisodeState();
        host.mode=scenario<(LoadedScene?2:HighRate?8:4)?EnemyControlMode.Normal:EnemyControlMode.ML;host.ResetEpisodeState();body.ResetEpisodeState();nav.ResetEpisodeState();
        float side=LoadedScene?(scenario%2==0?1:-1):HighRate && scenario%8>=4?-1:1;
        Vector3 feet=kind==0?new Vector3(0,.5f,0):kind==1?new Vector3(side*12,.5f,-1):kind==2?new Vector3(side*12,1.5f,16):new Vector3(side*8,.5f,15);
        Vector3 target=kind==0?new Vector3(0,4.5f,-12):kind==2?new Vector3(side*12,.5f,-5):new Vector3(side*12,1.5f,kind==3?17:18);
        PlacePlayer(target);
        Vector3 position=feet+Vector3.up*(enemyPivot+.02f);
        Check(nav.Agent.Warp(position),"fixture starts on navigation mesh");
        host.GetComponent<EnemyMotor>().Teleport(position,Quaternion.LookRotation(Vector3.ProjectOnPlane(player.transform.position-position,Vector3.up)));
        body.verticalVelocity=0;body.grounded=body.CheckGround();actor.ContactsSuppressed=false;
        CombatWorld.ForgetMotion(actor);Physics.SyncTransforms();nav.SendMessage("Update");
        Check(nav.CanSeePlayer(),"known target visible at pursuit start");
        body.stateMachine.ForceChangeState(EnemyState.EnemyStateType.chase);
        Check(host.SetModeImmediately(host.mode)==BrainSwitchResult.Applied,"fixture activates "+host.mode);
        start=position;
        Debug.Log("ELEVATION_SCENARIO "+scenario+" "+host.mode);
    }
    static void PlacePlayer(Vector3 feet) {
        var position=feet+Vector3.up*playerPivot;player.rb.position=position;player.transform.position=position;Physics.SyncTransforms();
    }
    static void Finish(int code) {
        done=true;
        if(LoadedScene)File.WriteAllText("Logs/ProvingGround/main-editor-slope-result.txt",$"{(code==0?"PASS":code==2?"CANCELLED":"FAIL")} checks={checks} scenarios={scenario} time={DateTime.UtcNow:O}");
        SessionState.SetBool(Pending,false);SessionState.SetBool("EnemyElevationHighRate",false);SessionState.SetBool("EnemyElevationLoadedScene",false);
        if(Application.isBatchMode)EditorApplication.Exit(code);else EditorApplication.isPlaying=false;
    }
}
