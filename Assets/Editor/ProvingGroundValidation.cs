using System;
using GameCombat;
using GameCombat.ML;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

[InitializeOnLoad]
public static class ProvingGroundValidation {
    const string Pending="ProvingGroundValidation";
    static bool started,done;
    static int checks,ticks;
    static double deadline;
    static EnemyBrainHost host;
    static Combatant actor;
    static EnemyLearningAgent policy;
    static long firstDecision;
    static ProvingGroundValidation() { if(SessionState.GetBool(Pending,false)) { deadline=EditorApplication.timeSinceStartup+75;EditorApplication.update+=Update; } }
    [MenuItem("Combat/Level/Validate Proving Ground")]
    public static void Run() {
        EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
        ProvingGroundSetup.ValidateRoutes();
        SessionState.SetBool(Pending,true);deadline=EditorApplication.timeSinceStartup+75;
        EditorApplication.update+=Update;EditorApplication.EnterPlaymode();
    }
    static void Check(bool ok,string message) { if(!ok)throw new Exception(message);checks++;Debug.Log("PROVING_CHECK "+message); }
    static void Update() {
        if(done)return;
        try {
            if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Arena validation deadline");
            if(!EditorApplication.isPlaying || started || Time.time<.8f)return;
            started=true;Rules();
        } catch(Exception e) {Debug.LogException(e);Finish(1);}
    }
    static void Rules() {
        host=UnityEngine.Object.FindFirstObjectByType<EnemyBrainHost>();actor=host.GetComponent<Combatant>();
        var player=UnityEngine.Object.FindFirstObjectByType<Player>();var hud=player.GetComponent<CombatHud>();
        policy=host.agentPolicy as EnemyLearningAgent;
        Check(host.mode==EnemyControlMode.Normal && host.ActiveMode==EnemyControlMode.Normal,"scene starts with state-based controller");
        Check(policy && policy.CanActivate(host.AgentTuningHash,out _),"existing learned policy remains compatible");
        Check(host.GetComponent<UnityEngine.AI.NavMeshAgent>().isOnNavMesh,"enemy spawns on baked navigation");
        Check(player.IsGrounded() && host.GetComponent<enemy>().CheckGround(),"both spawn capsules grounded");
        var world=UnityEngine.Object.FindFirstObjectByType<CombatWorld>();world.enabled=false;host.enabled=false;
        Free(actor);
        float health=actor.Health.Health,posture=actor.Posture;
        foreach(float elapsed in new[]{0f,actor.light.Windup+.01f,actor.light.Windup+actor.light.Active+.01f}) {
            actor.Runner.SetGuard(false);Check(actor.Runner.TryStart(CombatRequest.Light,actor.facing.forward)==RequestResult.Accepted,"start switch fixture attack");
            actor.Runner.Tick(elapsed);var phase=actor.Runner.Phase;var id=actor.Runner.AttackId;uint generation=host.Generation;
            Check(host.SetModeImmediately(EnemyControlMode.ML)==BrainSwitchResult.Applied && host.ActiveMode==EnemyControlMode.ML && !host.HasPendingSwitch,"ML ownership changes immediately during "+phase);
            Check(host.Generation!=generation && !host.BufferIntent(generation,1,new CombatIntent{request=CombatRequest.Heavy}),"previous owner cannot deliver stale commands");
            Check(actor.Runner.Action==CombatAction.Attack && actor.Runner.Phase==phase && actor.Runner.AttackId==id && actor.Runner.Elapsed==elapsed,"switch preserves committed attack");
            Check(host.SetModeImmediately(EnemyControlMode.Normal)==BrainSwitchResult.Applied && host.ActiveMode==EnemyControlMode.Normal,"state-based ownership changes immediately during "+phase);
            Free(actor);
        }
        Check(actor.Health.Health==health && actor.Posture<=posture,"switching preserves combat resources");
        actor.Runner.TryStart(CombatRequest.Dodge,Vector3.back);float cooldown=actor.Runner.DodgeCooldownFraction;
        host.SetModeImmediately(EnemyControlMode.ML);Check(actor.Runner.Action==CombatAction.Dodge && actor.Runner.DodgeCooldownFraction==cooldown,"switch retains dodge and cooldown");Free(actor);
        actor.Runner.Interrupt(1,CombatAction.Stagger,true);float remaining=actor.Runner.Remaining;
        host.SetModeImmediately(EnemyControlMode.Normal);host.SetModeImmediately(EnemyControlMode.ML);
        Check(actor.Runner.Broken && actor.Runner.Remaining==remaining && host.GetComponent<enemy>().IsStunned,"immediate switch cannot escape posture break");Free(actor);
        var binding=host.agentPolicy;host.agentPolicy=null;
        Check(host.SetModeImmediately(EnemyControlMode.ML)==BrainSwitchResult.Unavailable && host.ActiveMode==EnemyControlMode.Normal && !string.IsNullOrEmpty(host.FallbackReason),"missing ML safely falls back with visible reason");
        host.agentPolicy=binding;host.SetModeImmediately(EnemyControlMode.Normal);
        host.enabled=true;
        InputChecks(player,hud);
        // Exercise the actual Academy/physics loop in the rebuilt arena, without a trainer.
        var target=host.transform.position+Vector3.back*2.5f;target.y=player.transform.position.y;
        player.rb.position=target;player.transform.position=target;player.rb.linearVelocity=Vector3.zero;
        host.GetComponent<EnemyPerceptionNav>().target=player.transform;Physics.SyncTransforms();
        Check(host.SetModeImmediately(EnemyControlMode.ML)==BrainSwitchResult.Applied,"activate real inference in rebuilt arena");
        firstDecision=policy.DecisionCount;world.enabled=true;CombatWorld.AfterStep+=Sample;
    }
    static void Free(Combatant a) {int budget=500;while(a.Runner.Action!=CombatAction.Free && !a.Dead && budget-->0)a.Runner.Tick(.02f);a.Runner.SetGuard(false);Check(budget>0,"bounded action completes");}
    static void InputChecks(Player player,CombatHud hud) {
        var input=player.inputComponent;var driver=player.GetComponent<UnityEngine.InputSystem.PlayerInput>();
        using var scope=new ValidationInputScope(driver);
        InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings.updateMode=InputSettings.UpdateMode.ProcessEventsManually;
        var keyboard=scope.AddDevice<Keyboard>();var mouse=scope.AddDevice<Mouse>();
        driver.SwitchCurrentControlScheme(keyboard,mouse);driver.ActivateInput();
        void Keys(params Key[] held) {InputSystem.QueueStateEvent(keyboard,new KeyboardState(held));InputSystem.Update();}
        Keys(Key.Tab);hud.SendMessage("Update");
        Check(input.HudInputActive && Cursor.lockState==CursorLockMode.None && Cursor.visible,"Tab releases HUD cursor");
        Keys();Keys(Key.W,Key.Space,Key.LeftShift);
        InputSystem.QueueStateEvent(mouse,new MouseState{delta=new Vector2(20,5)}.WithButton(MouseButton.Left).WithButton(MouseButton.Right));InputSystem.Update();
        Check(input.input.movement==Vector2.zero && input.input.look==Vector2.zero && !input.input.attackPressed && !input.input.jumpTriggered && input.ConsumeCombat()==CombatRequest.None && !input.ConsumeGuard(),"HUD clicks cannot leak attack, guard, look or movement");
        Keys(Key.F1);hud.SendMessage("Update");Check(host.ActiveMode==EnemyControlMode.ML,"F1 selects ML through HUD");
        Keys();Keys(Key.F1);hud.SendMessage("Update");Check(host.ActiveMode==EnemyControlMode.Normal,"F1 returns to state-based controller");
        Keys();Keys(Key.Escape);hud.SendMessage("Update");
        Check(!input.HudInputActive,"Escape resumes gameplay input");
        Keys();InputSystem.QueueStateEvent(mouse,new MouseState());InputSystem.Update();
        Keys(Key.W);InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Left));InputSystem.Update();
        Check(input.input.movement.y>0 && input.ConsumeCombat()==CombatRequest.Light,"movement and attack resume after HUD closes");
        Keys();InputSystem.QueueStateEvent(mouse,new MouseState());InputSystem.Update();input.ResetEpisodeState();
    }
    static void Sample() {
        if(done)return;
        try {
            if(++ticks<180)return;
            Check(policy.DecisionCount>=firstDecision+5 && host.ActiveMode==EnemyControlMode.ML && string.IsNullOrEmpty(host.FallbackReason),"real model delivers live decisions without fallback: "+(policy.DecisionCount-firstDecision));
            Check(host.GetComponent<UnityEngine.AI.NavMeshAgent>().isOnNavMesh,"enemy stays on new navigation mesh");
            host.SetModeImmediately(EnemyControlMode.Normal);
            Check(host.ActiveMode==EnemyControlMode.Normal,"return to state-based after live inference");
            actor.Health.takeDamage(1000);host.SetModeImmediately(EnemyControlMode.ML);
            Check(actor.Dead && actor.Runner.Action==CombatAction.Dead,"mode selection cannot revive defeated enemy");
            Debug.Log("PROVING_GROUND_VALIDATION_PASS checks="+checks+" liveTicks="+ticks);Finish(0);
        }catch(Exception e){Debug.LogException(e);Finish(1);}
    }
    static void Finish(int code) {done=true;CombatWorld.AfterStep-=Sample;SessionState.SetBool(Pending,false);if(Application.isBatchMode)EditorApplication.Exit(code);else EditorApplication.isPlaying=false;}
}
