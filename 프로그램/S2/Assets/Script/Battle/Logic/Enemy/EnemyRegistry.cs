using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 현재 씬에 활성화된 적 Context를 등록해 다른 시스템이 조회할 수 있게 하는 등록소다.
/// 적 AI나 애드 판정은 처리하지 않고, 목록 관리만 담당한다.
/// </summary>
public class EnemyRegistry : MonoBehaviour
{
    // 씬에서 사용하는 단일 적 등록소 인스턴스다.
    public static EnemyRegistry Instance { get; private set; }

    // 현재 활성화되어 있는 적 Context 목록이다.
    private readonly List<EnemyContext> enemies = new();

    public IReadOnlyList<EnemyContext> Enemies => enemies;

    /// <summary>
    /// 씬의 단일 EnemyRegistry 인스턴스를 등록한다.
    /// </summary>
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"{nameof(EnemyRegistry)}: 이미 인스턴스가 있습니다. 중복 오브젝트 {name}의 컴포넌트를 비활성화합니다.", this);
            enabled = false;
            return;
        }

        Instance = this;
    }

    /// <summary>
    /// 현재 인스턴스가 제거될 때 싱글톤 참조를 정리한다.
    /// </summary>
    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// 현재 씬에서 활성화된 적 Context를 등록한다.
    /// </summary>
    public void RegisterEnemy(EnemyContext enemy)
    {
        if (enemy == null || enemies.Contains(enemy))
        {
            return;
        }

        enemies.Add(enemy);
    }

    /// <summary>
    /// 더 이상 활성 적으로 취급하지 않을 적 Context 등록을 해제한다.
    /// </summary>
    public void UnregisterEnemy(EnemyContext enemy)
    {
        if (enemy == null)
        {
            return;
        }

        enemies.Remove(enemy);
    }
}
