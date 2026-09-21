using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 살아 있는 플레이어 조작 유닛의 시야를 합산하고 현재 시야와 누적 탐색 칸을 관리한다.
/// 화면 표시는 직접 바꾸지 않고 시야 스냅샷 연출 이벤트만 생성한다.
/// </summary>
public class PlayerVisionManager : MonoBehaviour, IActionLogicEventHandler
{
    // 플레이어 유닛이 벽 모서리와 무관하게 항상 인식하는 주변 8칸의 거리다.
    private const int GuaranteedAdjacentVisionRange = 1;

    [Header("Reference")]
    // 플레이어 시야 계산에 필요한 씬 참조 주머니다.
    [SerializeField] private PlayerVisionContext context;

    [Header("Log")]
    // true면 현재 시야와 탐색 칸 갱신 결과를 Unity 콘솔에 출력한다.
    [SerializeField] private bool logVisionRefresh;

    // 현재 플레이어 유닛들의 합산 시야 칸이다.
    private readonly HashSet<GridPosition> visiblePositions = new();
    // 전투 중 한 번이라도 플레이어 시야에 들어온 누적 탐색 칸이다.
    private readonly HashSet<GridPosition> exploredPositions = new();
    // 새 시야 계산 결과를 만드는 재사용 버퍼다.
    private readonly HashSet<GridPosition> nextVisiblePositions = new();
    // 시야 스냅샷 생성 시점에 실제로 보이는 적 Actor를 담는 재사용 버퍼다.
    private readonly HashSet<GridActor> visibleEnemyActors = new();
    // 이번 논리 시점의 시야 원점 목록이다. 연출이 최신 유닛 위치를 잘못 읽지 않게 스냅샷에 복사한다.
    private readonly List<PlayerVisionSource> visionSources = new();
    // 벽과 닫힌 문 차단 칸을 저장하는 스냅샷 작성용 버퍼다.
    private readonly HashSet<GridPosition> sightBlockers = new();

    public static PlayerVisionManager Instance { get; private set; }
    public PlayerVisionSnapshot CurrentSnapshot { get; private set; }

    // 큐 재생 전 초기화처럼 즉시 화면 동기화가 필요한 시야 변경을 알린다.
    public event Action<PlayerVisionSnapshot> VisionChangedImmediately;

