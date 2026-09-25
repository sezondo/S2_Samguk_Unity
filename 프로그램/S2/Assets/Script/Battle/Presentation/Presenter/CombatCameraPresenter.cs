using System.Collections;
using UnityEngine;

/// <summary>
/// 실제 전장 위에서 공격 시작점과 대상점을 함께 보여 주고 공격 연출 뒤 원래 카메라 상태로 복귀시킨다.
/// 공격 판정에는 관여하지 않고 전투 카메라 포커스와 복귀 연출만 담당한다.
/// </summary>
public class CombatCameraPresenter : MonoBehaviour, IPresentationEventHandler
{
    [Header("Combat Stage")]
    // 시험 비교 옵션이다. false면 두 캐릭터 위치를 유지하며, true면 기존 접근 연출을 사용한다.
    [SerializeField] private bool moveAttackerForCombat = false;
    // 논리 Actor와 현재 화면 비주얼을 연결하는 등록소다.
    [SerializeField] private ActorPresentationRegistry presentationRegistry;
    // 기존 접근 방향을 유지하며 줄일 근접·원거리/해킹 최대 간격이다. 이미 가까우면 이동하지 않는다.
    [SerializeField] private float meleeSeparation = 1.65f;
    [SerializeField] private float rangedSeparation = 2.8f;
    // 타격 순간 대상 방향으로 이동할 거리와 화면 Z 기울기다.
    [SerializeField] private float impactDistance = 0.16f;
    [SerializeField] private float impactRoll = 11.2f;
    // 타격 쏠림의 빠른 진입 시간이다. 도착한 구도는 복귀까지 유지한다.
    [SerializeField] private float impactDuration = 0.07f;
    // 주인공 외 배우에게 적용할 흐림 템플릿이다. 기존 머티리얼은 복귀 시 복원한다.
    [SerializeField] private Material backgroundBlurMaterial;
    // 승인된 한 조각 원화이며 좌우 반전 반복으로 이어 붙인다.
    [SerializeField] private Texture2D borderTexture;
    // 결과 숫자·회피·해킹 완료에 사용할 교체 가능한 임시 글꼴이다.
    [SerializeField] private Font resultFont;
    // 집중 연출 동안 잠시 가릴 기존 HUD Canvas다. 없는 구형 씬은 빈 목록이다.
    [SerializeField] private Canvas[] hudCanvases = System.Array.Empty<Canvas>();
    // 화면 높이에 대한 띠의 최대 안쪽 끝 비율과 초당 흐름 비율이다.
    [SerializeField, Range(0.05f, 0.25f)] private float borderHeight = 0.16f;
    [SerializeField] private float borderSpeed = 0.035f;
    // 결과 텍스트의 유지 시간이다. 복귀가 먼저 끝나면 즉시 함께 정리한다.
    [SerializeField] private float resultDuration = 0.7f;

    // 진입 이벤트 당시의 두 배우와 공격자에게만 적용할 이동량이다.
    private ActorVisualController stagedAttacker, stagedTarget;
    private Vector3 attackerDestinationOffset;
    // 타격 방향·원복 회전·집중 상태의 기본 카메라 위치다.
    private float stageFacing;
    private Quaternion savedRotation;
    private Vector3 stageCameraPosition;
    // 진입/복귀 보간값, 띠 위상과 순간 가속 종료 시각이다.
    private float stageWeight, borderPhase, boostUntil;
    // 타격 쏠림과 복귀 종료 콜백을 보관한다.
    private Coroutine impactCoroutine;
    private System.Action restoredCallbacks;
    // 원상태로 돌릴 주변 배우와 HUD 활성 상태다.
    private readonly System.Collections.Generic.List<ActorVisualController> backgroundActors = new();
    private readonly System.Collections.Generic.Dictionary<Canvas, bool> savedHudStates = new();
    // 런타임에 한 번 만들고 재사용하는 화면 전용 UI와 반복 조각 풀이다.
    private Canvas combatCanvas;
    private RectTransform combatCanvasRect;
    private readonly System.Collections.Generic.List<UnityEngine.UI.RawImage> borderTiles = new();
    private UnityEngine.UI.Text resultText;
    // 결과 표시 위치는 타격 자세 전환 직후 저장하므로 사망 그림에도 흔들리지 않는다.
    private Vector3 resultWorldPosition;
    private float resultStartedAt = -100f;
    public bool IsCombatActive => hasSavedCameraState;
    public bool HasActorTarget => stagedTarget != null;

