# S2-T 현재 구현 구조

- 최신 기준: 2026-07-23
- 기준 브랜치: `main`
- Unity 버전: `6000.0.64f1`
- 기준 테스트 씬: `Assets/Scenes/Tset.unity`

이 문서는 S2-T의 **현재 실제 코드와 씬 구조만** 설명하는 최신 스냅샷이다.
날짜별 작업 과정, 이전 설계, 제거된 구조와 테스트 이력은 `S2-T 작업일지.md`에서 관리한다.

## 1. 프로젝트 현재 방향

S2-T는 사이버 조선 세계관을 사용하는 보드게임식 턴제 잠입 퍼즐·소규모 전술 게임이다.

현재 플레이 가능한 핵심 루프는 다음과 같다.

1. 플레이어 진영의 여러 유닛 중 하나를 선택한다.
2. AP를 사용해 이동, 해킹, 검 투척·회수, 근접 공격, 총 공격을 실행한다.
3. 이동 경로가 적 시야에 들어가면 적이 경계 상태로 전환되고 주변 적에게 애드가 전파된다.
4. 경계 상태가 된 적은 즉시 엄폐 반응 이동을 할 수 있다.
5. 적 턴에는 경계 상태의 적이 플레이어를 공격하거나 유리한 엄폐 위치로 이동한다.
6. 목표 칸에 살아 있는 플레이어 조작 유닛이 도착하면 스테이지가 클리어된다.

현재 작업 방향은 `Tset` 테스트 씬에서 튜토리얼 1스테이지의 핵심 진행을 먼저 완성한 뒤 실제 튜토리얼 씬으로 옮기는 것이다.
해킹 터미널과 보안문의 프리팹화를 마쳤으며, 다음 제작 단위는 이 오브젝트를 사용하는 튜토리얼 진행 순서와 안내 구조 설계다.

## 2. 핵심 구조 원칙

### 논리와 연출 분리

- 이동, 피해, 사망, 해킹, 문 개방 같은 게임 결과는 논리 계층에서 먼저 확정한다.
- 논리 결과는 `PresentationEvent`로 변환해 `ActionPresentationQueue`에 넣는다.
- Presenter는 화면 연출만 처리하며 AP, HP, 그리드 점유, 경계 상태 같은 논리 값을 직접 바꾸지 않는다.
- 연출 중에는 새 행동 실행을 막고, 각 Presenter가 `PresentationEventHandle.Complete()`를 호출해야 다음 연출로 진행한다.

### Context 참조

- 플레이어 전술 유닛은 `TacticalUnitContext`에 핵심 컴포넌트 참조를 모은다.
- 적은 `EnemyContext`에 핵심 컴포넌트 참조를 모은다.
- Context는 참조 주머니이며 튜닝 수치나 상태 변경 정책을 갖지 않는다.
- 같은 루트의 핵심 컴포넌트를 기능 컴포넌트가 임의 `GetComponent<T>()`로 보정하지 않는다.
- Context에서 얻은 컴포넌트 상태는 해당 컴포넌트의 요청 메서드로 변경한다.

### 필수 참조와 데이터 검증

- 필수 데이터나 참조가 없으면 임의 fallback으로 실행하지 않는다.
- 참조 오류는 `HasValidReference()`, 값 오류는 `HasValidData()`에서 한국어 오류로 알린다.
- 치명적인 구성 오류가 있으면 해당 컴포넌트를 비활성화하거나 행동 진입을 중단한다.
- `ControllableUnitData`, `EnemyData` 같은 튜닝 에셋 자체가 아니라 데이터를 사용하는 런타임 컴포넌트가 필요한 값만 검사한다.

## 3. 코드 폴더 책임

