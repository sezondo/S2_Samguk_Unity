using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 외부에서 완성한 만화 이미지 한 장을 Story 화면에 표시한다.
/// </summary>
public class StoryComicPanelPresenter : MonoBehaviour
{
    [Header("Root")]
    // 만화 패널 화면 전체의 표시 상태를 제어하는 CanvasGroup이다.
    [SerializeField] private CanvasGroup comicPanelGroup;

    [Header("Comic Image")]
    // 완성된 만화 이미지 한 장을 표시할 화면 루트다.
    [SerializeField] private GameObject fullScreenRoot;
    // 외부에서 완성한 만화 이미지를 표시할 Image다.
    [SerializeField] private Image fullScreenImage;

    /// <summary>
    /// 단일 만화 이미지 표시 참조를 검사하고 화면을 숨긴다.
    /// </summary>
    private void Awake()
    {
        if (!HasValidReference())
        {
            enabled = false;
            return;
        }

        HideImmediately();
    }

    /// <summary>
    /// 외부에서 완성한 만화 이미지 한 장을 화면에 표시한다.
    /// </summary>
    public bool Show(Sprite comicImage)
    {
        if (!isActiveAndEnabled || !HasValidData(comicImage))
        {
            return false;
        }

        fullScreenImage.sprite = comicImage;
        fullScreenRoot.SetActive(true);
        comicPanelGroup.alpha = 1f;
        comicPanelGroup.interactable = false;
        comicPanelGroup.blocksRaycasts = false;
        return true;
    }

    /// <summary>
    /// 현재 만화 이미지와 화면을 즉시 숨기고 초기화한다.
    /// </summary>
    public void HideImmediately()
    {
        if (fullScreenRoot != null)
        {
            fullScreenRoot.SetActive(false);
        }

        if (fullScreenImage != null)
        {
            fullScreenImage.sprite = null;
        }

        if (comicPanelGroup != null)
        {
            comicPanelGroup.alpha = 0f;
            comicPanelGroup.interactable = false;
            comicPanelGroup.blocksRaycasts = false;
        }
    }

    /// <summary>
    /// 단일 만화 이미지 화면에 필요한 참조가 모두 연결되어 있는지 확인한다.
    /// </summary>
    public bool HasValidReference()
    {
        bool isValid =
            comicPanelGroup != null &&
            fullScreenRoot != null &&
            fullScreenImage != null;

        if (isValid)
        {
            return true;
        }

        Debug.LogError($"{nameof(StoryComicPanelPresenter)} on {name}에는 만화 화면 CanvasGroup, 루트와 Image 참조가 필요합니다.", this);
        return false;
    }

    /// <summary>
    /// 표시할 완성 만화 이미지가 비어 있지 않은지 확인한다.
    /// </summary>
    public bool HasValidData(Sprite comicImage)
    {
        if (comicImage == null)
        {
            Debug.LogError($"{nameof(StoryComicPanelPresenter)} on {name}에 표시할 완성 만화 이미지가 비어 있습니다.", this);
            return false;
        }

        return true;
    }
}
