using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SpeechBubbleView : MonoBehaviour
{
    private const string DefaultKoreanFontResourcePath = "Fonts/NotoSansKR-VF";
    private static TMP_FontAsset cachedKoreanFontAsset;

    [Header("Root")]
    [SerializeField] private Camera worldCamera;

    [Header("Bubble")]
    [SerializeField] private Image bubbleImage;

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
        if (!HasRequiredLayoutReferences())
        {
            enabled = false;
            return;
        }

        ApplyKoreanFontIfAvailable();
        CacheDefaultTextStyle();
        Hide();
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

    public void Show(Transform anchor, DialogueLineData line)
    {
        if (line == null)
        {
            Debug.LogError($"{nameof(SpeechBubbleView)}: {name} 오브젝트가 비어 있는 대사 줄을 받았습니다.", this);
            return;
        }

        speakerAnchor = anchor;
        visible = true;
        gameObject.SetActive(true);

        ApplyLineTextStyle(line);
        dialogueText.text = line.text ?? string.Empty;
        dialogueText.ForceMeshUpdate();

        RebuildLayout();
        StartTypewriter(line);
        FollowSpeakerAnchor();
    }

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
        canvas = GetComponentInParent<Canvas>();

        if (worldCamera == null)
        {
            worldCamera = Camera.main;
        }

        if (bubbleImage != null)
        {
            bubbleImage.type = Image.Type.Sliced;
        }

        if (dialogueText != null)
        {
            dialogueText.textWrappingMode = TextWrappingModes.NoWrap;
            dialogueText.overflowMode = TextOverflowModes.Overflow;
        }
    }

    private void ApplyKoreanFontIfAvailable()
    {
        if (dialogueText == null)
        {
            return;
        }

        TMP_FontAsset fontAsset = ResolveKoreanFontAsset();
        if (fontAsset != null)
        {
            dialogueText.font = fontAsset;
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

    private TMP_FontAsset ResolveKoreanFontAsset()
    {
        if (cachedKoreanFontAsset != null)
        {
            return cachedKoreanFontAsset;
        }

        Font sourceFont = koreanSourceFont != null
            ? koreanSourceFont
            : Resources.Load<Font>(DefaultKoreanFontResourcePath);

        if (sourceFont == null)
        {
            return null;
        }

        cachedKoreanFontAsset = TMP_FontAsset.CreateFontAsset(sourceFont);
        cachedKoreanFontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
        cachedKoreanFontAsset.isMultiAtlasTexturesEnabled = true;
        return cachedKoreanFontAsset;
    }

    private void RebuildLayout()
    {
        if (!HasRequiredLayoutReferences())
        {
            return;
        }

        dialogueText.ForceMeshUpdate();

        RectTransform textRect = dialogueText.rectTransform;
        RectTransform bubbleRect = bubbleImage.rectTransform;
        float textScale = ResolveHorizontalScale(textRect);
        float bubbleScale = ResolveHorizontalScale(bubbleRect);

        float textVisualWidth = dialogueText.preferredWidth * textScale;
        float targetVisualWidth = textVisualWidth + horizontalOffset;
        float bubbleLocalWidth = targetVisualWidth / bubbleScale;

        SetWidth(bubbleRect, bubbleLocalWidth);
    }

    private void FollowSpeakerAnchor()
    {
        if (root == null || canvas == null || speakerAnchor == null)
        {
            return;
        }

        Camera cameraForWorld = worldCamera != null ? worldCamera : Camera.main;
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(cameraForWorld, speakerAnchor.position) + screenOffset;

        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            root.position = screenPoint;
            return;
        }

        RectTransform canvasRect = canvas.transform as RectTransform;
        Camera cameraForUi = canvas.worldCamera;
        if (canvasRect != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, cameraForUi, out Vector2 localPoint))
        {
            root.anchoredPosition = localPoint;
        }
    }

    private bool HasRequiredLayoutReferences()
    {
        if (root != null && canvas != null && bubbleImage != null && dialogueText != null)
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
