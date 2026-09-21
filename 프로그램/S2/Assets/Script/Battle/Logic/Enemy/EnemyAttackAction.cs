using UnityEngine;

/// <summary>
/// 적의 근접·원거리 공격 판정과 피해 적용 요청을 담당한다.
/// AP 소비와 행동 순서는 EnemyTurnAgent가 관리하고, 이 컴포넌트는 공격 가능 여부와 피해 이벤트 생성만 맡는다.
/// </summary>
public class EnemyAttackAction : MonoBehaviour
{
    [Header("Reference")]
    // 이 공격 행동을 수행할 적 Context다.
    [SerializeField] private EnemyContext enemyContext;

    [Header("Log")]
    // true면 적 원거리 공격 실행과 차단 사유를 Unity 콘솔에 출력한다.
    [SerializeField] private bool logAttack = true;

    public int AttackRange => enemyContext.EnemyData.RangedAttackRange;
    public int AttackDamage => enemyContext.EnemyData.RangedAttackDamage;

    /// <summary>
    /// 적 공격에 필요한 참조와 데이터를 확인한다.
    /// </summary>
    private void Awake()
    {
        if (!HasValidReference() || !HasValidData())
        {
            enabled = false;
        }
    }

    /// <summary>
    /// 모든 Awake가 끝난 뒤 필수 공격 판정 조정자가 활성화됐는지 확인한다.
    /// </summary>
    private void Start()
    {
        if (AttackResolutionCoordinator.Instance != null)
        {
            return;
        }

        Debug.LogError($"{nameof(EnemyAttackAction)} on {name}에는 활성 {nameof(AttackResolutionCoordinator)}가 반드시 필요합니다. 원거리 공격 컴포넌트를 비활성화합니다.", this);
        enabled = false;
    }

    /// <summary>
    /// 지정한 위치에서 대상 칸을 원거리 공격할 수 있는지 확인한다.
    /// </summary>
    public bool CanAttackFrom(GridPosition attackerPosition, GridPosition targetPosition)
    {
        return CanAttackFrom(attackerPosition, targetPosition, EnemyPlannedActionKind.Ranged);
    }

    /// <summary>
    /// 가상 위치에서 공격 종류에 따른 사거리와 합산 시야를 검사한다.
    /// </summary>
    public bool CanAttackFrom(GridPosition from, GridPosition target, EnemyPlannedActionKind kind)
    {
        var settings=enemyContext.EnemyData.Combat;
        if(kind==EnemyPlannedActionKind.Melee)return settings.allowMelee&&CombatTargetRules.IsMeleeAdjacent(from,target);
        if(kind!=EnemyPlannedActionKind.Ranged||!settings.allowRanged||from.ManhattanDistanceTo(target)>AttackRange)return false;
        // 플레이어의 합산 시야와 동일하게 발각된 아군 관측을 공유한다. 잠행 시야는 건드리지 않는다.
        if(CombatTargetRules.CanObserve(GridManager.Instance,from,target,AttackRange,true))return true;
        if(EnemyRegistry.Instance==null)return false;
        foreach(var ally in EnemyRegistry.Instance.Enemies)
            if(ally!=null&&ally!=enemyContext&&ally.isActiveAndEnabled&&ally.IsAlive&&ally.AlertState.IsAlerted&&
                CombatTargetRules.CanObserve(GridManager.Instance,ally.GridActor.GridPosition,target,ally.EnemyData.RangedAttackRange,true))return true;
        return false;
    }

    /// <summary>현재 위치에서 원거리 공격 가능한지 검사한다.</summary>
    public bool CanAttack(GridActor targetActor)
    {
        if (targetActor == null || !HasValidReference() || !HasValidData())
        {
            return false;
        }

        return CanAttackFrom(enemyContext.GridActor.GridPosition, targetActor.GridPosition);
    }

