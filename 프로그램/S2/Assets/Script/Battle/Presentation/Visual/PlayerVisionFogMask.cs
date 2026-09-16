using System;
using UnityEngine;

/// <summary>칸의 중심이 아닌 세부 표본에 시선을 보내 원형 시야와 벽 뒤의 쐐기형 그림자를 만든다.</summary>
public sealed class PlayerVisionFogMask
{
    // 보드 바깥까지 불투명 안개를 연결하기 위한 여백 칸 수다.
    public const int Padding = 1;
    // 마스크의 픽셀 크기다.
    public int Width { get; }
    public int Height { get; }
    // 보드 크기와 한 칸당 표본 수다.
    private readonly int boardWidth, boardHeight, samples;
    // 경계 흐림의 두 번 박스 필터 반경이다.
    private readonly int blurRadius;
    // 현재 시야, 누적 탐색과 필터 계산에 재사용하는 버퍼다.
    private readonly float[] current, explored, scratch, softCurrent, softExplored;

    /// <summary>보드와 월드 경계 폭에 맞춰 마스크와 재사용 버퍼를 준비한다.</summary>
    public PlayerVisionFogMask(int boardWidth, int boardHeight, int samples, float cellSize, float softness)
    {
        if (boardWidth <= 0 || boardHeight <= 0 || samples < 2 || cellSize <= 0f ||
            float.IsNaN(cellSize) || float.IsInfinity(cellSize) || softness <= 0f ||
            float.IsNaN(softness) || float.IsInfinity(softness))
            throw new ArgumentOutOfRangeException(nameof(softness), "안개 보드·해상도·셀 크기·경계 폭은 유효한 양수여야 합니다.");
        this.boardWidth = boardWidth;
        this.boardHeight = boardHeight;
        this.samples = samples;
        Width = checked((boardWidth + Padding * 2) * samples);
        Height = checked((boardHeight + Padding * 2) * samples);
        blurRadius = Mathf.Max(1, Mathf.CeilToInt(softness / cellSize * samples * 0.5f));
        current = new float[Width * Height];
        explored = new float[current.Length];
        scratch = new float[current.Length];
        softCurrent = new float[current.Length];
        softExplored = new float[current.Length];
    }

    /// <summary>스냅샷 시점의 연속 시야를 계산하고, 실제로 드러난 세부 영역만 탐색 이력에 누적한다.</summary>
    public void BuildColors(PlayerVisionSnapshot snapshot, Color unseen, Color remembered, Color visible, Color32[] output)
    {
        if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
        if (output == null || output.Length != current.Length) throw new ArgumentException("안개 색 버퍼 크기가 맞지 않습니다.");
        Array.Clear(current, 0, current.Length);
        foreach (PlayerVisionSource source in snapshot.Sources)
        {
            int minX = Mathf.Max(Padding * samples, Mathf.FloorToInt((source.Origin.x - source.Radius + Padding + 0.5f) * samples));
            int maxX = Mathf.Min((Padding + boardWidth) * samples - 1, Mathf.CeilToInt((source.Origin.x + source.Radius + Padding + 0.5f) * samples));
            int minY = Mathf.Max(Padding * samples, Mathf.FloorToInt((source.Origin.y - source.Radius + Padding + 0.5f) * samples));
            int maxY = Mathf.Min((Padding + boardHeight) * samples - 1, Mathf.CeilToInt((source.Origin.y + source.Radius + Padding + 0.5f) * samples));
            for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                int index = y * Width + x;
                if (current[index] > 0f) continue;
                float gridX = (x + 0.5f) / samples - Padding - 0.5f;
                float gridY = (y + 0.5f) / samples - Padding - 0.5f;
                float dx = gridX - source.Origin.x, dy = gridY - source.Origin.y;
                if (dx * dx + dy * dy > source.Radius * source.Radius) continue;
                if (HasLineOfSight(snapshot, source, gridX, gridY))
                    current[index] = explored[index] = 1f;
            }
        }

