using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 현재 씬의 활성 해킹 가능 대상 목록을 관리하는 씬 단위 등록소다.
/// 해킹 판정과 효과 처리는 담당하지 않고 대상 조회만 제공한다.
/// </summary>
public class HackableRegistry : MonoBehaviour
{
    // 현재 씬에서 사용하는 해킹 대상 등록소 인스턴스다.
    public static HackableRegistry Instance { get; private set; }

    // 활성 해킹 대상 목록이다.
    private readonly List<HackableObject> hackables = new();

    public IReadOnlyList<HackableObject> Hackables => hackables;

    /// <summary>
    /// 씬의 단일 HackableRegistry 인스턴스를 등록한다.
    /// </summary>
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"{nameof(HackableRegistry)}: 이미 인스턴스가 있습니다. 중복 오브젝트 {name}의 컴포넌트를 비활성화합니다.", this);
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
    /// 해킹 가능 대상을 등록한다.
    /// </summary>
    public void Register(HackableObject hackable)
    {
        if (hackable == null || hackables.Contains(hackable))
        {
            return;
        }

        hackables.Add(hackable);
    }

    /// <summary>
    /// 해킹 가능 대상 등록을 해제한다.
    /// </summary>
    public void Unregister(HackableObject hackable)
    {
        if (hackable == null)
        {
            return;
        }

        hackables.Remove(hackable);
    }

    /// <summary>
    /// 지정한 그리드 칸에 있는 해킹 가능 대상을 찾는다.
    /// </summary>
    public bool TryGetHackableAt(GridPosition position, out HackableObject hackable)
    {
        for (int i = 0; i < hackables.Count; i++)
        {
            HackableObject candidate = hackables[i];
            if (candidate != null && candidate.enabled && candidate.GridPosition == position)
            {
                hackable = candidate;
                return true;
            }
        }

        hackable = null;
        return false;
    }
}
