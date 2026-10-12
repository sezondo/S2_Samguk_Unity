using System;
using UnityEngine;

/// <summary>튜토리얼 단계의 진행 방식을 구분한다.</summary>
public enum BattleTutorialStepKind { Dialogue, Guide, Action, EndTurn, Camera, FreePlay }

/// <summary>한 단계에서 허용할 행동과 대사·안내를 보관한다.</summary>
[Serializable]
public sealed class BattleTutorialStep
{
    // 문서와 실행 중 단계를 대조하는 고유 이름이다.
    public string id;
    // 이 단계의 진행 방식이다.
    public BattleTutorialStepKind kind;
    // 대사 또는 안내 화면의 본문이다.
    [TextArea(2, 8)] public string text;
    // 대사의 발화자 태그다. 유진·금두꺼비·큰못을 사용한다.
    public string speaker;
    // 행동 단계에서 유일하게 허용하는 행동이다.
    public BattleHudActionType action;
    // 이동·공격·투척·해킹 또는 카메라가 가리키는 칸이다.
    public GridPosition target;
    // 적 턴의 순찰·회피·접근 공격을 선택하는 튜토리얼 플레이어 턴 번호다.
    public int turn;
    // 안내 단계에서 펼칠 수첩 페이지다. 0 이동, 1 시야, 2 전투, 3 검 정찰, 4 침입.
    [Range(0, 4)] public int manualPage;
}

/// <summary>1-1 튜토리얼의 수정 가능한 단계 배열과 통제된 적 행동 위치다.</summary>
[CreateAssetMenu(menuName = "S2/Battle/Tutorial", fileName = "Stage11_Tutorial")]
public sealed class BattleTutorialData : ScriptableObject
{
    // 대사와 필수 행동을 순서대로 실행하는 목록이다.
    public BattleTutorialStep[] steps;
    // 1턴에 순찰 적이 시야 밖으로 이동할 칸이다.
    public GridPosition patrolDestination;
    // 망치가 유진 옆으로 접근할 칸이다.
    public GridPosition hammerAttackPosition;
    // 확정 회피 공격을 실행할 턴이다.
    public int firstCombatTurn;
    // 망치의 접근·공격을 실행할 턴이다.
    public int secondCombatTurn;
}
