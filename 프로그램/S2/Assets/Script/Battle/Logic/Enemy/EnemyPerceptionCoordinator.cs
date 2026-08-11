using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 적 이동·회전 뒤 갱신된 시야로 살아 있는 플레이어를 새로 감지했는지 검사한다.
/// </summary>
public class EnemyPerceptionCoordinator : MonoBehaviour, IActionLogicEventHandler
{
    // 플레이어 진영 조회에 재사용하는 버퍼다.
    private readonly List<ITacticalUnit> playerBuffer = new();

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

    /// <summary>
    /// 아직 발각 상태가 아닌 적이 플레이어를 감지하면 기존 발각 이벤트를 발행한다.
    /// </summary>
    public void Handle(IActionLogicEvent logicEvent, ActionResolutionContext context)
    {
        if (logicEvent is not EnemyPerceptionChangedLogicEvent perceptionChanged)
        {
            return;
        }

        EnemyContext enemy = perceptionChanged.Enemy;
        if (enemy == null || !enemy.IsAlive || enemy.AlertState == null || enemy.AlertState.IsAlerted ||
            enemy.GridSight == null || TacticalUnitRegistry.Instance == null)
        {
            return;
        }

        TacticalUnitRegistry.Instance.GetAliveUnits(UnitFaction.Player, playerBuffer);
        for (int i = 0; i < playerBuffer.Count; i++)
        {
            if (playerBuffer[i] is not TacticalUnitContext player || player.GridActor == null ||
                !enemy.GridSight.CanDetectPlayer(player.GridActor.GridPosition))
            {
                continue;
            }

            context.Publish(new AlertTriggeredLogicEvent(player.GridActor.GridPosition, enemy, enemy.GridSight));
            return;
        }
    }
}
