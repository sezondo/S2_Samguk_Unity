using UnityEngine;

/// <summary>
/// 적이 현재 조사하는 이상 현상과 조사 진행 정보를 보관한다.
/// </summary>
public readonly struct EnemySuspicionInfo
{
    public GridPosition Position { get; }
    public SuspicionSource Source { get; }
    public EnemyContext Detector { get; }
    public int DetectedTurnIndex { get; }
    public Object SourceObject { get; }
    public EnemySuspicionRole Role { get; }

    /// <summary>
    /// 지정한 이상 현상을 조사하기 위한 런타임 정보를 만든다.
    /// </summary>
    public EnemySuspicionInfo(
        GridPosition position,
        SuspicionSource source,
        EnemyContext detector,
        int detectedTurnIndex,
        Object sourceObject,
        EnemySuspicionRole role)
    {
        Position = position;
        Source = source;
        Detector = detector;
        DetectedTurnIndex = detectedTurnIndex;
        SourceObject = sourceObject;
        Role = role;
    }
}
