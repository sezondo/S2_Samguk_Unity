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
    MeleeMove = 5,
    HiddenBySlash = 6,
}

[RequireComponent(typeof(PlayerWeaponContext))]
public class PlayerWeaponVisualFSM : MonoBehaviour
{
    [Header("Context")]
    // PlayerWeapon 계열 참조는 이 Context에서만 꺼내 쓴다.
    // 자동 탐색하지 않으므로 Unity 인스펙터에서 직접 연결한다.
    [SerializeField] private PlayerWeaponContext weaponContext;

    [Header("Visual Data")]
    // 검 비주얼 튜닝 데이터다. 연결되어 있으면 아래 레거시 인스펙터 값보다 이 데이터를 우선 사용한다.
    // 나중에는 이 데이터 에셋을 기준으로 무기별/캐릭터별 검 연출을 조정한다.
    [SerializeField] private PlayerWeaponVisualData visualData;

    [Header("Legacy Fallback Settings")]
    // 아래 값들은 visualData를 아직 연결하지 않은 기존 씬을 위한 fallback이다.
    // 새 작업에서는 PlayerWeaponVisualData 에셋을 만들어 그쪽에서 수치를 조정한다.
    [SerializeField] private Vector2 weaponAnchorOffset = new(0f, 0.48f);

    [Header("Orbit")]
    // 아래 Orbit 값들은 PlayerWeaponVisualMotion이 없을 때 런타임에 넘겨주는 초기 설정이다.
    // 평소 검이 플레이어 뒤통수/등 뒤 기준으로 떠다니는 위치와 작은 흔들림을 조정한다.
    // x는 등 뒤 기준 좌우 보정, y는 플레이어가 바라보는 방향의 반대쪽으로 떨어지는 거리다.
    [SerializeField] private Vector2 orbitCenterOffset = new(-0.22f, 0.48f);
    // 기준 위치 주변에서 작게 흔들리는 폭이다.
    [SerializeField] private float orbitRadius = 0.12f;
    // 작은 흔들림의 속도다. 기존 데이터 호환을 위해 이름은 유지한다.
    [SerializeField] private float orbitDegreesPerSecond = 65f;
    // 이동 중 검이 이동 방향 반대로 살짝 밀리는 거리다. 이동감/부유감을 만들기 위한 값이다.
    [SerializeField] private float moveLagDistance = 0.12f;
    // 평소 검이 좌우로 기울어지는 각도 폭이다.
    [SerializeField] private float floatTiltAmount = 5f;
    // 평소 좌우 기울어짐이 반복되는 속도다.
    [SerializeField] private float floatTiltSpeed = 2.4f;

    [Header("Aiming")]
    // 아래 Aiming 값들은 우클릭 조준 상태에서 검이 어디에 놓이고 어떤 이펙트를 낼지 정한다.
    // 조준 방향의 반대쪽으로 검을 얼마나 당겨서 장전 자세처럼 보이게 할지 정한다.
    [SerializeField] private float aimingPullBackDistance = 0.48f;
    // 조준선과 완전히 겹치지 않게 수직 방향으로 살짝 밀어주는 거리다.
    [SerializeField] private float aimingSideOffset = 0.08f;
    // 최소 조준 시간이 채워져 AimingCharged 상태로 들어갈 때 한 번 재생할 VFX ID다.
    [SerializeField] private VfxId aimingChargedVfxId = VfxId.None;

    [Header("Melee")]
    // 아래 Melee 값은 PlayerMeleeAttackData.hitboxOffset 기준에서 검 비주얼만 살짝 보정한다.
    // x는 공격 방향 앞/뒤, y는 공격 방향 기준 좌/우 보정이다.
    [FormerlySerializedAs("meleeMoveOffset")]
    [SerializeField] private Vector2 meleeVisualOffset = Vector2.zero;
    // MeleeMove 상태에 들어갈 때 한 번 재생할 검 반짝임/이동 시작 VFX ID다.
    [SerializeField] private VfxId meleeMoveVfxId = VfxId.None;
    // 참격 이펙트가 켜진 동안 검 본체를 숨길지 정한다.
    [SerializeField] private bool hideVisualDuringSlash = true;