    /// <summary>복귀 직후 수행할 검 배치·자세 초기화를 현재 연출 수명에 묶는다.</summary>
    public void DeferUntilRestored(System.Action action)
    {
        if (hasSavedCameraState) restoredCallbacks += action;
        else action?.Invoke();
    }

    /// <summary>투척 도착점에 사용할 현재 대상 그림의 중심을 반환한다.</summary>
    public bool TryGetTargetCenter(out Vector3 center)
    {
        center = stagedTarget != null ? stagedTarget.TargetRenderer.bounds.center : default;
        return stagedTarget != null;
    }

    /// <summary>위아래 칸 공격도 실제 무대 배치 방향과 공격 자세 방향을 일치시킨다.</summary>
    public void AlignAttackerFacing(ActorVisualController visual)
    {
        if (!hasSavedCameraState || visual == null || visual != stagedAttacker) return;
        visual.SetCombatFacing(stageFacing > 0f);
    }

    /// <summary>배우와 HUD를 보존하고 두 발 위치를 잇는 방향으로 거리만 줄인다. 피격자 엄폐 위치는 유지한다.</summary>
    private bool PrepareStage(PresentationEvent evt, out Vector3 source, out Vector3 target)
    {
        source = gridManager.GridToWorld(evt.FromPosition);
        target = gridManager.GridToWorld(evt.ToPosition);
        // 좌표만 있는 기존 디버그 이벤트는 카메라 구도 검증 용도로 유지한다.
        if (evt.Actor == null) return true;
        if (!presentationRegistry.TryGetVisual(evt.Actor, out stagedAttacker))
        {
            Debug.LogError("전투 진입 배우의 비주얼 연결이 없습니다.", this);
            return false;
        }
        presentationRegistry.TryGetVisual(evt.TargetActor, out stagedTarget);
        if (evt.TargetActor != null && stagedTarget == null && evt.Hackable == null)
        {
            Debug.LogError("전투 대상의 비주얼 연결이 없습니다.", this);
            return false;
        }
        if (stagedTarget != null) target = stagedTarget.GroundWorldPosition;
        stageFacing = target.x == stagedAttacker.GroundWorldPosition.x
            ? (stagedAttacker.IsFacingRight ? 1f : -1f)
            : Mathf.Sign(target.x - stagedAttacker.GroundWorldPosition.x);
        bool melee = evt.AttackKind == AttackPresentationKind.MeleeWithSword ||
            evt.AttackKind == AttackPresentationKind.MeleeUnarmed || evt.AttackKind == AttackPresentationKind.EnemyMelee;
        float separation = melee ? meleeSeparation : rangedSeparation;
        // 수평 재배치 없이 현재 두 발 위치 사이의 선 위에서만 접근한다.
        Vector3 towardTarget = target - stagedAttacker.GroundWorldPosition;
        towardTarget.z = 0f;
        float approachDistance = Mathf.Max(0f, towardTarget.magnitude - separation);
        attackerDestinationOffset = moveAttackerForCombat ? towardTarget.normalized * approachDistance : Vector3.zero;
        source = stagedAttacker.TargetRenderer.bounds.center + attackerDestinationOffset;
        if (stagedTarget != null) target = stagedTarget.TargetRenderer.bounds.center;
        else target += Vector3.up * 0.7f;
        stagedAttacker.SetCombatStaged(true);
        stagedAttacker.TryPlayAnimationState("Idle", 0f);
        stagedAttacker.SetCombatFacing(stageFacing > 0f);
        stagedTarget?.SetCombatStaged(true);
        foreach (var visual in presentationRegistry.Visuals)
        {
            if (visual == null || visual == stagedAttacker || visual == stagedTarget) continue;
            backgroundActors.Add(visual);
        }
        foreach (var hud in hudCanvases)
        {
            if (hud == null) continue;
            savedHudStates[hud] = hud.enabled;
            hud.enabled = false;
        }
        EnsureCombatUI();
        combatCanvas.enabled = true;
        return true;
    }

