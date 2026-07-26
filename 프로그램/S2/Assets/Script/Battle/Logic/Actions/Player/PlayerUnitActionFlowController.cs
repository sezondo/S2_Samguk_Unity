using UnityEngine;

/// <summary>
/// 플레이어 행동 하나의 논리 처리와 연출 큐 실행 시점을 조정한다.
/// 논리 이벤트가 모두 처리된 뒤 연출 큐를 재생한다.
/// </summary>
public class PlayerUnitActionFlowController : MonoBehaviour
{
    [Header("Reference")]
    // 논리 결과 연출을 재생할 씬 단위 연출 큐다.
    [SerializeField] private ActionPresentationQueue presentationQueue;
    // 새 플레이어 행동을 시작할 수 있는 스테이지 진행 상태를 제공한다.
    [SerializeField] private StageStateManager stageStateManager;

    [Header("Log")]
    // true면 행동 실행 차단 사유를 Unity 콘솔에 출력한다.
    [SerializeField] private bool logBlockedAction = true;

    // 씬에서 사용하는 플레이어 행동 흐름 조정자 인스턴스다.
    public static PlayerUnitActionFlowController Instance { get; private set; }
    // 현재 플레이어 제어 매니저가 선택한 전술 유닛이다.
    private TacticalUnitContext ActiveUnit => PlayerUnitControlManager.Instance != null
        ? PlayerUnitControlManager.Instance.ActiveUnit
        : null;

