using UnityEngine;


[CreateAssetMenu(menuName = "Scriptable/PlayerData", fileName = "PlayerData")]

public class PlayerData : ScriptableObject
{
    public int maxHp;
    // 현재 플레이어가 사용할 화살 종류 번호.
    // 실제 번호 해석과 장착 로직은 나중에 인벤토리/장비 시스템에서 담당한다.
    public int equippedArrowTypeId;
    public float attackIntersection;
    public float moveSpeed;
    public float attackSpeed;
    public float rotationSpeed;
    public float RotationThreshold;
    public GameObject bulletPrefab;
    public AudioClip dieAudioClip;
    public AudioClip attackAudioClip;


}
