/// <summary>
/// 캠페인 전체 흐름이 현재 머무는 단계다.
/// </summary>
public enum CampaignFlowPhase
{
    Bootstrapping,
    LoadingLobby,
    Lobby,
    LoadingStory,
    Story,
    LoadingBattle,
    Battle,
}
