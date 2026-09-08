using UnityEngine;

/// <summary>조작 유닛의 상하좌우 낮은 엄폐물·벽 유무를 대기 애니메이션에 반영한다.</summary>
public class LowCoverIdlePresenter : MonoBehaviour
{
    [Header("Reference")]
    // 대상 유닛의 GridActor와 생존 상태를 제공하는 Context다.
    [SerializeField] private TacticalUnitContext unitContext;
    // 엄폐 대기 여부를 전달할 표시 컴포넌트다.
    [SerializeField] private ActorVisualController visualController;
    // 낮은 엄폐물과 벽의 논리 타일을 조회하는 그리드다.
    [SerializeField] private GridManager gridManager;
    // 논리 위치보다 늦게 진행되는 이동·공격 연출이 끝났는지 확인하는 큐다.
    [SerializeField] private ActionPresentationQueue presentationQueue;

    // 현재 표시 컴포넌트에 전달한 낮은 엄폐 상태다.
    private bool isNearLowCover;
    // 현재 표시 컴포넌트에 전달한 벽 엄폐 상태다.
    private bool isNearWallCover;
    // 이번 활성화 이후 엄폐 대기 여부를 한 번 이상 전달했는지 나타낸다.
    private bool hasAppliedState;
    // 필수 참조와 Animator 구성이 검증되었는지 나타낸다.
    private bool initialized;

    public bool IsNearLowCover => isNearLowCover;
    public bool IsNearWallCover => isNearWallCover;

    /// <summary>필수 참조와 엄폐 애니메이션 구성을 검사하고 첫 갱신을 준비한다.</summary>
    private void OnEnable()
    {
        hasAppliedState = false;
        initialized = HasValidReference() && visualController.HasValidCoverIdleAnimation();
        if (!initialized)
        {
            enabled = false;
        }
    }

    /// <summary>연출 종료 후 확정된 논리 위치의 엄폐 상태를 대기에 반영한다.</summary>
    private void LateUpdate()
    {
        if (presentationQueue.IsPlaying)
        {
            return;
        }

        bool canUseCover = unitContext.isActiveAndEnabled && unitContext.IsAlive &&
                           unitContext.GridActor.IsRegisteredOnGrid;
        bool nextState = canUseCover && gridManager.HasAdjacentLowObstacle(unitContext.GridActor.GridPosition);
        bool nextWallState = canUseCover && gridManager.HasAdjacentWall(unitContext.GridActor.GridPosition);
        if (hasAppliedState && nextState == isNearLowCover && nextWallState == isNearWallCover)
        {
            return;
        }

        isNearLowCover = nextState;
        isNearWallCover = nextWallState;
        hasAppliedState = true;
        visualController.SetCoverIdle(nextState, nextWallState);
    }

    /// <summary>기능을 끌 때 이전 낮은 엄폐·벽 엄폐 대기 설정을 해제한다.</summary>
    private void OnDisable()
    {
        if (initialized && visualController != null && visualController.Animator != null)
        {
            visualController.SetCoverIdle(false, false);
        }

        initialized = false;
        hasAppliedState = false;
        isNearLowCover = false;
        isNearWallCover = false;
    }

    /// <summary>대상 Context와 표시·그리드·연출 큐 참조가 연결되어 있는지 검사한다.</summary>
    public bool HasValidReference()
    {
        if (unitContext == null || unitContext.GridActor == null || unitContext.Health == null ||
            visualController == null || gridManager == null || presentationQueue == null)
        {
            Debug.LogError($"{nameof(LowCoverIdlePresenter)} on {name}에는 GridActor·Health가 연결된 TacticalUnitContext, ActorVisualController, GridManager, ActionPresentationQueue 참조가 필요합니다.", this);
            return false;
        }

        return true;
    }
}
