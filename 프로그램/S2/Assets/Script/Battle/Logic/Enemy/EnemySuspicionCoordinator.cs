using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 도깨비검 기척 감지와 단일 단계 의심 전파를 계산해 적 조사 행동을 시작한다.
/// </summary>
public class EnemySuspicionCoordinator : MonoBehaviour, IActionLogicEventHandler
{
    [Header("Log")]
    // true면 직접 감지와 의심 전파 결과를 출력한다.
    [SerializeField] private bool logSuspicionSpread = true;

    private readonly List<EnemyContext> directDetectors = new();
    private readonly HashSet<EnemyContext> processedEnemies = new();

    /// <summary>
    /// 검 위치 변화 이벤트를 받기 위해 등록한다.
    /// </summary>
    private void OnEnable()
    {
        ActionLogicEventBus.Register(this);
    }

    /// <summary>
    /// 검 위치 변화 이벤트 등록을 해제한다.
    /// </summary>
    private void OnDisable()
    {
        ActionLogicEventBus.Unregister(this);
    }

    /// <summary>
    /// 시작 시 의심 계산에 필요한 등록소를 확인한다.
    /// </summary>
    private void Start()
    {
        if (EnemyRegistry.Instance == null || TacticalUnitRegistry.Instance == null)
        {
            Debug.LogError($"{nameof(EnemySuspicionCoordinator)} on {name}에는 EnemyRegistry와 TacticalUnitRegistry가 필요합니다.", this);
            enabled = false;
        }
    }

    /// <summary>
    /// 검 투척·해킹과 적 이동 완료 중 검 기척이 달라질 수 있는 이벤트를 처리한다.
    /// </summary>
    public bool CanHandle(IActionLogicEvent logicEvent)
    {
        return logicEvent is SwordThrownLogicEvent ||
            logicEvent is HackCompletedLogicEvent ||
            logicEvent is MoveCompletedLogicEvent;
    }

    /// <summary>
    /// 이벤트에서 검 위치와 소유자를 찾아 현재 감지 가능한 적에게 의심을 발생시킨다.
    /// </summary>
    public void Handle(IActionLogicEvent logicEvent, ActionResolutionContext context)
    {
        switch (logicEvent)
        {
            case SwordThrownLogicEvent thrown:
                HandleSwordPositionChanged(thrown.Actor, thrown.ToPosition, true, context);
                break;
            case HackCompletedLogicEvent hacked:
                HandleSwordPositionChanged(hacked.Actor, hacked.ExecutionPosition, false, context);
                break;
            case MoveCompletedLogicEvent moved:
                if (TryFindEnemy(moved.Actor, out _))
                {
                    DetectAllDeployedSwords(context);
                }
                break;
        }
    }

    /// <summary>
    /// 바뀐 검 위치를 직접 감지한 적과 전파 대상 적을 계산한다.
    /// </summary>
    private void HandleSwordPositionChanged(GridActor ownerActor, GridPosition swordPosition, bool excludeDirectHitGroup, ActionResolutionContext context)
    {
        PlayerSwordState swordState = TryGetSwordState(ownerActor);
        if (excludeDirectHitGroup && GridManager.Instance.TryGetActorAt(swordPosition, out GridActor targetActor) &&
            TryFindEnemy(targetActor, out _))
        {
            // 적 직접 타격은 피해 처리에서 곧바로 Alerted와 애드 전파를 만들므로 의심 판정을 만들지 않는다.
            return;
        }

        DetectSword(swordPosition, swordState, true, context);
    }

    /// <summary>
    /// 현재 배치된 모든 플레이어 도깨비검을 다시 검사한다.
    /// </summary>
    private void DetectAllDeployedSwords(ActionResolutionContext context)
    {
        IReadOnlyList<TacticalUnitContext> players = TacticalUnitRegistry.Instance.PlayerControllableUnits;
        for (int i = 0; i < players.Count; i++)
        {
            TacticalUnitContext player = players[i];
            if (player == null || !player.IsAlive || player.SwordState == null || player.SwordState.IsRecalled)
            {
                continue;
            }

            DetectSword(player.SwordState.CurrentPosition, player.SwordState, false, context);
        }
    }

    /// <summary>
    /// 검을 직접 감지한 적을 정렬하고 각 직접 감지자를 기준으로 한 번만 의심을 전파한다.
    /// </summary>
    private void DetectSword(GridPosition swordPosition, Object sourceObject, bool refreshExistingSuspicion, ActionResolutionContext context)
    {
        directDetectors.Clear();
        IReadOnlyList<EnemyContext> enemies = EnemyRegistry.Instance.Enemies;
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyContext enemy = enemies[i];
            if (CanBecomeSuspicious(enemy) &&
                (refreshExistingSuspicion || enemy.AlertState.IsUnaware) &&
                enemy.GridSight.CanDetectSword(swordPosition))
            {
                directDetectors.Add(enemy);
            }
        }

        directDetectors.Sort((left, right) =>
            left.GridActor.GridPosition.ManhattanDistanceTo(swordPosition)
                .CompareTo(right.GridActor.GridPosition.ManhattanDistanceTo(swordPosition)));

