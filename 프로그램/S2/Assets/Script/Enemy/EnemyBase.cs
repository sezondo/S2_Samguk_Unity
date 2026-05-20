using UnityEngine;

[RequireComponent(typeof(EnemyContext))]
public class EnemyBase : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private EnemyData enemyData;

    public EnemyData Data => enemyData;

    private void Awake()
    {
        if (enemyData == null)
        {
            Debug.LogError($"{nameof(EnemyBase)} on {name} requires {nameof(EnemyData)}.", this);
            enabled = false;
        }
    }
}
