using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// S2-T 턴제 테스트 중 주요 런타임 수치를 화면에 출력하는 디버그 오버레이다.
/// 게임 상태를 바꾸지 않고 연결된 참조와 논리 이벤트 스냅샷만 읽는다.
/// </summary>
public class S2TDebugOverlay : MonoBehaviour, IActionLogicEventHandler
{
    [Header("Reference")]
    // 스테이지 진행 상태를 표시하기 위한 선택 참조다.
    [SerializeField] private StageStateManager stageStateManager;

    [Header("Overlay")]
    // true면 디버그 오버레이를 화면에 출력한다.
    [SerializeField] private bool showOverlay = true;
    // 오버레이가 시작될 화면 좌상단 위치다.
    [SerializeField] private Vector2 screenPosition = new(12f, 12f);
    // 오버레이 너비다.
    [SerializeField] private float overlayWidth = 360f;
    // 오버레이 높이다.
    [SerializeField] private float overlayHeight = 430f;
    // 오버레이 텍스트 크기다.
    [SerializeField] private int fontSize = 14;
    // 오버레이 텍스트 색상이다.
    [SerializeField] private Color textColor = Color.white;
    // 오버레이 배경 색상이다.
    [SerializeField] private Color backgroundColor = new(0f, 0f, 0f, 0.72f);

    // 화면 출력 문자열을 매 프레임 재사용하는 버퍼다.
    private readonly StringBuilder builder = new();
    // 마지막 피해 이벤트가 있었는지 나타낸다.
    private bool hasLastDamage;
    // 마지막 피해 이벤트의 공격자 이름이다.
    private string lastDamageAttackerName = "None";
    // 마지막 피해 이벤트의 대상 이름이다.
    private string lastDamageTargetName = "None";
    // 마지막 피해 이벤트의 대상 칸이다.
    private GridPosition lastDamageTargetPosition;
    // 마지막 피해 이벤트의 피해 결과 스냅샷이다.
    private DamageResult lastDamageResult;
    // 마지막 사망 이벤트가 있었는지 나타낸다.
    private bool hasLastDeath;
    // 마지막 사망 이벤트의 공격자 이름이다.
    private string lastDeathAttackerName = "None";
    // 마지막 사망 이벤트의 액터 이름이다.
    private string lastDeathActorName = "None";
    // 마지막 사망 이벤트의 발생 칸이다.
    private GridPosition lastDeathPosition;
    // 마지막 사망 이벤트의 피해 결과 스냅샷이다.
    private DamageResult lastDeathResult;
    // 오버레이 텍스트 출력에 사용하는 GUI 스타일이다.
    private GUIStyle labelStyle;
    // 오버레이 배경 출력에 사용하는 GUI 스타일이다.
    private GUIStyle boxStyle;
    // 단색 배경을 그리기 위한 런타임 텍스처다.
    private Texture2D backgroundTexture;
    // 현재 플레이어 제어 매니저가 선택한 전술 유닛이다.
    private TacticalUnitContext ActiveUnit => PlayerUnitControlManager.Instance != null
        ? PlayerUnitControlManager.Instance.ActiveUnit
        : null;

    /// <summary>
    /// 논리 이벤트 버스에 디버그 이벤트 수신자로 등록한다.
    /// </summary>
    private void OnEnable()
    {
        ActionLogicEventBus.Register(this);
    }

    /// <summary>
    /// 논리 이벤트 버스에서 디버그 이벤트 수신자를 해제한다.
    /// </summary>
    private void OnDisable()
    {
        ActionLogicEventBus.Unregister(this);
    }

    /// <summary>
    /// 런타임 배경 텍스처를 정리한다.
    /// </summary>
    private void OnDestroy()
    {
        if (backgroundTexture != null)
        {
            Destroy(backgroundTexture);
            backgroundTexture = null;
        }
    }

