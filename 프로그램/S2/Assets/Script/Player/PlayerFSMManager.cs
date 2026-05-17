using System;
using UnityEngine;

public class PlayerFSMManager : MonoBehaviour
{
    private PlayerControlLock controlLock;

    public PlayerState CurrentState { get; private set; } = PlayerState.Idle;

    public event Action<PlayerState, PlayerState> OnStateChanged;

    private void Awake()
    {
        PlayerContext context = GetComponent<PlayerContext>();
        if (context == null)
        {
            return;
        }

        context.ResolveReferences();
        controlLock = context.ControlLock;
    }

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

    public bool CompleteHacking()
    {
        if (CurrentState != PlayerState.Hacking)
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
            PlayerState.Hacking => CanEnterHacking(from),
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
            PlayerState.Hacking => 4,
            PlayerState.WeaponReceiving => 4,
            PlayerState.WeaponThrowing => 3,
            PlayerState.WeaponAiming => 3,
            PlayerState.MeleeAttack => 2,
            _ => 1,
        };
    }

    public bool CanMove() //Move 가능 여부 헬퍼
    {
        return CanMoveInMainState() && (controlLock == null || !controlLock.IsMovementLocked);
    }

    public bool CanMoveInMainState()
    {
        return CurrentState == PlayerState.Idle 
        || CurrentState == PlayerState.WeaponAiming 
        || CurrentState == PlayerState.WeaponReceiving
        || CurrentState == PlayerState.Hacking;
    }

    private static bool CanEnterIdle(PlayerState from)
    {
        return from == PlayerState.Idle
            || from == PlayerState.Dodge
            || from == PlayerState.WeaponAiming
            || from == PlayerState.MeleeAttack
            || from == PlayerState.Hacking;
    }

    private bool CanEnterDodge(PlayerState from)
    {
        return !IsDodgeLocked() && from != PlayerState.Dead;
    }

    private bool CanEnterWeaponAiming(PlayerState from)
    {
        return !IsWeaponThrowLocked() && from == PlayerState.Idle;
    }

    private bool CanEnterWeaponThrowing(PlayerState from)
    {
        return !IsWeaponThrowLocked() && from == PlayerState.WeaponAiming;
    }

    private static bool CanEnterWeaponReceiving(PlayerState from)
    {
        return from == PlayerState.WeaponThrowing || from == PlayerState.Dodge;
    }

    private static bool CanEnterHacking(PlayerState from)
    {
        return from == PlayerState.WeaponReceiving || from == PlayerState.Dodge;
    }

    private bool CanEnterMeleeAttack(PlayerState from)
    {
        return !IsMeleeAttackLocked() && (from == PlayerState.Idle || from == PlayerState.MeleeAttack);
    }

    private bool IsMeleeAttackLocked()
    {
        return controlLock != null && controlLock.IsMeleeAttackLocked;
    }

    private bool IsWeaponThrowLocked()
    {
        return controlLock != null && controlLock.IsWeaponThrowLocked;
    }

    private bool IsDodgeLocked()
    {
        return controlLock != null && controlLock.IsDodgeLocked;
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