- `Assets/Script/Grid`: 그리드 좌표, 셀 상태, 점유, 좌표 변환, BFS 경로 탐색.
- `Assets/Script/Turn`: 플레이어·적 턴과 AP 관리.
- `Assets/Script/Unit`: 공통 전술 유닛 규약, 진영, 등록소, 플레이어 제어권과 적 표적 선택.
- `Assets/Script/TurnAction/Core`: 논리 이벤트, 논리 이벤트 버스, 행동 해석 문맥.
- `Assets/Script/TurnAction/Grid`: 이동 범위·경로·위험도 계산과 하이라이트.
- `Assets/Script/TurnAction/Input`: 현재 조작 유닛에 입력을 전달하는 통합 입력 계층.
- `Assets/Script/TurnAction/Player`: 플레이어 이동·해킹·검·근접·총 행동과 행동 흐름.
- `Assets/Script/Player`: 원시 입력, 총알 상태, 검 위치 상태.
- `Assets/Script/Enemy`: 적 Context, 시야, 경계, 엄폐 반응, 적 턴 AI와 공격.
- `Assets/Script/Combat`: HP, 피해 결과, 해킹 대상, 피해·사망 논리 처리자.
- `Assets/Script/Presentation`: 연출 이벤트, 연출 큐, Actor 연결 등록소.
- `Assets/Script/Presentation/Presenter`: 이동, 발각, 전투, 검, 해킹, 문 개방 연출 처리자.
- `Assets/Script/Presentation/Visual`: 실제 SpriteRenderer와 Animator 제어.
- `Assets/Script/Presentation/Data`: 이동·전투 연출 튜닝 에셋.
- `Assets/Script/Stage`: 목표, 스테이지 상태, 동적 보안문 논리.
- `Assets/Script/DataScript/Data`: 전술 유닛, 적, 해킹, 대사 데이터 에셋.
- `Assets/Script/Dialogue`: 말풍선 대사 시스템.
- `Assets/Script/Common`: 공용 VFX 풀.
- `Assets/Script/Camera`: 테스트 카메라 이동.
- `Assets/Script/Debug`: 런타임 정보와 편집용 점유 칸 표시.

## 4. Tset 씬 구성

### 씬 단위 시스템

`Manager` 오브젝트에는 다음 씬 단위 컴포넌트가 연결되어 있다.

- `GridManager`
- `TurnManager`
- `TacticalUnitRegistry`
- `EnemyRegistry`
- `HackableRegistry`
- `PlayerInputReader`
- `PlayerUnitControlManager`
- `PlayerUnitInputController`
- `PlayerUnitActionFlowController`
- `EnemyAlertCoordinator`
- `EnemyTurnCoordinator`
- `ActorPresentationRegistry`
- `CombatActionPresenter`
- `VfxManager`

`ActionPresentationQueue` 오브젝트에는 씬 단일 연출 큐와 현재 디버그 리시버·테스터가 있다.

### 맵 계층

```text
MapVisualGrid
├─ FloorTilemap
├─ ObjectTilemap
└─ LogicTilemap
```

- `FloorTilemap`: 바닥 화면 표시.
- `ObjectTilemap`: 벽과 장식 오브젝트 화면 표시.
- `LogicTilemap`: 고정 이동불가 칸의 단일 논리 원본.
- 화면용 타일맵과 논리 타일맵은 서로 독립적이다.
- 문처럼 런타임에 열리는 장애물은 `LogicTilemap`에 칠하지 않고 `GridActor` 점유로 막는다.

### 전술 유닛

- 플레이어 조작 유닛 2개가 `TacticalUnitContext`로 등록된다.
- 적 2개가 `EnemyContext`로 등록된다.
- 각 Actor는 논리 `GridActor`와 별도 `VisualRoot`를 가진다.
- `ActorPresentationBinding`이 논리 Actor와 해당 `ActorVisualController`를 연결한다.

### 기타 주요 오브젝트

- `HackTerminal`: 해킹 가능한 터미널.
- `SecurityDoor01`: 두 칸을 점유하는 동적 보안문.
- `StageGoal`: 목표 칸과 스테이지 상태 관리.
- `Debug`: `S2TDebugOverlay`, `GridActorOccupationDebugVisualizer`.
- `Main Camera`: `CameraKeyboardMover`.

## 5. Grid와 맵 규칙

### GridPosition

`GridPosition`은 월드 좌표와 분리된 정수형 보드 좌표다.

- `x`, `y`를 보관한다.
- 상하좌우 기본 방향과 덧셈·뺄셈·동등 비교를 지원한다.
- `ManhattanDistanceTo()`로 행동 거리와 전파 거리를 계산한다.

### GridCellState

각 칸은 다음 상태를 가진다.

- `IsBlocked`: `LogicTilemap`에서 읽은 고정 장애물 여부.
- `OccupiedActor`: 현재 칸을 점유한 `GridActor`.
- `CanEnter`: 고정 장애물도 없고 점유자도 없는지 여부.

### GridManager

`GridManager`는 씬 단일 보드 관리자다.

- 보드 크기, 셀 크기, 월드 원점을 관리한다.
- `GridToWorld()`, `WorldToGrid()`로 좌표를 변환한다.
- 논리 칸의 월드 위치를 `LogicTilemap.WorldToCell()`로 변환해 타일 유무를 읽는다.
- `RegisterActor()`, `UnregisterActor()`, `TryMoveActor()`로 동적 점유를 관리한다.
- `CanEnter()`, `IsBlocked()`, `IsOccupied()`, `TryGetActorAt()`을 제공한다.

