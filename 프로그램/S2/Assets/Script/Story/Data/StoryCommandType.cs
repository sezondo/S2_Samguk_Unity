/// <summary>
/// 선형 Story 시퀀스에서 순서대로 실행할 명령 종류다.
/// </summary>
public enum StoryCommandType
{
    Dialogue,
    ChangeBackground,
    ShowStanding,
    HideStanding,
    ChangeStandingExpression,
    ChangeStandingFocus,
    ShowComicPanel,
    HideComicPanel,
    Fade,
    Wait,
}
