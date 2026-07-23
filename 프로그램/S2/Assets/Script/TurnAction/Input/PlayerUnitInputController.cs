using UnityEngine;

/// <summary>
/// 씬의 단일 입력 Reader를 현재 선택된 전술 유닛의 행동 요청으로 변환한다.
/// 유닛 선택 입력을 행동 확정보다 먼저 처리해 같은 클릭으로 두 작업이 동시에 실행되지 않게 한다.
/// </summary>
public class PlayerUnitInputController : MonoBehaviour
{
    [Header("Reference")]
    // 플레이어 입력을 처리할 수 있는 스테이지 진행 상태를 제공한다.
    [SerializeField] private StageStateManager stageStateManager;

    [Header("Log")]
    // true면 현재 유닛이 지원하지 않는 행동을 선택했을 때 안내 로그를 출력한다.
    [SerializeField] private bool logUnavailableAbility = true;

    /// <summary>
    /// 입력 시스템과 제어 매니저가 준비되어 있는지 시작 시 확인한다.
    /// </summary>
    private void Start()
    {
        if (!HasValidReference())
        {
            enabled = false;
            return;
        }

        stageStateManager.StageStateChanged -= HandleStageStateChanged;
        stageStateManager.StageStateChanged += HandleStageStateChanged;

        if (!stageStateManager.IsPlaying)
        {
            CancelCurrentActionSelections();
        }
    }

    /// <summary>
    /// 스테이지 상태 이벤트 구독을 해제한다.
    /// </summary>
    private void OnDisable()
    {
        if (stageStateManager != null)
        {
            stageStateManager.StageStateChanged -= HandleStageStateChanged;
        }
    }

    /// <summary>
    /// 유닛 선택, 행동 모드 선택, 취소와 목표 확정 입력을 처리한다.
    /// </summary>
    private void Update()
    {
        if (!HasValidReference())
        {
            return;
        }

        if (!stageStateManager.IsPlaying)
        {
            return;
        }

        PlayerInputReader input = PlayerInputReader.Instance;
        PlayerUnitControlManager controlManager = PlayerUnitControlManager.Instance;
        if (controlManager.TryProcessSelectionInput())
        {
            return;
        }

        TacticalUnitContext unit = controlManager.ActiveUnit;
        if (unit == null)
        {
            return;
        }

        if (input.CancelPressedThisFrame)
        {
            unit.CancelAllActionSelections();
            return;
        }

        HandleActionSelection(input, unit);
        RefreshMovePreview(input, unit);

        if (input.RecallSwordPressedThisFrame)
        {
            ExecuteSwordRecall(unit);
            return;
        }

        if (!input.ConfirmPressedThisFrame || !input.TryGetPointerGridPosition(out GridPosition targetPosition))
        {
            return;
        }

        ExecuteSelectedAction(unit, targetPosition);
    }

    /// <summary>
    /// 이번 프레임에 눌린 행동 키에 맞춰 현재 유닛의 기존 모드를 해제하고 새 모드를 선택한다.
    /// </summary>
    private void HandleActionSelection(PlayerInputReader input, TacticalUnitContext unit)
    {
        if (input.SelectMovePressedThisFrame)
        {
            SelectAction(unit, UnitAbilityType.Move, unit.GridMoveAction, action => action.SelectMoveAction());
        }
        else if (input.SelectHackPressedThisFrame)
        {
            SelectAction(unit, UnitAbilityType.Hack, unit.HackAction, action => action.SelectHackAction());
        }
        else if (input.SelectSwordThrowPressedThisFrame)
        {
            SelectAction(unit, UnitAbilityType.Sword, unit.SwordThrowAction, action => action.SelectSwordThrowAction());
        }
        else if (input.SelectMeleeAttackPressedThisFrame)
        {
            SelectAction(unit, UnitAbilityType.Melee, unit.MeleeAttackAction, action => action.SelectMeleeAttackAction());
        }
        else if (input.SelectGunAttackPressedThisFrame)
        {
            SelectAction(unit, UnitAbilityType.Gun, unit.GunAttackAction, action => action.SelectGunAttackAction());
        }
    }

    /// <summary>
    /// 지정한 능력의 행동 컴포넌트가 있으면 기존 모드를 정리하고 선택 요청을 실행한다.
    /// </summary>
    private void SelectAction<T>(TacticalUnitContext unit, UnitAbilityType ability, T action, System.Action<T> select)
        where T : Component
    {
        unit.CancelAllActionSelections();
        if (action == null)
        {
            LogUnavailableAbility(unit, ability);
            return;
        }

        select(action);
    }

