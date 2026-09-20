using System.Collections.Generic;
using UnityEngine;

/// <summary>낮은 소품의 바닥 경계와 캐릭터 발 위치로 앞뒤 정렬만 결정한다.</summary>
public sealed class PropDepthPresenter : MonoBehaviour
{
    [Header("Reference")]
    // 앞뒤를 비교할 소품 본체이며 경계의 로컬 좌표 기준이다.
    [SerializeField] private SpriteRenderer propRenderer;
    // 표시 중인 전술 유닛과 실제 그림을 연결하는 명시적 등록소다.
    [SerializeField] private TacticalUnitRegistry unitRegistry;
    [SerializeField] private ActorPresentationRegistry presentationRegistry;

    [Header("Depth Boundary")]
    // 캐릭터 발이 이 바닥선보다 화면 아래에 있으면 소품 앞으로 표시한다.
    [SerializeField] private float groundLocalY;
    // 바닥선에서 미세한 흔들림 때문에 앞뒤가 반복 반전되지 않게 하는 로컬 여유다.
    [SerializeField, Min(0f)] private float boundaryHysteresis = 0.03f;

    // 이전 프레임의 앞뒤 판정과 이번 프레임에서 해제할 대상을 보관한다.
    private readonly Dictionary<ActorVisualController, bool> targets = new();
    private readonly List<ActorVisualController> releaseTargets = new();
    // 필수 구성 검증이 끝났는지 나타낸다.
    private bool initialized;

    public SpriteRenderer PropRenderer => propRenderer;
    public float GroundLocalY => groundLocalY;

    /// <summary>참조와 경계 설정이 잘못됐으면 정렬을 시작하지 않는다.</summary>
    private void OnEnable()
    {
        initialized = HasValidReference() && HasValidData();
        if (!initialized) enabled = false;
    }

    /// <summary>최종 표시 위치를 기준으로 소품 앞/뒤 요청을 갱신한다.</summary>
    private void LateUpdate() => RefreshDepth();

    /// <summary>소품과 그림이 겹치는 보이는 유닛만 정렬하며 위치·알파는 건드리지 않는다.</summary>
    public void RefreshDepth()
    {
        if (!initialized) return;
        releaseTargets.Clear();
        releaseTargets.AddRange(targets.Keys);
        if (propRenderer.enabled && propRenderer.gameObject.activeInHierarchy)
        {
            foreach (ITacticalUnit unit in unitRegistry.Units)
            {
                if (unit?.GridActor == null || !unit.GridActor.isActiveAndEnabled ||
                    !presentationRegistry.TryGetVisual(unit.GridActor, out var visual) || !visual.isActiveAndEnabled) continue;
                SpriteRenderer renderer = visual.TargetRenderer;
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy ||
                    visual.VisionAlpha <= 0.001f || renderer.color.a <= 0.001f ||
                    !propRenderer.bounds.Intersects(renderer.bounds)) continue;
                float y = propRenderer.transform.InverseTransformPoint(visual.GroundWorldPosition).y;
                bool inFront = y <= groundLocalY;
                if (targets.TryGetValue(visual, out bool previous) && Mathf.Abs(y - groundLocalY) <= boundaryHysteresis)
                    inFront = previous;
                visual.SetPropDepthSorting(propRenderer, inFront);
                targets[visual] = inFront;
                releaseTargets.Remove(visual);
            }
        }
        foreach (var visual in releaseTargets)
        {
            if (visual != null) visual.SetPropDepthSorting(propRenderer, null);
            targets.Remove(visual);
        }
    }

    /// <summary>소품 기능이 꺼지면 남아 있는 정렬 요청을 모두 회수한다.</summary>
    private void OnDisable()
    {
        foreach (var visual in targets.Keys)
            if (visual != null) visual.SetPropDepthSorting(propRenderer, null);
        targets.Clear();
        initialized = false;
    }

    /// <summary>소품과 등록소가 명시적으로 연결됐는지 확인한다.</summary>
    public bool HasValidReference()
    {
        if (propRenderer != null && unitRegistry != null && presentationRegistry != null) return true;
        Debug.LogError($"{nameof(PropDepthPresenter)} on {name}에는 소품 렌더러와 전술 유닛·표시 등록소 참조가 필요합니다.", this);
        return false;
    }

    /// <summary>바닥 경계와 흔들림 방지 여유가 유한한 값인지 확인한다.</summary>
    public bool HasValidData()
    {
        if (!float.IsNaN(groundLocalY) && !float.IsInfinity(groundLocalY) &&
            !float.IsNaN(boundaryHysteresis) && !float.IsInfinity(boundaryHysteresis) && boundaryHysteresis >= 0f) return true;
        Debug.LogError($"{nameof(PropDepthPresenter)} on {name}의 바닥 경계와 0 이상의 여유를 확인하세요.", this);
        return false;
    }

    /// <summary>선택한 소품의 앞뒤 판정선을 노란색으로 표시한다.</summary>
    private void OnDrawGizmosSelected()
    {
        if (propRenderer == null || propRenderer.sprite == null) return;
        Bounds bounds = propRenderer.sprite.bounds;
        Color previous = Gizmos.color;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(propRenderer.transform.TransformPoint(new Vector3(bounds.min.x, groundLocalY, 0f)),
            propRenderer.transform.TransformPoint(new Vector3(bounds.max.x, groundLocalY, 0f)));
        Gizmos.color = previous;
    }
}