    /// <summary>
    /// 근접은 확정 피해, 원거리는 명중 판정 이벤트를 발행한다. AP는 호출자가 소비한다.
    /// </summary>
    public bool TryExecuteAttack(GridActor targetActor, ActionResolutionContext resolutionContext, EnemyPlannedActionKind kind = EnemyPlannedActionKind.Ranged)
    {
        if (resolutionContext == null)
        {
            Debug.LogError($"{nameof(EnemyAttackAction)} on {name}에는 공격을 처리할 {nameof(ActionResolutionContext)}가 필요합니다.", this);
            return false;
        }

        if (!HasValidReference() || !HasValidData())
        {
            return false;
        }

        if (AttackResolutionCoordinator.Instance == null)
        {
            Debug.LogError($"{nameof(EnemyAttackAction)} on {name}에는 활성 {nameof(AttackResolutionCoordinator)}가 필요합니다.", this);
            return false;
        }

        if (targetActor == null)
        {
            LogBlockedAttack("공격 대상이 없습니다");
            return false;
        }

        if (!CanAttackFrom(enemyContext.GridActor.GridPosition, targetActor.GridPosition, kind))
        {
            LogBlockedAttack("공격 종류·사거리·장애물 시야 조건을 만족하지 않습니다");
            return false;
        }

        ActorHealth targetHealth = targetActor.GetComponent<ActorHealth>();
        if (targetHealth != null && targetHealth.IsDead)
        {
            LogBlockedAttack($"{targetActor.name} 대상은 이미 전투불능입니다");
            return false;
        }

        if (targetActor.GetComponent<IDamageable>() == null)
        {
            LogBlockedAttack($"{targetActor.name} 대상에는 {nameof(IDamageable)}이 없습니다");
            return false;
        }

        GridPosition attackerPosition = enemyContext.GridActor.GridPosition;
        GridPosition targetPosition = targetActor.GridPosition;
        if (kind == EnemyPlannedActionKind.Melee)
            resolutionContext.Publish(new ApplyDamageLogicEvent(enemyContext.GridActor, targetActor, attackerPosition, targetPosition,
                enemyContext.EnemyData.Combat.meleeDamage, AttackPresentationKind.EnemyMelee, "적 근접 공격 연출"));
        else resolutionContext.Publish(new ResolveAttackLogicEvent(
            enemyContext.GridActor,
            targetActor,
            attackerPosition,
            targetPosition,
            AttackDamage,
            enemyContext.EnemyData.RangedAttackAccuracy,
            AttackPresentationKind.EnemyRanged,
            "적 원거리 공격 연출"));
        // 시야 밖 공격자 표시와 전투 연출보다 먼저 실제 공격 구도를 잡는다.
        resolutionContext.EnqueuePresentation(PresentationEvent.CombatCameraFocus(
            attackerPosition,
            targetPosition,
            "적 원거리 공격 카메라 포커스"));

        if (logAttack)
        {
            string attackName = kind == EnemyPlannedActionKind.Melee ? "근접" : "원거리";
            int damage = kind == EnemyPlannedActionKind.Melee ? enemyContext.EnemyData.Combat.meleeDamage : AttackDamage;
            Debug.Log($"{nameof(EnemyAttackAction)}: {enemyContext.name} 적이 {targetActor.name} 대상에게 {attackName} 공격을 요청했습니다. 피해량: {damage}", this);
        }

        return true;
    }

    /// <summary>
    /// 적 공격이 막힌 사유를 로그로 남긴다.
    /// </summary>
    private void LogBlockedAttack(string reason)
    {
        if (logAttack)
        {
            Debug.Log($"{nameof(EnemyAttackAction)}: {enemyContext.name} 적이 공격할 수 없습니다. 사유: {reason}", this);
        }
    }

    /// <summary>
    /// 원거리 공격에 필요한 필수 참조가 연결되어 있는지 확인한다.
    /// </summary>
    public bool HasValidReference()
    {
        if (enemyContext == null)
        {
            Debug.LogError($"{nameof(EnemyAttackAction)} on {name}에는 {nameof(EnemyContext)} 참조가 필요합니다.", this);
            return false;
        }

        if (!enemyContext.enabled || !enemyContext.HasValidReference())
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// 원거리 공격에 필요한 적 데이터가 유효한지 확인한다.
    /// </summary>
    public bool HasValidData()
    {
        EnemyData enemyData = enemyContext.EnemyData;
        if (enemyData.Combat == null)
        { Debug.LogError($"{name}: 적 전투 설정이 필요합니다.", this); return false; }
        if (enemyData.RangedAttackRange < 0)
        {
            Debug.LogError($"{nameof(EnemyAttackAction)} on {name}의 원거리 공격 사거리는 0 이상이어야 합니다.", this);
            return false;
        }

        if (enemyData.RangedAttackDamage <= 0)
        {
            Debug.LogError($"{nameof(EnemyAttackAction)} on {name}의 원거리 공격 피해량은 0보다 커야 합니다.", this);
            return false;
        }

        RangedAttackAccuracyData accuracyData = enemyData.RangedAttackAccuracy;
        if (!HasValidAccuracyData(accuracyData))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// 적 원거리 공격의 유닛별 명중률과 엄폐 페널티 값이 유효한지 확인한다.
    /// </summary>
    private bool HasValidAccuracyData(RangedAttackAccuracyData accuracyData)
    {
        if (accuracyData == null)
        {
            Debug.LogError($"{nameof(EnemyAttackAction)} on {name}의 {nameof(EnemyData)}에 원거리 공격 명중 데이터가 필요합니다.", this);
            return false;
        }

        if (accuracyData.BaseHitChance < 0 || accuracyData.BaseHitChance > 100 ||
            accuracyData.LowCoverHitPenalty < 0 || accuracyData.LowCoverHitPenalty > 100)
        {
            Debug.LogError($"{nameof(EnemyAttackAction)} on {name}의 기본 명중률과 낮은 엄폐 페널티는 0~100이어야 합니다.", this);
            return false;
        }

        if (accuracyData.MinimumHitChance < 0 || accuracyData.MaximumHitChance > 100 ||
            accuracyData.MinimumHitChance > accuracyData.MaximumHitChance)
        {
            Debug.LogError($"{nameof(EnemyAttackAction)} on {name}의 최소·최대 명중률 범위가 올바르지 않습니다.", this);
            return false;
        }

        return true;
    }
}
