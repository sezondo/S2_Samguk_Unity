using System.Collections;
using UnityEngine;

/// <summary>
/// 해킹 연출 이벤트의 연결·진행·완료를 고정 원화와 공개 마스크로 재생한다.
/// 검 접근은 앞선 SwordMove 이벤트가 처리하며 여기서는 해킹 논리 값을 변경하지 않는다.
/// </summary>
public class HackPresenter : MonoBehaviour, IPresentationEventHandler
{
    [Header("Target")]
    // 이 Presenter가 해킹 연출을 처리할 해킹 대상이다.
    [SerializeField] private HackableObject targetHackable;

    // 해킹 구도와 완료 순간 강조를 공유할 카메라 Presenter다.
    [SerializeField] private CombatCameraPresenter cameraPresenter;

    [Header("Presentation")]
    // true면 HackableData.HackDuration만큼 대기한 뒤 큐 완료 신호를 보낸다.
    [SerializeField] private bool waitHackDuration = true;

    [Header("Debug")]
    // true면 해킹 연출 시작과 종료 흐름을 Unity 콘솔에 출력한다.
    [SerializeField] private bool logHackFlow = true;

    // 현재 실행 중인 해킹 연출 코루틴이다.
    private Coroutine hackCoroutine;
    // 현재 처리 중인 큐 이벤트 완료 핸들이다.
    private PresentationEventHandle activeHandle;
    [Header("Hack VFX")]
    // 해킹 문양의 월드 캔버스 크기와 중심 오프셋이다.
    [SerializeField] private float effectSize = 2f;
    [SerializeField] private Vector3 effectOffset = new(0f, 0.7f, 0f);
    // 해킹 시간이 0인 데이터에서도 완료를 읽을 수 있게 하는 최소 표시 시간이다.
    [SerializeField] private float minimumEffectDuration = 0.8f;
    // 현재 재생 중인 네 부품 문양 대여 핸들이다.
    private VfxHandle hackEffect;

    /// <summary>
    /// 필수 참조를 검사하고 연출 큐 등록을 시도한다.
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
    /// 씬 초기화 순서 때문에 OnEnable에서 놓친 큐 등록을 시작 시점에 한 번 더 시도한다.
    /// </summary>
    private void Start()
    {
        TryRegisterQueue(true);
    }

    /// <summary>
    /// 큐 등록을 해제하고 진행 중인 이벤트가 있으면 큐 정지를 막기 위해 완료 처리한다.
    /// </summary>
    private void OnDisable()
    {
        if (ActionPresentationQueue.Instance != null)
        {
            ActionPresentationQueue.Instance.Unregister(this);
        }

        if (hackCoroutine != null)
        {
            StopCoroutine(hackCoroutine);
            hackCoroutine = null;
        }

        hackEffect?.Release(); hackEffect = null;
        if (activeHandle != null && !activeHandle.IsCompleted)
        {
            activeHandle.Complete();
            activeHandle = null;
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
                Debug.LogError($"{nameof(HackPresenter)} on {name}에는 씬의 {nameof(ActionPresentationQueue)}가 필요합니다.", this);
                enabled = false;
            }

            return;
        }

