using System;
using UnityEngine;
using UnityEngine.Serialization;

public enum WeaponVisualState
{
    Orbit, // 평소에 플레이어 주변을 떠다님
    AimingMove, // 투척 조준 중, 아직 최소 조준 시간이 안 됨
    AimingCharged, // 투척 조준 완료됨
    FlyingOut, // 검이 날아가는 투척체를 따라가는 중
    Returning, // 검이 돌아오는 투척체를 따라가는 중
    EmbeddedForHack, // 해킹 대상에 박혀 전자 부적 삽입 대기/진행 중
    MeleeMove, // 근접 공격 위치로 이동
    HiddenBySlash, // 참격 이펙트가 켜져서 검 본체 숨김
}

public enum WeaponAnimState
{
    Orbit = 0,
    AimingMove = 1,
    AimingCharged = 2,
    FlyingOut = 3,
    Returning = 4,
    EmbeddedForHack = 5,
    MeleeMove = 6,
    HiddenBySlash = 7,
}

[RequireComponent(typeof(PlayerWeaponContext))]
public class PlayerWeaponVisualFSM : MonoBehaviour
{
    [Header("Context")]
    // PlayerWeapon 계열 참조는 이 Context에서만 꺼내 쓴다.
    // 자동 탐색하지 않으므로 Unity 인스펙터에서 직접 연결한다.
    [SerializeField] private PlayerWeaponContext weaponContext;

    [Header("Visual Data")]
    // 검 비주얼 튜닝 데이터다. 연결되지 않으면 비주얼 FSM을 실행하지 않는다.
    [SerializeField] private PlayerWeaponVisualData visualData;

    [Header("Melee")]
    // 참격 이펙트가 켜진 동안 검 본체를 숨길지 정한다.
    [SerializeField] private bool hideVisualDuringSlash = true;

    public WeaponVisualState CurrentVisualState { get; private set; } = WeaponVisualState.Orbit;
    public event Action<WeaponVisualState, WeaponVisualState> OnVisualStateChanged;

    private PlayerContext playerContext;
    private PlayerWeaponVisualMotion motion;
    private PlayerWeaponVisualPresentation presentation;

    private void Awake()
    {
        ResolveContexts();
        if (!ValidateContexts())
        {
            enabled = false;
            return;
        }

        if (!HasValidData())
        {
            enabled = false;
            return;
        }

        ResolveWorkerComponents();
        SubscribeWorkers();
        EnterVisualState(WeaponVisualState.Orbit);
    }

    private void OnDestroy()
    {
        UnsubscribeWorkers();
    }

    private void LateUpdate()
    {
        WeaponVisualState nextState = ResolveVisualState();
        if (nextState != CurrentVisualState)
        {
            EnterVisualState(nextState);
        }

        // 표시/숨김을 먼저 처리한다.
        // HiddenBySlash 상태에서도 Motion은 계속 돌려서, 다시 보일 때 이미 자연스러운 위치에 있게 한다.
        presentation.UpdateVisibility(CurrentVisualState);
        if (!presentation.IsWeaponVisible)
        {
            motion.ApplyMotion(CurrentVisualState);
            return;
        }

        motion.ApplyMotion(CurrentVisualState);
        presentation.UpdateSorting();
    }

    private void ResolveContexts()
    {
        playerContext = weaponContext != null ? weaponContext.Player : null;
    }

    private bool ValidateContexts()
    {
        if (weaponContext != null && weaponContext.HasWeaponVisualRequiredReferences())
        {
            return true;
        }

        Debug.LogError($"{nameof(PlayerWeaponVisualFSM)} on {name} could not find required player context references.", this);
        return false;
    }

    private void ResolveWorkerComponents()
    {
        motion = weaponContext.Motion;
        presentation = weaponContext.Presentation;

        motion.ApplyData(visualData);
        presentation.ApplyData(visualData);

        motion.Initialize(weaponContext);
        presentation.Initialize(weaponContext);
    }

