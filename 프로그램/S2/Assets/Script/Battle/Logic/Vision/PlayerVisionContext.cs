using UnityEngine;

/// <summary>
/// 플레이어 시야 논리와 연출에서 사용하는 씬 단위 필수 참조를 모은 참조 주머니다.
/// 시야 계산, 상태 변경이나 표시 정책은 갖지 않는다.
/// </summary>
public class PlayerVisionContext : MonoBehaviour
{
    [Header("Battle References")]
    // 시야 범위와 가림 판정에 사용할 씬 그리드다.
    [SerializeField] private GridManager gridManager;
    // 살아 있는 플레이어 전술 유닛과 유닛별 시야 데이터를 제공하는 등록소다.
    [SerializeField] private TacticalUnitRegistry tacticalUnitRegistry;
    // 시야에 따라 표시할 적 목록을 제공하는 등록소다.
    [SerializeField] private EnemyRegistry enemyRegistry;
    // 적 논리 Actor에 연결된 Visual을 찾는 연출 등록소다.
    [SerializeField] private ActorPresentationRegistry actorPresentationRegistry;
    // 시야 변경 이벤트를 순서대로 재생할 씬 연출 큐다.
    [SerializeField] private ActionPresentationQueue presentationQueue;
    // 플레이어 시야 논리를 계산하고 스냅샷을 생성하는 매니저다.
    [SerializeField] private PlayerVisionManager playerVisionManager;
    // Fog와 적 표시를 실제 화면에 적용하는 Presenter다.
    [SerializeField] private PlayerVisionPresenter playerVisionPresenter;
    // 런타임 Fog 칸 SpriteRenderer를 배치할 전용 루트다.
    [SerializeField] private Transform fogRoot;

    public GridManager GridManager => gridManager;
    public TacticalUnitRegistry TacticalUnitRegistry => tacticalUnitRegistry;
    public EnemyRegistry EnemyRegistry => enemyRegistry;
    public ActorPresentationRegistry ActorPresentationRegistry => actorPresentationRegistry;
    public ActionPresentationQueue PresentationQueue => presentationQueue;
    public PlayerVisionManager PlayerVisionManager => playerVisionManager;
    public PlayerVisionPresenter PlayerVisionPresenter => playerVisionPresenter;
    public Transform FogRoot => fogRoot;

    /// <summary>
    /// 플레이어 시야 시스템에 필요한 모든 씬 참조가 연결되어 있는지 확인한다.
    /// </summary>
    public bool HasValidReference()
    {
        if (gridManager == null)
        {
            Debug.LogError($"{nameof(PlayerVisionContext)} on {name}에는 {nameof(GridManager)} 참조가 필요합니다.", this);
            return false;
        }

        if (tacticalUnitRegistry == null)
        {
            Debug.LogError($"{nameof(PlayerVisionContext)} on {name}에는 {nameof(TacticalUnitRegistry)} 참조가 필요합니다.", this);
            return false;
        }

        if (enemyRegistry == null)
        {
            Debug.LogError($"{nameof(PlayerVisionContext)} on {name}에는 {nameof(EnemyRegistry)} 참조가 필요합니다.", this);
            return false;
        }

        if (actorPresentationRegistry == null)
        {
            Debug.LogError($"{nameof(PlayerVisionContext)} on {name}에는 {nameof(ActorPresentationRegistry)} 참조가 필요합니다.", this);
            return false;
        }

        if (presentationQueue == null)
        {
            Debug.LogError($"{nameof(PlayerVisionContext)} on {name}에는 {nameof(ActionPresentationQueue)} 참조가 필요합니다.", this);
            return false;
        }

        if (playerVisionManager == null)
        {
            Debug.LogError($"{nameof(PlayerVisionContext)} on {name}에는 {nameof(PlayerVisionManager)} 참조가 필요합니다.", this);
            return false;
        }

        if (playerVisionPresenter == null)
        {
            Debug.LogError($"{nameof(PlayerVisionContext)} on {name}에는 {nameof(PlayerVisionPresenter)} 참조가 필요합니다.", this);
            return false;
        }

        if (fogRoot == null)
        {
            Debug.LogError($"{nameof(PlayerVisionContext)} on {name}에는 Fog 표시 전용 루트 참조가 필요합니다.", this);
            return false;
        }

        return true;
    }
}
