using System;
using UnityEngine;

public enum WeaponVisualState
{
    Orbit,
    AimingMove,
    AimingCharged,
    FlyingOut,
    Returning,
    MeleeMove,
    HiddenBySlash,
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

public class PlayerWeaponVisualFSM : MonoBehaviour
{
    [Header("References")]
    // 이 도깨비 환도를 소유한 플레이어 루트다. 비워두면 transform.root에서 자동으로 찾는다.
    [SerializeField] private Transform playerRoot;
    // 검 본체를 숨길 때 끌 시각 자식 오브젝트다. 자기 자신을 넣으면 스크립트까지 꺼지므로 사용하지 않는다.
    [SerializeField] private GameObject visualRoot;
    // 검 전용 애니메이터다. AnimState int와 RestartAnimation trigger를 PlayerAnim과 같은 방식으로 사용한다.
    [SerializeField] private Animator animator;
    // 정렬 순서 조정과 자동 참조 검색에 사용할 검 스프라이트 렌더러다.
    [SerializeField] private SpriteRenderer weaponRenderer;

    [Header("Orbit")]
    // 평소 검이 플레이어 주변에서 도는 기준 위치다. x는 좌우, y는 높이다.
    [SerializeField] private Vector2 orbitCenterOffset = new(-0.22f, 0.48f);
    // 검이 기준 위치 주변을 도는 반지름이다.
    [SerializeField] private float orbitRadius = 0.12f;
    // 초당 몇 도 회전할지 정한다. 값이 클수록 검이 빠르게 돈다.
    [SerializeField] private float orbitDegreesPerSecond = 65f;
    // 이동 중 검이 이동 방향 반대로 얼마나 뒤처져 보일지 정한다.
    [SerializeField] private float moveLagDistance = 0.12f;
    // 기본 부유 상태에서 좌우로 기울어지는 각도 폭이다.
    [SerializeField] private float floatTiltAmount = 5f;
    // 기본 부유 상태에서 좌우 기울어짐이 반복되는 속도다.
    [SerializeField] private float floatTiltSpeed = 2.4f;

    [Header("Aiming")]
    // WeaponAiming 상태에서 검이 마우스 반대 방향으로 얼마나 물러나 장전 자세를 잡을지 정한다.
    [SerializeField] private float aimingPullBackDistance = 0.48f;
    // WeaponAiming 상태에서 마우스 방향의 수직 방향으로 살짝 밀어주는 값이다.
    [SerializeField] private float aimingSideOffset = 0.08f;
    // 최소 장전 시간이 끝났을 때 한 번 재생할 충전 완료 VFX다.
    [SerializeField] private VfxId aimingChargedVfxId = VfxId.None;

    [Header("Melee")]
    // 근접 공격 시작 시 검이 이동할 공격 위치다. x는 공격 방향 앞쪽, y는 공격 방향의 수직 오프셋이다.
    [SerializeField] private Vector2 meleeMoveOffset = new(0.55f, 0f);
    // 근접 공격 위치로 이동할 때 한 번 재생할 검 반짝임 VFX다.
    [SerializeField] private VfxId meleeMoveVfxId = VfxId.None;
    // 참격 이펙트가 켜진 동안 검 본체를 숨길지 정한다.
    [SerializeField] private bool hideVisualDuringSlash = true;

    [Header("Throw")]
    // 투척 시작 순간에 한 번 재생할 VFX다. 실제 투척 판정은 ThrownWeapon이 담당한다.
    [SerializeField] private VfxId throwStartVfxId = VfxId.None;
    // 투척체 위치를 따라갈 때 이 값 이상 멀면 보간하지 않고 즉시 붙인다.
    [SerializeField] private float thrownSnapDistance = 1.5f;

    [Header("Visibility VFX")]
    // 검 본체가 숨겨지는 순간 호출할 전역 VFX ID다.
    [SerializeField] private VfxId vanishVfxId = VfxId.WeaponVanish;
    // 검 본체가 다시 보이는 순간 호출할 전역 VFX ID다.
    [SerializeField] private VfxId reappearVfxId = VfxId.WeaponReappear;