    [Header("Throw")]
    // 아래 Throw 값들은 투척 판정체와 보이는 검 본체를 맞춰 보이게 하는 데 쓰인다.
    // FlyingOut 상태에 들어갈 때 한 번 재생할 투척 시작 VFX ID다.
    [SerializeField] private VfxId throwStartVfxId = VfxId.None;
    // 보이는 검과 판정용 투척체가 이 거리 이상 벌어지면 보간하지 않고 즉시 투척체 위치로 붙인다.
    [SerializeField] private float thrownSnapDistance = 1.5f;

    [Header("Visibility VFX")]
    // 검 본체가 숨겨지거나 다시 나타날 때 쓰는 VFX ID다.
    // 예: 참격 이펙트가 켜져 HiddenBySlash로 들어가면 vanishVfxId, 다시 보이면 reappearVfxId를 재생한다.
    [SerializeField] private VfxId vanishVfxId = VfxId.WeaponVanish;
    [SerializeField] private VfxId reappearVfxId = VfxId.WeaponReappear;

    [Header("Motion")]
    // 아래 Motion 값들은 위치/회전 보간 속도와 스프라이트 방향 보정을 조정한다.
    // Orbit, FlyingOut, Returning 같은 일반 상태에서 목표 위치를 따라가는 속도다.
    [SerializeField] private float followSharpness = 18f;
    // Aiming처럼 즉각 반응해야 하는 상태에서 목표 위치를 따라가는 속도다.
    // Melee 이동은 PlayerMeleeAttackData.hitboxStartTime 기준 타임라인을 사용하므로 이 값을 쓰지 않는다.
    [SerializeField] private float fastFollowSharpness = 55f;
    // 검 회전이 목표 각도를 따라가는 속도다.
    [SerializeField] private float rotateSharpness = 24f;
    // 검 이미지의 기본 칼날 방향을 공격 방향에 맞추기 위한 보정 각도다.
    // 오른쪽이 손잡이, 왼쪽이 칼날인 스프라이트면 보통 180이 맞다.
    [SerializeField] private float baseAngleOffset = 180f;
    // 조준 중 검을 조준 반대 방향으로 눕힐 때 추가로 더하는 회전 보정값이다.
    [SerializeField] private float aimingPerpendicularAngle = 90f;

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

        if (visualData != null)
        {
            motion.ApplyData(visualData);
        }
        else
        {
            motion.ApplyLegacySettings(
                weaponAnchorOffset,
                orbitCenterOffset,
                orbitRadius,
                orbitDegreesPerSecond,
                moveLagDistance,
                floatTiltAmount,
                floatTiltSpeed,
                aimingPullBackDistance,
                aimingSideOffset,
                meleeVisualOffset,
                thrownSnapDistance,
                followSharpness,
                fastFollowSharpness,
                rotateSharpness,
                baseAngleOffset,
                aimingPerpendicularAngle);
        }

        if (visualData != null)
        {
            presentation.ApplyData(visualData);
        }
        else
        {
            presentation.ApplyLegacySettings(
                aimingChargedVfxId,
                meleeMoveVfxId,
                throwStartVfxId,
                vanishVfxId,
                reappearVfxId);
        }

        motion.Initialize(weaponContext);
        presentation.Initialize(weaponContext);
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

        return playerContext.Fsm.CurrentState switch
        {
            PlayerState.WeaponAiming => playerContext.WeaponThrow.IsMinAimHoldComplete
                ? WeaponVisualState.AimingCharged //검 투척 준비 완료
                : WeaponVisualState.AimingMove, // 검투척 준비중
            PlayerState.WeaponThrowing => ResolveThrownState(),
            PlayerState.WeaponReceiving => ResolveThrownState(),
            PlayerState.MeleeAttack => ResolveMeleeState(),
            _ => WeaponVisualState.Orbit,
        };
    }

    private WeaponVisualState ResolveThrownState()
    {
        ThrownWeapon thrownWeapon = playerContext.WeaponThrow.ActiveThrownWeapon;
        if (thrownWeapon == null)
        {
            return WeaponVisualState.Orbit;
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
        if (currentState is PlayerState.WeaponAiming or PlayerState.WeaponThrowing or PlayerState.WeaponReceiving or PlayerState.Dead)
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
