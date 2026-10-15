using System.Collections.Generic;
using UnityEngine;

/// <summary>지정한 건물 가림 영역에 현재 표시되는 아군·적의 위치가 들어오면 건물 본체를 반투명하게 표시한다.</summary>
public sealed class BuildingOcclusionPresenter : MonoBehaviour
{
    [Header("Reference")]
    // 반투명 처리할 건물 본체이며, 가림 다각형의 로컬 좌표 기준이다.
    [SerializeField] private SpriteRenderer buildingRenderer;
    // 아군과 적을 포함한 전술 유닛 목록을 제공한다.
    [SerializeField] private TacticalUnitRegistry unitRegistry;
    // 논리 유닛에 대응하는 실제 화면 표시 컴포넌트를 제공한다.
    [SerializeField] private ActorPresentationRegistry presentationRegistry;

    [Header("Occlusion Area")]
    // 건물 로컬 좌표의 단순 다각형이다. 외곽을 순서대로 입력하며 빈 마당은 오목한 윤곽으로 제외한다.
    [SerializeField] private Vector2[] occlusionPolygon = new Vector2[0];
    // 벽이 바닥과 만나는 경계이며 건물 로컬 X가 증가하는 순서로 지정한다.
    [SerializeField] private Vector2[] frontBoundary = new Vector2[0];
    // 발이 경계에 닿거나 조금 걸쳐 있으면 앞에 선 것으로 취급하는 로컬 여유다.
    [SerializeField, Min(0f)] private float frontBoundaryTolerance = 0.1f;
    // 이 건물이 이전 프레임에 정렬을 요청한 표시 컴포넌트다.
    private readonly HashSet<ActorVisualController> sortingTargets = new();
    // 이번 프레임에서 더 이상 요청하지 않는 표시 컴포넌트를 회수하는 임시 목록이다.
    private readonly List<ActorVisualController> releasedTargets = new();
    // 유닛 표시 Transform에서 몸통 가림을 검사할 월드 좌표 오프셋이다.
    [SerializeField] private Vector2 bodySampleOffset = Vector2.zero;
    // 유닛 표시 Transform에서 머리 가림을 검사할 월드 좌표 오프셋이다.
    [SerializeField] private Vector2 headSampleOffset = new Vector2(0f, 0.45f);

    [Header("Fade")]
    // 가림 중 원래 건물 알파에 곱할 불투명도 비율이다.
    [SerializeField, Range(0f, 1f)] private float occludedOpacity = 0.4f;
    // 원래 표시와 반투명 표시 사이를 완전히 전환하는 시간이다.
    [SerializeField, Min(0.01f)] private float transitionDuration = 0.15f;
    // 경계에서 반복 복원되는 현상을 줄이기 위해 가림 해제 뒤 기다리는 시간이다.
    [SerializeField, Min(0f)] private float restoreDelay = 0.1f;

    // 비활성화 시 복원할 건물 본체의 원래 알파다.
    private float originalAlpha;
    // 원래 알파에 적용 중인 현재 투명도 비율이다.
    private float opacityMultiplier = 1f;
    // 마지막 가림 감지 뒤 복원까지 남은 시간이다.
    private float remainingRestoreDelay;
    // 참조와 데이터 검증 및 원래 알파 저장을 완료했는지 나타낸다.
    private bool initialized;

    /// <summary>필수 구성을 검증하고 건물 본체의 원래 알파를 저장한다.</summary>
    private void OnEnable()
    {
        initialized = false;
        if (!HasValidReference() || !HasValidData())
        {
            enabled = false;
            return;
        }

        originalAlpha = buildingRenderer.color.a;
        opacityMultiplier = 1f;
        remainingRestoreDelay = 0f;
        initialized = true;
    }

    /// <summary>이동 연출이 반영된 아군·적의 표시 위치로 가림 여부를 검사하고 건물 알파를 갱신한다.</summary>
    private void LateUpdate()
    {
        if (!initialized)
        {
            return;
        }

        if (buildingRenderer == null || unitRegistry == null || presentationRegistry == null)
        {
            Debug.LogError($"{nameof(BuildingOcclusionPresenter)}: 실행 중 필수 참조가 사라져 반투명 처리를 중단합니다.", this);
            enabled = false;
            return;
        }

        bool occluded = HasOccludedUnit();
        remainingRestoreDelay = occluded ? restoreDelay : Mathf.Max(0f, remainingRestoreDelay - Time.deltaTime);
        float targetOpacity = occluded || remainingRestoreDelay > 0f ? occludedOpacity : 1f;
        float speed = (1f - occludedOpacity) / transitionDuration;
        opacityMultiplier = Mathf.MoveTowards(opacityMultiplier, targetOpacity, speed * Time.deltaTime);
        ApplyAlpha(originalAlpha * opacityMultiplier);
    }

