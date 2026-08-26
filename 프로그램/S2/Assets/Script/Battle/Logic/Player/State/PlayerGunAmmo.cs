using System;
using UnityEngine;

/// <summary>
/// 플레이어 총 공격에 사용하는 현재 총알 수를 보관하고 소비를 처리한다.
/// 총알 최대값은 ControllableUnitData에서 읽고, 현재 총알은 이 컴포넌트의 런타임 상태로만 관리한다.
/// </summary>
public class PlayerGunAmmo : MonoBehaviour
{
    [Header("Reference")]
    // 총알 최대값을 읽을 플레이어 Context다.
    [SerializeField] private TacticalUnitContext playerContext;

    [Header("Ammo")]
    // 현재 남아 있는 총알 수다.
    [SerializeField] private int currentAmmo;
    // true면 시작 시 총알을 최대치로 채운다.
    [SerializeField] private bool refillOnAwake = true;

    [Header("Log")]
    // true면 총알 보충과 소비 로그를 출력한다.
    [SerializeField] private bool logAmmo = true;

    public int CurrentAmmo => currentAmmo;
    public int MaxAmmo => playerContext.UnitData.MaxGunAmmo;

    // 총알 수가 바뀔 때 현재 총알과 최대 총알을 전달한다.
    public event Action<int, int> AmmoChanged;

    /// <summary>
    /// 총알 상태에 필요한 참조와 데이터를 확인하고 시작 총알을 세팅한다.
    /// </summary>
    private void Awake()
    {
        if (!HasValidReference() || !HasValidData())
        {
            enabled = false;
            return;
        }

        currentAmmo = Mathf.Clamp(currentAmmo, 0, MaxAmmo);
        if (refillOnAwake)
        {
            RefillToMax();
        }
    }

    /// <summary>
    /// 지정한 수량의 총알을 소비할 수 있는지 확인한다.
    /// </summary>
    public bool CanSpend(int amount)
    {
        return amount > 0 && currentAmmo >= amount;
    }

    /// <summary>
    /// 지정한 수량의 총알 소비를 시도한다.
    /// </summary>
    public bool TrySpend(int amount)
    {
        if (amount <= 0)
        {
            Debug.LogError($"{nameof(PlayerGunAmmo)} on {name}에는 0보다 큰 총알 소비량만 사용할 수 있습니다. 입력값: {amount}", this);
            return false;
        }

        if (currentAmmo < amount)
        {
            if (logAmmo)
            {
                Debug.Log($"{nameof(PlayerGunAmmo)}: 총알이 부족합니다. 필요 총알: {amount}, 현재 총알: {currentAmmo}", this);
            }

            return false;
        }

        currentAmmo -= amount;
        AmmoChanged?.Invoke(currentAmmo, MaxAmmo);
        if (logAmmo)
        {
            Debug.Log($"{nameof(PlayerGunAmmo)}: 총알 {amount}발을 소비했습니다. 남은 총알: {currentAmmo}/{MaxAmmo}", this);
        }

        return true;
    }

    /// <summary>
    /// 현재 총알을 최대치로 채운다.
    /// </summary>
    public void RefillToMax()
    {
        currentAmmo = MaxAmmo;
        AmmoChanged?.Invoke(currentAmmo, MaxAmmo);
        if (logAmmo)
        {
            Debug.Log($"{nameof(PlayerGunAmmo)}: 총알을 최대치로 보충했습니다. 현재 총알: {currentAmmo}/{MaxAmmo}", this);
        }
    }

    /// <summary>
    /// 총알 상태에 필요한 필수 참조가 연결되어 있는지 확인한다.
    /// </summary>
    public bool HasValidReference()
    {
        if (playerContext == null)
        {
            Debug.LogError($"{nameof(PlayerGunAmmo)} on {name}에는 {nameof(TacticalUnitContext)} 참조가 필요합니다.", this);
            return false;
        }

        if (playerContext.UnitData == null)
        {
            Debug.LogError($"{nameof(PlayerGunAmmo)} on {name}에는 {nameof(TacticalUnitContext)}에 연결된 {nameof(ControllableUnitData)} 참조가 필요합니다.", this);
            return false;
        }

        return true;
    }

    /// <summary>
    /// 총알 상태에 필요한 데이터가 유효한지 확인한다.
    /// </summary>
    public bool HasValidData()
    {
        if (playerContext.UnitData.MaxGunAmmo <= 0)
        {
            Debug.LogError($"{nameof(PlayerGunAmmo)} on {name}의 {nameof(ControllableUnitData)} 최대 총알 수는 0보다 커야 합니다.", this);
            return false;
        }

        return true;
    }
}
