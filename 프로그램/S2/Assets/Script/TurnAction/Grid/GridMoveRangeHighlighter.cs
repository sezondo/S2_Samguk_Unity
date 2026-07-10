using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// PlayerGridMoveAction의 이동 가능 칸 이벤트를 공용 그리드 하이라이트 표시기에 연결한다.
/// 이동 규칙은 처리하지 않고, 이동 행동의 표시 요청만 중계한다.
/// </summary>
[RequireComponent(typeof(PlayerGridMoveAction))]
public class GridMoveRangeHighlighter : MonoBehaviour
{
    [Header("Source")]
    // 이동 가능 칸 이벤트를 발생시키는 플레이어 이동 행동 컴포넌트다.
    [SerializeField] private PlayerGridMoveAction moveAction;

    [Header("Segment Style")]
    // AP 1개 구간 이동 가능 칸에 적용할 색이다.
    [SerializeField] private Color blueRangeColor = new(0.2f, 0.75f, 1f, 0.35f);
    // AP 2개 구간 이동 가능 칸에 적용할 색이다.
    [SerializeField] private Color yellowRangeColor = new(1f, 0.85f, 0.2f, 0.35f);
    // AP 3개 이상 구간 이동 가능 칸에 적용할 색이다.
    [SerializeField] private Color redRangeColor = new(1f, 0.25f, 0.2f, 0.35f);
    // 이동 가능 칸 하이라이트가 그리드 한 칸에서 차지할 비율이다.
    [SerializeField] private float cellScaleRatio = 0.85f;
    // 이동 가능 칸 하이라이트 렌더러의 정렬 순서다.
    [SerializeField] private int sortingOrder = 20;
    // 이동 가능 칸 하이라이트를 월드 좌표에서 살짝 앞뒤로 보낼 때 쓰는 Z 오프셋이다.
    [SerializeField] private float zOffset = -0.05f;

    // AP 1개 구간 표시를 담당하는 런타임 하이라이터다.
    private GridCellHighlighter blueRangeHighlighter;
    // AP 2개 구간 표시를 담당하는 런타임 하이라이터다.
    private GridCellHighlighter yellowRangeHighlighter;
    // AP 3개 이상 구간 표시를 담당하는 런타임 하이라이터다.
    private GridCellHighlighter redRangeHighlighter;

    /// <summary>
    /// 이동 범위 하이라이트 연결에 필요한 필수 참조를 확인한다.
    /// </summary>
    private void Awake()
    {
        if (!HasValidReference())
        {
            enabled = false;
            return;
        }

        ConfigureHighlighters();
    }

    /// <summary>
    /// 이동 가능 칸 표시 이벤트를 구독한다.
    /// </summary>
    private void OnEnable()
    {
        if (!HasValidReference())
        {
            enabled = false;
            return;
        }

        ConfigureHighlighters();
        moveAction.MoveRangeSegmentsShown += ShowMoveRangeSegments;
        moveAction.MoveRangeHidden += HideMoveRange;
    }

    /// <summary>
    /// 이동 가능 칸 표시 이벤트 구독을 해제하고 표시를 정리한다.
    /// </summary>
    private void OnDisable()
    {
        if (moveAction != null)
        {
            moveAction.MoveRangeSegmentsShown -= ShowMoveRangeSegments;
            moveAction.MoveRangeHidden -= HideMoveRange;
        }

        HideMoveRange();
    }

    /// <summary>
    /// 컴포넌트가 제거될 때 런타임 하이라이터를 정리한다.
    /// </summary>
    private void OnDestroy()
    {
        if (blueRangeHighlighter != null)
        {
            Destroy(blueRangeHighlighter.gameObject);
            blueRangeHighlighter = null;
        }

        if (yellowRangeHighlighter != null)
        {
            Destroy(yellowRangeHighlighter.gameObject);
            yellowRangeHighlighter = null;
        }

        if (redRangeHighlighter != null)
        {
            Destroy(redRangeHighlighter.gameObject);
            redRangeHighlighter = null;
        }
    }

    /// <summary>
    /// 이동 행동이 계산한 AP 구간별 이동 가능 칸 목록을 각 하이라이트 표시기에 전달한다.
    /// </summary>
    private void ShowMoveRangeSegments(
        IReadOnlyList<GridPosition> bluePositions,
        IReadOnlyList<GridPosition> yellowPositions,
        IReadOnlyList<GridPosition> redPositions)
    {
        blueRangeHighlighter.Show(bluePositions);
        yellowRangeHighlighter.Show(yellowPositions);
        redRangeHighlighter.Show(redPositions);
    }

    /// <summary>
    /// 공용 하이라이트 표시기에 현재 이동 가능 칸 표시를 숨기도록 요청한다.
    /// </summary>
    private void HideMoveRange()
    {
        if (blueRangeHighlighter != null)
        {
            blueRangeHighlighter.Hide();
        }

        if (yellowRangeHighlighter != null)
        {
            yellowRangeHighlighter.Hide();
        }

        if (redRangeHighlighter != null)
        {
            redRangeHighlighter.Hide();
        }
    }

    /// <summary>
    /// 이동 범위 구간별 하이라이터를 준비하고 색상 기준을 적용한다.
    /// </summary>
    private void ConfigureHighlighters()
    {
        blueRangeHighlighter = EnsureRuntimeHighlighter(blueRangeHighlighter, "Blue", blueRangeColor, zOffset);
        yellowRangeHighlighter = EnsureRuntimeHighlighter(yellowRangeHighlighter, "Yellow", yellowRangeColor, zOffset - 0.01f);
        redRangeHighlighter = EnsureRuntimeHighlighter(redRangeHighlighter, "Red", redRangeColor, zOffset - 0.02f);
    }

    /// <summary>
    /// AP 구간 표시용 런타임 하이라이터를 생성하거나 기존 인스턴스의 표시 기준을 갱신한다.
    /// </summary>
    private GridCellHighlighter EnsureRuntimeHighlighter(GridCellHighlighter highlighter, string segmentName, Color color, float segmentZOffset)
    {
        if (highlighter == null)
        {
            GameObject highlighterObject = new($"{nameof(GridMoveRangeHighlighter)}_{segmentName}RangeHighlighter");
            highlighter = highlighterObject.AddComponent<GridCellHighlighter>();
        }

        highlighter.ConfigureFallbackStyle(color, cellScaleRatio, sortingOrder, segmentZOffset);
        return highlighter;
    }

    /// <summary>
    /// 이동 범위 하이라이트 연결에 필요한 필수 참조가 연결되어 있는지 확인한다.
    /// </summary>
    private bool HasValidReference()
    {
        if (moveAction == null)
        {
            Debug.LogError($"{nameof(GridMoveRangeHighlighter)} on {name}에는 {nameof(PlayerGridMoveAction)} 참조가 필요합니다.", this);
            return false;
        }

        return true;
    }
}