        Blur(current, softCurrent);
        Blur(explored, softExplored);
        for (int i = 0; i < output.Length; i++)
        {
            // 누적 탐색과 현재 시야를 별도로 혼합해 경계가 이동해도 미탐색 정보가 갑자기 열리지 않게 한다.
            float seen = Mathf.Clamp01(softExplored[i]);
            float now = Mathf.Min(seen, Mathf.Clamp01(softCurrent[i]));
            output[i] = unseen * (1f - seen) + remembered * (seen - now) + visible * now;
        }
    }

    /// <summary>연속 좌표까지 셀 경계를 따라 시선을 추적한다. 벽 끝에서 뒤로 퍼지는 그림자를 자연스럽게 만든다.</summary>
    public static bool HasLineOfSight(PlayerVisionSnapshot snapshot, PlayerVisionSource source, float targetX, float targetY)
    {
        int x = source.Origin.x, y = source.Origin.y;
        int endX = Mathf.FloorToInt(targetX + 0.5f), endY = Mathf.FloorToInt(targetY + 0.5f);
        if (source.GuaranteeAdjacent && Mathf.Max(Mathf.Abs(endX - x), Mathf.Abs(endY - y)) <= 1)
            return true;
        float dx = targetX - x, dy = targetY - y;
        int stepX = Math.Sign(dx), stepY = Math.Sign(dy);
        float deltaX = stepX == 0 ? float.PositiveInfinity : 1f / Mathf.Abs(dx);
        float deltaY = stepY == 0 ? float.PositiveInfinity : 1f / Mathf.Abs(dy);
        float nextX = 0.5f * deltaX, nextY = 0.5f * deltaY;
        int maxSteps = Mathf.Abs(endX - x) + Mathf.Abs(endY - y) + 1;
        for (int i = 0; i < maxSteps && (x != endX || y != endY); i++)
        {
            if (Mathf.Abs(nextX - nextY) < 0.000001f)
            {
                // 기존 LOS처럼 정확한 모서리는 양쪽이 모두 막혔을 때만 차단한다.
                if (snapshot.IsSightBlocked(new GridPosition(x + stepX, y)) &&
                    snapshot.IsSightBlocked(new GridPosition(x, y + stepY))) return false;
                x += stepX; y += stepY; nextX += deltaX; nextY += deltaY;
            }
            else if (nextX < nextY) { x += stepX; nextX += deltaX; }
            else { y += stepY; nextY += deltaY; }
            // 도착 칸의 벽 표면은 기존 판정처럼 관측할 수 있다.
            if ((x != endX || y != endY) && snapshot.IsSightBlocked(new GridPosition(x, y))) return false;
        }
        return x == endX && y == endY;
    }

    /// <summary>두 번의 분리형 박스 필터로 원과 그림자 경계를 완만하게 만든다.</summary>
    private void Blur(float[] input, float[] output)
    {
        BoxBlur(input, scratch, true);
        BoxBlur(scratch, output, false);
        BoxBlur(output, scratch, true);
        BoxBlur(scratch, output, false);
    }

    /// <summary>이동 합으로 필터 폭과 무관하게 선형 시간에 한 축을 흐린다. 보드 밖은 미탐색으로 취급한다.</summary>
    private void BoxBlur(float[] input, float[] output, bool horizontal)
    {
        int length = horizontal ? Width : Height;
        int lines = horizontal ? Height : Width;
        int stride = horizontal ? 1 : Width;
        float divisor = blurRadius * 2 + 1;
        for (int line = 0; line < lines; line++)
        {
            int start = horizontal ? line * Width : line;
            float sum = 0f;
            for (int p = 0; p <= blurRadius && p < length; p++) sum += input[start + p * stride];
            for (int p = 0; p < length; p++)
            {
                output[start + p * stride] = sum / divisor;
                int remove = p - blurRadius, add = p + blurRadius + 1;
                if (remove >= 0) sum -= input[start + remove * stride];
                if (add < length) sum += input[start + add * stride];
            }
        }
    }
}
