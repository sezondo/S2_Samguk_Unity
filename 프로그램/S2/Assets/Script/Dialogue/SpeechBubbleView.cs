using TMPro;
using UnityEngine;

public class SpeechBubbleView : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private RectTransform root;
    [SerializeField] private Canvas canvas;
    [SerializeField] private Camera worldCamera;
    [SerializeField] private Camera uiCamera;

    [Header("Bubble Pieces")]
    [SerializeField] private RectTransform leftCap;
    [SerializeField] private RectTransform leftScale;
    [SerializeField] private RectTransform center;
    [SerializeField] private RectTransform rightScale;
    [SerializeField] private RectTransform rightCap;

    [Header("Text")]
    [SerializeField] private TMP_Text dialogueText;
    [SerializeField] private float horizontalTextPadding = 24f;
    [SerializeField] private float verticalTextPadding = 10f;
    [SerializeField] private float maxTextWidth = 360f;

    [Header("Scale")]
    [SerializeField] private float minScaleWidth = 0f;
    [SerializeField] private float maxScaleWidth = 180f;

    [Header("Follow")]
    [SerializeField] private Vector2 screenOffset = new(0f, 42f);

    private Transform speakerAnchor;
    private bool visible;

    private void Awake()
    {
        ResolveReferences();
        Hide();
    }

    private void LateUpdate()
    {
        if (!visible || speakerAnchor == null)
        {
            return;
        }

        FollowSpeakerAnchor();
    }

    public void Show(Transform anchor, string text)
    {
        speakerAnchor = anchor;
        visible = true;
        gameObject.SetActive(true);

        if (dialogueText != null)
        {
            dialogueText.text = text;
            dialogueText.ForceMeshUpdate();
        }

        RebuildLayout(text);
        FollowSpeakerAnchor();
    }

    public void Hide()
    {
        visible = false;
        speakerAnchor = null;

        if (dialogueText != null)
        {
            dialogueText.text = string.Empty;
        }

        gameObject.SetActive(false);
    }

    private void ResolveReferences()
    {
        if (root == null)
        {
            root = transform as RectTransform;
        }

        if (canvas == null)
        {
            canvas = GetComponentInParent<Canvas>();
        }

        if (worldCamera == null)
        {
            worldCamera = Camera.main;
        }
    }

    private void RebuildLayout(string text)
    {
        if (!HasRequiredLayoutReferences())
        {
            return;
        }

        float leftCapWidth = leftCap.rect.width;
        float centerWidth = center.rect.width;
        float rightCapWidth = rightCap.rect.width;
        float fixedWidth = leftCapWidth + centerWidth + rightCapWidth;

        Vector2 preferredTextSize = dialogueText.GetPreferredValues(text, maxTextWidth, 0f);
        float targetWidth = Mathf.Max(fixedWidth, preferredTextSize.x + horizontalTextPadding);
        float scaleWidth = Mathf.Clamp((targetWidth - fixedWidth) * 0.5f, minScaleWidth, maxScaleWidth);
        float totalWidth = fixedWidth + (scaleWidth * 2f);
        float totalHeight = Mathf.Max(root.rect.height, preferredTextSize.y + verticalTextPadding);

        SetWidth(leftScale, scaleWidth);
        SetWidth(rightScale, scaleWidth);
        SetWidth(root, totalWidth);
        SetHeight(root, totalHeight);

        PlacePieces(leftCapWidth, scaleWidth, centerWidth, rightCapWidth);
        RebuildTextRect(totalWidth, totalHeight);
    }

    private void PlacePieces(float leftCapWidth, float scaleWidth, float centerWidth, float rightCapWidth)
    {
        SetAnchoredX(center, 0f);
        SetAnchoredX(leftScale, -(centerWidth * 0.5f) - (scaleWidth * 0.5f));
        SetAnchoredX(leftCap, -(centerWidth * 0.5f) - scaleWidth - (leftCapWidth * 0.5f));
        SetAnchoredX(rightScale, (centerWidth * 0.5f) + (scaleWidth * 0.5f));
        SetAnchoredX(rightCap, (centerWidth * 0.5f) + scaleWidth + (rightCapWidth * 0.5f));
    }

    private void RebuildTextRect(float totalWidth, float totalHeight)
    {
        RectTransform textRect = dialogueText.rectTransform;
        SetWidth(textRect, Mathf.Max(0f, totalWidth - horizontalTextPadding));
        SetHeight(textRect, Mathf.Max(0f, totalHeight - verticalTextPadding));
        textRect.anchoredPosition = Vector2.zero;
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
        Camera cameraForUi = uiCamera != null ? uiCamera : canvas.worldCamera;
        if (canvasRect != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, cameraForUi, out Vector2 localPoint))
        {
            root.anchoredPosition = localPoint;
        }
    }

    private bool HasRequiredLayoutReferences()
    {
        if (root != null && leftCap != null && leftScale != null && center != null
            && rightScale != null && rightCap != null && dialogueText != null)
        {
            return true;
        }

        Debug.LogError($"{nameof(SpeechBubbleView)} on {name} requires all bubble pieces and text references.", this);
        return false;
    }

    private static void SetWidth(RectTransform target, float width)
    {
        target.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
    }

    private static void SetHeight(RectTransform target, float height)
    {
        target.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
    }

    private static void SetAnchoredX(RectTransform target, float x)
    {
        Vector2 position = target.anchoredPosition;
        position.x = x;
        target.anchoredPosition = position;
    }
}
