using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PlayerInput))]
[RequireComponent(typeof(PlayerFSMManager))]
[RequireComponent(typeof(PlayerLoadout))]
[RequireComponent(typeof(PlayerContext))]
public class PlayerDodge : MonoBehaviour
{
    private static readonly Vector2 DefaultDodgeDirection = Vector2.down;

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
            return;
        }

        if (!HasValidData())
        {
            enabled = false;
            return;
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

        return DefaultDodgeDirection;
    }

    private float ResolveDodgeDuration()
    {
        return ResolveDodgeData().duration;
    }

    private float ResolveDodgeSpeed()
    {
        return ResolveDodgeData().speed;
    }

    private PlayerDodgeData ResolveDodgeData()
    {
        return loadout.PlayerData.dodge;
    }

    private bool HasValidData()
    {
        PlayerData playerData = loadout.PlayerData;
        if (playerData == null)
        {
            Debug.LogError($"{nameof(PlayerDodge)} on {name} requires {nameof(PlayerData)}.", this);
            return false;
        }

        if (playerData.dodge == null)
        {
            Debug.LogError($"{nameof(PlayerDodge)} on {name} requires {nameof(PlayerDodgeData)} in {playerData.name}.", this);
            return false;
        }

        if (playerData.dodge.duration <= 0f)
        {
            Debug.LogError($"{nameof(PlayerDodge)} on {name} requires dodge duration greater than 0 in {playerData.name}.", this);
            return false;
        }

        if (playerData.dodge.speed <= 0f)
        {
            Debug.LogError($"{nameof(PlayerDodge)} on {name} requires dodge speed greater than 0 in {playerData.name}.", this);
            return false;
        }

        return true;
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
