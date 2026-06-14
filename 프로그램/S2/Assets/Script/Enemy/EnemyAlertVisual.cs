using UnityEngine;

/// <summary>
/// 적의 경계 상태 변경을 받아 임시 시각 피드백으로 표시한다.
/// 게임 규칙에는 관여하지 않고, 디버그와 프로토타입 확인용 표시만 담당한다.
/// </summary>
public class EnemyAlertVisual : MonoBehaviour
{
    [Header("Reference")]
    // 적 경계 상태 참조를 제공하는 적 Context다.
    [SerializeField] private EnemyContext enemyContext;
    // 색상을 바꿀 적 스프라이트 렌더러다.
    [SerializeField] private SpriteRenderer targetRenderer;

    [Header("Color")]
    // 평상 상태일 때 적용할 색이다.
    [SerializeField] private Color normalColor = Color.white;
    // 발각 상태일 때 적용할 색이다.
    [SerializeField] private Color alertedColor = new(1f, 0.25f, 0.2f, 1f);

    // EnemyContext에서 꺼낸 적 경계 상태 컴포넌트다.
    private EnemyAlertState alertState;

    /// <summary>
    /// 시각 표시를 위해 필요한 참조를 확인한다.
    /// </summary>
    private void Awake()
    {
        if (!HasValidReference())
        {
            enabled = false;
            return;
        }

        ApplyCurrentStateColor();
    }

    /// <summary>
    /// 경계 상태 변경 이벤트를 구독하고 현재 상태 색상을 반영한다.
    /// </summary>
    private void OnEnable()
    {
        if (!HasValidReference())
        {
            enabled = false;
            return;
        }

        alertState.AlertLevelChanged += HandleAlertLevelChanged;
        ApplyCurrentStateColor();
    }

    /// <summary>
    /// 경계 상태 변경 이벤트 구독을 해제한다.
    /// </summary>
    private void OnDisable()
    {
        if (alertState != null)
        {
            alertState.AlertLevelChanged -= HandleAlertLevelChanged;
        }
    }

    /// <summary>
    /// 적 경계 상태가 바뀌면 새 상태에 맞는 색상을 적용한다.
    /// </summary>
    private void HandleAlertLevelChanged(EnemyAlertLevel previousLevel, EnemyAlertLevel currentLevel)
    {
        ApplyColor(currentLevel);
    }

    /// <summary>
    /// 현재 적 경계 상태에 맞는 색상을 적용한다.
    /// </summary>
    private void ApplyCurrentStateColor()
    {
        ApplyColor(alertState.CurrentLevel);
    }

    /// <summary>
    /// 지정한 경계 상태에 맞는 색상을 스프라이트 렌더러에 적용한다.
    /// </summary>
    private void ApplyColor(EnemyAlertLevel level)
    {
        targetRenderer.color = level == EnemyAlertLevel.Alerted ? alertedColor : normalColor;
    }

    /// <summary>
    /// 시각 표시에 필요한 필수 참조가 연결되어 있는지 확인한다.
    /// </summary>
    private bool HasValidReference()
    {
        if (enemyContext == null)
        {
            Debug.LogError($"{nameof(EnemyAlertVisual)} on {name}에는 {nameof(EnemyContext)} 참조가 필요합니다.", this);
            return false;
        }

        alertState = enemyContext.AlertState;
        if (alertState == null)
        {
            Debug.LogError($"{nameof(EnemyAlertVisual)} on {name}에는 {nameof(EnemyContext)}에 연결된 {nameof(EnemyAlertState)} 참조가 필요합니다.", this);
            return false;
        }

        if (targetRenderer == null)
        {
            Debug.LogError($"{nameof(EnemyAlertVisual)} on {name}에는 색상을 바꿀 {nameof(SpriteRenderer)} 참조가 필요합니다.", this);
            return false;
        }

        return true;
    }
}