현재 `Tset` 설정은 `16 x 16`, 셀 크기 `1`, 원점 `(0, 0, 0)`이다.

### GridActor

`GridActor`는 보드에 존재하는 논리 Actor의 최소 단위다.

- 직렬화된 `GridPosition`을 가진다.
- `OccupyCell`이 켜진 Actor는 활성화될 때 칸을 점유한다.
- `TryMoveTo()`와 `TryMoveBy()`는 `GridManager` 승인을 받아 이동한다.
- `ReleaseCellOccupation()`은 오브젝트를 남긴 채 점유만 영구 해제한다.
- 사망 Actor와 열린 문 Blocker가 이 점유 해제 API를 사용한다.

### GridPathfinder

`GridPathfinder`는 상하좌우 4방향 BFS를 사용한다.

- 이동 가능 범위 계산.
- 도달 칸별 최단 거리 계산.
- 목표 칸까지 최단 경로 계산.
- 턴, AP, 입력 정책은 포함하지 않는다.

### 편집용 점유 표시

`GridActorOccupationDebugVisualizer`는 활성 `GridActor` 중 `OccupyCell`이 켜진 Actor의 직렬화 좌표를 Scene 뷰에 회색 사각형으로 표시한다.
문과 장치 위치를 플레이 전에도 확인하기 위한 디버그 전용 컴포넌트다.

## 6. 턴, 전술 유닛과 입력

### TurnManager와 AP

- `TurnManager`는 `Player`, `Enemy` 턴을 전환하고 시작·종료 이벤트를 발행한다.
- 플레이어 `ActionPoint`는 플레이어 턴 시작 시 `ControllableUnitData.StartTurnActionPoint`로 보충된다.
- 적 `EnemyActionPoint`는 적 턴에 `EnemyData.TurnActionPoint`로 보충된다.
- `TurnManager`는 `StageStateManager`가 `Playing`일 때만 새 턴 시작과 현재 턴 종료를 허용한다.
- 현재 `Tset`에서는 Space 키로 턴 종료를 시험할 수 있다.

### 공통 전술 유닛

`ITacticalUnit`은 진영 기반 조회에 필요한 다음 값을 제공한다.

- `Faction`
- `GridActor`
- `ActorHealth`
- `IsAlive`

`TacticalUnitRegistry`는 모든 전술 유닛과 플레이어 조작 가능 유닛 목록을 관리한다.
`EnemyRegistry`는 적 전용 시스템과의 기존 연결을 위해 활성 `EnemyContext` 목록을 별도로 유지한다.

### TacticalUnitContext

플레이어 유닛의 참조 주머니다.

- 공통: 데이터, `GridActor`, `ActorHealth`, `ActionPoint`.
- 이동: `PlayerGridMoveAction`, `GridMoveRiskEvaluator`, `GridMoveRangeHighlighter`.
- 해킹: `PlayerHackAction`.
- 검: `PlayerSwordState`, `PlayerSwordThrowAction`, `PlayerSwordRecallAction`.
- 근접: `PlayerMeleeAttackAction`.
- 총: `PlayerGunAmmo`, `PlayerGunAttackAction`.

`ControllableUnitData.RequiredAbilities`와 실제 연결된 선택 행동 컴포넌트는 정확히 일치해야 한다.

현재 능력 플래그는 다음과 같다.

- `Move`
- `Gun`
- `Hack`
- `Sword`
- `Melee`
- `HeavyGun`: 타입만 있으며 실제 행동은 아직 없다.

### 플레이어 제어권

`PlayerUnitControlManager`가 현재 `ActiveUnit`을 관리한다.

- 살아 있고 AP가 1 이상인 플레이어 조작 유닛만 선택할 수 있다.
- 다른 유닛을 선택하면 이전 유닛의 행동 선택을 모두 취소한다.
- 현재 유닛의 AP가 0이 되면 등록 순서상 다음 AP 보유 유닛으로 자동 전환한다.
- 선택 변경은 `ActiveUnitChanged` 이벤트로 Presenter에 전달된다.

### 입력 흐름

```text
PlayerInputReader
→ PlayerUnitInputController
→ PlayerUnitActionFlowController
→ 현재 TacticalUnitContext의 행동 컴포넌트
→ ActionResolutionContext.Resolve()
→ ActionPresentationQueue.PlayQueuedEvents()
```

현재 임시 입력은 다음과 같다.

- `M`: 이동 선택.
- `H`: 해킹 선택.
- `T`: 검 투척 선택.
- `R`: 검 회수 실행.
- `F`: 근접 공격 선택.
- `G`: 총 공격 선택.
- 좌클릭: 유닛 선택 또는 선택 행동 확정.
- 우클릭 / Escape: 행동 선택 취소.

