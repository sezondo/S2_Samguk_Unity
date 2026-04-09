using System;
using UnityEngine;

public class PlayerFSMManager : MonoBehaviour
{
    
    public PlayerState CurrentState { get; private set; } = PlayerState.Idle;

    public event Action<PlayerState, PlayerState> OnStateChanged;

    public bool RequestState(PlayerState requestedState)
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
        if (CurrentState == PlayerState.Dead)
        {
            return;
        }

        ChangeState(PlayerState.Dead);
    }

    public bool IsState(PlayerState state)// 상태 확인용 헬퍼
    {
        return CurrentState == state;
    }

    public bool CanTransition(PlayerState from, PlayerState to)
    {
        if (from == PlayerState.Dead)
        {
            return false;
        }

        if (to == PlayerState.Dead)
        {
            return true;
        }

        return to switch
        {
            PlayerState.Idle => CanEnterIdle(from),
            PlayerState.Dodge => CanEnterDodge(from),
            PlayerState.BowCharge => CanEnterBowCharge(from),
            PlayerState.BowShoot => CanEnterBowShoot(from),
            PlayerState.MeleeAttack => CanEnterMeleeAttack(from),
            _ => false,
        };
    }

    public static int GetPriority(PlayerState state) // 우선순위
    {
        return state switch
        {
            PlayerState.Dead => 6,
            PlayerState.Dodge => 5,
            PlayerState.BowShoot => 4,
            PlayerState.BowCharge => 3,
            PlayerState.MeleeAttack => 2,
            _ => 1,
        };
    }

    public bool CanMoveInMainState() //Move 가능 여부 헬퍼
    {
        return CurrentState == PlayerState.Idle || CurrentState == PlayerState.BowCharge;
    }

    private static bool CanEnterIdle(PlayerState from)
    {
        return from == PlayerState.Idle
            || from == PlayerState.Dodge
            || from == PlayerState.BowCharge
            || from == PlayerState.BowShoot
            || from == PlayerState.MeleeAttack;
    }

    private static bool CanEnterDodge(PlayerState from)
    {
        return from != PlayerState.Dead;
    }

    private static bool CanEnterBowCharge(PlayerState from)
    {
        return from == PlayerState.Idle;
    }

    private static bool CanEnterBowShoot(PlayerState from)
    {
        return from == PlayerState.BowCharge;
    }

    private static bool CanEnterMeleeAttack(PlayerState from)
    {
        return from == PlayerState.Idle || from == PlayerState.MeleeAttack;
    }

    private void ChangeState(PlayerState nextState)
    {
        if (CurrentState == nextState)
        {
            return;
        }

        PlayerState previousState = CurrentState;
        CurrentState = nextState;
        OnStateChanged?.Invoke(previousState, nextState);
    }
}