    /// <summary>구성 요소를 끄거나 제거하면 건물의 원래 알파를 복원한다.</summary>
    private void OnDisable()
    {
        foreach (var visual in sortingTargets)
            if (visual != null) visual.SetBuildingFrontSorting(buildingRenderer, false);
        sortingTargets.Clear();
        if (initialized && buildingRenderer != null)
        {
            ApplyAlpha(originalAlpha);
        }

        initialized = false;
    }

    /// <summary>색상과 머티리얼을 유지하면서 건물 본체의 알파만 변경한다.</summary>
    private void ApplyAlpha(float alpha)
    {
        Color color = buildingRenderer.color;
        color.a = alpha;
        buildingRenderer.color = color;
    }

    /// <summary>현재 화면에 표시되는 아군·적 중 이 건물에 머리나 몸통이 가려지는 유닛을 찾는다.</summary>
    private bool HasOccludedUnit()
    {
        releasedTargets.Clear();
        releasedTargets.AddRange(sortingTargets);
        bool occluded = false;
        if (!buildingRenderer.enabled || !buildingRenderer.gameObject.activeInHierarchy)
        {
            ReleaseUnusedSorting();
            return false;
        }

        foreach (ITacticalUnit unit in unitRegistry.Units)
        {
            // 논리 사망은 연출보다 먼저 확정되므로 화면의 사망 자세를 기준으로 제외한다.
            if (unit == null || unit.GridActor == null || !unit.GridActor.isActiveAndEnabled ||
                (unit.Faction != UnitFaction.Player && unit.Faction != UnitFaction.Enemy) ||
                !presentationRegistry.TryGetVisual(unit.GridActor, out ActorVisualController visual))
            {
                continue;
            }

            SpriteRenderer actorRenderer = visual.TargetRenderer;
            // 최신 논리 시야 대신 연출에 적용된 시야 알파를 사용해 숨은 적의 위치를 누설하지 않는다.
            if (!visual.isActiveAndEnabled || visual.VisionAlpha <= 0.001f ||
                actorRenderer == null || !actorRenderer.enabled || !actorRenderer.gameObject.activeInHierarchy ||
                actorRenderer.color.a <= 0.001f || visual.CurrentAnimationStateName == "Death")
            {
                continue;
            }

            Vector3 origin = actorRenderer.transform.position;
            bool inFront = IsInFrontOfBuilding(visual.GroundWorldPosition);
            bool overlaps = buildingRenderer.bounds.Intersects(actorRenderer.bounds);
            visual.SetBuildingFrontSorting(buildingRenderer, inFront && overlaps);
            if (inFront && overlaps)
            {
                sortingTargets.Add(visual);
                releasedTargets.Remove(visual);
            }
            // 발 기준으로 앞에 있는 유닛은 머리가 이미지와 겹쳐도 가림 대상이 아니다.
            if (inFront || visual.HasVisionSortingOverride) continue;
            if (ContainsWorldPoint(origin + (Vector3)bodySampleOffset) ||
                ContainsWorldPoint(origin + (Vector3)headSampleOffset))
            {
                occluded = true;
            }
        }

        ReleaseUnusedSorting();
        return occluded;
    }

    /// <summary>등록 해제·사망·비활성 등으로 사라진 유닛의 정렬 요청을 복원한다.</summary>
    private void ReleaseUnusedSorting()
    {
        foreach (var visual in releasedTargets)
        {
            if (visual != null) visual.SetBuildingFrontSorting(buildingRenderer, false);
            sortingTargets.Remove(visual);
        }
    }

    /// <summary>현재 표시 발 위치와 구간별 바닥 경계를 비교해 건물 앞에 서 있는지 판정한다.</summary>
    public bool IsInFrontOfBuilding(Vector3 groundWorldPosition)
    {
        Vector2 point = buildingRenderer.transform.InverseTransformPoint(groundWorldPosition);
        float boundaryY = frontBoundary[0].y;
        if (point.x >= frontBoundary[frontBoundary.Length - 1].x)
            boundaryY = frontBoundary[frontBoundary.Length - 1].y;
        else
            for (int i = 1; i < frontBoundary.Length; i++)
            {
                if (point.x > frontBoundary[i].x) continue;
                Vector2 a = frontBoundary[i - 1], b = frontBoundary[i];
                boundaryY = Mathf.Lerp(a.y, b.y, Mathf.InverseLerp(a.x, b.x, point.x));
                break;
            }
        return point.y <= boundaryY + frontBoundaryTolerance;
    }