연출 큐가 재생 중이면 새 행동을 실행할 수 없다.
스테이지가 `Cleared` 또는 `Failed`로 바뀌면 `PlayerUnitInputController`가 현재 행동 선택과 경로 표시를 정리하고 이후 플레이어 입력을 처리하지 않는다.
입력 계층을 거치지 않은 직접 행동 요청도 `PlayerUnitActionFlowController`가 `StageStateManager.IsPlaying`을 다시 확인해 차단한다.

## 7. 플레이어 행동

### 이동

`PlayerGridMoveAction`은 현재 AP로 감당 가능한 이동 거리를 계산하고 BFS 경로를 따라 한 칸씩 논리 이동한다.

- AP 1개당 이동 거리는 `MoveDistancePerActionPoint`다.
- 실제 경로 길이를 AP 구간으로 환산해 이동 시작 시 AP를 소비한다.
- 이동 가능 칸을 AP 구간별 파랑·노랑·빨강으로 표시한다.
- 마우스 목표까지의 최단 경로를 별도 하이라이트로 표시한다.
- 각 칸 진입마다 `MoveStepEnteredLogicEvent`를 발행하고 즉시 해석한다.
- 최종 도착 뒤 `MoveCompletedLogicEvent`를 발행해 목표 달성 같은 후속 논리를 확정한다.

### 경로 위험 평가

`GridMoveRiskEvaluator`는 이동 미리보기와 실제 칸 진입을 적 시야와 비교한다.

- 미리보기에서 첫 위험 칸을 경고색으로 표시한다.
- 실제 이동 중 처음 감지된 칸에서 `AlertTriggeredLogicEvent`를 발행한다.
- 이동 1회당 첫 발각만 처리한다.
- 발각 자체는 현재 논리 이동을 중단시키지 않는다.

### 해킹

`PlayerHackAction`은 `HackableRegistry`의 `HackableObject`를 대상으로 한다.

- 이미 해킹된 대상은 다시 해킹할 수 없다.
- 검 능력 유닛은 현재 검 위치, 그 외 유닛은 본체 위치를 해킹 거리 기준으로 사용한다.
- 대상 주변 8칸 중 진입 가능한 칸을 찾고 플레이어와 가까운 칸을 실행 위치로 선택한다.
- AP를 소비하고 대상 상태를 해킹 완료로 바꾼다.
- `HackCompletedLogicEvent`와 `Hack` 연출을 만든다.
- 검 능력 유닛은 `SwordMove(Hack)`을 `Hack`보다 먼저 재생한다.
- `HackPresenter`의 현재 연출은 `HackableData.HackDuration`만큼 대기하고 로그를 출력하는 1차 형태다.

### 도깨비 환도

`PlayerSwordState`가 검의 현재 기준 칸과 회수 여부를 보관한다.
검은 보드 점유 Actor가 아니므로 `GridManager`에 등록되지 않는다.

- 투척은 현재 검 위치에서 사거리 안의 보드 칸으로 이동한다.
- 목표 칸에 `IDamageable` 대상이 있으면 표준 피해 이벤트를 발행한다.
- 회수는 검을 플레이어 칸으로 되돌린다.
- 해킹은 대상 주변 실행 칸으로 검 기준 위치를 옮긴다.
- `SwordActionPresenter`가 검 Visual의 부모, 위치, 방향 회전과 근접 공격 기울기를 제어한다.
- 경로 VFX 통로는 있으나 현재 테스트의 `Path Vfx Id`는 `None`이다.

### 근접 공격

`PlayerMeleeAttackAction`은 인접한 피해 가능 Actor를 공격한다.

- 검이 회수 상태면 검 보유 피해량과 `MeleeWithSword` 연출을 사용한다.
- 검이 배치된 상태면 검 없음 피해량과 `MeleeUnarmed` 연출을 사용한다.
- 직접 HP를 바꾸지 않고 `ApplyDamageLogicEvent`를 발행한다.

### 총 공격

`PlayerGunAttackAction`은 사거리 안의 피해 가능 Actor를 공격한다.

- AP와 `PlayerGunAmmo`의 총알을 함께 소비한다.
- 현재 1회 공격의 총알 비용은 1이다.
- `ApplyDamageLogicEvent`에 `PlayerGun` 공격 표현 종류를 담는다.

## 8. 논리 이벤트 처리

### ActionResolutionContext

행동 하나에서 발생한 논리 이벤트와 연출 이벤트를 모은다.

- `Publish()`: 후속 논리 이벤트를 FIFO 큐에 추가한다.
- `Resolve()`: 논리 큐가 빌 때까지 `ActionLogicEventBus`로 전달한다.
- `EnqueuePresentation()`: 화면에 보여 줄 결과를 연출 큐에 추가한다.

