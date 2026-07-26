using UnityEngine;

/// <summary>
/// 게임 시작 시 AppRoot를 영속화하고 캠페인 저장과 최초 로비 진입을 한 번만 초기화한다.
/// </summary>
[DefaultExecutionOrder(-1000)]
public class CampaignBootstrap : MonoBehaviour
{
    [Header("Reference")]
    // 영속 AppRoot의 캠페인 데이터와 핵심 런타임 참조 주머니다.
    [SerializeField] private CampaignContext context;

    // 현재 실행에서 유지되는 단일 Bootstrap 인스턴스다.
    public static CampaignBootstrap Instance { get; private set; }

    public CampaignContext Context => context;

    /// <summary>
    /// 중복 AppRoot를 제거하고 현재 루트 오브젝트를 씬 전환 뒤에도 유지한다.
    /// </summary>
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"{nameof(CampaignBootstrap)}: 영속 AppRoot가 이미 있어 중복 오브젝트 {name}을 제거합니다.", this);
            Destroy(gameObject);
            return;
        }

        if (!HasValidReference() || !HasValidData())
        {
            enabled = false;
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// 저장 데이터를 준비한 뒤 캠페인 흐름의 최초 로비 진입을 시작한다.
    /// </summary>
    private void Start()
    {
        if (Instance != this)
        {
            return;
        }

        if (!context.SaveManager.Initialize())
        {
            Debug.LogError($"{nameof(CampaignBootstrap)}: 캠페인 저장 시스템 초기화에 실패했습니다.", this);
            enabled = false;
            return;
        }

        if (!context.FlowController.Initialize())
        {
            Debug.LogError($"{nameof(CampaignBootstrap)}: 캠페인 흐름 초기화에 실패했습니다.", this);
            enabled = false;
        }
    }

    /// <summary>
    /// 현재 영속 AppRoot가 제거될 때 전역 Bootstrap 참조를 정리한다.
    /// </summary>
    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// Bootstrap에 필요한 캠페인 Context와 그 내부 참조가 연결되어 있는지 확인한다.
    /// </summary>
    public bool HasValidReference()
    {
        if (context == null)
        {
            Debug.LogError($"{nameof(CampaignBootstrap)} on {name}에는 {nameof(CampaignContext)} 참조가 필요합니다.", this);
            return false;
        }

        return context.HasValidReference();
    }

    /// <summary>
    /// 영속 AppRoot가 씬 루트 오브젝트로 배치되어 있는지 확인한다.
    /// </summary>
    public bool HasValidData()
    {
        if (transform.parent != null)
        {
            Debug.LogError($"{nameof(CampaignBootstrap)} on {name}은 다른 오브젝트의 자식이 아닌 씬 루트에 있어야 합니다.", this);
            return false;
        }

        return true;
    }
}
