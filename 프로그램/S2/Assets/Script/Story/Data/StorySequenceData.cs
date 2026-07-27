using UnityEngine;

/// <summary>
/// 하나의 선형 Story를 식별하고 순서대로 실행할 명령 목록을 보관한다.
/// </summary>
[CreateAssetMenu(menuName = "Scriptable/Story/Story Sequence", fileName = "StorySequence")]
public class StorySequenceData : ScriptableObject
{
    [Header("Identity")]
    // 저장과 캠페인 요청에서 사용하는 변경되지 않는 Story ID다.
    [SerializeField] private string storyId;
    // 로비와 디버그 화면에서 확인할 Story 표시 이름이다.
    [SerializeField] private string displayName;

    [Header("Commands")]
    // 분기 없이 배열 순서대로 실행할 전체 Story 명령 목록이다.
    [SerializeField] private StoryCommandData[] commands;

    public string StoryId => storyId;
    public string DisplayName => displayName;
    public StoryCommandData[] Commands => commands;
}
