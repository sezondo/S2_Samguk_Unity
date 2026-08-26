using TMPro;
using UnityEngine;

/// <summary>
/// 전투 HUD가 사용하는 폰트와 UI Sprite 참조를 한 에셋에 모아 제공한다.
/// </summary>
[CreateAssetMenu(fileName = "BattleHudAssetSet", menuName = "S2/Battle/HUD Asset Set")]
public sealed class BattleHudAssetSet : ScriptableObject
{
    [Header("Font")]
    // HUD의 모든 한국어 텍스트에 사용할 TMP 폰트다.
    [SerializeField] private TMP_FontAsset font;

    [Header("Screen HUD")]
    // 좌측 상단 목표 표시 패널이다.
    [SerializeField] private Sprite objectivePanel;
    // 상단 중앙 턴 배너 기본 프레임이다.
    [SerializeField] private Sprite turnBanner;
    // 플레이어 턴 배너에 겹쳐 표시할 청록빛 상태다.
    [SerializeField] private Sprite turnGlowPlayer;
    // 적 턴 배너에 겹쳐 표시할 적색 상태다.
    [SerializeField] private Sprite turnGlowEnemy;
    // 중립·진입 상태 턴 배너에 겹쳐 표시할 상태다.
    [SerializeField] private Sprite turnGlowNeutral;
    // 경고 상태 턴 배너에 겹쳐 표시할 상태다.
    [SerializeField] private Sprite turnGlowWarning;
    // 우측 상단 유닛 초상화 공통 프레임이다.
    [SerializeField] private Sprite portraitFrame;
    // 하단 중앙 행동 정보 패널이다.
    [SerializeField] private Sprite actionInfoPanel;
    // 우측 하단 턴 종료 버튼이다.
    [SerializeField] private Sprite endTurnButton;
    // 선택 유닛의 HP 외곽 프레임이다.
    [SerializeField] private Sprite healthBarFrame;
    // 선택 유닛의 HP 한 칸 표시다.
    [SerializeField] private Sprite healthSegment;
    // 선택 유닛의 AP 한 칸 표시다.
    [SerializeField] private Sprite actionPointSegment;
    // 선택 유닛의 총알 한 발 표시다.
    [SerializeField] private Sprite ammoCartridge;

    [Header("Action Button")]
    // 모든 행동 버튼이 공유하는 프레임이다.
    [SerializeField] private Sprite actionButtonFrame;
    // 행동 버튼 Hover 상태 오버레이다.
    [SerializeField] private Sprite actionStateHover;
    // 행동 버튼 Pressed 상태 오버레이다.
    [SerializeField] private Sprite actionStatePressed;
    // 현재 선택된 행동 버튼 상태 오버레이다.
    [SerializeField] private Sprite actionStateSelected;
    // 자원 부족 등 비활성 행동 상태 오버레이다.
    [SerializeField] private Sprite actionStateDisabled;
    // 현재 사용할 수 없는 행동의 잠금 상태 오버레이다.
    [SerializeField] private Sprite actionStateLocked;
    // 발각 위험이 있는 행동의 경고 상태 오버레이다.
    [SerializeField] private Sprite actionStateWarning;
    // 이동 행동 아이콘이다.
    [SerializeField] private Sprite moveIcon;
    // 총격 행동 아이콘이다.
    [SerializeField] private Sprite gunIcon;
    // 근접 공격 행동 아이콘이다.
    [SerializeField] private Sprite meleeIcon;
    // 검 투척 행동 아이콘이다.
    [SerializeField] private Sprite swordThrowIcon;
    // 검 회수 행동 아이콘이다.
    [SerializeField] private Sprite swordRecallIcon;
    // 해킹 행동 아이콘이다.
    [SerializeField] private Sprite hackIcon;

    [Header("Portrait State")]
    // 행동 가능한 유닛 초상화 상태 오버레이다.
    [SerializeField] private Sprite portraitAvailable;
    // 현재 선택된 유닛 초상화 상태 오버레이다.
    [SerializeField] private Sprite portraitSelected;
    // AP를 모두 사용한 유닛 초상화 상태 오버레이다.
    [SerializeField] private Sprite portraitFinished;
    // 사망한 유닛 초상화 상태 오버레이다.
    [SerializeField] private Sprite portraitDead;

    [Header("World Overlay")]
    // 선택된 플레이어 유닛을 표시하는 브래킷이다.
    [SerializeField] private Sprite selectedUnitBracket;
    // 총격 대상 브래킷이다.
    [SerializeField] private Sprite attackTargetBracket;
    // 이동 목표 브래킷이다.
    [SerializeField] private Sprite moveTargetBracket;
    // 근접 공격 목표 브래킷이다.
    [SerializeField] private Sprite meleeTargetBracket;
    // 검 행동 목표 브래킷이다.
    [SerializeField] private Sprite swordTargetBracket;
    // 해킹 목표 브래킷이다.
    [SerializeField] private Sprite hackTargetBracket;
    // 발각 위험 목표 브래킷이다.
    [SerializeField] private Sprite detectionRiskBracket;
    // 유효하지 않은 목표 브래킷이다.
    [SerializeField] private Sprite invalidTargetBracket;
    // 스테이지 목표 브래킷이다.
    [SerializeField] private Sprite objectiveBracket;
    // 적 명중률 정보 패널이다.
    [SerializeField] private Sprite enemyHitPanel;
    // 적 명중률 정보의 조준선 아이콘이다.
    [SerializeField] private Sprite enemyCrosshairIcon;
    // 낮은 엄폐 표시 아이콘이다.
    [SerializeField] private Sprite lowCoverIcon;
    // 높은 엄폐 표시 아이콘이다.
    [SerializeField] private Sprite highCoverIcon;
    // 적 미인지 상태 아이콘이다.
    [SerializeField] private Sprite enemyUnaware;
    // 적 의심 상태 아이콘이다.
    [SerializeField] private Sprite enemySuspicious;
    // 적 조사 상태 아이콘이다.
    [SerializeField] private Sprite enemyInvestigating;
    // 적 의심 공유 상태 아이콘이다.
    [SerializeField] private Sprite enemySharedSuspicion;
    // 적 발각 상태 아이콘이다.
    [SerializeField] private Sprite enemyAlerted;
    // 적이 플레이어를 발견한 상태 아이콘이다.
    [SerializeField] private Sprite enemyTargetSpotted;