    [Header("Motion")]
    [SerializeField] private float followSharpness = 18f; // 검 위치가 목표점을 따라가는 속도.
    [SerializeField] private float fastFollowSharpness = 55f; // 조준/근접처럼 순간 이동감이 필요한 상태의 추적 속도.
    [SerializeField] private float rotateSharpness = 24f; // 검 회전이 목표 각도를 따라가는 속도.
    // 이미지가 왼쪽을 기준으로 만들어졌을 때 보정하는 각도다.
    [SerializeField] private float baseAngleOffset = 180f;
    // WeaponAiming 상태에서 마우스 반대 방향 기준으로 검을 얼마나 수직 보정할지 정한다.
    [SerializeField] private float aimingPerpendicularAngle = 90f;

    public WeaponVisualState CurrentVisualState { get; private set; } = WeaponVisualState.Orbit;

    // 입력 이동값을 읽어서 이동 중 부유 검의 뒤처짐을 만든다.
    private PlayerInput input;
    // 마우스 기준 조준 방향을 읽어서 조준/근접/투척 연출에 사용한다.
    private PlayerAim aim;
    // 현재 플레이어 메인 상태를 읽어서 검 비주얼 상태를 결정한다.
    private PlayerFSMManager fsm;
    // 현재 캐릭터 표시 방향을 읽어서 기본 부유 위치와 정렬 순서를 정한다.
    private PlayerAnim playerAnim;
    // 투척 조준 시간, 투척체 위치, 회수 상태를 읽는다. 판정 처리는 이 스크립트가 하지 않는다.
    private PlayerWeaponThrow weaponThrow;
    // 근접 공격 방향과 진행률을 읽어서 검을 공격 위치로 보낸다.
    private PlayerMeleeAttack meleeAttack;
    // 참격 이펙트가 실제로 켜져 있는지 읽어서 검 본체를 숨긴다.
    private PlayerMeleeSlashEffect slashEffect;

    private Vector3 positionVelocity;
    private bool wasVisualVisible;
    private float orbitAngle;
    private WeaponAnimState? currentAnimState;
    private WeaponVisualState previousVisualState;

    private static readonly int AnimStateHash = Animator.StringToHash("AnimState");
    private static readonly int RestartAnimationHash = Animator.StringToHash("RestartAnimation");

    private void Awake()
    {
        ResolveReferences();
        if (!ValidateReferences())
        {
            enabled = false;
            return;
        }

        orbitAngle = UnityEngine.Random.Range(0f, 360f);
        wasVisualVisible = IsWeaponVisible();
        previousVisualState = CurrentVisualState;
        EnterVisualState(WeaponVisualState.Orbit);
    }

    private void LateUpdate()
    {
        WeaponVisualState nextState = ResolveVisualState();
        if (nextState != CurrentVisualState)
        {
            EnterVisualState(nextState);
        }

        UpdateVisibilityForState();

        if (!IsWeaponVisible())
        {
            return;
        }

        ApplyMotionForState();
        UpdateSorting();
    }

    private void ResolveReferences()
    {
        if (playerRoot == null)
        {
            playerRoot = transform.root;
        }

        if (weaponRenderer == null)
        {
            weaponRenderer = GetComponentInChildren<SpriteRenderer>(true);
        }

        if (visualRoot == null && weaponRenderer != null && weaponRenderer.gameObject != gameObject)
        {
            visualRoot = weaponRenderer.gameObject;
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>(true);
        }

        input = playerRoot != null ? playerRoot.GetComponent<PlayerInput>() : null;
        aim = playerRoot != null ? playerRoot.GetComponent<PlayerAim>() : null;
        fsm = playerRoot != null ? playerRoot.GetComponent<PlayerFSMManager>() : null;
        playerAnim = playerRoot != null ? playerRoot.GetComponent<PlayerAnim>() : null;
        weaponThrow = playerRoot != null ? playerRoot.GetComponent<PlayerWeaponThrow>() : null;
        meleeAttack = playerRoot != null ? playerRoot.GetComponent<PlayerMeleeAttack>() : null;
        slashEffect = playerRoot != null ? playerRoot.GetComponent<PlayerMeleeSlashEffect>() : null;
    }

    private bool ValidateReferences()
    {
        if (playerRoot != null && input != null && aim != null && fsm != null && playerAnim != null && weaponThrow != null)
        {
            return true;
        }

        Debug.LogError($"{nameof(PlayerWeaponVisualFSM)} on {name} could not find required player references.", this);
        return false;
    }

