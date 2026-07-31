using UnityEngine;

/// <summary>
/// 캠페인의 공통 씬과 선형 스테이지 순서를 보관하는 데이터 에셋이다.
/// </summary>
[CreateAssetMenu(menuName = "Scriptable/Campaign/Campaign Data", fileName = "CampaignData")]
public class CampaignData : ScriptableObject
{
    [Header("Scene")]
    // Bootstrap 초기화가 끝난 뒤 진입할 메인 로비 씬 이름이다.
    [SerializeField] private string lobbySceneName = "LobbyTest";
    // 전투 전·후 Story 데이터를 재생할 공통 Story 씬 이름이다.
    [SerializeField] private string storySceneName = "StoryTest";

    [Header("Stage")]
    // 해금 순서대로 정렬된 전체 스테이지 정의 목록이다.
    [SerializeField] private StageDefinitionData[] stages;

    public string LobbySceneName => lobbySceneName;
    public string StorySceneName => storySceneName;
    public StageDefinitionData[] Stages => stages;

    /// <summary>
    /// 지정한 ID와 일치하는 스테이지 정의를 찾는다.
    /// </summary>
    public bool TryGetStage(string stageId, out StageDefinitionData stage)
    {
        if (stages != null)
        {
            for (int i = 0; i < stages.Length; i++)
            {
                StageDefinitionData candidate = stages[i];
                if (candidate != null && candidate.StageId == stageId)
                {
                    stage = candidate;
                    return true;
                }
            }
        }

        stage = null;
        return false;
    }
}
