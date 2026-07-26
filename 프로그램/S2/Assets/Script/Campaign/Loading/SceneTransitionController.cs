using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 영속 로딩 화면을 사용해 단일 씬을 비동기로 전환한다.
/// </summary>
public class SceneTransitionController : MonoBehaviour
{
    [Header("Reference")]
    // 모든 씬 위에 유지되는 로딩 화면 Presenter다.
    [SerializeField] private LoadingScreenPresenter loadingScreenPresenter;

    [Header("Loading")]
    // 로딩 화면이 너무 짧게 깜빡이지 않도록 보장할 최소 표시 시간이다.
    [SerializeField] private float minimumVisibleSeconds = 0.25f;

    // 현재 비동기 씬 전환이 진행 중인지 나타낸다.
    private bool isLoading;

    public bool IsLoading => isLoading;

    /// <summary>
    /// 필수 로딩 화면 참조와 설정을 검사한다.
    /// </summary>
    private void Awake()
    {
        if (!HasValidReference() || !HasValidData())
        {
            enabled = false;
        }
    }

    /// <summary>
    /// 지정한 Build Settings 씬으로 비동기 전환을 시작한다.
    /// </summary>
    public bool TryLoadSceneAsync(string sceneName, Action sceneLoaded = null)
    {
        if (!isActiveAndEnabled)
        {
            Debug.LogError($"{nameof(SceneTransitionController)}가 비활성화되어 씬 전환을 시작할 수 없습니다.", this);
            return false;
        }

        if (isLoading)
        {
            Debug.LogWarning($"{nameof(SceneTransitionController)}: 이미 다른 씬을 불러오는 중입니다.", this);
            return false;
        }

        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError($"{nameof(SceneTransitionController)}: 불러올 씬 이름이 비어 있습니다.", this);
            return false;
        }

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"{nameof(SceneTransitionController)}: Build Settings에서 활성화된 씬 '{sceneName}'을 찾을 수 없습니다.", this);
            return false;
        }

        StartCoroutine(LoadSceneRoutine(sceneName, sceneLoaded));
        return true;
    }

    /// <summary>
    /// 로딩 화면을 표시하고 씬을 90%까지 준비한 뒤 활성화한다.
    /// </summary>
    private IEnumerator LoadSceneRoutine(string sceneName, Action sceneLoaded)
    {
        isLoading = true;
        loadingScreenPresenter.SetProgress(0f);
        yield return loadingScreenPresenter.FadeIn();

        float visibleStartTime = Time.unscaledTime;
        AsyncOperation loadOperation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        if (loadOperation == null)
        {
            Debug.LogError($"{nameof(SceneTransitionController)}: 씬 '{sceneName}'의 비동기 로딩 작업을 만들지 못했습니다.", this);
            yield return loadingScreenPresenter.FadeOut();
            isLoading = false;
            yield break;
        }

        loadOperation.allowSceneActivation = false;
        while (loadOperation.progress < 0.9f)
        {
            loadingScreenPresenter.SetProgress(loadOperation.progress / 0.9f);
            yield return null;
        }

        while (Time.unscaledTime - visibleStartTime < minimumVisibleSeconds)
        {
            yield return null;
        }

        loadingScreenPresenter.SetProgress(1f);
        loadOperation.allowSceneActivation = true;
        while (!loadOperation.isDone)
        {
            yield return null;
        }

        sceneLoaded?.Invoke();
        yield return loadingScreenPresenter.FadeOut();
        isLoading = false;
    }

    /// <summary>
    /// 씬 전환에 필요한 로딩 화면 참조가 연결되어 있는지 확인한다.
    /// </summary>
    public bool HasValidReference()
    {
        if (loadingScreenPresenter == null)
        {
            Debug.LogError($"{nameof(SceneTransitionController)} on {name}에는 {nameof(LoadingScreenPresenter)} 참조가 필요합니다.", this);
            return false;
        }

        return loadingScreenPresenter.HasValidReference() && loadingScreenPresenter.HasValidData();
    }

    /// <summary>
    /// 최소 로딩 화면 표시 시간이 사용할 수 있는 값인지 확인한다.
    /// </summary>
    public bool HasValidData()
    {
        if (minimumVisibleSeconds < 0f)
        {
            Debug.LogError($"{nameof(SceneTransitionController)} on {name}의 최소 표시 시간은 0 이상이어야 합니다.", this);
            return false;
        }

        return true;
    }
}
