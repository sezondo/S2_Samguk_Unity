using System;
using UnityEngine;

[Obsolete("Bow system is currently unused. Use PlayerWeaponThrow instead.")]
public class PlayerBow : MonoBehaviour
{
    private void Awake()
    {
        Debug.LogWarning($"{nameof(PlayerBow)} is currently unused. Use {nameof(PlayerWeaponThrow)} instead.", this);
        enabled = false;
    }
}
