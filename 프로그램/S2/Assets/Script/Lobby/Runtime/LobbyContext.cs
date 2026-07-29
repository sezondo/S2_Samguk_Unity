using UnityEngine;

/// <summary>
/// Lobby 씬의 스테이지 목록과 상세 화면 참조를 모아 제공하는 참조 주머니다.
/// </summary>
public class LobbyContext : MonoBehaviour
{
    [Header("View")]
    // 캠페인 스테이지 버튼 목록을 생성하고 선택 표시를 관리하는 View다.
    [SerializeField] private LobbyStageListView stageListView;
    // 현재 선택한 스테이지의 상세 정보와 선택 확정 버튼을 관리하는 View다.
    [SerializeField] private LobbyStageDetailView stageDetailView;

    public LobbyStageListView StageListView => stageListView;
    public LobbyStageDetailView StageDetailView => stageDetailView;

    /// <summary>
    /// Lobby 화면 구성에 필요한 필수 View 참조가 연결되어 있는지 확인한다.
    /// </summary>
    public bool HasValidReference()
    {
        bool isValid = true;

        if (stageListView == null)
        {
            Debug.LogError($"{nameof(LobbyContext)} on {name}에는 {nameof(LobbyStageListView)} 참조가 필요합니다.", this);
            isValid = false;
        }

        if (stageDetailView == null)
        {
            Debug.LogError($"{nameof(LobbyContext)} on {name}에는 {nameof(LobbyStageDetailView)} 참조가 필요합니다.", this);
            isValid = false;
        }

        return isValid;
    }
}
