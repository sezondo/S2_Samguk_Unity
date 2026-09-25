using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 논리 GridActor와 화면 표시용 ActorVisualController의 연결을 관리하는 씬 단위 등록소다.
/// </summary>
public class ActorPresentationRegistry : MonoBehaviour
{
    public static ActorPresentationRegistry Instance { get; private set; }
    // 전투 집중 연출이 현재 화면 배우들을 순회할 때 사용하는 읽기 전용 등록 목록이다.
    public IEnumerable<ActorVisualController> Visuals => visualByActor.Values;

    // 논리 Actor를 기준으로 등록된 시각 제어 컴포넌트를 찾는 맵이다.
    private readonly Dictionary<GridActor, ActorVisualController> visualByActor = new();
    // 논리 Actor를 기준으로 등록된 적 시야 방향 표시 Presenter를 찾는 맵이다.
    private readonly Dictionary<GridActor, EnemyFacingIndicatorPresenter> facingIndicatorByActor = new();

    // 최초 피해 직전 값을 보존하고 피격 연출 시작 때만 갱신하는 화면 HP다.
    private readonly Dictionary<GridActor, int> presentedHealth = new();
    // 적 머리 위 아이콘도 같은 연출 시점의 인식 상태와 조사 단계를 사용한다.
    private readonly Dictionary<GridActor, (EnemyAwarenessState awareness, SuspiciousBehaviorPhase phase)> presentedEnemyStates = new();

    // 수색·발각 진입 연출에서 눈이 켜진 구간의 적을 보관한다.
    private readonly HashSet<GridActor> flashingAlertIcons = new();
    // 켜짐·꺼짐 구간 모두를 포함해 눈 점멸 연출이 진행 중인 적이다.
    private readonly HashSet<GridActor> activeIconFlashes = new();

    /// <summary>눈 점멸 진행 여부를 공유하고 종료 시 켜짐 상태도 정리한다.</summary>
    public void SetAlertIconFlashing(GridActor actor, bool flashing)
    {
        if (flashing) activeIconFlashes.Add(actor);
        else
        {
            activeIconFlashes.Remove(actor);
            flashingAlertIcons.Remove(actor);
        }
    }

    /// <summary>수색·발각 눈의 점멸 연출이 진행 중인지 반환한다.</summary>
    public bool IsAlertIconFlashing(GridActor actor) => activeIconFlashes.Contains(actor);

    /// <summary>수색·발각 연출의 눈 점멸 상태를 HUD와 공유한다.</summary>
    public void SetAlertIconVisible(GridActor actor, bool visible)
    {
        if (visible) flashingAlertIcons.Add(actor);
        else flashingAlertIcons.Remove(actor);
    }

    /// <summary>해당 적의 눈이 현재 점멸 중 켜진 구간인지 반환한다.</summary>
    public bool IsAlertIconVisible(GridActor actor) => flashingAlertIcons.Contains(actor);

    /// <summary>현재 화면에 반영한 적 인식 상태를 HUD에 공유한다.</summary>
    public void PresentEnemyState(GridActor actor, EnemyAwarenessState awareness, SuspiciousBehaviorPhase phase)
        => presentedEnemyStates[actor] = (awareness, phase);

    /// <summary>초기 상태 또는 마지막 재생된 인식 상태와 조사 단계를 반환한다.</summary>
    public (EnemyAwarenessState awareness, SuspiciousBehaviorPhase phase) GetPresentedEnemyState(
        EnemyContext enemy)
        => presentedEnemyStates.TryGetValue(enemy.GridActor, out var state)
            ? state : (enemy.AlertState.CurrentState, enemy.AlertState.SuspiciousPhase);


    /// <summary>아직 피해 연출을 시작하지 않은 액터의 초기 화면 HP를 보존한다.</summary>
    public void PreserveHealthBeforeDamage(GridActor actor, int hitPointBefore)
    {
        if (!presentedHealth.ContainsKey(actor)) presentedHealth.Add(actor, hitPointBefore);
    }

    /// <summary>현재 공격 이벤트의 결과를 화면 HP에 적용한다. 논리 HP는 변경하지 않는다.</summary>
    public void PresentDamage(GridActor actor, DamageResult result)
    {
        presentedHealth[actor] = result.HitPointAfter;
    }

    /// <summary>피해 이력이 없으면 초기 논리 HP를, 있으면 마지막으로 재생한 HP를 반환한다.</summary>
    public int GetPresentedHitPoint(GridActor actor, int initialHitPoint)
        => presentedHealth.TryGetValue(actor, out int value) ? value : initialHitPoint;

    // 새 Actor와 Visual 연결이 등록됐을 때 발생한다.
    public event Action<GridActor, ActorVisualController> ActorRegistered;
    // Actor와 Visual 연결이 등록 해제됐을 때 발생한다.
    public event Action<GridActor, ActorVisualController> ActorUnregistered;

