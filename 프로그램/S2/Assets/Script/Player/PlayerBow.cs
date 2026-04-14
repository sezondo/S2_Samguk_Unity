using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(PlayerInput))]
[RequireComponent(typeof(PlayerFSMManager))]
public class PlayerBow : MonoBehaviour
{
    [Serializable]
    private struct FirePointEntry
    {
        // 조준 8방향마다 사용할 발사 시작 위치.
        public PlayerSide8 side;
        public Transform point;
    }

    [Header("References")]
    [SerializeField] private PlayerData playerData;
    [SerializeField] private Transform defaultFirePoint;
    [SerializeField] private FirePointEntry[] firePoints;

    [Header("Bow Settings")]
    [SerializeField] private float maxChargeTime = 1.2f;
    [SerializeField] private float bowShootDuration = 0.08f;
    [SerializeField] private float projectileSpeed = 12f;
    [SerializeField] private float maxSpreadAngle = 12f;
    [SerializeField] private float minSpreadAngle = 0.5f;
    [SerializeField] private Vector2 defaultAimDirection = Vector2.down;

    private PlayerInput input;
    private PlayerFSMManager fsm;

    private float currentChargeTime;
    private float bowShootTimer;
    private Vector2 lastAimDirection = Vector2.down;

    private void Awake()
    {
        input = GetComponent<PlayerInput>();
        fsm = GetComponent<PlayerFSMManager>();

        if (input == null || fsm == null)
        {
            Debug.LogError($"{nameof(PlayerBow)} on {name} is missing a required component.", this);
            enabled = false;
            return;
        }

        if (defaultFirePoint == null)
        {
            defaultFirePoint = transform;
        }
    }

    private void OnEnable()
    {
        if (fsm != null)
        {
            fsm.OnStateChanged += HandleStateChanged;
        }
    }

    private void OnDisable()
    {
        if (fsm != null)
        {
            fsm.OnStateChanged -= HandleStateChanged;
        }
    }

    private void Update()
    {
        UpdateAimDirection();

        // 활은 "입력 즉시 실행"이 아니라 현재 상태에 따라 요청/처리를 나눈다.
        if (fsm.IsState(PlayerState.Idle))
        {
            TryEnterBowCharge();
            return;
        }

        if (fsm.IsState(PlayerState.BowCharge))
        {
            UpdateBowCharge();
            return;
        }

        if (fsm.IsState(PlayerState.BowShoot))
        {
            UpdateBowShoot();
        }
    }

    private void TryEnterBowCharge()
    {
        // 우클릭을 누른 첫 프레임에만 활 차지 상태를 요청한다.
        if (!input.BowPressedThisFrame)
        {
            return;
        }

        fsm.RequestState(PlayerState.BowCharge);
    }

    private void UpdateBowCharge()
    {
        // BowCharge는 우클릭을 누르고 있는 동안만 유지되며 release 시 발사로 넘어간다.
        currentChargeTime = Mathf.Min(currentChargeTime + Time.deltaTime, maxChargeTime);

        if (input.BowReleasedThisFrame)
        {
            fsm.RequestState(PlayerState.BowShoot);
        }
    }

    private void UpdateBowShoot()
    {
        bowShootTimer -= Time.deltaTime;
        if (bowShootTimer > 0f)
        {
            return;
        }

        // BowShoot은 짧은 실행 상태만 맡고 끝나면 항상 Idle로 복귀한다.
        fsm.RequestState(PlayerState.Idle);
    }

    private void HandleStateChanged(PlayerState previousState, PlayerState nextState)
    {
        if (nextState == PlayerState.BowCharge)
        {
            // 새 차지가 시작되면 이전 차지 시간은 버린다.
            currentChargeTime = 0f;
            return;
        }

        if (nextState != PlayerState.BowShoot)
        {
            return;
        }

        bowShootTimer = bowShootDuration;
        FireArrow();
        currentChargeTime = 0f;
    }

