using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Story 화면의 전체 배경 Sprite 교체를 담당한다.
/// </summary>
public class StoryBackgroundPresenter : MonoBehaviour
{
    [Header("Reference")]
    // Story 화면 전체에 배경을 표시하는 Image다.
    [SerializeField] private Image backgroundImage;

    /// <summary>
    /// 필수 배경 Image 참조가 연결되어 있는지 확인한다.
    /// </summary>
    private void Awake()
    {
        if (!HasValidReference())
        {
            enabled = false;
        }
    }

    /// <summary>
    /// 지정한 Sprite를 현재 Story 배경으로 즉시 표시한다.
    /// </summary>
    public bool ShowBackground(Sprite backgroundSprite)
    {
        if (!isActiveAndEnabled || backgroundSprite == null)
        {
            Debug.LogError($"{nameof(StoryBackgroundPresenter)} on {name}이 비어 있는 배경 Sprite를 받았습니다.", this);
            return false;
        }

        backgroundImage.sprite = backgroundSprite;
        backgroundImage.enabled = true;
        return true;
    }

    /// <summary>
    /// Story 배경 표시에 필요한 Image 참조가 연결되어 있는지 확인한다.
    /// </summary>
    public bool HasValidReference()
    {
        if (backgroundImage != null)
        {
            return true;
        }

        Debug.LogError($"{nameof(StoryBackgroundPresenter)} on {name}에는 배경 {nameof(Image)} 참조가 필요합니다.", this);
        return false;
    }
}