### ActionLogicEventBus

활성 `IActionLogicEventHandler` 모두에게 이벤트를 전달하고 각 핸들러의 `CanHandle()`로 대상을 걸러낸다.
씬 배치가 필요 없는 `DamageResolutionCoordinator`, `ActorDeathCoordinator`는 기본 등록된다.

현재 논리 이벤트:

- 이동: `MoveStepEnteredLogicEvent`, `MoveCompletedLogicEvent`.
- 발각: `AlertTriggeredLogicEvent`, `EnemyAlertedLogicEvent`.
- 해킹: `HackCompletedLogicEvent`.
- 검: `SwordThrownLogicEvent`, `SwordRecalledLogicEvent`.
- 전투: `ApplyDamageLogicEvent`, `DamageAppliedLogicEvent`, `ActorDiedLogicEvent`.
- 스테이지: `StageClearedLogicEvent`.

## 9. 적 시스템

### EnemyContext

적 하나의 참조 주머니다.

- `EnemyData`
- `GridActor`
- `ActorHealth`
- `EnemyGridSight`
- `EnemyAlertState`
- `EnemyActionPoint`
- `EnemyTurnAgent`
- `EnemyAttackAction`

적은 `ITacticalUnit`과 `EnemyRegistry` 양쪽에 등록된다.

### 시야

`EnemyGridSight`는 상하좌우 방향 기준의 전방 부채꼴과 주변 근접 감지를 계산한다.

- 전방 거리가 멀어질수록 좌우 폭이 넓어진다.
- 고정 장애물은 같은 시야 레인의 뒤쪽 칸을 가린다.
- 장애물 칸 자체는 감지 칸에 포함하지 않는다.
- 근접 감지는 방향과 장애물의 영향을 받지 않는다.

### 경계와 애드

`EnemyAlertCoordinator`는 시야 발각과 적 피격을 처리한다.

- 최초 사건 적의 `AlertSpreadRange` 안에 있는 살아 있는 적을 경계 상태로 바꾼다.
- 새로 경계 상태가 된 적마다 `AlertDetected` 연출과 `EnemyAlertedLogicEvent`를 만든다.
- 현재 애드는 최초 사건 적을 기준으로 한 단일 단계 전파다.

`EnemyAlertReactionCoordinator`는 새로 경계 상태가 된 적을 한 번만 반응시킨다.

- 가장 가까운 살아 있는 플레이어를 기준으로 엄폐 후보를 계산한다.
- `EnemyTacticalPositionScorer` 점수로 벽 인접·차폐·거리 조건을 평가한다.
- 선택한 경로로 논리 이동한 뒤 `EnemyReactionMove` 연출을 추가한다.

### 적 턴

`EnemyTurnCoordinator`는 적 턴에 등록된 적을 순서대로 실행하고 각 적의 연출 완료를 기다린다.
적 턴 시작 전과 각 적 행동 사이에 스테이지 상태를 확인하며, 종료 상태가 되면 현재 논리와 연출까지만 마치고 남은 적 행동과 다음 턴 전환을 중단한다.

`EnemyTurnAgent`의 현재 우선순위:

1. 현재 위치에서 공격 가능하면 공격 후 더 좋은 엄폐 위치로 이동을 시도한다.
2. 공격할 수 없다면 공격 가능한 엄폐 위치로 이동한 뒤 공격을 시도한다.
3. 공격 가능한 위치가 없으면 이동 범위 안의 최고 엄폐 위치로 이동한다.
4. 살아 있지 않거나 경계 상태가 아니면 행동하지 않는다.

표적은 `EnemyTargetSelector`가 가장 가까운 살아 있는 플레이어 진영 유닛으로 선택한다.

## 10. 피해와 사망

모든 플레이어·적 공격은 같은 피해 통로를 사용한다.

```text
공격 행동
→ ApplyDamageLogicEvent
→ DamageResolutionCoordinator
→ ActorHealth.TakeDamage()
→ DamageAppliedLogicEvent
→ 필요 시 ActorDiedLogicEvent
→ CombatAction 연출
```

- `DamageResult`가 피해 적용 전후 HP와 이번 피해로 사망했는지를 보관한다.
- `EnemyAlertCoordinator`는 적의 `DamageAppliedLogicEvent`를 경계 원인으로 사용할 수 있다.
- `ActorDeathCoordinator`는 사망 Actor의 칸 점유를 해제한다.
- 사망 오브젝트는 제거하지 않고 `CombatActionPresenter`가 `Death` 자세를 유지한다.

