using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PlayerInput))]
[RequireComponent(typeof(PlayerFSMManager))]
public class PlayerDodge : MonoBehaviour
{
    [SerializeField] private float dodgeDuration = 0.18f;
    [SerializeField] private float dodgeSpeed = 10f;
    [SerializeField] private Vector2 defaultDodgeDirection = Vector2.down;

    private Rigidbody2D rb;
    private PlayerInput input;
    private PlayerFSMManager fsm;

    private Vector2 lastMoveDirection = Vector2.down;
    private Vector2 dodgeDirection = Vector2.down;
    private float dodgeTimer;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        input = GetComponent<PlayerInput>();
        fsm = GetComponent<PlayerFSMManager>();

        if (rb == null || input == null || fsm == null)
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
            fsm.RequestState(PlayerState.Idle);
        }
    }

    private void FixedUpdate()
    {
        if (!fsm.IsState(PlayerState.Dodge))
        {
            return;
        }

        Vector2 nextPosition = rb.position + dodgeDirection * dodgeSpeed * Time.fixedDeltaTime;
        rb.MovePosition(nextPosition);
    }

    private void HandleStateChanged(PlayerState previousState, PlayerState nextState)
    {
        if (nextState != PlayerState.Dodge)
        {
            return;
        }

        // 실제 회피 시작은 상태가 승인되어 Dodge로 바뀐 뒤에만 일어난다.
        dodgeTimer = dodgeDuration;
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
}
