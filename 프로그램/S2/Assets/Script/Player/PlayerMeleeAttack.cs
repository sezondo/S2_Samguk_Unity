using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PlayerInput))]
[RequireComponent(typeof(PlayerFSMManager))]
[RequireComponent(typeof(PlayerAim))]
[RequireComponent(typeof(PlayerLoadout))]
[RequireComponent(typeof(PlayerContext))]
public class PlayerMeleeAttack : MonoBehaviour
{
    [Header("References")]
    // 플레이어 자식 오브젝트에 붙일 실제 공격 판정 콜라이더.
    [SerializeField] private MeleeHitbox meleeHitbox;

    [Header("Attack Settings")]
    // 콤보 데이터가 1개뿐일 때 입력 버퍼가 있으면 같은 공격을 반복할지 정한다.
    [SerializeField] private bool repeatSingleAttackFromBuffer = true;

    private PlayerInput input;
    private PlayerFSMManager fsm;
    private PlayerAim aim;
    private PlayerLoadout loadout;
    private Rigidbody2D rb;

    // 현재 실행 중인 공격 데이터와, 공격 시작 순간에 고정한 방향.
    // 공격 중 마우스를 움직여도 이미 시작한 공격의 방향이 흔들리지 않게 한다.
    private PlayerMeleeAttackData currentAttackData;
    private Vector2 currentAttackDirection = Vector2.down;

    // attackTimer는 현재 공격의 진행 시간, meleeInputBufferTimer는 미리 입력된 공격 입력의 남은 시간이다.
    private float attackTimer;
    private float meleeInputBufferTimer;
    // 현재 공격에서 이미 전진한 거리다.
    // 공격 데이터별 advanceDistance를 넘지 않도록 누적해서 프레임마다 남은 거리만 이동한다.
    private float advancedDistance;
    // 공격이 끝난 뒤 다음 공격으로 이어질 수 있는 유예 시간이다.
    private float comboExpireTimer;

    // 지금은 1타만 써도 되지만, comboAttacks 배열이 늘어나면 이 인덱스로 다음 공격 데이터를 고른다.
    private int comboIndex;
    // 같은 FSM 상태 안에서 콤보가 이어져도 새 공격 시작을 외부 연출이 구분할 수 있게 하는 번호다.
    private int attackSequenceId;
    // 한 공격에서 Hitbox가 중복으로 켜지거나 꺼지는 것을 막는 플래그.
    private bool hitboxActivated;
    private bool hitboxDeactivated;
    // Dodge 중 콤보 유예 시간을 멈출 수 있게 남겨둔 확장 지점이다.
    private bool comboTimerPaused;

    // 현재 실행 중인 근접 공격 데이터다.
    // 검 연출 같은 외부 표현 컴포넌트가 읽기만 하며, 실제 공격 선택과 진행은 PlayerMeleeAttack이 소유한다.
    public PlayerMeleeAttackData CurrentAttackData => currentAttackData;

    // 현재 공격이 0~1 사이에서 얼마나 진행됐는지 알려주는 연출용 진행률이다.
    // attackDuration을 기준으로 계산하므로 PlayerWeaponVisualFSM은 공격별 휘두르는 시간을 따로 추측하지 않는다.
    public float AttackNormalizedTime
    {
        get
        {
            if (currentAttackData == null || currentAttackData.attackDuration <= 0f)
            {
                return 0f;
            }

            return Mathf.Clamp01(attackTimer / currentAttackData.attackDuration);
        }
    }

    // 현재 공격이 시작된 뒤 흐른 실제 시간이다.
    // 참격 이펙트와 검 이동은 이 값을 기준으로 PlayerMeleeAttackData의 판정 시간에 맞춘다.
    public float AttackElapsedTime => attackTimer;

    // 공격 시작 순간에 고정된 마우스 기준 방향이다.
    // 공격 도중 마우스를 움직여도 실제 판정과 검 연출 방향이 함께 흔들리지 않게 한다.
    public Vector2 CurrentAttackDirection => currentAttackDirection;

    // 현재 콤보 인덱스다. 디버그 표시나 연출 분기용으로만 읽게 둔다.
    public int CurrentComboIndex => comboIndex;

    // 현재 실행 중인 공격을 식별하는 번호다.
    // 같은 콤보 번호를 반복해도 공격이 새로 시작되면 값이 바뀐다.
    public int AttackSequenceId => attackSequenceId;

    // 공격 종료 후 다음 콤보 입력을 기다리는 중인지 알려준다.
    // 검 비주얼은 이 시간 동안 본체가 잠깐 나타나는 것을 막는 데 사용한다.
    public bool IsComboWindowOpenForVisual => IsComboWindowOpen() && currentAttackData != null;

