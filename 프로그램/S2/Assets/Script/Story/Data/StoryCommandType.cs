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
    // 대사 없이 장면을 유지할 때 현재 대화창을 숨긴다. 기존 명령 번호는 유지한다.
    HideDialogue,
    // 고정 만화 페이지에 지정 컷을 추가 공개한 뒤 대사를 진행한다.
    ShowComicCut,
}
