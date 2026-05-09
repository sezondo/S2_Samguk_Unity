using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerContext))]
public class PlayerDebugOverlay : MonoBehaviour
{
    [SerializeField] private bool showOverlay = true;
    [SerializeField] private Vector2 screenPosition = new(12f, 12f);
    [SerializeField] private int fontSize = 16;

    private PlayerFSMManager fsm;
    private PlayerWeaponThrow weaponThrow;
    private PlayerHealth health;

    private GUIStyle labelStyle;

    private void Awake()
    {
        PlayerContext context = GetComponent<PlayerContext>();
        context.ResolveReferences();

        fsm = context.Fsm;
        weaponThrow = context.WeaponThrow;
        health = context.Health;
    }

    private void OnGUI()
    {
        if (!showOverlay)
        {
            return;
        }

        EnsureStyle();

        Rect area = new(screenPosition.x, screenPosition.y, 500f, 200f);
        GUILayout.BeginArea(area, GUI.skin.box);
        GUILayout.Label($"State: {GetStateText()}", labelStyle);
        GUILayout.Label($"Has Weapon: {GetHasWeaponText()}", labelStyle);
        GUILayout.Label($"HP: {GetHealthText()}", labelStyle);
        GUILayout.EndArea();
    }

    private void EnsureStyle()
    {
        if (labelStyle != null)
        {
            return;
        }

        labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = fontSize,
            normal =
            {
                textColor = Color.white,
            },
        };
    }

    private string GetStateText()
    {
        return fsm != null ? fsm.CurrentState.ToString() : "None";
    }

    private string GetHasWeaponText()
    {
        return weaponThrow != null ? weaponThrow.HasWeapon.ToString() : "None";
    }

    private string GetHealthText()
    {
        return health != null ? $"{health.CurrentHp}/{health.MaxHp}" : "None";
    }
}
