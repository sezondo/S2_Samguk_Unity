using System.Collections.Generic;
using UnityEngine;

/// <summary>만화 페이지의 좌상단 기준 다각형을 잘라낸 Sprite의 삼각형 표시 영역으로 변환한다.</summary>
public static class StoryComicCutGeometry
{
    // 정규화 좌표에서 면적이 없는 꼭짓점과 삼각형을 구분하는 오차다.
    private const float Epsilon = 0.000001f;

    /// <summary>단순 다각형의 유효성을 검사하고 픽셀 자르기 영역·로컬 꼭짓점·삼각형을 계산한다.</summary>
    public static bool TryBuild(Vector2[] polygon, Rect pageRect, float pixelsPerUnit,
        out Rect cropRect, out Vector2[] vertices, out ushort[] triangles)
    {
        cropRect = default; vertices = null; triangles = null;
        if (polygon == null || polygon.Length < 3 || polygon.Length > 16 ||
            !IsFinite(pixelsPerUnit) || pixelsPerUnit <= 0f || pageRect.width <= 0f || pageRect.height <= 0f)
            return false;

        var points = new Vector2[polygon.Length];
        var minimum = Vector2.one;
        var maximum = Vector2.zero;
        float area = 0f;
        for (int i = 0; i < polygon.Length; i++)
        {
            Vector2 point = polygon[i];
            if (!IsFinite(point.x) || !IsFinite(point.y) || point.x < 0f || point.x > 1f || point.y < 0f || point.y > 1f)
                return false;
            points[i] = new Vector2(point.x, 1f - point.y);
            minimum = Vector2.Min(minimum, points[i]);
            maximum = Vector2.Max(maximum, points[i]);
        }

        for (int i = 0; i < points.Length; i++)
        {
            Vector2 a = points[i];
            Vector2 b = points[(i + 1) % points.Length];
            if ((a - b).sqrMagnitude <= Epsilon * Epsilon) return false;
            area += a.x * b.y - a.y * b.x;
            for (int j = i + 1; j < points.Length; j++)
            {
                if (j == (i + 1) % points.Length || (j + 1) % points.Length == i) continue;
                if (SegmentsIntersect(a, b, points[j], points[(j + 1) % points.Length])) return false;
            }
        }
        if (Mathf.Abs(area) <= Epsilon) return false;

        // 반시계 방향으로 통일한 뒤 오목한 컷도 내부 삼각형만 남도록 귀 자르기를 사용한다.
        var remaining = new List<int>();
        for (int i = 0; i < points.Length; i++) remaining.Add(area > 0f ? i : points.Length - 1 - i);
        var indices = new List<ushort>();
        while (remaining.Count > 3)
        {
            bool clipped = false;
            for (int i = 0; i < remaining.Count; i++)
            {
                int a = remaining[(i + remaining.Count - 1) % remaining.Count];
                int b = remaining[i];
                int c = remaining[(i + 1) % remaining.Count];
                if (Cross(points[a], points[b], points[c]) <= Epsilon) continue;
                bool containsPoint = false;
                foreach (int index in remaining)
                {
                    if (index == a || index == b || index == c) continue;
                    Vector2 p = points[index];
                    if (Cross(points[a], points[b], p) >= -Epsilon &&
                        Cross(points[b], points[c], p) >= -Epsilon && Cross(points[c], points[a], p) >= -Epsilon)
                    { containsPoint = true; break; }
                }
                if (containsPoint) continue;
                indices.Add((ushort)a); indices.Add((ushort)b); indices.Add((ushort)c);
                remaining.RemoveAt(i); clipped = true; break;
            }
            if (!clipped) return false;
        }
        if (Cross(points[remaining[0]], points[remaining[1]], points[remaining[2]]) <= Epsilon) return false;
        foreach (int index in remaining) indices.Add((ushort)index);

        // Unity의 Sprite 경계 검사는 픽셀 반올림을 포함하므로 자르기 영역을 정수 픽셀로 감싼다.
        float left = Mathf.Floor(pageRect.x + minimum.x * pageRect.width);
        float bottom = Mathf.Floor(pageRect.y + minimum.y * pageRect.height);
        float right = Mathf.Ceil(pageRect.x + maximum.x * pageRect.width);
        float top = Mathf.Ceil(pageRect.y + maximum.y * pageRect.height);
        cropRect = new Rect(left, bottom, right - left, top - bottom);
        Vector2 center = cropRect.center;
        vertices = new Vector2[points.Length];
        for (int i = 0; i < points.Length; i++)
            vertices[i] = (new Vector2(pageRect.x + points[i].x * pageRect.width,
                pageRect.y + points[i].y * pageRect.height) - center) / pixelsPerUnit;
        triangles = indices.ToArray();
        return true;
    }

    /// <summary>세 점이 만드는 부호 있는 면적을 계산한다.</summary>
    private static float Cross(Vector2 a, Vector2 b, Vector2 c) =>
        (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);

    /// <summary>서로 이웃하지 않는 두 변의 교차 또는 접촉을 검사한다.</summary>
    private static bool SegmentsIntersect(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        float abC = Cross(a, b, c), abD = Cross(a, b, d), cdA = Cross(c, d, a), cdB = Cross(c, d, b);
        if (((abC > Epsilon && abD < -Epsilon) || (abC < -Epsilon && abD > Epsilon)) &&
            ((cdA > Epsilon && cdB < -Epsilon) || (cdA < -Epsilon && cdB > Epsilon))) return true;
        return (Mathf.Abs(abC) <= Epsilon && OnSegment(a, b, c)) ||
            (Mathf.Abs(abD) <= Epsilon && OnSegment(a, b, d)) ||
            (Mathf.Abs(cdA) <= Epsilon && OnSegment(c, d, a)) ||
            (Mathf.Abs(cdB) <= Epsilon && OnSegment(c, d, b));
    }

    /// <summary>일직선 위의 점이 선분 범위에 포함되는지 검사한다.</summary>
    private static bool OnSegment(Vector2 a, Vector2 b, Vector2 p) =>
        p.x >= Mathf.Min(a.x, b.x) - Epsilon && p.x <= Mathf.Max(a.x, b.x) + Epsilon &&
        p.y >= Mathf.Min(a.y, b.y) - Epsilon && p.y <= Mathf.Max(a.y, b.y) + Epsilon;

    /// <summary>NaN과 무한대를 데이터 오류로 구분한다.</summary>
    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