## 11. 연출 시스템

### ActionPresentationQueue

씬 단일 FIFO 연출 큐다.

- 활성 연출 처리자는 `IPresentationEventHandler`를 구현하고 큐의 `Register()` / `Unregister()`로 명시적으로 등록한다.
- 큐는 등록된 처리자의 `CanHandle()`로 담당 여부를 확인하고, 처리 가능한 대상의 `Handle()`에 이벤트와 `PresentationEventHandle`을 전달한다.
- 등록 목록의 복사본을 순회하므로 이벤트 처리 도중 등록 상태가 바뀌어도 현재 전달 순서는 안전하게 유지된다.
- 처리자가 없으면 경고 후 자동 완료한다.
- 여러 처리자가 같은 이벤트를 담당하면 경고한다.
- 완료 신호가 오래 오지 않으면 타임아웃 경고를 남긴다.
- 큐 재생 중에는 플레이어 행동과 설정에 따른 카메라 이동이 잠긴다.

현재 실제로 사용하는 연출 이벤트:

- `MoveActor`
- `EnemyReactionMove`
- `AlertDetected`
- `SwordMove`
- `Hack`
- `SecurityDoorOpen`
- `CombatAction`
- `StageCleared`

### Actor 비주얼 연결

- `ActorPresentationRegistry`: `GridActor`와 `ActorVisualController` 연결을 보관한다.
- `ActorPresentationBinding`: 각 Actor의 논리·비주얼 연결을 등록한다.
- `ActorPresentationSynchronizer`: 논리 위치와 VisualRoot 위치를 초기 동기화한다.
- `ActorVisualController`: SpriteRenderer 색상·좌우 반전과 Animator 상태를 적용한다.
- 현재 원본 캐릭터 아트의 기본 방향은 오른쪽이다.

### Presenter 책임

- `GridActorMovePresenter`: 플레이어·적 이동 위치 보간과 이동 상태 전환.
- `AlertDetectedPresenter`: 경고색 점멸과 선택적 발각 애니메이션.
- `CombatActionPresenter`: 공격자·피격자 방향과 공격·피격·사망 자세 동시 처리.
- `SwordActionPresenter`: 검 투척·해킹·회수 위치와 근접 기울기.
- `HackPresenter`: 현재 해킹 시간 대기와 로그.
- `SecurityDoorPresenter`: 열린 문의 `DoorVisual` 비활성화.
- `StageResultPresenter`: 클리어·실패 연출 이벤트 처리. 현재는 한국어 로그 출력 후 즉시 완료.
- `PlayerUnitSelectionPresenter`: 현재 조작 유닛의 임시 LineRenderer 선택 링.

`MovePresentationData`는 이동 시간·커브·이동/Idle 애니메이션 옵션을 보관한다.
`CombatPresentationData`는 공격 종류별 공격 상태와 유지 시간, 공통 Hit·Death·Idle 상태를 보관한다.

## 12. 해킹 터미널과 보안문

### HackTerminal

```text
HackTerminal
├─ Logic
│  ├─ GridActor
│  ├─ HackableObject
│  └─ HackPresenter
└─ Visual
```

- 현재 터미널 논리 좌표는 `(7, 8)`이다.
- `HackableDataTest.HackDuration`은 `2초`다.
- 터미널은 해킹 대상 위치 제공을 위해 `GridActor`를 사용한다.
- 재사용 프리팹은 `Assets/Prefab/Map/HackingObject/HackTerminal.prefab`이다.
- 스테이지마다 달라지는 `GridActor.GridPosition`은 배치한 프리팹 인스턴스에서 설정한다.

### SecurityDoor01

```text
SecurityDoor01
├─ SecurityDoorController
├─ SecurityDoorPresenter
├─ LeftCellBlocker
│  └─ Logic (GridActor)
├─ RightCellBlocker
│  └─ Logic (GridActor)
└─ DoorVisual (SpriteRenderer)
```

- 왼쪽 Blocker는 `(8, 9)`, 오른쪽 Blocker는 `(9, 9)`를 점유한다.
- 두 Blocker 모두 `OccupyCell = true`, `SnapToGridOnEnable = false`다.
- 두 칸은 `LogicTilemap`에 칠하지 않는다.
- `SecurityDoorController.UnlockHackable`은 터미널의 `HackableObject`를 참조한다.
- `BlockingActors`에는 좌우 Logic의 `GridActor` 두 개가 연결되어 있다.
- `SecurityDoorPresenter`는 같은 문의 Controller와 `DoorVisual`을 참조한다.
- 재사용 프리팹은 `Assets/Prefab/Map/HackingObject/SecurityDoor.prefab`이다.
- 문과 좌우 Blocker의 스테이지별 `GridPosition`은 배치한 프리팹 인스턴스에서 설정한다.

