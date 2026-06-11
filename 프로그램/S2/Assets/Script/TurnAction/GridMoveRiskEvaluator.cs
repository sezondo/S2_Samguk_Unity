using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어 이동 경로가 적 감지 칸에 들어가는지 평가하고, 첫 위험 칸을 표시한다.
/// 실제 애드 연출과 이동 중단은 후속 액션 시퀀스 단계에서 연결한다.
/// </summary>
[RequireComponent(typeof(PlayerContext))]
[RequireComponent(typeof(PlayerGridMoveAction))]
public class GridMoveRiskEvaluator : MonoBehaviour
{
    [Header("Source")]
    // 플레이어 공통 참조와 이동 행동 컴포넌트를 제공하는 필수 Context다.
    [SerializeField] private PlayerContext playerContext;

    [Header("Warning Highlight")]
    // true면 경로 미리보기 중 첫 애드 위험 칸을 하이라이트로 표시한다.
    [SerializeField] private bool showWarningHighlight = true;
    // 첫 애드 위험 칸에 표시할 경고 하이라이트 색이다.
    [SerializeField] private Color warningColor = new(1f, 0.8f, 0.05f, 0.9f);
    // 경고 하이라이트가 그리드 한 칸에서 차지할 비율이다.
    [SerializeField] private float warningCellScaleRatio = 0.42f;
    // 경고 하이라이트 렌더러의 정렬 순서다.
    [SerializeField] private int warningSortingOrder = 45;
    // 경고 하이라이트를 월드 좌표에서 살짝 앞뒤로 보낼 때 쓰는 Z 오프셋이다.
    [SerializeField] private float warningZOffset = -0.12f;

    [Header("Log")]
    // 실제 이동 중 감지 칸에 진입했을 때 1차 애드 로그를 출력할지 정한다.
    [SerializeField] private bool logAddTriggered = true;

    // 경고 칸 하나를 GridCellHighlighter에 전달하기 위한 재사용 목록이다.
    private readonly List<GridPosition> warningPositions = new(1);

    // 경로 미리보기와 이동 칸 진입 이벤트를 발생시키는 플레이어 이동 행동 컴포넌트다.
    private PlayerGridMoveAction moveAction;
    // 첫 위험 칸 표시를 담당하는 런타임 하이라이터다.
    private GridCellHighlighter warningHighlighter;
    // 현재 경로 미리보기에서 처음으로 감지되는 칸이다.
    private GridPosition previewRiskPosition;
    // 현재 이동 실행 중 애드 로그를 이미 남겼는지 나타낸다.
    private bool didLogAddInCurrentMove;
    // 이동 행동 이벤트를 현재 구독 중인지 나타낸다.
    private bool subscribedMoveAction;

    /// <summary>
    /// 이동 위험 평가에 필요한 참조를 확인하고 경고 하이라이터를 준비한다.
    /// </summary>
    private void Awake()
    {
        if (!HasValidReference())
        {
            enabled = false;
            return;
        }

        moveAction = playerContext.GridMoveAction;
        warningHighlighter = CreateWarningHighlighter();
    }

    /// <summary>
    /// 이동 행동 이벤트를 구독한다.
    /// </summary>
    private void OnEnable()
    {
        TrySubscribeMoveAction(false);
    }

    /// <summary>
    /// 씬 초기화 순서 때문에 OnEnable에서 놓친 이동 행동 이벤트 구독을 시작 시점에 한 번 더 시도한다.
    /// </summary>
    private void Start()
    {
        TrySubscribeMoveAction(true);
    }

    /// <summary>
    /// 이동 행동 이벤트 구독을 시도한다.
    /// </summary>
    private void TrySubscribeMoveAction(bool logMissingRegistry)
    {
        if (subscribedMoveAction)
        {
            return;
        }

        if (!HasValidReference())
        {
            enabled = false;
            return;
        }

        if (EnemyRegistry.Instance == null)
        {
            if (logMissingRegistry)
            {
                Debug.LogError($"{nameof(GridMoveRiskEvaluator)} on {name}에는 이동 위험 평가에 사용할 씬의 {nameof(EnemyRegistry)}가 필요합니다.", this);
                enabled = false;
            }

            return;
        }

        moveAction = playerContext.GridMoveAction;
        moveAction.MovePathPreviewShown += HandleMovePathPreviewShown;
        moveAction.MovePathPreviewHidden += HidePreviewRisk;
        moveAction.MoveStepEntered += HandleMoveStepEntered;
        moveAction.MoveCompleted += HandleMoveCompleted;
        moveAction.MoveRangeHidden += HidePreviewRisk;
        subscribedMoveAction = true;
    }

    /// <summary>
    /// 이동 행동 이벤트 구독을 해제하고 표시를 정리한다.
    /// </summary>
    private void OnDisable()
    {
        if (moveAction != null)
        {
            moveAction.MovePathPreviewShown -= HandleMovePathPreviewShown;
            moveAction.MovePathPreviewHidden -= HidePreviewRisk;
            moveAction.MoveStepEntered -= HandleMoveStepEntered;
            moveAction.MoveCompleted -= HandleMoveCompleted;
            moveAction.MoveRangeHidden -= HidePreviewRisk;
        }

        subscribedMoveAction = false;
        HidePreviewRisk();
    }

