using UnityEngine;

[RequireComponent(typeof(PlayerInput))]
[RequireComponent(typeof(PlayerFSMManager))]
[RequireComponent(typeof(PlayerAim))]
public class PlayerMeleeAttack : MonoBehaviour
{
    [Header("References")]
    // 플레이어 자식 오브젝트에 붙일 실제 공격 판정 콜라이더.
    [SerializeField] private MeleeHitbox meleeHitbox;

    [Header("Attack Data")]
    // 지금은 1개만 넣어도 되고, 나중에 1타/2타/3타 데이터를 순서대로 넣으면 된다.
    [SerializeField] private MeleeAttackData[] comboAttacks;
    // 콤보 데이터가 1개뿐일 때 입력 버퍼가 있으면 같은 공격을 반복할지 정한다.
    [SerializeField] private bool repeatSingleAttackFromBuffer = true;

    private PlayerInput input;
    private PlayerFSMManager fsm;
    private PlayerAim aim;

    // 현재 실행 중인 공격 데이터와, 공격 시작 순간에 고정한 방향.
    // 공격 중 마우스를 움직여도 이미 시작한 공격의 방향이 흔들리지 않게 한다.
    private MeleeAttackData currentAttackData;
    private PlayerSide8 currentAttackSide;

    // attackTimer는 현재 공격의 진행 시간, meleeInputBufferTimer는 미리 입력된 공격 입력의 남은 시간이다.
    private float attackTimer;
    private float meleeInputBufferTimer;
    // 공격이 끝난 뒤 다음 공격으로 이어질 수 있는 유예 시간이다.
    private float comboExpireTimer;

    // 지금은 1타만 써도 되지만, comboAttacks 배열이 늘어나면 이 인덱스로 다음 공격 데이터를 고른다.
    private int comboIndex;
    // 한 공격에서 Hitbox가 중복으로 켜지거나 꺼지는 것을 막는 플래그.
    private bool hitboxActivated;
    private bool hitboxDeactivated;
    // Dodge 중 콤보 유예 시간을 멈출 수 있게 남겨둔 확장 지점이다.
    private bool comboTimerPaused;

    private void Awake()
    {
        input = GetComponent<PlayerInput>();
        fsm = GetComponent<PlayerFSMManager>();
        aim = GetComponent<PlayerAim>();

        // 인스펙터에 직접 연결하지 않아도 자식 오브젝트에서 한 번 찾아본다.
        if (meleeHitbox == null)
        {
            meleeHitbox = GetComponentInChildren<MeleeHitbox>(true);
        }

        if (input == null || fsm == null || aim == null)
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
        MeleeAttackData attackData = currentAttackData != null ? currentAttackData : 
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

    private void StartAttack(MeleeAttackData attackData, int nextComboIndex)
    {
        if (attackData == null)
        {
            fsm.RequestState(PlayerState.Idle);
            return;
        }

        currentAttackData = attackData;
        // 공격 방향은 자유 각도가 아니라 PlayerSide8 기준 8방향으로 고정한다.
        currentAttackSide = aim.AimSide;
        comboIndex = Mathf.Clamp(nextComboIndex, 0, comboAttacks.Length - 1);
        comboExpireTimer = 0f;
        comboTimerPaused = false;

        attackTimer = 0f;
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
        Vector2 attackDirection = PlayerFacingUtil.Side8ToDir(currentAttackSide);
        meleeHitbox.Activate(transform, currentAttackData, attackDirection);
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

    private MeleeAttackData GetCurrentComboAttackData()
    {
        if (!HasAttackData())
        {
            return null;
        }

        comboIndex = Mathf.Clamp(comboIndex, 0, comboAttacks.Length - 1);
        return comboAttacks[comboIndex];
    }

    private bool HasAttackData()
    {
        return comboAttacks != null && comboAttacks.Length > 0 && comboAttacks[0] != null;
    }
}
