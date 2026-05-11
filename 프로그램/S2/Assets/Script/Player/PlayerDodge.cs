using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PlayerInput))]
[RequireComponent(typeof(PlayerFSMManager))]
[RequireComponent(typeof(PlayerLoadout))]
[RequireComponent(typeof(PlayerContext))]
public class PlayerDodge : MonoBehaviour
{
    [SerializeField] private float fallbackDodgeDuration = 0.18f;
    [SerializeField] private float fallbackDodgeSpeed = 10f;
    [SerializeField] private Vector2 defaultDodgeDirection = Vector2.down;

    private Rigidbody2D rb;
    private PlayerInput input;
    private PlayerFSMManager fsm;
    private PlayerLoadout loadout;
    private PlayerWeaponThrow weaponThrow;
    private PlayerHackController hackController;

    private Vector2 lastMoveDirection = Vector2.down;
    private Vector2 dodgeDirection = Vector2.down;
    private PlayerState stateBeforeDodge = PlayerState.Idle;
    private float dodgeTimer;

    private void Awake()
    {
        PlayerContext context = GetComponent<PlayerContext>();
        context.ResolveReferences();

        rb = context.Body;
        input = context.Input;
        fsm = context.Fsm;
        loadout = context.Loadout;
        weaponThrow = context.WeaponThrow;
        hackController = context.HackController;

        if (rb == null || input == null || fsm == null || loadout == null)
        {
            Debug.LogError($"{nameof(PlayerDodge)} on {name} is missing a required component.", this);
            enabled = false;
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
        if (input.Move.sqrMagnitude > 0.0001f)
        {
            lastMoveDirection = input.Move.normalized;
        }

        // 입력은 상태를 직접 바꾸는 게 아니라 Dodge 상태를 요청한다.
        if (input.DodgePressedThisFrame)
        {
            fsm.RequestState(PlayerState.Dodge);
        }

        if (!fsm.IsState(PlayerState.Dodge))
        {
            return;
        }

        dodgeTimer -= Time.deltaTime;
        if (dodgeTimer <= 0f)
        {
            RequestStateAfterDodge();
        }
    }

    private void FixedUpdate()
    {
        if (!fsm.IsState(PlayerState.Dodge))
        {
            return;
        }

        Vector2 nextPosition = rb.position + dodgeDirection * ResolveDodgeSpeed() * Time.fixedDeltaTime;
        rb.MovePosition(nextPosition);
    }

    private void HandleStateChanged(PlayerState previousState, PlayerState nextState)
    {
        if (nextState != PlayerState.Dodge)
        {
            return;
        }

        // 실제 회피 시작은 상태가 승인되어 Dodge로 바뀐 뒤에만 일어난다.
        stateBeforeDodge = previousState;
        dodgeTimer = ResolveDodgeDuration();
        dodgeDirection = ResolveDodgeDirection();
    }

    private Vector2 ResolveDodgeDirection()
    {
        if (input.Move.sqrMagnitude > 0.0001f)
        {
            return input.Move.normalized;
        }

        if (lastMoveDirection.sqrMagnitude > 0.0001f)
        {
            return lastMoveDirection.normalized;
        }

        return defaultDodgeDirection.normalized;
    }

    private float ResolveDodgeDuration()
    {
        PlayerDodgeData dodgeData = ResolveDodgeData();
        return dodgeData != null && dodgeData.duration > 0f
            ? dodgeData.duration
            : Mathf.Max(0f, fallbackDodgeDuration);
    }

    private float ResolveDodgeSpeed()
    {
        PlayerDodgeData dodgeData = ResolveDodgeData();
        return dodgeData != null && dodgeData.speed > 0f
            ? dodgeData.speed
            : Mathf.Max(0f, fallbackDodgeSpeed);
    }

    private PlayerDodgeData ResolveDodgeData()
    {
        PlayerData playerData = loadout != null ? loadout.PlayerData : null;
        return playerData != null ? playerData.dodge : null;
    }

    private void RequestStateAfterDodge()
    {
        if (stateBeforeDodge == PlayerState.Hacking && hackController != null && hackController.IsHackSessionActive)
        {
            fsm.RequestState(PlayerState.Hacking);
            return;
        }

        if (weaponThrow != null && !weaponThrow.HasWeapon)
        {
            fsm.RequestState(PlayerState.WeaponReceiving);
            return;
        }

        fsm.RequestState(PlayerState.Idle);
    }
}
