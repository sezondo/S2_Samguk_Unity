using System;
using UnityEngine;
using UnityEngine.Serialization;


[CreateAssetMenu(menuName = "Scriptable/PlayerData", fileName = "PlayerData")]

public class PlayerData : ScriptableObject
{
    public int maxHp;
    // 피격 후 추가 데미지를 막는 시간. PlayerHealth의 HitJudgment 유지 시간으로 사용한다.
    public float invincibleDuration = 0.6f;

    [Header("Weapon Throw")]
    public PlayerWeaponThrowData weaponThrow = new();

    [Header("Dodge")]
    public PlayerDodgeData dodge = new();

    [Header("Hack")]
    public PlayerHackData hack = new();

    public float attackIntersection;
    public float moveSpeed;
    public float attackSpeed;
    public float rotationSpeed;
    public float RotationThreshold;
    public AudioClip dieAudioClip;
    public AudioClip attackAudioClip;
}

[Serializable]
public class PlayerWeaponThrowData
{
    // 우클릭 조준을 최대로 인정하는 시간.
    public float maxChargeTime = 1.2f;
    // 이 시간 전에 우클릭을 떼면 투척하지 않고 취소한다.
    public float minAimHoldTime = 0.15f;
    // WeaponAiming 상태에 진입했을 때 최소로 유지할 짧은 실행 시간.
    [FormerlySerializedAs("throwStateDuration")]
    public float aimingStateDuration = 0.08f;
    // WeaponThrowing 상태를 유지할 짧은 실행 시간.
    [FormerlySerializedAs("thrownStateDuration")]
    public float throwingStateDuration = 0.08f;
    // 조준 시간이 짧을 때 적용할 최대 오차 각도.
    public float maxSpreadAngle = 12f;
    // 충분히 조준했을 때 남길 최소 오차 각도.
    public float minSpreadAngle = 0.5f;
    public float throwSpeed = 12f;
    public float returnSpeed = 16f;
    public float maxDistance = 6f;
    public int damage = 1;
}

[Serializable]
public class PlayerDodgeData
{
    public float duration = 0.18f;
    public float speed = 10f;
}

[Serializable]
public class PlayerHackData
{
    // 이 거리보다 해킹 대상에 박힌 검과 멀어지면 해킹을 취소한다.
    public float maxHackDistance = 5f;
}
