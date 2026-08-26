using UnityEngine;

/// <summary>
/// 플레이어가 조작할 수 있는 전술 유닛의 능력 구성과 턴 기반 행동 수치를 보관하는 데이터 에셋이다.
/// 데이터에 선언된 필수 능력과 실제 행동 컴포넌트 구성은 TacticalUnitContext가 대조한다.
/// </summary>
[CreateAssetMenu(menuName = "Scriptable/ControllableUnitData", fileName = "ControllableUnitData")]
public class ControllableUnitData : ScriptableObject
{
    [Header("Identity")]
    // 디버그와 UI에서 사용할 전술 유닛 표시 이름이다.
    [SerializeField] private string displayName = "전술 유닛";
    // 전투 HUD의 유닛 카드에 표시할 캐릭터 초상화다.
    [SerializeField] private Sprite portrait;
    // 이 데이터의 유닛이 반드시 갖춰야 하는 행동 능력 조합이다.
    [SerializeField] private UnitAbilityType requiredAbilities =
        UnitAbilityType.Move |
        UnitAbilityType.Gun |
        UnitAbilityType.Hack |
        UnitAbilityType.Sword |
        UnitAbilityType.Melee;
    // 이 유닛이 사망했을 때 스테이지 패배 조건으로 사용할지 나타낸다. 현재는 기록용이다.
    [SerializeField] private bool defeatOnDeath;

    [Header("Vision")]
    // 이 유닛이 360도로 확보하는 플레이어 시야의 최대 그리드 거리다.
    [SerializeField] private int visionRange = 6;

    [Header("AP")]
    // 플레이어가 가질 수 있는 최대 AP다.
    [SerializeField] private int maxActionPoint = 3;
    // 플레이어 턴 시작 시 보충할 AP 양이다.
    [SerializeField] private int startTurnActionPoint = 3;

    [Header("Move")]
    // AP 1을 이동에 썼을 때 갈 수 있는 최대 칸 수다.
    [SerializeField] private int moveDistancePerActionPoint = 3;
    // 기존 이동 범위 값이다. 새 이동 구조에서는 호환용으로만 남기고 직접 사용하지 않는다.
    [SerializeField] private int moveRange = 3;
    // 이동 거리 구간 1개가 소비하는 AP 비용이다. 기본값 1이면 1~3칸은 AP 1, 4~6칸은 AP 2를 쓴다.
    [SerializeField] private int moveActionPointCost = 1;

    [Header("Hack")]
    // 플레이어가 해킹 대상을 선택할 수 있는 최대 맨해튼 거리다.
    [SerializeField] private int hackRange = 3;
    // 해킹 행동 1회가 소비하는 AP 비용이다.
    [SerializeField] private int hackActionPointCost = 1;

    [Header("Sword")]
    // 검 현재 위치 기준으로 검을 다시 던질 수 있는 최대 맨해튼 거리다.
    [SerializeField] private int swordThrowRange = 3;
    // 검이 플레이어에게서 떨어져 배치됐을 때 주변을 밝히는 최대 그리드 거리다.
    [SerializeField] private int swordVisionRange = 3;
    // 검 투척 행동 1회가 소비하는 AP 비용이다.
    [SerializeField] private int swordThrowActionPointCost = 1;
    // 검 투척 목표 칸에 피해 가능 대상이 있을 때 적용할 피해량이다.
    [SerializeField] private int swordThrowDamage = 2;
    // 검 회수 행동 1회가 소비하는 AP 비용이다. 회수에는 거리 제한이 없다.
    [SerializeField] private int swordRecallActionPointCost = 1;

    [Header("Melee")]
    // 근접 공격 행동 1회가 소비하는 AP 비용이다.
    [SerializeField] private int meleeAttackActionPointCost = 1;
    // 검을 소유 중일 때 근접 공격으로 적용할 피해량이다.
    [SerializeField] private int meleeDamageWithSword = 3;
    // 검을 소유하지 않을 때 근접 공격으로 적용할 피해량이다.
    [SerializeField] private int meleeDamageWithoutSword = 1;

    [Header("Gun")]
    // 총 공격 행동 1회가 소비하는 AP 비용이다.
    [SerializeField] private int gunAttackActionPointCost = 1;
    // 플레이어 현재 위치 기준으로 총 공격 대상을 선택할 수 있는 최대 맨해튼 거리다.
    [SerializeField] private int gunAttackRange = 5;
    // 총 공격으로 적용할 피해량이다.
    [SerializeField] private int gunAttackDamage = 2;
    // 이 유닛의 총 공격 기본 명중률과 엄폐 대응 수치다.
    [SerializeField] private RangedAttackAccuracyData gunAttackAccuracy = new();
    // 플레이어가 보유할 수 있는 총알 수다. 현재 총알은 PlayerGunAmmo가 런타임 상태로 보관한다.
    [SerializeField] private int maxGunAmmo = 3;

    public string DisplayName => displayName;
    public Sprite Portrait => portrait;
    public UnitAbilityType RequiredAbilities => requiredAbilities;
    public bool DefeatOnDeath => defeatOnDeath;
    public int VisionRange => visionRange;
    public int MaxActionPoint => maxActionPoint;
    public int StartTurnActionPoint => startTurnActionPoint;
    public int MoveDistancePerActionPoint => moveDistancePerActionPoint;
    public int MoveRange => moveRange;
    public int MoveActionPointCost => moveActionPointCost;
    public int HackRange => hackRange;
    public int HackActionPointCost => hackActionPointCost;
    public int SwordThrowRange => swordThrowRange;
    public int SwordVisionRange => swordVisionRange;
    public int SwordThrowActionPointCost => swordThrowActionPointCost;
    public int SwordThrowDamage => swordThrowDamage;
    public int SwordRecallActionPointCost => swordRecallActionPointCost;
    public int MeleeAttackActionPointCost => meleeAttackActionPointCost;
    public int MeleeDamageWithSword => meleeDamageWithSword;
    public int MeleeDamageWithoutSword => meleeDamageWithoutSword;
    public int GunAttackActionPointCost => gunAttackActionPointCost;
    public int GunAttackRange => gunAttackRange;
    public int GunAttackDamage => gunAttackDamage;
    public RangedAttackAccuracyData GunAttackAccuracy => gunAttackAccuracy;
    public int MaxGunAmmo => maxGunAmmo;

    /// <summary>
    /// 지정한 행동 능력이 데이터상 필수 능력인지 확인한다.
    /// </summary>
    public bool RequiresAbility(UnitAbilityType ability)
    {
        return ability != UnitAbilityType.None && (requiredAbilities & ability) == ability;
    }
}
