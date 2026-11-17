using UnityEngine;

[System.Serializable]
public class PlayerStateMachine
{
    public PlayerState[] states =
    {
        new DeadState(),
        new IdleState(),
        new WalkState(),
        new AttackState(),
        new RunState(),
        new JumpState(),
        new FallState(),
        new DodgeState(),
        new HeavyAttackState(),
        new GuardState(),
        new StaggerState()
    };

    public PlayerState currentState;

    private Player context;

    private bool transitioning;

    // Force transition management.
    private bool forceTransitionLocked;
    private bool pendingForceTransition;

    private PlayerState pendingForceState;

    public PlayerStateMachine(Player context)
    {
        this.context = context;

        foreach (PlayerState state in states)
        {
            state.context = context;
            state.Init();
        }

        Transition(states[(int)PlayerState.PlayerStateType.idle]);
    }

    public void Update()
    {
        ProcessPendingForceTransition();

        if (!transitioning && !forceTransitionLocked)
            currentState?.Update();
    }

    public void FixedUpdate()
    {
        if (!transitioning && !forceTransitionLocked)
            currentState?.FixedUpdate();
    }

    // NORMAL TRANSITION
    public void TryChangeState(
        PlayerState caller,
        PlayerState.PlayerStateType stateType)
    {
        // Combat input and action transitions are coordinated at fixed-step boundaries.
        if (stateType == PlayerState.PlayerStateType.attack || stateType == PlayerState.PlayerStateType.heavyAttack || stateType == PlayerState.PlayerStateType.dodge) return;
        // Block all normal transitions while force transition is pending.
        if (transitioning || forceTransitionLocked)
            return;

        PlayerState newState = states[(int)stateType];

        if (currentState == null ||
            newState == null ||
            currentState != caller ||
            !currentState.canExit ||
            !newState.canEnter)
        {
            return;
        }

        transitioning = true;

        Transition(newState);

        transitioning = false;
    }

    // FORCE TRANSITION REQUEST
    public void ForceChangeState(
        PlayerState.PlayerStateType stateType)
    {
        PlayerState newState = states[(int)stateType];

        if (newState == null)
            return;

        // Check whether this destination is forbidden
        // by the currently active state.
        if (currentState != null &&
            !newState.CanBeForceEntered(
                currentState.GetStateType()))
        {
            return;
        }

        // Already transitioning or another force request
        // is being processed: queue this request.
        if (transitioning || forceTransitionLocked)
        {
            pendingForceState = newState;
            pendingForceTransition = true;

            forceTransitionLocked = true;

            return;
        }

        // Lock immediately so no normal transition can interrupt.
        forceTransitionLocked = true;

        pendingForceState = newState;
        pendingForceTransition = true;
    }

    // PROCESS QUEUED FORCE TRANSITION
    private void ProcessPendingForceTransition()
    {
        if (!pendingForceTransition || transitioning)
            return;

        PlayerState target = pendingForceState;

        pendingForceTransition = false;
        pendingForceState = null;

        if (target == null)
        {
            forceTransitionLocked = false;
            return;
        }

        transitioning = true;

        Transition(target);

        transitioning = false;

        // Force transition has completed.
        forceTransitionLocked = false;
    }

    // ACTUAL TRANSITION
    private void Transition(PlayerState state)
    {
        if (state == null)
            return;

        if(!context.standaloneControl)Debug.Log(
            $"[STATE TRANSITION] " +
            $"{currentState?.GetType().Name ?? "NULL"} -> " +
            $"{state.GetType().Name} | " +
            $"Frame: {Time.frameCount} | " +
            $"Time: {Time.time:F3} | " +
            $"Grounded: {context.grounded} | " +
            $"Velocity: {context.rb.linearVelocity} | " +
            $"CanExit: {currentState?.canExit} | " +
            $"CanEnter: {state.canEnter}"
        );

        currentState?.Exit();

        currentState = state;

        currentState.Enter();
    }
}
