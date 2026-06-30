using UnityEngine;

/// <summary>
/// 적 전술 위치 평가에 사용하는 점수 가중치 묶음이다.
/// </summary>
[System.Serializable]
public struct EnemyTacticalPositionScoreSettings
{
    // 후보 칸이 플레이어와 같은 직선축에 있고 사이를 막는 벽이 없을 때 적용할 감점이다.
    public int frontalExposurePenalty;
    // 후보 칸이 현재 위치보다 플레이어에게 가까워질 때 거리 차이마다 적용할 감점이다.
    public int closerToPlayerPenalty;
    // 후보 칸 주변 벽이 적과 플레이어 사이를 실제로 막는다고 볼 수 있을 때 벽 1개마다 더하는 점수다.
    public int blockingCoverWallScore;
    // 후보 칸 주변 4방향에 인접한 벽 1개마다 더하는 기본 엄폐 점수다.
    public int adjacentWallScore;
    // 후보 칸의 엄폐 품질이 현재 위치보다 좋아졌을 때 더하는 점수다.
    public int improvedCoverScore;
    // 적 현재 위치에서 가까운 후보를 선호하기 위해 이동 거리마다 적용할 감점이다.
    public int moveDistancePenalty;

    /// <summary>
    /// 경계 반응 엄폐 이동에서 사용할 기본 점수 설정을 만든다.
    /// </summary>
    public static EnemyTacticalPositionScoreSettings CreateDefaultCoverReactionSettings()
    {
        return new EnemyTacticalPositionScoreSettings
        {
            frontalExposurePenalty = 60,
            closerToPlayerPenalty = 20,
            blockingCoverWallScore = 45,
            adjacentWallScore = 8,
            improvedCoverScore = 25,
            moveDistancePenalty = 3,
        };
    }
}

/// <summary>
/// 적 전술 위치 후보 하나의 점수와 점수 산정 근거다.
/// </summary>
public readonly struct EnemyTacticalPositionScoreResult
{
    public GridPosition Position { get; }
    public int Score { get; }
    public int MoveDistance { get; }
    public int DistanceToPlayer { get; }
    public int CurrentCoverQuality { get; }
    public int CandidateCoverQuality { get; }
    public int AdjacentWallCount { get; }
    public int BlockingWallCount { get; }
    public bool IsExposedToPlayer { get; }

    /// <summary>
    /// 지정한 값으로 전술 위치 평가 결과를 만든다.
    /// </summary>
    public EnemyTacticalPositionScoreResult(
        GridPosition position,
        int score,
        int moveDistance,
        int distanceToPlayer,
        int currentCoverQuality,
        int candidateCoverQuality,
        int adjacentWallCount,
        int blockingWallCount,
        bool isExposedToPlayer)
    {
        Position = position;
        Score = score;
        MoveDistance = moveDistance;
        DistanceToPlayer = distanceToPlayer;
        CurrentCoverQuality = currentCoverQuality;
        CandidateCoverQuality = candidateCoverQuality;
        AdjacentWallCount = adjacentWallCount;
        BlockingWallCount = blockingWallCount;
        IsExposedToPlayer = isExposedToPlayer;
    }

    /// <summary>
    /// Unity 콘솔에서 후보 점수 내역을 읽기 좋은 문자열로 반환한다.
    /// </summary>
    public override string ToString()
    {
        return $"{Position} 점수 {Score}. 이동:{MoveDistance}, 플레이어거리:{DistanceToPlayer}, " +
            $"차단벽:{BlockingWallCount}, 인접벽:{AdjacentWallCount}, 정면노출:{IsExposedToPlayer}, " +
            $"현재엄폐품질:{CurrentCoverQuality}, 후보엄폐품질:{CandidateCoverQuality}";
    }
}

/// <summary>
/// 격자 기반 적 전술 위치 후보의 엄폐 품질과 위험 점수를 계산한다.
/// </summary>
public static class EnemyTacticalPositionScorer
{
    // 엄폐 판정에 사용하는 상하좌우 4방향이다.
    private static readonly GridPosition[] CardinalDirections =
    {
        GridPosition.Up,
        GridPosition.Down,
        GridPosition.Left,
        GridPosition.Right,
    };

