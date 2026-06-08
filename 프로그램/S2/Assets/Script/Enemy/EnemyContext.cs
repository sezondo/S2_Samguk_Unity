using UnityEngine;

/// <summary>
/// 적 루트에 붙은 핵심 컴포넌트와 데이터를 모아 제공하는 참조 주머니다.
/// 정책 계산이나 상태 변경은 하지 않고, 다른 적 컴포넌트가 필요한 참조를 꺼내 쓰게 한다.
/// </summary>
public class EnemyContext : MonoBehaviour
{
    [Header("Data")]
    // 적 시야와 행동에 사용하는 튜닝 데이터다.
    [SerializeField] private EnemyData enemyData;

    [Header("Core Components")]
    // 적이 보드에서 차지하는 칸과 실제 격자 이동을 관리하는 공용 말 컴포넌트다.
    [SerializeField] private GridActor gridActor;
    // 적의 그리드 시야 칸 계산을 담당하는 컴포넌트다.
    [SerializeField] private EnemyGridSight gridSight;

    public EnemyData EnemyData => enemyData;
    public GridActor GridActor => gridActor;
    public EnemyGridSight GridSight => gridSight;

    /// <summary>
    /// 적 Context에 필수 참조가 모두 연결되어 있는지 확인한다.
    /// </summary>
    public bool HasValidReference()
    {
        if (enemyData == null)
        {
            Debug.LogError($"{nameof(EnemyContext)} on {name}에는 {nameof(EnemyData)} 참조가 필요합니다.", this);
            return false;
        }

        if (gridActor == null)
        {
            Debug.LogError($"{nameof(EnemyContext)} on {name}에는 {nameof(GridActor)} 참조가 필요합니다.", this);
            return false;
        }

        if (gridSight == null)
        {
            Debug.LogError($"{nameof(EnemyContext)} on {name}에는 {nameof(EnemyGridSight)} 참조가 필요합니다.", this);
            return false;
        }

        return true;
    }
}
