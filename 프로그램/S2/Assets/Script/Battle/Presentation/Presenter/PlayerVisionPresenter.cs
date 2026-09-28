using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어 시야 스냅샷을 연속 안개와 적 Visual에 적용하고 공격자의 임시 노출을 처리한다.
/// 시야 판정이나 대상 선택 규칙은 바꾸지 않는다.
/// </summary>
public class PlayerVisionPresenter : MonoBehaviour, IPresentationEventHandler
{
    [Header("Reference")]
    // Fog와 적 표시 연출에 필요한 씬 참조 주머니다.
    [SerializeField] private PlayerVisionContext context;

    [Header("Fog Colors")]
    // 아직 한 번도 탐색하지 않은 칸을 가리는 색이다.
    [SerializeField] private Color unexploredColor = new(0f, 0f, 0f, 1f);
    // 탐색했지만 현재 시야 밖인 영역의 불투명도다. 기존 직렬화 호환을 위해 Color의 알파를 사용한다.
    [SerializeField] private Color exploredColor = new(0f, 0f, 0f, 0.65f);
    // 현재 시야 안의 Fog 색이다. 기본값은 완전 투명이다.
    [SerializeField] private Color visibleColor = new(0f, 0f, 0f, 0f);

    [Header("Fog Render")]
    // Fog SpriteRenderer가 사용할 Sorting Layer 이름이다.
    [SerializeField] private string fogSortingLayerName = "Default";
    // Fog SpriteRenderer가 월드 Visual보다 위에 그려질 정렬 순서다.
    [SerializeField] private int fogSortingOrder = 1000;
    // 한 칸 시야 변화와 적 표시 전환에 사용할 페이드 시간이다.
    [SerializeField] private float transitionDuration = 0.12f;

    [Header("Debug")]
    // true면 실제 시야 판정은 유지하면서 Fog와 적 숨김만 해제한다.
    [SerializeField] private bool revealAllForDebug;

    [Header("Log")]
    // true면 Fog와 적 표시 갱신 결과를 Unity 콘솔에 출력한다.
    [SerializeField] private bool logVisionPresentation;

    [Header("Soft Fog")]
    // 한 칸을 나누어 시선을 계산하는 표본 수다.
    [SerializeField, Range(4, 24)] private int fogSamplesPerCell = 12;
    // 원과 벽 그림자 가장자리를 흐리는 월드 거리다.
    [SerializeField, Min(0.05f)] private float fogEdgeSoftness = 0.35f;
    // 탐색 영역의 회색빛이다. 불투명도는 기존 exploredColor 값을 사용한다.
    [SerializeField] private Color exploredGrayTint = new(0.16f, 0.17f, 0.18f, 1f);
    // 연속 시야와 누적 탐색을 계산하는 마스크다.
    private PlayerVisionFogMask fogMask;
    // 현재 표시, 전환 시작 및 목표 픽셀 버퍼다.
    private Color32[] fogPixels, startFogPixels, targetFogPixels;
    // 이 컴포넌트가 소유하는 런타임 안개 루트다.
    private GameObject runtimeFogRoot;
    // 외곽 가림용 공용 리소스다.
    private Sprite outsideFogSprite;
    private Texture2D outsideFogTexture;
    // 디버그 공개를 적용할 외곽 렌더러 목록이다.
    private readonly List<SpriteRenderer> outsideFogRenderers = new();
    // 공격 연출 때문에 현재 시야와 무관하게 잠시 보여 주는 Actor 집합이다.
    private readonly HashSet<GridActor> forcedVisibleActors = new();
    // 현재 화면 연출이 알고 있는 적 Actor별 표시 상태다. 적 이동이 끝날 때 스냅샷 이후 상태를 이어받는다.
    private readonly Dictionary<GridActor, bool> presentedEnemyVisibility = new();

