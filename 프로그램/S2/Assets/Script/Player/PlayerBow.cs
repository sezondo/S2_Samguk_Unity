using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(PlayerInput))]
[RequireComponent(typeof(PlayerFSMManager))]
public class PlayerBow : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerData playerData;
    [SerializeField] private Transform firePoint;

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

        if (firePoint == null)
        {
            firePoint = transform;
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

        // 활 입력은 현재 메인 FSM 상태에 따라 요청/처리를 나눈다.
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
        if (!input.BowHeld)
        {
            return;
        }

        fsm.RequestState(PlayerState.BowCharge);
    }

    private void UpdateBowCharge()
    {
        // 차지 시간은 BowShoot의 명중 오차 계산에만 사용한다.
        currentChargeTime = Mathf.Min(currentChargeTime + Time.deltaTime, maxChargeTime);

        if (!input.BowHeld || input.BowReleasedThisFrame)
        {
            fsm.RequestState(PlayerState.Idle);
            return;
        }

        if (input.MeleeAttackPressedThisFrame)
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

        // 발사 직후에도 우클릭을 계속 누르고 있으면 다시 차지 상태로 복귀한다.
        if (input.BowHeld)
        {
            fsm.RequestState(PlayerState.BowCharge);
            return;
        }

        fsm.RequestState(PlayerState.Idle);
    }

    private void HandleStateChanged(PlayerState previousState, PlayerState nextState)
    {
        if (nextState == PlayerState.BowCharge)
        {
            // 새 차지는 이전 발사의 차지 시간을 이어받지 않는다.
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
        // 실제 발사 방향은 현재 조준 방향 + 차지 시간 기반 오차로 결정한다.
        Vector2 finalDirection = GetShotDirection();

        if (playerData != null && playerData.bulletPrefab != null)
        {
            GameObject bullet = Instantiate(playerData.bulletPrefab, firePoint.position, Quaternion.identity);
            bullet.transform.right = finalDirection;

            if (bullet.TryGetComponent<Rigidbody2D>(out Rigidbody2D bulletRb))
            {
                bulletRb.linearVelocity = finalDirection * projectileSpeed;
            }
        }

        Debug.DrawRay(firePoint.position, finalDirection * 2.5f, Color.yellow, 1f);
    }

    private Vector2 GetShotDirection()
    {
        Vector2 aimDirection = ResolveAimDirection();
        float spreadAngle = GetSpreadAngle();
        float randomAngle = Random.Range(-spreadAngle, spreadAngle);

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
        // 현재 1순위 조준 기준은 마우스 월드 좌표다.
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

        // 마우스 조준이 불가능한 상황에서는 마지막 이동/조준 방향을 fallback으로 사용한다.
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
