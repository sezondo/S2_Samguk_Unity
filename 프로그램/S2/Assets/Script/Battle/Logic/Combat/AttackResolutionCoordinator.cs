using UnityEngine;

/// <summary>
/// 엄폐가 적용되는 공격 요청의 방향·각도 보정과 명중 난수를 확정하는 씬 단위 조정자다.
/// 실제 HP 변경과 피해 후속 처리는 DamageResolutionCoordinator에 맡긴다.
/// </summary>
public sealed class AttackResolutionCoordinator : MonoBehaviour, IActionLogicEventHandler
{
    [Header("Reference")]
    // 인접 낮은 장애물 칸을 조회할 전투 그리드다.
    [SerializeField] private GridManager gridManager;

    [Header("Cover Angle Rule")]
    // 이 각도 이하에서는 낮은 엄폐 효과를 전부 적용한다.
    [SerializeField, Range(0f, 90f)] private float fullEffectMaximumAngle = CoverCalculator.DefaultFullEffectMaximumAngle;
    // 이 각도 이상에서는 측면 공격으로 보고 엄폐를 적용하지 않는다.
    [SerializeField, Range(0f, 180f)] private float flankMinimumAngle = CoverCalculator.DefaultFlankMinimumAngle;

    [Header("Log")]
    // true면 엄폐 방향·각도와 최종 명중 판정을 Unity 콘솔에 출력한다.
    [SerializeField] private bool logAttackResolution = true;

    // 현재 전투 씬에서 활성화된 공격 판정 조정자다.
    public static AttackResolutionCoordinator Instance { get; private set; }

    // 현재 GridManager를 사용하는 순수 엄폐 계산기다.
    private CoverCalculator coverCalculator;

    /// <summary>
    /// 필수 참조와 튜닝값을 검사하고 씬의 단일 공격 판정 조정자로 등록한다.
    /// </summary>
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError($"{nameof(AttackResolutionCoordinator)}: 씬에 공격 판정 조정자가 중복 배치되어 {name} 컴포넌트를 비활성화합니다.", this);
            enabled = false;
            return;
        }

        if (!HasValidReference() || !HasValidData())
        {
            enabled = false;
            return;
        }

        Instance = this;
        coverCalculator = new CoverCalculator(gridManager);
    }

    /// <summary>
    /// 활성 공격 판정 조정자를 논리 이벤트 버스에 등록한다.
    /// </summary>
    private void OnEnable()
    {
        if (Instance == this)
        {
            ActionLogicEventBus.Register(this);
        }
    }

    /// <summary>
    /// 논리 이벤트 등록과 현재 씬 인스턴스를 정리한다.
    /// </summary>
    private void OnDisable()
    {
        ActionLogicEventBus.Unregister(this);
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// 엄폐 기반 공격 판정 요청만 처리한다.
    /// </summary>
    public bool CanHandle(IActionLogicEvent logicEvent)
    {
        return logicEvent is ResolveAttackLogicEvent;
    }

    /// <summary>
    /// 인접 엄폐 방향·각도와 난수로 명중 여부를 확정해 피해 결과 처리기로 전달한다.
    /// </summary>
    public void Handle(IActionLogicEvent logicEvent, ActionResolutionContext context)
    {
        if (logicEvent is not ResolveAttackLogicEvent resolveAttack)
        {
            return;
        }

        if (resolveAttack.Attacker == null || resolveAttack.Target == null)
        {
            Debug.LogError($"{nameof(AttackResolutionCoordinator)}: 공격자 또는 대상이 없어 명중 판정을 중단합니다.", this);
            context.EnqueuePresentation(PresentationEvent.CombatCameraRestore("공격 판정 실패 카메라 복귀"));
            return;
        }

        if (!HasValidAttackData(resolveAttack))
        {
            context.EnqueuePresentation(PresentationEvent.CombatCameraRestore("공격 명중 데이터 오류 카메라 복귀"));
            return;
        }

        CoverResult coverResult = coverCalculator.Calculate(
            resolveAttack.FromPosition,
            resolveAttack.TargetPosition,
            resolveAttack.LowCoverHitPenalty,
            fullEffectMaximumAngle,
            flankMinimumAngle);

        int finalHitChance = Mathf.Clamp(
            resolveAttack.BaseHitChance + coverResult.HitChanceModifier,
            resolveAttack.MinimumHitChance,
            resolveAttack.MaximumHitChance);

        int roll = Random.Range(0, 100);

        AttackResult attackResult = new(
            roll < finalHitChance,
            resolveAttack.BaseHitChance,
            finalHitChance,
            roll,
            coverResult);

        context.Publish(new AttackResolvedLogicEvent(resolveAttack, attackResult));

        if (logAttackResolution)
        {
            string coverText = coverResult.HasCover
                ? $"{coverResult.Type}, 방향: {coverResult.CoverDirection}, 각도: {coverResult.Angle:F1}, 보정: {coverResult.HitChanceModifier}"
                : "없음";
            string hitText = attackResult.IsHit ? "명중" : "빗나감";
            Debug.Log($"{nameof(AttackResolutionCoordinator)}: {resolveAttack.Attacker.name} → {resolveAttack.Target.name}, 엄폐: {coverText}, 명중률: {finalHitChance}%, 난수: {roll}, 결과: {hitText}", this);
        }
    }

    /// <summary>
    /// 엄폐 계산에 필요한 GridManager 참조가 연결되어 있는지 확인한다.
    /// </summary>
    public bool HasValidReference()
    {
        if (gridManager == null)
        {
            Debug.LogError($"{nameof(AttackResolutionCoordinator)} on {name}에는 {nameof(GridManager)} 참조가 필요합니다.", this);
            return false;
        }

        return true;
    }

    /// <summary>
    /// 공통 엄폐 각도 규칙이 유효한지 확인한다.
    /// </summary>
    public bool HasValidData()
    {
        if (fullEffectMaximumAngle < 0f || flankMinimumAngle > 180f || fullEffectMaximumAngle >= flankMinimumAngle)
        {
            Debug.LogError($"{nameof(AttackResolutionCoordinator)} on {name}의 엄폐 최대 효과 각도는 측면 판정 각도보다 작아야 합니다.", this);
            return false;
        }

        return true;
    }

    /// <summary>
    /// 공격 요청에 복사된 유닛별 명중률과 엄폐 페널티 값이 유효한지 확인한다.
    /// </summary>
    private bool HasValidAttackData(ResolveAttackLogicEvent resolveAttack)
    {
        if (resolveAttack.BaseHitChance < 0 || resolveAttack.BaseHitChance > 100 ||
            resolveAttack.LowCoverHitPenalty < 0 || resolveAttack.LowCoverHitPenalty > 100)
        {
            Debug.LogError($"{nameof(AttackResolutionCoordinator)}: 공격 요청의 기본 명중률과 낮은 엄폐 페널티는 0~100이어야 합니다.", this);
            return false;
        }

        if (resolveAttack.MinimumHitChance < 0 || resolveAttack.MaximumHitChance > 100 ||
            resolveAttack.MinimumHitChance > resolveAttack.MaximumHitChance)
        {
            Debug.LogError($"{nameof(AttackResolutionCoordinator)}: 공격 요청의 최소·최대 명중률 범위가 올바르지 않습니다.", this);
            return false;
        }

        return true;
    }
}
