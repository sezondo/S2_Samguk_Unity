using UnityEngine;

// 검 오브젝트에 붙는 PlayerWeapon 카테고리 참조 주머니다.
// 자동 탐색하지 않으므로 필요한 참조는 Unity 인스펙터에서 직접 연결한다.
// 로직이나 튜닝 수치를 가지지 않고, 검 비주얼 계열 컴포넌트가 공유할 참조만 들고 있다.
public class PlayerWeaponContext : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private PlayerContext player;
    [SerializeField] private Transform playerRoot;
    [SerializeField] private Transform weaponAnchorRoot;

    [Header("Weapon Visual")]
    [SerializeField] private PlayerWeaponVisualFSM visualFsm;
    [SerializeField] private PlayerWeaponVisualMotion motion;
    [SerializeField] private PlayerWeaponVisualPresentation presentation;
    [SerializeField] private GameObject visualRoot;
    [SerializeField] private Animator animator;
    [SerializeField] private SpriteRenderer weaponRenderer;

    public Transform WeaponTransform => transform;
    public PlayerContext Player => player;
    public Transform PlayerRoot => playerRoot;
    public Transform WeaponAnchorRoot => weaponAnchorRoot;
    public PlayerWeaponVisualFSM VisualFsm => visualFsm;
    public PlayerWeaponVisualMotion Motion => motion;
    public PlayerWeaponVisualPresentation Presentation => presentation;
    public GameObject VisualRoot => visualRoot;
    public Animator Animator => animator;
    public SpriteRenderer WeaponRenderer => weaponRenderer;

    public bool HasWeaponVisualRequiredReferences()
    {
        return player != null
            && player.HasWeaponVisualRequiredReferences()
            && playerRoot != null
            && motion != null
            && presentation != null;
    }
}
