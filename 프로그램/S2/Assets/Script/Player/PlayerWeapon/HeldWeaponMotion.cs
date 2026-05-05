using UnityEngine;

public class HeldWeaponMotion : MonoBehaviour
{
    [Header("References")]
    // 이 도깨비 환도를 소유한 플레이어 루트다. 비워두면 transform.root에서 자동으로 찾는다.
    [SerializeField] private Transform playerRoot;
    // 검 본체를 숨길 때 끌 시각 자식 오브젝트다. 자기 자신을 넣으면 스크립트까지 꺼지므로 사용하지 않는다.
    [SerializeField] private GameObject visualRoot;
    // 정렬 순서 조정과 자동 참조 검색에 사용할 검 스프라이트 렌더러다.
    [SerializeField] private SpriteRenderer weaponRenderer;

    [Header("Floating Blade")]
    // 기본 상태에서 검이 플레이어 머리 뒤/등 뒤에 떠 있을 위치다. x는 좌우, y는 높이다.
    [SerializeField] private Vector2 backFloatOffset = new(-0.22f, 0.48f);
    // 이동 중 검이 이동 방향 반대로 얼마나 뒤처져 보일지 정한다.
    [SerializeField] private float moveLagDistance = 0.12f;
    // 기본 부유 상태에서 상하로 흔들리는 거리다.
    [SerializeField] private float floatBobAmount = 0.035f;
    // 기본 부유 상태에서 상하로 흔들리는 속도다.
    [SerializeField] private float floatBobSpeed = 3.5f;
    // 기본 부유 상태에서 좌우로 기울어지는 각도 폭이다.
    [SerializeField] private float floatTiltAmount = 5f;
    // 기본 부유 상태에서 좌우 기울어짐이 반복되는 속도다.
    [SerializeField] private float floatTiltSpeed = 2.4f;

    [Header("Aiming Pose")]
    // WeaponAiming 상태에서 검이 마우스 반대 방향으로 얼마나 물러나 장전 자세를 잡을지 정한다.
    [SerializeField] private float weaponAimingPullBackDistance = 0.42f;
    // WeaponAiming 상태에서 마우스 방향의 수직 방향으로 살짝 밀어주는 값이다.
    [SerializeField] private float weaponAimingSideOffset = 0.08f;
    // 이미지가 왼쪽을 기준으로 만들어졌을 때 보정하는 각도다.
    [SerializeField] private float baseAngleOffset = 180f;
    // WeaponAiming 상태에서 마우스 반대 방향 기준으로 검을 얼마나 수직 보정할지 정한다.
    [SerializeField] private float weaponAimingPerpendicularAngle = 90f;

    [Header("Melee Visibility")]
    // MeleeAttack 상태에서는 참격 이펙트가 공격을 보여주므로 검 본체를 숨길지 정한다.
    [SerializeField] private bool hideVisualDuringMeleeAttack = true;
    // 검 본체가 공격 때문에 사라질 때 켤 이펙트다. 비워두면 사용하지 않는다.
    [SerializeField] private GameObject vanishEffectPrefab;
    // 검 본체가 공격 후 다시 나타날 때 켤 이펙트다. 비워두면 사용하지 않는다.
    [SerializeField] private GameObject reappearEffectPrefab;

    [Header("Motion")]
    [SerializeField] private float followSharpness = 18f; // 검 위치가 목표점을 따라가는 속도.
    [SerializeField] private float rotateSharpness = 24f; // 검 회전이 목표 각도를 따라가는 속도.
    // 검이 너무 멀리 떨어져 있으면 보간하지 않고 즉시 목표 위치로 붙이는 거리다.
    [SerializeField] private float snapOnEnableDistance = 1.2f;