    public TMP_FontAsset Font => font;
    public Sprite ObjectivePanel => objectivePanel;
    public Sprite TurnBanner => turnBanner;
    public Sprite TurnGlowPlayer => turnGlowPlayer;
    public Sprite TurnGlowEnemy => turnGlowEnemy;
    public Sprite TurnGlowNeutral => turnGlowNeutral;
    public Sprite TurnGlowWarning => turnGlowWarning;
    public Sprite PortraitFrame => portraitFrame;
    public Sprite ActionInfoPanel => actionInfoPanel;
    public Sprite EndTurnButton => endTurnButton;
    public Sprite HealthBarFrame => healthBarFrame;
    public Sprite HealthSegment => healthSegment;
    public Sprite ActionPointSegment => actionPointSegment;
    public Sprite AmmoCartridge => ammoCartridge;
    public Sprite ActionButtonFrame => actionButtonFrame;
    public Sprite ActionStateHover => actionStateHover;
    public Sprite ActionStatePressed => actionStatePressed;
    public Sprite ActionStateSelected => actionStateSelected;
    public Sprite ActionStateDisabled => actionStateDisabled;
    public Sprite ActionStateLocked => actionStateLocked;
    public Sprite ActionStateWarning => actionStateWarning;
    public Sprite PortraitAvailable => portraitAvailable;
    public Sprite PortraitSelected => portraitSelected;
    public Sprite PortraitFinished => portraitFinished;
    public Sprite PortraitDead => portraitDead;
    public Sprite SelectedUnitBracket => selectedUnitBracket;
    public Sprite AttackTargetBracket => attackTargetBracket;
    public Sprite MoveTargetBracket => moveTargetBracket;
    public Sprite MeleeTargetBracket => meleeTargetBracket;
    public Sprite SwordTargetBracket => swordTargetBracket;
    public Sprite HackTargetBracket => hackTargetBracket;
    public Sprite DetectionRiskBracket => detectionRiskBracket;
    public Sprite InvalidTargetBracket => invalidTargetBracket;
    public Sprite ObjectiveBracket => objectiveBracket;
    public Sprite EnemyHitPanel => enemyHitPanel;
    public Sprite EnemyCrosshairIcon => enemyCrosshairIcon;
    public Sprite LowCoverIcon => lowCoverIcon;
    public Sprite HighCoverIcon => highCoverIcon;
    public Sprite EnemyUnaware => enemyUnaware;
    public Sprite EnemySuspicious => enemySuspicious;
    public Sprite EnemyInvestigating => enemyInvestigating;
    public Sprite EnemySharedSuspicion => enemySharedSuspicion;
    public Sprite EnemyAlerted => enemyAlerted;
    public Sprite EnemyTargetSpotted => enemyTargetSpotted;

    /// <summary>
    /// 지정한 행동 종류에 대응하는 행동 아이콘을 반환한다.
    /// </summary>
    public Sprite GetActionIcon(BattleHudActionType actionType)
    {
        return actionType switch
        {
            BattleHudActionType.Move => moveIcon,
            BattleHudActionType.Gun => gunIcon,
            BattleHudActionType.Melee => meleeIcon,
            BattleHudActionType.SwordThrow => swordThrowIcon,
            BattleHudActionType.SwordRecall => swordRecallIcon,
            BattleHudActionType.Hack => hackIcon,
            _ => null,
        };
    }

    /// <summary>
    /// HUD 구성에 반드시 필요한 폰트와 핵심 Sprite가 모두 연결되어 있는지 확인한다.
    /// </summary>
    public bool HasValidData()
    {
        return font != null && objectivePanel != null && turnBanner != null &&
               turnGlowPlayer != null && turnGlowEnemy != null && turnGlowNeutral != null &&
               turnGlowWarning != null && portraitFrame != null && actionInfoPanel != null &&
               endTurnButton != null && actionPointSegment != null && ammoCartridge != null &&
               actionButtonFrame != null && actionStateHover != null && actionStatePressed != null &&
               actionStateSelected != null && actionStateDisabled != null && actionStateLocked != null &&
               actionStateWarning != null && moveIcon != null && gunIcon != null && meleeIcon != null &&
               swordThrowIcon != null && swordRecallIcon != null && hackIcon != null &&
               portraitAvailable != null && portraitSelected != null && portraitFinished != null &&
               portraitDead != null && selectedUnitBracket != null && attackTargetBracket != null &&
               moveTargetBracket != null && meleeTargetBracket != null && swordTargetBracket != null &&
               hackTargetBracket != null && detectionRiskBracket != null && invalidTargetBracket != null &&
               objectiveBracket != null && enemyHitPanel != null && enemyCrosshairIcon != null &&
               lowCoverIcon != null && highCoverIcon != null && enemyUnaware != null &&
               enemySuspicious != null && enemyInvestigating != null && enemySharedSuspicion != null &&
               enemyAlerted != null && enemyTargetSpotted != null;
    }
}
