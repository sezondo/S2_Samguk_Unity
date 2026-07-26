using System;
using UnityEngine;

/// <summary>
/// 적 하나의 현재 경계 상태를 보관하고 외부 상태 전환 요청을 처리한다.
/// 현재 단계에서는 평상과 발각 상태만 구분한다.
/// </summary>
public class EnemyAlertState : MonoBehaviour
{
    [Header("Reference")]
    // 이 상태 컴포넌트가 속한 적의 핵심 참조 주머니다.
    [SerializeField] private EnemyContext enemyContext;

    [Header("State")]
    // 현재 적의 경계 상태다.
    [SerializeField] private EnemyAlertLevel currentLevel = EnemyAlertLevel.Normal;

    [Header("Log")]
    // true면 상태 변경 결과를 Unity 콘솔에 출력한다.
    [SerializeField] private bool logStateChanges = true;

    public EnemyAlertLevel CurrentLevel => currentLevel;
    public bool IsAlerted => currentLevel == EnemyAlertLevel.Alerted;

    // 경계 상태가 바뀔 때 이전 상태와 새 상태를 전달한다.
    public event Action<EnemyAlertLevel, EnemyAlertLevel> AlertLevelChanged;

    /// <summary>
    /// 상태 관리에 필요한 참조를 확인한다.
    /// </summary>
    private void Awake()
    {
        if (!HasValidReference())
        {
            enabled = false;
        }
    }

    /// <summary>
    /// 외부 시스템에서 이 적에게 발각 상태 전환을 요청한다.
    /// </summary>
    public bool RequestAlert(GridPosition detectedPosition, EnemyContext sourceEnemy)
    {
        return TrySetLevel(EnemyAlertLevel.Alerted, detectedPosition, sourceEnemy);
    }

    /// <summary>
    /// 현재 경계 상태를 새 상태로 바꾸고 변경 이벤트를 알린다.
    /// </summary>
    private bool TrySetLevel(EnemyAlertLevel nextLevel, GridPosition detectedPosition, EnemyContext sourceEnemy)
    {
        if (currentLevel == nextLevel)
        {
            return false;
        }

        EnemyAlertLevel previousLevel = currentLevel;
        currentLevel = nextLevel;

        if (logStateChanges)
        {
            string sourceName = sourceEnemy != null ? sourceEnemy.name : "알 수 없는 적";
            Debug.Log($"{nameof(EnemyAlertState)}: {enemyContext.name} 적 상태가 {previousLevel}에서 {currentLevel}로 변경됐습니다. 발각 칸: {detectedPosition}, 전파 출처: {sourceName}", this);
        }

        AlertLevelChanged?.Invoke(previousLevel, currentLevel);
        return true;
    }

    /// <summary>
    /// 상태 관리에 필요한 필수 참조가 연결되어 있는지 확인한다.
    /// </summary>
    private bool HasValidReference()
    {
        if (enemyContext == null)
        {
            Debug.LogError($"{nameof(EnemyAlertState)} on {name}에는 {nameof(EnemyContext)} 참조가 필요합니다.", this);
            return false;
        }

        if (!enemyContext.HasValidReference())
        {
            return false;
        }

        return true;
    }
}

/// <summary>
/// 적의 현재 경계 상태 단계다.
/// </summary>
public enum EnemyAlertLevel
{
    Normal,
    Alerted,
}
