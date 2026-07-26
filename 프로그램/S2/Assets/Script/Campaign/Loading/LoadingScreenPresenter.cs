using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 영속 Canvas의 표시, 입력 차단, 페이드와 로딩 진행률 표시를 담당한다.
/// </summary>
public class LoadingScreenPresenter : MonoBehaviour
{
    [Header("Reference")]
    // 로딩 화면 전체의 투명도와 입력 차단 상태를 제어하는 CanvasGroup이다.
    [SerializeField] private CanvasGroup canvasGroup;
    // 비동기 씬 로딩 진행률을 0~1 값으로 표시하는 Slider다.
    [SerializeField] private Slider progressBar;

    [Header("Fade")]
    // 로딩 화면이 나타나고 사라질 때 사용하는 비스케일 시간 기준 페이드 길이다.
    [SerializeField] private float fadeDuration = 0.2f;

    /// <summary>
    /// 필수 UI 참조와 페이드 설정을 검사하고 로딩 화면을 숨긴 상태로 초기화한다.
    /// </summary>
    private void Awake()
    {
        if (!HasValidReference() || !HasValidData())
        {
            enabled = false;
            return;
        }

        HideImmediately();
    }

    /// <summary>
    /// 로딩 화면을 즉시 숨기고 입력 차단을 해제한다.
    /// </summary>
    public void HideImmediately()
    {
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        SetProgress(0f);
    }

    /// <summary>
    /// 로딩 화면의 진행률을 0~1 범위로 갱신한다.
    /// </summary>
    public void SetProgress(float progress)
    {
        progressBar.SetValueWithoutNotify(Mathf.Clamp01(progress));
    }

    /// <summary>
    /// 입력을 차단하면서 로딩 화면을 서서히 표시한다.
    /// </summary>
    public IEnumerator FadeIn()
    {
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
        yield return FadeTo(1f);
    }

    /// <summary>
    /// 로딩 화면을 서서히 숨긴 뒤 입력 차단을 해제한다.
    /// </summary>
    public IEnumerator FadeOut()
    {
        yield return FadeTo(0f);
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    /// <summary>
    /// CanvasGroup의 투명도를 비스케일 시간으로 목표값까지 보간한다.
    /// </summary>
    private IEnumerator FadeTo(float targetAlpha)
    {
        float startAlpha = canvasGroup.alpha;
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / fadeDuration);
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;
    }

    /// <summary>
    /// 로딩 화면에 필요한 UI 참조가 연결되어 있는지 확인한다.
    /// </summary>
    public bool HasValidReference()
    {
        if (canvasGroup == null)
        {
            Debug.LogError($"{nameof(LoadingScreenPresenter)} on {name}에는 {nameof(CanvasGroup)} 참조가 필요합니다.", this);
            return false;
        }

        if (progressBar == null)
        {
            Debug.LogError($"{nameof(LoadingScreenPresenter)} on {name}에는 진행률을 표시할 {nameof(Slider)} 참조가 필요합니다.", this);
            return false;
        }

        return true;
    }

    /// <summary>
    /// 로딩 화면의 페이드 시간이 사용할 수 있는 값인지 확인한다.
    /// </summary>
    public bool HasValidData()
    {
        if (fadeDuration <= 0f)
        {
            Debug.LogError($"{nameof(LoadingScreenPresenter)} on {name}의 페이드 시간은 0보다 커야 합니다.", this);
            return false;
        }

        return true;
    }
}
