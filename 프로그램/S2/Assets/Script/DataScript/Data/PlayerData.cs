using UnityEngine;


[CreateAssetMenu(menuName = "Scriptable/PlayerData", fileName = "PlayerData")]

public class PlayerData : ScriptableObject
{
    public int maxHp;
    // 피격 후 추가 데미지를 막는 시간. PlayerHealth의 HitJudgment 유지 시간으로 사용한다.
    public float invincibleDuration = 0.6f;

    public float attackIntersection;
    public float moveSpeed;
    public float attackSpeed;
    public float rotationSpeed;
    public float RotationThreshold;
    public AudioClip dieAudioClip;
    public AudioClip attackAudioClip;
}
