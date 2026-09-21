using UnityEngine;

/// <summary>
/// 적 이동·회전 뒤 갱신된 시야로 살아 있는 플레이어를 새로 감지했는지 검사한다.
/// </summary>
public class EnemyPerceptionCoordinator : MonoBehaviour, IActionLogicEventHandler
{

    /// <summary>
    /// 활성화될 때 논리 이벤트 처리자로 등록한다.
    /// </summary>
    private void OnEnable()
    {
        ActionLogicEventBus.Register(this);
    }

    /// <summary>
    /// 비활성화될 때 논리 이벤트 처리자 등록을 해제한다.
    /// </summary>
    private void OnDisable()
    {
        ActionLogicEventBus.Unregister(this);
    }

    /// <summary>
    /// 적 시야가 바뀐 이벤트만 처리한다.
    /// </summary>
    public bool CanHandle(IActionLogicEvent logicEvent)
    {
        return logicEvent is EnemyPerceptionChangedLogicEvent;
    }

    /// <summary>이미 확정한 감지 결과를 발각·전파·엄폐 반응 논리로 이어준다.</summary>
    public void Handle(IActionLogicEvent logicEvent, ActionResolutionContext context)
    {
        if (logicEvent is not EnemyPerceptionChangedLogicEvent perception || !perception.HasDetectedPlayer)
            return;
        EnemyContext enemy = perception.Enemy;
        if (enemy == null || !enemy.IsAlive || enemy.AlertState == null || enemy.AlertState.IsAlerted)
            return;
        context.Publish(new AlertTriggeredLogicEvent(perception.DetectedPlayerPosition, enemy, enemy.GridSight));
    }

    /// <summary>현재 한 칸의 시야에서 감지를 즉시 확정한다. 후속 논리 큐를 재귀 실행하지 않는다.</summary>
    public static EnemyPerceptionChangedLogicEvent CapturePlayerDetection(EnemyContext enemy)
    {
        if (enemy != null && enemy.IsAlive && enemy.AlertState != null && !enemy.AlertState.IsAlerted &&
            enemy.GridSight != null && TacticalUnitRegistry.Instance != null)
        {
            var units = TacticalUnitRegistry.Instance.Units;
            for (int i = 0; i < units.Count; i++)
            {
                if (units[i] is not TacticalUnitContext player || player == null || !player.IsAlive ||
                    player.Faction != UnitFaction.Player || player.GridActor == null ||
                    !enemy.GridSight.CanDetectPlayer(player.GridActor.GridPosition)) continue;
                return new EnemyPerceptionChangedLogicEvent(enemy, true, player.GridActor.GridPosition);
            }
        }
        return new EnemyPerceptionChangedLogicEvent(enemy, false, default);
    }
}
