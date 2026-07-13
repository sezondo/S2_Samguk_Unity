using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 공격 종류 하나의 공격자 애니메이션과 동시 연출 유지 시간을 보관한다.
/// </summary>
[Serializable]
public struct CombatPresentationEntry
{
    // 액션에서 전달한 공격 연출 종류다.
    [SerializeField] private AttackPresentationKind attackKind;
    // 공격자에게 재생할 Animator 상태 이름이다.
    [SerializeField] private string attackerAnimationStateName;
    // 공격자와 피격자의 자세를 동시에 유지할 시간이다.
    [SerializeField] private float presentationDuration;

    public AttackPresentationKind AttackKind => attackKind;
    public string AttackerAnimationStateName => attackerAnimationStateName;
    public float PresentationDuration => presentationDuration;
}

/// <summary>
/// 통합 전투 연출에서 사용할 공격·피격·사망·복귀 애니메이션 값을 보관한다.
/// 실제 유효성 검사는 이 데이터를 사용하는 CombatActionPresenter가 담당한다.
/// </summary>
[CreateAssetMenu(fileName = "CombatPresentationData", menuName = "S2-T/Presentation/Combat Presentation Data")]
public class CombatPresentationData : ScriptableObject
{
    [Header("Combat Animation")]
    // 공격 종류별 공격자 상태와 동시 연출 유지 시간 목록이다.
    [SerializeField] private List<CombatPresentationEntry> entries = new();
    // 피해를 받고 살아 있는 대상에게 재생할 Animator 상태 이름이다.
    [SerializeField] private string hitAnimationStateName = "Hit";
    // 이번 피해로 사망한 대상에게 재생할 Animator 상태 이름이다.
    [SerializeField] private string deathAnimationStateName = "Death";
    // 전투 연출이 끝난 생존 Actor에게 재생할 Animator 상태 이름이다.
    [SerializeField] private string idleAnimationStateName = "Idle";
    // Animator 상태를 전환할 때 사용할 CrossFade 시간이다. 한 프레임 이미지 전환은 0을 사용한다.
    [SerializeField] private float crossFadeDuration;

    public IReadOnlyList<CombatPresentationEntry> Entries => entries;
    public string HitAnimationStateName => hitAnimationStateName;
    public string DeathAnimationStateName => deathAnimationStateName;
    public string IdleAnimationStateName => idleAnimationStateName;
    public float CrossFadeDuration => crossFadeDuration;

    /// <summary>
    /// 지정한 공격 종류에 해당하는 전투 연출 설정을 찾는다.
    /// </summary>
    public bool TryGetEntry(AttackPresentationKind attackKind, out CombatPresentationEntry entry)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].AttackKind == attackKind)
            {
                entry = entries[i];
                return true;
            }
        }

        entry = default;
        return false;
    }
}
