using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 디자이너가 씬에 배치하는 순찰 지점과 연결 가능한 다음 지점을 정의한다.
/// </summary>
public class PatrolPoint : MonoBehaviour
{
    [Header("Patrol Point")]
    // 이 지점에 도착한 적들이 기본으로 바라볼 방향이다.
    [SerializeField] private GridDirection lookDirection = GridDirection.Down;
    // 도착 후 다음 지점으로 출발하기 전에 대기할 적 턴 수다.
    [SerializeField] private int waitTurns;
    // 이 지점에서 선택할 수 있는 연결 지점 목록이다.
    [SerializeField] private List<PatrolPoint> connectedPoints = new();

    public GridDirection LookDirection => lookDirection;
    public int WaitTurns => waitTurns;
    public IReadOnlyList<PatrolPoint> ConnectedPoints => connectedPoints;

    /// <summary>
    /// 현재 Transform의 월드 위치를 씬 GridPosition으로 변환한다.
    /// </summary>
    public bool TryGetGridPosition(out GridPosition position)
    {
        GridManager gridManager = GridManager.Instance;
        if (gridManager == null)
        {
            position = GridPosition.Zero;
            return false;
        }

        position = gridManager.WorldToGrid(transform.position);
        return gridManager.IsInside(position);
    }

    /// <summary>
    /// 순찰 지점의 대기 턴과 연결 목록이 유효한지 확인한다.
    /// </summary>
    public bool HasValidData()
    {
        if (waitTurns < 0)
        {
            Debug.LogError($"{nameof(PatrolPoint)} on {name}의 대기 턴은 0 이상이어야 합니다.", this);
            return false;
        }

        HashSet<PatrolPoint> uniquePoints = new();
        for (int i = 0; i < connectedPoints.Count; i++)
        {
            PatrolPoint point = connectedPoints[i];
            if (point == null || point == this || !uniquePoints.Add(point))
            {
                Debug.LogError($"{nameof(PatrolPoint)} on {name}의 연결 지점에 빈 값, 자기 자신 또는 중복 참조가 있습니다.", this);
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Scene 뷰에서 순찰 지점과 연결 관계를 확인할 수 있게 표시한다.
    /// </summary>
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.2f, 0.85f, 1f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, 0.2f);
        Vector3 forward = GridDirectionUtility.ToForwardOffset(lookDirection) switch
        {
            GridPosition value when value == GridPosition.Up => Vector3.up,
            GridPosition value when value == GridPosition.Down => Vector3.down,
            GridPosition value when value == GridPosition.Left => Vector3.left,
            _ => Vector3.right,
        };
        Gizmos.DrawLine(transform.position, transform.position + forward * 0.45f);

        Gizmos.color = new Color(0.2f, 0.85f, 1f, 0.45f);
        for (int i = 0; i < connectedPoints.Count; i++)
        {
            if (connectedPoints[i] != null)
            {
                Gizmos.DrawLine(transform.position, connectedPoints[i].transform.position);
            }
        }
    }
}