    // 입력 이동값을 읽어서 이동 중 부유 검의 뒤처짐을 만든다.
    private PlayerInput input;
    // 마우스 기준 조준 방향을 읽어서 조준 연출에 사용한다.
    private PlayerAim aim;
    // 현재 플레이어 상태를 읽어서 부유/조준/숨김 연출을 분기한다.
    private PlayerFSMManager fsm;
    // 현재 캐릭터 표시 방향을 읽어서 기본 부유 위치와 정렬 순서를 정한다.
    private PlayerAnim playerAnim;
    // 검 보유 여부를 읽어서 손에 검이 없을 때 연출 오브젝트를 숨긴다.
    private PlayerWeaponThrow weaponThrow;

    // SmoothDamp가 내부적으로 사용하는 위치 보간 속도값이다.
    private Vector3 positionVelocity;
    private GameObject vanishEffectInstance;
    private GameObject reappearEffectInstance;
    private bool wasVisualVisible;

    private void Awake() // 필요한 플레이어/검 참조를 자동으로 찾고, 없으면 컴포넌트를 비활성화한다.
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

        input = playerRoot != null ? playerRoot.GetComponent<PlayerInput>() : null;
        aim = playerRoot != null ? playerRoot.GetComponent<PlayerAim>() : null;
        fsm = playerRoot != null ? playerRoot.GetComponent<PlayerFSMManager>() : null;
        playerAnim = playerRoot != null ? playerRoot.GetComponent<PlayerAnim>() : null;
        weaponThrow = playerRoot != null ? playerRoot.GetComponent<PlayerWeaponThrow>() : null;

        if (playerRoot == null || input == null || aim == null || fsm == null || playerAnim == null || weaponThrow == null)
        {
            Debug.LogError($"{nameof(HeldWeaponMotion)} on {name} could not find required references.", this);
            enabled = false;
            return;
        }