        queue.Register(this);
    }

    /// <summary>
    /// 해킹 연출 이벤트 중 자신이 담당하는 해킹 대상 이벤트인지 확인한다.
    /// </summary>
    public bool CanHandle(PresentationEvent presentationEvent)
    {
        return presentationEvent.Type == PresentationEventType.Hack &&
               presentationEvent.Hackable == targetHackable;
    }

    /// <summary>
    /// 담당 대상의 해킹 연출을 시작한다.
    /// </summary>
    public void Handle(PresentationEvent presentationEvent, PresentationEventHandle handle)
    {
        if (hackCoroutine != null)
        {
            Debug.LogError($"{nameof(HackPresenter)} on {name}은 이미 해킹 연출을 처리 중입니다. 새 이벤트를 자동 완료합니다. 이벤트: {presentationEvent}", this);
            handle.Complete();
            return;
        }

        if (!HasValidReference() || !HasValidData() || GridManager.Instance == null || ActorPresentationRegistry.Instance == null ||
            VfxManager.Instance == null || !VfxManager.Instance.isActiveAndEnabled)
        {
            Debug.LogError("해킹 연출에 필요한 참조 또는 그리드가 없습니다.", this);
            handle.Complete();
            return;
        }

        hackCoroutine = StartCoroutine(PlayHack(presentationEvent, handle));
    }

    /// <summary>사방 꼭지점·회로 공개·맥동·중앙 채움·섬광을 순서대로 재생한다.</summary>
    private IEnumerator PlayHack(PresentationEvent evt, PresentationEventHandle handle)
    {
        activeHandle = handle;
        hackEffect = VfxManager.TryAcquire(VfxId.HackCircuit);
        if (hackEffect != null && hackEffect.PartCount != 4)
        {
            Debug.LogError("해킹 프리팹에는 회로·중앙·섬광·꼭지점 4개 Sprite가 필요합니다.", this);
            hackEffect.Release(); hackEffect = null;
        }
        float duration = Mathf.Max(minimumEffectDuration, waitHackDuration ? targetHackable.HackData.HackDuration : 0f);
        float elapsed = 0f;
        if (logHackFlow) Debug.Log($"{targetHackable.name}의 해킹 이펙트를 시작합니다.", this);
        bool completionPresented = false;
        while (elapsed < duration)
        {
            float progress = Mathf.Clamp01(elapsed / duration);
            if (!completionPresented && progress >= 0.8f)
            {
                completionPresented = true;
                cameraPresenter.PresentHackCompletion(evt);
            }
            UpdateHackEffect(evt, progress, elapsed);
            elapsed += Time.deltaTime;
            yield return null;
        }
        hackEffect?.Release(); hackEffect = null;
        if (logHackFlow) Debug.Log($"{targetHackable.name}의 해킹 이펙트를 완료했습니다.", this);
        CompleteActiveHack(handle);
    }

    /// <summary>점은 고정하고 셰이더 표시량과 빛만 바꿔 문양 흔들림을 방지한다.</summary>
    private void UpdateHackEffect(PresentationEvent evt, float progress, float elapsed)
    {
        if (hackEffect == null || !hackEffect.IsValid) return;
        Vector3 position = GridManager.Instance.GridToWorld(evt.EventPosition) + effectOffset;
        float visibility = 1f;
        int layer = SortingLayer.NameToID("Default");
        int order = 10;
        if (ActorPresentationRegistry.Instance.TryGetVisual(targetHackable.GridActor, out ActorVisualController visual))
        {
            position = visual.TargetRenderer.bounds.center;
            visibility = visual.VisionAlpha;
            layer = visual.TargetRenderer.sortingLayerID;
            order = visual.TargetRenderer.sortingOrder + 3;
        }
        hackEffect.SetTransform(position, Quaternion.identity, Vector3.one);
        hackEffect.SetSorting(layer, order);
        float fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.9f, 1f, progress));
        float alpha = visibility * fade;
        float reveal = Mathf.InverseLerp(0.06f, 0.42f, progress);
        float fill = Mathf.InverseLerp(0.62f, 0.8f, progress);
        float flash = progress < 0.8f ? 0f : progress < 0.86f
            ? Mathf.InverseLerp(0.8f, 0.86f, progress) : 1f - Mathf.InverseLerp(0.86f, 0.96f, progress);
        float pulse = progress >= 0.42f && progress < 0.62f ? (Mathf.Sin(elapsed * 12f) * 0.5f + 0.5f) * 0.25f : 0f;
        Vector2 size = Vector2.one * effectSize;
        hackEffect.SetPart(0, Vector3.zero, size, alpha, reveal, pulse);
        hackEffect.SetPart(1, Vector3.zero, size, alpha, fill);
        hackEffect.SetPart(2, Vector3.zero, size, alpha * flash);
        hackEffect.SetPart(3, Vector3.zero, size, alpha);
    }

    /// <summary>
    /// 현재 해킹 연출 상태를 정리하고 큐 완료 신호를 보낸다.
    /// </summary>
    private void CompleteActiveHack(PresentationEventHandle handle)
    {
        hackCoroutine = null;
        activeHandle = null;
        handle.Complete();
    }

    /// <summary>
    /// 해킹 연출에 필요한 필수 참조가 연결되어 있는지 확인한다.
    /// </summary>
    public bool HasValidReference()
    {
        if (cameraPresenter == null)
        {
            Debug.LogError("해킹 연출 카메라 참조가 없습니다.", this);
            return false;
        }
        if (targetHackable == null)
        {
            Debug.LogError($"{nameof(HackPresenter)} on {name}에는 해킹 연출 대상 {nameof(HackableObject)} 참조가 필요합니다.", this);
            return false;
        }

        if (!targetHackable.HasValidReference() || !targetHackable.HasValidData())
        {
            return false;
        }

        return true;
    }

    /// <summary>해킹 이펙트의 크기와 최소 표시 시간을 검사한다.</summary>
    public bool HasValidData()
    {
        if (effectSize > 0f && minimumEffectDuration > 0f) return true;
        Debug.LogError("해킹 VFX 크기와 최소 시간은 0보다 커야 합니다.", this);
        return false;
    }
}
