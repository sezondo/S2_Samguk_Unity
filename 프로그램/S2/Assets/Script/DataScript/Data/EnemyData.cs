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

    [Header("Alert")]
    // 이 적이 플레이어를 발견했을 때 주변 적에게 애드를 전파하는 맨해튼 거리다.
    [SerializeField] private int alertSpreadRange = 5;

    public int SightRange => sightRange;
    public bool UseAdjacentDetection => useAdjacentDetection;
    public int AdjacentDetectionRange => adjacentDetectionRange;
    public int AlertSpreadRange => alertSpreadRange;
}
