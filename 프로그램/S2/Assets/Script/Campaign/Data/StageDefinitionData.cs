using UnityEngine;

/// <summary>
/// 선형 캠페인에서 스테이지 하나를 식별하고 해당 전투 씬을 연결하는 데이터 에셋이다.
/// </summary>
[CreateAssetMenu(menuName = "Scriptable/Campaign/Stage Definition", fileName = "StageDefinition")]
public class StageDefinitionData : ScriptableObject
{
    [Header("Identity")]
    // 저장 데이터와 런타임 조회에 사용하는 변경되지 않는 스테이지 ID다.
    [SerializeField] private string stageId = "1-1";
    // 화면 분류와 정렬 확인에 사용하는 챕터 번호다.
    [SerializeField] private int chapterNumber = 1;
    // 챕터 안에서 사용하는 스테이지 번호다.
    [SerializeField] private int stageNumber = 1;
    // 로비에서 보여 줄 스테이지 이름이다.
    [SerializeField] private string displayName = "1-1";

    [Header("Scene")]
    // 이 스테이지가 진입할 전투 씬 이름이다.
    [SerializeField] private string battleSceneName = "BattleTest";

    [Header("Story")]
    // 로비에서 스테이지를 시작한 뒤 전투 진입 전에 재생할 Story 데이터다.
    [SerializeField] private StorySequenceData preBattleStory;
    // 전투 승리 뒤 후일담 스토리를 재생해야 하는지 나타낸다.
    [SerializeField] private bool hasPostBattleStory;
    // 후일담 사용이 켜졌을 때 전투 승리 뒤 재생할 Story 데이터다.
    [SerializeField] private StorySequenceData postBattleStory;

    public string StageId => stageId;
    public int ChapterNumber => chapterNumber;
    public int StageNumber => stageNumber;
    public string DisplayName => displayName;
    public string BattleSceneName => battleSceneName;
    public StorySequenceData PreBattleStory => preBattleStory;
    public bool HasPostBattleStory => hasPostBattleStory;
    public StorySequenceData PostBattleStory => postBattleStory;
}
