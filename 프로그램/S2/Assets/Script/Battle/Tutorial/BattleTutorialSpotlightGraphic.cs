using UnityEngine;
using UnityEngine.UI;

/// <summary>노출할 사각 영역을 제외한 화면을 검게 그리고 가려진 영역의 클릭을 차단한다.</summary>
public sealed class BattleTutorialSpotlightGraphic : MaskableGraphic, ICanvasRaycastFilter
{
    // 이 Graphic의 로컬 좌표에서 버튼 또는 목표 칸을 노출할 영역이다.
    private Rect aperture;
    public Rect Aperture => aperture;

    /// <summary>화면 안의 노출 영역을 갱신하고 가림막을 표시한다.</summary>
    public void ShowAperture(Rect value)
    {
        Rect bounds = rectTransform.rect;
        var clamped = Rect.MinMaxRect(Mathf.Clamp(value.xMin, bounds.xMin, bounds.xMax), Mathf.Clamp(value.yMin, bounds.yMin, bounds.yMax),
            Mathf.Clamp(value.xMax, bounds.xMin, bounds.xMax), Mathf.Clamp(value.yMax, bounds.yMin, bounds.yMax));
        if (aperture != clamped) { aperture = clamped; SetVerticesDirty(); }
        gameObject.SetActive(true);
    }

    /// <summary>대사·연출·메뉴 등 가림막이 필요 없는 순간에 표시와 입력 차단을 해제한다.</summary>
    public void Hide() { gameObject.SetActive(false); }

    /// <summary>구멍 안의 클릭은 아래 버튼/맵으로 통과시키고 검은 영역의 클릭만 받는다.</summary>
    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPoint, eventCamera, out var point);
        return !aperture.Contains(point);
    }

    /// <summary>사각 구멍 주위 네 면과 얇은 청록 테두리를 UI 메시로 만든다.</summary>
    protected override void OnPopulateMesh(VertexHelper helper)
    {
        helper.Clear();
        Rect b = rectTransform.rect;
        AddQuad(helper, Rect.MinMaxRect(b.xMin, b.yMin, aperture.xMin, b.yMax), color);
        AddQuad(helper, Rect.MinMaxRect(aperture.xMax, b.yMin, b.xMax, b.yMax), color);
        AddQuad(helper, Rect.MinMaxRect(aperture.xMin, b.yMin, aperture.xMax, aperture.yMin), color);
        AddQuad(helper, Rect.MinMaxRect(aperture.xMin, aperture.yMax, aperture.xMax, b.yMax), color);
        Color edge = new Color(.2f, 1f, .95f, 1f);
        AddQuad(helper, new Rect(aperture.xMin, aperture.yMin, aperture.width, 2), edge);
        AddQuad(helper, new Rect(aperture.xMin, aperture.yMax - 2, aperture.width, 2), edge);
        AddQuad(helper, new Rect(aperture.xMin, aperture.yMin, 2, aperture.height), edge);
        AddQuad(helper, new Rect(aperture.xMax - 2, aperture.yMin, 2, aperture.height), edge);
    }

    /// <summary>양의 크기를 가진 사각형만 두 삼각형으로 추가한다.</summary>
    private static void AddQuad(VertexHelper helper, Rect rect, Color tint)
    {
        if (rect.width <= 0 || rect.height <= 0) return;
        int start = helper.currentVertCount;
        helper.AddVert(new Vector3(rect.xMin, rect.yMin, 0), tint, Vector2.zero);
        helper.AddVert(new Vector3(rect.xMin, rect.yMax, 0), tint, Vector2.zero);
        helper.AddVert(new Vector3(rect.xMax, rect.yMax, 0), tint, Vector2.zero);
        helper.AddVert(new Vector3(rect.xMax, rect.yMin, 0), tint, Vector2.zero);
        helper.AddTriangle(start, start + 1, start + 2); helper.AddTriangle(start, start + 2, start + 3);
    }
}
