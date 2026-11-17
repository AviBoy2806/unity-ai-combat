using UnityEngine;
using System.Collections.Generic;

public class PlayerState
{
    public int ForwardDirHash = Animator.StringToHash("ForwardDir");
    public int SideDirHash = Animator.StringToHash("SideDir");
    public int SpeedHash = Animator.StringToHash("NormalizedSpeed");
    public int JumpHash = Animator.StringToHash("Jump");
    public int AttackHash = Animator.StringToHash("Attack");
    public int HeavyAttackHash = Animator.StringToHash("Heavy Attack");
    public int DodgeHash = Animator.StringToHash("Dodge");
    public int DieHash = Animator.StringToHash("Die");
    public int MovingForwardHash = Animator.StringToHash("MovingForward");

    public enum PlayerStateType
    {
        dead,
        idle,
        walk,
        attack,
        run,
        jump,
        fall,
        dodge,
        heavyAttack,
        guard,
        stagger
    };

    // States that CANNOT be force-transitioned into from this state.
    public HashSet<PlayerStateType> forbiddenForceStates = new();

    public Player context;

    public bool canEnter = true;
    public bool canExit = true;

    protected void SetDampedFloat(int hash, float target, float dampTime)
    {
        if (!context.animator) return;
        float current = context.animator.GetFloat(hash);

        if (Mathf.Abs(current - target) < 0.001f)
        {
            context.animator.SetFloat(hash, target);
            return;
        }

        context.animator.SetFloat(
            hash,
            target,
            dampTime,
            Time.deltaTime
        );
    }

    public PlayerStateType GetStateType()
    {
        for (int i = 0; i < context.stateMachine.states.Length; i++)
        {
            if (context.stateMachine.states[i] == this)
                return (PlayerStateType)i;
        }

        return PlayerStateType.idle;
    }

    // Add a state that cannot be force-entered.
    public void AddForbiddenForceState(PlayerStateType state)
    {
        forbiddenForceStates.Add(state);
    }

    // Remove a restriction.
    public void RemoveForbiddenForceState(PlayerStateType state)
    {
        forbiddenForceStates.Remove(state);
    }

    public bool CanBeForceEntered(PlayerStateType state)
    {
        return !forbiddenForceStates.Contains(state);
    }

    public virtual void Init() { }
    public virtual void Enter() { }
    public virtual void Exit() { }
    public virtual void FixedUpdate() { }
    public virtual void Update() { }
}