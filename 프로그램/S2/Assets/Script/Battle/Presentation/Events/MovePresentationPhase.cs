/// <summary>
/// 한 칸 단위 이동 연출이 전체 이동 경로에서 어느 단계인지 나타낸다.
/// </summary>
public enum MovePresentationPhase
{
    None,
    Single,
    Start,
    Continue,
    End,
}

/// <summary>
/// 이동 경로의 칸 인덱스를 연출 단계로 변환한다.
/// </summary>
public static class MovePresentationPhaseUtility
{
    /// <summary>
    /// 전체 경로 길이와 현재 칸 인덱스를 기준으로 이동 연출 단계를 반환한다.
    /// </summary>
    public static MovePresentationPhase GetPhase(int stepIndex, int stepCount)
    {
        if (stepCount <= 1)
        {
            return MovePresentationPhase.Single;
        }

        if (stepIndex <= 0)
        {
            return MovePresentationPhase.Start;
        }

        if (stepIndex >= stepCount - 1)
        {
            return MovePresentationPhase.End;
        }

        return MovePresentationPhase.Continue;
    }
}
