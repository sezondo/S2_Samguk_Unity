using System;
using UnityEngine;

/// <summary>병종 대신 사용 가능한 공격과 계획 평가 성향을 지정한다.</summary>
[Serializable]
public class EnemyCombatSettings
{
    // 이 유닛이 계획에 포함할 수 있는 공격 종류다.
    public bool allowMelee;
    public bool allowRanged = true;
    // 이동 한 구간과 각 공격의 AP 비용이다. 무한 행동 방지를 위해 양수만 허용한다.
    public int moveCost = 1;
    public int meleeCost = 1;
    public int rangedCost = 1;
    // 근접·원거리가 공유하는 턴당 공격 횟수 한도와 근접 피해량이다.
    public int maximumAttacks = 1;
    public int meleeDamage = 2;
    // 예상 실피해·처치 확률·공격 성립에 부여하는 점수다.
    public float damageWeight = 10;
    public float killWeight = 35;
    public float attackWeight = 3;
    // 공격 대상의 가까움·잃은 체력 비율을 선호하는 점수다.
    public float targetDistanceWeight = 2;
    public float woundedTargetWeight = 4;
    // 다음 공격 위치까지 실제 경로가 단축되는 이득이다.
    public float approachWeight = 3;
    // 남은 위협 전체에 대한 엄폐·예상 피격·선호 거리 이탈 점수다.
    public float coverWeight = 8;
    public float dangerWeight = 6;
    public float preferredDistance = 4;
    public float distanceWeight = 1;
    // 소비 AP와 이동 칸 수를 감점해 의미 없는 왕복을 억제한다.
    public float actionPointPenalty = 0.2f;
    public float movementPenalty = 0.1f;
    // AP 깊이별 유지할 상위 상태 수다. 탐색량을 제한하는 빔 탐색 설정이다.
    public int beamWidth = 128;
}