문 개방 흐름:

1. `PlayerHackAction`이 터미널을 해킹 완료 상태로 만든다.
2. `HackCompletedLogicEvent`가 발행된다.
3. `SecurityDoorController`가 두 Blocker의 점유를 즉시 해제하고 `IsOpen`을 기록한다.
4. Controller가 `SecurityDoorOpen` 연출을 기존 해킹 연출 뒤에 추가한다.
5. `SecurityDoorPresenter`가 `DoorVisual`을 비활성화한다.

검 능력 유닛 기준 화면 순서는 `SwordMove(Hack) → Hack → SecurityDoorOpen`이다.
현재 플레이 모드에서 해킹 전 통로 차단, 해킹 후 비주얼 제거, 두 칸 점유 해제와 통과 가능 상태까지 검증했다.

## 13. 스테이지 목표와 상태

- `StageGoal`은 목표 `GridPosition`과 씬의 `GridManager` 참조를 가진다.
- 편집 모드에는 목표 좌표를 Scene 뷰 녹색 디버그 칸으로 표시한다.
- 플레이 모드에는 Game 뷰의 Gizmos가 켜진 경우 같은 좌표에 프리팹 비주얼 교체 전 임시 목표 표시를 그린다.
- 두 표시는 실제 빌드용 비주얼이 아니며 최종 목표 표시는 이후 프리팹으로 교체한다.
- `StageGoalManager`는 `MoveCompletedLogicEvent`를 받아 살아 있는 플레이어 조작 유닛의 목표 도착을 검사한다.
- 목표 달성 시 `StageClearedLogicEvent`와 `StageCleared` 연출을 만든다.
- `StageStateManager`는 `Playing`, `Cleared`, `Failed` 상태를 보관한다.
- `StageResultPresenter`는 `StageCleared`와 `StageFailed` 연출을 전담하며 현재는 결과 로그를 출력하고 큐를 즉시 완료한다.
- `Cleared` 또는 `Failed` 상태에서는 플레이어 입력, 새 행동 실행, 턴 전환과 남은 적 행동이 차단된다.
- 상태가 바뀌기 전에 시작된 논리 처리와 이미 큐에 들어간 연출은 끝까지 완료한다.
- 현재 자동 실패 조건은 아직 연결되지 않았으며 `RequestFail()` 진입점만 있다.
- `ControllableUnitData.DefeatOnDeath`도 현재 기록용이며 실패 판정에 사용되지 않는다.

## 14. 카메라, 대사, VFX와 디버그

### 카메라

`CameraKeyboardMover`는 WASD와 방향키를 합쳐 카메라를 이동한다.

- 대각선 속도를 정규화한다.
- 시작 위치 기준 X/Y 최대 이동 거리 안으로 제한한다.
- Z 위치는 유지한다.
- 연출 큐 재생 중에는 이동을 막을 수 있다.

현재 `Tset` 값은 이동 속도 `8`, 최대 이동 거리 `(10, 10)`, 연출 중 잠금 활성화다.

### 대사

기존 말풍선 대사 구조는 유지되어 있다.

- `DialogueSequenceData`, `DialogueStepData`, `DialogueLineData`: 대사 데이터.
- `DialogueManager`: 대사 진행.
- `DialogueSpeaker`, `DialogueBubblePresenter`, `SpeechBubbleView`: 화자와 말풍선 표시.

현재 전술 행동 흐름과 튜토리얼 진행을 묶는 전용 대사 트리거는 아직 없다.

### VFX

`VfxManager`는 ID 기반 풀링과 수명 관리를 제공한다.
검 경로 VFX 연결 지점은 마련되어 있지만 현재 검 Presenter 설정은 `VfxId.None`이라 재생하지 않는다.

### 디버그

- `S2TDebugOverlay`: 턴, AP, 이동·경계·행동 상태 확인.
- `GridActorOccupationDebugVisualizer`: 편집 모드 점유 칸 회색 표시.
- `DebugPresentationEventReceiver`: 미처리 연출 이벤트 확인.
- `DebugPresentationQueueTester`: 샘플 연출 큐 확인.
- `GridPlayerDebugMover`: 정식 이동 구조와 별도로 남아 있는 단순 그리드 이동 시험 도구.

## 15. 현재 테스트 데이터

### 플레이어

유진 테스트 데이터:

- 능력: `Move + Gun + Hack + Sword + Melee`.
- 최대/턴 시작 AP: `3 / 3`.
- AP 1개당 이동 거리: `3칸`.
- 해킹·검 투척 사거리: 각각 `5칸`.
- 총 사거리: `5칸`, 최대 총알 `10`.

