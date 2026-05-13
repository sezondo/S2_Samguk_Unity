using UnityEngine;

[RequireComponent(typeof(PlayerInput))]
[RequireComponent(typeof(PlayerFSMManager))]
[RequireComponent(typeof(PlayerAim))]
[RequireComponent(typeof(PlayerLoadout))]
[RequireComponent(typeof(PlayerContext))]
public class PlayerWeaponThrow : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform throwPoint;
    [SerializeField] private ThrownWeapon thrownWeaponPrefab;

    private PlayerInput input;
    private PlayerFSMManager fsm;
    private PlayerAim aim;
    private PlayerLoadout loadout;
    private ThrownWeapon activeThrownWeapon;

    private float chargeTime;
    private float stateTimer;
    private bool hasWeapon = true;

    public bool HasWeapon => hasWeapon;
    public ThrownWeapon ActiveThrownWeapon => activeThrownWeapon;
    public bool HasEmbeddedHackWeapon => activeThrownWeapon != null && activeThrownWeapon.IsEmbeddedForHack;
    public float ChargeTime => chargeTime;
    public bool IsMinAimHoldComplete => chargeTime >= GetThrowData().minAimHoldTime;
    public float AimChargeRatio
    {
        get
        {
            PlayerWeaponThrowData throwData = GetThrowData();
            return throwData.maxChargeTime <= 0f ? 1f : Mathf.Clamp01(chargeTime / throwData.maxChargeTime);
        }
    }

    public event System.Action<ThrownWeapon, IHackable> EmbeddedHackWeaponRegistered;
    public event System.Action<ThrownWeapon> WeaponRecovered;

    private void Awake()
    {
        PlayerContext context = GetComponent<PlayerContext>();
        context.ResolveReferences();

        input = context.Input;
        fsm = context.Fsm;
        aim = context.Aim;
        loadout = context.Loadout;

        if (input == null || fsm == null || aim == null || loadout == null)
        {
            Debug.LogError($"{nameof(PlayerWeaponThrow)} on {name} is missing a required component.", this);
            enabled = false;
            return;
        }

        if (!HasValidData())
        {
            enabled = false;
            return;
        }

        if (throwPoint == null)
        {
            throwPoint = transform;
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
        if (fsm.IsState(PlayerState.Idle))
        {
            TryEnterWeaponAiming();
            return;
        }

        if (fsm.IsState(PlayerState.WeaponAiming))
        {
            UpdateWeaponAiming();
            return;
        }

        if (fsm.IsState(PlayerState.WeaponThrowing))
        {
            UpdateWeaponThrowing();
            return;
        }

        if (fsm.IsState(PlayerState.WeaponReceiving))
        {
            UpdateWeaponReceiving();
        }
    }

    private void TryEnterWeaponAiming()
    {
        if (!hasWeapon || !input.WeaponThrowPressedThisFrame)
        {
            return;
        }

        fsm.RequestState(PlayerState.WeaponAiming);
    }

    private void UpdateWeaponAiming()
    {
        PlayerWeaponThrowData throwData = GetThrowData();
        chargeTime = Mathf.Min(chargeTime + Time.deltaTime, throwData.maxChargeTime);

        if (input.WeaponThrowReleasedThisFrame)
        {
            // 최소 조준 시간이 되기 전에 우클릭을 떼면 투척을 취소하고 Idle로 복귀한다.
            // 검 비주얼은 PlayerWeaponVisualFSM이 PlayerState.Idle을 보고 Orbit으로 돌아간다.
            if (!IsMinAimHoldComplete)
            {
                chargeTime = 0f;
                fsm.RequestState(PlayerState.Idle);
                return;
            }

            if (ThrowWeapon())
            {
                fsm.RequestState(PlayerState.WeaponThrowing);
            }
            else
            {
                fsm.RequestState(PlayerState.Idle);
            }

            return;
        }

        if (!input.WeaponThrowHeld)
        {
            fsm.RequestState(PlayerState.Idle);
        }
    }

    private void UpdateWeaponThrowing()
    {
        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f)
        {
            fsm.RequestState(PlayerState.WeaponReceiving);
        }
    }

    private void UpdateWeaponReceiving()
    {
        if (hasWeapon)
        {
            fsm.CompleteWeaponReceiving();
        }
    }

    private void HandleStateChanged(PlayerState previousState, PlayerState nextState)
    {
        if (nextState == PlayerState.WeaponAiming)
        {
            chargeTime = 0f;
            stateTimer = GetThrowData().aimingStateDuration;
            return;
        }

        if (nextState == PlayerState.WeaponThrowing)
        {
            stateTimer = GetThrowData().throwingStateDuration;
            return;
        }

        if (nextState == PlayerState.WeaponReceiving)
        {
            stateTimer = 0f;
            return;
        }

        if (nextState == PlayerState.Idle)
        {
            chargeTime = 0f;
        }
    }

    private bool ThrowWeapon()
    {
        if (!hasWeapon || thrownWeaponPrefab == null)
        {
            Debug.LogWarning($"{nameof(PlayerWeaponThrow)} could not throw a weapon.", this);
            return false;
        }

        Vector2 direction = GetThrowDirection();
        activeThrownWeapon = Instantiate(thrownWeaponPrefab, throwPoint.position, Quaternion.identity);
        activeThrownWeapon.Initialize(this, direction, GetThrowData());
        SetHasWeapon(false);
        chargeTime = 0f;
        return true;
    }

    public void RecoverWeapon(ThrownWeapon weapon)
    {
        if (activeThrownWeapon != weapon)
        {
            return;
        }

        WeaponRecovered?.Invoke(weapon);
        activeThrownWeapon = null;
        SetHasWeapon(true);

        if (fsm.IsState(PlayerState.WeaponReceiving))
        {
            fsm.CompleteWeaponReceiving();
            return;
        }

        if (fsm.IsState(PlayerState.Hacking))
        {
            fsm.CompleteHacking();
        }
    }

    public void RegisterEmbeddedHackWeapon(ThrownWeapon weapon, IHackable hackable)
    {
        if (weapon == null || weapon != activeThrownWeapon)
        {
            return;
        }

        EmbeddedHackWeaponRegistered?.Invoke(weapon, hackable);
    }

    private void SetHasWeapon(bool nextHasWeapon)
    {
        // hasWeapon은 투척 가능 여부와 회수 완료 여부만 나타낸다.
        // 검 본체의 표시/숨김과 투척체 추적은 PlayerWeaponVisualFSM이 담당한다.
        hasWeapon = nextHasWeapon;
    }

    private Vector2 GetThrowDirection()
    {
        Vector2 aimDirection = aim.AimDirection;
        float spreadAngle = GetSpreadAngle();
        float randomAngle = Random.Range(-spreadAngle, spreadAngle);
        return Rotate(aimDirection, randomAngle).normalized;
    }

    private float GetSpreadAngle()
    {
        PlayerWeaponThrowData throwData = GetThrowData();
        float chargeRatio = throwData.maxChargeTime <= 0f ? 1f : Mathf.Clamp01(chargeTime / throwData.maxChargeTime);
        return Mathf.Lerp(throwData.maxSpreadAngle, throwData.minSpreadAngle, chargeRatio);
    }

    private PlayerWeaponThrowData GetThrowData()
    {
        return loadout.PlayerData.weaponThrow;
    }

    private bool HasValidData()
    {
        PlayerData playerData = loadout.PlayerData;
        if (playerData == null)
        {
            Debug.LogError($"{nameof(PlayerWeaponThrow)} on {name} requires {nameof(PlayerData)}.", this);
            return false;
        }

        PlayerWeaponThrowData throwData = playerData.weaponThrow;
        if (throwData == null)
        {
            Debug.LogError($"{nameof(PlayerWeaponThrow)} on {name} requires {nameof(PlayerWeaponThrowData)} in {playerData.name}.", this);
            return false;
        }

        if (throwData.maxChargeTime <= 0f)
        {
            Debug.LogError($"{nameof(PlayerWeaponThrow)} on {name} requires maxChargeTime greater than 0 in {playerData.name}.", this);
            return false;
        }

        if (throwData.minAimHoldTime < 0f)
        {
            Debug.LogError($"{nameof(PlayerWeaponThrow)} on {name} requires minAimHoldTime greater than or equal to 0 in {playerData.name}.", this);
            return false;
        }

        if (throwData.aimingStateDuration < 0f || throwData.throwingStateDuration < 0f)
        {
            Debug.LogError($"{nameof(PlayerWeaponThrow)} on {name} requires non-negative throw state durations in {playerData.name}.", this);
            return false;
        }

        if (throwData.throwSpeed <= 0f || throwData.returnSpeed <= 0f || throwData.maxDistance <= 0f)
        {
            Debug.LogError($"{nameof(PlayerWeaponThrow)} on {name} requires positive throwSpeed, returnSpeed, and maxDistance in {playerData.name}.", this);
            return false;
        }

        if (throwData.damage < 0)
        {
            Debug.LogError($"{nameof(PlayerWeaponThrow)} on {name} requires damage greater than or equal to 0 in {playerData.name}.", this);
            return false;
        }

        return true;
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