    /// <summary>진입·복귀의 같은 보간값으로 표시 위치와 주변 흐림을 갱신한다.</summary>
    private void SetStageWeight(float value)
    {
        stageWeight = Mathf.Clamp01(value);
        stagedAttacker?.SetCombatWorldOffset(attackerDestinationOffset * stageWeight);
        foreach (var visual in backgroundActors)
            if (visual != null) visual.SetCombatBackground(backgroundBlurMaterial, stageWeight);
    }

    /// <summary>저장 상태 복원 뒤 각 Presenter의 자세와 검 후처리를 실행한다.</summary>
    private void ReleaseStage()
    {
        SetStageWeight(0f);
        stagedAttacker?.SetCombatStaged(false);
        stagedTarget?.SetCombatStaged(false);
        stagedAttacker?.EndCombatPresentation();
        stagedTarget?.EndCombatPresentation();
        backgroundActors.Clear();
        foreach (var entry in savedHudStates)
            if (entry.Key != null) entry.Key.enabled = entry.Value;
        savedHudStates.Clear();
        if (combatCanvas != null) combatCanvas.enabled = false;
        resultStartedAt = -100f;
        if (resultText != null) resultText.enabled = false;
        var callbacks = restoredCallbacks;
        restoredCallbacks = null;
        callbacks?.Invoke();
        if (stagedAttacker != null && !stagedAttacker.IsDeathPresentation)
            stagedAttacker.TryPlayAnimationState("Idle", 0f, false);
        stagedAttacker = stagedTarget = null;
        attackerDestinationOffset = Vector3.zero;
    }

    /// <summary>공격·회피·사망 자세가 바뀐 바로 그 프레임에 결과와 카메라 박자를 시작한다.</summary>
    public void PresentImpact(PresentationEvent evt, ActorVisualController target)
    {
        if (!hasSavedCameraState || target == null) return;
        string text = evt.AttackResult.IsHit ? evt.DamageResult.Damage.ToString() : "회피";
        ShowResult(text, target.HeadWorldPosition, evt.AttackResult.IsHit ? new Color(1f, 0.88f, 0.7f) : Color.white);
        BeginImpact();
    }

    /// <summary>해킹 문양의 마지막 섬광에 맞춰 완료 문구와 카메라 쏠림을 재생한다.</summary>
    public void PresentHackCompletion(PresentationEvent evt)
    {
        if (!hasSavedCameraState) return;
        Vector3 point = stagedTarget != null ? stagedTarget.HeadWorldPosition :
            gridManager.GridToWorld(evt.EventPosition) + Vector3.up * 1.4f;
        ShowResult("해킹 완료", point, new Color(0.65f, 1f, 0.91f));
        BeginImpact();
    }

    /// <summary>결과 표시 시작 시간을 저장하고 기존 Text를 재사용한다.</summary>
    private void ShowResult(string text, Vector3 position, Color color)
    {
        EnsureCombatUI();
        resultWorldPosition = position;
        resultStartedAt = Time.time;
        resultText.text = text;
        resultText.color = color;
        resultText.enabled = true;
    }

