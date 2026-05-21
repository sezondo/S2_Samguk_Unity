using UnityEngine;

[RequireComponent(typeof(EnemyContext))]
[RequireComponent(typeof(EnemyFSMManager))]
public class EnemyAnimation : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private SpriteRenderer spriteRenderer;

    private EnemyFSMManager fsm;
    private EnemyMovement movement;
    private IAttackInfoProvider attackInfoProvider;
    private EnemyDirection4 lastDirection = EnemyDirection4.Down;
    private EnemyAnimState? currentAnimState;

    public EnemyDirection4 CurrentDirection { get; private set; } = EnemyDirection4.Down;

    private static readonly int AnimStateHash = Animator.StringToHash("AnimState");
    private static readonly int RestartAnimationHash = Animator.StringToHash("RestartAnimation");

    private void Awake()
    {
        EnemyContext context = GetComponent<EnemyContext>();
        context.ResolveReferences();

        fsm = context.Fsm;
        movement = context.Movement;
        attackInfoProvider = context.AttackInfoProvider;

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        if (fsm == null || animator == null || spriteRenderer == null)
        {
            Debug.LogError($"{nameof(EnemyAnimation)} on {name} is missing required components.", this);
            enabled = false;
        }
    }

    private void Update()
    {
        UpdateDirection();
        PlayResolvedAnimation();
    }

    private void UpdateDirection()
    {
        Vector2 direction = Vector2.zero;

        if (fsm.IsState(EnemyState.Attack) && attackInfoProvider != null)
        {
            direction = attackInfoProvider.AttackDirection;
        }
        else if (movement != null)
        {
            direction = movement.MoveDirection;
        }

        if (direction.sqrMagnitude > 0.0001f)
        {
            lastDirection = Quantize4(direction);
        }

        CurrentDirection = lastDirection;
    }

    private void PlayResolvedAnimation()
    {
        if (fsm.IsState(EnemyState.Dead))
        {
            Play("Dead", lastDirection);
            return;
        }

        if (fsm.IsState(EnemyState.Attack))
        {
            Play("Attack", lastDirection);
            return;
        }

        if (movement != null && movement.MoveDirection.sqrMagnitude > 0.0001f && fsm.CanMove())
        {
            Play("Move", lastDirection);
            return;
        }

        Play("Idle", lastDirection);
    }

    private void Play(string clipGroup, EnemyDirection4 direction)
    {
        EnemyAnimState animState = BuildAnimState(clipGroup, direction);
        if (currentAnimState != animState)
        {
            animator.SetInteger(AnimStateHash, (int)animState);
            animator.SetTrigger(RestartAnimationHash);
            currentAnimState = animState;
        }

        spriteRenderer.flipX = direction == EnemyDirection4.Left;
        CurrentDirection = direction;
    }

    private static EnemyDirection4 Quantize4(Vector2 direction)
    {
        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
        {
            return direction.x < 0f ? EnemyDirection4.Left : EnemyDirection4.Right;
        }

        return direction.y > 0f ? EnemyDirection4.Up : EnemyDirection4.Down;
    }

    private static EnemyAnimState BuildAnimState(string clipGroup, EnemyDirection4 direction)
    {
        bool side = direction is EnemyDirection4.Left or EnemyDirection4.Right;
        return clipGroup switch
        {
            "Move" => side ? EnemyAnimState.MoveSide : direction == EnemyDirection4.Up ? EnemyAnimState.MoveUp : EnemyAnimState.MoveDown,
            "Attack" => side ? EnemyAnimState.AttackSide : direction == EnemyDirection4.Up ? EnemyAnimState.AttackUp : EnemyAnimState.AttackDown,
            "Dead" => side ? EnemyAnimState.DeadSide : direction == EnemyDirection4.Up ? EnemyAnimState.DeadUp : EnemyAnimState.DeadDown,
            _ => side ? EnemyAnimState.IdleSide : direction == EnemyDirection4.Up ? EnemyAnimState.IdleUp : EnemyAnimState.IdleDown,
        };
    }
}
