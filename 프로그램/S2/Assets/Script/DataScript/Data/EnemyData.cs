using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Scriptable/EnemyData", fileName = "EnemyData")]
public class EnemyData : ScriptableObject
{
    [Header("Health")]
    public int maxHp = 3;
    public bool destroyOnDeath = true;
    public float hitStateDuration = 0.12f;

    [Header("Movement")]
    public float moveSpeed = 2f;
    public float detectionRange = 4f;
    public float attackRange = 0.8f;
    public float stopDistance = 0.6f;

    [Header("Sight")]
    public float viewAngle = 90f;
    public LayerMask sightBlockLayers;
    public bool drawSightDebug = true;
    public Color sightDebugColor = Color.cyan;

    [Header("Attack")]
    public EnemyMeleeAttackData meleeAttack = new();
}

[Serializable]
public class EnemyMeleeAttackData
{
    public int damage = 1;
    public float attackDuration = 0.45f;
    public float hitboxStartTime = 0.18f;
    public float hitboxActiveTime = 0.12f;
    public float cooldown = 0.6f;

    [Header("Hitbox")]
    public Vector2 hitboxOffset = new(0.55f, 0f);
    public Vector2 hitboxSize = new(0.7f, 0.45f);
    public bool rotateHitboxToAim = true;

    [Header("Debug")]
    public bool drawDebug = true;
    public Color debugColor = Color.yellow;
    public float debugDrawDuration = 0.08f;
}
