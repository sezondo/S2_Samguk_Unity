using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Story 화면의 좌·중앙·우 스탠딩, 표정과 화자 포커스를 담당한다.
/// </summary>
public class StoryStandingPresenter : MonoBehaviour
{
    [Header("Reference")]
    // 화면 왼쪽 캐릭터 스탠딩을 표시하는 Image다.
    [SerializeField] private Image leftStandingImage;
    // 화면 중앙 캐릭터 스탠딩을 표시하는 Image다.
    [SerializeField] private Image centerStandingImage;
    // 화면 오른쪽 캐릭터 스탠딩을 표시하는 Image다.
    [SerializeField] private Image rightStandingImage;

    [Header("Focus")]
    // 현재 화자로 포커스된 스탠딩에 적용할 색이다.
    [SerializeField] private Color focusedColor = Color.white;
    // 현재 화자가 아닌 스탠딩에 적용할 어두운 색이다.
    [SerializeField] private Color unfocusedColor = new(0.45f, 0.45f, 0.45f, 1f);

    // 현재 화자로 강조 중인 스탠딩 위치다.
    private StoryStandingPosition focusedPosition = StoryStandingPosition.None;

    /// <summary>
    /// 필수 슬롯과 포커스 색을 검사하고 모든 스탠딩을 숨긴다.
    /// </summary>
    private void Awake()
    {
        if (!HasValidReference() || !HasValidData())
        {
            enabled = false;
            return;
        }

        HideAllImmediately();
    }

    /// <summary>
    /// 지정한 슬롯에 캐릭터 Sprite를 표시한다.
    /// </summary>
    public bool ShowStanding(StoryStandingPosition position, Sprite standingSprite)
    {
        Image target = ResolveImage(position);
        if (target == null || standingSprite == null)
        {
            Debug.LogError($"{nameof(StoryStandingPresenter)} on {name}이 잘못된 스탠딩 위치 또는 비어 있는 Sprite를 받았습니다.", this);
            return false;
        }

        target.sprite = standingSprite;
        target.enabled = true;
        target.gameObject.SetActive(true);
        RefreshFocusColors();
        return true;
    }

    /// <summary>
    /// 지정한 슬롯의 현재 캐릭터를 숨긴다.
    /// </summary>
    public bool HideStanding(StoryStandingPosition position)
    {
        Image target = ResolveImage(position);
        if (target == null)
        {
            Debug.LogError($"{nameof(StoryStandingPresenter)} on {name}이 숨길 수 없는 스탠딩 위치를 받았습니다.", this);
            return false;
        }

        target.enabled = false;
        target.sprite = null;
        target.gameObject.SetActive(false);

        if (focusedPosition == position)
        {
            focusedPosition = StoryStandingPosition.None;
        }

        RefreshFocusColors();
        return true;
    }

    /// <summary>
    /// 지정한 슬롯의 캐릭터 Sprite를 새 표정 Sprite로 교체한다.
    /// </summary>
    public bool ChangeExpression(StoryStandingPosition position, Sprite expressionSprite)
    {
        Image target = ResolveImage(position);
        if (target == null || !target.gameObject.activeSelf || expressionSprite == null)
        {
            Debug.LogError($"{nameof(StoryStandingPresenter)} on {name}이 표시되지 않은 슬롯 또는 비어 있는 표정 Sprite를 받았습니다.", this);
            return false;
        }

        target.sprite = expressionSprite;
        return true;
    }

    /// <summary>
    /// 지정한 슬롯을 현재 화자로 강조하고 나머지 표시 슬롯을 어둡게 만든다.
    /// </summary>
    public bool SetFocus(StoryStandingPosition position)
    {
        if (position != StoryStandingPosition.None)
        {
            Image target = ResolveImage(position);
            if (target == null || !target.gameObject.activeSelf)
            {
                Debug.LogError($"{nameof(StoryStandingPresenter)} on {name}이 표시되지 않은 스탠딩에 포커스를 요청받았습니다.", this);
                return false;
            }
        }

        focusedPosition = position;
        RefreshFocusColors();
        return true;
    }

    /// <summary>
    /// 모든 스탠딩 슬롯과 포커스 상태를 즉시 숨기고 초기화한다.
    /// </summary>
    public void HideAllImmediately()
    {
        focusedPosition = StoryStandingPosition.None;
        HideImageImmediately(leftStandingImage);
        HideImageImmediately(centerStandingImage);
        HideImageImmediately(rightStandingImage);
    }

    /// <summary>
    /// Story 스탠딩 표시에 필요한 세 위치의 Image 참조가 연결되어 있는지 확인한다.
    /// </summary>
    public bool HasValidReference()
    {
        if (leftStandingImage != null && centerStandingImage != null && rightStandingImage != null)
        {
            return true;
        }

        Debug.LogError($"{nameof(StoryStandingPresenter)} on {name}에는 좌·중앙·우 스탠딩 {nameof(Image)} 참조가 모두 필요합니다.", this);
        return false;
    }

    /// <summary>
    /// 포커스와 비포커스 색상이 완전히 투명하지 않은지 확인한다.
    /// </summary>
    public bool HasValidData()
    {
        if (focusedColor.a > 0f && unfocusedColor.a > 0f)
        {
            return true;
        }

        Debug.LogError($"{nameof(StoryStandingPresenter)} on {name}의 포커스 색상 알파는 0보다 커야 합니다.", this);
        return false;
    }

    /// <summary>
    /// 고정 화면 위치에 대응하는 스탠딩 Image를 반환한다.
    /// </summary>
    private Image ResolveImage(StoryStandingPosition position)
    {
        return position switch
        {
            StoryStandingPosition.Left => leftStandingImage,
            StoryStandingPosition.Center => centerStandingImage,
            StoryStandingPosition.Right => rightStandingImage,
            _ => null,
        };
    }

    /// <summary>
    /// 현재 포커스 위치를 모든 활성 스탠딩 색상에 반영한다.
    /// </summary>
    private void RefreshFocusColors()
    {
        ApplyFocusColor(leftStandingImage, StoryStandingPosition.Left);
        ApplyFocusColor(centerStandingImage, StoryStandingPosition.Center);
        ApplyFocusColor(rightStandingImage, StoryStandingPosition.Right);
    }

    /// <summary>
    /// 한 스탠딩 슬롯에 현재 포커스 규칙에 맞는 색을 적용한다.
    /// </summary>
    private void ApplyFocusColor(Image target, StoryStandingPosition position)
    {
        if (target == null || !target.gameObject.activeSelf)
        {
            return;
        }

        target.color = focusedPosition == StoryStandingPosition.None || focusedPosition == position
            ? focusedColor
            : unfocusedColor;
    }

    /// <summary>
    /// 지정한 스탠딩 Image의 Sprite와 활성 상태를 즉시 초기화한다.
    /// </summary>
    private static void HideImageImmediately(Image target)
    {
        if (target == null)
        {
            return;
        }

        target.sprite = null;
        target.enabled = false;
        target.gameObject.SetActive(false);
    }
}