    /// <summary>
    /// Unity IMGUI로 디버그 정보를 화면에 출력한다.
    /// </summary>
    private void OnGUI()
    {
        if (!showOverlay)
        {
            return;
        }

        InitializeStyles();
        builder.Clear();
        BuildOverlayText();

        Rect area = new(screenPosition.x, screenPosition.y, overlayWidth, overlayHeight);
        GUI.Box(area, GUIContent.none, boxStyle);
        GUI.Label(new Rect(area.x + 10f, area.y + 8f, area.width - 20f, area.height - 16f), builder.ToString(), labelStyle);
    }

    /// <summary>
    /// 지정한 논리 이벤트를 이 오버레이가 처리할 수 있는지 확인한다.
    /// </summary>
    public bool CanHandle(IActionLogicEvent logicEvent)
    {
        return logicEvent is DamageAppliedLogicEvent || logicEvent is ActorDiedLogicEvent;
    }

    /// <summary>
    /// 마지막 피해/사망 이벤트 스냅샷을 저장한다.
    /// </summary>
    public void Handle(IActionLogicEvent logicEvent, ActionResolutionContext context)
    {
        if (logicEvent is DamageAppliedLogicEvent damageApplied)
        {
            hasLastDamage = true;
            lastDamageAttackerName = damageApplied.Attacker != null ? damageApplied.Attacker.name : "None";
            lastDamageTargetName = damageApplied.Target != null ? damageApplied.Target.name : "None";
            lastDamageTargetPosition = damageApplied.TargetPosition;
            lastDamageResult = damageApplied.Result;
            return;
        }

        if (logicEvent is ActorDiedLogicEvent actorDied)
        {
            hasLastDeath = true;
            lastDeathAttackerName = actorDied.Attacker != null ? actorDied.Attacker.name : "None";
            lastDeathActorName = actorDied.DeadActor != null ? actorDied.DeadActor.name : "None";
            lastDeathPosition = actorDied.DeadPosition;
            lastDeathResult = actorDied.Result;
        }
    }

    /// <summary>
    /// 현재 연결된 참조와 마지막 이벤트를 읽어 출력 문자열을 만든다.
    /// </summary>
    private void BuildOverlayText()
    {
        builder.AppendLine("[S2-T Debug Overlay]");
        builder.AppendLine();
        AppendTurnState();
        AppendStageState();
        AppendPresentationQueueState();
        builder.AppendLine();
        AppendPlayerState();
        builder.AppendLine();
        AppendEnemyState();
        builder.AppendLine();
        AppendLastDamageState();
        builder.AppendLine();
        AppendLastDeathState();
    }

    /// <summary>
    /// 현재 턴 상태를 출력 버퍼에 추가한다.
    /// </summary>
    private void AppendTurnState()
    {
        TurnManager turnManager = TurnManager.Instance;
        builder.Append("Turn: ");
        builder.AppendLine(turnManager != null ? turnManager.CurrentSide.ToString() : "None");
    }

    /// <summary>
    /// 현재 스테이지 상태를 출력 버퍼에 추가한다.
    /// </summary>
    private void AppendStageState()
    {
        builder.Append("Stage: ");
        builder.AppendLine(stageStateManager != null ? stageStateManager.CurrentState.ToString() : "None");
    }

    /// <summary>
    /// 현재 연출 큐 상태를 출력 버퍼에 추가한다.
    /// </summary>
    private void AppendPresentationQueueState()
    {
        ActionPresentationQueue queue = ActionPresentationQueue.Instance;
        if (queue == null)
        {
            builder.AppendLine("Presentation: None");
            return;
        }

        builder.Append("Presentation: ");
        builder.Append(queue.IsPlaying ? "Playing" : "Idle");
        builder.Append(" / Queued ");
        builder.AppendLine(queue.QueuedEventCount.ToString());
    }

    /// <summary>
    /// 플레이어 관련 수치를 출력 버퍼에 추가한다.
    /// </summary>
    private void AppendPlayerState()
    {
        builder.AppendLine("[Player]");
        TacticalUnitContext playerContext = ActiveUnit;
        if (playerContext == null)
        {
            builder.AppendLine("Context: None");
            AppendPlayerHealth();
            return;
        }

        GridActor gridActor = playerContext.GridActor;
        builder.Append("Position: ");
        builder.AppendLine(gridActor != null ? gridActor.GridPosition.ToString() : "None");

        AppendPlayerActionPoint();
        AppendPlayerHealth();
        AppendSwordState();
        AppendGunState();
        AppendActionSelectionState();
    }

