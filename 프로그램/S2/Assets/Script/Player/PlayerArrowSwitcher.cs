using UnityEngine;

[RequireComponent(typeof(PlayerInput))]
[RequireComponent(typeof(PlayerLoadout))]
public class PlayerArrowSwitcher : MonoBehaviour
{
    private PlayerInput input;
    private PlayerLoadout loadout;

    private void Awake()
    {
        input = GetComponent<PlayerInput>();
        loadout = GetComponent<PlayerLoadout>();

        if (input == null || loadout == null)
        {
            Debug.LogError($"{nameof(PlayerArrowSwitcher)} on {name} is missing a required component.", this);
            enabled = false;
        }
    }

    private void Update()
    {
        if (input.PreviousArrowPressedThisFrame)
        {
            EquipPreviousArrow();
        }

        if (input.NextArrowPressedThisFrame)
        {
            EquipNextArrow();
        }
    }

    private void EquipPreviousArrow()
    {
        if (!loadout.EquipPreviousArrow())
        {
            Debug.LogWarning($"{nameof(PlayerArrowSwitcher)} could not equip the previous arrow.", this);
            return;
        }

        LogEquippedArrow();
    }

    private void EquipNextArrow()
    {
        if (!loadout.EquipNextArrow())
        {
            Debug.LogWarning($"{nameof(PlayerArrowSwitcher)} could not equip the next arrow.", this);
            return;
        }

        LogEquippedArrow();
    }

    private void LogEquippedArrow()
    {
        ArrowData equippedArrow = loadout.EquippedArrow;
        if (equippedArrow == null)
        {
            Debug.LogWarning($"{nameof(PlayerArrowSwitcher)} switched arrow, but equipped arrow data was not found.", this);
            return;
        }

        Debug.Log($"Equipped arrow type: {equippedArrow.arrowTypeId}", this);
    }
}