    private WeaponVisualState ResolveVisualState()
    {
        // 플레이어 FSM은 큰 행동 흐름만 말해준다. 검은 그 안에서 실제 연출 단계로 다시 쪼갠다.
        return fsm.CurrentState switch
        {
            PlayerState.WeaponAiming => weaponThrow.IsMinAimHoldComplete
                ? WeaponVisualState.AimingCharged
                : WeaponVisualState.AimingMove,
            PlayerState.WeaponThrowing => ResolveThrownState(),
            PlayerState.WeaponReceiving => ResolveThrownState(),
            PlayerState.MeleeAttack => ResolveMeleeState(),
            _ => WeaponVisualState.Orbit,
        };
    }

    private WeaponVisualState ResolveThrownState()
    {
        ThrownWeapon thrownWeapon = weaponThrow.ActiveThrownWeapon;
        if (thrownWeapon == null)
        {
            return WeaponVisualState.Orbit;
        }

        return thrownWeapon.IsReturning ? WeaponVisualState.Returning : WeaponVisualState.FlyingOut;
    }

    private WeaponVisualState ResolveMeleeState()
    {
        if (hideVisualDuringSlash && slashEffect != null && slashEffect.IsSlashVisible)
        {
            return WeaponVisualState.HiddenBySlash;
        }

        return WeaponVisualState.MeleeMove;
    }

    private void EnterVisualState(WeaponVisualState nextState)
    {
        previousVisualState = CurrentVisualState;
        CurrentVisualState = nextState;
        positionVelocity = Vector3.zero;

        PlayAnimation(ToAnimState(nextState));
        PlayEnterVfx(nextState);

        if (nextState == WeaponVisualState.Orbit && previousVisualState != WeaponVisualState.Orbit)
        {
            // 다른 연출에서 돌아올 때 궤도 각도를 현재 위치 기준으로 재계산해서 복귀가 튀지 않게 한다.
            orbitAngle = ResolveCurrentOrbitAngle();
        }
    }

    private void PlayEnterVfx(WeaponVisualState state)
    {
        // 상태 진입 순간에만 호출해야 하는 이펙트를 이곳에 모아둔다.
        // 위치/회전은 현재 검 Transform을 사용하므로, 실제 프리팹 배치는 VfxManager 쪽 테이블에서만 관리하면 된다.
        VfxId vfxId = state switch
        {
            WeaponVisualState.AimingCharged => aimingChargedVfxId,
            WeaponVisualState.FlyingOut => throwStartVfxId,
            WeaponVisualState.MeleeMove => meleeMoveVfxId,
            _ => VfxId.None,
        };

        if (vfxId != VfxId.None)
        {
            VfxManager.TryPlay(vfxId, transform.position, transform.rotation);
        }
    }

    private void UpdateVisibilityForState()
    {
        bool shouldShow = CurrentVisualState != WeaponVisualState.HiddenBySlash;
        if (IsWeaponVisible() == shouldShow)
        {
            return;
        }

        SetWeaponVisible(shouldShow);
        PlayVisibilityEffect(shouldShow);
    }

    private void ApplyMotionForState()
    {
        Vector3 targetPosition = ResolveTargetPosition();
        float targetAngle = ResolveTargetAngle();
        float sharpness = ResolveFollowSharpness();

        if (CurrentVisualState is WeaponVisualState.FlyingOut or WeaponVisualState.Returning
            && Vector2.Distance(transform.position, targetPosition) >= thrownSnapDistance)
        {
            // 판정용 투척체와 시각 검이 너무 벌어지면 한 번 붙여서 충돌 위치와 연출 위치가 어긋나지 않게 한다.
            transform.position = targetPosition;
            positionVelocity = Vector3.zero;
        }
        else
        {
            transform.position = Vector3.SmoothDamp(
                transform.position,
                targetPosition,
                ref positionVelocity,
                1f / Mathf.Max(1f, sharpness));
        }

        Quaternion targetRotation = Quaternion.Euler(0f, 0f, targetAngle);
        transform.rotation = Quaternion.Lerp(
            transform.rotation,
            targetRotation,
            1f - Mathf.Exp(-rotateSharpness * Time.deltaTime));
    }

