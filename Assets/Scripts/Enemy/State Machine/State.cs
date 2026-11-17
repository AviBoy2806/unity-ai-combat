
using UnityEngine;
using System.Collections.Generic;

public class EnemyState
{
    public int SpeedHash = Animator.StringToHash("Speed");
    public int AttackHash = Animator.StringToHash("Attack");
    public int DieHash = Animator.StringToHash("Die");

    public enum EnemyStateType
    {
        dead,
        idle,
        wander,
        chase,
        combat,
        stunned
    }

    // States that cannot be force-entered from this state.
    public HashSet<EnemyStateType> forbiddenForceStates = new();

    public enemy context;

    public bool canEnter = true;
    public bool canExit = true;

    protected void SetDampedFloat(int hash, float target, float dampTime)
    {
        if (context.animator == null)
            return;

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

    public EnemyStateType GetStateType()
    {
        for (int i = 0; i < context.stateMachine.states.Length; i++)
        {
            if (context.stateMachine.states[i] == this)
                return (EnemyStateType)i;
        }

        return EnemyStateType.idle;
    }

    public void AddForbiddenForceState(EnemyStateType state)
    {
        forbiddenForceStates.Add(state);
    }

    public void RemoveForbiddenForceState(EnemyStateType state)
    {
        forbiddenForceStates.Remove(state);
    }

    public bool CanBeForceEntered(EnemyStateType state)
    {
        return !forbiddenForceStates.Contains(state);
    }

    public virtual void Init() { }
    public virtual void Enter() { }
    public virtual void Exit() { }
    public virtual void Update() { }
    public virtual void FixedUpdate() { }
}
