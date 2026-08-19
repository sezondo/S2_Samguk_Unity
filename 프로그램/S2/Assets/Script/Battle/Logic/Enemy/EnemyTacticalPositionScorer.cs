using UnityEngine;

/// <summary>
/// 적 전술 위치 평가에 사용하는 점수 가중치와 공통 엄폐 각도 규칙이다.
/// </summary>
[System.Serializable]
public struct EnemyTacticalPositionScoreSettings
{
    // 낮은 엄폐 효과가 100%일 때 후보 칸에 더하는 최대 점수다.
    public int maximumCoverEffectScore;
    // 후보 칸의 실제 엄폐 효과가 현재 위치보다 좋아졌을 때 더하는 점수다.
    public int improvedCoverScore;
    // 후보 칸이 현재 위치보다 플레이어에게 가까워질 때 거리 차이마다 적용할 감점이다.
    public int closerToPlayerPenalty;
    // 적 현재 위치에서 가까운 후보를 선호하기 위해 이동 거리마다 적용할 감점이다.
    public int moveDistancePenalty;
    // 이 각도 이하에서는 낮은 엄폐 효과를 전부 적용한다.
    public float fullEffectMaximumAngle;
    // 이 각도 이상에서는 측면 엄폐로 보고 엄폐 효과를 적용하지 않는다.
    public float flankMinimumAngle;

    // 새 엄폐도 점수 필드가 직렬화 데이터에 초기화됐는지 나타낸다.
    public bool IsInitialized =>
        maximumCoverEffectScore > 0 &&
        flankMinimumAngle > 0f;

    /// <summary>
    /// 경계 반응과 적 턴 엄폐 이동에서 사용할 기본 점수 설정을 만든다.
    /// </summary>
    public static EnemyTacticalPositionScoreSettings CreateDefaultCoverReactionSettings()
    {
        return new EnemyTacticalPositionScoreSettings
        {
            maximumCoverEffectScore = 60,
            improvedCoverScore = 25,
            closerToPlayerPenalty = 20,
            moveDistancePenalty = 3,
            fullEffectMaximumAngle = CoverCalculator.DefaultFullEffectMaximumAngle,
            flankMinimumAngle = CoverCalculator.DefaultFlankMinimumAngle,
        };
    }

    /// <summary>
    /// 점수와 엄폐 각도 설정이 계산에 사용할 수 있는 범위인지 확인한다.
    /// </summary>
    public bool HasValidData()
    {
        return maximumCoverEffectScore > 0 &&
            improvedCoverScore >= 0 &&
            closerToPlayerPenalty >= 0 &&
            moveDistancePenalty >= 0 &&
            fullEffectMaximumAngle >= 0f &&
            flankMinimumAngle <= 180f &&
            fullEffectMaximumAngle < flankMinimumAngle;
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
    public CoverResult CurrentCover { get; }
    public CoverResult CandidateCover { get; }
    public int CoverEffectScore { get; }
    // 후보 칸이 공격 방향에서 실제 낮은 엄폐 효과를 받는지 나타낸다.
    public bool HasEffectiveCover => CandidateCover.Type != CoverType.None && CandidateCover.Effectiveness > 0f;

    /// <summary>
    /// 지정한 값으로 전술 위치 평가 결과를 만든다.
    /// </summary>
    public EnemyTacticalPositionScoreResult(
        GridPosition position,
        int score,
        int moveDistance,
        int distanceToPlayer,
        CoverResult currentCover,
        CoverResult candidateCover,
        int coverEffectScore)
    {
        Position = position;
        Score = score;
        MoveDistance = moveDistance;
        DistanceToPlayer = distanceToPlayer;
        CurrentCover = currentCover;
        CandidateCover = candidateCover;
        CoverEffectScore = coverEffectScore;
    }

    /// <summary>
    /// Unity 콘솔에서 후보 점수 내역을 읽기 좋은 문자열로 반환한다.
    /// </summary>
    public override string ToString()
    {
        return $"{Position} 점수 {Score}. 이동:{MoveDistance}, 플레이어거리:{DistanceToPlayer}, " +
            $"엄폐방향:{CandidateCover.CoverDirection}, 엄폐각도:{CandidateCover.Angle:F1}, " +
            $"현재엄폐도:{CurrentCover.Effectiveness:F2}, 후보엄폐도:{CandidateCover.Effectiveness:F2}, " +
            $"엄폐점수:{CoverEffectScore}";
    }
}

/// <summary>
/// 실제 원거리 공격과 같은 방향·각도 규칙으로 적 전술 위치의 엄폐 효과를 점수화한다.
/// </summary>
public static class EnemyTacticalPositionScorer
{
    // 엄폐 유무와 효과 비율만 계산할 때 사용하는 기준 페널티다.
    private const int CoverEvaluationPenalty = 100;