    /// <summary>월드 위치를 건물 로컬 좌표로 바꾸어 지정한 오목 다각형 내부인지 확인한다.</summary>
    public bool ContainsWorldPoint(Vector3 worldPoint)
    {
        Vector2 point = buildingRenderer.transform.InverseTransformPoint(worldPoint);
        bool inside = false;
        for (int i = 0, j = occlusionPolygon.Length - 1; i < occlusionPolygon.Length; j = i++)
        {
            Vector2 a = occlusionPolygon[j];
            Vector2 b = occlusionPolygon[i];
            Vector2 edge = b - a;
            float projection = Mathf.Clamp01(Vector2.Dot(point - a, edge) / edge.sqrMagnitude);
            if ((point - (a + edge * projection)).sqrMagnitude <= 0.000001f)
            {
                return true;
            }

            if ((a.y > point.y) != (b.y > point.y) &&
                point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    /// <summary>건물 본체와 유닛·표시 등록소의 필수 참조를 검사한다.</summary>
    public bool HasValidReference()
    {
        if (buildingRenderer == null || unitRegistry == null || presentationRegistry == null)
        {
            Debug.LogError($"{nameof(BuildingOcclusionPresenter)} on {name}에는 건물 SpriteRenderer, TacticalUnitRegistry, ActorPresentationRegistry 참조가 필요합니다.", this);
            return false;
        }

        return true;
    }

    /// <summary>가림 다각형과 투명도 전환 수치에 누락이나 잘못된 값이 없는지 검사한다.</summary>
    public bool HasValidData()
    {
        if (frontBoundary == null || frontBoundary.Length < 2 || !IsFinite(frontBoundaryTolerance) || frontBoundaryTolerance < 0f)
        {
            Debug.LogError($"{nameof(BuildingOcclusionPresenter)} on {name}에는 유효한 건물 바닥 경계가 필요합니다.", this);
            return false;
        }
        for (int i = 0; i < frontBoundary.Length; i++)
        {
            if (!IsFinite(frontBoundary[i].x) || !IsFinite(frontBoundary[i].y) ||
                (i > 0 && frontBoundary[i].x <= frontBoundary[i - 1].x))
            {
                Debug.LogError($"{nameof(BuildingOcclusionPresenter)} on {name}의 바닥 경계는 유한한 좌표를 X 오름차순으로 지정해야 합니다.", this);
                return false;
            }
        }
        if (occlusionPolygon == null || occlusionPolygon.Length < 3)
        {
            Debug.LogError($"{nameof(BuildingOcclusionPresenter)} on {name}에는 건물 가림 영역을 이루는 꼭짓점이 3개 이상 필요합니다.", this);
            return false;
        }

        float twiceArea = 0f;
        for (int i = 0; i < occlusionPolygon.Length; i++)
        {
            Vector2 a = occlusionPolygon[i];
            Vector2 b = occlusionPolygon[(i + 1) % occlusionPolygon.Length];
            if (!IsFinite(a.x) || !IsFinite(a.y) || (b - a).sqrMagnitude < 0.000001f)
            {
                Debug.LogError($"{nameof(BuildingOcclusionPresenter)} on {name}의 가림 영역 좌표가 유효하지 않거나 연속 꼭짓점이 중복됩니다.", this);
                return false;
            }

            twiceArea += a.x * b.y - b.x * a.y;
        }

        if (Mathf.Abs(twiceArea) < 0.000001f ||
            !IsFinite(bodySampleOffset.x) || !IsFinite(bodySampleOffset.y) ||
            !IsFinite(headSampleOffset.x) || !IsFinite(headSampleOffset.y) ||
            !IsFinite(occludedOpacity) || occludedOpacity < 0f || occludedOpacity >= 1f ||
            !IsFinite(transitionDuration) || transitionDuration <= 0f ||
            !IsFinite(restoreDelay) || restoreDelay < 0f)
        {
            Debug.LogError($"{nameof(BuildingOcclusionPresenter)} on {name}의 가림 영역 면적, 유닛 표본 위치 또는 반투명 전환 수치가 잘못되었습니다.", this);
            return false;
        }

        return true;
    }

    /// <summary>직렬화된 수치가 유한한 실수인지 확인한다.</summary>
    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>선택한 건물의 가림 영역을 Scene 뷰에 표시한다.</summary>
    private void OnDrawGizmosSelected()
    {
        if (buildingRenderer == null || occlusionPolygon == null || occlusionPolygon.Length < 3)
        {
            return;
        }

        Color previousColor = Gizmos.color;
        Gizmos.color = Color.cyan;
        for (int i = 0; i < occlusionPolygon.Length; i++)
        {
            Vector3 a = buildingRenderer.transform.TransformPoint(occlusionPolygon[i]);
            Vector3 b = buildingRenderer.transform.TransformPoint(occlusionPolygon[(i + 1) % occlusionPolygon.Length]);
            Gizmos.DrawLine(a, b);
        }

        Gizmos.color = Color.yellow;
        if (frontBoundary != null)
            for (int i = 1; i < frontBoundary.Length; i++)
                Gizmos.DrawLine(buildingRenderer.transform.TransformPoint(frontBoundary[i - 1]), buildingRenderer.transform.TransformPoint(frontBoundary[i]));
        Gizmos.color = previousColor;
    }
}
