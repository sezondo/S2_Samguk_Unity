using UnityEngine;

/// <summary>튜토리얼 기능들이 공유하는 명시적 씬 참조 주머니다.</summary>
public sealed class BattleTutorialContext : MonoBehaviour
{
    // 기존 게임 규칙을 실행할 보드와 플레이어다.
    public GridManager grid;
    public TacticalUnitContext player;
    // 통제된 순찰·첫 전투·두 번째 전투를 담당하는 적들이다.
    public EnemyContext patrol;
    public EnemyContext bigNail;
    public EnemyContext hammer;
    // 문 개방과 최종 승리를 확인할 기존 대상들이다.
    public HackableObject terminal;
    public SecurityDoorController door;
    public StageGoal goal;
    // 턴·게임 진행·연출과 행동 요청을 제공한다.
    public TurnManager turns;
    public StageStateManager stage;
    public ActionPresentationQueue queue;
    public PlayerUnitControlManager control;
    public PlayerUnitActionFlowController actions;
    // 카메라·말풍선·튜토리얼 화면 표시를 제공한다.
    public Camera worldCamera;
    public DialogueManager dialogue;
    public DialogueSpeaker[] speakers;
    public BattleTutorialPresenter presenter;
}