    /// <summary>
    /// 플레이어 AP 수치를 출력 버퍼에 추가한다.
    /// </summary>
    private void AppendPlayerActionPoint()
    {
        TacticalUnitContext playerContext = ActiveUnit;
        if (playerContext == null)
        {
            builder.AppendLine("AP: None");
            return;
        }

        ActionPoint actionPoint = playerContext.ActionPoint;
        ControllableUnitData unitData = playerContext.UnitData;
        if (actionPoint == null || unitData == null)
        {
            builder.AppendLine("AP: None");
            return;
        }

        builder.Append("AP: ");
        builder.Append(actionPoint.Current);
        builder.Append(" / ");
        builder.AppendLine(unitData.MaxActionPoint.ToString());
    }

    /// <summary>
    /// 플레이어 HP 수치를 출력 버퍼에 추가한다.
    /// </summary>
    private void AppendPlayerHealth()
    {
        ActorHealth playerHealth = ActiveUnit != null ? ActiveUnit.Health : null;
        if (playerHealth == null)
        {
            builder.AppendLine("HP: None");
            return;
        }

        builder.Append("HP: ");
        builder.Append(playerHealth.CurrentHitPoint);
        builder.Append(" / ");
        builder.Append(playerHealth.MaxHitPoint);
        builder.Append(" / Dead ");
        builder.AppendLine(playerHealth.IsDead.ToString());
    }

    /// <summary>
    /// 검 위치와 회수 상태를 출력 버퍼에 추가한다.
    /// </summary>
    private void AppendSwordState()
    {
        TacticalUnitContext playerContext = ActiveUnit;
        if (playerContext == null)
        {
            builder.AppendLine("Sword: None");
            return;
        }

        PlayerSwordState swordState = playerContext.SwordState;
        if (swordState == null)
        {
            builder.AppendLine("Sword: None");
            return;
        }

        builder.Append("Sword: ");
        builder.Append(swordState.IsRecalled ? "Recalled" : "Deployed");
        builder.Append(" / ");
        builder.AppendLine(swordState.CurrentPosition.ToString());
    }

    /// <summary>
    /// 총알 상태를 출력 버퍼에 추가한다.
    /// </summary>
    private void AppendGunState()
    {
        TacticalUnitContext playerContext = ActiveUnit;
        if (playerContext == null)
        {
            builder.AppendLine("Gun Ammo: None");
            return;
        }

        PlayerGunAmmo gunAmmo = playerContext.GunAmmo;
        if (gunAmmo == null)
        {
            builder.AppendLine("Gun Ammo: None");
            return;
        }

        builder.Append("Gun Ammo: ");
        builder.Append(gunAmmo.CurrentAmmo);
        builder.Append(" / ");
        builder.AppendLine(gunAmmo.MaxAmmo.ToString());
    }

    /// <summary>
    /// 현재 행동 선택 상태를 출력 버퍼에 추가한다.
    /// </summary>
    private void AppendActionSelectionState()
    {
        TacticalUnitContext playerContext = ActiveUnit;
        if (playerContext == null)
        {
            builder.AppendLine("Action Selection: None");
            return;
        }

        builder.Append("Move Selected: ");
        builder.AppendLine(playerContext.GridMoveAction != null ? playerContext.GridMoveAction.IsMoveSelected.ToString() : "None");
        builder.Append("Hack Selected: ");
        builder.AppendLine(playerContext.HackAction != null ? playerContext.HackAction.IsHackSelected.ToString() : "None");
        builder.Append("Sword Throw Selected: ");
        builder.AppendLine(playerContext.SwordThrowAction != null ? playerContext.SwordThrowAction.IsSwordThrowSelected.ToString() : "None");
        builder.Append("Melee Selected: ");
        builder.AppendLine(playerContext.MeleeAttackAction != null ? playerContext.MeleeAttackAction.IsMeleeAttackSelected.ToString() : "None");
        builder.Append("Gun Selected: ");
        builder.AppendLine(playerContext.GunAttackAction != null ? playerContext.GunAttackAction.IsGunAttackSelected.ToString() : "None");
    }

