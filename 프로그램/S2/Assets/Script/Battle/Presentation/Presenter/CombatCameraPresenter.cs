using System.Collections;
using UnityEngine;

/// <summary>
/// 실제 전장 위에서 공격 시작점과 대상점을 함께 보여 주고 공격 연출 뒤 원래 카메라 상태로 복귀시킨다.
/// 공격 판정에는 관여하지 않고 전투 카메라 포커스와 복귀 연출만 담당한다.
/// </summary>
public class CombatCameraPresenter : MonoBehaviour, IPresentationEventHandler
{
    [Header("Reference")]
    // 위치와 Orthographic Size를 직접 제어할 전투 카메라다.
    [SerializeField] private Camera targetCamera;
    // 이벤트의 그리드 위치를 월드 위치로 변환할 씬 단위 그리드 관리자다.
    [SerializeField] private GridManager gridManager;

    [Header("Framing")]
    // 공격 시작점과 대상점 좌우에 확보할 월드 단위 여백이다.
    [SerializeField] private float horizontalPadding = 2f;
    // 공격 시작점과 대상점 상하에 확보할 월드 단위 여백이다.
    [SerializeField] private float verticalPadding = 1.5f;
    // 가까운 공격에서 카메라가 이 값보다 확대되지 않도록 제한하는 최소 Orthographic Size다.
    [SerializeField] private float minimumOrthographicSize = 3.5f;
    // 먼 공격에서 카메라가 이 값보다 축소되지 않도록 제한하는 최대 Orthographic Size다.
    [SerializeField] private float maximumOrthographicSize = 8.5f;

    [Header("Timing")]
    // 현재 전술 화면에서 공격 구도로 이동하고 줌을 맞출 시간이다.
    [SerializeField] private float focusDuration = 0.2f;
    // 공격 구도 도착 뒤 공격 연출 전에 잠시 멈출 시간이다.
    [SerializeField] private float settleDuration = 0.08f;
    // 공격 구도에서 원래 전술 화면으로 복귀할 시간이다.
    [SerializeField] private float restoreDuration = 0.25f;

    [Header("Log")]
    // true면 전투 카메라 포커스와 복귀 흐름을 Unity 콘솔에 출력한다.
    [SerializeField] private bool logCameraFlow = true;

    // 현재 실행 중인 카메라 이동 코루틴이다.
    private Coroutine cameraCoroutine;
    // 현재 처리 중인 큐 이벤트 완료 핸들이다.
    private PresentationEventHandle activeHandle;
    // 포커스 시작 직전 카메라의 월드 위치다.
    private Vector3 savedPosition;
    // 포커스 시작 직전 카메라의 Orthographic Size다.
    private float savedOrthographicSize;
    // 복귀할 원래 카메라 상태를 저장했는지 나타낸다.
    private bool hasSavedCameraState;

    /// <summary>
    /// 필수 참조와 카메라 연출 값을 검사하고 연출 큐 등록을 시도한다.
    /// </summary>
    private void OnEnable()
    {
        if (!HasValidReference() || !HasValidData())
        {
            enabled = false;
            return;
        }

        TryRegisterQueue(false);
    }

    /// <summary>
    /// 초기화 순서 때문에 OnEnable에서 놓친 큐 등록을 시작 시점에 다시 시도한다.
    /// </summary>
    private void Start()
    {
        TryRegisterQueue(true);
    }

    /// <summary>
    /// 큐 등록과 진행 중인 연출을 정리하고 저장된 카메라 상태를 즉시 복구한다.
    /// </summary>
    private void OnDisable()
    {
        if (ActionPresentationQueue.Instance != null)
        {
            ActionPresentationQueue.Instance.QueueEmptied -= HandleQueueEmptied;
            ActionPresentationQueue.Instance.Unregister(this);
        }

        if (cameraCoroutine != null)
        {
            StopCoroutine(cameraCoroutine);
            cameraCoroutine = null;
        }

        RestoreSavedCameraImmediately();

        if (activeHandle != null && !activeHandle.IsCompleted)
        {
            activeHandle.Complete();
        }

        activeHandle = null;
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
                Debug.LogError($"{nameof(CombatCameraPresenter)} on {name}에는 씬의 {nameof(ActionPresentationQueue)}가 필요합니다.", this);
                enabled = false;
            }