    /// <summary>
    /// 경계 반응과 적 턴 이동 후보 칸의 전술 점수를 계산한다.
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
        CoverResult currentCover = EvaluateCover(gridManager, startPosition, knownPlayerPosition, settings);
        CoverResult candidateCover = EvaluateCover(gridManager, candidatePosition, knownPlayerPosition, settings);
        int coverEffectScore = Mathf.RoundToInt(candidateCover.Effectiveness * settings.maximumCoverEffectScore);

        int score = coverEffectScore;
        if (candidateCover.Effectiveness > currentCover.Effectiveness + Mathf.Epsilon)
        {
            score += settings.improvedCoverScore;
        }

        if (candidateDistanceToPlayer < startDistanceToPlayer)
        {
            score -= (startDistanceToPlayer - candidateDistanceToPlayer) * settings.closerToPlayerPenalty;
        }

        score -= moveDistance * settings.moveDistancePenalty;

        return new EnemyTacticalPositionScoreResult(
            candidatePosition,
            score,
            moveDistance,
            candidateDistanceToPlayer,
            currentCover,
            candidateCover,
            coverEffectScore);
    }

    /// <summary>
    /// 지정한 칸이 공격 방향에서 실제 낮은 엄폐 효과를 받는 후보인지 확인한다.
    /// </summary>
    public static bool IsCoverCandidate(
        GridManager gridManager,
        GridPosition position,
        GridPosition playerPosition,
        float fullEffectMaximumAngle = CoverCalculator.DefaultFullEffectMaximumAngle,
        float flankMinimumAngle = CoverCalculator.DefaultFlankMinimumAngle)
    {
        CoverResult cover = EvaluateCover(
            gridManager,
            position,
            playerPosition,
            fullEffectMaximumAngle,
            flankMinimumAngle);
        return cover.Type != CoverType.None && cover.Effectiveness > 0f;
    }

    /// <summary>
    /// 플레이어를 공격자로 보고 지정한 적 위치가 받는 낮은 엄폐 결과를 계산한다.
    /// </summary>
    private static CoverResult EvaluateCover(
        GridManager gridManager,
        GridPosition enemyPosition,
        GridPosition playerPosition,
        EnemyTacticalPositionScoreSettings settings)
    {
        return EvaluateCover(
            gridManager,
            enemyPosition,
            playerPosition,
            settings.fullEffectMaximumAngle,
            settings.flankMinimumAngle);
    }

    /// <summary>
    /// 지정한 공통 각도 규칙으로 적 위치의 낮은 엄폐 결과를 계산한다.
    /// </summary>
    private static CoverResult EvaluateCover(
        GridManager gridManager,
        GridPosition enemyPosition,
        GridPosition playerPosition,
        float fullEffectMaximumAngle,
        float flankMinimumAngle)
    {
        return CoverCalculator.Calculate(
            gridManager,
            playerPosition,
            enemyPosition,
            CoverEvaluationPenalty,
            fullEffectMaximumAngle,
            flankMinimumAngle);
    }
}
