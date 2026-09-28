using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>이동 계산 결과를 AP별 외곽선, 경로와 도착 모서리로 표시한다.</summary>
[RequireComponent(typeof(PlayerGridMoveAction))]
public class GridMoveRangeHighlighter : MonoBehaviour
{
    [Header("Source")]
    // 이동 범위와 경로 이벤트를 제공하는 행동 참조다.
    [SerializeField] private PlayerGridMoveAction moveAction;
    // 선의 안티앨리어싱과 색상을 그리는 필수 머티리얼이다.
    [SerializeField] private Material lineMaterial;
    // AP 경계 표기에 사용할 명시적 폰트 에셋이다.
    [SerializeField] private TMP_FontAsset labelFont;
    [Header("Movement Style")]
    // AP별 누적 경계를 구분하는 청록·노란색·빨간색이다.
    [SerializeField] private Color oneApColor = new(.41f, .83f, .8f, .85f);
    [SerializeField] private Color twoApColor = new(1f, .85f, .2f, .9f);
    [SerializeField] private Color threeApColor = new(1f, .25f, .2f, .9f);
    // 경로와 새 발각 위험 구간의 색이다.
    [SerializeField] private Color routeColor = new(.94f, .89f, .77f, 1);
    [SerializeField] private Color riskColor = new(1, .57f, .29f, 1);
    // 한 칸 크기에 대한 선 두께이며 반투명 여백은 셰이더에서 처리한다.
    [SerializeField] private float lineWidthRatio = .035f;
    // 안개보다 아래, 지형보다 위에 표시하는 정렬 순서다.
    [SerializeField] private int overlayOrder = 1900;

    // 범위와 경로를 독립 갱신하는 런타임 메시다.
    private MovementOverlayMesh rangeMesh;
    private MovementOverlayMesh pathMesh;
    // AP 경계와 공통 장애물 경계의 중복 출력을 막는 누적 집합이다.
    private readonly HashSet<GridPosition> area = new();
    private readonly HashSet<(GridPosition, int)> drawnEdges = new();
    // 경고 상태가 변했을 때 다시 그릴 현재 경로다.
    private readonly List<GridPosition> previewPath = new();
    // 현재 첫 위험 칸 인덱스이며 -1은 안전한 경로다.
    private int riskIndex = -1;
    // 위험 판정 재검사 간격으로 커서를 움직이지 않아도 상태 변화를 반영한다.
    private float nextRiskCheck;
    // 경계선 위에 표시하는 AP 비용 표기 세 개다.
    private readonly TextMeshPro[] labels = new TextMeshPro[3];

    /// <summary>필수 표시 참조와 수치를 검사하고 메시를 준비한다.</summary>
    private void Awake()
    {
        if (!HasValidReference() || !HasValidData()) { enabled = false; return; }
        rangeMesh = new MovementOverlayMesh(name + "_APBoundaries", lineMaterial, overlayOrder);
        pathMesh = new MovementOverlayMesh(name + "_MoveRoute", lineMaterial, overlayOrder + 1);
        for (int i = 0; i < labels.Length; i++)
        {
            labels[i] = new GameObject(name + "_APLabel" + i).AddComponent<TextMeshPro>();
            labels[i].font = labelFont;
            labels[i].fontSize = 2;
            labels[i].alignment = TextAlignmentOptions.Center;
            labels[i].rectTransform.sizeDelta = new Vector2(.8f, .3f);
            labels[i].renderer.sortingOrder = overlayOrder + 2;
            labels[i].gameObject.SetActive(false);
        }
    }

    /// <summary>이동 행동의 표시 이벤트를 구독한다.</summary>
    private void OnEnable()
    {
        if (moveAction == null) return;
        moveAction.MoveRangeSegmentsShown += ShowMoveRangeSegments;
        moveAction.MoveRangeHidden += HideMoveRange;
        moveAction.MovePathPreviewShown += ShowPath;
        moveAction.MovePathPreviewHidden += HidePath;
    }

