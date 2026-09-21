# 적 AP AI 튜닝 가이드

2026-09-21 기준. `Assets/Data/TestStage/Enemy`의 적 데이터 에셋에서 조절한다.

## 기본 자원과 공격 종류

| 항목 | 의미 | 초기값 |
|---|---|---|
| Turn Action Point | 턴 시작 AP | 3 |
| Turn Move Range | 이동 한 번에 최대 이동 칸 | 3 |
| Combat / Allow Melee, Allow Ranged | 사용할 수 있는 공격 | 망치: 근접 / 큰못: 원거리 / 작은못: 둘 다 |
| Move Cost, Melee Cost, Ranged Cost | 각 행동의 AP 비용 | 각각 1 |
| Maximum Attacks | 근접·원거리 합산 턴당 공격 한도 | 1, 엘리트는 2 등 |
| Melee Damage | 근접 피해 | 2 |
| Ranged Attack Range, Damage | 원거리 사거리·피해 | 테스트 6칸·1 |
| Ranged Attack Accuracy | 명중률과 낮은 엄폐 페널티 | 기존 데이터 사용 |

비용은 1 이상이다. AP와 공격 한도는 독립적이다. 1회 공격한 일반 적도 남은 AP로 이동할 수 있다.

## 성향 조정

| Combat 항목 | 높이면 |
|---|---|
| Damage Weight | 예상 실피해를 더 중시 |
| Kill Weight | 이번 턴 처치 가능한 대상을 더 중시 |
| Attack Weight | 공격이 성립하는 계획을 더 중시 |
| Target Distance Weight | 판단 시작 때 가까운 표적 선호 |
| Wounded Target Weight | 잃은 체력 비율이 높은 표적 선호 |
| Approach Weight | 실제 경로상 다음 공격 위치로 접근 선호 |
| Danger Weight | 여러 적대 표적에게 받을 수 있는 피해 회피 |
| Cover Weight | 생존 표적 방향에 유효한 엄폐 선호 |
| Preferred Distance / Distance Weight | 지정 거리 유지 / 이탈 감점 강화 |
| Action Point Penalty / Movement Penalty | AP 절약 / 이동 칸 절약 |
| Beam Width | 더 많은 계획을 유지, 계산량 증가 |

망치는 낮은 위험·엄폐 가중치와 거리 유지 감점 0으로 접근 공격에 무게를 준다. 큰못은 위험과 엄폐를 중시한다. 작은못은 원거리와 근접 계획을 함께 비교한다. 근접 후 복귀를 필수로 요구하지 않으므로 근접 처치 이득이 크면 노출을 감수할 수 있다.

예: 원거리 피해 4, 근접 피해 5, 상대 HP 5라면 Kill Weight가 근접 처치에 보너스를 준다. 상대 HP가 충분하고 근접 후 노출 위험이 크면 Danger Weight가 원거리 계획에 유리하게 작용한다. 이동→근접→엄폐 복귀가 가능한 AP가 있으면 다시 근접 계획이 유리해질 수 있다.

## 확인 방법

1. `EnemyContext`가 참조하는 Enemy Data를 확인한다. 공유 에셋을 수정하면 같은 에셋을 쓰는 적 모두에 적용된다.
2. AP, 공격 가능 종류, 공격 한도를 먼저 정한 뒤 피해·처치·위험·엄폐 순으로 조정한다.
3. `EnemyTurnAgent`의 Log Turn Action을 켜고 콘솔에서 계획과 점수 내역을 본다. 표적 번호는 해당 판단의 살아 있는 표적 목록 순서다.
4. 저장된 `BattleTest01`에서 `Tools/S2/Verify Enemy AP AI`를 실행하면 계획 시나리오와 실제 전투 연결 검증을 반복할 수 있다.

계획기는 탐색 폭을 제한한 빔 탐색이다. AP 0~12, 탐색 폭 1~1024 범위에서 검증하며 초기값 3AP/128을 권장한다. 위험 점수는 상대의 현재 위치에서 가능한 공격을 평가하며 상대의 다음 턴 전체를 예측하지 않는다. 가상 계획에서는 사망 확률을 반영하지만 점유 해제는 실제 사망 처리 후 다음 판단부터 반영한다.

사격 장애물 규칙은 현재 플레이어와 동일한 합산 시야 방식이다. 아군이 보고 있는 표적은 내 사거리 안이면 공격할 수 있다. 향후 자기 총구부터의 별도 탄도 차단을 도입하려면 플레이어·적 규칙을 함께 바꿔야 한다.

## 논리 생산과 연출 재생 (2026-09-21)

적 행동의 피해·사망·점유·시야 등 파생 논리를 확정한 뒤 다음 판단을 시작한다. 앞 행동의 애니메이션 완료는 기다리지 않는다. 연출은 발생 순서대로 재생하며 마지막 연출까지 끝난 뒤 플레이어 턴으로 돌아온다.

EnemyTurnCoordinator 로그는 최근 턴 논리 합계, 단일 행동 최대 시간, 판단 횟수를 보여 준다. 누적 4ms 이상이면 다음 행동 전에 프레임을 양보하지만 단일 Plan은 나누지 않는다. 적 수·표적 수·경로 수·Beam Width가 증가하면 긴 판단이 프레임을 막을 수 있다. 배포 성능은 대상 장치에서 별도로 확인한다.

Tools/S2/Verify Enemy Queue Flow는 저장된 BattleTest01의 플레이 사본에 적 11명을 구성해 논리 선행·연출 순서·HP·입력·결과 UI를 검사한다. 씬과 튜닝 에셋 원본을 저장하지 않으며 결과는 Temp/QueueFlow/verification.txt다.
