using UnityEngine;

/// <summary>
/// Story 씬의 Runner, 입력과 화면 Presenter 참조를 모아 제공하는 참조 주머니다.
/// </summary>
public class StoryContext : MonoBehaviour
{
    [Header("Runtime")]
    // 선형 Story 명령 실행과 완료 통지를 담당하는 Runner다.
    [SerializeField] private StoryRunner runner;
    // Story 진행과 스킵 입력을 Runner에 전달하는 입력 컴포넌트다.
    [SerializeField] private StoryInputReader inputReader;

    [Header("Presentation")]
    // 화자명, 대사 본문과 타이핑을 담당하는 Presenter다.
    [SerializeField] private StoryDialoguePresenter dialoguePresenter;
    // 배경 Sprite 표시를 담당하는 Presenter다.
    [SerializeField] private StoryBackgroundPresenter backgroundPresenter;
    // 좌·중앙·우 스탠딩과 포커스를 담당하는 Presenter다.
    [SerializeField] private StoryStandingPresenter standingPresenter;
    // 고정 레이아웃 만화 패널 표시를 담당하는 Presenter다.
    [SerializeField] private StoryComicPanelPresenter comicPanelPresenter;
    // Story 화면 전체 페이드를 담당하는 Presenter다.
    [SerializeField] private StoryFadePresenter fadePresenter;

    public StoryRunner Runner => runner;
    public StoryInputReader InputReader => inputReader;
    public StoryDialoguePresenter DialoguePresenter => dialoguePresenter;
    public StoryBackgroundPresenter BackgroundPresenter => backgroundPresenter;
    public StoryStandingPresenter StandingPresenter => standingPresenter;
    public StoryComicPanelPresenter ComicPanelPresenter => comicPanelPresenter;
    public StoryFadePresenter FadePresenter => fadePresenter;

    /// <summary>
    /// Story 재생에 필요한 모든 필수 컴포넌트 참조가 연결되어 있는지 확인한다.
    /// </summary>
    public bool HasValidReference()
    {
        bool isValid = true;

        if (runner == null)
        {
            Debug.LogError($"{nameof(StoryContext)} on {name}에는 {nameof(StoryRunner)} 참조가 필요합니다.", this);
            isValid = false;
        }

        if (inputReader == null)
        {
            Debug.LogError($"{nameof(StoryContext)} on {name}에는 {nameof(StoryInputReader)} 참조가 필요합니다.", this);
            isValid = false;
        }

        if (dialoguePresenter == null)
        {
            Debug.LogError($"{nameof(StoryContext)} on {name}에는 {nameof(StoryDialoguePresenter)} 참조가 필요합니다.", this);
            isValid = false;
        }

        if (backgroundPresenter == null)
        {
            Debug.LogError($"{nameof(StoryContext)} on {name}에는 {nameof(StoryBackgroundPresenter)} 참조가 필요합니다.", this);
            isValid = false;
        }

        if (standingPresenter == null)
        {
            Debug.LogError($"{nameof(StoryContext)} on {name}에는 {nameof(StoryStandingPresenter)} 참조가 필요합니다.", this);
            isValid = false;
        }

        if (comicPanelPresenter == null)
        {
            Debug.LogError($"{nameof(StoryContext)} on {name}에는 {nameof(StoryComicPanelPresenter)} 참조가 필요합니다.", this);
            isValid = false;
        }

        if (fadePresenter == null)
        {
            Debug.LogError($"{nameof(StoryContext)} on {name}에는 {nameof(StoryFadePresenter)} 참조가 필요합니다.", this);
            isValid = false;
        }

        return isValid;
    }
}
