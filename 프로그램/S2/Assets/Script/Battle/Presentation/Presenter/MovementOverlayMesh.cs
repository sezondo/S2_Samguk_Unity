using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>이동 표시의 선분을 하나의 동적 메시로 묶고 소유한 자원을 정리한다.</summary>
public sealed class MovementOverlayMesh : IDisposable
{
    // 월드 좌표로 그려 액터의 이동·스케일 영향을 받지 않는 표시 루트다.
    private readonly GameObject root;
    // 재사용하는 메시와 꼭짓점·색·삼각형 버퍼다.
    private readonly Mesh mesh;
    private readonly List<Vector3> vertices = new();
    private readonly List<Color> colors = new();
    private readonly List<Vector2> uvs = new();
    private readonly List<int> triangles = new();

    /// <summary>명시적으로 전달한 머티리얼과 정렬 순서로 표시기를 만든다.</summary>
    public MovementOverlayMesh(string name, Material material, int order)
    {
        root = new GameObject(name);
        mesh = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.MarkDynamic();
        root.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = root.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.sortingOrder = order;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    /// <summary>이번 갱신의 선분 버퍼를 비운다.</summary>
    public void Clear()
    {
        vertices.Clear(); colors.Clear(); uvs.Clear(); triangles.Clear();
    }

    /// <summary>실선 또는 일정 간격의 점선으로 선분을 추가한다.</summary>
    public void Line(Vector3 a, Vector3 b, float width, Color color, float period = 0, float duty = .62f)
    {
        float length = Vector3.Distance(a, b);
        if (length < .0001f) return;
        if (period <= 0) { Quad(a, b, width, color); return; }
        Vector3 direction = (b - a) / length;
        for (float d = 0; d < length; d += period)
            Quad(a + direction * d, a + direction * Mathf.Min(length, d + period * duty), width, color);
    }

    /// <summary>셰이더가 가장자리를 부드럽게 처리할 여백을 포함해 선분을 만든다.</summary>
    private void Quad(Vector3 a, Vector3 b, float width, Color color)
    {
        Vector3 direction = (b - a).normalized;
        Vector3 normal = new Vector3(-direction.y, direction.x, 0) * width;
        int i = vertices.Count;
        vertices.Add(a - normal); vertices.Add(a + normal); vertices.Add(b + normal); vertices.Add(b - normal);
        for (int n = 0; n < 4; n++) colors.Add(color);
        uvs.Add(new Vector2(0, -1)); uvs.Add(new Vector2(0, 1));
        uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(1, -1));
        triangles.Add(i); triangles.Add(i + 1); triangles.Add(i + 2);
        triangles.Add(i); triangles.Add(i + 2); triangles.Add(i + 3);
    }

    /// <summary>모서리만 남긴 도착 칸 브래킷을 추가한다.</summary>
    public void Bracket(Vector3 center, float size, float width, Color color)
    {
        float half = size * .43f, arm = size * .2f;
        for (int x = -1; x <= 1; x += 2)
        for (int y = -1; y <= 1; y += 2)
        {
            Vector3 corner = center + new Vector3(x * half, y * half);
            Line(corner, corner - new Vector3(x * arm, 0), width, color);
            Line(corner, corner - new Vector3(0, y * arm), width, color);
        }
    }

    /// <summary>현재 버퍼를 렌더러에 반영한다.</summary>
    public void Apply()
    {
        mesh.Clear(); mesh.SetVertices(vertices); mesh.SetColors(colors);
        mesh.SetUVs(0, uvs); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
    }

    /// <summary>표시를 지우고 재사용 가능한 상태로 둔다.</summary>
    public void Hide() { Clear(); Apply(); }

    /// <summary>표시 루트와 메시를 함께 해제한다.</summary>
    public void Dispose() { UnityEngine.Object.Destroy(root); UnityEngine.Object.Destroy(mesh); }
}
