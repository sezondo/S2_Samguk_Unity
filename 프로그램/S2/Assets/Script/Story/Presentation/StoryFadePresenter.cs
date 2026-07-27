using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Story 화면 전체를 비스케일 시간으로 가리거나 드러내는 페이드를 담당한다.
/// </summary>
public class StoryFadePresenter : MonoBehaviour
{
    [Header("Reference")]
    // Story 화면 위를 덮는 페이드 오버레이의 CanvasGroup이다.
    [SerializeField] private CanvasGroup fadeGroup;

    // 현재 실행 중인 페이드 코루틴이다.
    private Coroutine fadeRoutine;

    /// <summary>
    /// 필수 페이드 오버레이 참조가 연결되어 있는지 확인한다.
    /// </summary>
    private void Awake()
    {
        if (!HasValidReference())
        {
            enabled = false;
        }
    }

    /// <summary>
    /// 지정한 방향과 시간으로 화면 페이드를 실행하고 완료 콜백을 한 번 호출한다.
    /// </summary>
    public bool PlayFade(StoryFadeDirection direction, float duration, Action completed)
    {
        if (!isActiveAndEnabled || duration <= 0f)
        {
            Debug.LogError($"{nameof(StoryFadePresenter)} on {name}의 페이드 시간은 0보다 커야 합니다.", this);
            return false;
        }

        StopFade();
        fadeRoutine = StartCoroutine(FadeRoutine(direction, duration, completed));
        return true;
    }

    /// <summary>
    /// 현재 페이드를 중단하고 실행 중 코루틴 참조를 정리한다.
    /// </summary>
    public void StopFade()
    {
        if (fadeRoutine == null)
        {
            return;
        }

        StopCoroutine(fadeRoutine);
        fadeRoutine = null;
    }

    /// <summary>
    /// 페이드 오버레이 알파를 지정한 값으로 즉시 설정한다.
    /// </summary>
    public void SetAlphaImmediately(float alpha)
    {
        StopFade();
        fadeGroup.alpha = Mathf.Clamp01(alpha);
        fadeGroup.interactable = false;
        fadeGroup.blocksRaycasts = alpha > 0.001f;
    }

    /// <summary>
    /// Story 페이드 표시에 필요한 CanvasGroup 참조가 연결되어 있는지 확인한다.
    /// </summary>
    public bool HasValidReference()
    {
        if (fadeGroup != null)
        {
            return true;
        }

        Debug.LogError($"{nameof(StoryFadePresenter)} on {name}에는 페이드 {nameof(CanvasGroup)} 참조가 필요합니다.", this);
        return false;
    }

    /// <summary>
    /// 비스케일 시간으로 페이드 알파를 보간하고 완료 콜백을 호출한다.
    /// </summary>
    private IEnumerator FadeRoutine(StoryFadeDirection direction, float duration, Action completed)
    {
        float startAlpha = fadeGroup.alpha;
        float targetAlpha = direction == StoryFadeDirection.Out ? 1f : 0f;
        float elapsed = 0f;

        fadeGroup.interactable = false;
        fadeGroup.blocksRaycasts = true;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            fadeGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
            yield return null;
        }

        fadeGroup.alpha = targetAlpha;
        fadeGroup.blocksRaycasts = targetAlpha > 0.001f;
        fadeRoutine = null;
        completed?.Invoke();
    }
}