    /// <summary>
    /// 씬의 단일 플레이어 시야 매니저를 등록하고 필수 참조를 검사한다.
    /// </summary>
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"{nameof(PlayerVisionManager)}: 이미 인스턴스가 있습니다. 중복 오브젝트 {name}의 컴포넌트를 비활성화합니다.", this);
            enabled = false;
            return;
        }

        if (!HasValidReference())
        {
            enabled = false;
            return;
        }

        Instance = this;
    }

    /// <summary>
    /// 이동·사망 논리 이벤트와 플레이어 유닛 등록 변경을 구독한다.
    /// </summary>
    private void OnEnable()
    {
        ActionLogicEventBus.Register(this);
        TrySubscribeUnitRegistry();
    }

    /// <summary>
    /// 씬 초기화 순서가 끝난 뒤 최초 합산 시야를 계산한다.
    /// </summary>
    private void Start()
    {
        TrySubscribeUnitRegistry();
        RefreshVision(null);
    }

    /// <summary>
    /// 논리 이벤트와 플레이어 유닛 등록 변경 구독을 해제한다.
    /// </summary>
    private void OnDisable()
    {
        ActionLogicEventBus.Unregister(this);
        UnsubscribeUnitRegistry();
    }

    /// <summary>
    /// 현재 인스턴스가 제거될 때 전역 참조를 정리한다.
    /// </summary>
    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// 지정한 칸이 현재 플레이어 합산 시야 안인지 확인한다.
    /// </summary>
    public bool IsVisible(GridPosition position)
    {
        return visiblePositions.Contains(position);
    }

    /// <summary>
    /// 지정한 칸의 현재 플레이어 탐색 상태를 반환한다.
    /// </summary>
    public GridVisibilityState GetVisibilityState(GridPosition position)
    {
        if (visiblePositions.Contains(position))
        {
            return GridVisibilityState.Visible;
        }

        return exploredPositions.Contains(position)
            ? GridVisibilityState.Explored
            : GridVisibilityState.Unexplored;
    }

    /// <summary>
    /// 현재 플레이어 유닛 위치와 시야 거리로 합산 시야를 다시 계산하고 광원·차단물도 함께 보존한다.
    /// 행동 문맥이 있으면 칸 단위 순서를 보존하도록 연출 큐에 스냅샷을 추가한다.
    /// </summary>
    public bool RefreshVision(ActionResolutionContext resolutionContext)
    {
        if (!HasValidReference())
        {
            return false;
        }

        nextVisiblePositions.Clear();
        visionSources.Clear();
        IReadOnlyList<TacticalUnitContext> playerUnits = context.TacticalUnitRegistry.PlayerControllableUnits;
        for (int i = 0; i < playerUnits.Count; i++)
        {
            TacticalUnitContext unit = playerUnits[i];
            if (unit == null || !unit.IsAlive)
            {
                continue;
            }

            if (!TryAddUnitVision(unit))
            {
                return false;
            }
        }

        bool visibleChanged = !visiblePositions.SetEquals(nextVisiblePositions);
        visiblePositions.Clear();
        visiblePositions.UnionWith(nextVisiblePositions);

        int previousExploredCount = exploredPositions.Count;
        exploredPositions.UnionWith(visiblePositions);
        bool exploredChanged = previousExploredCount != exploredPositions.Count;
        CaptureSightBlockers();
        if (!visibleChanged && !exploredChanged && CurrentSnapshot != null &&
            CurrentSnapshot.HasSameGeometry(visionSources, sightBlockers))
        {
            return true;
        }

        CaptureVisibleEnemyActors();
        CurrentSnapshot = new PlayerVisionSnapshot(visiblePositions, exploredPositions, visibleEnemyActors,
            visionSources, sightBlockers);
        if (resolutionContext != null)
        {
            resolutionContext.EnqueuePresentation(PresentationEvent.PlayerVisionChanged(CurrentSnapshot, "플레이어 시야 갱신 연출"));
        }
        else
        {
            VisionChangedImmediately?.Invoke(CurrentSnapshot);
        }

        if (logVisionRefresh)
        {
            Debug.Log($"{nameof(PlayerVisionManager)}: 현재 시야 {visiblePositions.Count}칸, 누적 탐색 {exploredPositions.Count}칸으로 갱신했습니다.", this);
        }

        return true;
    }

    /// <summary>
    /// 플레이어 한 명의 360도 원형 시야와 배치된 도깨비검 시야를 합산 버퍼에 추가한다.
    /// </summary>
    private bool TryAddUnitVision(TacticalUnitContext unit)
    {
        if (unit.UnitData == null || unit.GridActor == null)
        {
            Debug.LogError($"{nameof(PlayerVisionManager)}: {unit.name} 유닛의 데이터 또는 GridActor 참조가 비어 있습니다.", unit);
            return false;
        }

        int visionRange = unit.UnitData.VisionRange;
        if (visionRange <= 0)
        {
            Debug.LogError($"{nameof(PlayerVisionManager)}: {unit.name} 유닛의 시야 거리는 0보다 커야 합니다. 현재 값: {visionRange}", unit.UnitData);
            return false;
        }

        GridPosition origin = unit.GridActor.GridPosition;
        AddVisionArea(origin, visionRange, true);

        if (!unit.HasAbility(UnitAbilityType.Sword))
        {
            return true;
        }

        PlayerSwordState swordState = unit.SwordState;
        if (swordState == null)
        {
            Debug.LogError($"{nameof(PlayerVisionManager)}: {unit.name} 검 능력 유닛의 {nameof(PlayerSwordState)} 참조가 비어 있습니다.", unit);
            return false;
        }

        int swordVisionRange = unit.UnitData.SwordVisionRange;
        if (swordVisionRange <= 0)
        {
            Debug.LogError($"{nameof(PlayerVisionManager)}: {unit.name} 도깨비검 시야 거리는 0보다 커야 합니다. 현재 값: {swordVisionRange}", unit.UnitData);
            return false;
        }

        if (!swordState.IsRecalled)
        {
            AddVisionArea(swordState.CurrentPosition, swordVisionRange, false);
        }

        return true;
    }

    /// <summary>
    /// 연속 안개용 원점을 기록하고 원형 논리 시야를 합산한다. 기존 플레이어 주변 8칸 예외를 유지한다.
    /// </summary>
    private void AddVisionArea(GridPosition origin, int visionRange, bool guaranteeAdjacentVision)
    {
        visionSources.Add(new PlayerVisionSource(origin, visionRange, guaranteeAdjacentVision));
        GridManager gridManager = context.GridManager;
        int squaredRange = visionRange * visionRange;
        for (int xOffset = -visionRange; xOffset <= visionRange; xOffset++)
        {
            for (int yOffset = -visionRange; yOffset <= visionRange; yOffset++)
            {
                bool isGuaranteedAdjacent = guaranteeAdjacentVision &&
                    Mathf.Max(Mathf.Abs(xOffset), Mathf.Abs(yOffset)) <= GuaranteedAdjacentVisionRange;
                if (!isGuaranteedAdjacent && xOffset * xOffset + yOffset * yOffset > squaredRange)
                {
                    continue;
                }

                GridPosition target = origin + new GridPosition(xOffset, yOffset);
                if (!gridManager.IsInside(target))
                {
                    continue;
                }

                if (CombatTargetRules.CanObserve(gridManager, origin, target, visionRange, guaranteeAdjacentVision))
                {
                    nextVisiblePositions.Add(target);
                }
            }
        }
    }

    /// <summary>현재 보드의 벽과 동적 차단물만 복사한다. 낮은 장애물은 기존처럼 시야를 막지 않는다.</summary>
    private void CaptureSightBlockers()
    {
        sightBlockers.Clear();
        GridManager grid = context.GridManager;
        for (int y = 0; y < grid.Height; y++)
        for (int x = 0; x < grid.Width; x++)
        {
            GridPosition position = new(x, y);
            if (grid.IsSightBlocked(position)) sightBlockers.Add(position);
        }
    }

    /// <summary>
    /// 현재 논리 시점의 적 위치를 기준으로 실제 시야 안에 있는 적 Actor를 스냅샷 버퍼에 기록한다.
    /// </summary>
    private void CaptureVisibleEnemyActors()
    {
        visibleEnemyActors.Clear();
        IReadOnlyList<EnemyContext> enemies = context.EnemyRegistry.Enemies;
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyContext enemy = enemies[i];
            if (enemy != null && enemy.GridActor != null &&
                visiblePositions.Contains(enemy.GridActor.GridPosition))
            {
                visibleEnemyActors.Add(enemy.GridActor);
            }
        }
    }

    /// <summary>
    /// 플레이어 이동·사망과 도깨비검 배치 상태 변경 이벤트를 처리할 수 있는지 확인한다.
    /// </summary>
    public bool CanHandle(IActionLogicEvent logicEvent)
    {
        return logicEvent is MoveStepEnteredLogicEvent ||
            logicEvent is ActorDiedLogicEvent ||
            logicEvent is SwordThrownLogicEvent ||
            logicEvent is SwordRecalledLogicEvent ||
            logicEvent is HackCompletedLogicEvent;
    }

    /// <summary>
    /// 플레이어 위치·생존 구성이나 도깨비검 위치가 바뀐 논리 시점에 시야 스냅샷을 갱신한다.
    /// </summary>
    public void Handle(IActionLogicEvent logicEvent, ActionResolutionContext resolutionContext)
    {
        GridActor changedActor = logicEvent switch
        {
            MoveStepEnteredLogicEvent moved => moved.Actor,
            ActorDiedLogicEvent died => died.DeadActor,
            SwordThrownLogicEvent thrown => thrown.Actor,
            SwordRecalledLogicEvent recalled => recalled.Actor,
            HackCompletedLogicEvent hackCompleted => hackCompleted.Actor,
            _ => null,
        };

        if (changedActor != null &&
            context.TacticalUnitRegistry.TryGetPlayerControllableUnit(changedActor, out _))
        {
            RefreshVision(resolutionContext);
        }
    }

    /// <summary>
    /// 플레이어 전술 유닛 등록소의 구성 변경 이벤트를 구독한다.
    /// </summary>
    private void TrySubscribeUnitRegistry()
    {
        if (context == null || context.TacticalUnitRegistry == null)
        {
            return;
        }

        context.TacticalUnitRegistry.PlayerUnitRegistered -= HandlePlayerUnitChanged;
        context.TacticalUnitRegistry.PlayerUnitRegistered += HandlePlayerUnitChanged;
        context.TacticalUnitRegistry.PlayerUnitUnregistered -= HandlePlayerUnitChanged;
        context.TacticalUnitRegistry.PlayerUnitUnregistered += HandlePlayerUnitChanged;
    }

    /// <summary>
    /// 플레이어 전술 유닛 등록소의 구성 변경 이벤트 구독을 해제한다.
    /// </summary>
    private void UnsubscribeUnitRegistry()
    {
        if (context == null || context.TacticalUnitRegistry == null)
        {
            return;
        }

        context.TacticalUnitRegistry.PlayerUnitRegistered -= HandlePlayerUnitChanged;
        context.TacticalUnitRegistry.PlayerUnitUnregistered -= HandlePlayerUnitChanged;
    }

    /// <summary>
    /// 플레이어 유닛 등록이나 해제 직후 초기 시야를 다시 계산한다.
    /// </summary>
    private void HandlePlayerUnitChanged(TacticalUnitContext _)
    {
        RefreshVision(null);
    }

    /// <summary>
    /// 플레이어 시야 계산에 필요한 Context와 내부 참조가 연결되어 있는지 확인한다.
    /// </summary>
    public bool HasValidReference()
    {
        if (context == null)
        {
            Debug.LogError($"{nameof(PlayerVisionManager)} on {name}에는 {nameof(PlayerVisionContext)} 참조가 필요합니다.", this);
            return false;
        }

        return context.HasValidReference();
    }
}