    // 연속 안개 마스크를 표시하는 Sprite와 Texture다.
    private Sprite fogSprite;
    private Texture2D fogTexture;
    // 현재 화면에 적용된 플레이어 시야 스냅샷이다.
    private PlayerVisionSnapshot presentedSnapshot;
    // 현재 처리 중인 시야 연출 코루틴이다.
    private Coroutine presentationCoroutine;
    // 현재 처리 중인 큐 완료 핸들이다.
    private PresentationEventHandle activeHandle;
    // 이벤트 구독 중인 시야 매니저다.
    private PlayerVisionManager subscribedVisionManager;
    // 런타임에 마지막으로 화면에 적용한 테스트용 전체 공개 설정값이다.
    private bool appliedRevealAllForDebug;

    public static PlayerVisionPresenter Instance { get; private set; }
    public PlayerVisionSnapshot PresentedSnapshot => presentedSnapshot;
    // 안개를 공개하지 않고 목표 표식만 위에 그리기 위한 정렬 기준이다.
    public string FogSortingLayerName => fogSortingLayerName;
    public int FogSortingOrder => fogSortingOrder;

    /// <summary>
    /// 씬의 단일 시야 Presenter를 등록하고 필수 참조와 표시 데이터를 검사한다.
    /// </summary>
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"{nameof(PlayerVisionPresenter)}: 이미 인스턴스가 있습니다. 중복 오브젝트 {name}의 컴포넌트를 비활성화합니다.", this);
            enabled = false;
            return;
        }

        if (!HasValidReference() || !HasValidData())
        {
            enabled = false;
            return;
        }

        Instance = this;
        appliedRevealAllForDebug = revealAllForDebug;
        BuildFogRenderers();
    }

    /// <summary>
    /// 시야 매니저, Actor 등록소와 연출 큐 구독을 시작하고 재활성화 시 현재 스냅샷을 복원한다.
    /// </summary>
    private void OnEnable()
    {
        if (runtimeFogRoot != null) runtimeFogRoot.SetActive(true);
        TrySubscribeVisionManager();
        SubscribeActorRegistry();
        TryRegisterQueue(false);
        if (fogMask != null && context.PlayerVisionManager.CurrentSnapshot != null)
            ApplySnapshotImmediately(context.PlayerVisionManager.CurrentSnapshot);
    }

    /// <summary>
    /// 초기화 순서가 끝난 뒤 구독을 보정하고 준비된 최초 시야를 즉시 적용한다.
    /// </summary>
    private void Start()
    {
        TrySubscribeVisionManager();
        SubscribeActorRegistry();
        TryRegisterQueue(true);

        if (context.PlayerVisionManager.CurrentSnapshot != null)
        {
            ApplySnapshotImmediately(context.PlayerVisionManager.CurrentSnapshot);
        }
    }

    /// <summary>
    /// 플레이 중 인스펙터의 전체 공개 설정이 바뀌면 현재 Fog와 적 표시를 즉시 다시 적용한다.
    /// </summary>
    private void Update()
    {
        if (appliedRevealAllForDebug == revealAllForDebug || presentationCoroutine != null)
        {
            return;
        }

        appliedRevealAllForDebug = revealAllForDebug;
        ApplyCurrentPresentationImmediately();
    }

    /// <summary>
    /// 시야 매니저, Actor 등록소와 연출 큐 구독을 해제한다.
    /// </summary>
    private void OnDisable()
    {
        UnsubscribeVisionManager();
        UnsubscribeActorRegistry();
        if (context != null && context.PresentationQueue != null)
        {
            context.PresentationQueue.Unregister(this);
        }

        ClearForcedActorOverrides();
        if (presentationCoroutine != null) StopCoroutine(presentationCoroutine);
        CompleteActivePresentation();
        if (runtimeFogRoot != null) runtimeFogRoot.SetActive(false);
    }

    /// <summary>
    /// 런타임 Fog 리소스와 전역 인스턴스를 정리한다.
    /// </summary>
    private void OnDestroy()
    {
        if (runtimeFogRoot != null) Destroy(runtimeFogRoot);
        if (outsideFogSprite != null) Destroy(outsideFogSprite);
        if (outsideFogTexture != null) Destroy(outsideFogTexture);
        if (fogSprite != null)
        {
            Destroy(fogSprite);
        }

        if (fogTexture != null)
        {
            Destroy(fogTexture);
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// 지정한 칸이 현재 화면에 적용된 플레이어 시야 안인지 확인한다.
    /// </summary>
    public bool IsPositionPresentedVisible(GridPosition position)
    {
        return presentedSnapshot != null && presentedSnapshot.IsVisible(position);
    }

    /// <summary>
    /// 지정한 Actor가 적이며 현재 강제 노출 중인지 확인한다.
    /// </summary>
    public bool IsActorForcedVisible(GridActor actor)
    {
        return actor != null && forcedVisibleActors.Contains(actor);
    }

    /// <summary>
    /// 지정한 Actor가 플레이어 시야로 숨김 처리할 적 Actor인지 확인한다.
    /// </summary>
    public bool IsEnemyActor(GridActor actor)
    {
        if (actor == null || context == null || context.EnemyRegistry == null)
        {
            return false;
        }

        IReadOnlyList<EnemyContext> enemies = context.EnemyRegistry.Enemies;
        for (int i = 0; i < enemies.Count; i++)
        {
            if (enemies[i] != null && enemies[i].GridActor == actor)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 플레이어 시야 또는 공격 임시 노출 기준으로 지정한 적 Actor가 보여야 하는지 확인한다.
    /// </summary>
    public bool ShouldShowEnemyActorAt(GridActor actor, GridPosition position)
    {
        return revealAllForDebug || IsActorForcedVisible(actor) || IsPositionPresentedVisible(position);
    }

    /// <summary>
    /// 적 한 칸 이동 연출이 끝난 뒤 화면상 최종 표시 상태를 기록한다.
    /// </summary>
    public void SetMovedEnemyVisibility(GridActor actor, bool isVisible)
    {
        if (actor != null && IsEnemyActor(actor))
        {
            presentedEnemyVisibility[actor] = isVisible;
        }
    }

    /// <summary>
    /// 현재 화면의 스냅샷·이동 완료·임시 노출 상태를 기준으로 적과 HUD의 표시 여부를 함께 판단한다.
    /// </summary>
    public bool ShouldShowEnemyActorInSnapshot(GridActor actor)
    {
        if (revealAllForDebug || IsActorForcedVisible(actor))
        {
            return true;
        }

        return presentedEnemyVisibility.TryGetValue(actor, out bool isVisible)
            ? isVisible
            : presentedSnapshot != null && presentedSnapshot.IsEnemyActorVisible(actor);
    }

    /// <summary>
    /// 시야 스냅샷 변경과 Actor 임시 노출 이벤트를 처리할 수 있는지 확인한다.
    /// </summary>
    public bool CanHandle(PresentationEvent presentationEvent)
    {
        return presentationEvent.Type == PresentationEventType.PlayerVisionChanged ||
            presentationEvent.Type == PresentationEventType.ActorVisibilityOverride;
    }

    /// <summary>
    /// Fog·적 표시 갱신 또는 공격자 임시 노출 연출을 시작한다.
    /// </summary>
    public void Handle(PresentationEvent presentationEvent, PresentationEventHandle handle)
    {
        if (presentationCoroutine != null)
        {
            Debug.LogError($"{nameof(PlayerVisionPresenter)} on {name}은 이미 시야 연출을 처리 중입니다. 새 이벤트를 자동 완료합니다. 이벤트: {presentationEvent}", this);
            handle.Complete();
            return;
        }

        if (!HasValidReference() || !HasValidData())
        {
            handle.Complete();
            return;
        }

        activeHandle = handle;
        presentationCoroutine = presentationEvent.Type == PresentationEventType.PlayerVisionChanged
            ? StartCoroutine(PlaySnapshotTransition(presentationEvent.VisionSnapshot, handle))
            : StartCoroutine(PlayActorVisibilityOverride(presentationEvent.Actor, presentationEvent.ActorVisibility, presentationEvent.EventPosition, handle));
    }

    /// <summary>
    /// 새 시야 스냅샷의 Fog 색과 적 표시 알파로 부드럽게 전환한다.
    /// </summary>
    private IEnumerator PlaySnapshotTransition(PlayerVisionSnapshot snapshot, PresentationEventHandle handle)
    {
        if (snapshot == null)
        {
            Debug.LogError($"{nameof(PlayerVisionPresenter)}: 적용할 플레이어 시야 스냅샷이 없습니다.", this);
            CompleteActivePresentation(handle);
            yield break;
        }

        presentedSnapshot = snapshot;
        SynchronizePresentedEnemyVisibility(snapshot);
        yield return FadeToCurrentState();

        if (logVisionPresentation)
        {
            Debug.Log($"{nameof(PlayerVisionPresenter)}: 플레이어 시야와 누적 탐색 Fog를 갱신했습니다.", this);
        }

        CompleteActivePresentation(handle);
    }

    /// <summary>
    /// 공격자 Actor의 강제 노출 상태를 바꾸고 현재 시야 기준 표시 알파로 전환한다.
    /// </summary>
    private IEnumerator PlayActorVisibilityOverride(GridActor actor, bool isVisible, GridPosition eventPosition, PresentationEventHandle handle)
    {
        if (actor == null)
        {
            Debug.LogError($"{nameof(PlayerVisionPresenter)}: 임시 표시를 바꿀 Actor가 없습니다.", this);
            CompleteActivePresentation(handle);
            yield break;
        }

        if (!context.ActorPresentationRegistry.TryGetVisual(actor, out ActorVisualController visual))
        {
            Debug.LogError($"{nameof(PlayerVisionPresenter)}: 임시 표시할 {actor.name} Actor의 Visual을 찾지 못했습니다.", actor);
            CompleteActivePresentation(handle);
            yield break;
        }

        if (isVisible)
        {
            forcedVisibleActors.Add(actor);
            // 불투명 Fog는 그대로 유지하고 공격자 Sprite만 Fog 위로 올려 위치 주변 지형 정보 노출을 막는다.
            visual.BeginVisionSortingOverride(fogSortingLayerName, fogSortingOrder + 1);
        }
        else
        {
            forcedVisibleActors.Remove(actor);
            if (IsEnemyActor(actor))
            {
                presentedEnemyVisibility[actor] = IsPositionPresentedVisible(eventPosition);
            }
        }

        yield return FadeActorToCurrentState(actor, eventPosition);
        if (!isVisible)
        {
            visual.EndVisionSortingOverride();
        }

        CompleteActivePresentation(handle);
    }

    /// <summary>
    /// 현재 시야 스냅샷을 큐 대기 없이 Fog와 적 Visual에 즉시 적용한다.
    /// </summary>
    private void ApplySnapshotImmediately(PlayerVisionSnapshot snapshot)
    {
        if (snapshot == null)
        {
            return;
        }

        presentedSnapshot = snapshot;
        SynchronizePresentedEnemyVisibility(snapshot);
        PrepareFogTarget(snapshot);
        ApplyFogProgress(1f);

        ApplyAllEnemyVisibilityImmediately();
    }

    /// <summary>
    /// 현재까지 화면에 적용된 시야를 기준으로 테스트용 전체 공개 설정을 즉시 반영한다.
    /// </summary>
    private void ApplyCurrentPresentationImmediately()
    {
        PlayerVisionSnapshot snapshot = presentedSnapshot;
        if (snapshot == null && context != null && context.PlayerVisionManager != null)
        {
            snapshot = context.PlayerVisionManager.CurrentSnapshot;
        }

        if (snapshot != null)
        {
            ApplySnapshotImmediately(snapshot);
            return;
        }

        System.Array.Fill(fogPixels, (Color32)GetFogColor(GridVisibilityState.Unexplored));
        UploadFogPixels();
        UpdateOutsideFog();

        ApplyAllEnemyVisibilityImmediately();
    }

    /// <summary>
    /// 연속 안개 마스크와 적 Visual을 현재 상태까지 동시에 페이드한다.
    /// </summary>
    private IEnumerator FadeToCurrentState()
    {
        PrepareFogTarget(presentedSnapshot);
        System.Array.Copy(fogPixels, startFogPixels, fogPixels.Length);

        List<(ActorVisualController visual, float startAlpha, float targetAlpha)> enemyTransitions = BuildEnemyTransitions();
        float elapsed = 0f;
        float duration = Mathf.Max(0f, transitionDuration);
        while (elapsed < duration)
        {
            float t = duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);
            ApplyFogProgress(t);

            ApplyEnemyTransitionProgress(enemyTransitions, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        ApplyFogProgress(1f);

        ApplyEnemyTransitionProgress(enemyTransitions, 1f);
    }

    /// <summary>
    /// 지정한 적 Actor 하나를 현재 시야와 강제 노출 상태에 맞는 알파로 페이드한다.
    /// </summary>
    private IEnumerator FadeActorToCurrentState(GridActor actor, GridPosition eventPosition)
    {
        if (!context.ActorPresentationRegistry.TryGetVisual(actor, out ActorVisualController visual))
        {
            yield break;
        }

        float startAlpha = visual.VisionAlpha;
        float targetAlpha = ShouldShowEnemyActorAt(actor, eventPosition) ? 1f : 0f;
        float elapsed = 0f;
        float duration = Mathf.Max(0f, transitionDuration);
        while (elapsed < duration)
        {
            float t = duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);
            visual.SetVisionAlpha(Mathf.Lerp(startAlpha, targetAlpha, t));
            elapsed += Time.deltaTime;
            yield return null;
        }

        visual.SetVisionAlpha(targetAlpha);
    }

    /// <summary>
    /// 현재 적 목록의 시작 알파와 목표 알파 전환 정보를 만든다.
    /// </summary>
    private List<(ActorVisualController visual, float startAlpha, float targetAlpha)> BuildEnemyTransitions()
    {
        List<(ActorVisualController visual, float startAlpha, float targetAlpha)> transitions = new();
        IReadOnlyList<EnemyContext> enemies = context.EnemyRegistry.Enemies;
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyContext enemy = enemies[i];
            if (enemy == null || enemy.GridActor == null ||
                !context.ActorPresentationRegistry.TryGetVisual(enemy.GridActor, out ActorVisualController visual))
            {
                continue;
            }

            float targetAlpha = ShouldShowEnemyActorInSnapshot(enemy.GridActor) ? 1f : 0f;
            transitions.Add((visual, visual.VisionAlpha, targetAlpha));
        }

        return transitions;
    }

    /// <summary>
    /// 적 Visual 전환 목록에 지정한 보간 진행률을 적용한다.
    /// </summary>
    private static void ApplyEnemyTransitionProgress(
        List<(ActorVisualController visual, float startAlpha, float targetAlpha)> transitions,
        float progress)
    {
        for (int i = 0; i < transitions.Count; i++)
        {
            (ActorVisualController visual, float startAlpha, float targetAlpha) = transitions[i];
            if (visual != null)
            {
                visual.SetVisionAlpha(Mathf.Lerp(startAlpha, targetAlpha, progress));
            }
        }
    }

    /// <summary>
    /// 현재 등록된 모든 적 Visual에 현재 시야 기준 알파를 즉시 적용한다.
    /// </summary>
    private void ApplyAllEnemyVisibilityImmediately()
    {
        IReadOnlyList<EnemyContext> enemies = context.EnemyRegistry.Enemies;
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyContext enemy = enemies[i];
            if (enemy == null || enemy.GridActor == null ||
                !context.ActorPresentationRegistry.TryGetVisual(enemy.GridActor, out ActorVisualController visual))
            {
                continue;
            }

            visual.SetVisionAlpha(ShouldShowEnemyActorInSnapshot(enemy.GridActor) ? 1f : 0f);
        }
    }

    /// <summary>
    /// 새 시야 스냅샷에 기록된 적 표시 상태로 화면상 상태 테이블을 동기화한다.
    /// </summary>
    private void SynchronizePresentedEnemyVisibility(PlayerVisionSnapshot snapshot)
    {
        presentedEnemyVisibility.Clear();
        IReadOnlyList<EnemyContext> enemies = context.EnemyRegistry.Enemies;
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyContext enemy = enemies[i];
            if (enemy != null && enemy.GridActor != null)
            {
                presentedEnemyVisibility[enemy.GridActor] = snapshot.IsEnemyActorVisible(enemy.GridActor);
            }
        }
    }

    /// <summary>
    /// 새로 등록된 적 Visual에 현재 플레이어 시야 상태를 즉시 적용한다.
    /// </summary>
    private void HandleActorRegistered(GridActor actor, ActorVisualController visual)
    {
        if (actor == null || visual == null || !IsEnemyActor(actor))
        {
            return;
        }

        visual.SetVisionAlpha(ShouldShowEnemyActorInSnapshot(actor) ? 1f : 0f);
    }

    /// <summary>
    /// 플레이어 시야 상태에 대응하는 Fog 색을 반환한다.
    /// </summary>
    private Color GetFogColor(GridVisibilityState state)
    {
        if (revealAllForDebug)
        {
            return Color.clear;
        }

        return state switch
        {
            GridVisibilityState.Visible => visibleColor,
            GridVisibilityState.Explored => new Color(exploredGrayTint.r, exploredGrayTint.g, exploredGrayTint.b, exploredColor.a),
            _ => unexploredColor,
        };
    }

    /// <summary>기존 FogRoot 아래에 연속 텍스처와 보드 밖 미탐색 가림을 런타임에 구성한다.</summary>
    private void BuildFogRenderers()
    {
        GridManager grid = context.GridManager;
        fogMask = new PlayerVisionFogMask(grid.Width, grid.Height, fogSamplesPerCell, grid.CellSize, fogEdgeSoftness);
        fogPixels = new Color32[fogMask.Width * fogMask.Height];
        startFogPixels = new Color32[fogPixels.Length];
        targetFogPixels = new Color32[fogPixels.Length];
        System.Array.Fill(fogPixels, (Color32)GetFogColor(GridVisibilityState.Unexplored));
        fogTexture = new Texture2D(fogMask.Width, fogMask.Height, TextureFormat.RGBA32, false)
        {
            name = "Player Vision Soft Fog", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp,
        };
        UploadFogPixels();
        fogSprite = Sprite.Create(fogTexture, new Rect(0, 0, fogMask.Width, fogMask.Height),
            new Vector2(0.5f, 0.5f), fogSamplesPerCell / grid.CellSize, 0, SpriteMeshType.FullRect);
        runtimeFogRoot = new GameObject("Player Vision Runtime Fog");
        runtimeFogRoot.transform.SetParent(context.FogRoot, false);
        Vector3 center = grid.GridToWorld(new GridPosition(0, 0)) +
            new Vector3((grid.Width - 1) * grid.CellSize * 0.5f, (grid.Height - 1) * grid.CellSize * 0.5f, 0f);
        CreateFogRenderer("Soft Fog", fogSprite, center, Vector3.one, Color.white);

        outsideFogTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        outsideFogTexture.SetPixel(0, 0, Color.white);
        outsideFogTexture.Apply();
        outsideFogSprite = Sprite.Create(outsideFogTexture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f, 1f);
        float halfWidth = (grid.Width + PlayerVisionFogMask.Padding * 2) * grid.CellSize * 0.5f;
        float halfHeight = (grid.Height + PlayerVisionFogMask.Padding * 2) * grid.CellSize * 0.5f;
        // 카메라가 보드 밖으로 이동해도 지형이 노출되지 않도록 충분히 넓은 네 장으로 외곽을 덮는다.
        const float extent = 10000f;
        AddOutsideFog(center + Vector3.left * (halfWidth + extent * 0.5f), new Vector3(extent, extent * 2f, 1f));
        AddOutsideFog(center + Vector3.right * (halfWidth + extent * 0.5f), new Vector3(extent, extent * 2f, 1f));
        AddOutsideFog(center + Vector3.up * (halfHeight + extent * 0.5f), new Vector3(halfWidth * 2f, extent, 1f));
        AddOutsideFog(center + Vector3.down * (halfHeight + extent * 0.5f), new Vector3(halfWidth * 2f, extent, 1f));
    }

    /// <summary>소유 루트 아래에 안개 렌더러를 만든다. 저장된 씬이나 Inspector 연결은 변경하지 않는다.</summary>
    private SpriteRenderer CreateFogRenderer(string objectName, Sprite sprite, Vector3 position, Vector3 scale, Color color)
    {
        GameObject fog = new(objectName);
        fog.transform.SetParent(runtimeFogRoot.transform, false);
        fog.transform.position = position;
        fog.transform.localScale = scale;
        SpriteRenderer renderer = fog.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingLayerName = fogSortingLayerName;
        renderer.sortingOrder = fogSortingOrder;
        return renderer;
    }

    /// <summary>미탐색 바깥 영역 한 장을 만들고 디버그 공개 전환 목록에 등록한다.</summary>
    private void AddOutsideFog(Vector3 position, Vector3 scale)
    {
        outsideFogRenderers.Add(CreateFogRenderer("Outside Fog", outsideFogSprite, position, scale,
            GetFogColor(GridVisibilityState.Unexplored)));
    }

    /// <summary>스냅샷 원점과 벽만 사용해 목표 픽셀을 만든다. 디버그 공개는 탐색 이력을 늘리지 않는다.</summary>
    private void PrepareFogTarget(PlayerVisionSnapshot snapshot)
    {
        Color remembered = new(exploredGrayTint.r, exploredGrayTint.g, exploredGrayTint.b, exploredColor.a);
        fogMask.BuildColors(snapshot, unexploredColor, remembered, visibleColor, targetFogPixels);
        if (revealAllForDebug) System.Array.Fill(targetFogPixels, (Color32)Color.clear);
        UpdateOutsideFog();
    }

    /// <summary>현재 표시 상태에서 새 안개 상태까지 픽셀을 보간한다.</summary>
    private void ApplyFogProgress(float progress)
    {
        for (int i = 0; i < fogPixels.Length; i++)
            fogPixels[i] = Color32.Lerp(startFogPixels[i], targetFogPixels[i], progress);
        UploadFogPixels();
    }

    /// <summary>재사용 픽셀 버퍼를 GPU 텍스처로 전달한다.</summary>
    private void UploadFogPixels()
    {
        fogTexture.SetPixels32(fogPixels);
        fogTexture.Apply(false, false);
    }

    /// <summary>보드 바깥 안개에도 현재 디버그 공개 설정을 반영한다.</summary>
    private void UpdateOutsideFog()
    {
        foreach (SpriteRenderer renderer in outsideFogRenderers)
            renderer.color = GetFogColor(GridVisibilityState.Unexplored);
    }

    /// <summary>
    /// 시야 매니저의 즉시 갱신 이벤트를 구독한다.
    /// </summary>
    private void TrySubscribeVisionManager()
    {
        PlayerVisionManager manager = context != null ? context.PlayerVisionManager : null;
        if (manager == null || subscribedVisionManager == manager)
        {
            return;
        }

        UnsubscribeVisionManager();
        manager.VisionChangedImmediately += ApplySnapshotImmediately;
        subscribedVisionManager = manager;
    }

    /// <summary>
    /// 현재 시야 매니저의 즉시 갱신 이벤트 구독을 해제한다.
    /// </summary>
    private void UnsubscribeVisionManager()
    {
        if (subscribedVisionManager != null)
        {
            subscribedVisionManager.VisionChangedImmediately -= ApplySnapshotImmediately;
        }

        subscribedVisionManager = null;
    }

    /// <summary>
    /// Actor Visual 등록 이벤트를 구독한다.
    /// </summary>
    private void SubscribeActorRegistry()
    {
        if (context == null || context.ActorPresentationRegistry == null)
        {
            return;
        }

        context.ActorPresentationRegistry.ActorRegistered -= HandleActorRegistered;
        context.ActorPresentationRegistry.ActorRegistered += HandleActorRegistered;
    }

    /// <summary>
    /// Actor Visual 등록 이벤트 구독을 해제한다.
    /// </summary>
    private void UnsubscribeActorRegistry()
    {
        if (context != null && context.ActorPresentationRegistry != null)
        {
            context.ActorPresentationRegistry.ActorRegistered -= HandleActorRegistered;
        }
    }

    /// <summary>
    /// Presenter가 비활성화될 때 남아 있는 공격자 임시 정렬 덮어쓰기를 모두 복원한다.
    /// </summary>
    private void ClearForcedActorOverrides()
    {
        if (context == null || context.ActorPresentationRegistry == null)
        {
            forcedVisibleActors.Clear();
            return;
        }

        foreach (GridActor actor in forcedVisibleActors)
        {
            if (actor != null &&
                context.ActorPresentationRegistry.TryGetVisual(actor, out ActorVisualController visual))
            {
                visual.EndVisionSortingOverride();
            }
        }

        forcedVisibleActors.Clear();
    }

    /// <summary>
    /// 현재 씬의 연출 큐에 시야 Presenter 등록을 시도한다.
    /// </summary>
    private void TryRegisterQueue(bool logMissingQueue)
    {
        if (context == null || context.PresentationQueue == null)
        {
            if (logMissingQueue)
            {
                Debug.LogError($"{nameof(PlayerVisionPresenter)} on {name}에는 시야 연출을 실행할 {nameof(ActionPresentationQueue)} 참조가 필요합니다.", this);
                enabled = false;
            }

            return;
        }

        context.PresentationQueue.Register(this);
    }

    /// <summary>
    /// 실행 중인 시야 연출을 정리하고 큐 완료 신호를 보낸다.
    /// </summary>
    private void CompleteActivePresentation(PresentationEventHandle handle = null)
    {
        presentationCoroutine = null;
        PresentationEventHandle handleToComplete = handle ?? activeHandle;
        activeHandle = null;
        if (handleToComplete != null && !handleToComplete.IsCompleted)
        {
            handleToComplete.Complete();
        }
    }

    /// <summary>
    /// Fog와 적 표시 연출에 필요한 Context 참조가 연결되어 있는지 확인한다.
    /// </summary>
    public bool HasValidReference()
    {
        if (context == null)
        {
            Debug.LogError($"{nameof(PlayerVisionPresenter)} on {name}에는 {nameof(PlayerVisionContext)} 참조가 필요합니다.", this);
            return false;
        }

        return context.HasValidReference();
    }

    /// <summary>
    /// 안개 해상도, 월드 경계 폭, 색과 전환 시간이 사용할 수 있는 값인지 확인한다.
    /// </summary>
    public bool HasValidData()
    {
        GridManager grid = context != null ? context.GridManager : null;
        if (fogSamplesPerCell < 4 || fogSamplesPerCell > 24 ||
            float.IsNaN(fogEdgeSoftness) || float.IsInfinity(fogEdgeSoftness) || fogEdgeSoftness <= 0f ||
            grid == null || grid.Width <= 0 || grid.Height <= 0 || grid.CellSize <= 0f ||
            float.IsNaN(grid.CellSize) || float.IsInfinity(grid.CellSize) || fogEdgeSoftness > grid.CellSize * 0.5f ||
            (long)(grid.Width + 2) * fogSamplesPerCell > SystemInfo.maxTextureSize ||
            (long)(grid.Height + 2) * fogSamplesPerCell > SystemInfo.maxTextureSize ||
            (long)(grid.Width + 2) * (grid.Height + 2) * fogSamplesPerCell * fogSamplesPerCell > 4194304)
        {
            Debug.LogError($"{nameof(PlayerVisionPresenter)}: 안개 해상도는 칸당 4~24, 경계 폭은 0 초과~셀 크기의 절반이어야 합니다. 유효한 보드와 장치 제한 및 419만 픽셀 이하의 텍스처가 필요합니다.", this);
            return false;
        }

        if (float.IsNaN(transitionDuration) || float.IsInfinity(transitionDuration) || transitionDuration < 0f)
        {
            Debug.LogError($"{nameof(PlayerVisionPresenter)} on {name}의 시야 전환 시간은 0 이상이어야 합니다.", this);
            return false;
        }

        if (string.IsNullOrWhiteSpace(fogSortingLayerName))
        {
            Debug.LogError($"{nameof(PlayerVisionPresenter)} on {name}의 Fog Sorting Layer 이름이 비어 있습니다.", this);
            return false;
        }

        return true;
    }
}
