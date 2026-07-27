/// <summary>
/// Story Runner가 현재 수행 중인 재생 단계다.
/// </summary>
public enum StoryPlaybackState
{
    Idle,
    Executing,
    WaitingForTimedCommand,
    WaitingForDialogueInput,
    WaitingForComicInput,
    Completed,
}
