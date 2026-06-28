using UnityEngine;

/// <summary>
/// 피해를 받을 수 있는 액터의 기본 HP 컴포넌트다.
/// 현재는 HP 감소와 로그만 담당하며, 이후 사망 처리와 피격 연출 연결 지점으로 확장한다.
/// </summary>
public class ActorHealth : MonoBehaviour, IDamageable
{
    [Header("Health")]
    // 액터가 가질 수 있는 최대 HP다.
    [SerializeField] private int maxHitPoint = 5;
    // 현재 남아 있는 HP다.
    [SerializeField] private int currentHitPoint = 5;

    [Header("Log")]
    // true면 피해 적용과 전투불능 전환 로그를 출력한다.
    [SerializeField] private bool logDamage = true;

    public int MaxHitPoint => maxHitPoint;
    public int CurrentHitPoint => currentHitPoint;
    public bool IsDead => currentHitPoint <= 0;

    /// <summary>
    /// 인스펙터에서 HP 값이 잘못 들어가지 않게 보정한다.
    /// </summary>
    private void OnValidate()
    {
        maxHitPoint = Mathf.Max(1, maxHitPoint);
        currentHitPoint = Mathf.Clamp(currentHitPoint, 0, maxHitPoint);
    }

    /// <summary>
    /// 지정한 피해량만큼 현재 HP를 줄인다.
    /// </summary>
    public bool TakeDamage(int damage)
    {
        if (damage <= 0)
        {
            Debug.LogError($"{nameof(ActorHealth)} on {name}에는 0보다 큰 피해량만 적용할 수 있습니다. 입력 피해량: {damage}", this);
            return false;
        }

        if (IsDead)
        {
            if (logDamage)
            {
                Debug.Log($"{nameof(ActorHealth)}: {name} 대상은 이미 전투불능이라 피해를 무시합니다.", this);
            }

            return false;
        }

        currentHitPoint = Mathf.Max(0, currentHitPoint - damage);

        if (logDamage)
        {
            Debug.Log($"{nameof(ActorHealth)}: {name} 대상이 {damage} 피해를 받았습니다. 현재 HP: {currentHitPoint}/{maxHitPoint}", this);
        }

        if (IsDead && logDamage)
        {
            Debug.Log($"{nameof(ActorHealth)}: {name} 대상이 전투불능 상태가 됐습니다.", this);
        }

        return true;
    }
}