    public bool TryGetCurrentHitboxCenter(out Vector3 worldPosition)
    {
        if (currentAttackData == null)
        {
            worldPosition = transform.position;
            return false;
        }

        worldPosition = (Vector2)transform.position + ResolveAttackOffset(currentAttackData, currentAttackDirection);
        return true;
    }

    private void Awake()
    {
        PlayerContext context = GetComponent<PlayerContext>();
        context.ResolveReferences();

        input = context.Input;
        fsm = context.Fsm;
        aim = context.Aim;
        loadout = context.Loadout;
        rb = context.Body;

        // 인스펙터에 직접 연결하지 않아도 자식 오브젝트에서 한 번 찾아본다.
        if (meleeHitbox == null)
        {
            meleeHitbox = GetComponentInChildren<MeleeHitbox>(true);
        }

        if (input == null || fsm == null || aim == null || loadout == null || rb == null)
        {
            Debug.LogError($"{nameof(PlayerMeleeAttack)} on {name} is missing a required component.", this);
            enabled = false;
            return;
        }

        if (meleeHitbox == null)
        {
            Debug.LogWarning($"{nameof(PlayerMeleeAttack)} on {name} has no melee hitbox assigned.", this);
        }

        if (!HasAttackData())
        {
            Debug.LogWarning($"{nameof(PlayerMeleeAttack)} on {name} has no melee attack data.", this);
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

        DeactivateHitbox();
    }

    private void Update()
    {
        // 입력 버퍼와 콤보 유예 시간은 행동 상태와 별개로 계속 갱신한다.
        UpdateInputBufferTimer();
        UpdateComboExpireTimer();

        if (input.MeleeAttackPressedThisFrame)
        {
            // Idle이 아니어도 입력을 버리지 않고 잠시 저장한다.
            BufferMeleeInput();
        }

        // 입력이 바로 공격을 실행하지 않고, 먼저 FSM에 MeleeAttack 상태를 요청한다.
        if (fsm.IsState(PlayerState.Idle))
        {
            TryStartBufferedAttackFromIdle();
        }

        if (!fsm.IsState(PlayerState.MeleeAttack))
        {
            return;
        }

        UpdateAttack();
    }

    private void FixedUpdate()
    {
        // Rigidbody2D 이동은 물리 프레임에서 처리해서 일반 이동/회피와 같은 방식으로 맞춘다.
        ApplyAttackAdvance();
    }

    private void HandleStateChanged(PlayerState previousState, PlayerState nextState)
    {
        if (previousState == PlayerState.MeleeAttack && nextState != PlayerState.MeleeAttack)
        {
            // 회피/사망 등으로 공격 상태가 끊겨도 판정은 반드시 꺼둔다.
            DeactivateHitbox();

            // Dodge 중 콤보 유지 같은 조작 정책은 나중에 이 훅을 기준으로 확장한다.
            if (nextState == PlayerState.Dodge)
            {
                PauseComboWindow();
            }
        }

        if (previousState == PlayerState.Dodge && nextState != PlayerState.Dodge) // 이놈이 다시 닷지후 콤보 이어가게함
        {
            ResumeComboWindow();
        }

        if (nextState != PlayerState.MeleeAttack)
        {
            return;
        }

        StartAttack(GetCurrentComboAttackData(), comboIndex);
    }

    private void BufferMeleeInput()
    {
        // 현재 공격 중이면 현재 공격 데이터의 버퍼 시간을 쓰고,
        // 아직 공격 전이면 이번에 시작할 공격 데이터의 버퍼 시간을 쓴다.
        PlayerMeleeAttackData attackData = currentAttackData != null ? currentAttackData : 
        GetCurrentComboAttackData();

        float bufferDuration = attackData != null ? attackData.inputBufferDuration : 0f;
        meleeInputBufferTimer = Mathf.Max(meleeInputBufferTimer, bufferDuration);
    }

    private void UpdateInputBufferTimer()
    {
        if (meleeInputBufferTimer <= 0f)
        {
            return;
        }

        meleeInputBufferTimer = Mathf.Max(0f, meleeInputBufferTimer - Time.deltaTime);
    }

    private void TryStartBufferedAttackFromIdle()
    {
        if (!HasBufferedMeleeInput() || !HasAttackData())
        {
            return;
        }

        ConsumeMeleeInputBuffer();

        // 콤보 유예 시간이 끝난 뒤 들어온 입력이면 다시 1타부터 시작한다.
        if (!IsComboWindowOpen())
        {
            ResetCombo();
        }

        fsm.RequestState(PlayerState.MeleeAttack);
    }

    private void StartAttack(PlayerMeleeAttackData attackData, int nextComboIndex)
    {
        if (attackData == null)
        {
            fsm.RequestState(PlayerState.Idle);
            return;
        }

        currentAttackData = attackData;
        // 공격 방향 계산은 PlayerAim이 담당하고, 공격은 시작 순간의 AimDirection만 고정해서 쓴다.
        currentAttackDirection = aim.AimDirection;
        PlayerMeleeAttackData[] comboAttacks = GetComboAttacks();
        comboIndex = Mathf.Clamp(nextComboIndex, 0, comboAttacks.Length - 1);
        comboExpireTimer = 0f;
        comboTimerPaused = false;

        attackTimer = 0f;
        attackSequenceId++;
        advancedDistance = 0f;
        hitboxActivated = false;
        hitboxDeactivated = false;
        // 새 공격을 시작하기 전 이전 판정 상태를 정리한다.
        DeactivateHitbox(false);
    }

    private void UpdateAttack()
    {
        if (currentAttackData == null)
        {
            fsm.RequestState(PlayerState.Idle);
            return;
        }

        attackTimer += Time.deltaTime;

        if (!hitboxActivated && attackTimer >= currentAttackData.hitboxStartTime)
        {
            // 애니메이션이 생기기 전까지는 타이머로 판정 시작 시점을 대신한다.
            ActivateHitbox();
        }

        if (!hitboxDeactivated && 
        attackTimer >= currentAttackData.hitboxStartTime + currentAttackData.hitboxActiveTime)
        {
            // 한 번 공격에서 판정이 너무 오래 남지 않도록 별도 시간으로 끈다.
            DeactivateHitbox(true);
        }

        if (attackTimer < currentAttackData.attackDuration)
        {
            return;
        }

        FinishAttack();
    }

    private void ApplyAttackAdvance()
    {
        // 공격 상태가 아니거나 공격 데이터가 없으면 전진 이동을 하지 않는다.
        if (!fsm.IsState(PlayerState.MeleeAttack) || currentAttackData == null || rb == null)
        {
            return;
        }

        // advanceDistance가 0이면 이 공격은 제자리 공격으로 처리한다.
        float totalDistance = Mathf.Max(0f, currentAttackData.advanceDistance);
        if (totalDistance <= 0f || advancedDistance >= totalDistance)
        {
            return;
        }

        // 공격마다 전진 시작 타이밍을 다르게 줄 수 있게 한다.
        if (attackTimer < currentAttackData.advanceStartTime)
        {
            return;
        }

        float remainingDistance = totalDistance - advancedDistance;
        float advanceDuration = Mathf.Max(0f, currentAttackData.advanceDuration);
        // advanceDuration이 0이면 남은 거리를 한 번에 이동한다.
        // 값이 있으면 총 이동 거리를 duration 동안 나눠서 이동한다.
        float distanceThisStep = advanceDuration <= 0f
            ? remainingDistance
            : totalDistance / advanceDuration * Time.fixedDeltaTime;

        // 마지막 프레임에서 목표 거리보다 더 나아가지 않도록 남은 거리로 제한한다.
        distanceThisStep = Mathf.Min(distanceThisStep, remainingDistance);
        advancedDistance += distanceThisStep;

        Vector2 moveDirection = GetAttackAdvanceDirection();
        Vector2 nextPosition = rb.position + moveDirection * distanceThisStep;
        rb.MovePosition(nextPosition);
    }

    private void FinishAttack()
    {
        DeactivateHitbox();

        // 공격 중 미리 눌러둔 입력이 있으면 지금은 다음 comboAttacks 항목으로 진행한다.
        // Dodge 후 자동 연계/재입력 요구 같은 세부 조작감은 이 분기에서 나중에 조정하면 된다.
        if (HasBufferedMeleeInput() && TryAdvanceCombo())
        {
            ConsumeMeleeInputBuffer();
            StartAttack(GetCurrentComboAttackData(), comboIndex);
            return;
        }

        if (IsLastComboAttack())
        {
            ConsumeMeleeInputBuffer();
            ResetCombo();
            fsm.RequestState(PlayerState.Idle);
            return;
        }

        OpenComboWindow();
        fsm.RequestState(PlayerState.Idle);
    }

    private void ActivateHitbox()
    {
        hitboxActivated = true;
        hitboxDeactivated = false;

        if (meleeHitbox == null || currentAttackData == null)
        {
            return;
        }

        // Hitbox의 위치/크기/회전 적용은 MeleeHitbox가 담당한다.
        // PlayerMeleeAttack은 어떤 공격 데이터와 방향을 쓸지만 넘긴다.
        meleeHitbox.Activate(transform, currentAttackData, GetAttackDirection());
    }

    private Vector2 GetAttackDirection()
    {
        // PlayerAim에서 받은 공격 시작 순간의 조준 방향을 그대로 사용한다.
        return currentAttackDirection;
    }

    private Vector2 GetAttackAdvanceDirection()
    {
        Vector2 attackDirection = GetAttackDirection();
        // 이동 방향만 데이터 값에 따라 정방향/역방향으로 바꾼다.
        // 히트박스 방향은 항상 공격 방향을 유지한다.
        return currentAttackData != null && currentAttackData.moveBackwardByAdvance
            ? -attackDirection
            : attackDirection;
    }

    private void DeactivateHitbox(bool markDeactivated = true)
    {
        if (markDeactivated)
        {
            hitboxDeactivated = true;
        }

        if (meleeHitbox != null)
        {
            meleeHitbox.Deactivate();
        }
    }

    private bool TryAdvanceCombo()
    {
        if (!HasAttackData())
        {
            return false;
        }

        PlayerMeleeAttackData[] comboAttacks = GetComboAttacks();
        // 다음 칸에 공격 데이터가 있으면 그 데이터를 다음 콤보 공격으로 사용한다.
        if (comboIndex + 1 < comboAttacks.Length && comboAttacks[comboIndex + 1] != null)
        {
            comboIndex++;
            return true;
        }

        // 아직 콤보 데이터가 1개뿐인 테스트 단계에서는 같은 1타를 반복할 수 있게 둔다.
        if (repeatSingleAttackFromBuffer && comboAttacks.Length == 1)
        {
            comboIndex = 0;
            return true;
        }

        return false;
    }

    private bool IsLastComboAttack()
    {
        if (!HasAttackData())
        {
            return true;
        }

        PlayerMeleeAttackData[] comboAttacks = GetComboAttacks();
        return comboIndex >= comboAttacks.Length - 1 || comboAttacks[comboIndex + 1] == null;
    }

    private void OpenComboWindow()
    {
        if (currentAttackData == null)
        {
            ResetCombo();
            return;
        }

        // 공격은 끝났지만 다음 입력을 받으면 콤보를 이어갈 수 있는 시간이다.
        comboExpireTimer = currentAttackData.comboExpireTime;
    }

    private void UpdateComboExpireTimer()
    {
        if (comboTimerPaused || comboExpireTimer <= 0f)
        {
            return;
        }

        comboExpireTimer = Mathf.Max(0f, comboExpireTimer - Time.deltaTime);
        if (comboExpireTimer <= 0f)
        {
            ResetCombo();
        }
    }

    private void PauseComboWindow()
    {
        // 현재는 Dodge 중 콤보 타이머 정지 가능성만 열어둔다.
        // 실제로 자동 연계할지, Dodge 후 재입력을 요구할지는 나중에 조작감 보고 결정한다.
        comboTimerPaused = true;
    }

    private void ResumeComboWindow()
    {
        comboTimerPaused = false;
    }

    private void ResetCombo()
    {
        comboIndex = 0;
        comboExpireTimer = 0f;
        comboTimerPaused = false;
        currentAttackData = null;
        advancedDistance = 0f;
        currentAttackDirection = Vector2.down;
    }

    private bool HasBufferedMeleeInput()
    {
        return meleeInputBufferTimer > 0f;
    }

    private void ConsumeMeleeInputBuffer()
    {
        meleeInputBufferTimer = 0f;
    }

    private bool IsComboWindowOpen()
    {
        return comboExpireTimer > 0f || comboTimerPaused;
    }

    private PlayerMeleeAttackData GetCurrentComboAttackData()
    {
        if (!HasAttackData())
        {
            return null;
        }

        PlayerMeleeAttackData[] comboAttacks = GetComboAttacks();
        comboIndex = Mathf.Clamp(comboIndex, 0, comboAttacks.Length - 1);
        return comboAttacks[comboIndex];
    }

    private bool HasAttackData()
    {
        PlayerMeleeAttackData[] comboAttacks = GetComboAttacks();
        return comboAttacks != null && comboAttacks.Length > 0 && comboAttacks[0] != null;
    }

    private PlayerMeleeAttackData[] GetComboAttacks()
    {
        return loadout != null ? loadout.MeleeComboAttacks : null;
    }

    public static Vector2 ResolveAttackOffset(PlayerMeleeAttackData attackData, Vector2 attackDirection)
    {
        if (attackData == null)
        {
            return Vector2.zero;
        }

        Vector2 normalizedDirection = attackDirection.sqrMagnitude > 0.0001f
            ? attackDirection.normalized
            : Vector2.down;
        Vector2 perpendicular = new(-normalizedDirection.y, normalizedDirection.x);

        return normalizedDirection * attackData.hitboxOffset.x
            + perpendicular * attackData.hitboxOffset.y;
    }
}