    private Vector3 ResolveTargetPosition()
    {
        return CurrentVisualState switch
        {
            WeaponVisualState.AimingMove => ResolveAimingPosition(),
            WeaponVisualState.AimingCharged => ResolveAimingPosition(),
            WeaponVisualState.FlyingOut => ResolveThrownPosition(),
            WeaponVisualState.Returning => ResolveThrownPosition(),
            WeaponVisualState.MeleeMove => ResolveMeleePosition(),
            _ => ResolveOrbitPosition(),
        };
    }

    private float ResolveTargetAngle()
    {
        return CurrentVisualState switch
        {
            WeaponVisualState.AimingMove => ResolveAimingAngle(),
            WeaponVisualState.AimingCharged => ResolveAimingAngle(),
            WeaponVisualState.FlyingOut => ResolveThrownAngle(),
            WeaponVisualState.Returning => ResolveThrownAngle(),
            WeaponVisualState.MeleeMove => ResolveMeleeAngle(),
            _ => ResolveOrbitAngle(),
        };
    }

    private float ResolveFollowSharpness()
    {
        return CurrentVisualState is WeaponVisualState.AimingMove or WeaponVisualState.AimingCharged or WeaponVisualState.MeleeMove
            ? fastFollowSharpness
            : followSharpness;
    }

    private Vector3 ResolveOrbitPosition()
    {
        orbitAngle += orbitDegreesPerSecond * Time.deltaTime;
        if (orbitAngle >= 360f)
        {
            orbitAngle -= 360f;
        }

        Vector2 anchor = ResolveOrbitAnchorOffset();
        Vector2 orbitOffset = AngleToVector(orbitAngle) * orbitRadius;
        Vector2 localPosition = anchor + orbitOffset;

        if (input.Move.sqrMagnitude > 0.0001f && fsm.CanMoveInMainState())
        {
            localPosition -= input.Move.normalized * moveLagDistance;
        }

        return (Vector2)playerRoot.position + localPosition;
    }

    private Vector2 ResolveOrbitAnchorOffset()
    {
        PlayerSide8 displaySide = playerAnim != null ? playerAnim.CurrentDisplaySide : PlayerSide8.Down;
        Vector2 offset = orbitCenterOffset;

        if (IsLeftSide(displaySide))
        {
            offset.x = -Mathf.Abs(offset.x);
        }
        else if (IsRightSide(displaySide))
        {
            offset.x = Mathf.Abs(offset.x);
        }

        return offset;
    }

    private Vector3 ResolveAimingPosition()
    {
        Vector2 aimDirection = ResolveAimDirection();
        Vector2 perpendicular = new(-aimDirection.y, aimDirection.x);
        Vector2 localPosition = -aimDirection * aimingPullBackDistance
            + perpendicular * aimingSideOffset
            + Vector2.up * orbitCenterOffset.y;

        return (Vector2)playerRoot.position + localPosition;
    }

    private Vector3 ResolveThrownPosition()
    {
        ThrownWeapon thrownWeapon = weaponThrow.ActiveThrownWeapon;
        return thrownWeapon != null ? thrownWeapon.transform.position : ResolveOrbitPosition();
    }

    private Vector3 ResolveMeleePosition()
    {
        Vector2 attackDirection = meleeAttack != null
            ? NormalizeOrDefault(meleeAttack.CurrentAttackDirection, ResolveAimDirection())
            : ResolveAimDirection();
        Vector2 perpendicular = new(-attackDirection.y, attackDirection.x);
        Vector2 localPosition = attackDirection * meleeMoveOffset.x + perpendicular * meleeMoveOffset.y;

        return (Vector2)playerRoot.position + localPosition;
    }

    private float ResolveOrbitAngle()
    {
        float sideSign = ResolveDisplaySideSign();
        float tilt = Mathf.Sin(Time.time * floatTiltSpeed) * floatTiltAmount;
        return 90f * sideSign + baseAngleOffset + tilt;
    }

    private float ResolveAimingAngle()
    {
        Vector2 aimDirection = ResolveAimDirection();
        return DirectionToAngle(-aimDirection) + aimingPerpendicularAngle + baseAngleOffset;
    }

    private float ResolveThrownAngle()
    {
        ThrownWeapon thrownWeapon = weaponThrow.ActiveThrownWeapon;
        if (thrownWeapon != null)
        {
            return thrownWeapon.transform.eulerAngles.z;
        }

        return DirectionToAngle(ResolveAimDirection()) + baseAngleOffset;
    }

