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
    // 이동 행동 1회로 도달할 수 있는 최대 맨해튼 거리다.
    [SerializeField] private int moveRange = 3;
    // 이동 행동 1회가 시작될 때 소비하는 AP 비용이다.
    [SerializeField] private int moveActionPointCost = 1;

    public int MaxActionPoint => maxActionPoint;
    public int StartTurnActionPoint => startTurnActionPoint;
    public int MoveRange => moveRange;
    public int MoveActionPointCost => moveActionPointCost;
}
