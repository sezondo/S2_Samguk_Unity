using System;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(menuName = "Scriptable/PlayerWeaponVisualData", fileName = "PlayerWeaponVisualData")]
public class PlayerWeaponVisualData : ScriptableObject
{
    [Header("Motion Anchor")]
    // 모든 검 비주얼 위치 계산의 기준점이다.
    // 플레이어 피벗이 발밑/몸 중앙이어도 검은 이 기준점을 중심으로 조준 반대 방향, 부유 위치, 공격 위치를 잡는다.
    public Vector2 weaponAnchorOffset = new(0f, 0.48f);

    [Header("Orbit")]
    public WeaponOrbitVisualData orbit = new();

    [Header("Aiming")]
    public WeaponAimingVisualData aiming = new();

    [Header("Melee")]
    public WeaponMeleeVisualData melee = new();

    [Header("Throw")]
    public WeaponThrowVisualData throwVisual = new();

    [Header("Presentation")]
    public WeaponPresentationVisualData presentation = new();

    [Header("Motion")]
    public WeaponMotionVisualData motion = new();
}

[Serializable]
public class WeaponOrbitVisualData
{
    // weaponAnchorOffset 기준으로 평소 검이 머무를 위치다.
    // x는 등 뒤 기준 좌우 보정, y는 플레이어가 바라보는 방향의 반대쪽으로 떨어지는 거리다.
    // y가 0이면 PlayerWeaponVisualMotion의 기본 등뒤 거리를 사용한다.
    public Vector2 centerOffset = new(0f, 0f);
    // 기준 위치 주변에서 작게 흔들리는 폭이다. 0에 가까울수록 고정 부유에 가깝다.
    public float radius = 0.12f;
    // 작은 흔들림의 속도다. 기존 데이터 호환을 위해 이름은 유지한다.
    public float degreesPerSecond = 65f;
    public float moveLagDistance = 0.12f;
    public float floatTiltAmount = 5f;
    public float floatTiltSpeed = 2.4f;
}

[Serializable]
public class WeaponAimingVisualData
{
    // weaponAnchorOffset 기준에서 조준 방향 반대쪽으로 물러나는 거리다.
    public float pullBackDistance = 0.48f;
    // 조준선과 겹치지 않게 수직 방향으로 밀어주는 거리다.
    public float sideOffset = 0.08f;
    public float perpendicularAngle = 90f;
    public VfxId chargedVfxId = VfxId.None;
}

[Serializable]
public class WeaponMeleeVisualData
{
    // PlayerMeleeAttackData.hitboxOffset 기준에서 검 비주얼만 살짝 보정하는 값이다.
    // x는 공격 방향 앞/뒤, y는 공격 방향 기준 좌/우 보정이다.
    [FormerlySerializedAs("moveOffset")]
    public Vector2 visualOffset = Vector2.zero;
    public VfxId moveVfxId = VfxId.None;
}

[Serializable]
public class WeaponThrowVisualData
{
    public VfxId startVfxId = VfxId.None;
    // 보이는 검과 판정용 투척체가 이 거리 이상 벌어지면 즉시 투척체 위치로 붙인다.
    public float snapDistance = 1.5f;
}

[Serializable]
public class WeaponPresentationVisualData
{
    // 현재는 숨김/재등장 VFX 호출을 의도적으로 사용하지 않는다.
    // 나중에 필요하면 PlayerWeaponVisualPresentation의 주석 처리된 호출부를 다시 열고 사용한다.
    public VfxId vanishVfxId = VfxId.WeaponVanish;
    public VfxId reappearVfxId = VfxId.WeaponReappear;
}

[Serializable]
public class WeaponMotionVisualData
{
    public float followSharpness = 18f;
    public float fastFollowSharpness = 55f;
    public float rotateSharpness = 24f;
    // 오른쪽이 손잡이, 왼쪽이 칼날인 스프라이트면 보통 180이 맞다.
    public float baseAngleOffset = 180f;
}
