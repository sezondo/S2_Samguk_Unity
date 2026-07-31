using UnityEngine;

/// <summary>
/// StoryTest 씬에서 지정한 샘플 Story를 자동 재생하고 완료 결과를 확인한다.
/// </summary>
public class StoryTestLauncher : MonoBehaviour
{
    [Header("Reference")]
    // 테스트할 Story 재생을 담당하는 Runner다.
    [SerializeField] private StoryRunner runner;
    // StoryTest 씬 시작 시 재생할 샘플 Story 데이터다.
    [SerializeField] private StorySequenceData sampleSequence;

    [Header("Test")]
    // 테스트 중 Escape 스킵 입력을 허용할지 나타낸다.
    [SerializeField] private bool allowSkip = true;

    /// <summary>
    /// 필수 테스트 참조를 확인하고 완료 이벤트를 구독한다.
    /// </summary>
    private void Awake()
    {
        // 캠페인으로 진입했을 때는 StoryCampaignBridge가 재생 데이터를 결정한다.
        if (CampaignBootstrap.Instance != null)
        {
            enabled = false;
            return;
        }

        if (!HasValidReference())
        {
            enabled = false;
            return;
        }

        runner.StoryCompleted += HandleStoryCompleted;
    }

    /// <summary>
    /// StoryTest 씬이 시작되면 샘플 Story를 처음부터 재생한다.
    /// </summary>
    private void Start()
    {
        if (!runner.Play(sampleSequence, allowSkip))
        {
            Debug.LogError($"{nameof(StoryTestLauncher)}: 샘플 Story 재생을 시작하지 못했습니다.", this);
        }
    }

    /// <summary>
    /// 테스트 완료 이벤트 구독을 해제한다.
    /// </summary>
    private void OnDestroy()
    {
        if (runner != null)
        {
            runner.StoryCompleted -= HandleStoryCompleted;
        }
    }

    /// <summary>
    /// 샘플 Story 완료 이벤트가 한 번 전달된 결과를 한국어 로그로 확인한다.
    /// </summary>
    private void HandleStoryCompleted(StorySequenceData sequence, StoryCompletionReason reason)
    {
        Debug.Log($"{nameof(StoryTestLauncher)}: '{sequence.DisplayName}' Story가 {reason} 상태로 완료됐습니다.", this);
    }

    /// <summary>
    /// StoryTest 자동 재생에 필요한 Runner와 샘플 데이터 참조가 연결되어 있는지 확인한다.
    /// </summary>
    public bool HasValidReference()
    {
        bool isValid = true;

        if (runner == null)
        {
            Debug.LogError($"{nameof(StoryTestLauncher)} on {name}에는 {nameof(StoryRunner)} 참조가 필요합니다.", this);
            isValid = false;
        }

        if (sampleSequence == null)
        {
            Debug.LogError($"{nameof(StoryTestLauncher)} on {name}에는 샘플 {nameof(StorySequenceData)} 참조가 필요합니다.", this);
            isValid = false;
        }

        return isValid;
    }
}
