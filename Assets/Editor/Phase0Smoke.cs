using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class Phase0Smoke
{
    static int stage;
    static double until;
    static Vector3 start;
    static bool failed;
    static Phase0Smoke() {
        if (SessionState.GetBool("Phase0Smoke", false)) {
            EditorApplication.update += Tick;
            Application.logMessageReceived += Log;
        }
    }
    public static void Run() {
        EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
        SessionState.SetBool("Phase0Smoke", true);
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        Application.logMessageReceived -= Log;
        Application.logMessageReceived += Log;
        EditorApplication.EnterPlaymode();
    }
    static void Log(string text, string stack, LogType type) {
        if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) failed = true;
    }
    static void Tick() {
        if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < until) return;
        try {
            var p = UnityEngine.Object.FindFirstObjectByType<Player>();
            var e = UnityEngine.Object.FindFirstObjectByType<enemy>();
            if (!p || !e) throw new Exception("Actors missing");
            switch (stage++) {
                case 0: until=EditorApplication.timeSinceStartup+2; break;
                case 1:
                    start=p.transform.position; p.input.movement=Vector2.up; until=EditorApplication.timeSinceStartup+1; break;
                case 2:
                    if(Vector3.Distance(start,p.transform.position)<0.2f) throw new Exception("Movement failed");
                    p.input.movement=Vector2.zero;
                    p.stateMachine.ForceChangeState(PlayerState.PlayerStateType.attack); until=EditorApplication.timeSinceStartup+5; break;
                case 3:
                    if(p.stateMachine.currentState is AttackState) throw new Exception("Attack did not finish");
                    p.stateMachine.ForceChangeState(PlayerState.PlayerStateType.heavyAttack); until=EditorApplication.timeSinceStartup+5; break;
                case 4:
                    if(p.stateMachine.currentState is HeavyAttackState) throw new Exception("Heavy attack did not finish");
                    p.stateMachine.ForceChangeState(PlayerState.PlayerStateType.dodge); until=EditorApplication.timeSinceStartup+2; break;
                case 5:
                    if(p.stateMachine.currentState is DodgeState) throw new Exception("Dodge did not finish");
                                        var targetNav=e.GetComponent<EnemyPerceptionNav>();
                    bool placed=false;
                    for(int i=0;i<8;i++) {
                        Vector3 offset=Quaternion.Euler(0,i*45,0)*Vector3.forward*3;
                        if(UnityEngine.AI.NavMesh.SamplePosition(e.transform.position+offset,out var hit,1,UnityEngine.AI.NavMesh.AllAreas) && !Physics.Linecast(e.transform.position+Vector3.up*0.5f,hit.position+Vector3.up*0.5f,targetNav.obstacleLayers)) {
                            p.rb.position=hit.position+Vector3.up; p.rb.linearVelocity=Vector3.zero; placed=true; break;
                        }
                    }
                    if(!placed) throw new Exception("No visible test position");
                    until=EditorApplication.timeSinceStartup+2; break;
                case 6:
                    var nav=e.GetComponent<EnemyPerceptionNav>();
                                        if(!nav.CanNavigate) throw new Exception("Enemy off NavMesh");
                    if(!(e.stateMachine.currentState is EnemyChaseState) || float.IsNegativeInfinity(nav.LastSeenTime)) throw new Exception("Chase detection failed");
                    p.lockOn.enabled=true;
                    if(p.lockOn.target!=e) throw new Exception("Lock-on failed");
                    if(!Camera.main) throw new Exception("Main camera missing");
                    nav.detectPlayer=false; until=EditorApplication.timeSinceStartup+20; break;
                case 7:
                    if(e.stateMachine.currentState is EnemyChaseState) throw new Exception("Investigation did not time out");
                    e.GetComponent<HealthComponent>().takeDamage(1000);
                    p.GetComponent<HealthComponent>().takeDamage(1000); until=EditorApplication.timeSinceStartup+1; break;
                default:
                    if(!e.isDead || !(p.stateMachine.currentState is DeadState)) throw new Exception("Death failed");
                    Finish(); break;
            }
        } catch(Exception ex) { failed=true; Debug.LogException(ex); Finish(); }
    }
    static void Finish() {
        SessionState.SetBool("Phase0Smoke",false);
        Debug.Log(failed ? "PHASE0_SMOKE_FAIL" : "PHASE0_SMOKE_PASS");
        EditorApplication.Exit(failed ? 1 : 0);
    }
}