    private float ResolveMeleeAngle()
    {
        Vector2 attackDirection = meleeAttack != null
            ? NormalizeOrDefault(meleeAttack.CurrentAttackDirection, ResolveAimDirection())
            : ResolveAimDirection();

        return DirectionToAngle(attackDirection) + baseAngleOffset;
    }

    private float ResolveCurrentOrbitAngle()
    {
        Vector2 anchorWorldPosition = (Vector2)playerRoot.position + ResolveOrbitAnchorOffset();
        Vector2 fromAnchor = (Vector2)transform.position - anchorWorldPosition;
        if (fromAnchor.sqrMagnitude <= 0.0001f)
        {
            return orbitAngle;
        }

        return DirectionToAngle(fromAnchor);
    }

    private void PlayAnimation(WeaponAnimState animState)
    {
        if (animator == null || currentAnimState == animState)
        {
            return;
        }

        animator.SetInteger(AnimStateHash, (int)animState);
        animator.SetTrigger(RestartAnimationHash);
        currentAnimState = animState;
    }

    private bool IsWeaponVisible()
    {
        if (visualRoot != null && visualRoot != gameObject)
        {
            return visualRoot.activeSelf;
        }

        return weaponRenderer == null || weaponRenderer.enabled;
    }

    private void SetWeaponVisible(bool visible)
    {
        if (visualRoot != null && visualRoot != gameObject)
        {
            visualRoot.SetActive(visible);
        }

        if (weaponRenderer != null)
        {
            weaponRenderer.enabled = visible;
        }
    }

    private void PlayVisibilityEffect(bool appearing)
    {
        if (wasVisualVisible == appearing)
        {
            return;
        }

        wasVisualVisible = appearing;
        VfxId vfxId = appearing ? reappearVfxId : vanishVfxId;
        if (vfxId != VfxId.None)
        {
            VfxManager.TryPlay(vfxId, transform.position, transform.rotation);
        }
    }

    private Vector2 ResolveAimDirection()
    {
        Vector2 direction = aim != null ? aim.AimDirection : Vector2.right;
        return NormalizeOrDefault(direction, Vector2.right);
    }

    private float ResolveDisplaySideSign()
    {
        PlayerSide8 displaySide = playerAnim != null ? playerAnim.CurrentDisplaySide : PlayerSide8.Down;
        return IsLeftSide(displaySide) ? -1f : 1f;
    }

    private void UpdateSorting()
    {
        if (weaponRenderer == null || playerAnim == null)
        {
            return;
        }

        PlayerSide8 displaySide = playerAnim.CurrentDisplaySide;
        weaponRenderer.sortingOrder = displaySide is PlayerSide8.Up or PlayerSide8.UpLeft or PlayerSide8.UpRight ? -1 : 1;
    }

    private static WeaponAnimState ToAnimState(WeaponVisualState visualState)
    {
        return visualState switch
        {
            WeaponVisualState.AimingMove => WeaponAnimState.AimingMove,
            WeaponVisualState.AimingCharged => WeaponAnimState.AimingCharged,
            WeaponVisualState.FlyingOut => WeaponAnimState.FlyingOut,
            WeaponVisualState.Returning => WeaponAnimState.Returning,
            WeaponVisualState.MeleeMove => WeaponAnimState.MeleeMove,
            WeaponVisualState.HiddenBySlash => WeaponAnimState.HiddenBySlash,
            _ => WeaponAnimState.Orbit,
        };
    }

    private static bool IsLeftSide(PlayerSide8 side)
    {
        return side is PlayerSide8.Left or PlayerSide8.DownLeft or PlayerSide8.UpLeft;
    }

    private static bool IsRightSide(PlayerSide8 side)
    {
        return side is PlayerSide8.Right or PlayerSide8.DownRight or PlayerSide8.UpRight;
    }

    private static Vector2 AngleToVector(float angleDegrees)
    {
        float radians = angleDegrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
    }

    private static float DirectionToAngle(Vector2 direction)
    {
        Vector2 normalizedDirection = NormalizeOrDefault(direction, Vector2.right);
        return Mathf.Atan2(normalizedDirection.y, normalizedDirection.x) * Mathf.Rad2Deg;
    }

    private static Vector2 NormalizeOrDefault(Vector2 direction, Vector2 fallback)
    {
        return direction.sqrMagnitude > 0.0001f ? direction.normalized : fallback.normalized;
    }
}
