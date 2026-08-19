using System;
using System.Collections.Generic;

/// <summary>
/// 적이 이동 가능한 전술 후보 칸을 찾고 목표 칸까지의 경로를 계산한다.
/// </summary>
public sealed class EnemyTacticalMovePlanner
{
    // BFS 결과 거리 버퍼다.
    private readonly Dictionary<GridPosition, int> reachableDistances = new();

    /// <summary>
    /// 경계 반응에 사용할 최적 엄폐 이동 경로를 찾는다.
    /// </summary>
    public bool TryFindBestCoverReactionPath(
        GridManager gridManager,
        GridPosition startPosition,
        GridPosition knownPlayerPosition,
        int moveRange,
        EnemyTacticalPositionScoreSettings scoreSettings,
        List<GridPosition> path,
        List<EnemyTacticalPositionScoreResult> scoreResults,
        out GridPosition targetPosition)
    {
        return TryFindBestCoverPath(
            gridManager,
            startPosition,
            knownPlayerPosition,
            moveRange,
            scoreSettings,
            path,
            scoreResults,
            out targetPosition,
            null,
            false);
    }

    /// <summary>
    /// 이동 가능 범위 안에서 조건을 만족하는 최적 엄폐 위치와 경로를 찾는다.
    /// 현재 위치를 후보에 포함하면 최선 위치가 현재 위치일 때 빈 경로를 반환한다.
    /// </summary>
    public bool TryFindBestCoverPath(
        GridManager gridManager,
        GridPosition startPosition,
        GridPosition knownPlayerPosition,
        int moveRange,
        EnemyTacticalPositionScoreSettings scoreSettings,
        List<GridPosition> path,
        List<EnemyTacticalPositionScoreResult> scoreResults,
        out GridPosition targetPosition,
        Func<GridPosition, bool> candidateFilter = null,
        bool includeStartPosition = true)
    {
        path.Clear();
        scoreResults?.Clear();
        targetPosition = GridPosition.Zero;

        if (gridManager == null || moveRange < 0)
        {
            return false;
        }

        reachableDistances.Clear();
        if (moveRange > 0)
        {
            GridPathfinder.FindReachablePositionDistances(gridManager, startPosition, moveRange, reachableDistances);
        }

        if (includeStartPosition && gridManager.IsInside(startPosition))
        {
            reachableDistances[startPosition] = 0;
        }

        EnemyTacticalPositionScoreResult bestResult = default;
        bool found = false;
        foreach (KeyValuePair<GridPosition, int> pair in reachableDistances)
        {
            GridPosition candidate = pair.Key;
            if ((!includeStartPosition && candidate == startPosition) ||
                (candidateFilter != null && !candidateFilter(candidate)))
            {
                continue;
            }

            EnemyTacticalPositionScoreResult result = EnemyTacticalPositionScorer.ScoreCoverReactionPosition(
                gridManager,
                startPosition,
                candidate,
                knownPlayerPosition,
                pair.Value,
                scoreSettings);

            if (!result.HasEffectiveCover)
            {
                continue;
            }

            scoreResults?.Add(result);

            if (!found || IsBetterScoredCandidate(result, bestResult))
            {
                found = true;
                bestResult = result;
                targetPosition = candidate;
            }
        }

        if (!found)
        {
            return false;
        }

        if (targetPosition == startPosition)
        {
            path.Clear();
            return true;
        }

        return GridPathfinder.TryFindPath(gridManager, startPosition, targetPosition, moveRange, path);
    }

    /// <summary>
    /// 새 점수 후보가 현재 최선 후보보다 나은지 비교한다.
    /// </summary>
    private static bool IsBetterScoredCandidate(EnemyTacticalPositionScoreResult candidate, EnemyTacticalPositionScoreResult best)
    {
        if (candidate.Score != best.Score)
        {
            return candidate.Score > best.Score;
        }

        if (candidate.MoveDistance != best.MoveDistance)
        {
            return candidate.MoveDistance < best.MoveDistance;
        }

        if (candidate.DistanceToPlayer != best.DistanceToPlayer)
        {
            return candidate.DistanceToPlayer > best.DistanceToPlayer;
        }

        return IsEarlierStablePosition(candidate.Position, best.Position);
    }

    /// <summary>
    /// 모든 전술 기준이 같을 때 Dictionary 순회 순서와 무관하게 같은 칸을 고른다.
    /// </summary>
    private static bool IsEarlierStablePosition(GridPosition candidate, GridPosition bestPosition)
    {
        if (candidate.x != bestPosition.x)
        {
            return candidate.x < bestPosition.x;
        }

        return candidate.y < bestPosition.y;
    }
}
