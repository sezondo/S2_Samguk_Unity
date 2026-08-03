using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어 시야 스냅샷을 Fog 칸과 적 Visual에 적용하고 공격자의 임시 노출을 처리한다.
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
    // 탐색했지만 현재 시야 밖인 칸을 어둡게 표시하는 색이다.
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

    [Header("Log")]
    // true면 Fog와 적 표시 갱신 결과를 Unity 콘솔에 출력한다.
    [SerializeField] private bool logVisionPresentation;

    // 좌표별 런타임 Fog SpriteRenderer다.
    private readonly Dictionary<GridPosition, SpriteRenderer> fogRendererByPosition = new();
    // 공격 연출 때문에 현재 시야와 무관하게 잠시 보여 주는 Actor 집합이다.
    private readonly HashSet<GridActor> forcedVisibleActors = new();
    // 현재 화면 연출이 알고 있는 적 Actor별 표시 상태다. 적 이동이 끝날 때 스냅샷 이후 상태를 이어받는다.
    private readonly Dictionary<GridActor, bool> presentedEnemyVisibility = new();

    // 런타임 Fog 사각형에 사용하는 공용 Sprite와 Texture다.
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

    public static PlayerVisionPresenter Instance { get; private set; }
    public PlayerVisionSnapshot PresentedSnapshot => presentedSnapshot;

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
        BuildFogRenderers();
    }

    /// <summary>
    /// 시야 매니저, Actor 등록소와 연출 큐 구독을 시작한다.
    /// </summary>
    private void OnEnable()
    {
        TrySubscribeVisionManager();
        SubscribeActorRegistry();
        TryRegisterQueue(false);
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
        CompleteActivePresentation();
    }

    /// <summary>
    /// 런타임 Fog 리소스와 전역 인스턴스를 정리한다.
    /// </summary>
    private void OnDestroy()
    {
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
        return IsActorForcedVisible(actor) || IsPositionPresentedVisible(position);
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
    /// 현재 화면에 적용된 스냅샷 시점에 지정한 적 Actor가 보여야 하는지 확인한다.
    /// </summary>
    private bool ShouldShowEnemyActorInSnapshot(GridActor actor)
    {
        if (IsActorForcedVisible(actor))
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
            : StartCoroutine(PlayActorVisibilityOverride(presentationEvent.Actor, presentationEvent.ActorVisibility, handle));
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
    private IEnumerator PlayActorVisibilityOverride(GridActor actor, bool isVisible, PresentationEventHandle handle)
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
                presentedEnemyVisibility[actor] = IsPositionPresentedVisible(actor.GridPosition);
            }
        }

        yield return FadeActorToCurrentState(actor);
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
        foreach (KeyValuePair<GridPosition, SpriteRenderer> pair in fogRendererByPosition)
        {
            pair.Value.color = GetFogColor(snapshot.GetState(pair.Key));
        }

        ApplyAllEnemyVisibilityImmediately();
    }

    /// <summary>
    /// 모든 Fog 칸과 적 Visual을 현재 상태까지 동시에 페이드한다.
    /// </summary>
    private IEnumerator FadeToCurrentState()
    {
        Dictionary<GridPosition, Color> startFogColors = new(fogRendererByPosition.Count);
        foreach (KeyValuePair<GridPosition, SpriteRenderer> pair in fogRendererByPosition)
        {
            startFogColors[pair.Key] = pair.Value.color;
        }

        List<(ActorVisualController visual, float startAlpha, float targetAlpha)> enemyTransitions = BuildEnemyTransitions();
        float elapsed = 0f;
        float duration = Mathf.Max(0f, transitionDuration);
        while (elapsed < duration)
        {
            float t = duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);
            foreach (KeyValuePair<GridPosition, SpriteRenderer> pair in fogRendererByPosition)
            {
                Color targetColor = GetFogColor(presentedSnapshot.GetState(pair.Key));
                pair.Value.color = Color.Lerp(startFogColors[pair.Key], targetColor, t);
            }

            ApplyEnemyTransitionProgress(enemyTransitions, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        foreach (KeyValuePair<GridPosition, SpriteRenderer> pair in fogRendererByPosition)
        {
            pair.Value.color = GetFogColor(presentedSnapshot.GetState(pair.Key));
        }

        ApplyEnemyTransitionProgress(enemyTransitions, 1f);
    }

    /// <summary>
    /// 지정한 적 Actor 하나를 현재 시야와 강제 노출 상태에 맞는 알파로 페이드한다.
    /// </summary>
    private IEnumerator FadeActorToCurrentState(GridActor actor)
    {
        if (!context.ActorPresentationRegistry.TryGetVisual(actor, out ActorVisualController visual))
        {
            yield break;
        }

        float startAlpha = visual.VisionAlpha;
        float targetAlpha = ShouldShowEnemyActorAt(actor, actor.GridPosition) ? 1f : 0f;
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
        return state switch
        {
            GridVisibilityState.Visible => visibleColor,
            GridVisibilityState.Explored => exploredColor,
            _ => unexploredColor,
        };
    }

    /// <summary>
    /// 보드의 모든 칸을 덮는 런타임 사각형 Fog SpriteRenderer를 만든다.
    /// </summary>
    private void BuildFogRenderers()
    {
        fogTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            name = "Player Vision Fog Texture",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
        };
        fogTexture.SetPixel(0, 0, Color.white);
        fogTexture.Apply();
        fogSprite = Sprite.Create(fogTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        fogSprite.name = "Player Vision Fog Sprite";

        GridManager gridManager = context.GridManager;
        for (int x = 0; x < gridManager.Width; x++)
        {
            for (int y = 0; y < gridManager.Height; y++)
            {
                GridPosition position = new(x, y);
                GameObject fogCell = new($"Fog {x},{y}");
                fogCell.transform.SetParent(context.FogRoot, false);
                fogCell.transform.position = gridManager.GridToWorld(position);
                fogCell.transform.localScale = Vector3.one * gridManager.CellSize;

                SpriteRenderer renderer = fogCell.AddComponent<SpriteRenderer>();
                renderer.sprite = fogSprite;
                renderer.color = unexploredColor;
                renderer.sortingLayerName = fogSortingLayerName;
                renderer.sortingOrder = fogSortingOrder;
                fogRendererByPosition[position] = renderer;
            }
        }
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
    /// Fog 색과 전환 시간이 사용할 수 있는 값인지 확인한다.
    /// </summary>
    public bool HasValidData()
    {
        if (transitionDuration < 0f)
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
