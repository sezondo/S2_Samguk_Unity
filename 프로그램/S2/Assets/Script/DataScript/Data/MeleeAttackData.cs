using UnityEngine;

[CreateAssetMenu(menuName = "Scriptable/MeleeAttackData", fileName = "MeleeAttackData")]
public class MeleeAttackData : ScriptableObject
{
    [Header("Damage")]
    // 이 공격이 EnemyHealthTest 같은 피해 대상에게 줄 기본 데미지.
    public int damage = 1;

    [Header("Timing")]
    // 공격 상태가 유지되는 전체 시간. 이 시간이 끝나면 다음 공격 또는 Idle로 넘어간다.
    public float attackDuration = 0.25f;
    // 공격 시작 후 몇 초 뒤에 실제 판정을 켤지 정한다.
    public float hitboxStartTime = 0f;
    // 실제 판정 콜라이더가 켜져 있는 시간.
    public float hitboxActiveTime = 0.08f;
    // 입력을 미리 받아둘 수 있는 시간. 조작감 조정용이다.
    public float inputBufferDuration = 0.15f;
    // 공격 종료 후 콤보 흐름을 유지할 수 있는 시간. 정책은 나중에 조정한다.
    public float comboExpireTime = 0.45f;

    [Header("Movement")]
    // 공격 중 플레이어가 공격 방향으로 전진할 총 거리다. 0이면 이동하지 않는다.
    public float advanceDistance = 0f;
    // 공격 시작 후 몇 초 뒤부터 전진을 시작할지 정한다.
    public float advanceStartTime = 0f;
    // 전진 거리를 몇 초에 걸쳐 이동할지 정한다. 0이면 한 번에 이동한다.
    public float advanceDuration = 0.08f;
    // 꺼져 있으면 공격 방향으로 이동하고, 켜져 있으면 공격 반대 방향으로 이동한다.
    // 무기 반동이나 뒤로 빠지는 공격을 만들 때 사용한다.
    public bool moveBackwardByAdvance = false;

    [Header("Hitbox")]
    // x는 공격 방향 앞쪽 거리, y는 공격 방향의 수직 오프셋이다.
    // 예: (0.6, 0)은 플레이어 앞쪽 0.6만큼 떨어진 위치에 판정을 둔다.
    public Vector2 hitboxOffset = new(0.6f, 0f);
    // BoxCollider2D/CapsuleCollider2D는 size, CircleCollider2D는 가장 큰 축의 절반을 radius로 쓴다.
    public Vector2 hitboxSize = new(0.8f, 0.5f);
    // 켜두면 사각형/캡슐형 Hitbox가 공격 방향을 바라보게 회전한다.
    public bool rotateHitboxToAim = true;

    [Header("Debug")]
    // 공격 판정이 켜지는 순간 Debug.DrawLine으로 범위를 그릴지 정한다.
    public bool drawDebug = true;
    public Color debugColor = Color.red;
    public float debugDrawDuration = 0.08f;
}