동료1 테스트 데이터:

- 능력: `Move + Melee`.
- 최대/턴 시작 AP: `3 / 3`.
- AP 1개당 이동 거리: `3칸`.

### 적

`EnemyDataTest`:

- 시야 거리 `5`, 근접 감지 거리 `1`.
- 애드 전파 거리 `5`, 경계 반응 이동 거리 `3`.
- 적 턴 AP `2`, 이동 거리 `3`.
- 원거리 공격 거리 `6`, 피해 `1`.

## 16. 현재 검증 상태

현재 코드와 `Tset` 씬에서 다음 항목을 확인했다.

- `dotnet build Assembly-CSharp.csproj --no-restore`: 경고 0개, 오류 0개.
- 다중 플레이어 유닛 직접 선택과 AP 소진 자동 전환.
- 플레이어 이동, 범위·경로 표시, 적 시야 발각과 경계 반응.
- 적 턴 공격·엄폐 이동.
- 검 투척·해킹·회수 Visual과 통합 전투 자세.
- `LogicTilemap` 기반 고정 장애물 판정.
- 편집 모드 `GridActor` 점유 칸 표시.
- WASD·방향키 카메라 이동과 범위 제한.
- 해킹 터미널과 2칸 보안문의 차단·개방 전체 흐름.
- `IPresentationEventHandler` 기반 연출 처리자 등록, 필터링, 실행과 완료 대기 흐름.
- 편집 모드 Scene 뷰와 플레이 모드 Game 뷰의 `StageGoal` 위치 표시.
- `StageResultPresenter`의 클리어 로그 처리와 검 투척·회수 반복 Visual 유지.
- 스테이지 클리어 후 플레이어 입력, 새 행동, 턴 전환과 남은 적 행동 차단.
- 프리팹으로 배치한 해킹 터미널과 보안문의 기존 해킹·개방 흐름 유지.

## 17. 현재 한계

- 입력은 임시 키·마우스 매핑이며 정식 UI와 Input Action Map은 아직 없다.
- 플레이어 이동은 논리적으로 즉시 확정되며 발각 시 중간 정지나 카메라 컷은 없다.
- 현재 적 시야는 장애물 가림을 반영하지 않아 벽이나 상자 뒤 칸에서도 발각과 애드 경고가 발생할 수 있다.
- 적 애드는 단일 단계 전파이며 연쇄 전파는 없다.
- 엄폐 평가는 벽 인접과 노출·거리 중심의 1차 점수 구조다.
- 해킹 연출은 시간 대기와 로그, 문 열림은 비주얼 비활성화 수준이다.
- 검 경로, 총격, 근접 타격, 적 공격의 최종 VFX가 없다.
- 선택 표시는 런타임 LineRenderer 임시 링이다.
- 스테이지 실패 조건과 최종 클리어·실패 UI가 없다.
- `HeavyGun`은 데이터 타입만 있고 행동 구현이 없다.

## 18. 다음 작업

1. 적 시야에 장애물 가림 판정을 추가해 실제로 보이는 칸만 시야 표시, 이동 위험, 발각과 애드 판정에 사용한다.
2. 구현 전에 고정·동적 장애물, 장애물 칸 자체, 대각선 모서리와 열린 문의 시야 차단 규칙을 확정한다.
3. 테스트 씬의 터미널 해킹과 문 통과 흐름을 기준으로 튜토리얼 진행 순서를 설계한다.
4. 튜토리얼 트리거, 안내 대사·UI, 행동 제한 규칙을 정한다.
5. 테스트 씬에서 검증한 구성을 실제 튜토리얼 씬으로 옮긴다.
6. 열린 문 아트·애니메이션과 검 경로·공격 VFX는 Presenter 계층에 추가한다.
7. 캐릭터·이펙트 흐름이 안정된 뒤 카메라 줌·컷 연출을 추가한다.

## 19. 문서 유지 규칙

- 이 문서에는 현재 실제로 존재하는 구조만 기록한다.
- 날짜별 구현 과정, 실패한 시도, 제거한 구조는 `S2-T 작업일지.md`에만 남긴다.
- 클래스가 제거되거나 책임이 바뀌면 과거 설명을 덧붙이지 않고 해당 현재 항목을 직접 갱신한다.
- 씬 설정을 바꾸면 코드 설명뿐 아니라 `Tset 씬 구성`, `현재 테스트 데이터`, `현재 검증 상태`도 함께 갱신한다.
- 다음 작업을 완료하면 `현재 한계`와 `다음 작업`에서 완료 항목을 제거하거나 새 상태로 교체한다.
