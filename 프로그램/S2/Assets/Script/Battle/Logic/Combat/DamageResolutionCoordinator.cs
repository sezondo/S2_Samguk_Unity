using UnityEngine;

/// <summary>
/// 확정된 공격의 명중·빗나감 후처리와 실제 피해 적용 결과를 논리·연출 이벤트로 변환하는 기본 처리자다.
/// 씬 배치 없이 ActionLogicEventBus에 등록되어 공격 수단별 중복 결과 처리를 줄인다.
/// </summary>
public sealed class DamageResolutionCoordinator : IActionLogicEventHandler
{
    public static DamageResolutionCoordinator Instance { get; } = new();

    /// <summary>
    /// 외부 생성을 막고 기본 인스턴스만 사용한다.
    /// </summary>
    private DamageResolutionCoordinator()
    {
    }

    /// <summary>
    /// 직접 피해 적용 요청과 명중 판정이 끝난 공격 결과를 처리한다.
    /// </summary>
    public bool CanHandle(IActionLogicEvent logicEvent)
    {
        return logicEvent is ApplyDamageLogicEvent or AttackResolvedLogicEvent;
    }

    /// <summary>
    /// 직접 피해 요청 또는 확정 공격 결과를 받아 HP 변경과 논리·연출 이벤트를 만든다.
    /// </summary>
    public void Handle(IActionLogicEvent logicEvent, ActionResolutionContext context)
    {
        if (logicEvent is AttackResolvedLogicEvent attackResolved)
        {
            HandleAttackResolved(attackResolved, context);
            return;
        }

        if (logicEvent is ApplyDamageLogicEvent applyDamage)
        {
            ResolveHitAndEnqueue(
                applyDamage.Attacker,
                applyDamage.Target,
                applyDamage.FromPosition,
                applyDamage.TargetPosition,
                applyDamage.Damage,
                applyDamage.PresentationKind,
                applyDamage.Message,
                AttackResult.GuaranteedHit(),
                context);
        }
    }

    /// <summary>
    /// 확정된 공격 결과가 빗나감이면 연출만 만들고, 명중이면 실제 피해 적용으로 이어간다.
    /// </summary>
    private static void HandleAttackResolved(AttackResolvedLogicEvent attackResolved, ActionResolutionContext context)
    {
        ResolveAttackLogicEvent attack = attackResolved.Attack;
        if (attack == null)
        {
            Debug.LogError($"{nameof(DamageResolutionCoordinator)}: 원본 공격 요청이 없어 결과 처리를 중단합니다.");
            context.EnqueuePresentation(PresentationEvent.CombatCameraRestore("공격 결과 처리 실패 카메라 복귀"));
            return;
        }

        if (!attackResolved.Result.IsHit)
        {
            EnqueueCombatPresentation(
                attack.Attacker,
                attack.Target,
                attack.FromPosition,
                attack.TargetPosition,
                attack.PresentationKind,
                attack.Message,
                attackResolved.Result,
                default,
                false,
                context);
            return;
        }

        ResolveHitAndEnqueue(
            attack.Attacker,
            attack.Target,
            attack.FromPosition,
            attack.TargetPosition,
            attack.Damage,
            attack.PresentationKind,
            attack.Message,
            attackResolved.Result,
            context);
    }

    /// <summary>
    /// 명중이 확정된 공격의 HP를 변경하고 피해·사망 논리 이벤트와 전투 연출을 만든다.
    /// </summary>
    private static void ResolveHitAndEnqueue(
        GridActor attacker,
        GridActor target,
        GridPosition fromPosition,
        GridPosition targetPosition,
        int damage,
        AttackPresentationKind presentationKind,
        string message,
        AttackResult attackResult,
        ActionResolutionContext context)
    {
        if (!TryResolveDamage(target, damage, out DamageResult damageResult))
        {
            context.EnqueuePresentation(PresentationEvent.CombatCameraRestore("피해 처리 실패 카메라 복귀"));
            return;
        }

        context.Publish(new DamageAppliedLogicEvent(
            attacker,
            target,
            targetPosition,
            damageResult));

        if (damageResult.KilledByThisDamage)
        {
            context.Publish(new ActorDiedLogicEvent(
                attacker,
                target,
                targetPosition,
                damageResult));
        }

        EnqueueCombatPresentation(
            attacker,
            target,
            fromPosition,
            targetPosition,
            presentationKind,
            message,
            attackResult,
            damageResult,
            true,
            context);
    }

    /// <summary>
    /// 명중·빗나감 공통 공격 연출과 카메라 복귀, 시야 밖 적 공격자 노출 이벤트를 순서대로 만든다.
    /// </summary>
    private static void EnqueueCombatPresentation(
        GridActor attacker,
        GridActor target,
        GridPosition fromPosition,
        GridPosition targetPosition,
        AttackPresentationKind presentationKind,
        string message,
        AttackResult attackResult,
        DamageResult damageResult,
        bool hasDamageResult,
        ActionResolutionContext context)
    {
        bool temporarilyRevealAttacker =
            presentationKind == AttackPresentationKind.EnemyRanged &&
            PlayerVisionManager.Instance != null &&
            !PlayerVisionManager.Instance.IsVisible(fromPosition);

        if (temporarilyRevealAttacker)
        {
            context.EnqueuePresentation(PresentationEvent.ActorVisibilityOverride(
                attacker,
                true,
                "시야 밖 공격자 임시 노출"));
        }

        context.EnqueuePresentation(PresentationEvent.CombatAction(
            attacker,
            target,
            fromPosition,
            targetPosition,
            presentationKind,
            attackResult,
            damageResult,
            hasDamageResult,
            message));

        context.EnqueuePresentation(PresentationEvent.CombatCameraRestore("전투 카메라 복귀"));

        if (temporarilyRevealAttacker)
        {
            context.EnqueuePresentation(PresentationEvent.ActorVisibilityOverride(
                attacker,
                false,
                "시야 밖 공격자 임시 노출 해제"));
        }
    }

    /// <summary>
    /// 대상의 IDamageable을 찾아 피해를 적용하고 결과를 반환한다.
    /// </summary>
    private static bool TryResolveDamage(GridActor target, int damage, out DamageResult damageResult)
    {
        damageResult = default;

        if (target == null)
        {
            Debug.LogError($"{nameof(DamageResolutionCoordinator)}: 피해 적용 대상이 없어 처리를 중단합니다.");
            return false;
        }

        IDamageable damageable = target.GetComponent<IDamageable>();
        if (damageable == null)
        {
            Debug.LogError($"{nameof(DamageResolutionCoordinator)}: {target.name} 대상에는 {nameof(IDamageable)}이 없어 피해를 적용할 수 없습니다.");
            return false;
        }

        damageResult = damageable.TakeDamage(damage);
        return true;
    }
}