    /// <summary>
    /// 씬의 단일 연출 등록소 인스턴스를 등록한다.
    /// </summary>
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"{nameof(ActorPresentationRegistry)}: 이미 인스턴스가 있습니다. 중복 오브젝트 {name}의 컴포넌트를 비활성화합니다.", this);
            enabled = false;
            return;
        }

        Instance = this;
    }

    /// <summary>
    /// 현재 인스턴스가 제거될 때 등록 정보와 전역 참조를 정리한다.
    /// </summary>
    private void OnDestroy()
    {
        flashingAlertIcons.Clear();
        activeIconFlashes.Clear();
        presentedHealth.Clear();
        presentedEnemyStates.Clear();
        visualByActor.Clear();
        facingIndicatorByActor.Clear();
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// 지정한 논리 Actor와 시각 제어 컴포넌트의 연결을 등록한다.
    /// </summary>
    public bool Register(GridActor actor, ActorVisualController visualController)
    {
        if (actor == null || visualController == null)
        {
            Debug.LogError($"{nameof(ActorPresentationRegistry)}: 등록할 {nameof(GridActor)} 또는 {nameof(ActorVisualController)} 참조가 비어 있습니다.", this);
            return false;
        }

        if (visualByActor.TryGetValue(actor, out ActorVisualController registeredVisual) && registeredVisual != visualController)
        {
            Debug.LogError($"{nameof(ActorPresentationRegistry)}: {actor.name} Actor에 서로 다른 시각 제어 컴포넌트가 중복 등록되었습니다.", this);
            return false;
        }

        bool isNewBinding = !visualByActor.ContainsKey(actor);
        visualByActor[actor] = visualController;
        if (isNewBinding)
        {
            ActorRegistered?.Invoke(actor, visualController);
        }

        return true;
    }

    /// <summary>
    /// 지정한 논리 Actor와 시각 제어 컴포넌트의 연결을 해제한다.
    /// </summary>
    public void Unregister(GridActor actor, ActorVisualController visualController)
    {
        if (actor == null || visualController == null)
        {
            return;
        }

        if (visualByActor.TryGetValue(actor, out ActorVisualController registeredVisual) && registeredVisual == visualController)
        {
            visualByActor.Remove(actor);
            flashingAlertIcons.Remove(actor);
            activeIconFlashes.Remove(actor);
            presentedHealth.Remove(actor);
            presentedEnemyStates.Remove(actor);
            ActorUnregistered?.Invoke(actor, visualController);
        }
    }

    /// <summary>
    /// 지정한 논리 Actor에 연결된 시각 제어 컴포넌트를 찾는다.
    /// </summary>
    public bool TryGetVisual(GridActor actor, out ActorVisualController visualController)
    {
        visualController = null;
        return actor != null && visualByActor.TryGetValue(actor, out visualController) && visualController != null;
    }

    /// <summary>
    /// 지정한 논리 Actor와 적 시야 방향 표시 Presenter의 연결을 등록한다.
    /// </summary>
    public bool RegisterFacingIndicator(GridActor actor, EnemyFacingIndicatorPresenter indicatorPresenter)
    {
        if (actor == null || indicatorPresenter == null)
        {
            Debug.LogError($"{nameof(ActorPresentationRegistry)}: 등록할 {nameof(GridActor)} 또는 {nameof(EnemyFacingIndicatorPresenter)} 참조가 비어 있습니다.", this);
            return false;
        }

        if (facingIndicatorByActor.TryGetValue(actor, out EnemyFacingIndicatorPresenter registeredIndicator) &&
            registeredIndicator != indicatorPresenter)
        {
            Debug.LogError($"{nameof(ActorPresentationRegistry)}: {actor.name} Actor에 서로 다른 시야 방향 표시 Presenter가 중복 등록되었습니다.", this);
            return false;
        }

        facingIndicatorByActor[actor] = indicatorPresenter;
        return true;
    }

    /// <summary>
    /// 지정한 논리 Actor와 적 시야 방향 표시 Presenter의 연결을 해제한다.
    /// </summary>
    public void UnregisterFacingIndicator(GridActor actor, EnemyFacingIndicatorPresenter indicatorPresenter)
    {
        if (actor == null || indicatorPresenter == null)
        {
            return;
        }

        if (facingIndicatorByActor.TryGetValue(actor, out EnemyFacingIndicatorPresenter registeredIndicator) &&
            registeredIndicator == indicatorPresenter)
        {
            facingIndicatorByActor.Remove(actor);
        }
    }

    /// <summary>
    /// 지정한 논리 Actor에 연결된 적 시야 방향 표시 Presenter를 찾는다.
    /// </summary>
    public bool TryGetFacingIndicator(
        GridActor actor,
        out EnemyFacingIndicatorPresenter indicatorPresenter)
    {
        indicatorPresenter = null;
        return actor != null &&
            facingIndicatorByActor.TryGetValue(actor, out indicatorPresenter) &&
            indicatorPresenter != null;
    }
}
