using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// PlayerGridMoveAction의 이동 가능 칸 이벤트를 공용 그리드 하이라이트 표시기에 연결한다.
/// 이동 규칙은 처리하지 않고, 이동 행동의 표시 요청만 중계한다.
/// </summary>
[RequireComponent(typeof(PlayerGridMoveAction))]
[RequireComponent(typeof(GridCellHighlighter))]
public class GridMoveRangeHighlighter : MonoBehaviour
{
    [Header("Source")]
    // 이동 가능 칸 이벤트를 발생시키는 플레이어 이동 행동 컴포넌트다.
    [SerializeField] private PlayerGridMoveAction moveAction;
    // 실제 칸 표시와 풀링을 담당하는 공용 하이라이트 컴포넌트다.
    [SerializeField] private GridCellHighlighter cellHighlighter;

    /// <summary>
    /// 이동 범위 하이라이트 연결에 필요한 필수 참조를 확인한다.
    /// </summary>
    private void Awake()
    {
        if (!HasValidReference())
        {
            enabled = false;
        }
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

        moveAction.MoveRangeShown += ShowMoveRange;
        moveAction.MoveRangeHidden += HideMoveRange;
    }

    /// <summary>
    /// 이동 가능 칸 표시 이벤트 구독을 해제하고 표시를 정리한다.
    /// </summary>
    private void OnDisable()
    {
        if (moveAction != null)
        {
            moveAction.MoveRangeShown -= ShowMoveRange;
            moveAction.MoveRangeHidden -= HideMoveRange;
        }

        HideMoveRange();
    }

    /// <summary>
    /// 이동 행동이 계산한 이동 가능 칸 목록을 공용 하이라이트 표시기에 전달한다.
    /// </summary>
    private void ShowMoveRange(IReadOnlyList<GridPosition> positions)
    {
        cellHighlighter.Show(positions);
    }

    /// <summary>
    /// 공용 하이라이트 표시기에 현재 이동 가능 칸 표시를 숨기도록 요청한다.
    /// </summary>
    private void HideMoveRange()
    {
        if (cellHighlighter != null)
        {
            cellHighlighter.Hide();
        }
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

        if (cellHighlighter == null)
        {
            Debug.LogError($"{nameof(GridMoveRangeHighlighter)} on {name}에는 {nameof(GridCellHighlighter)} 참조가 필요합니다.", this);
            return false;
        }

        return true;
    }
}
