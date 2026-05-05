using UnityEngine;

[RequireComponent(typeof(PlayerInput))]
[RequireComponent(typeof(PlayerFSMManager))]
[RequireComponent(typeof(PlayerAim))]
public class PlayerAnim : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private SpriteRenderer spriteRenderer;

    private PlayerInput input;
    private PlayerFSMManager fsm;
    private PlayerAim aim;

    private PlayerSide8 lastMoveSide = PlayerSide8.Down;
    private PlayerSide8 lastDisplaySide = PlayerSide8.Down;
    private PlayerSide8 lockedActionSide = PlayerSide8.Down;
    private PlayerAnimState? currentAnimState;

    // 외부 연출 컴포넌트가 현재 캐릭터가 어느 방향으로 표시되는지만 읽을 수 있게 여는 값이다.
    // HeldWeaponMotion 같은 시각 연출은 이 값을 참조하지만, 표시 방향 결정 자체는 PlayerAnim이 계속 담당한다.
    public PlayerSide8 CurrentDisplaySide { get; private set; } = PlayerSide8.Down;

    private static readonly int AnimStateHash = Animator.StringToHash("AnimState");
    private static readonly int RestartAnimationHash = Animator.StringToHash("RestartAnimation");

    private void Awake()
    {
        input = GetComponent<PlayerInput>();
        fsm = GetComponent<PlayerFSMManager>();
        aim = GetComponent<PlayerAim>();

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        if (input == null || fsm == null || aim == null || animator == null || spriteRenderer == null)
        {
            Debug.LogError($"{nameof(PlayerAnim)} on {name} is missing a required component.", this);
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
        UpdateMoveSide();
        PlayResolvedAnimation();
    }

    private void HandleStateChanged(PlayerState previousState, PlayerState nextState)
    {
        if (nextState == PlayerState.MeleeAttack
            || nextState == PlayerState.WeaponThrowing
            || nextState == PlayerState.WeaponAiming)
        {
            lockedActionSide = aim != null ? aim.AimSide : lastDisplaySide;
            return;
        }

        if (nextState == PlayerState.Dodge)
        {
            lockedActionSide = GetMoveSideOrLast();
        }
    }

    private void UpdateMoveSide()
    {
        if (PlayerFacingUtil.TryQuantize8(input.Move, out PlayerSide8 moveSide))
        {
            lastMoveSide = moveSide;
        }
    }

    private void PlayResolvedAnimation()
    {
        PlayerState state = fsm.CurrentState;

        if (state == PlayerState.Dead)
        {
            Play("Dead", lastDisplaySide);
            return;
        }

        if (state == PlayerState.Dodge)
        {
            Play("Dodge", lockedActionSide);
            return;
        }

        if (state == PlayerState.MeleeAttack)
        {
            Play("Attack", lockedActionSide);
            return;
        }

        if (state == PlayerState.WeaponThrowing)
        {
            Play("WeaponThrow", lockedActionSide);
            return;
        }

        if (state == PlayerState.WeaponAiming && !HasMoveInput())
        {
            Play("WeaponThrowReady", aim.AimSide);
            return;
        }

        PlayMoveOrIdle();
    }

    private void PlayMoveOrIdle()
    {
        if (HasMoveInput())
        {
            Play("Run", lastMoveSide);
            return;
        }

        Play("Idle", lastDisplaySide);
    }

    private void Play(string clipGroup, PlayerSide8 side)
    {
        PlayerAnimFacingUtil.ToAnim3(side, out PlayerAnimDir3 animDir, out bool flipX);

        PlayerAnimState animState = BuildAnimState(clipGroup, animDir);
        if (currentAnimState != animState)
        {
            animator.SetInteger(AnimStateHash, (int)animState); //지시 신호
            animator.SetTrigger(RestartAnimationHash); //실행신호
            currentAnimState = animState;
        }

        spriteRenderer.flipX = flipX;
        lastDisplaySide = side;
        CurrentDisplaySide = side;
    }

    private PlayerSide8 GetMoveSideOrLast()
    {
        return HasMoveInput() ? lastMoveSide : lastDisplaySide;
    }

    private bool HasMoveInput()
    {
        return input.Move.sqrMagnitude > 0.0001f;
    }

    private static PlayerAnimState BuildAnimState(string clipGroup, PlayerAnimDir3 animDir)
    {
        return clipGroup switch
        {
            "Idle" => animDir switch
            {
                PlayerAnimDir3.Front => PlayerAnimState.IdleFront,
                PlayerAnimDir3.Side => PlayerAnimState.IdleSide,
                _ => PlayerAnimState.IdleBack,
            },
            "Run" => animDir switch
            {
                PlayerAnimDir3.Front => PlayerAnimState.RunFront,
                PlayerAnimDir3.Side => PlayerAnimState.RunSide,
                _ => PlayerAnimState.RunBack,
            },
            "Attack" => animDir switch
            {
                PlayerAnimDir3.Front => PlayerAnimState.AttackFront,
                PlayerAnimDir3.Side => PlayerAnimState.AttackSide,
                _ => PlayerAnimState.AttackBack,
            },
            "Dodge" => animDir switch
            {
                PlayerAnimDir3.Front => PlayerAnimState.DodgeFront,
                PlayerAnimDir3.Side => PlayerAnimState.DodgeSide,
                _ => PlayerAnimState.DodgeBack,
            },
            "Dead" => animDir switch
            {
                PlayerAnimDir3.Front => PlayerAnimState.DeadFront,
                PlayerAnimDir3.Side => PlayerAnimState.DeadSide,
                _ => PlayerAnimState.DeadBack,
            },
            "WeaponThrowReady" => animDir switch
            {
                PlayerAnimDir3.Front => PlayerAnimState.WeaponThrowReadyFront,
                PlayerAnimDir3.Side => PlayerAnimState.WeaponThrowReadySide,
                _ => PlayerAnimState.WeaponThrowReadyBack,
            },
            "WeaponThrow" => animDir switch
            {
                PlayerAnimDir3.Front => PlayerAnimState.WeaponThrowFront,
                PlayerAnimDir3.Side => PlayerAnimState.WeaponThrowSide,
                _ => PlayerAnimState.WeaponThrowBack,
            },
            _ => PlayerAnimState.IdleFront,
        };
    }
}
