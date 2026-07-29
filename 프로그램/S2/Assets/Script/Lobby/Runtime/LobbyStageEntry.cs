/// <summary>
/// Lobby 화면에 표시할 스테이지 정의와 현재 저장 진행 상태를 함께 보관한다.
/// </summary>
public sealed class LobbyStageEntry
{
    // 변경되지 않는 스테이지 설계 데이터다.
    public StageDefinitionData Stage { get; }
    // Lobby를 구성한 시점에 저장 파일에서 읽은 진행 상태다.
    public StageProgressState ProgressState { get; }

    /// <summary>
    /// 지정한 스테이지와 저장 상태로 Lobby 표시 항목을 만든다.
    /// </summary>
    public LobbyStageEntry(StageDefinitionData stage, StageProgressState progressState)
    {
        Stage = stage;
        ProgressState = progressState;
    }
}
