using TMPro;
using UnityEngine;

/// <summary>
/// Story 대화창의 화자명, 본문과 타이핑 표시를 담당한다.
/// </summary>
public class StoryDialoguePresenter : MonoBehaviour
{
    [Header("Reference")]
    // 대화창 전체 표시 상태를 제어하는 CanvasGroup이다.
    [SerializeField] private CanvasGroup dialogueGroup;
    // 현재 대사의 화자 이름을 표시하는 TMP 텍스트다.
    [SerializeField] private TMP_Text speakerNameText;
    // 현재 대사 본문과 타이핑 진행을 표시하는 TMP 텍스트다.
    [SerializeField] private TMP_Text dialogueBodyText;
    // 한국어 동적 TMP 폰트 생성에 사용할 원본 폰트다.
    [SerializeField] private Font koreanSourceFont;

    // 현재 본문에 표시할 전체 TMP 문자 수다.
    private int totalCharacterCount;
    // 현재 화면에 표시된 TMP 문자 수다.
    private int visibleCharacterCount;
    // 초당 표시할 문자 수를 누적하는 비스케일 시간 타이머다.
    private float typewriterTimer;
    // 현재 대사에 적용 중인 초당 표시 문자 수다.
    private float charactersPerSecond;
    // 현재 대사가 타이핑 방식으로 진행 중인지 나타낸다.
    private bool isTypewriting;
    // 현재 Presenter가 대화창을 표시 중인지 나타낸다.
    private bool isVisible;

    public bool IsTextComplete => !isTypewriting || visibleCharacterCount >= totalCharacterCount;

    /// <summary>
    /// 필수 UI 참조를 검사하고 한국어 폰트를 적용한 뒤 대화창을 숨긴다.
    /// </summary>
    private void Awake()
    {
        if (!HasValidReference())
        {
            enabled = false;
            return;
        }

        TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(koreanSourceFont);
        if (fontAsset == null)
        {
            Debug.LogError($"{nameof(StoryDialoguePresenter)} on {name}이 한국어 TMP 폰트를 생성하지 못했습니다.", this);
            enabled = false;
            return;
        }

        fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
        fontAsset.isMultiAtlasTexturesEnabled = true;
        speakerNameText.font = fontAsset;
        dialogueBodyText.font = fontAsset;
        HideImmediately();
    }

    /// <summary>
    /// 비스케일 시간을 사용해 현재 대사의 표시 문자 수를 갱신한다.
    /// </summary>
    private void Update()
    {
        if (!isVisible || !isTypewriting || IsTextComplete)
        {
            return;
        }

        typewriterTimer += Time.unscaledDeltaTime * charactersPerSecond;
        int nextVisibleCount = Mathf.Min(totalCharacterCount, Mathf.FloorToInt(typewriterTimer));
        if (nextVisibleCount <= visibleCharacterCount)
        {
            return;
        }

        visibleCharacterCount = nextVisibleCount;
        dialogueBodyText.maxVisibleCharacters = visibleCharacterCount;
        if (visibleCharacterCount >= totalCharacterCount)
        {
            isTypewriting = false;
        }
    }

    /// <summary>
    /// 새 화자와 본문을 대화창에 표시하고 설정에 따라 타이핑을 시작한다.
    /// </summary>
    public bool ShowDialogue(string speakerName, string body, bool useTypewriter, float speed)
    {
        if (!isActiveAndEnabled || !HasValidReference())
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(speakerName) || string.IsNullOrWhiteSpace(body))
        {
            Debug.LogError($"{nameof(StoryDialoguePresenter)} on {name}이 비어 있는 화자 또는 대사 본문을 받았습니다.", this);
            return false;
        }

        speakerNameText.text = speakerName;
        dialogueBodyText.text = body;
        dialogueBodyText.ForceMeshUpdate();

        totalCharacterCount = dialogueBodyText.textInfo.characterCount;
        visibleCharacterCount = useTypewriter ? 0 : totalCharacterCount;
        typewriterTimer = 0f;
        charactersPerSecond = speed;
        isTypewriting = useTypewriter && totalCharacterCount > 0 && speed > 0f;
        dialogueBodyText.maxVisibleCharacters = visibleCharacterCount;

        isVisible = true;
        dialogueGroup.alpha = 1f;
        dialogueGroup.interactable = false;
        dialogueGroup.blocksRaycasts = false;

        if (!isTypewriting)
        {
            CompleteText();
        }

        return true;
    }

    /// <summary>
    /// 현재 타이핑을 중단하고 대사 본문 전체를 즉시 표시한다.
    /// </summary>
    public void CompleteText()
    {
        visibleCharacterCount = totalCharacterCount;
        isTypewriting = false;
        dialogueBodyText.maxVisibleCharacters = totalCharacterCount;
    }

    /// <summary>
    /// 대화창과 현재 타이핑 상태를 즉시 숨기고 초기화한다.
    /// </summary>
    public void HideImmediately()
    {
        isVisible = false;
        isTypewriting = false;
        totalCharacterCount = 0;
        visibleCharacterCount = 0;
        typewriterTimer = 0f;
        charactersPerSecond = 0f;

        if (speakerNameText != null)
        {
            speakerNameText.text = string.Empty;
        }

        if (dialogueBodyText != null)
        {
            dialogueBodyText.text = string.Empty;
            dialogueBodyText.maxVisibleCharacters = int.MaxValue;
        }

        if (dialogueGroup != null)
        {
            dialogueGroup.alpha = 0f;
            dialogueGroup.interactable = false;
            dialogueGroup.blocksRaycasts = false;
        }
    }

    /// <summary>
    /// Story 대화 표시에 필요한 UI와 한국어 원본 폰트 참조가 연결되어 있는지 확인한다.
    /// </summary>
    public bool HasValidReference()
    {
        bool isValid = true;

        if (dialogueGroup == null)
        {
            Debug.LogError($"{nameof(StoryDialoguePresenter)} on {name}에는 {nameof(CanvasGroup)} 참조가 필요합니다.", this);
            isValid = false;
        }

        if (speakerNameText == null)
        {
            Debug.LogError($"{nameof(StoryDialoguePresenter)} on {name}에는 화자명 TMP 텍스트 참조가 필요합니다.", this);
            isValid = false;
        }

        if (dialogueBodyText == null)
        {
            Debug.LogError($"{nameof(StoryDialoguePresenter)} on {name}에는 대사 본문 TMP 텍스트 참조가 필요합니다.", this);
            isValid = false;
        }

        if (koreanSourceFont == null)
        {
            Debug.LogError($"{nameof(StoryDialoguePresenter)} on {name}에는 한국어 원본 폰트 참조가 필요합니다.", this);
            isValid = false;
        }

        return isValid;
    }
}
