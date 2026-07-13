using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 논리 GridActor와 화면 표시용 ActorVisualController의 연결을 관리하는 씬 단위 등록소다.
/// </summary>
public class ActorPresentationRegistry : MonoBehaviour
{
    public static ActorPresentationRegistry Instance { get; private set; }

    // 논리 Actor를 기준으로 등록된 시각 제어 컴포넌트를 찾는 맵이다.
    private readonly Dictionary<GridActor, ActorVisualController> visualByActor = new();

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
        visualByActor.Clear();
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

        visualByActor[actor] = visualController;
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
}