    /// <summary>구독을 해제하고 모든 임시 표시를 숨긴다.</summary>
    private void OnDisable()
    {
        if (moveAction != null)
        {
            moveAction.MoveRangeSegmentsShown -= ShowMoveRangeSegments;
            moveAction.MoveRangeHidden -= HideMoveRange;
            moveAction.MovePathPreviewShown -= ShowPath;
            moveAction.MovePathPreviewHidden -= HidePath;
        }
        HideMoveRange();
    }

    /// <summary>경계 상태가 바뀌면 정지한 포인터의 위험 표시도 갱신한다.</summary>
    private void Update()
    {
        if (previewPath.Count == 0 || Time.unscaledTime < nextRiskCheck) return;
        nextRiskCheck = Time.unscaledTime + .1f;
        int current = moveAction.UnitContext.GridMoveRiskEvaluator.FindFirstPreviewRiskIndex(previewPath);
        if (current != riskIndex) { riskIndex = current; DrawPath(); }
    }

    /// <summary>소유한 메시와 런타임 루트를 해제한다.</summary>
    private void OnDestroy()
    {
        rangeMesh?.Dispose(); pathMesh?.Dispose();
        foreach (TextMeshPro label in labels) if (label != null) Destroy(label.gameObject);
    }

    /// <summary>AP별 칸 목록을 누적하고 같은 장애물 경계는 한 번만 그린다.</summary>
    private void ShowMoveRangeSegments(IReadOnlyList<GridPosition> first, IReadOnlyList<GridPosition> second, IReadOnlyList<GridPosition> third)
    {
        if (rangeMesh == null || GridManager.Instance == null) return;
        rangeMesh.Clear(); area.Clear(); drawnEdges.Clear();
        foreach (TextMeshPro label in labels) label.gameObject.SetActive(false);
        area.Add(moveAction.UnitContext.GridActor.GridPosition);
        AddArea(first);
        if (first.Count > 0) DrawBoundary(oneApColor, 0);
        AddArea(second);
        if (second.Count > 0) DrawBoundary(twoApColor, 1);
        AddArea(third);
        if (third.Count > 0) DrawBoundary(threeApColor, 2);
        rangeMesh.Apply();
    }

    /// <summary>다음 AP 구간의 칸을 누적 집합에 합친다.</summary>
    private void AddArea(IReadOnlyList<GridPosition> positions)
    {
        for (int i = 0; i < positions.Count; i++) area.Add(positions[i]);
    }

    /// <summary>집합 밖과 맞닿은 면만 그려 내부 칸을 비워둔다.</summary>
    private void DrawBoundary(Color color, int tier)
    {
        GridManager grid = GridManager.Instance;
        float size = grid.CellSize, half = size * .5f;
        Vector3 origin = grid.GridToWorld(moveAction.UnitContext.GridActor.GridPosition);
        float best = float.NegativeInfinity;
        Vector3 labelPosition = Vector3.zero;
        foreach (GridPosition p in area)
        {
            Vector3 c = grid.GridToWorld(p); c.z = -.05f;
            Edge(p, 0, GridPosition.Up, c + new Vector3(-half, half), c + new Vector3(half, half));
            Edge(p, 1, GridPosition.Down, c + new Vector3(-half, -half), c + new Vector3(half, -half));
            Edge(p, 2, GridPosition.Left, c + new Vector3(-half, -half), c + new Vector3(-half, half));
            Edge(p, 3, GridPosition.Right, c + new Vector3(half, -half), c + new Vector3(half, half));
        }
        if (!float.IsNegativeInfinity(best))
        {
            TextMeshPro label = labels[tier];
            label.gameObject.SetActive(true);
            label.transform.position = labelPosition;
            label.transform.localScale = Vector3.one * size;
            label.color = color;
            int ap = (tier + 1) * moveAction.UnitContext.UnitData.MoveActionPointCost;
            label.text = ap + (tier == 2 && moveAction.MoveRange > moveAction.MoveDistancePerActionPoint * 3 ? "+ AP" : " AP");
            rangeMesh.Line(labelPosition - Vector3.right * size * .32f, labelPosition + Vector3.right * size * .32f,
                size * .24f, new Color(.035f, .065f, .075f, .95f));
        }
        // 내부 면과 이미 표시한 공통 외벽은 생략한다.
        void Edge(GridPosition p, int side, GridPosition offset, Vector3 a, Vector3 b)
        {
            if (area.Contains(p + offset) || !drawnEdges.Add((p, side))) return;
            rangeMesh.Line(a, b, size * lineWidthRatio, color);
            Vector3 mid = (a + b) * .5f;
            // 현재 공개된 경계에서 오른쪽의 읽기 쉬운 위치를 선택한다.
            float score = mid.x - origin.x - Mathf.Abs(mid.y - origin.y) * .3f;
            if (PlayerVisionManager.Instance != null && PlayerVisionManager.Instance.IsVisible(p) && score > best)
            { best = score; labelPosition = mid; }
        }
    }

