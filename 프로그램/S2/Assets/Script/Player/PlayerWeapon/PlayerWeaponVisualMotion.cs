using UnityEngine;
using UnityEngine.Serialization;

// 도깨비 환도 본체의 위치와 회전만 담당한다.
// 상태 판단은 PlayerWeaponVisualFSM, 애니메이션/VFX/표시는 PlayerWeaponVisualPresentation이 처리한다.
public class PlayerWeaponVisualMotion : MonoBehaviour
{
    private const float DefaultBackHoverDistance = 0.35f;

    [Header("Anchor")]
    // 기준 Transform에서 한 번 더 밀어주는 보정값이다.
    // PlayerWeaponContext.weaponAnchorRoot가 연결되어 있으면 그 빈 오브젝트 위치를 기준으로 쓰고,
    // 연결되어 있지 않으면 PlayerWeaponContext.PlayerRoot.position을 기준으로 fallback 처리한다.
    [SerializeField] private Vector2 weaponAnchorOffset = new(0f, 0.48f);

    [Header("Orbit")]
    // weaponAnchorOffset 기준으로 평소 검이 머무를 위치다.
    // x는 등 뒤 기준 좌우 보정, y는 플레이어가 바라보는 방향의 반대쪽으로 떨어지는 거리다.
    // y가 0이면 기존 데이터 호환을 위해 DefaultBackHoverDistance를 사용한다.
    [SerializeField] private Vector2 orbitCenterOffset = Vector2.zero;
    // 기준 위치 주변에서 작게 흔들리는 폭이다.
    [SerializeField] private float orbitRadius = 0.12f;
    // 작은 흔들림의 속도다. 기존 데이터 호환을 위해 이름은 유지한다.
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

    [Header("Melee")]
    // PlayerMeleeAttackData.hitboxOffset 기준에서 검 비주얼만 살짝 보정하는 값이다.
    // x는 공격 방향 앞/뒤, y는 공격 방향 기준 좌/우 보정이다.
    [FormerlySerializedAs("meleeMoveOffset")]
    [SerializeField] private Vector2 meleeVisualOffset = Vector2.zero;

    [Header("Throw")]
    // 투척체 위치를 따라갈 때 이 값 이상 멀면 보간하지 않고 즉시 붙인다.
    [SerializeField] private float thrownSnapDistance = 1.5f;

    [Header("Motion")]
    [SerializeField] private float followSharpness = 18f; // 검 위치가 목표점을 따라가는 속도.
    [SerializeField] private float fastFollowSharpness = 55f; // 조준/근접처럼 순간 이동감이 필요한 상태의 추적 속도.
    [SerializeField] private float rotateSharpness = 24f; // 검 회전이 목표 각도를 따라가는 속도.
    // 검 이미지의 기본 칼날 방향을 공격 방향에 맞추기 위한 보정 각도다.
    // 오른쪽이 손잡이, 왼쪽이 칼날인 스프라이트면 보통 180이 맞다.
    [SerializeField] private float baseAngleOffset = 180f;
    // WeaponAiming 상태에서 마우스 반대 방향 기준으로 검을 얼마나 수직 보정할지 정한다.
    [SerializeField] private float aimingPerpendicularAngle = 90f;

    private PlayerWeaponContext context;
    private Vector3 positionVelocity;
    private float orbitFloatPhase;
    private int activeMeleeAttackSequenceId = -1;
    private Vector3 meleeStartPosition;

    public void Initialize(PlayerWeaponContext newContext)
    {
        context = newContext;
        orbitFloatPhase = Random.Range(0f, Mathf.PI * 2f);
    }

