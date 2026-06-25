using UnityEngine;

/// <summary>
/// S2-T 플레이어의 턴 기반 행동 수치를 보관하는 데이터 에셋이다.
/// AP, 이동 범위, 이동 비용처럼 여러 플레이어 컴포넌트가 공유하는 값을 한 곳에서 관리한다.
/// </summary>
[CreateAssetMenu(menuName = "Scriptable/PlayerTurnData", fileName = "PlayerTurnData")]
public class PlayerTurnData : ScriptableObject
{
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
    // 검 투척 행동 1회가 소비하는 AP 비용이다.
    [SerializeField] private int swordThrowActionPointCost = 1;
    // 검 회수 행동 1회가 소비하는 AP 비용이다. 회수에는 거리 제한이 없다.
    [SerializeField] private int swordRecallActionPointCost = 1;

    public int MaxActionPoint => maxActionPoint;
    public int StartTurnActionPoint => startTurnActionPoint;
    public int MoveDistancePerActionPoint => moveDistancePerActionPoint;
    public int MoveRange => moveRange;
    public int MoveActionPointCost => moveActionPointCost;
    public int HackRange => hackRange;
    public int HackActionPointCost => hackActionPointCost;
    public int SwordThrowRange => swordThrowRange;
    public int SwordThrowActionPointCost => swordThrowActionPointCost;
    public int SwordRecallActionPointCost => swordRecallActionPointCost;
}
