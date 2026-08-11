using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 적의 4방향 부채꼴 시야와 장애물을 무시하는 인접 청각 감지 칸을 계산한다.
/// 정면 시야는 각 후보 칸까지의 시선을 검사해 고정·동적 구조물 뒤쪽을 제외한다.
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
    // 도깨비검 기척을 감지할 수 있는 별도 360도 원형 칸 목록이다.
    private readonly List<GridPosition> swordDetectionPositions = new();
    // 도깨비검 감지 칸 중복 등록을 막는 집합이다.
    private readonly HashSet<GridPosition> swordDetectionPositionSet = new();

    // 같은 오브젝트의 적 말 컴포넌트다.
    private GridActor actor;
    // 동적 시야 차단 변경 이벤트를 구독한 그리드 매니저다.
    private GridManager subscribedGridManager;

    public GridDirection FacingDirection => facingDirection;
    public IReadOnlyList<GridPosition> DetectedPositions => detectedPositions;
    public IReadOnlyList<GridPosition> SwordDetectionPositions => swordDetectionPositions;

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
        TrySubscribeSightBlockingChanged();
        RefreshSight();
    }

    /// <summary>
    /// 컴포넌트가 다시 활성화될 때 동적 시야 차단 변경 이벤트 구독을 복원한다.
    /// </summary>
    private void OnEnable()
    {
        TrySubscribeSightBlockingChanged();
    }

    /// <summary>
    /// 다른 오브젝트의 Awake 순서 때문에 GridManager 준비 전 시야 계산이 비었을 수 있어 시작 시점에 한 번 더 갱신한다.
    /// </summary>
    private void Start()
    {
        TrySubscribeSightBlockingChanged();
        RefreshSight();
    }

    /// <summary>
    /// 컴포넌트가 비활성화될 때 동적 시야 차단 변경 이벤트 구독을 해제한다.
    /// </summary>
    private void OnDisable()
    {
        if (subscribedGridManager != null)
        {
            subscribedGridManager.SightBlockingChanged -= HandleSightBlockingChanged;
            subscribedGridManager = null;
        }
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
        swordDetectionPositions.Clear();
        swordDetectionPositionSet.Clear();
        TrySubscribeSightBlockingChanged();

        GridManager gridManager = GridManager.Instance;
        if (gridManager == null || actor == null)
        {
            return;
        }

        AddForwardConeSight(gridManager);
        AddAdjacentDetection(gridManager);
        AddSwordDetectionArea(gridManager);

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
        if (!enemyContext.IsAlive)
        {
            return false;
        }

        return detectedPositionSet.Contains(targetPosition);
    }

    /// <summary>
    /// 지정한 플레이어 칸이 현재 전방 시야 또는 근접 절대 감지에 들어오는지 확인한다.
    /// </summary>
    public bool CanDetectPlayer(GridPosition targetPosition)
    {
        return CanDetect(targetPosition);
    }

    /// <summary>
    /// 지정한 도깨비검 칸이 현재 별도 기척 감지 범위에 들어오는지 확인한다.
    /// </summary>
    public bool CanDetectSword(GridPosition swordPosition)
    {
        return enemyContext.IsAlive && swordDetectionPositionSet.Contains(swordPosition);
    }

    /// <summary>
    /// 현재 적 위치를 기준으로 벽과 닫힌 문에 막히는 도깨비검 원형 감지 칸을 추가한다.
    /// </summary>
    private void AddSwordDetectionArea(GridManager gridManager)
    {
        GridPosition origin = actor.GridPosition;
        int range = enemyContext.EnemyData.SwordDetectionRange;
        int squaredRange = range * range;

        for (int xOffset = -range; xOffset <= range; xOffset++)
        {
            for (int yOffset = -range; yOffset <= range; yOffset++)
            {
                if (xOffset == 0 && yOffset == 0 || xOffset * xOffset + yOffset * yOffset > squaredRange)
                {
                    continue;
                }

                GridPosition position = origin + new GridPosition(xOffset, yOffset);
                if (gridManager.IsInside(position) &&
                    GridLineOfSight.HasLineOfSight(gridManager, origin, position) &&
                    swordDetectionPositionSet.Add(position))
                {
                    swordDetectionPositions.Add(position);
                }
            }
        }
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

        for (int forwardDistance = 1; forwardDistance <= data.SightRange; forwardDistance++)
        {
            int maximumLateralOffset = forwardDistance - 1;
            for (int lateralOffset = -maximumLateralOffset; lateralOffset <= maximumLateralOffset; lateralOffset++)
            {
                GridPosition position =
                    origin +
                    Multiply(forward, forwardDistance) +
                    Multiply(right, lateralOffset);

                if (gridManager.IsInside(position) &&
                    GridLineOfSight.HasLineOfSight(gridManager, origin, position))
                {
                    AddDetectedPosition(position);
                }
            }
        }
    }

    /// <summary>
    /// 바라보는 방향과 장애물에 무관하게 주변 8방향의 청각 근접 감지 칸을 추가한다.
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
    /// 현재 씬의 동적 시야 차단 변경 이벤트를 구독한다.
    /// </summary>
    private void TrySubscribeSightBlockingChanged()
    {
        if (subscribedGridManager != null)
        {
            return;
        }

        GridManager gridManager = GridManager.Instance;
        if (gridManager == null)
        {
            return;
        }

        gridManager.SightBlockingChanged -= HandleSightBlockingChanged;
        gridManager.SightBlockingChanged += HandleSightBlockingChanged;
        subscribedGridManager = gridManager;
    }

    /// <summary>
    /// 문 개방처럼 동적 시야 차단 상태가 바뀌면 현재 시야 칸을 다시 계산한다.
    /// </summary>
    private void HandleSightBlockingChanged()
    {
        RefreshSight();
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

        if (data.SwordDetectionRange <= 0)
        {
            Debug.LogError($"{nameof(EnemyGridSight)} on {name}의 도깨비검 감지 거리는 0보다 커야 합니다.", this);
            return false;
        }

        return true;
    }
}
