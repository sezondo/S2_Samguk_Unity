using UnityEngine;

[CreateAssetMenu(menuName = "Scriptable/MeleeComboData", fileName = "MeleeComboData")]
public class MeleeComboData : ScriptableObject
{
    // 이 콤보가 순서대로 사용할 근접 공격 데이터 목록이다.
    // Element 0은 1타, Element 1은 2타처럼 해석한다.
    public PlayerMeleeAttackData[] attacks;
}