    /// <summary>
    /// 경계 반응 엄폐 이동 후보 칸의 전술 점수를 계산한다.
    /// </summary>
    public static EnemyTacticalPositionScoreResult ScoreCoverReactionPosition(
        GridManager gridManager,
        GridPosition startPosition,
        GridPosition candidatePosition,
        GridPosition knownPlayerPosition,
        int moveDistance,
        EnemyTacticalPositionScoreSettings settings)
    {
        int startDistanceToPlayer = startPosition.ManhattanDistanceTo(knownPlayerPosition);
        int candidateDistanceToPlayer = candidatePosition.ManhattanDistanceTo(knownPlayerPosition);
        CoverInfo currentCover = EvaluateCover(gridManager, startPosition, knownPlayerPosition);
        CoverInfo candidateCover = EvaluateCover(gridManager, candidatePosition, knownPlayerPosition);

        int score = 0;
        if (candidateCover.IsExposedToPlayer)
        {
            score -= settings.frontalExposurePenalty;
        }

        if (candidateDistanceToPlayer < startDistanceToPlayer)
        {
            score -= (startDistanceToPlayer - candidateDistanceToPlayer) * settings.closerToPlayerPenalty;
        }

        score += candidateCover.BlockingWallCount * settings.blockingCoverWallScore;
        score += candidateCover.AdjacentWallCount * settings.adjacentWallScore;

        if (candidateCover.Quality > currentCover.Quality)
        {
            score += settings.improvedCoverScore;
        }

        score -= moveDistance * settings.moveDistancePenalty;

        return new EnemyTacticalPositionScoreResult(
            candidatePosition,
            score,
            moveDistance,
            candidateDistanceToPlayer,
            currentCover.Quality,
            candidateCover.Quality,
            candidateCover.AdjacentWallCount,
            candidateCover.BlockingWallCount,
            candidateCover.IsExposedToPlayer);
    }

    /// <summary>
    /// 지정한 칸이 엄폐 후보로 사용할 수 있는지 확인한다.
    /// </summary>
    public static bool IsCoverCandidate(GridManager gridManager, GridPosition position, GridPosition playerPosition)
    {
        return EvaluateCover(gridManager, position, playerPosition).AdjacentWallCount > 0;
    }

    /// <summary>
    /// 지정한 칸의 엄폐 정보를 계산한다.
    /// </summary>
    private static CoverInfo EvaluateCover(GridManager gridManager, GridPosition position, GridPosition playerPosition)
    {
        int adjacentWallCount = 0;
        int blockingWallCount = 0;
        int positionDistanceToPlayer = position.ManhattanDistanceTo(playerPosition);

        for (int i = 0; i < CardinalDirections.Length; i++)
        {
            GridPosition wallPosition = position + CardinalDirections[i];
            if (!gridManager.IsBlocked(wallPosition))
            {
                continue;
            }

            adjacentWallCount++;
            if (wallPosition.ManhattanDistanceTo(playerPosition) < positionDistanceToPlayer)
            {
                blockingWallCount++;
            }
        }

        bool isExposedToPlayer = HasOpenStraightLineToPlayer(gridManager, position, playerPosition);
        return new CoverInfo(adjacentWallCount, blockingWallCount, isExposedToPlayer);
    }

    /// <summary>
    /// 플레이어와 후보 칸이 같은 직선축에 있고 사이에 벽이 없는지 확인한다.
    /// </summary>
    private static bool HasOpenStraightLineToPlayer(GridManager gridManager, GridPosition position, GridPosition playerPosition)
    {
        if (position.x != playerPosition.x && position.y != playerPosition.y)
        {
            return false;
        }

        GridPosition direction = GridPosition.Zero;
        if (position.x == playerPosition.x)
        {
            direction = playerPosition.y > position.y ? GridPosition.Up : GridPosition.Down;
        }
        else if (position.y == playerPosition.y)
        {
            direction = playerPosition.x > position.x ? GridPosition.Right : GridPosition.Left;
        }

        GridPosition current = position + direction;
        while (current != playerPosition)
        {
            if (gridManager.IsBlocked(current))
            {
                return false;
            }

            current += direction;
        }

        return true;
    }

    /// <summary>
    /// 엄폐 후보의 품질 정보를 담는다.
    /// </summary>
    private readonly struct CoverInfo
    {
        public int AdjacentWallCount { get; }
        public int BlockingWallCount { get; }
        public bool IsExposedToPlayer { get; }
        public int Quality => BlockingWallCount * 2 + AdjacentWallCount - (IsExposedToPlayer ? 1 : 0);

        /// <summary>
        /// 지정한 값으로 엄폐 품질 정보를 만든다.
        /// </summary>
        public CoverInfo(int adjacentWallCount, int blockingWallCount, bool isExposedToPlayer)
        {
            AdjacentWallCount = adjacentWallCount;
            BlockingWallCount = blockingWallCount;
            IsExposedToPlayer = isExposedToPlayer;
        }
    }
}
