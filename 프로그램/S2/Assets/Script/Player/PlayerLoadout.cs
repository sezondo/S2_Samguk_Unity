using UnityEngine;

public class PlayerLoadout : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private PlayerData playerData;

    [Header("Arrow")]
    [SerializeField] private ArrowDataTable arrowDataTable;
    [SerializeField] private int[] ownedArrowTypeIds;
    [SerializeField] private int equippedArrowIndex;

    [Header("Melee")]
    [SerializeField] private MeleeComboData meleeComboData;

    public PlayerData PlayerData => playerData;
    public ArrowDataTable ArrowDataTable => arrowDataTable;
    public MeleeComboData MeleeComboData => meleeComboData;
    public int[] OwnedArrowTypeIds => ownedArrowTypeIds;
    public int EquippedArrowIndex => equippedArrowIndex;
    public PlayerMeleeAttackData[] MeleeComboAttacks => meleeComboData != null ? meleeComboData.attacks : null;

    public ArrowData EquippedArrow
    {
        get
        {
            if (!HasOwnedArrows() || arrowDataTable == null)
            {
                return null;
            }

            equippedArrowIndex = Mathf.Clamp(equippedArrowIndex, 0, ownedArrowTypeIds.Length - 1);
            return arrowDataTable.FindByTypeId(ownedArrowTypeIds[equippedArrowIndex]);
        }
    }

    public bool EquipArrow(int index)
    {
        if (!HasOwnedArrows() || index < 0 || index >= ownedArrowTypeIds.Length)
        {
            return false;
        }

        equippedArrowIndex = index;
        return true;
    }

    public bool EquipNextArrow()
    {
        if (!HasOwnedArrows())
        {
            return false;
        }

        equippedArrowIndex = (equippedArrowIndex + 1) % ownedArrowTypeIds.Length;
        return true;
    }

    public bool EquipPreviousArrow()
    {
        if (!HasOwnedArrows())
        {
            return false;
        }

        equippedArrowIndex--;
        if (equippedArrowIndex < 0)
        {
            equippedArrowIndex = ownedArrowTypeIds.Length - 1;
        }

        return true;
    }

    public bool EquipArrowByTypeId(int arrowTypeId)
    {
        if (!HasOwnedArrows())
        {
            return false;
        }

        for (int i = 0; i < ownedArrowTypeIds.Length; i++)
        {
            if (ownedArrowTypeIds[i] == arrowTypeId)
            {
                equippedArrowIndex = i;
                return true;
            }
        }

        return false;
    }

    private bool HasOwnedArrows()
    {
        return ownedArrowTypeIds != null && ownedArrowTypeIds.Length > 0;
    }
}