        BuildEffectPool();
        wasVisualVisible = IsWeaponVisible();
    }

    private void LateUpdate() // 매 프레임 마지막에 검 표시 여부, 목표 위치, 목표 회전을 도깨비 환도 오브젝트에 적용한다.
    {
        UpdateWeaponVisibility();
        if (!weaponThrow.HasWeapon || ShouldHideForMeleeAttack())
        {
            return;
        }

        Vector2 targetLocalPosition = ResolveTargetLocalPosition(); // 검이 가야할 위치
        float targetAngle = ResolveTargetAngle(); // 검이 바라봐야할 목표 각도

        if (Vector2.Distance(transform.localPosition, targetLocalPosition) >= snapOnEnableDistance)
        {
            transform.localPosition = targetLocalPosition;
            positionVelocity = Vector3.zero;
        }
        else
        {
            // 전자식 환도는 실제 판정과 분리된 연출용 오브젝트라서 목표 궤도로 부드럽게 따라가게 한다.
            transform.localPosition = Vector3.SmoothDamp(
                transform.localPosition, // 현재위치
                targetLocalPosition, // 목표위치
                ref positionVelocity, // 현재 속도
                1f / Mathf.Max(1f, followSharpness)); // 목표위치까지 얼마나 부드럽게 따라갈지 값이 작으면 빨리감
        }

        Quaternion targetRotation = Quaternion.Euler(0f, 0f, targetAngle);
        transform.localRotation = Quaternion.Lerp(
            transform.localRotation, // 현재 회전
            targetRotation, // 목표 회전
            1f - Mathf.Exp(-rotateSharpness * Time.deltaTime)); // 보간 비율 높으면 빠르게 따라감

        UpdateSorting();
    }

    private void UpdateWeaponVisibility() // PlayerWeaponThrow.HasWeapon 값과 공격 상태에 따라 부유 검 시각 요소를 켜거나 끈다.
    {
        if (weaponThrow == null)
        {
            return;
        }

        bool shouldShowWeapon = weaponThrow.HasWeapon && !ShouldHideForMeleeAttack();
        if (IsWeaponVisible() == shouldShowWeapon)
        {
            return;
        }

        SetWeaponVisible(shouldShowWeapon);
        PlayVisibilityEffect(shouldShowWeapon);

        if (shouldShowWeapon)
        {
            transform.localPosition = ResolveFloatingPosition();
            positionVelocity = Vector3.zero;
        }
    }

    private bool ShouldHideForMeleeAttack() // 참격 이펙트가 공격을 담당하는 동안 검 본체를 숨길지 결정한다.
    {
        return hideVisualDuringMeleeAttack && fsm != null && fsm.IsState(PlayerState.MeleeAttack);
    }

    private bool IsWeaponVisible() // 현재 도깨비 환도 시각 요소가 보이는지 확인한다.
    {
        if (visualRoot != null && visualRoot != gameObject)
        {
            return visualRoot.activeSelf;
        }

        return weaponRenderer == null || weaponRenderer.enabled;
    }

    private void SetWeaponVisible(bool visible) // 스크립트가 붙은 오브젝트는 유지하고 렌더러 또는 시각 자식만 켜고 끈다.
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

    private void BuildEffectPool() // 검 본체가 사라지고 나타날 때 사용할 이펙트 오브젝트를 미리 만들어 둔다.
    {
        vanishEffectInstance = CreateVisibilityEffect(vanishEffectPrefab, "VanishEffect");
        reappearEffectInstance = CreateVisibilityEffect(reappearEffectPrefab, "ReappearEffect");
    }

    private GameObject CreateVisibilityEffect(GameObject prefab, string objectName) // 표시 전환 이펙트 프리팹을 비활성 상태로 생성한다.
    {
        if (prefab == null)
        {
            return null;
        }

        GameObject effect = Instantiate(prefab, transform);
        effect.name = objectName;
        effect.SetActive(false);
        return effect;
    }

    private void PlayVisibilityEffect(bool appearing) // 검 본체가 나타나거나 사라지는 순간에 해당 이펙트를 잠깐 켠다.
    {
        if (wasVisualVisible == appearing)
        {
            return;
        }

        wasVisualVisible = appearing;
        GameObject effect = appearing ? reappearEffectInstance : vanishEffectInstance;
        if (effect == null)
        {
            return;
        }

        effect.transform.localPosition = Vector3.zero;
        effect.transform.localRotation = Quaternion.identity;
        effect.SetActive(false);
        effect.SetActive(true);
    }

    private Vector2 ResolveTargetLocalPosition() // 현재 FSM 상태에 맞는 검의 목표 로컬 위치를 고른다.
    {
        if (fsm.IsState(PlayerState.WeaponAiming))
        {
            return ResolveWeaponAimingPosition();
        }

        return ResolveFloatingPosition();
    }

    private Vector2 ResolveFloatingPosition() // 기본/이동 상태에서 플레이어 뒤에 떠다니는 검 위치를 계산한다.
    {
        Vector2 position = ResolveBackAnchorOffset();

        // 이동 중에는 진행 방향 반대로 살짝 밀려서 플레이어 뒤에서 따라오는 자동추적 느낌을 낸다.
        if (input.Move.sqrMagnitude > 0.0001f && fsm.CanMoveInMainState())
        {
            position -= input.Move.normalized * moveLagDistance;
        }

        float bob = Mathf.Sin(Time.time * floatBobSpeed) * floatBobAmount;
        position += Vector2.up * bob;
        return position;
    }

    private Vector2 ResolveBackAnchorOffset() // 캐릭터 표시 방향에 따라 등 뒤 부유 기준점을 좌우 반전해서 계산한다.
    {
        PlayerSide8 displaySide = playerAnim != null ? playerAnim.CurrentDisplaySide : PlayerSide8.Down;
        Vector2 offset = backFloatOffset;

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

    private Vector2 ResolveWeaponAimingPosition() // 투척 조준 중 마우스 반대 방향으로 물러난 장전 위치를 계산한다.
    {
        Vector2 aimDirection = ResolveAimDirection();
        Vector2 perpendicular = new(-aimDirection.y, aimDirection.x);

        // 투척 준비 중에는 검이 마우스 반대 방향으로 물러나 전자식으로 장전되는 느낌을 준다.
        return -aimDirection * weaponAimingPullBackDistance + perpendicular * weaponAimingSideOffset + Vector2.up * backFloatOffset.y;
    }

    private float ResolveTargetAngle() // 현재 FSM 상태에 맞는 검의 목표 회전 각도를 고른다.
    {
        if (fsm.IsState(PlayerState.WeaponAiming))
        {
            return ResolveWeaponAimingAngle();
        }

        return ResolveFloatingAngle();
    }

    private float ResolveFloatingAngle() // 기본 부유 상태에서 검이 비스듬히 떠 있는 회전 각도를 계산한다.
    {
        float sideSign = ResolveDisplaySideSign();
        float tilt = Mathf.Sin(Time.time * floatTiltSpeed) * floatTiltAmount;
        return 90f * sideSign + baseAngleOffset + tilt;
    }

    private float ResolveWeaponAimingAngle() // 투척 조준 중 마우스 반대 방향 기준으로 검을 수직 정렬하는 각도를 계산한다.
    {
        Vector2 aimDirection = ResolveAimDirection();
        // 조준 중에는 마우스 반대 방향을 기준으로 검을 수직으로 세운다.
        return DirectionToAngle(-aimDirection) + weaponAimingPerpendicularAngle + baseAngleOffset;
    }

    private Vector2 ResolveAimDirection() // PlayerAim의 조준 방향을 안전하게 정규화해서 반환한다.
    {
        Vector2 direction = aim != null ? aim.AimDirection : Vector2.right;
        return NormalizeOrDefault(direction, Vector2.right);
    }

    private float ResolveDisplaySideSign() // 캐릭터 표시 방향이 왼쪽인지 오른쪽인지 회전 부호로 변환한다.
    {
        PlayerSide8 displaySide = playerAnim != null ? playerAnim.CurrentDisplaySide : PlayerSide8.Down;
        return IsLeftSide(displaySide) ? -1f : 1f;
    }

    private void UpdateSorting() // 캐릭터가 위를 볼 때 검이 뒤에 보이도록 스프라이트 정렬 순서를 조정한다.
    {
        if (weaponRenderer == null || playerAnim == null)
        {
            return;
        }

        // 기본 설정상 검은 플레이어 머리/등 뒤에 떠 있으므로 위쪽 방향을 볼 때는 확실히 뒤로 보낸다.
        PlayerSide8 displaySide = playerAnim.CurrentDisplaySide;
        weaponRenderer.sortingOrder = displaySide is PlayerSide8.Up or PlayerSide8.UpLeft or PlayerSide8.UpRight ? -1 : 1;
    }

    private static bool IsLeftSide(PlayerSide8 side) // 8방향 값이 왼쪽 계열인지 확인한다.
    {
        return side is PlayerSide8.Left or PlayerSide8.DownLeft or PlayerSide8.UpLeft;
    }

    private static bool IsRightSide(PlayerSide8 side) // 8방향 값이 오른쪽 계열인지 확인한다.
    {
        return side is PlayerSide8.Right or PlayerSide8.DownRight or PlayerSide8.UpRight;
    }

    private static float DirectionToAngle(Vector2 direction) // 방향 벡터를 Z축 회전 각도로 변환한다.
    {
        Vector2 normalizedDirection = NormalizeOrDefault(direction, Vector2.right);
        return Mathf.Atan2(normalizedDirection.y, normalizedDirection.x) * Mathf.Rad2Deg;
    }

    private static Vector2 NormalizeOrDefault(Vector2 direction, Vector2 fallback) // 방향이 0에 가까우면 fallback을 대신 정규화해서 반환한다.
    {
        return direction.sqrMagnitude > 0.0001f ? direction.normalized : fallback.normalized;
    }
}