    /// <summary>
    /// 적 등록소 기준 적 수와 경계 수를 출력 버퍼에 추가한다.
    /// </summary>
    private void AppendEnemyState()
    {
        builder.AppendLine("[Enemy]");
        EnemyRegistry registry = EnemyRegistry.Instance;
        if (registry == null)
        {
            builder.AppendLine("Registry: None");
            return;
        }

        IReadOnlyList<EnemyContext> enemies = registry.Enemies;
        int activeCount = 0;
        int suspiciousCount = 0;
        int alertedCount = 0;
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyContext enemy = enemies[i];
            if (enemy == null || !enemy.enabled)
            {
                continue;
            }

            activeCount++;
            if (enemy.AlertState != null && enemy.AlertState.IsSuspicious)
            {
                suspiciousCount++;
            }
            else if (enemy.AlertState != null && enemy.AlertState.IsAlerted)
            {
                alertedCount++;
            }
        }

        builder.Append("Active: ");
        builder.Append(activeCount);
        builder.Append(" / Suspicious: ");
        builder.Append(suspiciousCount);
        builder.Append(" / Alerted: ");
        builder.AppendLine(alertedCount.ToString());
    }

    /// <summary>
    /// 마지막 피해 이벤트 스냅샷을 출력 버퍼에 추가한다.
    /// </summary>
    private void AppendLastDamageState()
    {
        builder.AppendLine("[Last Damage]");
        if (!hasLastDamage)
        {
            builder.AppendLine("None");
            return;
        }

        builder.Append("Attacker: ");
        builder.AppendLine(lastDamageAttackerName);
        builder.Append("Target: ");
        builder.Append(lastDamageTargetName);
        builder.Append(" at ");
        builder.AppendLine(lastDamageTargetPosition.ToString());
        builder.Append("Damage: ");
        builder.Append(lastDamageResult.Damage);
        builder.Append(" / Applied ");
        builder.AppendLine(lastDamageResult.Applied.ToString());
        builder.Append("HP: ");
        builder.Append(lastDamageResult.HitPointBefore);
        builder.Append(" -> ");
        builder.AppendLine(lastDamageResult.HitPointAfter.ToString());
        builder.Append("Killed: ");
        builder.AppendLine(lastDamageResult.KilledByThisDamage.ToString());
    }

    /// <summary>
    /// 마지막 사망 이벤트 스냅샷을 출력 버퍼에 추가한다.
    /// </summary>
    private void AppendLastDeathState()
    {
        builder.AppendLine("[Last Death]");
        if (!hasLastDeath)
        {
            builder.AppendLine("None");
            return;
        }

        builder.Append("Attacker: ");
        builder.AppendLine(lastDeathAttackerName);
        builder.Append("Dead Actor: ");
        builder.Append(lastDeathActorName);
        builder.Append(" at ");
        builder.AppendLine(lastDeathPosition.ToString());
        builder.Append("Damage: ");
        builder.Append(lastDeathResult.Damage);
        builder.Append(" / HP: ");
        builder.Append(lastDeathResult.HitPointBefore);
        builder.Append(" -> ");
        builder.AppendLine(lastDeathResult.HitPointAfter.ToString());
    }

    /// <summary>
    /// OnGUI 출력에 필요한 스타일을 초기화한다.
    /// </summary>
    private void InitializeStyles()
    {
        if (backgroundTexture == null)
        {
            backgroundTexture = new Texture2D(1, 1);
            backgroundTexture.SetPixel(0, 0, backgroundColor);
            backgroundTexture.Apply();
        }

        if (boxStyle == null)
        {
            boxStyle = new GUIStyle(GUI.skin.box);
            boxStyle.normal.background = backgroundTexture;
        }

        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                normal =
                {
                    textColor = textColor
                }
            };
        }
    }
}