    private void FireArrow()
    {
        // PlayerBow는 발사 타이밍/방향/오차만 결정하고,
        // 실제 이동/수명/충돌 처리는 Arrow 쪽으로 넘긴다.
        Vector2 finalDirection = GetShotDirection();
        Transform selectedFirePoint = ResolveFirePoint(finalDirection);

        if (playerData != null && playerData.bulletPrefab != null)
        {
            GameObject bullet = Instantiate(playerData.bulletPrefab, selectedFirePoint.position, Quaternion.identity);
            bullet.transform.right = finalDirection;

            if (bullet.TryGetComponent<Arrow>(out Arrow arrow))
            {
                int arrowTypeId = playerData != null ? playerData.equippedArrowTypeId : 0;
                arrow.Initialize(finalDirection, projectileSpeed, arrowTypeId);
            }
            else
            {
                Debug.LogWarning($"{playerData.bulletPrefab.name} is missing an {nameof(Arrow)} component.", this);
            }
        }

        Debug.DrawRay(selectedFirePoint.position, finalDirection * 2.5f, Color.yellow, 1f);
    }

    private Vector2 GetShotDirection()
    {
        Vector2 aimDirection = ResolveAimDirection();
        float spreadAngle = GetSpreadAngle();
        float randomAngle = UnityEngine.Random.Range(-spreadAngle, spreadAngle);

        // 차지 시간에 따라 spread를 계산한 뒤 최종 발사 방향을 만든다.
        return Rotate(aimDirection, randomAngle).normalized;
    }

    private float GetSpreadAngle()
    {
        // 오래 차지할수록 spread가 줄어들어 목표 지점에 가깝게 날아간다.
        float chargeRatio = maxChargeTime <= 0f ? 1f : Mathf.Clamp01(currentChargeTime / maxChargeTime);
        return Mathf.Lerp(maxSpreadAngle, minSpreadAngle, chargeRatio);
    }

    private void UpdateAimDirection()
    {
        Vector2 aimDirection = ResolveAimDirection();
        if (aimDirection.sqrMagnitude > 0.0001f)
        {
            lastAimDirection = aimDirection.normalized;
        }
    }

    private Vector2 ResolveAimDirection()
    {
#if ENABLE_INPUT_SYSTEM
        // 현재 1순위 조준 기준은 마우스의 월드 위치다.
        if (Mouse.current != null && Camera.main != null)
        {
            Vector3 mouseScreenPosition = Mouse.current.position.ReadValue();
            Vector3 worldPosition = Camera.main.ScreenToWorldPoint(mouseScreenPosition);
            Vector2 aimDirection = worldPosition - transform.position;
            if (aimDirection.sqrMagnitude > 0.0001f)
            {
                return aimDirection.normalized;
            }
        }
#endif

        // 마우스 조준이 불가능할 때만 이동 방향/이전 조준 방향으로 fallback 한다.
        if (input.Move.sqrMagnitude > 0.0001f)
        {
            return input.Move.normalized;
        }

        if (lastAimDirection.sqrMagnitude > 0.0001f)
        {
            return lastAimDirection.normalized;
        }

        return defaultAimDirection.normalized;
    }

    private Transform ResolveFirePoint(Vector2 aimDirection)
    {
        // 조준 방향을 8방향으로 양자화해서 해당 방향 firePoint를 고른다.
        PlayerSide8 side = PlayerFacingUtil.Quantize8OrDefault(aimDirection, PlayerSide8.Down);

        foreach (FirePointEntry firePointEntry in firePoints)
        {
            if (firePointEntry.side == side && firePointEntry.point != null)
            {
                return firePointEntry.point;
            }
        }

        return defaultFirePoint;
    }

    private static Vector2 Rotate(Vector2 direction, float angleDegrees)
    {
        float radians = angleDegrees * Mathf.Deg2Rad;
        float sin = Mathf.Sin(radians);
        float cos = Mathf.Cos(radians);

        return new Vector2(
            direction.x * cos - direction.y * sin,
            direction.x * sin + direction.y * cos);
    }
}
