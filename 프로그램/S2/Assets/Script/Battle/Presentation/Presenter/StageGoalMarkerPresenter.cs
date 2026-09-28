using UnityEngine;

/// <summary>목표 한 칸에 고정된 빛을 안개 위에 표시한다. 시야와 목표 판정은 변경하지 않는다.</summary>
public sealed class StageGoalMarkerPresenter : MonoBehaviour
{
    // 목표 좌표와 그리드 참조를 제공하는 목표 컴포넌트다.
    [SerializeField] private StageGoal stageGoal;
    // 현재 안개 정렬 기준을 제공하는 연출 컴포넌트다.
    [SerializeField] private PlayerVisionPresenter visionPresenter;
    // 고정 경계·내부 흐름·입자를 그리는 전용 렌더러다.
    [SerializeField] private MeshRenderer markerRenderer;
    // 테두리 바깥의 좁은 빛 번짐까지 담는 메시 크기다. 셰이더의 좌표 배율과 일치한다.
    public const float MeshCellRatio = 1.24f;

    /// <summary>필수 연결을 검사하고 첫 화면부터 목표 위치에 표시한다.</summary>
    private void OnEnable()
    {
        if (!HasValidReference() || !HasValidData()) { enabled = false; return; }
        RefreshMarker();
    }

    /// <summary>목표 좌표와 안개 정렬 변경을 화면에 반영한다.</summary>
    private void LateUpdate() => RefreshMarker();

    /// <summary>비활성화된 표시가 화면에 남지 않도록 정리한다.</summary>
    private void OnDisable()
    {
        if (markerRenderer != null) markerRenderer.enabled = false;
    }

    /// <summary>목표 칸 중심과 크기에 맞추고 안개보다 나중에 그린다.</summary>
    public void RefreshMarker()
    {
        GridManager grid = stageGoal.GridManager;
        markerRenderer.transform.position = grid.GridToWorld(stageGoal.GoalPosition);
        markerRenderer.transform.rotation = Quaternion.identity;
        markerRenderer.transform.localScale = Vector3.one * (grid.CellSize * MeshCellRatio);
        markerRenderer.sortingLayerName = visionPresenter.FogSortingLayerName;
        markerRenderer.sortingOrder = visionPresenter.FogSortingOrder + 2;
        markerRenderer.enabled = stageGoal.isActiveAndEnabled;
    }

    /// <summary>목표·안개·표시 소재의 명시적인 연결을 검사한다.</summary>
    public bool HasValidReference()
    {
        bool valid = stageGoal != null && stageGoal.GridManager != null && visionPresenter != null &&
            markerRenderer != null && markerRenderer.sharedMaterial != null &&
            markerRenderer.sharedMaterial.shader != null &&
            markerRenderer.sharedMaterial.shader.name == "S2/Presentation/GoalFloor";
        if (!valid) Debug.LogError($"{name}: 목표 표시에는 StageGoal, GridManager, 시야 Presenter와 목표 전용 소재가 필요합니다.", this);
        return valid;
    }

    /// <summary>목표 칸·셀 크기·정렬 범위와 효과 주기가 유효한지 검사한다.</summary>
    public bool HasValidData()
    {
        float cell = stageGoal.GridManager.CellSize;
        Material material = markerRenderer.sharedMaterial;
        bool valid = cell > 0f && !float.IsNaN(cell) && !float.IsInfinity(cell) &&
            stageGoal.GridManager.IsInside(stageGoal.GoalPosition) && visionPresenter.FogSortingOrder <= 32765 &&
            material.GetFloat("_PulsePeriod") > 0f && material.GetFloat("_ParticlePeriod") > 0f;
        if (!valid) Debug.LogError($"{name}: 목표 표시의 좌표·크기·정렬·애니메이션 주기가 유효하지 않습니다.", this);
        return valid;
    }
}