    /// <summary>실제 진입 칸 목록을 보관하고 경로를 그린다.</summary>
    private void ShowPath(IReadOnlyList<GridPosition> path)
    {
        previewPath.Clear(); previewPath.AddRange(path);
        riskIndex = moveAction.UnitContext.GridMoveRiskEvaluator.FindFirstPreviewRiskIndex(previewPath);
        DrawPath();
    }

    /// <summary>경로와 도착 모서리, 첫 위험 칸의 느낌표를 표시한다.</summary>
    private void DrawPath()
    {
        if (pathMesh == null || GridManager.Instance == null) return;
        pathMesh.Clear();
        GridManager grid = GridManager.Instance;
        float size = grid.CellSize, width = size * lineWidthRatio;
        Vector3 previous = grid.GridToWorld(moveAction.UnitContext.GridActor.GridPosition);
        previous.z = -.08f;
        for (int i = 0; i < previewPath.Count; i++)
        {
            Vector3 next = grid.GridToWorld(previewPath[i]); next.z = -.08f;
            bool danger = riskIndex >= 0 && i >= riskIndex;
            pathMesh.Line(previous, next, width, danger ? riskColor : routeColor, danger ? size * .22f : 0);
            previous = next;
        }
        if (previewPath.Count > 0)
            pathMesh.Bracket(previous, size, width * 1.4f, riskIndex >= 0 ? riskColor : routeColor);
        if (riskIndex >= 0)
        {
            Vector3 p = grid.GridToWorld(previewPath[riskIndex]); p.z = -.09f;
            // 경로와 겹치지 않게 첫 위험 칸 위쪽에 작은 느낌표를 둔다.
            p += Vector3.up * size * .25f;
            pathMesh.Line(p, p + Vector3.up * size * .13f, width * 1.5f, riskColor);
            pathMesh.Line(p - Vector3.up * size * .08f, p - Vector3.up * size * .05f, width * 1.5f, riskColor);
        }
        pathMesh.Apply();
    }

    /// <summary>현재 경로와 경고를 함께 지운다.</summary>
    private void HidePath() { previewPath.Clear(); riskIndex = -1; pathMesh?.Hide(); }

    /// <summary>행동 종료·취소 시 범위와 경로를 함께 지운다.</summary>
    private void HideMoveRange()
    {
        rangeMesh?.Hide(); HidePath();
        foreach (TextMeshPro label in labels) if (label != null) label.gameObject.SetActive(false);
    }

    /// <summary>이동 계산과 전용 선 머티리얼 연결을 검사한다.</summary>
    private bool HasValidReference()
    {
        if (moveAction != null && lineMaterial != null && labelFont != null && moveAction.UnitContext != null && moveAction.UnitContext.GridMoveRiskEvaluator != null) return true;
        Debug.LogError($"{name}: 이동 표시에는 이동 행동·Context·위험 평가·선 머티리얼 연결이 필요합니다.", this);
        return false;
    }

    /// <summary>선 두께가 유효한지 검사한다.</summary>
    private bool HasValidData()
    {
        if (lineWidthRatio > 0 && lineWidthRatio <= .15f) return true;
        Debug.LogError($"{name}: 이동 선 두께는 0 초과 0.15 이하여야 합니다.", this);
        return false;
    }
}