    /// <summary>
    /// 컴포넌트가 제거될 때 런타임 경고 하이라이터를 정리한다.
    /// </summary>
    private void OnDestroy()
    {
        if (warningHighlighter != null)
        {
            Destroy(warningHighlighter.gameObject);
            warningHighlighter = null;
        }
    }

    /// <summary>
    /// 경로 미리보기 칸 목록에서 처음 감지되는 칸을 찾아 표시한다.
    /// </summary>
    private void HandleMovePathPreviewShown(IReadOnlyList<GridPosition> path)
    {
        if (TryFindFirstRiskPosition(path, out GridPosition riskPosition, out _))
        {
            ShowPreviewRisk(riskPosition);
            return;
        }

        HidePreviewRisk();
    }

    /// <summary>
    /// 실제 이동 중 감지 칸에 들어가면 1차 애드 로그를 남긴다.
    /// </summary>
    private void HandleMoveStepEntered(GridPosition stepPosition)
    {
        if (didLogAddInCurrentMove || !TryFindDetectingEnemy(stepPosition, out EnemyGridSight detectingEnemy))
        {
            return;
        }

        didLogAddInCurrentMove = true;
        if (logAddTriggered)
        {
            Debug.Log($"{nameof(GridMoveRiskEvaluator)}: {stepPosition} 칸에서 {detectingEnemy.name} 시야에 들어와 애드가 발생했습니다.", this);
        }
    }

    /// <summary>
    /// 이동 완료 후 이동 실행 중 애드 로그 상태를 초기화한다.
    /// </summary>
    private void HandleMoveCompleted(GridPosition _)
    {
        didLogAddInCurrentMove = false;
    }

    /// <summary>
    /// 경로에서 처음으로 적에게 감지되는 칸을 찾는다.
    /// </summary>
    private bool TryFindFirstRiskPosition(
        IReadOnlyList<GridPosition> path,
        out GridPosition riskPosition,
        out EnemyGridSight detectingEnemy)
    {
        if (path != null)
        {
            for (int i = 0; i < path.Count; i++)
            {
                if (TryFindDetectingEnemy(path[i], out detectingEnemy))
                {
                    riskPosition = path[i];
                    return true;
                }
            }
        }

        riskPosition = GridPosition.Zero;
        detectingEnemy = null;
        return false;
    }

    /// <summary>
    /// 지정한 칸을 감지할 수 있는 적 시야 컴포넌트를 찾는다.
    /// </summary>
    private bool TryFindDetectingEnemy(GridPosition position, out EnemyGridSight detectingEnemy)
    {
        if (EnemyRegistry.Instance == null)
        {
            detectingEnemy = null;
            return false;
        }

        IReadOnlyList<EnemyGridSight> enemySights = EnemyRegistry.Instance.GridSights;
        for (int i = 0; i < enemySights.Count; i++)
        {
            EnemyGridSight enemySight = enemySights[i];
            if (enemySight != null && enemySight.enabled && enemySight.CanDetect(position))
            {
                detectingEnemy = enemySight;
                return true;
            }
        }

        detectingEnemy = null;
        return false;
    }

    /// <summary>
    /// 첫 위험 칸 경고 하이라이트를 표시한다.
    /// </summary>
    private void ShowPreviewRisk(GridPosition riskPosition)
    {
        previewRiskPosition = riskPosition;

        if (!showWarningHighlight || warningHighlighter == null)
        {
            return;
        }

        warningPositions.Clear();
        warningPositions.Add(previewRiskPosition);
        warningHighlighter.Show(warningPositions);
    }

    /// <summary>
    /// 현재 경로 위험 경고 표시를 숨긴다.
    /// </summary>
    private void HidePreviewRisk()
    {
        warningPositions.Clear();

        if (warningHighlighter != null)
        {
            warningHighlighter.Hide();
        }
    }

    /// <summary>
    /// 첫 위험 칸 표시 전용 런타임 하이라이터를 만든다.
    /// </summary>
    private GridCellHighlighter CreateWarningHighlighter()
    {
        GameObject highlighterObject = new($"{nameof(GridMoveRiskEvaluator)}_WarningHighlighter");
        GridCellHighlighter highlighter = highlighterObject.AddComponent<GridCellHighlighter>();
        highlighter.ConfigureFallbackStyle(warningColor, warningCellScaleRatio, warningSortingOrder, warningZOffset);
        return highlighter;
    }

    /// <summary>
    /// 이동 위험 평가에 필요한 필수 참조가 연결되어 있는지 확인한다.
    /// </summary>
    private bool HasValidReference()
    {
        if (playerContext == null)
        {
            Debug.LogError($"{nameof(GridMoveRiskEvaluator)} on {name}에는 {nameof(PlayerContext)} 참조가 필요합니다.", this);
            return false;
        }

        if (!playerContext.HasValidReference())
        {
            return false;
        }

        if (playerContext.GridMoveAction == null)
        {
            Debug.LogError($"{nameof(GridMoveRiskEvaluator)} on {name}에는 {nameof(PlayerContext)}에 연결된 {nameof(PlayerGridMoveAction)} 참조가 필요합니다.", this);
            return false;
        }

        return true;
    }
}
