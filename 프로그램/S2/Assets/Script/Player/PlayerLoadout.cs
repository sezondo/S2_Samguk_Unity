using UnityEngine;

public class PlayerLoadout : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private PlayerData playerData;

    [Header("Melee")]
    [SerializeField] private MeleeComboData meleeComboData;

    public PlayerData PlayerData => playerData;
    public MeleeComboData MeleeComboData => meleeComboData;
    public PlayerMeleeAttackData[] MeleeComboAttacks => meleeComboData != null ? meleeComboData.attacks : null;
}
