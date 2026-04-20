using UnityEngine;

[CreateAssetMenu(menuName = "Scriptable/ArrowData", fileName = "ArrowData")]
public class ArrowData : ScriptableObject
{
    // PlayerData.equippedArrowTypeId와 매칭되는 화살 종류 번호.
    public int arrowTypeId;

    // 이 타입으로 발사할 실제 화살 프리팹.
    // 일반/화염/독/번개 화살처럼 외형이나 충돌 구성이 다를 때 여기서 프리팹을 나눈다.
    public Arrow arrowPrefab;

    // 타입별 기본 투사체 속도. 필요하면 화살 종류마다 다르게 줄 수 있다.
    public float projectileSpeed = 12f;

    // 테스트용 데미지. 나중에 전투 데이터가 분리되면 그쪽으로 옮길 수 있다.
    public int damage = 1;
}
