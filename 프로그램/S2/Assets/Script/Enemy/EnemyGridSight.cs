using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 적의 4방향 부채꼴 시야와 인접 근접 감지 칸을 계산한다.
/// 장애물 칸은 정면 시야에 포함하지 않고, 같은 레인에서 그 뒤 칸을 차단한다.
/// </summary>
[RequireComponent(typeof(GridActor))]
public class EnemyGridSight : MonoBehaviour
{
    [Header("Sight")]
    // 적 공통 참조와 시야 데이터를 제공하는 필수 Context다.
    [SerializeField] private EnemyContext enemyContext;
    // 현재 적이 바라보는 그리드 4방향이다.
    [SerializeField] private GridDirection facingDirection = GridDirection.Down;
    // true면 시야 칸이 갱신될 때 로그를 출력한다.
    [SerializeField] private bool logSightRefresh;

    // 현재 정면 시야와 근접 감지를 합친 최종 감지 칸 목록이다.
    private readonly List<GridPosition> detectedPositions = new();
    // 시야 칸 중복 등록을 막기 위한 재사용 집합이다.
    private readonly HashSet<GridPosition> detectedPositionSet = new();

    // 같은 오브젝트의 적 말 컴포넌트다.
    private GridActor actor;

    public GridDirection FacingDirection => facingDirection;
    public IReadOnlyList<GridPosition> DetectedPositions => detectedPositions;

    // 시야 칸 목록이 갱신될 때 발생한다.
    public event Action<IReadOnlyList<GridPosition>> SightRefreshed;

    /// <summary>
    /// 적 시야 계산에 필요한 참조와 데이터를 확인한다.
    /// </summary>
    private void Awake()
    {
        if (!HasValidReference() || !HasValidData())
        {
            enabled = false;
            return;
        }

        actor = enemyContext.GridActor;
        RefreshSight();
    }

    /// <summary>
    /// 다른 오브젝트의 Awake 순서 때문에 GridManager 준비 전 시야 계산이 비었을 수 있어 시작 시점에 한 번 더 갱신한다.
    /// </summary>
    private void Start()
    {
        RefreshSight();
    }

    /// <summary>
    /// 적이 바라보는 방향을 바꾸고 시야 칸을 다시 계산한다.
    /// </summary>
    public void SetFacingDirection(GridDirection nextDirection)
    {
        if (facingDirection == nextDirection)
        {
            return;
        }

        facingDirection = nextDirection;
        RefreshSight();
    }

    /// <summary>
    /// 현재 적 위치와 방향 기준으로 감지 칸 목록을 다시 계산한다.
    /// </summary>
    public void RefreshSight()
    {
        detectedPositions.Clear();
        detectedPositionSet.Clear();

        GridManager gridManager = GridManager.Instance;
        if (gridManager == null || actor == null)
        {
            return;
        }

        AddForwardConeSight(gridManager);
        AddAdjacentDetection(gridManager);

        if (logSightRefresh)
        {
            Debug.Log($"{nameof(EnemyGridSight)}: {name} 감지 칸 {detectedPositions.Count}개를 갱신했습니다.", this);
        }

        SightRefreshed?.Invoke(detectedPositions);
    }

    /// <summary>
    /// 지정한 칸이 현재 감지 칸 목록에 포함되는지 확인한다.
    /// </summary>
    public bool CanDetect(GridPosition targetPosition)
    {
        return detectedPositionSet.Contains(targetPosition);
    }

    /// <summary>
    /// 바라보는 방향 기준 정면 부채꼴 시야 칸을 추가한다.
    /// </summary>
    private void AddForwardConeSight(GridManager gridManager)
    {
        EnemyData data = enemyContext.EnemyData;
        GridPosition origin = actor.GridPosition;
        GridPosition forward = GridDirectionUtility.ToForwardOffset(facingDirection);
        GridPosition right = GridDirectionUtility.ToRightOffset(facingDirection);

        for (int lateralOffset = -(data.SightRange - 1); lateralOffset <= data.SightRange - 1; lateralOffset++)
        {
            AddForwardSightLane(gridManager, origin, forward, right, lateralOffset, data.SightRange);
        }
    }

    /// <summary>
    /// 부채꼴 시야의 한 레인을 가까운 칸부터 검사하고 장애물을 만나면 뒤를 차단한다.
    /// </summary>
    private void AddForwardSightLane(
        GridManager gridManager,
        GridPosition origin,
        GridPosition forward,
        GridPosition right,
        int lateralOffset,
        int sightRange)
    {
        int absoluteOffset = Mathf.Abs(lateralOffset);

        for (int forwardDistance = Mathf.Max(1, absoluteOffset + 1); forwardDistance <= sightRange; forwardDistance++)
        {
            GridPosition position = origin + Multiply(forward, forwardDistance) + Multiply(right, lateralOffset);
            if (!gridManager.IsInside(position))
            {
                break;
            }

            if (gridManager.IsBlocked(position))
            {
                break;
            }

            AddDetectedPosition(position);
        }
    }

    /// <summary>
    /// 바라보는 방향과 무관한 주변 근접 감지 칸을 추가한다.
    /// </summary>
    private void AddAdjacentDetection(GridManager gridManager)
    {
        EnemyData data = enemyContext.EnemyData;
        if (!data.UseAdjacentDetection)
        {
            return;
        }

        GridPosition origin = actor.GridPosition;
        int range = data.AdjacentDetectionRange;

        for (int x = -range; x <= range; x++)
        {
            for (int y = -range; y <= range; y++)
            {
                if (x == 0 && y == 0)
                {
                    continue;
                }

                GridPosition position = origin + new GridPosition(x, y);
                if (gridManager.IsInside(position))
                {
                    AddDetectedPosition(position);
                }
            }
        }
    }

    /// <summary>
    /// 중복 없이 감지 칸 목록에 좌표를 추가한다.
    /// </summary>
    private void AddDetectedPosition(GridPosition position)
    {
        if (detectedPositionSet.Add(position))
        {
            detectedPositions.Add(position);
        }
    }

    /// <summary>
    /// 그리드 좌표 오프셋을 정수 배율만큼 곱한다.
    /// </summary>
    private static GridPosition Multiply(GridPosition position, int multiplier)
    {
        return new GridPosition(position.x * multiplier, position.y * multiplier);
    }

    /// <summary>
    /// 적 시야 계산에 필요한 필수 참조가 연결되어 있는지 확인한다.
    /// </summary>
    private bool HasValidReference()
    {
        if (enemyContext == null)
        {
            Debug.LogError($"{nameof(EnemyGridSight)} on {name}에는 {nameof(EnemyContext)} 참조가 필요합니다.", this);
            return false;
        }

        if (!enemyContext.HasValidReference())
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// 적 시야 계산에 필요한 데이터가 유효한지 확인한다.
    /// </summary>
    private bool HasValidData()
    {
        EnemyData data = enemyContext.EnemyData;
        if (data == null)
        {
            Debug.LogError($"{nameof(EnemyGridSight)} on {name}에는 {nameof(EnemyData)} 참조가 필요합니다.", this);
            return false;
        }

        if (data.SightRange <= 0)
        {
            Debug.LogError($"{nameof(EnemyGridSight)} on {name}의 {nameof(EnemyData)} 정면 시야 거리는 0보다 커야 합니다.", this);
            return false;
        }

        if (data.AdjacentDetectionRange < 0)
        {
            Debug.LogError($"{nameof(EnemyGridSight)} on {name}의 {nameof(EnemyData)} 근접 감지 반경은 0 이상이어야 합니다.", this);
            return false;
        }

        return true;
    }
}