            return;
        }

        queue.Register(this);
        queue.QueueEmptied -= HandleQueueEmptied;
        queue.QueueEmptied += HandleQueueEmptied;
    }

    /// <summary>
    /// 예외적인 이벤트 누락으로 복귀 이벤트 없이 큐가 끝나면 저장된 카메라 상태를 즉시 복구한다.
    /// </summary>
    private void HandleQueueEmptied()
    {
        if (!hasSavedCameraState || cameraCoroutine != null)
        {
            return;
        }

        Debug.LogError($"{nameof(CombatCameraPresenter)} on {name}의 전투 카메라 복귀 이벤트가 누락되어 원래 상태를 즉시 복구합니다.", this);
        RestoreSavedCameraImmediately();
    }

    /// <summary>
    /// 전투 카메라 포커스 또는 복귀 이벤트인지 확인한다.
    /// </summary>
    public bool CanHandle(PresentationEvent presentationEvent)
    {
        return presentationEvent.Type == PresentationEventType.CombatCameraFocus ||
               presentationEvent.Type == PresentationEventType.CombatCameraRestore;
    }

    /// <summary>
    /// 이벤트 종류에 따라 공격 구도 진입 또는 원래 카메라 상태 복귀를 시작한다.
    /// </summary>
    public void Handle(PresentationEvent presentationEvent, PresentationEventHandle handle)
    {
        if (cameraCoroutine != null)
        {
            Debug.LogError($"{nameof(CombatCameraPresenter)} on {name}은 이미 카메라 연출을 처리 중입니다. 새 이벤트를 자동 완료합니다. 이벤트: {presentationEvent}", this);
            handle.Complete();
            return;
        }

        activeHandle = handle;
        if (presentationEvent.Type == PresentationEventType.CombatCameraFocus)
        {
            cameraCoroutine = StartCoroutine(FocusRoutine(presentationEvent, handle));
            return;
        }

        cameraCoroutine = StartCoroutine(RestoreRoutine(handle));
    }

    /// <summary>
    /// 현재 카메라 상태를 저장하고 공격 시작점과 대상점이 함께 보이는 위치와 줌으로 이동한다.
    /// </summary>
    private IEnumerator FocusRoutine(PresentationEvent presentationEvent, PresentationEventHandle handle)
    {
        if (hasSavedCameraState)
        {
            Debug.LogError($"{nameof(CombatCameraPresenter)} on {name}에 복귀하지 않은 전투 카메라 상태가 남아 있어 새 포커스를 시작할 수 없습니다.", this);
            CompleteActiveEvent(handle);
            yield break;
        }

        savedPosition = targetCamera.transform.position;
        savedOrthographicSize = targetCamera.orthographicSize;
        hasSavedCameraState = true;

        Vector3 sourceWorldPosition = gridManager.GridToWorld(presentationEvent.FromPosition);
        Vector3 targetWorldPosition = gridManager.GridToWorld(presentationEvent.ToPosition);
        Vector3 focusPosition = CalculateFocusPosition(sourceWorldPosition, targetWorldPosition);
        float focusOrthographicSize = CalculateFocusOrthographicSize(sourceWorldPosition, targetWorldPosition);

        if (logCameraFlow)
        {
            Debug.Log($"{nameof(CombatCameraPresenter)}: {presentationEvent.FromPosition}에서 {presentationEvent.ToPosition}까지 전투 카메라 포커스를 시작합니다. 줌: {savedOrthographicSize:0.##} -> {focusOrthographicSize:0.##}", this);
        }

        yield return MoveCamera(savedPosition, savedOrthographicSize, focusPosition, focusOrthographicSize, focusDuration);

        if (settleDuration > 0f)
        {
            yield return new WaitForSeconds(settleDuration);
        }

        CompleteActiveEvent(handle);
    }

    /// <summary>
    /// 저장된 전술 화면 위치와 줌으로 카메라를 복귀시킨다.
    /// </summary>
    private IEnumerator RestoreRoutine(PresentationEventHandle handle)
    {
        if (!hasSavedCameraState)
        {
            Debug.LogWarning($"{nameof(CombatCameraPresenter)} on {name}에는 복귀할 전투 카메라 상태가 없습니다.", this);
            CompleteActiveEvent(handle);
            yield break;
        }

        Vector3 restorePosition = savedPosition;
        float restoreOrthographicSize = savedOrthographicSize;
        Vector3 currentPosition = targetCamera.transform.position;
        float currentOrthographicSize = targetCamera.orthographicSize;

        if (logCameraFlow)
        {
            Debug.Log($"{nameof(CombatCameraPresenter)}: 전투 카메라를 원래 전술 화면으로 복귀합니다.", this);
        }

        yield return MoveCamera(
            currentPosition,
            currentOrthographicSize,
            restorePosition,
            restoreOrthographicSize,
            restoreDuration);

        hasSavedCameraState = false;
        CompleteActiveEvent(handle);
    }

    /// <summary>
    /// 공격 시작점과 대상점의 중점을 사용하고 현재 카메라 Z축을 유지한 포커스 위치를 계산한다.
    /// </summary>
    private Vector3 CalculateFocusPosition(Vector3 sourceWorldPosition, Vector3 targetWorldPosition)
    {
        Vector3 midpoint = (sourceWorldPosition + targetWorldPosition) * 0.5f;
        midpoint.z = targetCamera.transform.position.z;
        return midpoint;
    }

    /// <summary>
    /// 화면 비율과 여백을 고려해 두 지점이 함께 보이는 Orthographic Size를 계산한다.
    /// </summary>
    private float CalculateFocusOrthographicSize(Vector3 sourceWorldPosition, Vector3 targetWorldPosition)
    {
        float halfWidth = Mathf.Abs(targetWorldPosition.x - sourceWorldPosition.x) * 0.5f + horizontalPadding;
        float halfHeight = Mathf.Abs(targetWorldPosition.y - sourceWorldPosition.y) * 0.5f + verticalPadding;
        float sizeForWidth = halfWidth / targetCamera.aspect;
        float requiredSize = Mathf.Max(halfHeight, sizeForWidth);
        return Mathf.Clamp(requiredSize, minimumOrthographicSize, maximumOrthographicSize);
    }

    /// <summary>
    /// 지정한 시작 상태에서 목표 상태까지 위치와 Orthographic Size를 부드럽게 보간한다.
    /// </summary>
    private IEnumerator MoveCamera(
        Vector3 fromPosition,
        float fromOrthographicSize,
        Vector3 toPosition,
        float toOrthographicSize,
        float duration)
    {
        if (duration <= 0f)
        {
            targetCamera.transform.position = toPosition;
            targetCamera.orthographicSize = toOrthographicSize;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float easedProgress = Mathf.SmoothStep(0f, 1f, progress);
            targetCamera.transform.position = Vector3.LerpUnclamped(fromPosition, toPosition, easedProgress);
            targetCamera.orthographicSize = Mathf.LerpUnclamped(fromOrthographicSize, toOrthographicSize, easedProgress);
            yield return null;
        }

        targetCamera.transform.position = toPosition;
        targetCamera.orthographicSize = toOrthographicSize;
    }

    /// <summary>
    /// 저장된 카메라 상태가 있으면 이동 연출 없이 즉시 복구한다.
    /// </summary>
    private void RestoreSavedCameraImmediately()
    {
        if (!hasSavedCameraState || targetCamera == null)
        {
            return;
        }

        targetCamera.transform.position = savedPosition;
        targetCamera.orthographicSize = savedOrthographicSize;
        hasSavedCameraState = false;
    }

    /// <summary>
    /// 현재 카메라 코루틴 상태를 정리하고 큐 이벤트 완료 신호를 보낸다.
    /// </summary>
    private void CompleteActiveEvent(PresentationEventHandle handle)
    {
        cameraCoroutine = null;
        activeHandle = null;
        handle.Complete();
    }

    /// <summary>
    /// 전투 카메라 연출에 필요한 필수 참조가 연결되어 있는지 확인한다.
    /// </summary>
    public bool HasValidReference()
    {
        if (targetCamera == null)
        {
            Debug.LogError($"{nameof(CombatCameraPresenter)} on {name}에는 전투 연출에 사용할 {nameof(Camera)} 참조가 필요합니다.", this);
            return false;
        }

        if (!targetCamera.orthographic)
        {
            Debug.LogError($"{nameof(CombatCameraPresenter)} on {name}의 전투 카메라는 Orthographic 모드여야 합니다.", this);
            return false;
        }

        if (gridManager == null)
        {
            Debug.LogError($"{nameof(CombatCameraPresenter)} on {name}에는 그리드 위치를 변환할 {nameof(GridManager)} 참조가 필요합니다.", this);
            return false;
        }

        return true;
    }

    /// <summary>
    /// 전투 카메라 여백, 줌 제한과 연출 시간이 사용할 수 있는 값인지 확인한다.
    /// </summary>
    public bool HasValidData()
    {
        if (horizontalPadding < 0f || verticalPadding < 0f)
        {
            Debug.LogError($"{nameof(CombatCameraPresenter)} on {name}의 전투 카메라 여백은 0 이상이어야 합니다.", this);
            return false;
        }

        if (minimumOrthographicSize <= 0f || maximumOrthographicSize < minimumOrthographicSize)
        {
            Debug.LogError($"{nameof(CombatCameraPresenter)} on {name}의 Orthographic Size 범위가 올바르지 않습니다.", this);
            return false;
        }

        if (focusDuration < 0f || settleDuration < 0f || restoreDuration < 0f)
        {
            Debug.LogError($"{nameof(CombatCameraPresenter)} on {name}의 카메라 연출 시간은 0 이상이어야 합니다.", this);
            return false;
        }

        return true;
    }
}