    /// <summary>
    /// 이동 모드일 때 현재 포인터 칸의 경로 미리보기를 갱신한다.
    /// </summary>
    private static void RefreshMovePreview(PlayerInputReader input, TacticalUnitContext unit)
    {
        PlayerGridMoveAction moveAction = unit.GridMoveAction;
        if (moveAction == null || !moveAction.IsMoveSelected)
        {
            return;
        }

        if (input.TryGetPointerGridPosition(out GridPosition targetPosition))
        {
            moveAction.RefreshPathPreview(targetPosition);
        }
        else
        {
            moveAction.ClearMovePathPreview();
        }
    }

    /// <summary>
    /// 현재 선택된 행동 모드에 따라 목표 칸 행동을 실행한다.
    /// </summary>
    private void ExecuteSelectedAction(TacticalUnitContext unit, GridPosition targetPosition)
    {
        PlayerUnitActionFlowController flow = PlayerUnitActionFlowController.Instance;
        if (unit.GridMoveAction != null && unit.GridMoveAction.IsMoveSelected)
        {
            flow.TryExecuteMove(targetPosition);
            return;
        }

        if (unit.HackAction != null && unit.HackAction.IsHackSelected)
        {
            if (HackableRegistry.Instance != null &&
                HackableRegistry.Instance.TryGetHackableAt(targetPosition, out HackableObject target))
            {
                flow.TryExecuteHack(target);
            }

            return;
        }

        if (unit.SwordThrowAction != null && unit.SwordThrowAction.IsSwordThrowSelected)
        {
            flow.TryExecuteSwordThrow(targetPosition);
            return;
        }

        if (unit.MeleeAttackAction != null && unit.MeleeAttackAction.IsMeleeAttackSelected)
        {
            flow.TryExecuteMeleeAttack(targetPosition);
            return;
        }

        if (unit.GunAttackAction != null && unit.GunAttackAction.IsGunAttackSelected)
        {
            flow.TryExecuteGunAttack(targetPosition);
        }
    }

    /// <summary>
    /// 현재 유닛이 검 회수 능력을 가지면 즉시 회수 행동을 요청한다.
    /// </summary>
    private void ExecuteSwordRecall(TacticalUnitContext unit)
    {
        unit.CancelAllActionSelections();
        if (unit.SwordRecallAction == null)
        {
            LogUnavailableAbility(unit, UnitAbilityType.Sword);
            return;
        }

        PlayerUnitActionFlowController.Instance.TryExecuteSwordRecall();
    }

    /// <summary>
    /// 스테이지가 클리어 또는 실패 상태로 바뀌면 현재 행동 선택과 경로 표시를 정리한다.
    /// </summary>
    private void HandleStageStateChanged(StageState _, StageState nextState)
    {
        if (nextState != StageState.Playing)
        {
            CancelCurrentActionSelections();
        }
    }

    /// <summary>
    /// 현재 조작 유닛에 남아 있는 모든 행동 선택 상태를 취소한다.
    /// </summary>
    private static void CancelCurrentActionSelections()
    {
        TacticalUnitContext activeUnit = PlayerUnitControlManager.Instance != null
            ? PlayerUnitControlManager.Instance.ActiveUnit
            : null;
        activeUnit?.CancelAllActionSelections();
    }

    /// <summary>
    /// 현재 유닛이 지원하지 않는 행동을 선택했다는 안내를 출력한다.
    /// </summary>
    private void LogUnavailableAbility(TacticalUnitContext unit, UnitAbilityType ability)
    {
        if (logUnavailableAbility)
        {
            Debug.Log($"{nameof(PlayerUnitInputController)}: {unit.name} 유닛은 {ability} 능력을 사용할 수 없습니다.", this);
        }
    }

    /// <summary>
    /// 통합 입력 처리에 필요한 씬 단위 참조가 준비되어 있는지 확인한다.
    /// </summary>
    private bool HasValidReference()
    {
        if (stageStateManager == null)
        {
            Debug.LogError($"{nameof(PlayerUnitInputController)} on {name}에는 진행 상태를 제공할 {nameof(StageStateManager)} 참조가 필요합니다.", this);
            return false;
        }

        if (PlayerInputReader.Instance == null)
        {
            Debug.LogError($"{nameof(PlayerUnitInputController)} on {name}에는 씬의 {nameof(PlayerInputReader)}가 필요합니다.", this);
            return false;
        }

        if (PlayerUnitControlManager.Instance == null)
        {
            Debug.LogError($"{nameof(PlayerUnitInputController)} on {name}에는 씬의 {nameof(PlayerUnitControlManager)}가 필요합니다.", this);
            return false;
        }

        if (PlayerUnitActionFlowController.Instance == null)
        {
            Debug.LogError($"{nameof(PlayerUnitInputController)} on {name}에는 씬의 {nameof(PlayerUnitActionFlowController)}가 필요합니다.", this);
            return false;
        }

        return true;
    }
}
