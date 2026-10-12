using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SpeechBubbleView : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private Camera worldCamera;

    [Header("Bubble")]
    [SerializeField] private Image bubbleImage;
    // 흰색 말풍선의 별도 꼬리와 몸통 접합부다. 기존 프리팹에서는 생략할 수 있다.
    [SerializeField] private Image tailImage, tailJoinImage;
    // 밝은 바탕에서는 짙은 청록, 기존 어두운 바탕에서는 밝은 청록을 사용한다.
    [SerializeField] private Color speakerNameColor = new(.46f, .78f, .79f, 1);

    [Header("Text")]
    [SerializeField] private TMP_Text dialogueText;
    [SerializeField] private Font koreanSourceFont;
    [SerializeField] private float horizontalOffset = 32f;

    [Header("Follow")]
    [SerializeField] private Vector2 screenOffset = new(0f, 42f);

    private Transform speakerAnchor;
    private RectTransform root;
    private Canvas canvas;
    private bool visible;
    private Color defaultTextColor;
    private float defaultFontSize;
    private float typewriterTimer;
    private float charactersPerSecond;
    private int visibleCharacterCount;
    private int totalCharacterCount;
    private bool typewriterActive;

    public bool IsTextFullyVisible => !typewriterActive || visibleCharacterCount >= totalCharacterCount;

    private void Awake()
    {
        ResolveReferences();
        if (dialogueText != null) CacheDefaultTextStyle();
    }

    private void Update()
    {
        if (!visible || !typewriterActive || IsTextFullyVisible)
        {
            return;
        }

        UpdateTypewriter();
    }

    private void LateUpdate()
    {
        if (!visible || speakerAnchor == null)
        {
            return;
        }

        FollowSpeakerAnchor();
    }

    /// <summary>Presenter에서 카메라와 Canvas 참조를 명시적으로 전달받는다.</summary>
    public void Initialize(Camera camera, Canvas parentCanvas)
    { worldCamera = camera; canvas = parentCanvas; ResolveReferences(); CacheDefaultTextStyle(); }

    /// <summary>화자 이름과 대사를 줄바꿈 가능한 말풍선에 표시한다.</summary>
    public void Show(Transform anchor, DialogueLineData line)
    {
        if (!HasRequiredLayoutReferences() || anchor == null || line == null)
        {
            Debug.LogError($"{nameof(SpeechBubbleView)}: {name} 오브젝트가 비어 있는 대사 줄을 받았습니다.", this);
            return;
        }

        speakerAnchor = anchor;
        visible = true;
        gameObject.SetActive(true);

        ApplyLineTextStyle(line);
        dialogueText.text = $"<size=80%><b><color=#{ColorUtility.ToHtmlStringRGB(speakerNameColor)}>{line.speakerTag}</color></b></size>\n{line.text}";
        dialogueText.ForceMeshUpdate();

        RebuildLayout();
        StartTypewriter(line);
        FollowSpeakerAnchor();
    }

    /// <summary>말풍선과 타이핑 상태를 정리한다.</summary>
    public void Hide()
    {
        visible = false;
        speakerAnchor = null;

        if (dialogueText != null)
        {
            dialogueText.text = string.Empty;
            dialogueText.maxVisibleCharacters = int.MaxValue;
        }

        typewriterTimer = 0f;
        charactersPerSecond = 0f;
        visibleCharacterCount = 0;
        totalCharacterCount = 0;
        typewriterActive = false;
        gameObject.SetActive(false);
    }

    /// <summary>첫 진행 입력으로 현재 문장을 즉시 완성한다.</summary>
    public void CompleteText()
    {
        visibleCharacterCount = totalCharacterCount;
        typewriterActive = false;

        if (dialogueText != null)
        {
            dialogueText.maxVisibleCharacters = totalCharacterCount;
        }
    }

    private void ResolveReferences()
    {
        root = transform as RectTransform;

        if (bubbleImage != null)
        {
            bubbleImage.type = Image.Type.Sliced;
        }

        if (dialogueText != null)
        {
            dialogueText.textWrappingMode = TextWrappingModes.Normal;
            dialogueText.overflowMode = TextOverflowModes.Truncate;
        }
    }

    private void CacheDefaultTextStyle()
    {
        defaultTextColor = dialogueText.color;
        defaultFontSize = dialogueText.fontSize;
    }

    private void ApplyLineTextStyle(DialogueLineData line)
    {
        dialogueText.color = line.overrideTextColor ? line.textColor : defaultTextColor;
        dialogueText.fontSize = line.overrideFontSize ? line.fontSize : defaultFontSize;
    }

    private void StartTypewriter(DialogueLineData line)
    {
        totalCharacterCount = dialogueText.textInfo.characterCount;
        visibleCharacterCount = line.useTypewriter ? 0 : totalCharacterCount;
        typewriterTimer = 0f;
        charactersPerSecond = line.charactersPerSecond;
        typewriterActive = line.useTypewriter && totalCharacterCount > 0 && charactersPerSecond > 0f;
        dialogueText.maxVisibleCharacters = visibleCharacterCount;

        if (!typewriterActive)
        {
            CompleteText();
        }
    }

    private void UpdateTypewriter()
    {
        typewriterTimer += Time.deltaTime * charactersPerSecond;
        int nextVisibleCharacterCount = Mathf.Min(totalCharacterCount, Mathf.FloorToInt(typewriterTimer));
        if (nextVisibleCharacterCount <= visibleCharacterCount)
        {
            return;
        }

        visibleCharacterCount = nextVisibleCharacterCount;
        dialogueText.maxVisibleCharacters = visibleCharacterCount;

        if (visibleCharacterCount >= totalCharacterCount)
        {
            typewriterActive = false;
        }
    }

    /// <summary>긴 대사도 화면 밖으로 늘어나지 않게 너비를 제한하고 높이를 계산한다.</summary>
    private void RebuildLayout()
    {
        dialogueText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 480f);
        dialogueText.ForceMeshUpdate();
        float height = dialogueText.GetPreferredValues(dialogueText.text, 480f, 0f).y + 36f;
        root.sizeDelta = new Vector2(520f, Mathf.Max(100f, height));
        // 재사용된 짧은 말풍선의 높이로 잘린 글자 수를 타이핑 종료점으로 삼지 않는다.
        dialogueText.ForceMeshUpdate();
    }

    /// <summary>화자를 따라가되 화면 가장자리와 하단 전투 HUD를 피한다.</summary>
    private void FollowSpeakerAnchor()
    {
        if (!HasRequiredLayoutReferences() || speakerAnchor == null) return;
        var canvasRect = (RectTransform)canvas.transform;
        Vector2 screenPoint = worldCamera.WorldToScreenPoint(speakerAnchor.position);
        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, uiCamera, out var localPoint);
        float speakerX = localPoint.x;
        localPoint += screenOffset;
        Rect bounds = canvasRect.rect;
        localPoint.x = Mathf.Clamp(localPoint.x, bounds.xMin + root.rect.width * 0.5f + 20f, bounds.xMax - root.rect.width * 0.5f - 20f);
        localPoint.y = Mathf.Clamp(localPoint.y, bounds.yMin + 250f, bounds.yMax - root.rect.height - 190f);
        root.anchoredPosition = localPoint;
        // 몸통이 화면 가장자리에서 이동해도 꼬리는 실제 화자 쪽을 가리킨다.
        float tailX = Mathf.Clamp(speakerX - localPoint.x, -root.rect.width * .5f + 44f, root.rect.width * .5f - 44f);
        if (tailImage != null) tailImage.rectTransform.anchoredPosition = new Vector2(tailX, 5f);
        if (tailJoinImage != null) tailJoinImage.rectTransform.anchoredPosition = new Vector2(tailX, 2f);
    }

    private bool HasRequiredLayoutReferences()
    {
        if (root != null && canvas != null && worldCamera != null && bubbleImage != null && dialogueText != null && dialogueText.font != null)
        {
            return true;
        }

        Debug.LogError($"{nameof(SpeechBubbleView)}: {name} 오브젝트에는 부모 Canvas, bubbleImage, dialogueText 참조가 필요합니다.", this);
        return false;
    }

    private static void SetWidth(RectTransform target, float width)
    {
        target.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
    }

    private static float ResolveHorizontalScale(RectTransform target)
    {
        float scale = Mathf.Abs(target.lossyScale.x);
        return scale > 0.0001f ? scale : 1f;
    }
}
