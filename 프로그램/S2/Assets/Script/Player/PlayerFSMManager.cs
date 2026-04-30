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

    public bool CompleteWeaponReceiving()
    {
        if (CurrentState != PlayerState.WeaponReceiving)
        {
            return false;
        }

        ChangeState(PlayerState.Idle);
        return true;
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
            PlayerState.WeaponAiming => CanEnterWeaponAiming(from),
            PlayerState.WeaponThrowing => CanEnterWeaponThrowing(from),
            PlayerState.WeaponReceiving => CanEnterWeaponReceiving(from),
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
            PlayerState.WeaponReceiving => 4,
            PlayerState.WeaponThrowing => 3,
            PlayerState.WeaponAiming => 3,
            PlayerState.MeleeAttack => 2,
            _ => 1,
        };
    }

    public bool CanMoveInMainState() //Move 가능 여부 헬퍼
    {
        return CurrentState == PlayerState.Idle 
        || CurrentState == PlayerState.WeaponAiming 
        || CurrentState == PlayerState.WeaponThrowing
        || CurrentState == PlayerState.WeaponReceiving;
    }

    private static bool CanEnterIdle(PlayerState from)
    {
        return from == PlayerState.Idle
            || from == PlayerState.Dodge
            || from == PlayerState.WeaponAiming
            || from == PlayerState.MeleeAttack;
    }

    private static bool CanEnterDodge(PlayerState from)
    {
        return from != PlayerState.Dead;
    }

    private static bool CanEnterWeaponAiming(PlayerState from)
    {
        return from == PlayerState.Idle;
    }

    private static bool CanEnterWeaponThrowing(PlayerState from)
    {
        return from == PlayerState.WeaponAiming;
    }

    private static bool CanEnterWeaponReceiving(PlayerState from)
    {
        return from == PlayerState.WeaponThrowing || from == PlayerState.Dodge;
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
