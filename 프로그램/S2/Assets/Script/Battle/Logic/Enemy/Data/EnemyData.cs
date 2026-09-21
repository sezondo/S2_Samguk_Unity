using UnityEngine;

/// <summary>
/// S2-T 적의 턴제 잠입 규칙에 필요한 튜닝 수치를 보관하는 데이터 에셋이다.
/// 시야 거리와 인접 감지처럼 여러 적 컴포넌트가 공유할 값을 관리한다.
/// </summary>
[CreateAssetMenu(menuName = "Scriptable/EnemyData", fileName = "EnemyData")]
public class EnemyData : ScriptableObject
{
    [Header("Sight")]
    // 정면 부채꼴 시야가 도달하는 최대 전방 거리다.
    [SerializeField] private int sightRange = 5;
    // true면 바라보는 방향과 무관하게 주변 칸을 근접 감지한다.
    [SerializeField] private bool useAdjacentDetection = true;
    // 근접 감지에 사용할 주변 칸 반경이다. 1이면 인접한 8칸을 검사한다.
    [SerializeField] private int adjacentDetectionRange = 1;

    [Header("Suspicion")]
    // 도깨비검 기척을 360도 원형으로 감지하는 최대 거리다.
    [SerializeField] private int swordDetectionRange = 3;
    // 직접 감지자 주변의 다른 패트롤 그룹에 의심을 전달하는 맨해튼 거리다.
    [SerializeField] private int suspicionSpreadRange = 5;
    // 의심 상태 진입 직후 AP와 별개로 조사 위치까지 반응 이동할 수 있는 최대 거리다.
    [SerializeField] private int suspicionReactionMoveRange = 3;
    // 새 이상 현상 없이 조사 상태를 유지할 적 턴 수다.
    [SerializeField] private int suspicionDurationTurns = 3;
    // 조사 위치가 이상 현상과 유지하려는 최소 거리다.
    [SerializeField] private int investigationMinimumDistance = 1;
    // 조사 위치가 이상 현상과 유지하려는 최대 거리다.
    [SerializeField] private int investigationMaximumDistance = 3;

    [Header("Alert")]
    // 이 적이 플레이어를 발견했을 때 주변 적에게 애드를 전파하는 맨해튼 거리다.
    [SerializeField] private int alertSpreadRange = 5;
    // 경계 상태로 전환됐을 때 벽 인접 엄폐 칸으로 이동할 수 있는 최대 거리다.
    [SerializeField] private int alertReactionMoveRange = 3;

    [Header("Turn AI")]
    // 평상 순찰이 적 턴마다 1AP로 이동할 수 있는 최대 칸 수다.
    [SerializeField] private int patrolMoveRange = 3;
    // 적 턴마다 이 적이 사용할 수 있는 행동 AP다.
    [SerializeField] private int turnActionPoint = 3;
    // 적 턴 이동 행동 1회로 이동할 수 있는 최대 칸 수다.
    [SerializeField] private int turnMoveRange = 3;
    // 적 원거리 공격이 닿는 최대 맨해튼 거리다.
    [SerializeField] private int rangedAttackRange = 4;
    // 적 원거리 공격이 적용할 피해량이다.
    [SerializeField] private int rangedAttackDamage = 1;
    // 이 적의 원거리 공격 기본 명중률과 엄폐 대응 수치다.
    [SerializeField] private RangedAttackAccuracyData rangedAttackAccuracy = new();

    // AP 비용, 사용 가능한 공격과 공통 판단 점수 튜닝이다.
    [SerializeField] private EnemyCombatSettings combat = new();
    public EnemyCombatSettings Combat => combat;

    public int SightRange => sightRange;
    public bool UseAdjacentDetection => useAdjacentDetection;
    public int AdjacentDetectionRange => adjacentDetectionRange;
    public int SwordDetectionRange => swordDetectionRange;
    public int SuspicionSpreadRange => suspicionSpreadRange;
    public int SuspicionReactionMoveRange => suspicionReactionMoveRange;
    public int SuspicionDurationTurns => suspicionDurationTurns;
    public int InvestigationMinimumDistance => investigationMinimumDistance;
    public int InvestigationMaximumDistance => investigationMaximumDistance;
    public int AlertSpreadRange => alertSpreadRange;
    public int AlertReactionMoveRange => alertReactionMoveRange;
    public int TurnActionPoint => turnActionPoint;
    public int PatrolMoveRange => patrolMoveRange;
    public int TurnMoveRange => turnMoveRange;
    public int RangedAttackRange => rangedAttackRange;
    public int RangedAttackDamage => rangedAttackDamage;
    public RangedAttackAccuracyData RangedAttackAccuracy => rangedAttackAccuracy;
}
