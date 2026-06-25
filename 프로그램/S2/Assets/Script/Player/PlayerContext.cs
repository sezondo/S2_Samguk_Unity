using UnityEngine;

/// <summary>
/// 플레이어 루트에 붙은 핵심 컴포넌트와 데이터를 모아 제공하는 참조 주머니다.
/// 정책 계산이나 상태 변경은 하지 않고, 다른 플레이어 컴포넌트가 필요한 참조를 꺼내 쓰게 한다.
/// </summary>
public class PlayerContext : MonoBehaviour
{
    [Header("Data")]
    // 플레이어 턴 행동에 사용하는 AP, 이동 같은 튜닝 데이터다.
    [SerializeField] private PlayerTurnData turnData;

    [Header("Core Components")]
    // 플레이어 원시 입력을 읽어 공개하는 입력 Reader다. 입력 기능을 쓰는 씬에서 연결한다.
    [SerializeField] private PlayerInputReader inputReader;
    // 플레이어가 보드에서 차지하는 칸과 실제 격자 이동을 관리하는 공용 말 컴포넌트다.
    [SerializeField] private GridActor gridActor;
    // 플레이어 턴 행동에서 소비하는 AP 컴포넌트다.
    [SerializeField] private ActionPoint actionPoint;
    // 플레이어의 마우스 기반 그리드 이동 행동 컴포넌트다.
    [SerializeField] private PlayerGridMoveAction gridMoveAction;
    // 플레이어 이동 경로의 적 시야 위험을 평가하고 경고 표시를 담당하는 컴포넌트다.
    [SerializeField] private GridMoveRiskEvaluator gridMoveRiskEvaluator;
    // 플레이어 이동 가능 범위의 AP 구간별 표시를 담당하는 컴포넌트다.
    [SerializeField] private GridMoveRangeHighlighter gridMoveRangeHighlighter;
    // 플레이어의 해킹 행동 판정과 실행을 담당하는 컴포넌트다. 해킹 기능을 쓰는 씬에서 연결한다.
    [SerializeField] private PlayerHackAction hackAction;
    // 도깨비 환도의 현재 기준 칸과 회수 상태를 보관하는 선택 컴포넌트다. 검 행동을 쓰는 씬에서 연결한다.
    [SerializeField] private PlayerSwordState swordState;
    // 도깨비 환도 투척 행동 판정과 실행을 담당하는 선택 컴포넌트다. 검 투척 기능을 쓰는 씬에서 연결한다.
    [SerializeField] private PlayerSwordThrowAction swordThrowAction;
    // 도깨비 환도 회수 행동 판정과 실행을 담당하는 선택 컴포넌트다. 검 회수 기능을 쓰는 씬에서 연결한다.
    [SerializeField] private PlayerSwordRecallAction swordRecallAction;

    public PlayerTurnData TurnData => turnData;
    public PlayerInputReader InputReader => inputReader;
    public GridActor GridActor => gridActor;
    public ActionPoint ActionPoint => actionPoint;
    public PlayerGridMoveAction GridMoveAction => gridMoveAction;
    public GridMoveRiskEvaluator GridMoveRiskEvaluator => gridMoveRiskEvaluator;
    public GridMoveRangeHighlighter GridMoveRangeHighlighter => gridMoveRangeHighlighter;
    public PlayerHackAction HackAction => hackAction;
    public PlayerSwordState SwordState => swordState;
    public PlayerSwordThrowAction SwordThrowAction => swordThrowAction;
    public PlayerSwordRecallAction SwordRecallAction => swordRecallAction;

    /// <summary>
    /// 플레이어 Context에 필수 참조가 모두 연결되어 있는지 확인한다.
    /// </summary>
    public bool HasValidReference()
    {
        if (turnData == null)
        {
            Debug.LogError($"{nameof(PlayerContext)} on {name}에는 {nameof(PlayerTurnData)} 참조가 필요합니다.", this);
            return false;
        }

        if (inputReader == null)
        {
            Debug.LogError($"{nameof(PlayerContext)} on {name}에는 {nameof(PlayerInputReader)} 참조가 필요합니다.", this);
            return false;
        }

        if (gridActor == null)
        {
            Debug.LogError($"{nameof(PlayerContext)} on {name}에는 {nameof(GridActor)} 참조가 필요합니다.", this);
            return false;
        }

        if (actionPoint == null)
        {
            Debug.LogError($"{nameof(PlayerContext)} on {name}에는 {nameof(ActionPoint)} 참조가 필요합니다.", this);
            return false;
        }

        if (gridMoveAction == null)
        {
            Debug.LogError($"{nameof(PlayerContext)} on {name}에는 {nameof(PlayerGridMoveAction)} 참조가 필요합니다.", this);
            return false;
        }

        if (gridMoveRiskEvaluator == null)
        {
            Debug.LogError($"{nameof(PlayerContext)} on {name}에는 {nameof(GridMoveRiskEvaluator)} 참조가 필요합니다.", this);
            return false;
        }

        if (gridMoveRangeHighlighter == null)
        {
            Debug.LogError($"{nameof(PlayerContext)} on {name}에는 {nameof(GridMoveRangeHighlighter)} 참조가 필요합니다.", this);
            return false;
        }

        return true;
    }
}
