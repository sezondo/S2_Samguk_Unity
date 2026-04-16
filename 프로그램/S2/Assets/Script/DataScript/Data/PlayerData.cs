using UnityEngine;


[CreateAssetMenu(menuName = "Scriptable/PlayerData", fileName = "PlayerData")]

public class PlayerData : ScriptableObject
{
    public int maxHp;
    // 현재 플레이어가 사용할 화살 종류 번호.
    // 실제 번호 해석과 장착 로직은 나중에 인벤토리/장비 시스템에서 담당한다.
    public int equippedArrowTypeId;

    // 플레이어가 사용할 수 있는 화살 데이터 목록.
    // equippedArrowTypeId와 같은 arrowTypeId를 가진 ArrowData를 찾아 발사에 사용한다.
    public ArrowData[] arrowDataList;
    public float attackIntersection;
    public float moveSpeed;
    public float attackSpeed;
    public float rotationSpeed;
    public float RotationThreshold;
    public AudioClip dieAudioClip;
    public AudioClip attackAudioClip;

    public ArrowData GetEquippedArrowData()
    {
        // 현재 장착된 화살 타입에 맞는 ArrowData를 찾아 반환한다.
        if (arrowDataList == null)
        {
            return null;
        }

        foreach (ArrowData arrowData in arrowDataList)
        {
            if (arrowData != null && arrowData.arrowTypeId == equippedArrowTypeId)
            {
                return arrowData;
            }
        }

        return null;
    }
}
