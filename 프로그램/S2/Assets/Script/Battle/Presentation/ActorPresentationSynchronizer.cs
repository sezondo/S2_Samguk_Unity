using UnityEngine;

/// <summary>
/// 논리 GridActor의 현재 그리드 좌표와 화면 표시용 VisualRoot 위치를 동기화한다.
/// 씬 시작 시 논리 위치 기준으로 연출 오브젝트의 시작 위치를 맞춘다.
/// </summary>
public class ActorPresentationSynchronizer : MonoBehaviour
{
    [Header("Target")]
    // 위치 기준으로 사용할 논리 Actor다.
    [SerializeField] private GridActor targetActor;
    // 실제 화면에서 표시되는 루트 Transform이다.
    [SerializeField] private Transform visualRoot;

    [Header("Sync")]
    // true면 Start 시점에 VisualRoot를 논리 Actor의 현재 칸 위치로 맞춘다.
    [SerializeField] private bool syncOnStart = true;
    // true면 OnEnable 시점에도 동기화를 시도한다.
    [SerializeField] private bool syncOnEnable;

    /// <summary>
    /// 컴포넌트 활성화 시 설정에 따라 위치 동기화를 시도한다.
    /// </summary>
    private void OnEnable()
    {
        if (syncOnEnable)
        {
            ForceSyncToActorPosition();
        }
    }

    /// <summary>
    /// 씬 시작 시 설정에 따라 위치 동기화를 시도한다.
    /// </summary>
    private void Start()
    {
        if (syncOnStart)
        {
            ForceSyncToActorPosition();
        }
    }

    /// <summary>
    /// VisualRoot 위치를 논리 Actor의 현재 GridPosition 기준 월드 위치로 강제 동기화한다.
    /// </summary>
    public bool ForceSyncToActorPosition()
    {
        if (!HasValidReference())
        {
            return false;
        }

        GridManager gridManager = GridManager.Instance;
        if (gridManager == null)
        {
            Debug.LogError($"{nameof(ActorPresentationSynchronizer)} on {name}에는 위치 동기화에 사용할 {nameof(GridManager)}가 필요합니다.", this);
            return false;
        }

        visualRoot.position = gridManager.GridToWorld(targetActor.GridPosition);
        return true;
    }

    /// <summary>
    /// 위치 동기화에 필요한 필수 참조가 연결되어 있는지 확인한다.
    /// </summary>
    private bool HasValidReference()
    {
        if (targetActor == null)
        {
            Debug.LogError($"{nameof(ActorPresentationSynchronizer)} on {name}에는 위치 기준 {nameof(GridActor)} 참조가 필요합니다.", this);
            return false;
        }

        if (visualRoot == null)
        {
            Debug.LogError($"{nameof(ActorPresentationSynchronizer)} on {name}에는 동기화할 VisualRoot Transform 참조가 필요합니다.", this);
            return false;
        }

        return true;
    }
}