    public void ApplyLegacySettings(
        Vector2 newWeaponAnchorOffset,
        Vector2 newOrbitCenterOffset,
        float newOrbitRadius,
        float newOrbitDegreesPerSecond,
        float newMoveLagDistance,
        float newFloatTiltAmount,
        float newFloatTiltSpeed,
        float newAimingPullBackDistance,
        float newAimingSideOffset,
        Vector2 newMeleeMoveOffset,
        float newThrownSnapDistance,
        float newFollowSharpness,
        float newFastFollowSharpness,
        float newRotateSharpness,
        float newBaseAngleOffset,
        float newAimingPerpendicularAngle)
    {
        weaponAnchorOffset = newWeaponAnchorOffset;
        orbitCenterOffset = newOrbitCenterOffset;
        orbitRadius = newOrbitRadius;
        orbitDegreesPerSecond = newOrbitDegreesPerSecond;
        moveLagDistance = newMoveLagDistance;
        floatTiltAmount = newFloatTiltAmount;
        floatTiltSpeed = newFloatTiltSpeed;
        aimingPullBackDistance = newAimingPullBackDistance;
        aimingSideOffset = newAimingSideOffset;
        meleeVisualOffset = newMeleeMoveOffset;
        thrownSnapDistance = newThrownSnapDistance;
        followSharpness = newFollowSharpness;
        fastFollowSharpness = newFastFollowSharpness;
        rotateSharpness = newRotateSharpness;
        baseAngleOffset = newBaseAngleOffset;
        aimingPerpendicularAngle = newAimingPerpendicularAngle;
    }

    public void ApplyData(PlayerWeaponVisualData visualData)
    {
        if (visualData == null)
        {
            return;
        }

        weaponAnchorOffset = visualData.weaponAnchorOffset;

        if (visualData.orbit != null)
        {
            orbitCenterOffset = visualData.orbit.centerOffset;
            orbitRadius = visualData.orbit.radius;
            orbitDegreesPerSecond = visualData.orbit.degreesPerSecond;
            moveLagDistance = visualData.orbit.moveLagDistance;
            floatTiltAmount = visualData.orbit.floatTiltAmount;
            floatTiltSpeed = visualData.orbit.floatTiltSpeed;
        }

        if (visualData.aiming != null)
        {
            aimingPullBackDistance = visualData.aiming.pullBackDistance;
            aimingSideOffset = visualData.aiming.sideOffset;
            aimingPerpendicularAngle = visualData.aiming.perpendicularAngle;
        }

        if (visualData.melee != null)
        {
            meleeVisualOffset = visualData.melee.visualOffset;
        }

        if (visualData.throwVisual != null)
        {
            thrownSnapDistance = visualData.throwVisual.snapDistance;
        }

        if (visualData.motion != null)
        {
            followSharpness = visualData.motion.followSharpness;
            fastFollowSharpness = visualData.motion.fastFollowSharpness;
            rotateSharpness = visualData.motion.rotateSharpness;
            baseAngleOffset = visualData.motion.baseAngleOffset;
        }
    }

    public void HandleVisualStateChanged(WeaponVisualState previousState, WeaponVisualState nextState)
    {
        positionVelocity = Vector3.zero;

        // Orbit은 뒤통수/등 뒤 기준 위치로 자연스럽게 복귀한다.
        // 예전처럼 현재 위치 기준 궤도 각도를 다시 잡지 않는다.
        if (nextState == WeaponVisualState.MeleeMove)
        {
            CaptureMeleeStartPositionIfNeeded(force: true);
        }

        if (nextState != WeaponVisualState.MeleeMove && nextState != WeaponVisualState.HiddenBySlash)
        {
            activeMeleeAttackSequenceId = -1;
        }
    }