    /// <summary>띠를 순간 가속하고 대상 방향 카메라 쏠림을 한 번 시작한다.</summary>
    private void BeginImpact()
    {
        // 공격·피격 Sprite가 커진 경우에도 원래 구도 중심을 유지하며 잘림을 방지한다.
        targetCamera.orthographicSize = Mathf.Max(targetCamera.orthographicSize,
            CalculateFocusOrthographicSize(stageCameraPosition, stageCameraPosition));
        boostUntil = Time.time + 0.18f;
        if (impactCoroutine != null) StopCoroutine(impactCoroutine);
        impactCoroutine = StartCoroutine(ImpactRoutine());
    }

    /// <summary>카메라 위치와 Z각도를 짧게 기울이고 그 상태를 복귀까지 유지한다.</summary>
    private IEnumerator ImpactRoutine()
    {
        float elapsed = 0f;
        Quaternion toRotation = savedRotation * Quaternion.Euler(0f, 0f, -stageFacing * impactRoll);
        while (elapsed < impactDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / impactDuration));
            targetCamera.transform.position = stageCameraPosition + Vector3.right * (stageFacing * impactDistance * t);
            targetCamera.transform.rotation = Quaternion.Slerp(savedRotation, toRotation, t);
            yield return null;
        }
        impactCoroutine = null;
    }

    /// <summary>화면 전용 띠와 숫자를 한 번 만들고 이후 전투마다 재사용한다.</summary>
    private void EnsureCombatUI()
    {
        if (combatCanvas != null) return;
        var root = new GameObject("CombatPresentationOverlay", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
        root.transform.SetParent(transform, false);
        combatCanvas = root.GetComponent<Canvas>();
        combatCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        combatCanvas.sortingOrder = 300;
        combatCanvasRect = (RectTransform)root.transform;
        var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        var textObject = new GameObject("CombatResult", typeof(RectTransform), typeof(UnityEngine.UI.Text), typeof(UnityEngine.UI.Outline));
        textObject.transform.SetParent(root.transform, false);
        resultText = textObject.GetComponent<UnityEngine.UI.Text>();
        resultText.font = resultFont;
        resultText.fontSize = 64;
        resultText.fontStyle = FontStyle.Bold;
        resultText.alignment = TextAnchor.MiddleCenter;
        resultText.raycastTarget = false;
        resultText.rectTransform.sizeDelta = new Vector2(480, 120);
        var outline = textObject.GetComponent<UnityEngine.UI.Outline>();
        outline.effectColor = new Color(0.025f, 0.025f, 0.025f, 1f);
        outline.effectDistance = new Vector2(3, -3);
        resultText.enabled = false;
    }

    /// <summary>화면 크기에 필요한 만큼만 반복 조각을 늘리고 풀에서 재사용한다.</summary>
    private void EnsureBorderTiles(int count)
    {
        while (borderTiles.Count < count)
        {
            var item = new GameObject("FlowingBorder", typeof(RectTransform), typeof(UnityEngine.UI.RawImage));
            item.transform.SetParent(combatCanvas.transform, false);
            var image = item.GetComponent<UnityEngine.UI.RawImage>();
            image.texture = borderTexture;
            image.raycastTarget = false;
            image.rectTransform.anchorMin = image.rectTransform.anchorMax = Vector2.zero;
            image.rectTransform.pivot = Vector2.zero;
            borderTiles.Add(image);
        }
        for (int i = 0; i < borderTiles.Count; i++) borderTiles[i].enabled = i < count;
        resultText.transform.SetAsLastSibling();
    }

    /// <summary>띠는 화면에 고정하고 그림만 반대 방향으로 흘린다. 숫자는 현재 카메라로 투영한다.</summary>
    private void LateUpdate()
    {
        if (!hasSavedCameraState || combatCanvas == null || !combatCanvas.enabled) return;
        float width = combatCanvasRect.rect.width;
        float height = combatCanvasRect.rect.height;
        if (width <= 0f || height <= 0f) return;
        // 원화의 유효 검정 높이는 약 65%다. 투명 부분까지 포함해 화면 밖에서 진입시킨다.
        float tileHeight = height * borderHeight / 0.65f;
        float tileWidth = tileHeight * borderTexture.width / borderTexture.height;
        int rowCount = Mathf.CeilToInt(width / tileWidth) + 2;
        EnsureBorderTiles(rowCount * 2);
        borderPhase = Mathf.Repeat(borderPhase + Time.deltaTime * borderSpeed * width *
            (Time.time < boostUntil ? 3.5f : 1f), tileWidth * 2f);
        float slide = tileHeight * (1f - stageWeight);
        for (int row = 0; row < 2; row++)
        {
            float phase = row == 0 ? borderPhase : -borderPhase;
            int first = Mathf.FloorToInt(phase / tileWidth);
            for (int i = 0; i < rowCount; i++)
            {
                var tile = borderTiles[row * rowCount + i];
                bool mirror = ((first + i) & 1) != 0;
                tile.uvRect = new Rect(mirror ? 1f : 0f, row == 0 ? 0f : 1f, mirror ? -1f : 1f, row == 0 ? 1f : -1f);
                tile.rectTransform.anchoredPosition = new Vector2((first + i) * tileWidth - phase,
                    row == 0 ? height - tileHeight + slide : -slide);
                tile.rectTransform.sizeDelta = new Vector2(tileWidth + 0.5f, tileHeight);
            }
        }
        float age = Time.time - resultStartedAt;
        resultText.enabled = age >= 0f && age < resultDuration;
        if (!resultText.enabled) return;
        Vector3 screen = targetCamera.WorldToScreenPoint(resultWorldPosition);
        if (screen.z < 0f) { resultText.enabled = false; return; }
        RectTransformUtility.ScreenPointToLocalPointInRectangle(combatCanvasRect, screen, null, out Vector2 point);
        point.y += 35f + Mathf.Min(age, 0.4f) * 55f;
        point.x = Mathf.Clamp(point.x, -width * 0.5f + 160f, width * 0.5f - 160f);
        float safeY = height * (0.5f - borderHeight) - 60f;
        point.y = Mathf.Clamp(point.y, -safeY, safeY);
        resultText.rectTransform.anchoredPosition = point;
        resultText.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.45f, 1f, Mathf.Clamp01(age / 0.12f));
        Color color = resultText.color;
        color.a = 1f - Mathf.InverseLerp(resultDuration * 0.7f, resultDuration, age);
        resultText.color = color;
    }

    [Header("Reference")]
    // 위치와 Orthographic Size를 직접 제어할 전투 카메라다.
    [SerializeField] private Camera targetCamera;
    // 이벤트의 그리드 위치를 월드 위치로 변환할 씬 단위 그리드 관리자다.
    [SerializeField] private GridManager gridManager;

    [Header("Framing")]
    // 공격 시작점과 대상점 좌우에 확보할 월드 단위 여백이다.
    [SerializeField] private float horizontalPadding = 0.35f;
    // 공격 시작점과 대상점 상하에 확보할 월드 단위 여백이다.
    [SerializeField] private float verticalPadding = 0.3f;
    // 가까운 공격에서 카메라가 이 값보다 확대되지 않도록 제한하는 최소 Orthographic Size다.
    [SerializeField] private float minimumOrthographicSize = 3.5f;
    // 기존 접근 모드의 최대 Orthographic Size다. 위치 고정 모드는 두 배우 표시를 우선해 이 상한을 넘을 수 있다.
    [SerializeField] private float maximumOrthographicSize = 8.5f;

    [Header("Timing")]
    // 현재 전술 화면에서 공격 구도로 이동하고 줌을 맞출 시간이다.
    [SerializeField] private float focusDuration = 0.08f;
    // 공격 구도 도착 뒤 공격 연출 전에 잠시 멈출 시간이다.
    [SerializeField] private float settleDuration = 0.5f;
    // 공격 구도에서 원래 전술 화면으로 복귀할 시간이다.
    [SerializeField] private float restoreDuration = 0.08f;
    // 발각 카메라는 전투의 빠른 접근 시간과 독립적으로 기존 속도를 유지한다.
    [SerializeField] private float alertFocusDuration = 0.28f;

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
               presentationEvent.Type == PresentationEventType.CombatCameraRestore ||
               presentationEvent.Type == PresentationEventType.AlertCameraFocus;
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
        if (presentationEvent.Type == PresentationEventType.AlertCameraFocus)
        {
            var started = StartCoroutine(FocusAlertRoutine(presentationEvent, handle));
            cameraCoroutine = activeHandle == null ? null : started;
            return;
        }
        if (presentationEvent.Type == PresentationEventType.CombatCameraFocus)
        {
            var started = StartCoroutine(FocusRoutine(presentationEvent, handle));
            cameraCoroutine = activeHandle == null ? null : started;
            return;
        }

        var restoring = StartCoroutine(RestoreRoutine(handle));
        cameraCoroutine = activeHandle == null ? null : restoring;
    }

    /// <summary>발각한 적으로 위치만 이동한다. 이전 화면으로 돌아갈 상태는 저장하지 않는다.</summary>
    private IEnumerator FocusAlertRoutine(PresentationEvent presentationEvent, PresentationEventHandle handle)
    {
        Vector3 destination = gridManager.GridToWorld(presentationEvent.EventPosition);
        destination.z = targetCamera.transform.position.z;
        float size = targetCamera.orthographicSize;
        // 전투 포커스 도중 들어온 경우에도 이후 복귀 목적지는 새로 확인한 적 위치다.
        if (hasSavedCameraState) savedPosition = destination;
        yield return MoveCamera(targetCamera.transform.position, size, destination, size, alertFocusDuration);
        CompleteActiveEvent(handle);
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

        savedRotation = targetCamera.transform.rotation;
        savedPosition = targetCamera.transform.position;
        savedOrthographicSize = targetCamera.orthographicSize;
        hasSavedCameraState = true;

        if (!PrepareStage(presentationEvent, out Vector3 sourceWorldPosition, out Vector3 targetWorldPosition))
        {
            RestoreSavedCameraImmediately();
            CompleteActiveEvent(handle);
            yield break;
        }
        Vector3 focusPosition = CalculateFocusPosition(sourceWorldPosition, targetWorldPosition);
        float focusOrthographicSize = CalculateFocusOrthographicSize(sourceWorldPosition, targetWorldPosition);
        stageCameraPosition = focusPosition;

        if (logCameraFlow)
        {
            Debug.Log($"{nameof(CombatCameraPresenter)}: {presentationEvent.FromPosition}에서 {presentationEvent.ToPosition}까지 전투 카메라 포커스를 시작합니다. 줌: {savedOrthographicSize:0.##} -> {focusOrthographicSize:0.##}", this);
        }

        yield return MoveCamera(savedPosition, savedOrthographicSize, focusPosition, focusOrthographicSize, focusDuration, true);

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

        if (impactCoroutine != null) StopCoroutine(impactCoroutine);
        impactCoroutine = null;
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
            restoreDuration, false);

        hasSavedCameraState = false;
        ReleaseStage();
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
        Vector3 center = CalculateFocusPosition(sourceWorldPosition, targetWorldPosition);
        Bounds bounds = new Bounds(sourceWorldPosition, Vector3.zero);
        bounds.Encapsulate(targetWorldPosition);
        if (stagedAttacker != null)
        {
            Bounds actorBounds = stagedAttacker.TargetRenderer.bounds;
            actorBounds.center += attackerDestinationOffset - stagedAttacker.CombatWorldOffset;
            bounds.Encapsulate(actorBounds);
        }
        if (stagedTarget != null) bounds.Encapsulate(stagedTarget.TargetRenderer.bounds);
        // 원래 카메라 회전 기준으로 전체 Sprite 외곽을 투영한다.
        Quaternion inverse = Quaternion.Inverse(savedRotation);
        float halfWidth = 0f, halfHeight = 0f;
        for (int x = 0; x < 2; x++)
        for (int y = 0; y < 2; y++)
        {
            Vector3 corner = new Vector3(x == 0 ? bounds.min.x : bounds.max.x,
                y == 0 ? bounds.min.y : bounds.max.y, center.z);
            Vector3 local = inverse * (corner - center);
            halfWidth = Mathf.Max(halfWidth, Mathf.Abs(local.x));
            halfHeight = Mathf.Max(halfHeight, Mathf.Abs(local.y));
        }
        halfWidth += horizontalPadding + Mathf.Abs(impactDistance);
        halfHeight += verticalPadding + Mathf.Abs(impactDistance);
        // 기울어지는 도중의 모든 각도를 보수적으로 포함하고 띠 안쪽 영역에 배치한다.
        float sin = Mathf.Sin(Mathf.Min(90f, Mathf.Abs(impactRoll)) * Mathf.Deg2Rad);
        float rotatedWidth = halfWidth + halfHeight * sin;
        float rotatedHeight = halfHeight + halfWidth * sin;
        float requiredSize = Mathf.Max(rotatedWidth / targetCamera.aspect,
            rotatedHeight / (1f - borderHeight * 2f));
        return moveAttackerForCombat
            ? Mathf.Clamp(requiredSize, minimumOrthographicSize, maximumOrthographicSize)
            : Mathf.Max(requiredSize, minimumOrthographicSize);
    }

    /// <summary>
    /// 지정한 시작 상태에서 목표 상태까지 위치와 Orthographic Size를 부드럽게 보간한다.
    /// </summary>
    private IEnumerator MoveCamera(
        Vector3 fromPosition,
        float fromOrthographicSize,
        Vector3 toPosition,
        float toOrthographicSize,
        float duration, bool? entering = null)
    {
        Quaternion fromRotation = targetCamera.transform.rotation;
        if (duration <= 0f)
        {
            if (entering.HasValue) SetStageWeight(entering.Value ? 1f : 0f);
            if (entering == false) targetCamera.transform.rotation = savedRotation;
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
            if (entering.HasValue) SetStageWeight(entering.Value ? easedProgress : 1f - easedProgress);
            if (entering == false) targetCamera.transform.rotation = Quaternion.Slerp(fromRotation, savedRotation, easedProgress);
            targetCamera.transform.position = Vector3.LerpUnclamped(fromPosition, toPosition, easedProgress);
            targetCamera.orthographicSize = Mathf.LerpUnclamped(fromOrthographicSize, toOrthographicSize, easedProgress);
            yield return null;
        }

        // 큰 타격 기울기도 보간 오차 없이 저장했던 회전으로 확정 복원한다.
        if (entering == false) targetCamera.transform.rotation = savedRotation;
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

        if (impactCoroutine != null) StopCoroutine(impactCoroutine);
        impactCoroutine = null;
        targetCamera.transform.position = savedPosition;
        targetCamera.transform.rotation = savedRotation;
        targetCamera.orthographicSize = savedOrthographicSize;
        hasSavedCameraState = false;
        ReleaseStage();
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
        if (presentationRegistry == null || backgroundBlurMaterial == null || borderTexture == null || resultFont == null)
        {
            Debug.LogError("전투 집중 연출의 등록소·흐림 머티리얼·띠 원화·결과 글꼴 연결이 필요합니다.", this);
            return false;
        }
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
        if (meleeSeparation <= 0f || rangedSeparation <= 0f || impactDuration <= 0f ||
            borderHeight <= 0f || borderHeight >= 0.3f || borderSpeed < 0f || resultDuration <= 0f)
        {
            Debug.LogError("전투 무대 거리·띠 비율·타격 및 결과 시간이 유효하지 않습니다.", this);
            return false;
        }
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

        if (focusDuration < 0f || settleDuration < 0f || restoreDuration < 0f || alertFocusDuration < 0f)
        {
            Debug.LogError($"{nameof(CombatCameraPresenter)} on {name}의 카메라 연출 시간은 0 이상이어야 합니다.", this);
            return false;
        }

        return true;
    }
}
