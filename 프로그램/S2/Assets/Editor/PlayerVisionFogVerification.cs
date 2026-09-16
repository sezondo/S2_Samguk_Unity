using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>씬이나 참조를 수정하지 않고 연속 시야의 차단·탐색·스냅샷 회귀를 검사한다.</summary>
public static class PlayerVisionFogVerification
{
    // 검증 결과와 화면을 저장하는 프로젝트 내 임시 경로다.
    private const string Output = "Temp/SoftVisionVerification";

    /// <summary>합성 벽과 현재 씬의 벽을 읽어 시야 계산의 회귀를 검사한다.</summary>
    [MenuItem("Tools/S2/Verify Soft Vision")]
    public static void Verify()
    {
        Directory.CreateDirectory(Output);
        var report = new StringBuilder();
        try
        {
            var source = new PlayerVisionSource(new GridPosition(3, 6), 10, false);
            var walls = new HashSet<GridPosition> { new(6, 6) };
            var snapshot = Snapshot(new[] { source }, walls);
            Assert(!PlayerVisionFogMask.HasLineOfSight(snapshot, source, 8, 6), "벽 뒤 차단");
            Assert(PlayerVisionFogMask.HasLineOfSight(snapshot, source, 6, 6), "벽 표면 표시");
            Assert(PlayerVisionFogMask.HasLineOfSight(snapshot, source, 7, 7), "벽 옆 근거리 노출");
            Assert(!PlayerVisionFogMask.HasLineOfSight(snapshot, source, 11, 7), "멀수록 넓어지는 벽 그림자");
            walls.Clear();
            Assert(!PlayerVisionFogMask.HasLineOfSight(snapshot, source, 8, 6), "스냅샷 차단물 독립 복사");
            Assert(PlayerVisionFogMask.HasLineOfSight(Snapshot(new[] { source }, walls), source, 8, 6), "문 개방 이후 새 시야");
            report.AppendLine("벽 차단·표면·쐐기 그림자·문 개방·이전 스냅샷 보존 통과");

            var cornerSource = new PlayerVisionSource(new GridPosition(3, 3), 6, false);
            walls.UnionWith(new[] { new GridPosition(4, 3), new GridPosition(3, 4) });
            Assert(!PlayerVisionFogMask.HasLineOfSight(Snapshot(new[] { cornerSource }, walls), cornerSource, 5, 5), "양쪽 모서리 차단");
            walls.Remove(new GridPosition(3, 4));
            Assert(PlayerVisionFogMask.HasLineOfSight(Snapshot(new[] { cornerSource }, walls), cornerSource, 5, 5), "한쪽 모서리 통과");
            report.AppendLine("기존 대각선 모서리 규칙 통과");

            var mask = new PlayerVisionFogMask(16, 16, 12, 1f, 0.35f);
            var colors = new Color32[mask.Width * mask.Height];
            Color remembered = new(0.16f, 0.17f, 0.18f, 0.65f);
            var circle = new PlayerVisionSource(new GridPosition(6, 6), 4, false);
            var timer = System.Diagnostics.Stopwatch.StartNew();
            mask.BuildColors(Snapshot(new[] { circle }, new HashSet<GridPosition>()), Color.black, remembered, Color.clear, colors);
            timer.Stop();
            Assert(Pixel(colors, mask.Width, 6, 6).a == 0, "중심 표시");
            Assert(Pixel(colors, mask.Width, 10, 10).a == 255, "사각형 대신 원형 반경");
            Assert(Pixel(colors, mask.Width, 10, 6).a > 0 && Pixel(colors, mask.Width, 10, 6).a < 255, "부드러운 원 테두리");
            Assert(colors[0].a == 255, "보드 외곽 미탐색");
            SaveMask(mask, colors, "circle.png");
            mask.BuildColors(Snapshot(Array.Empty<PlayerVisionSource>(), new HashSet<GridPosition>()), Color.black, remembered, Color.clear, colors);
            Color32 explored = Pixel(colors, mask.Width, 6, 6);
            Assert(Mathf.Abs(explored.a - 166) <= 1 && explored.r > 0, "빈 시야에서 회색 탐색 기억 유지");
            Assert(Pixel(colors, mask.Width, 10, 10).a == 255, "미관측 영역 기억 오염 없음");
            SaveMask(mask, colors, "remembered.png");
            report.AppendLine($"원형·경계·탐색 기억·외곽 통과. 16×16 마스크 계산 {timer.Elapsed.TotalMilliseconds:F2}ms");

            var coneMask = new PlayerVisionFogMask(16, 16, 12, 1f, 0.35f);
            coneMask.BuildColors(snapshot, Color.black, remembered, Color.clear, colors);
            SaveMask(coneMask, colors, "shadow.png");
            var second = new PlayerVisionSource(new GridPosition(11, 6), 2, false);
            coneMask.BuildColors(Snapshot(new[] { source, second }, new HashSet<GridPosition> { new(6, 6) }),
                Color.black, remembered, Color.clear, colors);
            Assert(Pixel(colors, mask.Width, 11, 6).a == 0, "아군·검 시야 합산");
            report.AppendLine("복수 원점 합산 통과");

            GridManager grid = UnityEngine.Object.FindFirstObjectByType<GridManager>();
            Assert(grid != null, "씬 그리드 존재");
            walls.Clear();
            for (int y = 0; y < grid.Height; y++)
            for (int x = 0; x < grid.Width; x++)
                if (grid.IsSightBlocked(new GridPosition(x, y))) walls.Add(new GridPosition(x, y));
            int comparisons = 0;
            foreach (GridPosition origin in new[] { new GridPosition(8, 5), new GridPosition(15, 10), new GridPosition(31, 14) })
            {
                var probe = new PlayerVisionSource(origin, 50, false);
                var real = Snapshot(new[] { probe }, walls);
                for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                {
                    Assert(GridLineOfSight.HasLineOfSight(grid, origin, new GridPosition(x, y)) ==
                        PlayerVisionFogMask.HasLineOfSight(real, probe, x, y), $"칸 중심 LOS 일치 {origin}→{x},{y}");
                    comparisons++;
                }
            }
            report.AppendLine($"현재 씬 칸 중심 LOS 비교 {comparisons}건 통과");
            var realMask = new PlayerVisionFogMask(grid.Width, grid.Height, 12, grid.CellSize, 0.35f);
            var realColors = new Color32[realMask.Width * realMask.Height];
            var realSnapshot = Snapshot(new[] { new PlayerVisionSource(new GridPosition(8, 5), 6, true) }, walls);
            timer.Restart();
            realMask.BuildColors(realSnapshot, Color.black, remembered, Color.clear, realColors);
            timer.Stop();
            SaveMask(realMask, realColors, "scene-mask.png");
            report.AppendLine($"현재 {grid.Width}×{grid.Height} 보드 계산 {timer.Elapsed.TotalMilliseconds:F2}ms");
            report.AppendLine("PASS");
            File.WriteAllText(Output + "/result.txt", report.ToString());
            Debug.Log("부드러운 시야 검증 통과: " + report);
        }
        catch (Exception error)
        {
            File.WriteAllText(Output + "/result.txt", report + "\nFAIL\n" + error);
            Debug.LogError("부드러운 시야 검증 실패: " + error);
        }
    }