    /// <summary>
    /// 씬의 단일 플레이어 유닛 행동 흐름 조정자 인스턴스를 등록한다.
    /// </summary>
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"{nameof(PlayerUnitActionFlowController)}: 이미 인스턴스가 있습니다. 중복 오브젝트 {name}의 컴포넌트를 비활성화합니다.", this);
            enabled = false;
            return;
        }

        Instance = this;

        if (presentationQueue == null || stageStateManager == null)
        {
            if (presentationQueue == null)
            {
                Debug.LogError($"{nameof(PlayerUnitActionFlowController)} on {name}에는 {nameof(ActionPresentationQueue)} 참조가 필요합니다.", this);
            }

            if (stageStateManager == null)
            {
                Debug.LogError($"{nameof(PlayerUnitActionFlowController)} on {name}에는 진행 상태를 제공할 {nameof(StageStateManager)} 참조가 필요합니다.", this);
            }

            enabled = false;
        }
    }

    /// <summary>
    /// 현재 인스턴스가 제거될 때 싱글톤 참조를 정리한다.
    /// </summary>
    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// 플레이어 이동 행동을 실행하고 논리 처리가 끝나면 연출 큐를 재생한다.
    /// </summary>
    public bool TryExecuteMove(GridPosition targetPosition)
    {
        if (!CanStartAction())
        {
            return false;
        }

        ActionResolutionContext resolutionContext = new(presentationQueue);
        if (!ActiveUnit.GridMoveAction.TryExecuteMoveTo(targetPosition, resolutionContext))
        {
            return false;
        }

        resolutionContext.Resolve();
        presentationQueue.PlayQueuedEvents();
        return true;
    }

    /// <summary>
    /// 플레이어 해킹 행동을 실행하고 논리 처리가 끝나면 연출 큐를 재생한다.
    /// </summary>
    public bool TryExecuteHack(HackableObject target)
    {
        if (!CanStartAction())
        {
            return false;
        }

        if (ActiveUnit.HackAction == null)
        {
            Debug.Log($"{nameof(PlayerUnitActionFlowController)}: 현재 유닛 {ActiveUnit.name}은 해킹 능력이 없습니다.", this);
            return false;
        }

        ActionResolutionContext resolutionContext = new(presentationQueue);
        if (!ActiveUnit.HackAction.TryExecuteHack(target, resolutionContext))
        {
            return false;
        }

        resolutionContext.Resolve();
        presentationQueue.PlayQueuedEvents();
        return true;
    }

    /// <summary>
    /// 플레이어 검 투척 행동을 실행하고 논리 처리가 끝나면 연출 큐를 재생한다.
    /// </summary>
    public bool TryExecuteSwordThrow(GridPosition targetPosition)
    {
        if (!CanStartAction())
        {
            return false;
        }

        if (ActiveUnit.SwordThrowAction == null)
        {
            Debug.Log($"{nameof(PlayerUnitActionFlowController)}: 현재 유닛 {ActiveUnit.name}은 검 투척 능력이 없습니다.", this);
            return false;
        }

        ActionResolutionContext resolutionContext = new(presentationQueue);
        if (!ActiveUnit.SwordThrowAction.TryExecuteSwordThrow(targetPosition, resolutionContext))
        {
            return false;
        }

        resolutionContext.Resolve();
        presentationQueue.PlayQueuedEvents();
        return true;
    }

    /// <summary>
    /// 플레이어 검 회수 행동을 실행하고 논리 처리가 끝나면 연출 큐를 재생한다.
    /// </summary>
    public bool TryExecuteSwordRecall()
    {
        if (!CanStartAction())
        {
            return false;
        }

        if (ActiveUnit.SwordRecallAction == null)
        {
            Debug.Log($"{nameof(PlayerUnitActionFlowController)}: 현재 유닛 {ActiveUnit.name}은 검 회수 능력이 없습니다.", this);
            return false;
        }

        ActionResolutionContext resolutionContext = new(presentationQueue);
        if (!ActiveUnit.SwordRecallAction.TryExecuteSwordRecall(resolutionContext))
        {
            return false;
        }

        resolutionContext.Resolve();
        presentationQueue.PlayQueuedEvents();
        return true;
    }

    /// <summary>
    /// 플레이어 근접 공격 행동을 실행하고 논리 처리가 끝나면 연출 큐를 재생한다.
    /// </summary>
    public bool TryExecuteMeleeAttack(GridPosition targetPosition)
    {
        if (!CanStartAction())
        {
            return false;
        }

        if (ActiveUnit.MeleeAttackAction == null)
        {
            Debug.Log($"{nameof(PlayerUnitActionFlowController)}: 현재 유닛 {ActiveUnit.name}은 근접 공격 능력이 없습니다.", this);
            return false;
        }

        ActionResolutionContext resolutionContext = new(presentationQueue);
        if (!ActiveUnit.MeleeAttackAction.TryExecuteMeleeAttack(targetPosition, resolutionContext))
        {
            return false;
        }

        resolutionContext.Resolve();
        presentationQueue.PlayQueuedEvents();
        return true;
    }

    /// <summary>
    /// 플레이어 총 공격 행동을 실행하고 논리 처리가 끝나면 연출 큐를 재생한다.
    /// </summary>
    public bool TryExecuteGunAttack(GridPosition targetPosition)
    {
        if (!CanStartAction())
        {
            return false;
        }

        if (ActiveUnit.GunAttackAction == null)
        {
            Debug.Log($"{nameof(PlayerUnitActionFlowController)}: 현재 유닛 {ActiveUnit.name}은 총 공격 능력이 없습니다.", this);
            return false;
        }

        ActionResolutionContext resolutionContext = new(presentationQueue);
        if (!ActiveUnit.GunAttackAction.TryExecuteGunAttack(targetPosition, resolutionContext))
        {
            return false;
        }

        resolutionContext.Resolve();
        presentationQueue.PlayQueuedEvents();
        return true;
    }

    /// <summary>
    /// 현재 플레이어 행동을 시작할 수 있는지 확인한다.
    /// </summary>
    private bool CanStartAction()
    {
        if (!HasValidSceneReference())
        {
            return false;
        }

        if (!stageStateManager.IsPlaying)
        {
            if (logBlockedAction)
            {
                Debug.Log($"{nameof(PlayerUnitActionFlowController)}: 스테이지가 {stageStateManager.CurrentState} 상태라 새 행동을 시작할 수 없습니다.", this);
            }

            return false;
        }

        if (ActiveUnit == null)
        {
            if (logBlockedAction)
            {
                Debug.Log($"{nameof(PlayerUnitActionFlowController)}: 현재 선택된 조작 유닛이 없습니다.", this);
            }

            return false;
        }

        if (!ActiveUnit.HasValidReference() || !ActiveUnit.IsAlive || ActiveUnit.ActionPoint.Current <= 0)
        {
            return false;
        }

        if (presentationQueue.IsPlaying)
        {
            if (logBlockedAction)
            {
                Debug.Log($"{nameof(PlayerUnitActionFlowController)}: 연출 큐가 실행 중이라 새 행동을 시작할 수 없습니다.", this);
            }

            return false;
        }

        return true;
    }

    /// <summary>
    /// 행동 흐름 조정에 필요한 필수 참조가 연결되어 있는지 확인한다.
    /// </summary>
    private bool HasValidSceneReference()
    {
        if (PlayerUnitControlManager.Instance == null)
        {
            Debug.LogError($"{nameof(PlayerUnitActionFlowController)} on {name}에는 씬의 {nameof(PlayerUnitControlManager)}가 필요합니다.", this);
            return false;
        }

        if (presentationQueue == null)
        {
            Debug.LogError($"{nameof(PlayerUnitActionFlowController)} on {name}에는 {nameof(ActionPresentationQueue)} 참조가 필요합니다.", this);
            return false;
        }

        if (stageStateManager == null)
        {
            Debug.LogError($"{nameof(PlayerUnitActionFlowController)} on {name}에는 진행 상태를 제공할 {nameof(StageStateManager)} 참조가 필요합니다.", this);
            return false;
        }

        return true;
    }
}