    public void ApplyMotion(WeaponVisualState state)
    {
        if (context == null)
        {
            return;
        }

        Vector3 targetPosition = ResolveTargetPosition(state);
        float targetAngle = ResolveTargetAngle(state);
        float sharpness = ResolveFollowSharpness(state);

        if (state == WeaponVisualState.MeleeMove)
        {
            transform.position = ResolveMeleeTimelinePosition(targetPosition);
        }
        else if (state is WeaponVisualState.FlyingOut or WeaponVisualState.Returning
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

    private Vector3 ResolveTargetPosition(WeaponVisualState state)
    {
        return state switch
        {
            WeaponVisualState.AimingMove => ResolveAimingPosition(),
            WeaponVisualState.AimingCharged => ResolveAimingPosition(),
            WeaponVisualState.FlyingOut => ResolveThrownPosition(),
            WeaponVisualState.Returning => ResolveThrownPosition(),
            WeaponVisualState.MeleeMove => ResolveMeleePosition(),
            _ => ResolveOrbitPosition(),
        };
    }

    private float ResolveTargetAngle(WeaponVisualState state)
    {
        return state switch
        {
            WeaponVisualState.AimingMove => ResolveAimingAngle(),
            WeaponVisualState.AimingCharged => ResolveAimingAngle(),
            WeaponVisualState.FlyingOut => ResolveThrownAngle(),
            WeaponVisualState.Returning => ResolveThrownAngle(),
            WeaponVisualState.MeleeMove => ResolveMeleeAngle(),
            _ => ResolveOrbitAngle(),
        };
    }

    private float ResolveFollowSharpness(WeaponVisualState state)
    {
        return state is WeaponVisualState.AimingMove or WeaponVisualState.AimingCharged
            ? fastFollowSharpness
            : followSharpness;
    }

    private Vector3 ResolveOrbitPosition()
    {
        orbitFloatPhase += orbitDegreesPerSecond * Mathf.Deg2Rad * Time.deltaTime;

        Vector2 anchor = ResolveWeaponAnchorPosition();
        // 큰 원형 궤도 대신 기준 위치 근처에서만 작게 흔들리게 한다.
        // x/y 주기를 다르게 둬서 기계적으로 보이지 않는 부유감을 만든다.
        Vector2 floatOffset = new(
            Mathf.Sin(orbitFloatPhase) * orbitRadius,
            Mathf.Sin(orbitFloatPhase * 0.73f + 1.1f) * orbitRadius * 0.45f);
        Vector2 localPosition = anchor + floatOffset;

        if (context.Player.Input.Move.sqrMagnitude > 0.0001f && context.Player.Fsm.CanMoveInMainState())
        {
            localPosition -= context.Player.Input.Move.normalized * moveLagDistance;
        }

        return localPosition;
    }

    private Vector2 ResolveWeaponAnchorPosition()
    {
        Vector2 rootPosition = context.WeaponAnchorRoot != null
            ? context.WeaponAnchorRoot.position
            : context.PlayerRoot.position;

        return rootPosition + weaponAnchorOffset + ResolveOrbitAnchorOffset();
    }

    private Vector2 ResolveOrbitAnchorOffset()
    {
        PlayerSide8 displaySide = context.Player.Anim != null ? context.Player.Anim.CurrentDisplaySide : PlayerSide8.Down;
        Vector2 backDirection = ResolveBackDirection(displaySide);
        Vector2 sideDirection = new(-backDirection.y, backDirection.x);

        // centerOffset.y는 캐릭터 등 뒤 방향 거리로 해석한다.
        // 기존 데이터가 0이어도 옆에 붙은 막대처럼 보이지 않도록 최소 등뒤 거리를 보장한다.
        float backDistance = Mathf.Abs(orbitCenterOffset.y);
        if (backDistance <= 0.0001f)
        {
            backDistance = DefaultBackHoverDistance;
        }

        return backDirection * backDistance + sideDirection * orbitCenterOffset.x;
    }

    private Vector3 ResolveAimingPosition()
    {
        Vector2 aimDirection = ResolveAimDirection();
        Vector2 perpendicular = new(-aimDirection.y, aimDirection.x);
        Vector2 localPosition = -aimDirection * aimingPullBackDistance
            + perpendicular * aimingSideOffset;

        return ResolveWeaponAnchorPosition() + localPosition;
    }

    private Vector3 ResolveThrownPosition()
    {
        ThrownWeapon thrownWeapon = context.Player.WeaponThrow.ActiveThrownWeapon;
        return thrownWeapon != null ? thrownWeapon.transform.position : ResolveOrbitPosition();
    }

    private Vector3 ResolveMeleePosition()
    {
        PlayerMeleeAttack meleeAttack = context.Player.MeleeAttack;
        if (meleeAttack == null || !meleeAttack.TryGetCurrentHitboxCenter(out Vector3 hitboxCenter))
        {
            return ResolveOrbitPosition();
        }

        Vector2 attackDirection = NormalizeOrDefault(meleeAttack.CurrentAttackDirection, ResolveAimDirection());
        Vector2 perpendicular = new(-attackDirection.y, attackDirection.x);
        Vector2 visualOffset = attackDirection * meleeVisualOffset.x + perpendicular * meleeVisualOffset.y;

        return hitboxCenter + (Vector3)visualOffset;
    }

    private Vector3 ResolveMeleeTimelinePosition(Vector3 targetPosition)
    {
        PlayerMeleeAttack meleeAttack = context.Player.MeleeAttack;
        PlayerMeleeAttackData attackData = meleeAttack != null ? meleeAttack.CurrentAttackData : null;
        if (meleeAttack == null || attackData == null)
        {
            return targetPosition;
        }

        CaptureMeleeStartPositionIfNeeded(force: false);

        float impactTime = Mathf.Max(0f, attackData.hitboxStartTime);
        if (impactTime <= 0.0001f)
        {
            return targetPosition;
        }

        // 공격 시작부터 실제 판정 시작 시점까지의 시간을 0~1로 바꿔 검 이동 타임라인으로 사용한다.
        float timeline = Mathf.Clamp01(meleeAttack.AttackElapsedTime / impactTime);
        return Vector3.Lerp(meleeStartPosition, targetPosition, timeline);
    }

    private void CaptureMeleeStartPositionIfNeeded(bool force)
    {
        PlayerMeleeAttack meleeAttack = context?.Player?.MeleeAttack;
        if (meleeAttack == null)
        {
            return;
        }

        int attackSequenceId = meleeAttack.AttackSequenceId;
        if (!force && activeMeleeAttackSequenceId == attackSequenceId)
        {
            return;
        }

        activeMeleeAttackSequenceId = attackSequenceId;
        meleeStartPosition = transform.position;
        positionVelocity = Vector3.zero;
    }

    private float ResolveOrbitAngle()
    {
        float tilt = Mathf.Sin(Time.time * floatTiltSpeed) * floatTiltAmount;
        return ResolveBackMountedAngle() + baseAngleOffset + tilt;
    }

    private float ResolveAimingAngle()
    {
        Vector2 aimDirection = ResolveAimDirection();
        return DirectionToAngle(-aimDirection) + aimingPerpendicularAngle + baseAngleOffset;
    }

    private float ResolveThrownAngle()
    {
        ThrownWeapon thrownWeapon = context.Player.WeaponThrow.ActiveThrownWeapon;
        if (thrownWeapon != null)
        {
            // 투척체의 회전은 진행 방향을 말하고, 비주얼 스프라이트는 칼날이 왼쪽을 본다.
            // baseAngleOffset을 더해서 손잡이가 아니라 칼날이 진행 방향을 보게 한다.
            return thrownWeapon.transform.eulerAngles.z + baseAngleOffset;
        }

        return DirectionToAngle(ResolveAimDirection()) + baseAngleOffset;
    }

    private float ResolveMeleeAngle()
    {
        Vector2 attackDirection = context.Player.MeleeAttack != null
            ? NormalizeOrDefault(context.Player.MeleeAttack.CurrentAttackDirection, ResolveAimDirection())
            : ResolveAimDirection();

        return DirectionToAngle(attackDirection) + baseAngleOffset;
    }

    private Vector2 ResolveAimDirection()
    {
        Vector2 direction = context.Player.Aim != null ? context.Player.Aim.AimDirection : Vector2.right;
        return NormalizeOrDefault(direction, Vector2.right);
    }

    private float ResolveBackMountedAngle()
    {
        PlayerSide8 displaySide = context.Player.Anim != null ? context.Player.Anim.CurrentDisplaySide : PlayerSide8.Down;

        // Orbit 상태에서는 검을 플레이어 뒤통수/등 뒤에 장착된 것처럼 보이게 한다.
        // 8방향을 세밀하게 따라가지 않고, 4방향 축으로 스냅해서 실루엣을 안정시킨다.
        if (displaySide is PlayerSide8.Up or PlayerSide8.UpLeft or PlayerSide8.UpRight
            or PlayerSide8.Down or PlayerSide8.DownLeft or PlayerSide8.DownRight)
        {
            return 0f;
        }

        return IsLeftSide(displaySide) ? 90f : -90f;
    }

    private static Vector2 ResolveBackDirection(PlayerSide8 displaySide)
    {
        // 화면 기준 4방향으로 스냅한다.
        // Down을 보고 있으면 등 뒤는 화면 위, Up을 보고 있으면 등 뒤는 화면 아래다.
        return displaySide switch
        {
            PlayerSide8.Up or PlayerSide8.UpLeft or PlayerSide8.UpRight => Vector2.down,
            PlayerSide8.Left => Vector2.right,
            PlayerSide8.Right => Vector2.left,
            _ => Vector2.up,
        };
    }

    private static bool IsLeftSide(PlayerSide8 side)
    {
        return side is PlayerSide8.Left or PlayerSide8.DownLeft or PlayerSide8.UpLeft;
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