    /// <summary>합성 시야 스냅샷을 만든다. 실제 씬 상태를 변경하지 않는다.</summary>
    private static PlayerVisionSnapshot Snapshot(IEnumerable<PlayerVisionSource> sources, IEnumerable<GridPosition> blockers)
        => new(Array.Empty<GridPosition>(), Array.Empty<GridPosition>(), Array.Empty<GridActor>(), sources, blockers);

    /// <summary>정수 칸 중심 근처의 마스크 픽셀을 읽는다.</summary>
    private static Color32 Pixel(Color32[] colors, int width, int x, int y)
        => colors[((y + 1) * 12 + 6) * width + (x + 1) * 12 + 6];

    /// <summary>실패 조건을 검사 이름과 함께 알린다.</summary>
    private static void Assert(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException(name);
    }

    /// <summary>마스크를 사람이 확인할 수 있는 PNG로 저장하고 임시 텍스처를 해제한다.</summary>
    private static void SaveMask(PlayerVisionFogMask mask, Color32[] colors, string name)
    {
        var texture = new Texture2D(mask.Width, mask.Height, TextureFormat.RGBA32, false);
        texture.SetPixels32(colors);
        texture.Apply();
        File.WriteAllBytes(Output + "/" + name, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
    }

    /// <summary>플레이 중 화면과 현재 시야 리소스 정보를 기록한다. 씬·참조·게임 상태는 수정하지 않는다.</summary>
    [MenuItem("Tools/S2/Capture Soft Vision")]
    public static void Capture()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("플레이 모드에서만 화면을 검수합니다.");
        Directory.CreateDirectory(Output);
        ScreenCapture.CaptureScreenshot(Output + "/play.png");
        var presenter = PlayerVisionPresenter.Instance;
        var grid = GridManager.Instance;
        var renderers = UnityEngine.Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);
        var report = new StringBuilder();
        report.AppendLine($"Presenter={presenter != null}, enabled={presenter != null && presenter.enabled}, snapshot={presenter != null && presenter.PresentedSnapshot != null}");
        report.AppendLine($"Grid={grid.Width}×{grid.Height}, size={grid.CellSize}");
        foreach (var renderer in renderers)
            if (renderer.name == "Soft Fog" || renderer.name == "Outside Fog")
                report.AppendLine($"{renderer.name}: bounds={renderer.bounds}, shader={renderer.sharedMaterial.shader.name}, order={renderer.sortingOrder}");
        File.WriteAllText(Output + "/play-state.txt", report.ToString());
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
    }
}
