using System;
using UnityEngine;

[RequireComponent(typeof(EnemyContext))]
public class EnemyFSMManager : MonoBehaviour
{
    public EnemyState CurrentState { get; private set; } = EnemyState.Idle;

    public event Action<EnemyState, EnemyState> OnStateChanged;

    public bool RequestState(EnemyState requestedState)
    {
        if (!CanTransition(CurrentState, requestedState))
        {
            return false;
        }

        ChangeState(requestedState);
        return true;
    }

    public void ForceDead()
    {
        if (CurrentState == EnemyState.Dead)
        {
            return;
        }

        ChangeState(EnemyState.Dead);
    }

    public bool IsState(EnemyState state)
    {
        return CurrentState == state;
    }

    public bool CanMove()
    {
        return CurrentState is EnemyState.Idle or EnemyState.Patrol or EnemyState.Chase;
    }

    public bool CanAttack()
    {
        return CurrentState is EnemyState.Idle or EnemyState.Patrol or EnemyState.Chase;
    }

    public bool CanTransition(EnemyState from, EnemyState to)
    {
        if (from == EnemyState.Dead)
        {
            return false;
        }

        if (to == EnemyState.Dead)
        {
            return true;
        }

        if (from == EnemyState.Hacked && to != EnemyState.Dead)
        {
            return false;
        }

        return to switch
        {
            EnemyState.Idle => from is EnemyState.Idle or EnemyState.Patrol or EnemyState.Chase or EnemyState.Attack or EnemyState.Hit,
            EnemyState.Patrol => from is EnemyState.Idle or EnemyState.Patrol or EnemyState.Chase,
            EnemyState.Chase => from is EnemyState.Idle or EnemyState.Patrol or EnemyState.Chase or EnemyState.Attack or EnemyState.Hit,
            EnemyState.Attack => from is EnemyState.Idle or EnemyState.Patrol or EnemyState.Chase,
            EnemyState.Hit => from is not EnemyState.Dead,
            EnemyState.Hacked => from is not EnemyState.Dead,
            _ => false,
        };
    }

    private void ChangeState(EnemyState nextState)
    {
        if (CurrentState == nextState)
        {
            return;
        }

        EnemyState previousState = CurrentState;
        CurrentState = nextState;
        OnStateChanged?.Invoke(previousState, nextState);
    }
}