        processedEnemies.Clear();
        for (int i = 0; i < directDetectors.Count; i++)
        {
            EnemyContext detector = directDetectors[i];
            if (processedEnemies.Contains(detector))
            {
                continue;
            }

            SpreadFromDetector(detector, swordPosition, sourceObject, context);
        }
    }

    /// <summary>
    /// 직접 감지자, 같은 그룹 전체와 거리 안 다른 그룹 전체에 단일 단계로 의심을 전달한다.
    /// </summary>
    private void SpreadFromDetector(EnemyContext detector, GridPosition position, Object sourceObject, ActionResolutionContext context)
    {
        ApplySuspicion(detector, detector, position, sourceObject, EnemySuspicionRole.Investigator, context);

        EnemyPatrolGroup detectorGroup = detector.RoutineController != null ? detector.RoutineController.PatrolGroup : null;
        IReadOnlyList<EnemyContext> enemies = EnemyRegistry.Instance.Enemies;
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyContext candidate = enemies[i];
            if (!CanBecomeSuspicious(candidate) || processedEnemies.Contains(candidate))
            {
                continue;
            }

            EnemyPatrolGroup candidateGroup = candidate.RoutineController != null ? candidate.RoutineController.PatrolGroup : null;
            bool sameGroup = detectorGroup != null && candidateGroup == detectorGroup;
            bool groupWithinRange = IsEnemyOrGroupWithinSpread(detector, candidate, candidateGroup);
            if (!sameGroup && !groupWithinRange)
            {
                continue;
            }

            if (candidateGroup != null)
            {
                IReadOnlyList<EnemyPatrolGroupMember> members = candidateGroup.Members;
                for (int memberIndex = 0; memberIndex < members.Count; memberIndex++)
                {
                    ApplySuspicion(members[memberIndex].Enemy, detector, position, sourceObject, EnemySuspicionRole.Support, context);
                }
            }
            else
            {
                ApplySuspicion(candidate, detector, position, sourceObject, EnemySuspicionRole.Support, context);
            }
        }
    }

    /// <summary>
    /// 다른 그룹의 구성원 하나라도 직접 감지자의 전파 거리 안인지 확인한다.
    /// </summary>
    private static bool IsEnemyOrGroupWithinSpread(EnemyContext detector, EnemyContext candidate, EnemyPatrolGroup group)
    {
        int range = detector.EnemyData.SuspicionSpreadRange;
        GridPosition origin = detector.GridActor.GridPosition;
        if (group == null)
        {
            return origin.ManhattanDistanceTo(candidate.GridActor.GridPosition) <= range;
        }

        IReadOnlyList<EnemyPatrolGroupMember> members = group.Members;
        for (int i = 0; i < members.Count; i++)
        {
            EnemyContext member = members[i].Enemy;
            if (member != null && member.IsAlive && origin.ManhattanDistanceTo(member.GridActor.GridPosition) <= range)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 적의 의심 정보를 갱신하고 즉시 조사 반응 이벤트를 발행한다.
    /// </summary>
    private void ApplySuspicion(
        EnemyContext enemy,
        EnemyContext detector,
        GridPosition position,
        Object sourceObject,
        EnemySuspicionRole role,
        ActionResolutionContext context)
    {
        if (!CanBecomeSuspicious(enemy) || !processedEnemies.Add(enemy))
        {
            return;
        }

        int turnIndex = TurnManager.Instance != null ? TurnManager.Instance.EnemyTurnIndex : 0;
        EnemySuspicionInfo info = new(position, SuspicionSource.SwordPresence, detector, turnIndex, sourceObject, role);
        if (!enemy.AlertState.RequestSuspicion(info))
        {
            return;
        }

        if (logSuspicionSpread)
        {
            Debug.Log($"{nameof(EnemySuspicionCoordinator)}: {enemy.name} 적이 {position} 칸의 검 기척을 {role} 역할로 조사합니다.", this);
        }

        context.EnqueuePresentation(PresentationEvent.SuspicionDetected(position, enemy, "적 의심 상태 전환 연출"));
        context.Publish(new EnemySuspicionTriggeredLogicEvent(enemy, info));
    }

    /// <summary>
    /// 지정한 적이 살아 있고 발각 전 상태인지 확인한다.
    /// </summary>
    private static bool CanBecomeSuspicious(EnemyContext enemy)
    {
        return enemy != null && enemy.enabled && enemy.IsAlive && enemy.AlertState != null &&
            !enemy.AlertState.IsAlerted && enemy.RoutineController != null && enemy.InvestigationAgent != null;
    }

    /// <summary>
    /// 플레이어 GridActor에 연결된 검 상태를 찾는다.
    /// </summary>
    private static PlayerSwordState TryGetSwordState(GridActor actor)
    {
        if (TacticalUnitRegistry.Instance != null &&
            TacticalUnitRegistry.Instance.TryGetPlayerControllableUnit(actor, out TacticalUnitContext player))
        {
            return player.SwordState;
        }

        return null;
    }

    /// <summary>
    /// GridActor에 해당하는 적 Context를 찾는다.
    /// </summary>
    private static bool TryFindEnemy(GridActor actor, out EnemyContext result)
    {
        if (actor != null && EnemyRegistry.Instance != null)
        {
            IReadOnlyList<EnemyContext> enemies = EnemyRegistry.Instance.Enemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                if (enemies[i] != null && enemies[i].GridActor == actor)
                {
                    result = enemies[i];
                    return true;
                }
            }
        }

        result = null;
        return false;
    }
}
