using UnityEngine;

[CreateAssetMenu(menuName = "Scriptable/HackableData", fileName = "HackableData")]
public class HackableData : ScriptableObject
{
    // 전자 부적 삽입에 걸리는 시간이다. 실제 타이머는 PlayerHackController가 돌린다.
    public float hackDuration = 1.5f;
}