    private bool HasValidData()
    {
        if (visualData == null)
        {
            Debug.LogError($"{nameof(PlayerWeaponVisualFSM)} on {name} requires {nameof(PlayerWeaponVisualData)}.", this);
            return false;
        }

        bool valid = true;
        valid &= RequireVisualSection(visualData.orbit, nameof(visualData.orbit));
        valid &= RequireVisualSection(visualData.aiming, nameof(visualData.aiming));
        valid &= RequireVisualSection(visualData.melee, nameof(visualData.melee));
        valid &= RequireVisualSection(visualData.throwVisual, nameof(visualData.throwVisual));
        valid &= RequireVisualSection(visualData.presentation, nameof(visualData.presentation));
        valid &= RequireVisualSection(visualData.motion, nameof(visualData.motion));
        return valid;
    }

    private bool RequireVisualSection(object section, string sectionName)
    {
        if (section != null)
        {
            return true;
        }

        Debug.LogError($"{nameof(PlayerWeaponVisualFSM)} on {name} requires {sectionName} in {visualData.name}.", this);
        return false;
    }

    private void SubscribeWorkers()
    {
        OnVisualStateChanged += motion.HandleVisualStateChanged;
        OnVisualStateChanged += presentation.HandleVisualStateChanged;
    }

    private void UnsubscribeWorkers()
    {
        if (motion != null)
        {
            OnVisualStateChanged -= motion.HandleVisualStateChanged;
        }

        if (presentation != null)
        {
            OnVisualStateChanged -= presentation.HandleVisualStateChanged;
        }
    }

    private WeaponVisualState ResolveVisualState()
    {
        // 플레이어 FSM은 큰 행동 흐름만 말해준다. 검은 그 안에서 실제 연출 단계로 다시 쪼갠다.
        if (ShouldHideWeaponVisual())
        {
            return WeaponVisualState.HiddenBySlash;
        }

        // 해킹 대상으로 박힌 검은 플레이어가 이동하거나 Dodge 중이어도 월드 위치에 고정되어야 한다.
        // 플레이어 FSM의 일시 상태보다 투척체의 Embedded 상태를 우선한다.
        if (TryResolveEmbeddedHackState(out WeaponVisualState embeddedState))
        {
            return embeddedState;
        }

        return playerContext.Fsm.CurrentState switch
        {
            PlayerState.WeaponAiming => playerContext.WeaponThrow.IsMinAimHoldComplete
                ? WeaponVisualState.AimingCharged //검 투척 준비 완료
                : WeaponVisualState.AimingMove, // 검투척 준비중
            PlayerState.WeaponThrowing => ResolveThrownState(),
            PlayerState.WeaponReceiving => ResolveThrownState(),
            PlayerState.Hacking => ResolveThrownState(),
            PlayerState.MeleeAttack => ResolveMeleeState(),
            _ => WeaponVisualState.Orbit,
        };
    }

    private bool TryResolveEmbeddedHackState(out WeaponVisualState visualState)
    {
        ThrownWeapon thrownWeapon = playerContext.WeaponThrow.ActiveThrownWeapon;
        if (thrownWeapon != null && thrownWeapon.IsEmbeddedForHack)
        {
            visualState = WeaponVisualState.EmbeddedForHack;
            return true;
        }

        visualState = WeaponVisualState.Orbit;
        return false;
    }

    private WeaponVisualState ResolveThrownState()
    {
        ThrownWeapon thrownWeapon = playerContext.WeaponThrow.ActiveThrownWeapon;
        if (thrownWeapon == null)
        {
            return WeaponVisualState.Orbit;
        }

        if (thrownWeapon.IsEmbeddedForHack)
        {
            return WeaponVisualState.EmbeddedForHack;
        }

        return thrownWeapon.IsReturning ? WeaponVisualState.Returning : WeaponVisualState.FlyingOut;
    }

    private WeaponVisualState ResolveMeleeState()
    {
        return WeaponVisualState.MeleeMove;
    }

    private bool ShouldHideWeaponVisual()
    {
        if (!hideVisualDuringSlash || playerContext.MeleeSlashEffect == null)
        {
            return false;
        }

        PlayerState currentState = playerContext.Fsm.CurrentState;
        if (currentState is PlayerState.WeaponAiming or PlayerState.WeaponThrowing or PlayerState.WeaponReceiving
            or PlayerState.Hacking or PlayerState.Dead)
        {
            return false;
        }

        return playerContext.MeleeSlashEffect.ShouldHideWeaponVisual;
    }

    private void EnterVisualState(WeaponVisualState nextState)
    {
        WeaponVisualState previousState = CurrentVisualState;
        CurrentVisualState = nextState;

        OnVisualStateChanged?.Invoke(previousState, nextState);
    }
}
