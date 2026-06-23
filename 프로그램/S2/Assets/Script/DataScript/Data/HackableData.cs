using UnityEngine;

/// <summary>
/// 해킹 가능한 대상의 튜닝 데이터를 보관하는 에셋이다.
/// 실제 유효성 검사는 데이터를 사용하는 컴포넌트가 담당한다.
/// </summary>
[CreateAssetMenu(menuName = "Scriptable/HackableData", fileName = "HackableData")]
public class HackableData : ScriptableObject
{
    // 전자 부적 삽입에 걸리는 시간이다. 실제 타이머는 PlayerHackController가 돌린다.
    public float hackDuration = 1.5f;

    public float HackDuration => hackDuration;
}
