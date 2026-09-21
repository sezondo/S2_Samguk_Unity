using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 스테이지 클리어와 실패 연출 이벤트를 받아 임시 결과 UI를 표시하고 확정 결과를 알린다.
/// </summary>
public class StageResultPresenter : MonoBehaviour, IPresentationEventHandler
{
    [Header("Reference")]
    // 결과가 발생하기 전에는 숨겨 둘 임시 결과 UI 루트다.
    [SerializeField] private GameObject resultRoot;
    // 클리어 또는 실패 제목을 표시할 텍스트다.
    [SerializeField] private TMP_Text titleText;
    // 결과 상세와 다음 흐름 안내를 표시할 텍스트다.
    [SerializeField] private TMP_Text descriptionText;
    // 클릭으로 결과를 확정할 임시 버튼이다.
    [SerializeField] private Button confirmButton;

    [Header("Debug")]
    // true면 스테이지 결과 연출 이벤트 처리 결과를 Unity 콘솔에 출력한다.
    [SerializeField] private bool logStageResult = true;

    // 현재 결과 연출 이벤트의 큐 완료 핸들이다.
    private PresentationEventHandle pendingHandle;
    // 현재 표시 중인 결과가 클리어인지 나타낸다.
    private bool pendingCleared;

    // 사용자가 결과 UI를 확정했을 때 클리어 여부를 전달한다.
    public event Action<bool> ResultConfirmed;

    /// <summary>
    /// 결과 UI를 숨기고 필수 참조를 즉시 검사한다.
    /// </summary>
    private void Awake()
    {
        if (!HasValidReference())
        {
            enabled = false;
            return;
        }

        resultRoot.SetActive(false);

    }

    /// <summary>
    /// 활성화될 때 현재 씬의 연출 큐에 핸들러 등록을 시도한다.
    /// </summary>
    private void OnEnable()
    {
        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveListener(ConfirmResult);
            confirmButton.onClick.AddListener(ConfirmResult);
        }
        TryRegisterQueue(false);
    }

    /// <summary>
    /// 씬 초기화 순서 때문에 OnEnable에서 놓친 큐 등록을 시작 시점에 한 번 더 시도한다.
    /// </summary>
    private void Start()
    {
        TryRegisterQueue(true);
    }

    /// <summary>
    /// 비활성화될 때 연출 큐 핸들러 등록을 해제한다.
    /// </summary>
    private void OnDisable()
    {
        // 씬 종료·비활성화는 결과 확정으로 취급하지 않고 대기 핸들만 정리한다.
        var handle = pendingHandle;
        pendingHandle = null;
        if (resultRoot != null) resultRoot.SetActive(false);
        handle?.Complete();
        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveListener(ConfirmResult);
        }

        if (ActionPresentationQueue.Instance != null)
        {
            ActionPresentationQueue.Instance.Unregister(this);
        }
    }

    /// <summary>
    /// 결과 UI가 열린 동안 Enter 또는 Space 입력으로도 결과를 확정한다.
    /// </summary>
    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (pendingHandle != null && keyboard != null &&
            (keyboard.enterKey.wasPressedThisFrame ||
             keyboard.numpadEnterKey.wasPressedThisFrame ||
             keyboard.spaceKey.wasPressedThisFrame))
        {
            ConfirmResult();
        }
    }

    /// <summary>
    /// 현재 씬의 연출 큐에 핸들러 등록을 시도한다.
    /// </summary>
    private void TryRegisterQueue(bool logMissingQueue)
    {
        ActionPresentationQueue queue = ActionPresentationQueue.Instance;
        if (queue == null)
        {
            if (logMissingQueue)
            {
                Debug.LogError($"{nameof(StageResultPresenter)} on {name}에는 씬의 {nameof(ActionPresentationQueue)}가 필요합니다.", this);
                enabled = false;
            }

            return;
        }

        queue.Register(this);
    }

    /// <summary>
    /// 스테이지 클리어 또는 실패 연출 이벤트인지 확인한다.
    /// </summary>
    public bool CanHandle(PresentationEvent presentationEvent)
    {
        return presentationEvent.Type == PresentationEventType.StageCleared ||
               presentationEvent.Type == PresentationEventType.StageFailed;
    }

    /// <summary>
    /// 스테이지 결과에 맞는 임시 UI를 열고 사용자 확정을 기다린다.
    /// </summary>
    public void Handle(PresentationEvent presentationEvent, PresentationEventHandle handle)
    {
        if (pendingHandle != null)
        {
            Debug.LogError($"{nameof(StageResultPresenter)}: 이미 다른 결과 UI를 표시 중입니다.", this);
            handle.Complete();
            return;
        }

        pendingCleared = presentationEvent.Type == PresentationEventType.StageCleared;
        pendingHandle = handle;
        titleText.text = pendingCleared ? "MISSION COMPLETE" : "MISSION FAILED";
        descriptionText.text = pendingCleared
            ? "목표를 달성했습니다.\n확인하면 다음 이야기 또는 로비로 이동합니다."
            : "작전 수행에 실패했습니다.\n확인하면 로비로 복귀합니다.";
        resultRoot.SetActive(true);
        confirmButton.Select();

        if (logStageResult)
        {
            Debug.Log($"{nameof(StageResultPresenter)}: {(pendingCleared ? "클리어" : "실패")} 결과 UI를 표시했습니다.", this);
        }
    }

    /// <summary>
    /// 현재 결과를 확정하고 캠페인 연결 이벤트와 연출 큐 완료를 한 번 알린다.
    /// </summary>
    public void ConfirmResult()
    {
        if (pendingHandle == null)
        {
            return;
        }

        bool cleared = pendingCleared;
        PresentationEventHandle completedHandle = pendingHandle;
        pendingHandle = null;
        resultRoot.SetActive(false);
        ResultConfirmed?.Invoke(cleared);
        completedHandle.Complete();
    }

    /// <summary>
    /// 결과 UI 표시에 필요한 모든 참조가 연결되어 있는지 확인한다.
    /// </summary>
    public bool HasValidReference()
    {
        if (resultRoot == null || titleText == null || descriptionText == null || confirmButton == null)
        {
            Debug.LogError($"{nameof(StageResultPresenter)} on {name}에는 결과 UI 루트·텍스트·버튼 참조가 필요합니다.", this);
            return false;
        }

        return true;
    }
}
