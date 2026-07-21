# S2-T 현재 구현 구조

최신 기준: 2026-07-21
브랜치: `main`
프로젝트 명칭: `S2-T`

이 문서는 현재 S2의 메인 개발 방향인 보드게임식 턴제 잠입 퍼즐/전술 게임 S2-T의 구현 구조를 빠르게 파악하기 위한 문서다.
작업 순서, 과거 실험 기록, 변경 이력은 `S2-T 작업일지.md`에서 관리한다.

문서를 읽을 때는 상단의 `최신 기준 요약`, `현재 한계`, `다음 작업`과 가장 최근 날짜의 구조 설명을 현재 기준으로 삼는다.
아래의 이전 날짜별 구조는 구현 변화 추적을 위해 남긴 이력이며, 내용이 충돌하면 더 최근 항목을 우선한다. 특히 `PlayerContext`, `PlayerTurnData`, 행동별 입력 컨트롤러, 개별 공격 Presenter 설명은 각각 `TacticalUnitContext`, `ControllableUnitData`, 통합 입력 계층, `CombatActionPresenter` 이전 구조다.

## 최신 기준 요약

- S2-T는 사이버 조선 세계관을 유지한 현재 메인 개발 방향의 보드게임식 턴제 잠입 퍼즐/전술 게임이다.
- 플레이어 진영은 여러 조작 유닛으로 구성되며, 각 유닛이 AP와 능력 구성을 가지고 이동/해킹/검 행동/공격을 실행한다.
- 이동은 목표 칸을 선택하면 BFS 경로를 따라 한 칸씩 처리하는 구조다.
- 이동 가능 범위는 현재 AP와 AP당 이동 거리 기준으로 계산한다.
- 고정 이동불가 칸은 수동 `blockedPositions` 목록이 아니라 필수 `LogicTilemap`에서 읽는다. 논리 그리드 칸의 월드 위치와 겹치는 LogicTilemap 셀에 타일이 있으면 해당 칸을 이동불가로 초기화하며, `Tset` 씬 연결과 플레이 모드 검증을 완료했다.
- 적 시야는 `EnemyGridSight`가 그리드 칸 단위로 계산한다.
- 플레이어 이동 경로가 적 시야에 들어가면 `GridMoveRiskEvaluator`가 `AlertTriggeredLogicEvent`를 발행한다.
- 애드 전파는 `EnemyAlertCoordinator`가 담당한다.
- 적의 현재 발각 상태는 `EnemyAlertState`가 보관한다.
- 현재 애드 구조는 단일 단계 전파 기준이며, 연쇄 전파는 맵 크기와 적 밀도 기준이 잡힌 뒤 확장한다.
- 실제로 새로 발각된 적마다 `AlertDetectedPresenter`가 큐 순서에 맞춰 경고색 점멸을 재생한다.
- Presenter의 색상 요청은 VisualRoot의 `ActorVisualController`가 실제 스프라이트에 적용한다.
- 적 턴에는 경계 상태이고 전투불능이 아닌 적이 `EnemyActionPoint`의 AP를 사용해 원거리 공격과 엄폐 이동을 순서대로 수행한다.
- 적 턴 AI는 `EnemyTacticalMovePlanner`와 `EnemyTacticalPositionScorer`를 재사용해 공격 가능한 엄폐 위치 또는 최고 엄폐 위치를 고른다.
- 플레이어와 적의 피해 행동은 `ApplyDamageLogicEvent`를 발행하고 `DamageResolutionCoordinator`가 실제 피해와 사망 이벤트를 확정한다.
- 해킹은 `PlayerHackAction`이 `HackableObject`를 대상으로 AP와 사거리를 검사하고 논리·연출 이벤트를 발행한다.
- 행동 판정은 논리 계층에서 먼저 확정하고, `ActionPresentationQueue`가 연출 이벤트를 순서대로 재생한다.
- 전투 연출은 씬 단일 `CombatActionPresenter`가 공격자와 피격자의 자세를 같은 프레임에 전환하고 데이터에 지정된 시간 뒤 상태를 정리한다.
- 검 Visual 이동은 `SwordMove` 연출 이벤트가 투척·해킹·회수를 구분하고 `SwordActionPresenter`가 실제 위치, 이동 방향 회전, 회수 위치를 적용한다.
- 피해 검 투척은 `SwordMove -> CombatAction`, 검 기반 해킹은 `SwordMove -> Hack` 순서로 연출 큐에서 처리한다.
- 검 소지 근접 공격은 `CombatActionPresenter`의 시작·종료 알림을 받아 검을 공격 방향으로 기울였다가 회수 자세로 복구한다.
- 새 검 Visual과 `SwordActionPresenter` 필수 참조 연결을 완료했고 현재 코드 기준 플레이 모드 테스트를 마쳤다.
- 캐릭터 연출 연결은 `ActorPresentationRegistry`와 `ActorPresentationBinding`이 관리하며, `ActorVisualController`가 Animator 상태와 좌우 방향을 적용한다. 현재 원본 캐릭터 아트의 기본 방향은 오른쪽이다.
- 조작 유닛 선택 표시는 유닛별 `PlayerUnitSelectionPresenter`가 `PlayerUnitControlManager.ActiveUnitChanged`를 받아 자기 유닛의 임시 LineRenderer 링만 켜는 구조다.
- `CameraKeyboardMover`가 WASD와 방향키 입력을 함께 받아 테스트 카메라를 이동하며, 시작 위치 기준 X/Y 최대 거리 안으로 이동 범위를 제한한다. 현재 설정 기준 플레이 모드 테스트를 완료했다.

## 현재 목표

S2-T의 현재 구현 목표는 완성된 다중 유닛/행동 판정/적 AI 통로를 실제 스테이지 제작과 반복 테스트에 사용할 수 있는 상태로 정리하는 것이다.
2026-07-21 기준 전투 애니메이션 기반, 조작 유닛 선택 링, 검 투척·해킹·회수 Visual, 키보드 카메라 이동과 LogicTilemap 기반 고정 장애물 제작 흐름을 연결하고 플레이 모드 검증을 완료했다. 후속 주요 작업 후보는 전투 카메라 연출과 튜토리얼 1스테이지 완성이다.

## 씬 구성 기준

현재 테스트 기준 씬은 `Assets/Scenes/Tset.unity`다.

씬에는 다음 계열 오브젝트가 필요하다.

- `GridManager`: 보드 크기, 좌표 변환, LogicTilemap 기반 고정 이동불가 상태와 점유 상태를 관리한다.
- `LogicTilemap`: 타일이 칠해진 셀을 고정 이동불가 칸으로 제공하는 필수 논리 타일맵이다. 화면용 `FloorTilemap`, `ObjectTilemap`과 분리한다.
- `TurnManager`: 플레이어/적 턴 전환 이벤트를 관리한다.
- 플레이어 진영 유닛: `TacticalUnitContext`, `GridActor`, `ActionPoint`, `PlayerUnitSelectionPresenter`와 `ControllableUnitData.RequiredAbilities`에 맞는 행동 컴포넌트를 가진다.
- 적 유닛: `EnemyContext`, `GridActor`, `EnemyGridSight`, `EnemyAlertState`, `EnemyActionPoint`, `EnemyTurnAgent`, `EnemyAttackAction`을 가진다.
- `TacticalUnitRegistry`: 플레이어/적/중립 전술 유닛을 통합 등록한다.
- `PlayerUnitControlManager`, `PlayerUnitInputController`, `PlayerUnitActionFlowController`: 조작 유닛 선택, 입력 전달, 논리 실행과 연출 큐 재생을 관리한다.
- `EnemyRegistry`: 현재 씬의 활성 `EnemyContext` 목록을 관리한다.
- `EnemyAlertCoordinator`: 플레이어 발각 이벤트와 피해 이벤트를 받아 애드 전파를 처리한다.
- `EnemyTurnCoordinator`: 적 턴 시작 시 활성 적을 순서대로 실행하고 연출 큐 완료 후 다음 적을 처리한다.
- `ActorPresentationRegistry`, `CombatActionPresenter`: 논리 Actor와 화면 Visual을 연결하고 통합 전투 애니메이션을 조정한다.
- Dialogue/VFX 관련 오브젝트는 필요한 테스트에서만 배치한다.

## 코드 폴더 구조

현재 주요 스크립트 폴더 역할은 다음과 같다.

- `Assets/Script/Grid`: 격자 좌표, 보드 상태, 점유, 경로 탐색.
- `Assets/Script/Turn`: 턴 진행과 AP 관리.
- `Assets/Script/TurnAction/Core`: 행동 로직 이벤트, 해석 컨텍스트, 공통 인터페이스.
- `Assets/Script/TurnAction/Grid`: 이동 범위/경로/위험도 표시와 평가.
- `Assets/Script/TurnAction/Input`: 현재 조작 유닛에 입력을 전달하는 씬 단일 입력 계층.
- `Assets/Script/TurnAction/Player`: 플레이어 진영의 실제 행동 실행과 행동 흐름.
- `Assets/Script/Player`: 플레이어 진영 관련 데이터와 제어 구성.
- `Assets/Script/Enemy`: 적 핵심 참조, 시야, 등록소, 애드 전파, 발각 상태, 적 턴 AI, 적 AP, 적 원거리 공격.
- `Assets/Script/DataScript/Data`: 플레이어/적/대사/해킹 데이터 에셋.
- `Assets/Script/Dialogue`: 말풍선 대사 시스템.
- `Assets/Script/Common`: 공용 VFX 풀.
- `Assets/Script/Camera`: 게임플레이 카메라의 키보드 이동과 이동 범위 제한.
- `Assets/Script/Combat`: 공용 전투/해킹 인터페이스.
- `Assets/Script/Presentation`: 연출 이벤트, 연출 큐, 연출 테스트 컴포넌트.
- `Assets/Script/Presentation/Data`: 연출 튜닝 데이터 에셋.
- `Assets/Script/Presentation/Presenter`: 연출 큐 이벤트를 처리하는 Presenter 컴포넌트.
- `Assets/Script/Presentation/Visual`: VisualRoot에 붙는 실제 시각 제어 컴포넌트.

## Grid 시스템

### GridPosition

`GridPosition`은 S2-T의 보드 칸 좌표 값 타입이다.
Unity 월드 좌표와 분리해서 턴제 규칙은 `GridPosition` 기준으로 계산한다.

주요 기능:

- `x`, `y` 칸 좌표 보관.
- `Up`, `Down`, `Left`, `Right`, `Zero` 기본 방향 값 제공.
- `ManhattanDistanceTo()`로 맨해튼 거리 계산.
- `+`, `-`, `==`, `!=` 연산 지원.

### GridCellState

`GridCellState`는 한 칸의 현재 상태를 보관한다.
게임 규칙 판단은 하지 않고, `GridManager`가 승인한 상태만 기록한다.

현재 보관 값:

- `Position`: 이 칸의 좌표.
- `IsBlocked`: 고정 이동불가 칸 여부.
- `OccupiedActor`: 현재 점유 중인 `GridActor`.
- `IsOccupied`: 점유 여부.
- `CanEnter`: 기본 이동 규칙 기준 진입 가능 여부.

### GridManager

`GridManager`는 격자 보드의 단일 관리자다.

책임:

- 보드 크기와 칸 크기 관리.
- `GridToWorld()`, `WorldToGrid()` 좌표 변환.
- 필수 `LogicTilemap`의 타일 유무를 기준으로 고정 이동불가 칸 반영.
- 논리 그리드 칸을 `GridToWorld()`로 변환한 뒤 `LogicTilemap.WorldToCell()`로 대응 셀을 찾아, Tilemap Transform이나 Grid 원점 차이를 좌표 변환에 반영.
- `Dictionary<GridPosition, GridCellState>`로 칸 상태 관리.
- `RegisterActor()`, `UnregisterActor()`, `TryMoveActor()`로 점유 상태 변경.
- Scene 뷰 Gizmo로 보드, 이동불가 칸, 점유 칸 표시.

주의:

- `GridManager`는 플레이어/적/장치 구분을 알지 않는다.
- 적 검색, 애드 전파, AI 판단은 `GridManager` 책임이 아니다.
- `LogicTilemap`은 고정 장애물의 단일 원본이며, 런타임에서 별도 목록이나 `SetBlocked()`로 상태를 이중 관리하지 않는다.

### GridActor

`GridActor`는 보드 위에 올라가는 말의 최소 단위다.
플레이어, 적, 장치처럼 칸 좌표를 가지는 대상이 사용한다.

책임:

- 현재 `GridPosition` 보관.
- 활성화 시 `GridManager`에 점유 등록.
- `TryMoveTo()`, `TryMoveBy()`로 이동 요청.
- 이동 성공 시 Transform을 그리드 월드 좌표로 스냅.

### GridPathfinder

`GridPathfinder`는 BFS 기반 경로 탐색 도구다.
턴, AP, 입력 정책은 포함하지 않는다.

주요 API:

- `FindReachablePositions()`: 최대 거리 안의 도달 가능 칸 계산.
- `FindReachablePositionDistances()`: 도달 가능 칸과 실제 최단 거리 계산.
- `TryFindPath()`: 시작 칸에서 목표 칸까지의 최단 경로 계산.

현재 이동은 상하좌우 4방향만 허용한다.

## Turn / AP

### TurnManager

`TurnManager`는 현재 턴 주체를 `TurnSide.Player`, `TurnSide.Enemy`로 관리한다.

책임:

- 현재 턴 진영 보관.
- 턴 시작/종료 이벤트 발행.
- 임시 디버그 턴 종료 키 입력 처리.

적 턴 행동은 `EnemyTurnCoordinator`가 활성 적을 순서대로 실행하고, 각 `EnemyTurnAgent`가 경계 상태·AP·표적·엄폐 점수를 기준으로 이동과 공격을 결정한다.

### ActionPoint

`ActionPoint`는 플레이어 조작 전술 유닛의 AP를 관리한다.

책임:

- 플레이어 턴 시작 시 AP 보충.
- `CanSpend()`, `TrySpend()`로 AP 소비 처리.
- AP 변경 이벤트 발행.

AP 수치는 `TacticalUnitContext.UnitData`의 `ControllableUnitData`에서 읽는다.
필수 참조나 데이터가 없으면 fallback 없이 오류를 남기고 컴포넌트를 비활성화한다.

## 플레이어 조작 전술 유닛 구조

### ControllableUnitData

`ControllableUnitData`는 플레이어가 조작할 수 있는 전술 유닛의 능력 구성과 턴 기반 행동 수치를 보관하는 `ScriptableObject`다.

주요 값:

- `DisplayName`: 디버그와 UI에서 사용할 표시 이름.
- `RequiredAbilities`: 유닛이 반드시 갖춰야 하는 행동 능력 조합.
- `DefeatOnDeath`: 핵심 유닛 사망 실패 조건 연결용 값. 현재는 기록용.
- `MaxActionPoint`: 최대 AP.
- `StartTurnActionPoint`: 턴 시작 시 보충 AP.
- `MoveDistancePerActionPoint`: AP 1개 구간당 이동 가능 칸 수.
- `MoveRange`: 기존 호환용 이동 범위 값. 새 이동 구조에서는 직접 사용하지 않는다.
- `MoveActionPointCost`: 이동 거리 구간 1개가 소비하는 AP 비용.
- 해킹, 검 투척·회수, 근접 공격, 총 공격의 사거리·AP 비용·피해량.

데이터 에셋 자체에는 `HasValidData()` 책임을 두지 않는다.
데이터를 사용하는 컴포넌트가 필요한 값의 유효성을 직접 검사한다.

### TacticalUnitContext

`TacticalUnitContext`는 전술 유닛 루트의 참조 주머니다.
정책 계산이나 상태 변경을 직접 하지 않는다.

주요 참조:

- `ControllableUnitData UnitData`
- `GridActor GridActor`
- `ActorHealth Health`
- `ActionPoint ActionPoint`
- `PlayerGridMoveAction GridMoveAction`
- `GridMoveRiskEvaluator GridMoveRiskEvaluator`
- `GridMoveRangeHighlighter GridMoveRangeHighlighter`
- `PlayerHackAction HackAction`
- `PlayerSwordState SwordState`
- `PlayerSwordThrowAction SwordThrowAction`
- `PlayerSwordRecallAction SwordRecallAction`
- `PlayerMeleeAttackAction MeleeAttackAction`
- `PlayerGunAmmo GunAmmo`
- `PlayerGunAttackAction GunAttackAction`

플레이어 계열 컴포넌트는 같은 루트의 핵심 컴포넌트를 직접 `GetComponent<T>()`로 찾지 않고 `TacticalUnitContext`에서 꺼내 쓴다.
`TacticalUnitContext`는 `ControllableUnitData.RequiredAbilities`와 실제 행동 컴포넌트 구성이 정확히 일치하는지도 검사한다.

## Player 이동

### PlayerGridMoveAction

`PlayerGridMoveAction`은 현재 선택된 플레이어 조작 유닛의 그리드 이동 판정과 실행을 담당한다.

입력 연결:

- 씬 단일 `PlayerUnitInputController`가 입력을 해석한다.
- `PlayerUnitActionFlowController`가 현재 선택 유닛의 이동 실행을 요청하고 논리 처리 후 연출 큐를 재생한다.
- 행동 전환이나 유닛 전환 시 이동 선택과 경로 미리보기를 취소한다.

책임:

- 플레이어 턴과 AP 조건 확인.
- 현재 AP 기준 이동 가능 거리 계산.
- `GridPathfinder`로 이동 가능 칸과 목표 경로 계산.
- 이동 시작 시 AP 소비.
- 경로 칸을 순서대로 이동 처리.
- 이동 중 각 칸 진입마다 `MoveStepEnteredLogicEvent` 발행.
- 이동 완료 시 `MoveCompletedLogicEvent` 발행.
- 경로 미리보기 이벤트 발행.

현재 이동은 즉시 순차 처리이며, 중간 발각 시 이동 일시 정지는 아직 구현하지 않았다.

### 이동 범위 표시

`GridMoveRangeHighlighter`는 `PlayerGridMoveAction.MoveRangeSegmentsShown` 이벤트를 받아 이동 가능 칸을 AP 소비 구간별로 표시한다.

현재 표시 기준:

- AP 1개 구간: 파랑.
- AP 2개 구간: 노랑.
- AP 3개 이상 구간: 빨강.

각 구간은 런타임 `GridCellHighlighter`를 내부 생성해 표시한다.

### GridCellHighlighter

`GridCellHighlighter`는 그리드 칸 목록을 받아 런타임 하이라이트 오브젝트로 표시하는 저수준 표시기다.
이동 범위, 경로 미리보기, 위험 칸 표시 등에 재사용한다.

프리팹이 없으면 임시 1픽셀 사각형 스프라이트를 생성한다.

## 이동 경로 위험 평가

### GridMoveRiskEvaluator

`GridMoveRiskEvaluator`는 플레이어 이동 경로가 적 감지 칸에 들어가는지 평가한다.

책임:

- `PlayerGridMoveAction.MovePathPreviewShown`을 받아 경로 중 첫 위험 칸 계산.
- 위험 칸을 경고 하이라이트로 표시.
- `MoveStepEntered`를 받아 실제 이동 중 감지 여부 확인.
- 발각 시 `AlertTriggered(GridPosition, EnemyGridSight)` 이벤트 발행.
- 한 번의 이동 안에서는 첫 발각만 처리.

적 시야 조회 기준:

- `EnemyRegistry.Instance.Enemies`를 순회한다.
- 각 `EnemyContext.GridSight.CanDetect(position)`으로 감지 여부를 확인한다.
- 현재 같은 칸을 여러 적이 동시에 볼 경우 등록 순서상 먼저 발견된 적이 최초 감지 적이 된다.

현재 한계:

- 플레이어 시야 밖 적의 경고 숨김은 아직 구현하지 않았다.
- 감지 시 이동 일시 정지, 카메라 줌, 경고 UI는 아직 구현하지 않았다.

## Enemy 구조

### EnemyData

`EnemyData`는 적 튜닝 수치를 보관하는 `ScriptableObject`다.

현재 값:

- `SightRange`: 정면 부채꼴 시야 최대 거리.
- `UseAdjacentDetection`: 주변 근접 감지 사용 여부.
- `AdjacentDetectionRange`: 근접 감지 반경.
- `AlertSpreadRange`: 이 적이 플레이어를 발견했을 때 주변 적에게 애드를 전파하는 맨해튼 거리.

`AlertSpreadRange`는 전역 고정값이 아니라 최초 감지 적의 데이터에서 읽는다.

### EnemyContext

`EnemyContext`는 적 루트의 참조 주머니다.

현재 참조:

- `EnemyData EnemyData`
- `GridActor GridActor`
- `EnemyGridSight GridSight`
- `EnemyAlertState AlertState`

역할:

- 적 하나를 대표하는 진입점이다.
- 활성화 시 `EnemyRegistry`에 자기 자신을 등록한다.
- 비활성화 시 `EnemyRegistry`에서 해제한다.
- 필수 참조가 비어 있으면 오류를 남기고 비활성화한다.

### EnemyRegistry

`EnemyRegistry`는 현재 씬의 활성 적 목록을 관리하는 씬 단위 싱글톤 등록소다.

현재 공개 목록:

- `IReadOnlyList<EnemyContext> Enemies`

역할:

- 적 목록 관리만 담당한다.
- 적 AI, 애드 판정, 시야 계산, 상태 전환은 담당하지 않는다.
- `GridMoveRiskEvaluator`, `EnemyAlertCoordinator`가 이 목록을 조회한다.

## Enemy 시야

### GridDirection

`GridDirection`은 적이 바라보는 방향을 상하좌우 4방향으로 제한한다.
대각선 방향은 현재 바라보는 방향으로 사용하지 않는다.

`GridDirectionUtility`는 방향을 전방 오프셋과 오른쪽 오프셋으로 변환한다.

### EnemyGridSight

`EnemyGridSight`는 적의 감지 칸을 계산한다.

책임:

- 현재 적 위치와 방향 기준 정면 부채꼴 시야 계산.
- 주변 근접 감지 칸 계산.
- `DetectedPositions` 목록과 내부 HashSet 갱신.
- `CanDetect(GridPosition)`으로 지정 칸 감지 여부 반환.
- `SightRefreshed` 이벤트 발행.

시야 규칙:

- 정면 시야는 전방 거리 1에서 1칸, 거리 2에서 3칸, 거리 3에서 5칸처럼 좌우 폭이 넓어진다.
- 장애물 칸은 정면 시야에 포함하지 않는다.
- 같은 레인에서 장애물을 만나면 그 뒤 칸은 차단한다.
- 근접 감지는 방향과 장애물 영향 없이 주변 칸을 감지한다.

`EnemyGridSight`는 등록소를 직접 알지 않는다.
등록/해제 책임은 `EnemyContext`가 가진다.

## Enemy Alert 구조

### AlertTriggeredLogicEvent

`AlertTriggeredLogicEvent`는 플레이어가 실제 이동 중 적 시야에 처음 들어갔을 때 `GridMoveRiskEvaluator`가 발행한다.

전달 값:

- 발각 칸 `GridPosition`
- 최초 감지 적 시야 `EnemyGridSight`

현재 기준:

- 이동 1회당 첫 발각만 이벤트를 발행한다.
- 여러 적이 동시에 감지할 때 대표 감지자 선택 규칙은 아직 단순 등록 순서 기반이다.

### EnemyAlertCoordinator

`EnemyAlertCoordinator`는 씬 단위 애드 전파 조정자다.

책임:

- `ActionLogicEventBus`를 통해 `AlertTriggeredLogicEvent`를 처리한다.
- 이벤트의 `DetectingEnemy`를 우선 사용하고, 없으면 `EnemyGridSight`를 기준으로 `EnemyRegistry.Enemies`에서 최초 감지 적 `EnemyContext`를 찾는다.
- 최초 감지 적의 `EnemyData.AlertSpreadRange`를 읽는다.
- 최초 감지 적 위치 기준으로 등록된 모든 적과의 맨해튼 거리를 계산한다.
- 범위 안의 적에게 `EnemyAlertState.RequestAlert()`를 호출한다.
- 실제로 새로 `Alerted`가 된 적마다 `AlertDetected` 연출 이벤트와 `EnemyAlertedLogicEvent`를 추가한다.

현재 전파 방식:

- 단일 단계 전파다.
- 연쇄 전파는 아직 구현하지 않았다.
- 연쇄 전파는 맵 크기, 적 밀도, 경계 규칙이 정해진 뒤 BFS/큐 기반으로 확장한다.

### EnemyAlertState

`EnemyAlertState`는 적 하나의 현재 경계 상태를 보관한다.

현재 상태 단계:

- `Normal`: 평상 상태.
- `Alerted`: 플레이어 발각 또는 애드 전파로 발각된 상태.

책임:

- `CurrentLevel` 보관.
- `RequestAlert(GridPosition detectedPosition, EnemyContext sourceEnemy)`로 발각 요청 처리.
- 이미 같은 상태이면 중복 전환을 하지 않는다.
- 상태 변경 시 `AlertLevelChanged` 이벤트 발행.
- 현재는 상태 변경 로그를 출력한다.

후속 확장 후보:

- `Suspicious`
- `Combat`
- 적 AI 행동 전환
- 경고 UI/색상 변경

## Data 에셋

### ControllableUnitData

플레이어 조작 유닛의 AP, 이동, 해킹, 검, 근접 공격, 총 공격 수치와 필수 능력 구성을 보관한다.
실제 유효성 검사는 데이터를 사용하는 컴포넌트가 담당한다.

### EnemyData

적의 시야와 애드 전파 관련 수치를 보관한다.
실제 유효성 검사는 데이터를 사용하는 컴포넌트가 담당한다.

### HackableData

해킹 가능한 대상의 기본 데이터 형태다.
`HackableObject`가 해킹 가능 횟수와 해킹 상태를 관리할 때 사용한다.

### Dialogue 데이터

- `DialogueSequenceData`
- `DialogueStepData`
- `DialogueLineData`

인게임 말풍선 또는 추후 스토리 화면에서 사용할 대사 데이터 후보로 유지한다.

## Dialogue / VFX 유지 구조

### Dialogue

현재 말풍선 시스템은 S2-T에서도 유지한다.

주요 구성:

- `DialogueManager`: 대사 시퀀스 재생과 진행.
- `DialogueBubblePresenter`: 한 스텝의 말풍선 표시와 풀링.
- `DialogueSpeaker`: 발화자 태그와 말풍선 앵커 제공.
- `SpeechBubbleView`: 말풍선 UI, 한글 폰트, 타자기식 출력.

현재 대사 시스템은 S2-T 턴/이동 루프와 강하게 결합되어 있지 않다.
외부 시스템이 `TryPlay()`와 `Advance()`를 호출하는 방식이다.

### VfxManager

`VfxManager`는 공용 VFX 풀이다.
현재 S2-T 핵심 루프와 직접 연결된 상태는 아니지만, 공격/해킹/경고 연출 확장 후보로 유지한다.

## Combat / Hacking 인터페이스

### IDamageable

피해를 받을 수 있는 대상의 공통 규약이다.
현재 공격 행동은 `ApplyDamageLogicEvent`를 발행하고 `DamageResolutionCoordinator`가 대상의 `IDamageable.TakeDamage()`를 호출한다.

### IHackable

해킹 가능한 대상의 공통 규약이다.
`HackableObject`가 구현하며 `PlayerHackAction`이 대상 검사, AP 소비, 해킹 적용과 연출 이벤트 생성을 담당한다.

## SwordMove / 검 Visual 연출

`SwordMove`는 검 투척, 검 기반 해킹, 회수로 발생하는 검 Visual 위치 변경을 하나의 연출 이벤트로 표현한다.
논리 검 위치는 기존 `SwordThrownLogicEvent`, `HackCompletedLogicEvent`, `SwordRecalledLogicEvent`와 `PlayerSwordState`가 관리하고, `SwordActionPresenter`는 화면 표시만 담당한다.

현재 이동 종류:

- `Throw`: 일반·피해 검 투척의 목표 칸으로 이동한다.
- `Hack`: 해킹 대상 주변에서 계산된 실행 칸으로 이동한다.
- `Recall`: 플레이어 VisualRoot의 현재 방향 기준 좌우 상단 위치로 복귀한다.

연출 순서:

- 일반 투척: `SwordMove(Throw)`.
- 피해 투척: `SwordMove(Throw) -> CombatAction`.
- 검 기반 해킹: `SwordMove(Hack) -> Hack`.
- 회수: `SwordMove(Recall)`.

`SwordActionPresenter`는 별도 SwordAnchor를 사용하지 않는다.
회수 상태에서는 검 Visual을 플레이어 `ActorVisualController`의 Transform 자식으로 두고, `IsFacingRight`에 따라 회수 오프셋 X를 반전한다.
투척과 해킹에서는 검을 월드 공간으로 분리하고 실제 현재 위치에서 목표 위치까지의 방향으로 회전한 뒤 목표 칸에 즉시 배치한다.
검 소지 근접 공격은 `CombatActionPresenter.CombatPresentationStarted`, `CombatPresentationCompleted` 알림을 받아 공격 방향으로 검을 기울였다가 회수 자세로 복구한다.

현재 `Tset` 씬의 검 능력 유닛 설정:

- `Recalled Offset`: `(1, 1.5, 0)`.
- `Deployed Position Offset`: `(0, 0.1, 0)`.
- `Melee Tilt Angle`: `70`.
- `Path Vfx Id`: `None`.
- 새 검 Visual과 `PlayerSwordState`, `ActorVisualController`, 씬 단일 `CombatActionPresenter` 참조 연결 완료.
- 현재 코드와 씬 연결 상태로 Unity 플레이 모드 테스트 완료.

## 현재 한계

- 플레이어 이동 중 발각 시 이동을 일시 정지하지 않는다.
- 발각 시 카메라 줌, 경고 UI, 컷인 연출은 없다.
- 적 상태는 `Normal`, `Alerted` 두 단계이며 애드 전파는 단일 단계다.
- 플레이어 시야/정보 공개 기준이 없어 보이지 않는 적의 위험 경고 숨김은 아직 없다.
- 검 Visual 이동과 플레이 모드 확인은 완료했지만 이동 경로 VFX는 `VfxId.None`으로 비어 있다.
- 현재 검 이동 각도와 즉시 위치 변경 방식은 기능 확인 기준이며, 최종 연출 품질을 위해 방향별 각도·위치·움직임 타이밍을 추가 튜닝해야 한다. 이 작업은 현재 단계에서 더 진행하지 않고 후속 연출 폴리싱 단계에서 다시 다룬다.
- 총·적 공격의 발사체, 탄흔, 타격 이펙트는 아직 없다.
- `MovePresentationDataTest`는 현재 `UseMoveAnimation`, `PlayIdleAnimationOnComplete`가 꺼져 있어 이동 애니메이션 상태 전환은 활성화하지 않은 상태다.
- 발각용 `Alert` 애니메이션은 선택 기능이며 현재 테스트 적에서는 비활성화되어 색상 점멸만 사용한다.
- 클리어 UI, 결과 화면, 다음 스테이지 전환, 메뉴/스토리 화면은 아직 구현하지 않았다.

## 다음 작업

1. 검 이동 경로 이펙트가 준비되면 `VfxManager`에 등록하고 `pathVfxId`를 연결한다.
2. 해킹 시작/성공/대상 반응 연출을 추가한다.
3. 총·근접·적 공격의 발사체/탄흔/타격 이펙트를 통합 전투 연출 위에 추가한다.
4. 캐릭터 연출이 안정된 뒤 공격자와 피격자 사이를 강조하는 카메라 줌/컷 연출을 추가한다.
5. 핵심 이펙트 연결이 끝난 후 검 이동 각도, 위치, 움직임 타이밍을 최종 연출 기준으로 다시 튜닝한다.


## Stage Goal / 승리 조건

### StageGoal

`StageGoal`은 스테이지 클리어 목표 칸을 나타내는 컴포넌트다.

현재 값:

- `GoalPosition`: 플레이어가 도착해야 하는 목표 그리드 칸 좌표.
- Scene 뷰 Gizmo 표시 옵션과 색상/크기 설정.

역할:

- 목표 칸 좌표를 제공한다.
- `IsGoalPosition()`으로 지정 칸이 목표 칸인지 확인한다.
- 직접 클리어 판정이나 UI 처리는 하지 않는다.

### StageGoalManager

`StageGoalManager`는 플레이어 진영 유닛의 이동 완료 논리 이벤트를 감시해 목표 칸 도달 시 스테이지 클리어를 알린다.

책임:

- `ActionLogicEventBus`에서 플레이어 진영 유닛의 `MoveCompletedLogicEvent`를 처리한다.
- 시작 시 `TacticalUnitRegistry.PlayerControllableUnits`의 현재 위치도 확인한다.
- 이동 완료 위치가 `StageGoal.GoalPosition`과 같으면 클리어 처리한다.
- `StageCleared` 이벤트를 발행한다.
- 같은 행동 문맥에 `StageClearedLogicEvent`와 `StageCleared` 연출 이벤트를 추가한다.

현재 한계:

- 클리어 UI, 결과 화면, 다음 스테이지 전환은 아직 구현하지 않았다.
- 복수 목표, 조건부 목표, 회수/해킹 목표는 아직 없다.

### StageStateManager

`StageStateManager`는 스테이지 전체 진행 상태를 관리하는 컴포넌트다. 개별 목표 판정은 `StageGoalManager`가 맡고, 상태 확정과 후속 시스템 연결점은 `StageStateManager`가 담당한다.

현재 상태 단계:

- `Playing`: 스테이지 진행 중.
- `Cleared`: 목표 달성으로 스테이지 클리어가 확정된 상태.
- `Failed`: 실패 조건으로 스테이지 실패가 확정된 상태.

책임:

- `StageGoalManager.StageCleared` 이벤트를 구독한다.
- 목표 달성 이벤트를 받으면 `Playing`에서 `Cleared`로 전환한다.
- 외부 실패 조건 연결을 위해 `RequestFail()`을 제공한다.
- 상태 변경 시 `StageStateChanged` 이벤트를 발행한다.
- 현재 단계에서는 상태 변경 로그만 출력한다.

후속 확장 후보:

- 클리어/실패 UI 표시.
- 플레이어 입력 잠금.
- 결과 화면 또는 다음 스테이지 전환.
- 턴 제한, 플레이어 사망, 발각 즉시 실패 같은 실패 조건 연결.

## 판정 / 연출 분리 규칙

S2-T의 행동 처리는 턴제 전술 게임 기준으로 판정과 연출을 분리한다. 이 규칙은 이동, 공격, 해킹, 오브젝트 조작, 적 AI 반응, 발각 연출에 공통으로 적용한다.

핵심 원칙:

- 판정 시스템은 논리 오브젝트 기준으로 결과를 먼저 계산한다.
- 연출 시스템은 계산된 결과를 화면에 순서대로 보여준다.
- 연출 오브젝트는 판정에 관여하지 않는다.
- 판정 결과는 연출 이벤트로 변환되어 연출 큐에 들어간다.
- 연출 큐가 실행 중일 때는 플레이어 입력과 인게임 UI 조작을 막는다.
- 연출 큐가 비면 연출 종료로 보고 다음 입력을 받을 수 있다.

### 논리 오브젝트와 연출 오브젝트

논리 오브젝트는 실제 게임 상태를 가진다.

예:

- `GridActor`: 실제 그리드 좌표, 점유 상태, 이동 판정 기준.
- `PlayerContext`, `EnemyContext`: 판정에 필요한 핵심 컴포넌트 참조.
- `ActionPoint`, `EnemyAlertState`, `StageStateManager`: 실제 규칙 상태.

연출 오브젝트는 플레이어에게 보여주는 화면 상태만 담당한다.

예:

- 플레이어/적 스프라이트 루트.
- 이동 슬라이드, 공격, 피격, 해킹, 발각, 카메라 연출.
- 향후 `GridActorView`, `GridActorVisual`, `ActorPresentation` 같은 컴포넌트 후보.

규칙:

- 논리 오브젝트는 입력 또는 AI 판정 시점에 즉시 갱신될 수 있다.
- 연출 오브젝트는 논리 결과를 따라가며 큐 순서대로 화면을 재생한다.
- 연출 중 다른 시스템이 판정에 필요한 위치나 상태를 확인할 때는 논리 오브젝트와 저장된 결과 데이터를 기준으로 판단한다.

### Actor 계층 기준

플레이어와 향후 NPC/적 Actor는 논리, 연출 이벤트 처리, 실제 시각 표시를 계층으로 나눈다.

기준 계층:

```text
Actor Root
- PlayerLogic 또는 EnemyLogic
  - GridActor
  - Context
  - AP, 이동, 시야, 상태 같은 논리 컴포넌트

- ActorPresentation
  - GridActorMovePresenter
  - ActorAttackPresenter
  - ActorHackPresenter
  - ActorDamagePresenter
  - 기타 연출 이벤트 처리 컴포넌트

- VisualRoot
  - SpriteRenderer
  - Animator
  - ActorVisualController 같은 시각 제어 컴포넌트
```

현재 플레이어 오브젝트의 기존 `GridActor` 자식 오브젝트는 역할을 유지하고 이름만 `PlayerLogic`으로 바꾸는 방향으로 한다. `PlayerContext`는 이 논리 계층의 핵심 참조 주머니로 유지한다.

`ActorPresentation`은 연출 큐 이벤트를 직접 구독하고 처리하는 컴포넌트들이 붙는 계층이다. 기존 후보 이름인 `GridActorView`는 단순 표시 Transform처럼 보일 수 있으므로, 이동/공격/해킹/피격 같은 연출 이벤트 처리자들이 모이는 계층 이름으로는 `ActorPresentation`을 우선 후보로 둔다.

`VisualRoot`는 실제 스프라이트, 애니메이터, 시각 제어 컴포넌트를 가지는 표시 루트다. 이동, 공격, 해킹 같은 Presenter는 필요할 때 `VisualRoot` 또는 `ActorVisualController`에 시각 작업을 요청한다.

### Presenter / VisualRoot 완료 책임

연출 큐의 완료 신호는 Presenter가 최종 책임진다.

흐름:

```text
ActionPresentationQueue
-> Presenter가 PresentationEvent 수신
-> Presenter가 VisualRoot 또는 ActorVisualController에 시각 작업 요청
-> VisualRoot 쪽 작업 완료 콜백
-> Presenter가 PresentationEventHandle.Complete() 호출
```

규칙:

- `VisualRoot`는 연출 큐를 직접 알지 않는다.
- `VisualRoot`는 시각 작업 완료 사실만 Presenter에게 알린다.
- `PresentationEventHandle.Complete()`는 해당 이벤트를 처리한 Presenter가 호출한다.
- 큐 입장에서는 Presenter만 이벤트 처리자다.
- 이 기준을 지키면 VisualRoot의 내부 애니메이션, 스프라이트, 보간 방식이 바뀌어도 큐 구조는 흔들리지 않는다.

### 연출 데이터 기준

연출 데이터는 처음부터 하나의 거대한 공용 Context에 넣지 않고, 기능별 Presenter가 각자 가진다.

예:

- `GridActorMovePresenter`: 이동 시간, 이동 커브, 이동 중 애니메이션 이름.
- `ActorAttackPresenter`: 공격 딜레이, 타격 타이밍, 공격 애니메이션 이름.
- `ActorHackPresenter`: 해킹 연출 시간, 이펙트, 완료 타이밍.
- `ActorDamagePresenter`: 피격 흔들림, 색상 점멸, HP 표시 타이밍.

데이터가 커지면 기능별 `ScriptableObject`로 분리한다. 예를 들어 `MovePresentationData`, `AttackPresentationData`, `HackPresentationData` 같은 식으로 확장한다. `Context`는 계속 참조 주머니로만 유지하고, 튜닝 수치와 연출 정책은 넣지 않는다.

### 행동 처리 기준

대부분의 행동은 입력 또는 AI 결정 시점에 결과를 먼저 확정한다.

예:

- 이동 경로와 AP 비용 계산.
- 공격 명중/피해/상태 변화 계산.
- 해킹 성공/실패 및 대상 상태 변화 계산.
- 문 개폐, 검 투척, 검 회수 같은 오브젝트/장비 조작 결과 계산.
- 적 AI 반응 결과 계산.

이후 결과에 따라 연출 이벤트를 큐에 넣는다.

예:

- 플레이어 이동 연출.
- 발각 연출.
- 적 AI 반응 이동 연출.
- 공격/피격 연출.
- 해킹 연출.
- 클리어/실패 결과 연출.

### 연출 큐 A안

연출 큐는 씬 단위 싱글톤 매니저로 둔다. 후보 이름은 `ActionPresentationQueue`다.

역할:

- `PresentationEvent`를 큐에 저장한다.
- 판정 시스템의 요청에 따라 이벤트를 큐에 추가한다.
- 판정이 끝난 뒤 큐를 하나씩 실행한다.
- 현재 이벤트를 구독자에게 브로드캐스트한다.
- 이벤트 처리자가 완료 신호를 보낼 때까지 기다린다.
- 완료 신호를 받으면 다음 이벤트를 실행한다.
- 큐가 비면 연출 종료 상태가 된다.
- `IsPlaying`으로 연출 실행 중 여부를 제공한다.

큐 매니저는 판정이나 실제 연출 내용을 알지 않는다. 큐 매니저는 순서 제어와 완료 대기만 담당한다.

### PresentationEvent

`PresentationEvent`는 연출 큐에 들어가는 결과 데이터다.

초기 포함 후보:

- `Type`: 이벤트 종류.
- `Actor`: 움직이거나 연출 대상이 되는 `GridActor`.
- `Enemy`: 발각 또는 적 반응 대상 `EnemyContext`.
- `FromPosition`: 시작 그리드 좌표.
- `ToPosition`: 목표 그리드 좌표.
- `EventPosition`: 발각, 피격, 해킹 등 사건이 발생한 그리드 좌표.
- `Message`: 임시 로그나 UI 표시용 문장.

`PresentationEventType` 후보:

- `MoveActor`
- `AlertDetected`
- `EnemyReactionMove`
- `Attack`
- `Hack`
- `Interact`
- `StageCleared`
- `StageFailed`

처음에는 enum과 구조체 기반으로 시작하고, 이벤트 데이터가 복잡해지면 타입별 이벤트 클래스로 분리한다.

### 이벤트 처리 방식

브로드캐스트 + bool 반환 방식으로 처리한다.

규칙:

- 큐 매니저는 현재 이벤트와 완료 핸들을 구독자에게 전달한다.
- 구독자는 자신이 처리할 이벤트이면 `true`를 반환한다.
- 구독자는 자신이 처리하지 않는 이벤트이면 `false`를 반환한다.
- `true`를 반환한 구독자는 연출이 끝났을 때 반드시 완료 핸들의 `Complete()`를 호출한다.
- 아무도 `true`를 반환하지 않으면 큐 매니저는 경고 로그를 남기고 해당 이벤트를 자동 완료 처리한다.

개념 API:

```csharp
public event Func<PresentationEvent, PresentationEventHandle, bool> PresentationEventStarted;
```

완료 핸들 규칙:

- `PresentationEventHandle.Complete()`는 한 번만 유효하다.
- 중복 완료 호출은 무시한다.
- 완료 호출 누락은 큐 정지 원인이 되므로 개발 중 타임아웃/경고 로그를 검토한다.

### 입력 잠금 기준

플레이어 입력과 인게임 플레이 UI 조작은 다음 조건에서만 허용한다.

```text
StageStateManager.IsPlaying == true
&& ActionPresentationQueue.IsPlaying == false
```

즉 스테이지가 `Cleared` 또는 `Failed` 상태이거나 연출 큐가 실행 중이면 이동, 공격, 해킹, 조작 버튼 입력을 막는다.

### 이동 예시

플레이어 이동 처리 흐름은 다음 기준으로 확장한다.

```text
1. 플레이어가 이동을 입력한다.
2. 논리 플레이어가 이동 경로, AP 비용, 발각 지점, 적 반응 결과를 계산한다.
3. 논리 상태와 결과 데이터를 확정한다.
4. 필요한 연출 이벤트를 큐에 순서대로 넣는다.
5. 큐 실행자가 플레이어 이동 연출을 재생한다.
6. 이동 연출 중 발각 지점에서 발각 연출 이벤트를 처리한다.
7. 적 AI 반응 연출을 처리한다.
8. 플레이어 남은 이동 연출을 처리한다.
9. 큐가 비면 입력 가능 상태로 돌아간다.
```

현재 `PlayerGridMoveAction`은 아직 즉시 이동 구조이므로, 후속 작업에서 이 기준에 맞춰 논리 이동과 연출 이동을 분리한다.

## Presentation / 연출 큐 구현

### PresentationEventType

`PresentationEventType`은 연출 큐에서 처리할 이벤트 종류를 나타내는 enum이다.

현재 값:

- `None`
- `MoveActor`
- `AlertDetected`
- `EnemyReactionMove`
- `Attack`
- `Hack`
- `Interact`
- `StageCleared`
- `StageFailed`

### PresentationEvent

`PresentationEvent`는 연출 큐에 들어가는 단일 연출 이벤트 데이터다.

현재 보관 값:

- `Type`: 연출 이벤트 종류.
- `Actor`: 움직이거나 연출 대상이 되는 `GridActor`.
- `Enemy`: 발각 또는 적 반응 대상 `EnemyContext`.
- `FromPosition`: 시작 그리드 좌표.
- `ToPosition`: 목표 그리드 좌표.
- `EventPosition`: 사건이 발생한 그리드 좌표.
- `Message`: 임시 로그나 UI 표시용 문장.

현재 정적 생성 함수:

- `MoveActor()`
- `AlertDetected()`
- `EnemyReactionMove()`
- `StageCleared()`
- `StageFailed()`

### PresentationEventHandle

`PresentationEventHandle`은 이벤트 처리자가 연출 완료를 큐 매니저에 알리는 손잡이다.

규칙:

- 이벤트를 처리하겠다고 `true`를 반환한 구독자는 반드시 `Complete()`를 호출한다.
- `Complete()`는 한 번만 유효하다.
- 중복 완료 호출은 무시한다.

### ActionPresentationQueue

`ActionPresentationQueue`는 씬 단위 싱글톤 연출 큐다.

책임:

- `PresentationEvent`를 큐에 추가한다.
- `PlayQueuedEvents()` 요청 시 큐를 순서대로 실행한다.
- 현재 이벤트를 `PresentationEventStarted` 이벤트로 브로드캐스트한다.
- 처리자가 `PresentationEventHandle.Complete()`를 호출할 때까지 기다린다.
- 처리자가 없으면 경고 로그 후 자동 완료한다.
- 완료 신호가 오래 오지 않으면 경고 로그를 남긴다.
- 큐가 비면 `QueueEmptied` 이벤트를 발행한다.
- `IsPlaying`으로 연출 실행 중 여부를 제공한다.

이벤트 구독 규칙:

```csharp
public event Func<PresentationEvent, PresentationEventHandle, bool> PresentationEventStarted;
```

- 자기 이벤트가 아니면 `false`를 반환한다.
- 자기 이벤트이면 `true`를 반환하고 연출 종료 시 `handle.Complete()`를 호출한다.
- 현재 구조에서는 이벤트별 책임 처리자를 하나로 두는 것을 권장한다.

### DebugPresentationEventReceiver

`DebugPresentationEventReceiver`는 연출 큐 흐름 확인용 임시 컴포넌트다.

역할:

- 모든 이벤트를 처리 대상으로 받을 수 있다.
- 받은 이벤트를 로그로 출력한다.
- 설정에 따라 즉시 `Complete()`를 호출한다.

### DebugPresentationQueueTester

`DebugPresentationQueueTester`는 연출 큐에 샘플 이벤트를 넣는 임시 테스트 컴포넌트다.

현재 샘플 이벤트:

- `MoveActor`
- `AlertDetected`
- `StageCleared`

사용 기준:

- `ActionPresentationQueue`와 `DebugPresentationEventReceiver`가 씬에 있어야 한다.
- `enqueueOnStart`를 켜면 시작 시 샘플 이벤트를 큐에 넣는다.
- `playAfterEnqueue`가 켜져 있으면 샘플 이벤트 추가 후 바로 큐 실행을 요청한다.

현재 한계:

- 실제 이동, 카메라, UI, 애니메이션 연출은 아직 연결하지 않았다.
- `PlayerGridMoveAction`, `EnemyAlertCoordinator`, `StageStateManager`는 아직 `ActionPresentationQueue`에 실제 이벤트를 넣지 않는다.
- 다음 단계에서 이동 연출 이벤트부터 실제 게임 흐름에 연결한다.

### MovePresentationData

`MovePresentationData`는 `GridActorMovePresenter`가 사용하는 이동 연출 튜닝 데이터다.

현재 값:

- `MoveDuration`: 이동 이벤트 하나를 화면에서 재생하는 시간.
- `MoveCurve`: 이동 시간 진행률을 실제 위치 보간률로 바꾸는 곡선.
- `UseMoveAnimation`: 이동 시작 시 애니메이션 재생 요청 여부.
- `MoveAnimationStateName`: 이동 중 재생할 Animator 상태 이름.
- `MoveAnimationCrossFadeDuration`: 이동 애니메이션 전환 시간.
- `PlayIdleAnimationOnComplete`: 이동 종료 후 대기 애니메이션 재생 요청 여부.
- `IdleAnimationStateName`: 이동 종료 후 재생할 Animator 상태 이름.
- `IdleAnimationCrossFadeDuration`: 대기 애니메이션 전환 시간.

데이터 에셋 자체는 유효성 검사를 맡지 않는다. 실제 검사는 이 데이터를 사용하는 `GridActorMovePresenter.HasValidData()`에서 수행한다.

### ActorVisualController

`ActorVisualController`는 `VisualRoot`에 붙는 시각 제어 컴포넌트다. 연출 큐를 직접 알지 않고, Presenter의 요청에 따라 SpriteRenderer와 Animator 같은 실제 시각 컴포넌트를 제어한다.

현재 책임:

- `SpriteRenderer` 참조 보관.
- `Animator` 참조 보관.
- 이동 시작 시 `MovePresentationData` 기준 이동 애니메이션 재생 요청.
- 이동 종료 시 설정에 따라 대기 애니메이션 재생 요청.
- Animator 상태 이름이 비어 있거나 Animator가 없으면 오류 로그를 남기고 실패를 반환한다.

규칙:

- `ActorVisualController`는 `PresentationEventHandle`을 직접 알지 않는다.
- 큐 완료 여부는 Presenter가 판단한다.
- 현재는 Animator 상태 이름을 직접 재생하는 1차 틀이다. 이후 필요하면 Trigger/Bool 파라미터 방식으로 확장한다.

### GridActorMovePresenter

`GridActorMovePresenter`는 `ActorPresentation` 계층에 붙는 이동 연출 Presenter다.

현재 책임:

- `ActionPresentationQueue.PresentationEventStarted`를 구독한다.
- `PresentationEventType.MoveActor` 중 자기 `GridActor` 대상 이벤트만 처리한다.
- 설정에 따라 `PresentationEventType.EnemyReactionMove`도 같은 방식으로 처리한다.
- `GridManager.GridToWorld()`로 `FromPosition`, `ToPosition`을 월드 좌표로 변환한다.
- `VisualRoot` Transform을 시작 위치에서 목표 위치까지 `MovePresentationData.MoveCurve` 기준으로 보간한다.
- 이동 시작/종료 애니메이션은 `ActorVisualController`에 요청한다.
- 이동 연출이 끝나면 `PresentationEventHandle.Complete()`를 호출한다.
- 비활성화 중 진행 중인 이벤트가 있으면 큐 정지를 막기 위해 완료 처리한다.

필수 참조:

- `TargetActor`: 이 Presenter가 처리할 논리 `GridActor`.
- `VisualRoot`: 실제 화면상 이동시킬 Transform.
- `VisualController`: VisualRoot의 `ActorVisualController`.
- `MoveData`: 이동 연출 데이터.

현재 한계:

- `PlayerGridMoveAction`의 실제 게임 이동 흐름과는 아직 연결하지 않았다.
- 디버그 테스트용 `DebugPresentationQueueTester.SampleMoveActor`에 같은 `GridActor`를 연결해야 `MoveActor` 이벤트를 받을 수 있다.
- 카메라, 발각 지점 중간 정지, 적 반응 삽입은 아직 없다.

### 연출 큐 테스트 상태

2026-06-17 기준 Unity 플레이 모드에서 디버그 연출 큐 흐름을 확인했다.

확인 내용:

- `DebugPresentationQueueTester`가 샘플 `MoveActor`, `AlertDetected`, `StageCleared` 이벤트를 큐에 추가한다.
- `ActionPresentationQueue`가 이벤트를 순서대로 꺼내 `PresentationEventStarted`로 브로드캐스트한다.
- `DebugPresentationEventReceiver`가 이벤트를 수신하고 `PresentationEventHandle.Complete()`를 호출한다.
- 완료 신호를 받은 뒤 다음 이벤트가 실행된다.
- 모든 이벤트가 끝나면 큐 종료 로그가 출력된다.

현재 상태:

- 연출 큐의 기본 순차 실행과 완료 신호 흐름은 확인됐다.
- `GridActorMovePresenter` 1차 구현으로 `PresentationEvent.MoveActor`를 받아 `VisualRoot`를 이동시키는 Presenter 틀이 추가됐다.
- 다음 작업은 Unity 씬에서 `MovePresentationData`, `ActorVisualController`, `GridActorMovePresenter`를 연결해 실제 VisualRoot 이동을 확인하는 것이다.
- 이후 `PlayerGridMoveAction`의 이동 결과를 `PresentationEvent.MoveActor`로 큐에 넣는 실제 게임 흐름 연결로 넘어간다.


## Action Resolution / 논리 이벤트 처리 구조

S2-T의 행동 처리는 최초 명령에서 파생되는 논리 사건을 `ActionResolutionContext` 안에서 처리한 뒤, 논리 처리가 끝나면 `ActionPresentationQueue`를 재생하는 기준으로 확장한다.

핵심 기준:

- 상위 실행자는 개별 논리 시스템을 직접 감시하지 않는다.
- 행동 중 발생한 논리 사건은 `IActionLogicEvent`로 `ActionResolutionContext`에 발행한다.
- `ActionResolutionContext.Resolve()`는 논리 이벤트 큐가 빌 때까지 `ActionLogicEventBus`를 통해 활성 핸들러에 이벤트를 전달한다.
- 처리 중 새 논리 이벤트가 생기면 같은 문맥 안에서 이어서 처리한다.
- 논리 이벤트 큐가 비면 해당 행동의 논리 처리가 끝난 것으로 본다.
- 시간이 걸리는 화면 표현은 `PresentationEvent`로 `ActionPresentationQueue`에 넣고, 논리 처리 종료 후 재생한다.

현재 1차 구성:

- `ActionResolutionContext`: 행동 하나의 논리 이벤트 큐와 연출 이벤트 추가 통로.
- `ActionLogicEventBus`: 활성 `IActionLogicEventHandler` 목록에 논리 이벤트를 전달하는 정적 통로.
- `MoveStepEnteredLogicEvent`: 액터가 이동 경로의 한 칸에 진입했음을 알린다.
- `MoveCompletedLogicEvent`: 액터의 이동 행동이 최종 칸에서 끝났음을 알린다.
- `AlertTriggeredLogicEvent`: 플레이어가 적 시야에 들어와 발각됐음을 알린다.
- `EnemyAlertedLogicEvent`: 적 하나가 발각 상태로 바뀌었음을 알린다.
- `StageClearedLogicEvent`: 스테이지 목표 달성이 확인됐음을 알린다.
- `PlayerActionFlowController`: 플레이어 이동 행동 실행, 논리 이벤트 처리, 연출 큐 재생 시점을 조정한다.

현재 연결:

- `PlayerGridMoveAction`은 이동 경로를 계산하고 AP를 소비한 뒤, 각 칸 이동마다 `MoveActor` 연출 이벤트와 `MoveStepEnteredLogicEvent`를 추가한다.
- `GridMoveRiskEvaluator`는 플레이어 `GridActor`의 `MoveStepEnteredLogicEvent`를 처리해 위험 칸 진입을 확인하고 `AlertTriggeredLogicEvent`를 추가한다.
- `EnemyAlertCoordinator`는 `AlertTriggeredLogicEvent`와 `DamageAppliedLogicEvent`를 처리해 경계 상태 전환, 애드 전파, `EnemyAlertedLogicEvent` 발행을 담당한다.
- `EnemyAlertReactionCoordinator`는 자기 `EnemyContext`의 `EnemyAlertedLogicEvent`를 처리해 경계 반응 엄폐 이동과 `EnemyReactionMove` 연출 이벤트를 추가한다.
- `StageGoalManager`는 플레이어 `GridActor`의 `MoveCompletedLogicEvent`를 처리해 목표 도착을 확인하고, 클리어 시 `StageClearedLogicEvent`와 `StageCleared` 연출 이벤트를 추가한다.
- `StageStateManager`는 `StageClearedLogicEvent`를 처리해 스테이지 상태를 `Cleared`로 바꾼다.

### CanHandle 필터링 기준

`ActionLogicEventBus`는 `CanHandle()`이 true인 모든 핸들러에 이벤트를 전달한다.
따라서 개별 대상이 정해진 핸들러는 `CanHandle()`에서 이벤트 타입뿐 아니라 대상 참조까지 확인한다.

현재 기준:

- `EnemyAlertReactionCoordinator`: `EnemyAlertedLogicEvent.Enemy`가 자기 `EnemyContext`일 때만 처리한다.
- `GridMoveRiskEvaluator`: 플레이어 `GridActor`의 `MoveStepEnteredLogicEvent`, `MoveCompletedLogicEvent`만 처리한다.
- `StageGoalManager`: 플레이어 `GridActor`의 `MoveCompletedLogicEvent`만 처리한다.
- `PlayerSwordState`: 플레이어 `GridActor`의 `HackCompletedLogicEvent`만 처리한다.
- `EnemyAlertCoordinator`: 씬 단위 애드 전파 담당이므로 `AlertTriggeredLogicEvent`, `DamageAppliedLogicEvent` 타입 기준으로 처리한다.
- `StageStateManager`: 스테이지 전체 상태 담당이므로 `StageClearedLogicEvent` 타입 기준으로 처리한다.

`Handle()` 내부의 대상 검사는 직접 호출이나 추후 이벤트 버스 구조 변경에 대한 2차 방어로 유지한다.

현재 한계:

- 이동 입력은 `PlayerMoveInputController`로 분리했다. `PlayerGridMoveAction`은 입력을 직접 처리하지 않고, 이동 선택 상태와 이동 판정/실행 책임만 가진다.
- 경계 반응은 Alerted 진입 시 수동 엄폐 이동까지만 처리한다.
- 적 턴의 능동 AI 판단과 공격 위치 선정은 아직 구현하지 않았다.

## ActorPresentationSynchronizer

`ActorPresentationSynchronizer`는 논리 `GridActor`와 화면 표시용 `VisualRoot`의 시작 위치를 맞추는 컴포넌트다.

역할:

- `TargetActor.GridPosition`을 `GridManager.GridToWorld()`로 변환한다.
- `VisualRoot.position`을 논리 Actor의 현재 칸 위치로 맞춘다.
- `syncOnStart`를 켜면 씬 시작 시 자동 동기화한다.
- 필요하면 `ForceSyncToActorPosition()`으로 강제 동기화할 수 있다.

배치 기준:

- `ActorPresentation` 계층 또는 해당 Actor의 연출 관리 오브젝트에 붙인다.
- `TargetActor`에는 논리 계층의 `GridActor`를 연결한다.
- `VisualRoot`에는 실제 스프라이트/애니메이터가 붙은 표시 루트를 연결한다.

### PlayerMoveInputController (제거됨)

`PlayerMoveInputController`는 플레이어 이동 행동의 임시 입력 담당 컴포넌트다.

현재 책임:

- M 키로 이동 행동 선택 요청.
- 마우스 화면 좌표를 `GridManager.WorldToGrid()` 기준 목표 칸으로 변환.
- 이동 선택 중 목표 칸 경로 미리보기 갱신 요청.
- 좌클릭 시 `PlayerActionFlowController.TryExecuteMove()`를 통해 이동 행동 실행 요청.
- `PlayerActionFlowController` 참조는 필수이며, `PlayerGridMoveAction`을 직접 실행하지 않는다.
- 우클릭 또는 Escape로 이동 선택 취소 요청.

분리 기준:

- `PlayerMoveInputController`: 입력 해석과 행동 요청.
- `PlayerActionFlowController`: 행동 실행 흐름과 연출 큐 실행 시점 조정.
- `PlayerGridMoveAction`: 이동 가능 범위, 경로 계산, AP 소비, 논리 이동, 논리/연출 이벤트 발행.

`PlayerGridMoveAction`에는 더 이상 `Update()` 기반 입력 처리와 `UnityEngine.InputSystem` 의존성을 두지 않는다.




### ActionResolutionContext Resolve 호출 규칙

`ActionResolutionContext.Resolve()`는 논리 이벤트 큐를 비울 때까지 현재 등록된 `IActionLogicEventHandler`들에게 이벤트를 전달한다.

현재 기준:

- `PlayerActionFlowController`는 행동 실행 함수가 반환된 뒤 마지막으로 `Resolve()`를 호출한다.
- 이 마지막 호출은 아직 처리되지 않은 논리 이벤트를 정리하는 안전망이다.
- `PlayerGridMoveAction`은 이동 경로의 각 칸마다 `MoveStepEnteredLogicEvent`를 발행한 직후 `Resolve()`를 호출한다.
- 이 칸 단위 `Resolve()`는 `MoveActor` 연출 이벤트 사이에 `AlertDetected`, `StageCleared` 같은 후속 연출 이벤트를 올바른 순서로 끼워 넣기 위한 의도적인 처리다.

예:

```text
MoveActor 2 -> 3
MoveStepEnteredLogicEvent Resolve
AlertDetected enqueue
MoveActor 3 -> 4
```

규칙:

- 액션은 연출 이벤트 순서가 중요한 지점에서 `Resolve()`를 직접 호출할 수 있다.
- 상위 `PlayerActionFlowController`의 마지막 `Resolve()`는 누락된 후속 논리 이벤트 처리용으로 유지한다.
- 새 액션을 만들 때는 논리 이벤트를 발행만 할지, 중간 순서 보장을 위해 즉시 `Resolve()`할지 명확히 정한다.

### PlayerGridMoveAction 직접 이벤트 제거

`PlayerGridMoveAction`의 `MoveStepEntered`, `MoveCompleted` 직접 C# 이벤트는 제거했다.

현재 이동 중 판정 통로:

- 칸 진입: `MoveStepEnteredLogicEvent`
- 이동 완료: `MoveCompletedLogicEvent`

직접 C# 이벤트는 이동 범위 표시, 경로 미리보기처럼 단순 표시/입력 상태 알림에만 유지한다.
이동 판정, 발각, 애드, 목표 달성 같은 게임 규칙 처리는 `ActionResolutionContext` 논리 이벤트 통로를 사용한다.

## 2026-06-20 테스트 기준 정리

오늘 기준 S2-T 행동 처리의 핵심 규칙은 다음과 같이 확정한다.

- 행동 판정은 논리 시스템에서 먼저 끝낸다.
- 논리 처리 중 파생되는 사건은 `ActionResolutionContext`의 논리 이벤트 큐로 처리한다.
- 논리 이벤트 큐가 비면 해당 행동의 논리 처리가 끝난 것으로 본다.
- 화면 연출은 `ActionPresentationQueue`에 쌓인 `PresentationEvent`를 순서대로 재생한다.
- 플레이어 이동은 논리상 먼저 최종 경로를 처리하고, 화면에서는 1칸 단위 `MoveActor` 이벤트가 따라오는 구조다.
- 이동 중 발각은 `MoveStepEnteredLogicEvent`에서 시작된 논리 처리 중 실제로 `Alerted`가 된 적마다 `AlertDetected` 연출 이벤트를 끼워 넣는 방식으로 표현한다.
- 현재 구조는 논리 선처리 / 연출 후재생이다. 연출 중간 결과를 보고 실제 규칙을 바꾸는 구조는 아직 목표가 아니다.

확인된 현재 한계:

- `AlertDetectedPresenter`가 담당 적의 발각 이벤트를 받아 경고색 점멸을 재생하고 큐 완료 신호를 보낸다.
- `EnemyAlertVisual`은 제거했다. 색상 적용은 VisualRoot의 `ActorVisualController`, 연출 순서와 상태별 색상 선택은 `AlertDetectedPresenter`가 담당한다.
- 적 AI 반응 이동, 카메라 줌, 발각 컷인, UI 경고는 아직 구현하지 않았다.
- 중간 연출을 본 뒤 남은 이동 경로를 실제 규칙상 변경하는 단계형 액션 시퀀서는 후속 확장 후보로 둔다.

현재 테스트 완료 기준:

- `PlayerMoveInputController`가 입력을 받고 `PlayerActionFlowController`에 이동 행동 실행을 요청한다.
- `PlayerActionFlowController`가 `ActionResolutionContext`를 만들고 이동 논리 처리 후 연출 큐를 재생한다.
- `PlayerGridMoveAction`은 이동 경로의 각 칸마다 1칸 단위 `MoveActor` 연출 이벤트와 `MoveStepEnteredLogicEvent`를 발행한다.
- `GridMoveRiskEvaluator`, `EnemyAlertCoordinator`, `StageGoalManager`, `StageStateManager`는 논리 이벤트 핸들러로 동작한다.
- `ActorPresentationSynchronizer`는 씬 시작 시 VisualRoot를 논리 `GridActor` 위치에 맞춘다.


## AlertDetected 발각 연출

### AlertDetectedPresenter

`AlertDetectedPresenter`는 담당 적의 `AlertDetected` 이벤트만 처리하는 ActorPresentation 계층 컴포넌트다.

- `TargetEnemy`: 연출 대상 `EnemyContext`.
- `VisualController`: 대상 VisualRoot의 `ActorVisualController`.
- `NormalColor`, `AlertedColor`, `WarningColor`: 상태와 점멸에 사용할 색상.
- `FlashInterval`, `FlashCount`: 점멸 간격과 횟수.
- 경고색과 현재 `EnemyAlertState`에 맞는 색상을 번갈아 적용한 뒤 `PresentationEventHandle.Complete()`를 호출한다.
- 비활성화될 때 진행 중인 코루틴과 완료 핸들을 정리해 연출 큐 정지를 방지한다.

### ActorVisualController 색상 제어

`ActorVisualController.ApplyColor()`가 Presenter의 색상 적용 요청을 실제 `SpriteRenderer`에 반영한다. `ActorVisualController`는 논리 상태와 연출 큐를 모르며 실제 시각 컴포넌트 제어만 담당한다.

기존 `EnemyAlertVisual`과 `EnemyContext.AlertVisual` 참조는 제거했다.

## 2026-06-23 씬 세팅 확인

`Assets/Scenes/Tset.unity` 기준으로 다음 연결은 이미 반영되어 있다.

- 각 적의 `AlertDetectedPresenter.VisualController`는 해당 VisualRoot의 `ActorVisualController`에 연결되어 있다.
- 기존 `EnemyAlertVisual` 스크립트와 씬 컴포넌트는 남아 있지 않다.
- `PlayerActionFlowController`, `PlayerMoveInputController`, `PlayerContext`, `EnemyContext`, `StageGoalManager`, `StageStateManager`의 핵심 참조는 씬에 연결되어 있다.
- `GridActorMovePresenter`와 `ActorPresentationSynchronizer`는 플레이어/적 VisualRoot 동기화와 이동 연출 기준으로 배치되어 있다.

따라서 발각 점멸 연결과 기존 `EnemyAlertVisual` 제거는 완료된 상태로 본다.


## Hack / 해킹 1차 통로

현재 해킹은 실제 검 비행 아트 없이 행동/논리/연출 통로만 열어둔 상태다.

### PlayerTurnData 해킹 값

`PlayerTurnData`에는 해킹 관련 플레이어 튜닝 값이 추가되어 있다.

- `HackRange`: 플레이어 위치와 해킹 대상 위치 사이의 최대 맨해튼 거리.
- `HackActionPointCost`: 해킹 행동 1회에 소비하는 AP.

데이터 에셋 자체에는 `HasValidData()` 책임을 두지 않는다. 실제 유효성 검사는 `PlayerHackAction`이 수행한다.

### HackableObject

`HackableObject`는 `IHackable`을 구현하는 해킹 가능 대상의 기본 런타임 컴포넌트다.

현재 책임:

- `HackableData` 참조 보관.
- 대상 위치를 제공하는 `GridActor` 참조 보관.
- 해킹 완료 상태 `IsHacked` 보관.
- 활성화 시 `HackableRegistry`에 등록하고 비활성화 시 해제.
- `OnHackReady()`, `OnHackStarted()`, `OnHackCompleted()`, `OnHackCanceled()` 생명주기 알림 처리.

필수 데이터나 참조가 비어 있으면 fallback 없이 오류 로그를 남기고 컴포넌트를 비활성화한다.

### HackableRegistry

`HackableRegistry`는 현재 씬의 활성 `HackableObject` 목록을 관리하는 씬 단위 등록소다.

현재 공개 목록과 조회:

- `IReadOnlyList<HackableObject> Hackables`
- `TryGetHackableAt(GridPosition, out HackableObject)`

역할은 대상 목록 관리뿐이며, 해킹 판정과 효과 처리는 담당하지 않는다.

### PlayerHackAction

`PlayerHackAction`은 플레이어 해킹 행동의 판정과 실행을 담당한다.

현재 기준:

- 해킹 행동 선택 상태를 관리한다.
- 플레이어 턴, 연출 큐 실행 여부, AP, `HackRange`, 대상 유효성을 검사한다.
- 해킹 대상 위치와 플레이어 위치의 맨해튼 거리가 `PlayerTurnData.HackRange` 이하여야 한다.
- 대상 주변 8칸 중 보드 안이고 `GridManager.CanEnter()`가 true인 칸을 해킹 실행 위치로 고른다.
- 이 실행 위치는 나중에 검이 날아가 도착할 후보 칸이다.
- 현재는 검 오브젝트나 실제 비행 연출을 만들지 않고 `PresentationEvent.ExecutionPosition`에 통로만 남긴다.
- 조건이 맞으면 AP를 소비하고 `HackableObject.OnHackStarted()`, `OnHackCompleted()`를 호출한다.
- `HackCompletedLogicEvent`와 `PresentationEvent.Hack()`을 추가한다.

### PlayerHackInputController

`PlayerHackInputController`는 임시 해킹 입력 담당 컴포넌트다.

현재 책임:

- H 키로 해킹 행동 선택 요청.
- 마우스 화면 좌표를 `GridManager.WorldToGrid()` 기준 목표 칸으로 변환.
- 좌클릭한 칸의 `HackableObject`를 `HackableRegistry`에서 찾는다.
- 찾은 대상은 `PlayerActionFlowController.TryExecuteHack()`으로 실행 요청한다.
- 우클릭 또는 Escape로 해킹 선택을 취소한다.

### HackPresenter

`HackPresenter`는 `PresentationEventType.Hack` 이벤트를 받아 임시 해킹 연출을 처리한다.

현재 책임:

- 담당 `HackableObject`의 해킹 이벤트만 처리한다.
- `HackableData.HackDuration`만큼 대기한 뒤 `PresentationEventHandle.Complete()`를 호출한다.
- 로그에는 대상 칸 `EventPosition`과 나중에 검이 도착할 `ExecutionPosition`을 출력한다.

후속 아트 작업에서는 이 Presenter를 확장해 검 현재 위치에서 `ExecutionPosition`까지 비행, 해킹 이펙트, 복귀/유지 연출을 연결한다.



## 2026-06-24 현재 기준 보정

오늘 기준으로 해킹 1차 통로와 플레이어 입력 구조는 다음 기준을 우선한다.

### 해킹 현재 상태

- 해킹 가능 거리는 플레이어와 대상 칸 사이의 맨해튼 거리로 판정한다.
- 해킹 거리와 AP 비용은 `PlayerTurnData.HackRange`, `PlayerTurnData.HackActionPointCost`에서 읽는다.
- `HackableObject`는 `IHackable` 구현체이며, `HackableData`, `GridActor`, 해킹 완료 상태를 가진다.
- `HackableRegistry`는 씬의 해킹 가능 대상 목록과 칸 기준 조회를 담당한다.
- `HackableRegistry`를 `EnemyRegistry`로 합치는 안은 보류한다. 해킹 대상은 적뿐 아니라 장치, 문, 기믹으로 확장될 수 있기 때문이다.
- `PlayerHackAction`은 턴/AP/거리/대상 검증 뒤 대상 주변 8칸 중 진입 가능한 칸을 해킹 실행 위치로 고른다.
- 해킹 실행 위치는 나중에 검이 날아가 도착할 칸 후보이며, 현재는 `PresentationEvent.ExecutionPosition`에 통로만 남긴다.
- `HackPresenter`는 `PresentationEventType.Hack`을 받아 `HackableData.HackDuration`만큼 기다린 뒤 큐 완료 신호를 보낸다.

### 입력 현재 상태

- `PlayerInputController` 통합 입력 구조와 `PlayerActionSelection`은 제거했다.
- 입력 원천은 `PlayerInputReader`가 담당한다.
- `PlayerInputReader`는 현재 임시 키 매핑과 포인터 그리드 좌표 변환만 담당한다.
- 나중에 Unity Input Action Map을 붙일 때는 `PlayerInputReader` 내부 매핑을 교체한다.
- `PlayerMoveInputController`는 이동 행동 전용 입력 컨트롤러다.
- `PlayerHackInputController`는 해킹 행동 전용 입력 컨트롤러다.
- 두 입력 컨트롤러는 직접 `PlayerInputReader` 인스펙터 참조를 갖지 않고 `PlayerContext.InputReader`에서 꺼내 쓴다.
- 새 행동이 추가되면 `PlayerInputReader`에는 입력값만 추가하고, 행동별 입력 컨트롤러를 별도 스크립트로 만든다.

### PlayerContext 현재 참조

`PlayerContext`는 플레이어 참조 주머니 역할만 유지한다. 현재 해킹/입력 기준으로 다음 참조가 포함된다.

- `PlayerTurnData TurnData`
- `GridActor GridActor`
- `ActionPoint ActionPoint`
- `PlayerGridMoveAction GridMoveAction`
- `PlayerHackAction HackAction`
- `GridMoveRiskEvaluator GridMoveRiskEvaluator`
- `GridMoveRangeHighlighter GridMoveRangeHighlighter`
- `PlayerInputReader InputReader`

### 현재 테스트 기준

`Tset` 씬 기준으로 다음 연결을 확인한 상태다.

- `PlayerInputReader`, `PlayerMoveInputController`, `PlayerHackInputController`, `PlayerHackAction`은 플레이어 논리 오브젝트에 있다.
- `PlayerContext.InputReader`는 `PlayerInputReader`에 연결되어 있다.
- `PlayerContext.HackAction`은 `PlayerHackAction`에 연결되어 있다.
- `PlayerMoveInputController`, `PlayerHackInputController`에는 직접 `PlayerInputReader` 직렬화 참조가 남아 있지 않다.
- 씬의 Missing Script 패턴은 확인되지 않았다.

### 다음 작업

1. Unity 플레이 모드에서 M 이동 선택, H 해킹 선택, 좌클릭 확정, 우클릭/Escape 취소 입력을 확인한다.
2. 해킹 실행 시 AP 소비, 맨해튼 거리 판정, 대상 주변 8칸 실행 위치 계산, `HackPresenter` 큐 완료 로그를 확인한다.
3. 입력 확인 뒤 해킹 대상별 실제 효과와 검 비행 연출 통로를 단계적으로 붙인다.




## Sword / 도깨비 환도 행동 1차 뼈대

현재 검 투척/회수는 실제 물리 오브젝트 이동 없이 행동 판정과 연출 이벤트 통로만 만든 상태다.

### PlayerSwordState

PlayerSwordState는 도깨비 환도의 현재 기준 칸을 보관한다.
검은 보드 점유 Actor가 아니므로 GridManager에 등록하지 않고 GridPosition 값만 관리한다.

현재 기준:

- 시작 시 검 기준 칸을 플레이어 현재 칸으로 초기화할 수 있다.
- 검 투척/해킹 후에는 검 기준 칸이 대상 위치로 갱신된다.
- 검 회수 후에는 검 기준 칸이 플레이어 현재 칸으로 돌아온다.
- HackCompletedLogicEvent를 처리해 해킹의 ExecutionPosition을 새 검 기준 칸으로 사용한다.

### PlayerSwordThrowAction

PlayerSwordThrowAction은 검 현재 위치 기준으로 목표 칸에 검을 투척하는 행동이다.

현재 규칙:

- 검 투척은 AP를 소비한다.
- 사거리는 플레이어 위치가 아니라 PlayerSwordState.CurrentPosition 기준 맨해튼 거리로 판정한다.
- 목표 칸은 GridManager.IsInside()로 보드 안인지 확인한다.
- 검은 이동 말이 아니므로 GridManager.CanEnter()로 목표 칸을 막지 않는다.
- 실행 시 SwordThrownLogicEvent와 PresentationEvent.SwordThrow를 추가한다.

### PlayerSwordRecallAction

PlayerSwordRecallAction은 거리 제한 없이 검을 플레이어 현재 칸으로 회수하는 행동이다.

현재 규칙:

- 검 회수는 AP를 소비한다.
- 회수에는 거리 제한이 없다.
- 실행 시 SwordRecalledLogicEvent와 PresentationEvent.SwordRecall을 추가한다.

### 입력

임시 입력 기준:

- PlayerInputReader가 T 키 검 투척 선택과 R 키 검 회수 실행 입력을 읽는다.
- PlayerSwordThrowInputController는 T 선택 후 좌클릭한 칸으로 검 투척 실행을 요청한다.
- PlayerSwordRecallInputController는 R 입력 시 검 회수 실행을 요청한다.

### SwordActionPresenter

SwordActionPresenter는 `PresentationEventType.SwordThrow`, `PresentationEventType.SwordRecall`, `AttackPresentationKind.SwordThrow`인 `CombatAction`을 받아 임시 대기/로그 연출을 처리한다.
실제 직선 이펙트, 검 위치 표시, 검 투척 피격/사망 연출은 후속 아트 작업에서 이 Presenter를 확장해 연결한다.



## 2026-06-25 입력 / 검 상태 보정

오늘 기준 입력 컨트롤러와 검 상태 규칙은 다음 기준을 우선한다.

### 입력 컨트롤러 실행 기준

- PlayerInputReader는 원시 입력과 포인터 그리드 좌표 변환만 담당한다.
- 행동별 입력 컨트롤러는 PlayerContext에서 자기 행동 컴포넌트와 PlayerInputReader를 꺼내 쓴다.
- 행동별 입력 컨트롤러는 PlayerActionFlowController 인스펙터 참조를 직접 갖지 않는다.
- 행동 실행 요청 시점에 PlayerActionFlowController.Instance를 조회한다.
- PlayerActionFlowController.Instance가 없으면 한국어 오류 로그를 남기고 실행을 중단한다.
- 이 기준은 이동, 해킹, 검 투척, 검 회수 입력 컨트롤러에 동일하게 적용한다.

현재 입력 컨트롤러:

- PlayerMoveInputController: M 선택, 좌클릭 이동 실행 요청.
- PlayerHackInputController: H 선택, 좌클릭 해킹 대상 실행 요청.
- PlayerSwordThrowInputController: T 선택, 좌클릭 검 투척 실행 요청.
- PlayerSwordRecallInputController: R 입력 시 검 회수 실행 요청.

### 검 회수 상태와 위치 기준

- 검이 회수된 상태라면 PlayerSwordState.IsRecalled는 true다.
- 회수 상태에서는 검 독립 위치가 의미 없으며, PlayerSwordState.CurrentPosition은 항상 플레이어 현재 칸을 반환한다.
- 따라서 검을 소유한 채 플레이어가 이동하면 다음 검 투척 사거리 기준도 플레이어 현재 칸을 따라간다.
- 검이 투척되거나 해킹 ExecutionPosition으로 이동하면 IsRecalled는 false가 되고, 이후 사거리 기준은 검이 나가 있는 칸이 된다.
- 이미 검을 소유 중인 상태에서는 PlayerSwordRecallAction이 회수 행동을 막는다.

### 현재 씬 정리 기준

- 입력 컨트롤러에 남아 있던 구 actionFlowController 직렬화 줄은 제거 대상이다.
- Tset.unity 기준으로 기존 이동/해킹 입력 컨트롤러의 구 actionFlowController 직렬화 줄은 제거했다.
- 새 검 행동 컴포넌트 연결은 아직 인스펙터 작업으로 남아 있다.



## 다음 작업 메모 - 2026-06-25

- 해킹 사거리는 현재 플레이어 기준이지만, 다음 작업에서 PlayerSwordState.CurrentPosition 기준으로 바꾼다.
- 검 투척을 이용한 공격 행동 뼈대를 추가한다.
- 근접 공격 행동 뼈대를 추가한다.
- 공격 행동도 현재 이동/해킹/검 행동과 동일하게 ActionResolutionContext -> PresentationEvent -> Presenter 흐름으로 만든다.



## 인터페이스 파일 규칙

- 새 인터페이스는 다른 클래스 파일 안에 함께 두지 않는다.
- 인터페이스명과 같은 독립 .cs 파일로 만든다.
- 예: IActionLogicEvent, IActionLogicEventHandler, IHackable, IDamageable.
- ActionLogicEventBus는 논리 이벤트 전달자 역할만 맡고, IActionLogicEvent, IActionLogicEventHandler는 별도 파일에서 관리한다.


## 2026-06-28 공격 / 피해 1차 통로

### 해킹 사거리 기준 변경

해킹 가능 거리는 이제 플레이어 위치가 아니라 `PlayerSwordState.CurrentPosition`과 해킹 대상 칸 사이의 맨해튼 거리로 판정한다.
검이 회수된 상태라면 `CurrentPosition`이 플레이어 현재 칸을 반환하므로 기존 플레이어 기준과 동일하게 동작하고, 검이 나가 있으면 나가 있는 검 위치가 해킹 기준점이 된다.

### ActorHealth

`ActorHealth`는 `IDamageable`을 구현하는 기본 HP 컴포넌트다.
현재는 임시 테스트 전용이 아니라 후속 확장 가능한 기본 컴포넌트로 둔다.

현재 값:

- `MaxHitPoint`: 최대 HP.
- `CurrentHitPoint`: 현재 HP.
- `IsDead`: 현재 HP가 0 이하인지 여부.

현재 책임:

- `TakeDamage(int damage)`로 피해를 적용하고 `DamageResult`를 반환한다.
- 실제 HP는 논리 처리 시점에 즉시 변경한다.
- 피해 전 HP, 피해 후 HP, 사망 전후 상태는 `DamageResult`에 스냅샷으로 담는다.
- HP가 0 이하가 되면 전투불능 로그를 남긴다.
- 실제 사망 제거, 애니메이션, 보상, AI 상태 전환은 아직 처리하지 않는다.

### DamageResult

`DamageResult`는 피해 적용 전후의 HP 스냅샷을 담는 값 타입이다.
논리 HP는 즉시 확정하지만, 연출은 이 스냅샷을 기준으로 HP바 감소와 사망 연출을 재생한다.

현재 값:

- `Applied`: 피해가 실제 적용됐는지 여부.
- `Damage`: 시도한 피해량.
- `HitPointBefore`: 피해 적용 전 HP.
- `HitPointAfter`: 피해 적용 후 HP.
- `WasDeadBefore`: 피해 전 이미 전투불능이었는지 여부.
- `IsDeadAfter`: 피해 후 전투불능인지 여부.
- `KilledByThisDamage`: 이번 피해로 새로 전투불능이 됐는지 여부.

현재 기준:

- 연출 Presenter는 피해 연출을 만들 때 `ActorHealth.CurrentHitPoint`를 다시 읽지 않고 `DamageResult`의 전후 HP를 사용한다.
- `DamageAppliedLogicEvent`는 `DamageResult`를 포함한다.
- 기존 피해 기반 경계 전환은 `DamageAppliedLogicEvent.Applied` 편의 속성으로 동일하게 동작한다.

### 검 투척 피해

`PlayerSwordThrowAction`은 기존 검 투척 행동을 유지하되, 목표 칸에 `GridActor`가 있고 해당 오브젝트가 `IDamageable`을 구현하면 피해를 적용한다.
목표 칸에 `IDamageable`이 없으면 공격 실패가 아니라 기존처럼 검만 해당 칸으로 이동한다.

현재 규칙:

- 검 투척 사거리는 기존 `PlayerTurnData.SwordThrowRange`를 공유한다.
- 검 투척 AP 비용은 `PlayerTurnData.SwordThrowActionPointCost`를 사용한다.
- 검 투척 피해량은 `PlayerTurnData.SwordThrowDamage`를 사용한다.
- 피해 적용 시도 후 `DamageAppliedLogicEvent`를 발행한다.
- 검은 투척 목표 칸에 남는다.

### 근접 공격

`PlayerMeleeAttackAction`은 플레이어 현재 칸 기준 8방향 1칸 대상에게 근접 공격을 실행한다.
근접 공격 입력은 `PlayerMeleeAttackInputController`가 담당하고, 임시 키는 F다.

현재 규칙:

- 근접 공격은 AP 1을 소비한다. 실제 비용은 `PlayerTurnData.MeleeAttackActionPointCost`에서 읽는다.
- 검을 소유 중이면 `PlayerTurnData.MeleeDamageWithSword` 피해를 적용한다.
- 검을 소유하지 않으면 `PlayerTurnData.MeleeDamageWithoutSword` 피해를 적용한다.
- 대상 칸에 `IDamageable`이 없으면 공격을 실행하지 않는다.
- 검 위치는 근접 공격으로 바뀌지 않는다.
- 피해 적용 시도 후 `DamageAppliedLogicEvent`를 발행한다.

### 근접 공격 연출 이벤트 분리

근접 공격은 검 소유 여부에 따라 서로 다른 연출 이벤트를 큐에 넣는다.

- `PresentationEventType.MeleeAttackWithSword`: 검 보유 근접 공격 연출.
- `PresentationEventType.MeleeAttackUnarmed`: 검 없음 근접 공격 연출.

현재 `PlayerMeleeAttackPresenter`가 두 이벤트와 `MeleeWithSword`, `MeleeUnarmed` 종류의 `CombatAction`을 임시 대기/로그 방식으로 처리한다.
후속 작업에서 검 보유 근접 공격과 검 없음 근접 공격의 실제 애니메이션, 이펙트, 타격 타이밍을 분리해 확장한다.

### 2026-06-28 테스트 확인

Unity 플레이 모드에서 공격 / 피해 1차 통로를 확인했다.

확인한 항목:

- T 검 투척으로 목표 칸에 `IDamageable` 대상이 있으면 피해가 적용된다.
- 목표 칸에 `IDamageable` 대상이 없으면 검만 이동한다.
- F 근접 공격은 플레이어 주변 8방향 1칸 대상에게 실행된다.
- 검 보유 중 근접 공격과 검 미보유 중 근접 공격은 서로 다른 피해량을 적용한다.
- 검 보유 여부에 따라 `MeleeAttackWithSword`, `MeleeAttackUnarmed` 연출 이벤트가 분리되어 큐에 등록된다.
- `ActorHealth`는 HP 감소와 전투불능 로그를 정상 출력한다.
- 해킹 사거리는 `PlayerSwordState.CurrentPosition` 기준으로 동작한다.

현재 후속 작업:

- 실제 피격 연출과 사망/제거 처리를 `ActorHealth`와 Presenter 기준으로 확장한다.
- 검 투척/근접 공격의 임시 로그 연출을 실제 검 표시, 타격 이펙트, 애니메이션 타이밍으로 교체한다.
- 공격 시 적 경계 상태 전환과 애드 규칙을 정한다.

## 2026-06-28 경계 반응 / 엄폐 이동 1차 통로

### 경계 상태 전환과 경계 반응 분리

경계 상태 전환은 적의 상태가 `Alerted`로 바뀌는 논리 처리다.
경계 반응 행동은 `Alerted`가 된 적이 실제로 무엇을 할지 결정하는 후속 논리 처리다.

현재 기준:

- `EnemyAlertCoordinator`: 시야 발각 또는 피해 적용을 받아 경계 상태 전환과 애드 전파를 담당한다.
- `EnemyAlertState`: 적 하나의 `Normal` / `Alerted` 상태를 보관한다.
- `EnemyAlertedLogicEvent`: 적이 새로 `Alerted`가 됐음을 알리는 논리 이벤트다.
- `EnemyAlertReactionCoordinator`: `EnemyAlertedLogicEvent`를 받아 경계 반응 이동을 계산한다.

### EnemyAlertReason

`EnemyAlertedLogicEvent`에는 경계 상태 전환 원인을 나타내는 `EnemyAlertReason`이 포함된다.

현재 값:

- `SightDetected`: 적 시야에 플레이어가 들어와 직접 발각된 경우.
- `Damaged`: 피해를 받아 경계 상태가 된 경우.
- `Spread`: 주변 적의 경계 전파로 경계 상태가 된 경우.
- `Scripted`: 이후 스크립트 이벤트용 후보.

### 피해 기반 경계 전환

`EnemyAlertCoordinator`는 `DamageAppliedLogicEvent`도 처리한다.
피해가 실제 적용됐고 대상 `GridActor`가 `EnemyContext`에 속하면, 피해를 받은 적을 기준으로 경계 상태 전환과 애드 전파를 실행한다.

현재 기준:

- 피해를 받은 적은 `Damaged` 원인으로 경계 상태가 된다.
- 전파로 경계 상태가 된 적은 `Spread` 원인으로 처리된다.
- 모든 새 경계 적은 `EnemyAlertedLogicEvent`를 발행한다.
- 모든 새 경계 적은 기존 `AlertDetected` 연출 이벤트도 받는다.

### EnemyData 경계 반응 값

`EnemyData`에 `AlertReactionMoveRange`를 추가했다.
이 값은 적이 경계 상태로 전환됐을 때 엄폐 반응으로 이동할 수 있는 최대 BFS 거리다.
0이면 경계 상태만 되고 엄폐 이동은 하지 않는다.

### EnemyAlertReactionCoordinator

`EnemyAlertReactionCoordinator`는 적 오브젝트별로 붙는 경계 반응 컴포넌트다.
경계 상태로 전환된 자기 `EnemyContext`의 `EnemyAlertedLogicEvent`만 처리한다.
현재는 이동 가능 범위 안의 벽 인접 칸으로 이동하는 엄폐 반응만 처리한다.

현재 참조:

- `EnemyContext EnemyContext`: 이 컴포넌트가 반응 이동을 처리할 적 Context.
- `EnemyTacticalPositionScoreSettings ScoreSettings`: 엄폐 후보 점수 가중치.

처리 흐름:

1. `EnemyAlertedLogicEvent`를 받는다.
2. 이벤트의 `Enemy`가 자기 `EnemyContext`가 아니면 무시한다.
3. 이미 현재 Alerted 진입에 대해 반응했다면 추가 이동을 하지 않는다.
4. 적의 `EnemyData.AlertReactionMoveRange` 기준으로 `EnemyTacticalMovePlanner`에 경로 계산을 요청한다.
5. 계산된 경로를 따라 적 논리 위치를 1칸씩 이동시킨다.
6. 각 1칸 이동마다 `PresentationEvent.EnemyReactionMove`를 큐에 추가한다.

경계 반응 이동은 상태 변화에 따른 수동 반응이다.
적 턴에 공격 위치를 찾아 이동하는 능동 AI 이동과 분리해서 관리한다.

### EnemyTacticalPositionScorer

`EnemyTacticalPositionScorer`는 적 전술 위치 후보의 점수를 계산하는 재사용 가능한 평가 도구다.
현재 경계 반응 엄폐 이동에서 사용하며, 이후 적 턴 AI의 공격 위치 선정에도 재사용할 수 있다.

현재 점수 항목:

- 정면 노출 패널티: 플레이어와 같은 직선축에 있고 사이에 벽이 없으면 감점한다.
- 플레이어 접근 감점: 현재 위치보다 플레이어에게 가까워지면 거리 차이만큼 감점한다.
- 실제 차단 엄폐 보너스: 후보 주변 벽이 후보보다 플레이어에 더 가까우면 차단 벽으로 점수를 준다.
- 인접 벽 수 보너스: 후보 주변 4방향의 벽 개수에 따라 기본 엄폐 점수를 준다.
- 엄폐 품질 개선 보너스: 후보 엄폐 품질이 현재 위치보다 좋으면 추가 점수를 준다.
- 이동 거리 패널티: 이동 거리가 길수록 감점한다.

후보별 점수 결과는 `EnemyTacticalPositionScoreResult`로 반환하며, 디버그 로그에서 점수 산정 근거를 확인할 수 있다.

### EnemyTacticalMovePlanner

`EnemyTacticalMovePlanner`는 적이 이동 가능한 전술 후보 칸을 찾고 목표 칸까지의 경로를 계산한다.
현재는 `TryFindBestCoverReactionPath()`로 경계 반응 엄폐 이동 경로를 계산한다.

역할:

- `GridPathfinder.FindReachablePositionDistances()`로 이동 가능 후보와 이동 거리를 계산한다.
- `EnemyTacticalPositionScorer`로 후보 점수를 계산한다.
- 최고 점수 후보를 목표 칸으로 선택한다.
- `GridPathfinder.TryFindPath()`로 목표까지의 실제 1칸 단위 경로를 만든다.

현재 연출은 기존 `GridActorMovePresenter`가 `EnemyReactionMove`를 처리하는 흐름을 재사용한다.

### 현재 한계

- 엄폐 후보 점수식은 1차 기준이며, 실제 XCOM식 엄폐 품질/각도/명중률은 아직 없다.
- 현재 Alerted 진입에 대한 경계 반응은 적별 1회만 처리한다.
- 벽 인접 칸이 없으면 경계 상태만 유지하고 이동하지 않는다.
- 적 유형별로 이동하지 않는 적, 자리 고수형 적, 즉시 공격형 적을 구분하는 정책은 아직 없다.
- 엄폐 이동은 경계 상태로 새로 전환된 순간에만 실행된다.

## Debug

### S2TDebugOverlay

`S2TDebugOverlay`는 턴제 테스트 중 주요 수치를 화면에 출력하는 디버그 전용 컴포넌트다.
정식 UI나 연출이 아니라 수치 확인용이며, 게임 상태를 변경하지 않는다.

현재 표시 항목:

- 현재 턴.
- 스테이지 상태.
- 연출 큐 실행 여부와 대기 이벤트 수.
- 플레이어 그리드 위치.
- 플레이어 AP.
- 플레이어 HP.
- 검 회수/투척 상태와 현재 검 기준 칸.
- 이동, 해킹, 검 투척, 근접 공격 선택 상태.
- 활성 적 수와 경계 상태 적 수.
- 마지막 피해 이벤트의 공격자, 대상, 대상 칸, 피해량, 적용 여부, HP 전후 값, 이번 피해 사망 여부.

현재 기준:

- `PlayerContext`, 플레이어 `ActorHealth`, `StageStateManager`는 인스펙터에서 연결한다.
- `TurnManager`, `ActionPresentationQueue`, `EnemyRegistry`는 씬 단위 인스턴스를 읽는다.
- 마지막 피해 수치는 `DamageAppliedLogicEvent.Result`의 `DamageResult` 스냅샷을 사용한다.
- 연결되지 않은 항목은 `None`으로 표시한다.

## 피해 요청 / 통합 전투 연출

### ApplyDamageLogicEvent

`ApplyDamageLogicEvent`는 공격 행동이 표준 피해 적용을 요청할 때 발행하는 논리 이벤트다.
공격 행동은 피해 결과를 직접 해석하지 않고, 누가 누구에게 어떤 공격 종류로 몇 피해를 요청했는지만 전달한다.

전달 값:

- `Attacker`: 공격자 `GridActor`.
- `Target`: 피해 대상 `GridActor`.
- `FromPosition`: 공격 또는 투사체 시작 칸.
- `TargetPosition`: 피해 대상 칸.
- `Damage`: 적용할 피해량.
- `PresentationKind`: 통합 전투 연출에서 사용할 공격 표현 종류.
- `Message`: 연출 로그용 설명.

### DamageResolutionCoordinator

`DamageResolutionCoordinator`는 `ApplyDamageLogicEvent`를 처리하는 기본 논리 이벤트 처리자다.
씬 배치 없이 `ActionLogicEventBus`에 기본 등록된다.

처리 흐름:

1. `ApplyDamageLogicEvent`를 받는다.
2. 대상의 `IDamageable`을 찾는다.
3. `TakeDamage()`를 호출해 `DamageResult`를 얻는다.
4. `DamageAppliedLogicEvent`를 발행한다.
5. `DamageResult.KilledByThisDamage`가 true면 `ActorDiedLogicEvent`를 발행한다.
6. 공격/피격/사망 여부를 함께 담은 `PresentationEvent.CombatAction()`을 연출 큐에 추가한다.

현재 기준:

- `ActorHealth`는 `ActionResolutionContext`나 연출 큐를 알지 않는다.
- 공격 행동도 피해 전후 HP나 사망 여부를 직접 해석하지 않는다.
- 피해 대상이 없는 검 투척은 기존 `SwordThrow` 연출만 사용한다.

### ActorDiedLogicEvent

`ActorDiedLogicEvent`는 이번 피해로 액터가 새로 전투불능이 됐음을 알리는 논리 이벤트다.
현재는 오브젝트 제거/비활성화는 하지 않고 후속 규칙과 디버그 확인용 통로만 둔다.

전달 값:

- `Attacker`: 사망을 유발한 공격자.
- `DeadActor`: 전투불능이 된 액터.
- `DeadPosition`: 사망이 발생한 칸.
- `Result`: 사망을 만든 `DamageResult`.

### CombatAction PresentationEvent

`PresentationEventType.CombatAction`은 공격, 피격, 사망 여부를 한 이벤트로 묶는 통합 전투 연출 이벤트다.
연출 큐는 순차 실행 구조이므로, 동시에 맞물려야 하는 공격/피격/사망 타이밍은 이 이벤트를 처리하는 Presenter 내부에서 맞춘다.

현재 포함 값:

- 공격자 `Actor`.
- 피격자 `TargetActor`.
- 공격 시작 칸 `FromPosition`.
- 대상 칸 `ToPosition` / `EventPosition`.
- 공격 표현 종류 `AttackKind`.
- 피해 결과 `DamageResult`.
- 피해 결과 포함 여부 `HasDamageResult`.

플레이어 공격 `CombatAction`은 공격 종류별 Presenter가 임시 로그/대기 방식으로 처리한다.

- `SwordActionPresenter`: `SwordThrow`
- `PlayerMeleeAttackPresenter`: `MeleeWithSword`, `MeleeUnarmed`
- `PlayerGunAttackPresenter`: `PlayerGun`

각 Presenter는 `PresentationEventType`, `AttackPresentationKind`, `ownerActor`를 함께 검사해 자기 공격 이벤트만 처리한다.
후속 작업에서 실제 공격 애니메이션, 피격 반응, 사망 애니메이션을 각 공격 Presenter에 연결한다.
현재 피격측 처리는 로그 확인용이다. 아트/애니메이션이 준비되면 `TargetActor`로 피격 주체를 찾고 `DamageResult`의 HP 전후 값과 `KilledByThisDamage`를 기준으로 피격 애니메이션 또는 사망 애니메이션을 재생한다.





## 적 턴 AI 1차

### EnemyTurnCoordinator

`EnemyTurnCoordinator`는 적 턴 시작 시 현재 씬의 활성 적들을 순서대로 실행하는 씬 단위 조정자다.

책임:

- `TurnManager.TurnStarted`를 구독해 적 턴 시작을 감지한다.
- `EnemyRegistry.Enemies`의 적을 순서대로 확인한다.
- 각 적의 `EnemyContext.TurnAgent`가 있으면 해당 적 턴 행동을 실행한다.
- 적 하나의 논리 처리를 `ActionResolutionContext`로 확정한 뒤 `ActionPresentationQueue`를 재생한다.
- 연출 큐가 비면 다음 적을 실행한다.
- 모든 적 처리가 끝나면 적 턴을 종료하고 플레이어 턴으로 넘긴다.

### EnemyTurnAgent

`EnemyTurnAgent`는 적 하나의 2AP 행동 판단을 담당한다.

현재 1차 규칙:

- `EnemyAlertState.IsAlerted`인 적만 행동한다.
- `ActorHealth.IsDead`가 true인 적은 행동하지 않는다.
- 현재 위치에서 플레이어가 원거리 공격 사거리 안이면 공격 후 남은 AP로 최고 엄폐 위치를 찾는다.
- 현재 위치가 최고 엄폐 위치면 이동하지 않는다.
- 현재 위치에서 공격 사거리 밖이면 공격 가능한 엄폐 위치 중 점수가 가장 높은 칸으로 이동한 뒤 공격한다.
- 공격 가능한 엄폐 위치가 없으면 공격하지 않고 이동 가능 범위 안의 최고 엄폐 위치로 이동 후 대기한다.
- 이동은 1AP, 공격은 1AP를 소비한다.

### EnemyAttackAction

`EnemyAttackAction`은 적 원거리 공격 판정과 피해 적용 요청을 담당한다.

책임:

- 현재 적 위치 또는 후보 위치 기준 공격 사거리 판정.
- 대상 `IDamageable` 확인.
- 대상이 이미 `ActorHealth.IsDead`이면 공격하지 않음.
- 공격 성공 시 `ApplyDamageLogicEvent`를 발행해 기존 `DamageResolutionCoordinator` 피해 흐름을 사용한다.
- 적 원거리 공격 표현은 `AttackPresentationKind.EnemyRanged`를 사용한다.

### EnemyAttackPresenter

`EnemyAttackPresenter`는 적 원거리 공격 `CombatAction`을 임시 로그/대기 연출로 처리한다.
실제 투사체, 피격 반응, 사망 애니메이션은 후속 아트 작업에서 확장한다.

### EnemyTacticalMovePlanner 적 턴 확장

`EnemyTacticalMovePlanner`는 기존 경계 반응 엄폐 이동 외에 적 턴 AI에서도 재사용된다.

추가된 기준:

- 현재 위치를 후보에 포함할 수 있다.
- 후보 필터를 받아 공격 가능한 엄폐 위치만 고를 수 있다.
- 현재 위치가 최고 후보면 빈 경로를 반환해 이동하지 않는 판단을 지원한다.

### EnemyActionPoint

`EnemyActionPoint`는 적 하나가 적 턴 행동에 사용할 AP를 관리한다.

책임:

- `EnemyData.TurnActionPoint` 기준 현재 AP와 최대 AP 제공.
- `CanSpend()`, `TrySpend()`로 AP 소비 가능 여부와 실제 소비 처리.
- `RefillForTurn()`으로 적 하나의 턴 행동 시작 시 AP를 보충.
- AP 변경 이벤트 발행.

현재 적 AP 보충은 `EnemyActionPoint`가 턴 이벤트를 직접 구독하지 않고, `EnemyTurnCoordinator`가 각 적 행동 실행 직전에 호출한다.
이는 적들이 하나의 Enemy 턴 안에서 순서대로 행동하는 구조이기 때문이다.

`EnemyTurnAgent`는 지역 AP 값을 만들지 않고 `EnemyContext.ActionPoint`를 통해 이동/공격 AP를 확인하고 소비한다.
현재 이동 1회와 원거리 공격 1회는 각각 AP 1을 소비한다.


## 논리 사망 처리

### ActorDeathCoordinator

`ActorDeathCoordinator`는 `ActorDiedLogicEvent`를 처리하는 기본 논리 이벤트 처리자다.
씬 배치 없이 `ActionLogicEventBus`에 기본 등록된다.

현재 기준:

- 사망한 액터의 GameObject는 삭제하거나 비활성화하지 않는다.
- 사망 상태는 `ActorHealth.IsDead`를 기준으로 판단한다.
- 사망 시 `GridActor.ReleaseCellOccupation()`을 호출해 현재 칸 점유만 해제한다.
- 점유가 해제된 시체는 같은 칸으로 다른 액터가 들어오는 것을 막지 않는다.
- 사망 애니메이션, 시체 스프라이트 정렬, 플레이어와 시체가 같은 칸에 있을 때의 표시 우선순위는 Presenter/Visual 작업에서 처리한다.

### GridActor 사망 점유 해제

`GridActor.ReleaseCellOccupation()`은 오브젝트의 논리 위치는 유지하면서 GridManager 점유 테이블에서만 액터를 제거한다.
호출 후 `occupyCell`은 false가 되어 해당 액터가 다시 활성화되더라도 전술 점유자로 재등록되지 않는다.

### 죽은 적 제외 기준

- `EnemyGridSight.CanDetect()`는 자기 `ActorHealth.IsDead`가 true면 감지하지 않는다.
- `EnemyAlertCoordinator`는 죽은 적을 애드 전파 수신 대상에서 제외한다.
- `EnemyTurnAgent`는 기존처럼 죽은 적의 적 턴 행동을 생략한다.

## 2026-07-09 다중 전술 유닛 / 진영 / 제어권 구조

### 공통 구분

- `UnitFaction`: `Player`, `Enemy`, `Neutral` 진영을 구분한다.
- `UnitControlType`: `Player`, `AI`, `None`으로 행동 결정 주체를 구분한다.
- 진영과 조작권은 서로 독립이다.

### TacticalUnitContext / ControllableUnitData

- 기존 `PlayerContext`는 `TacticalUnitContext`로 변경했다.
- 기존 `PlayerTurnData`는 `ControllableUnitData`로 변경했다.
- `RequiredAbilities`는 유닛이 반드시 갖춰야 하는 `Move`, `Gun`, `Hack`, `Sword`, `Melee`, `HeavyGun` 능력 조합을 선언한다.
- `TacticalUnitContext.HasValidAbilityComposition()`은 데이터의 필수 능력과 실제 행동 컴포넌트 구성이 정확히 일치하는지 검사한다.
- 필수 능력 컴포넌트 누락과 데이터에 선언되지 않은 추가 행동 컴포넌트는 모두 구성 오류로 처리한다.
- `DefeatOnDeath`는 유진 같은 핵심 유닛 사망 시 패배 조건으로 사용할 기록용 데이터이며 아직 게임 오버 흐름에는 연결하지 않았다.

### 전술 유닛 등록소

- `ITacticalUnit`은 진영, `GridActor`, `ActorHealth`, 생존 여부를 제공한다.
- `TacticalUnitRegistry`는 플레이어와 적을 포함한 모든 전술 유닛을 등록한다.
- 플레이어 진영이면서 `UnitControlType.Player`인 유닛은 별도 조작 가능 목록에도 등록한다.
- `EnemyRegistry`는 기존 적 전용 시스템 호환을 위해 유지한다.

### 입력 / 선택 / 제어권

- `PlayerInputReader`는 씬 단일 인스턴스로 동작한다.
- 기존 행동별 입력 컨트롤러는 제거됐고 실제 입력은 `PlayerUnitInputController`가 통합 처리한다.
- `PlayerUnitControlManager.ActiveUnit`이 현재 플레이어가 조작할 전술 유닛을 보관한다.
- 플레이어 진영, 플레이어 조작권, 생존, AP 1 이상 조건을 만족해야 선택할 수 있다.
- 연출 큐 실행 중에도 유닛 선택은 가능하지만 `PlayerUnitActionFlowController`가 실제 행동 실행을 차단한다.
- 다른 유닛 선택 시 이전 유닛의 모든 행동 모드와 이동 미리보기를 해제한다.
- 현재 유닛 AP가 0이 되면 등록 순서 기준 다음 AP 보유 유닛으로 자동 전환한다.
- 모든 유닛 AP가 0이면 `ActiveUnit`을 비우고 다음 플레이어 턴 AP 보충 이벤트에서 다시 선택한다.

### 선택 능력 처리

- 현재 유닛에게 행동 컴포넌트가 없는 것은 정상적인 능력 부재로 취급하고 안내 로그만 출력한다.
- 데이터가 능력을 필수로 선언했는데 해당 컴포넌트가 없으면 구성 오류로 처리한다.
- 해킹 유닛이 검 능력을 가지면 검 위치를 해킹 사거리 기준으로 사용하고, 검 능력이 없으면 자기 위치를 기준으로 사용한다.
- 근접 공격 유닛이 검 능력을 가지며 검을 회수한 상태면 검 근접 공격을 사용하고, 아니면 맨손 근접 공격을 사용한다.

### 플레이어 발각 / 적 표적

- 경계 상태가 된 적은 살아 있는 모든 플레이어 진영 유닛을 표적 후보로 인식한다.
- `EnemyTargetSelector`는 적 위치 기준 맨해튼 거리가 가장 가까운 살아 있는 플레이어 진영 유닛을 선택한다.
- 같은 거리면 전술 유닛 등록 순서가 빠른 유닛을 선택한다.
- 경계 진입 반응 이동과 적 턴 AI가 같은 표적 선정 기준을 사용한다.

### 아직 연결하지 않은 항목

- 유닛 선택 및 조작 가능 상태의 실제 발판/테두리 시각 표시는 후속 작업으로 남긴다.
- `DefeatOnDeath` 기반 게임 오버 처리는 후속 작업으로 남긴다.
- `HeavyGun`은 능력 구성표 자리만 열었고 실제 행동 컴포넌트는 아직 없다.

## TurnAction 폴더 구조
- `TurnAction/Core`: 행동 로직 이벤트 버스, 이벤트 타입, 해석 컨텍스트, 이벤트 인터페이스를 둔다.
- `TurnAction/Grid`: 이동 범위/경로/위험도 표시와 평가 컴포넌트를 둔다.
- `TurnAction/Input`: 씬 단일 플레이어 유닛 입력 컨트롤러를 둔다.
- `TurnAction/Player`: 플레이어 조작 유닛의 실제 행동 실행 컴포넌트와 행동 플로우 컨트롤러를 둔다.

## 2026-07-10 현재 테스트 기준 정리

### 씬 단위 필수 매니저

`Tset` 씬 테스트 기준으로 다음 컴포넌트는 씬에 하나씩 둔다.

- `TacticalUnitRegistry`: 플레이어/적/중립 전술 유닛 등록소.
- `PlayerUnitControlManager`: 현재 조작 유닛 선택과 AP 소진 자동 전환 관리.
- `PlayerUnitInputController`: 씬 단일 입력을 현재 조작 유닛 행동으로 변환.
- `PlayerUnitActionFlowController`: 행동 논리 실행 후 `ActionPresentationQueue` 재생.
- `PlayerInputReader`: 입력 원본.
- `ActionPresentationQueue`: 논리 결과 연출 큐.
- `EnemyRegistry`: 기존 적 전용 등록소 호환용.
- `StageGoalManager`: 스테이지 목표 판정 매니저. 씬에 1개만 둔다.
- `StageStateManager`: 스테이지 클리어/실패 상태 관리.

### 현재 테스트 유닛 구성

- 유진 테스트 유닛: `Move + Gun + Hack + Sword + Melee`.
- 동료 테스트 유닛: 현재 `Move + Melee`.
- 동료를 일반 총기 아군으로 테스트할 경우 `ControllableUnitData.RequiredAbilities`를 `Move + Gun`으로 바꾸고, 실제 연결도 `GridMoveAction`, `GridMoveRiskEvaluator`, `GridMoveRangeHighlighter`, `PlayerGunAmmo`, `PlayerGunAttackAction`만 남긴다.

### 인스펙터 점검 기준

`TacticalUnitContext`는 데이터의 `RequiredAbilities`와 실제 연결된 선택 행동 컴포넌트가 정확히 일치해야 한다.

- `Move`: `PlayerGridMoveAction`, `GridMoveRiskEvaluator`, `GridMoveRangeHighlighter`.
- `Gun`: `PlayerGunAmmo`, `PlayerGunAttackAction`.
- `Hack`: `PlayerHackAction`.
- `Sword`: `PlayerSwordState`, `PlayerSwordThrowAction`, `PlayerSwordRecallAction`.
- `Melee`: `PlayerMeleeAttackAction`.
- `HeavyGun`: 타입만 열려 있고 실제 행동 컴포넌트는 아직 없다.

데이터에 선언되지 않은 행동 컴포넌트가 연결되어 있거나, 데이터에 선언된 행동 컴포넌트가 빠져 있으면 `TacticalUnitContext`가 구성 오류를 출력하고 비활성화된다.

### 아트 적용 연출 작업 목표

다음 단계는 현재 논리/입력/대상 선정 구조 위에 실제 아트를 씌워 연출 품질을 올리는 것이다.

우선 연결할 연출 항목:

- 현재 조작 유닛 선택 표시.
- 이동 경로/도착 연출의 실제 캐릭터 이동 애니메이션 보강.
- 플레이어 총 공격 발사/탄흔/피격 연출.
- 근접 공격 타격 연출.
- 검 투척/회수 궤적과 피격 연출.
- 해킹 시작/성공/대상 반응 연출.
- 적 원거리 공격 연출.
- 피격/사망/시체 표시 연출.

사망 연출은 논리적으로 `ActorHealth.IsDead`, `ActorDiedLogicEvent`, `DamageResult.KilledByThisDamage`, `GridActor.ReleaseCellOccupation()` 기준을 유지한다. 즉 죽은 액터는 칸 점유를 해제하지만, 화면상 사망 애니메이션과 시체 표현은 Presenter/Visual 계층에서 처리한다.

## Git 브랜치 기준

- 2026-07-10 기준 S2-T 작업 기준 브랜치는 `main`이다.
- 기존 개발 브랜치 `turn-based-stealth`는 로컬 `main`에 fast-forward 병합 완료됐다.
- 로컬 `main`과 `turn-based-stealth`는 커밋 `0fef79e` 기준으로 같은 내용을 가진다.
- 원격 `origin/main` 반영은 별도 `git push origin main`이 필요하다.

## 통합 전투 애니메이션 연출 구조 - 2026-07-13

### CombatActionPresenter

`CombatActionPresenter`는 씬에 하나만 배치하는 통합 전투 연출 감독이다.
`PresentationEventType.CombatAction`만 처리하며, 공격 판정이나 피해 적용에는 관여하지 않는다.

처리 흐름:

1. `PresentationEvent.Actor`, `TargetActor`로 공격자와 피격자를 확인한다.
2. `ActorPresentationRegistry`에서 양쪽 `ActorVisualController`를 찾는다.
3. 두 Actor가 서로 마주보도록 마지막 좌우 방향을 갱신한다.
4. `AttackPresentationKind`에 맞는 공격자 상태와 `DamageResult.KilledByThisDamage`에 맞는 피격 또는 사망 상태를 같은 프레임에 재생한다.
5. `CombatPresentationData`에 지정된 시간 동안 두 자세를 유지한다.
6. 공격자와 살아 있는 피격자는 `Idle`로 복귀하고, 사망한 피격자는 `Death` 마지막 상태를 유지한다.
7. `PresentationEventHandle.Complete()`를 호출해 다음 큐 이벤트를 진행한다.

현재 처리 대상으로 사용하는 공격 종류:

- `SwordThrow`
- `MeleeWithSword`
- `MeleeUnarmed`
- `PlayerGun`
- `EnemyRanged`

기존 `PlayerGunAttackPresenter`, `PlayerMeleeAttackPresenter`, `EnemyAttackPresenter`는 제거했다.
`SwordActionPresenter`는 피해가 없는 `SwordThrow`와 `SwordRecall`만 처리한다.
피해가 발생한 검 투척은 액션이 `AttackPresentationKind.SwordThrow`를 전달하며 `CombatActionPresenter`가 처리한다.

### ActorPresentationRegistry / ActorPresentationBinding

- `ActorPresentationRegistry`는 씬 단위로 `GridActor -> ActorVisualController` 연결을 관리한다.
- `ActorPresentationBinding`은 캐릭터별 논리 `GridActor`와 화면 `ActorVisualController`를 등록소에 등록한다.
- 논리 액션과 `GridActor`는 Animator나 화면 연출 컴포넌트를 직접 참조하지 않는다.
- 중복 Actor 등록이나 Animator 누락은 fallback 없이 한국어 오류 로그를 남기고 해당 바인딩을 비활성화한다.

### CombatPresentationData

전투 연출 튜닝을 보관하는 `ScriptableObject`다.

공격 종류별 값:

- `AttackKind`: 액션에서 전달한 공격 표현 종류.
- `AttackerAnimationStateName`: 공격자에게 재생할 한 프레임 Animator 상태.
- `PresentationDuration`: 공격자와 피격자 자세를 동시에 유지할 시간.

공통 값:

- `HitAnimationStateName`
- `DeathAnimationStateName`
- `IdleAnimationStateName`
- `CrossFadeDuration`: 한 프레임 이미지 즉시 전환은 0을 사용한다.

데이터 에셋 자체에 `HasValidData()`를 두지 않고 `CombatActionPresenter.HasValidData()`가 상태 이름, 시간, 공격 종류 중복을 검사한다.

### ActorVisualController 애니메이션 / 방향 책임

- Presenter가 요청한 Animator 상태를 재생한다.
- 마지막으로 요청한 상태 이름을 기억하며 루프 상태는 같은 상태 재시작을 생략할 수 있다.
- 기본 일러스트가 왼쪽을 향한다는 기준으로 `SpriteRenderer.flipX`를 적용한다.
- 목표 X 좌표가 오른쪽이면 오른쪽, 왼쪽이면 왼쪽을 바라본다.
- 수직 이동이나 같은 X 좌표 대상은 마지막 좌우 방향을 유지한다.
- 큐 순서, 연출 시간 대기, 피해/사망 판정은 담당하지 않는다.

### 연속 이동 애니메이션 단계

한 칸 단위 `MoveActor`, `EnemyReactionMove` 이벤트는 전체 경로에서 다음 `MovePresentationPhase`를 가진다.

- `Single`: 한 칸 이동. Move 재생 후 Idle 복귀.
- `Start`: 연속 이동 첫 칸. Move 재생.
- `Continue`: 중간 칸. 위치만 보간하고 애니메이션 상태를 다시 시작하지 않음.
- `End`: 마지막 칸. 위치 보간 후 Idle 복귀.

플레이어 이동, 적 경계 반응 이동, 적 턴 이동이 같은 단계 계산을 사용한다.
따라서 여러 칸 이동 중 칸마다 `Move -> Idle -> Move`가 반복되지 않는다.

### 검 Visual 보류 범위

- 검 투척 논리는 현재처럼 검 위치를 즉시 갱신한다.
- 피해 없는 검 투척은 `SwordThrow` 이벤트, 피해가 있는 검 투척은 `CombatAction + AttackPresentationKind.SwordThrow`를 사용한다.
- `FromPosition`, `ToPosition`, `ExecutionPosition` 통로는 유지한다.
- 실제 검 Visual은 후속 작업에서 한 프레임 위치 이동과 이동 경로를 따라 이펙트를 배치하는 방식으로 연결한다.

## 조작 유닛 임시 선택 링 구조 - 2026-07-14

### PlayerUnitSelectionPresenter

`PlayerUnitSelectionPresenter`는 플레이어 조작 유닛마다 하나씩 두는 선택 표시 전용 컴포넌트다.
선택 판정이나 제어권 전환에는 관여하지 않고 기존 `PlayerUnitControlManager.ActiveUnitChanged` 결과만 시각화한다.

현재 동작:

- 담당 `TacticalUnitContext`가 현재 `ActiveUnit`이면 자기 임시 링을 표시한다.
- 다른 유닛으로 제어권이 넘어가면 기존 링을 끄고 새 유닛의 링을 켠다.
- AP 소진에 따른 자동 전환과 직접 클릭 선택이 같은 이벤트 통로를 사용한다.
- `OnEnable`과 `Start`에서 초기화 순서 차이를 보정하고 현재 선택 상태를 즉시 동기화한다.
- 컴포넌트가 꺼지거나 제거되면 이벤트 구독과 런타임 링 머티리얼을 정리한다.
- 대상 유닛 참조 누락, 플레이어 조작 유닛이 아닌 대상, 잘못된 반지름·굵기·선분 수는 fallback 없이 한국어 오류로 드러내고 컴포넌트를 비활성화한다.

임시 표시 방식:

- 별도 아트 에셋 없이 `Sprites/Default` 셰이더와 `LineRenderer`로 원형 링을 런타임 생성한다.
- 유닛마다 위치, 반지름, 선 굵기, 색상, 선분 수, 정렬 순서를 조절할 수 있다.
- 이후 실제 선택 표시 아트가 준비되면 선택 이벤트 구독 구조는 유지하고 임시 LineRenderer 출력만 아트 오브젝트나 애니메이션으로 교체한다.
- 현재 이동 연출은 `GridActorMovePresenter`와 `ActorPresentationSynchronizer`가 `VisualRoot.position`을 직접 갱신하므로 Presenter도 각 유닛의 `VisualRoot`에 둔다.
- `ActorPresentation` 오브젝트는 현재 이동 대상이 아니므로 여기에 선택 표시를 두면 캐릭터 이동을 따라가지 않는다.
- 현재 `Tset` 씬의 두 플레이어 `VisualRoot`에 Presenter가 연결되어 있으며 임시 테스트 값은 `Radius 3`, `Line Width 0.5`, `Sorting Order -1`이다.
- 2D 표시 앞뒤는 우선 `Sorting Order`로 맞추며, 링은 캐릭터보다 낮은 값을 사용한다.

현재 코드 컴파일과 `Tset` 씬의 두 플레이어 유닛 연결을 완료했다.
2026-07-18 기준 직접 선택, AP 자동 전환, 연출 중 선택 변경에 대한 최종 플레이 모드 확인도 완료했다.

### 후속 카메라 연출 기준

- 선택 링은 캐릭터 이동을 따라야 하므로 계속 유닛별 `VisualRoot` 아래에 둔다.
- 카메라 줌·컷·컷신 중 링을 숨길 때 각 Presenter를 개별로 찾거나 끄지 않는다.
- 후속 카메라 시스템에서 씬 단위 게임플레이 표시 허용 상태를 제공하고, 최종 표시 조건을 `현재 선택 유닛 && 게임플레이 표시 허용`으로 확장한다.
- 카메라 연출 시작 시 전역 표시를 한 번 끄고 종료·중단 시 다시 켜 선택된 유닛의 링만 자동 복구한다.
- 전역 표시 컨트롤러의 구체적인 클래스와 이벤트는 카메라 연출 구조를 설계할 때 함께 결정한다.

## 원본 캐릭터 아트 기본 방향 변경 - 2026-07-17

### ActorVisualController

- 원본 캐릭터 아트는 기본적으로 오른쪽을 바라보는 기준을 사용한다.
- `FaceRight()`는 `SpriteRenderer.flipX`를 끄고 원본 방향을 그대로 표시한다.
- `FaceLeft()`는 `SpriteRenderer.flipX`를 켜 원본을 수평 반전한다.
- 인스펙터 필드는 `artworkFacesRight`이며 기본값은 `true`다.
- 기존 `artworkFacesLeft` 직렬화 값은 `FormerlySerializedAs`로 이어받는다. 현재 `Tset` 씬의 기존 값이 모두 `true`이므로 새 오른쪽 기본 아트 기준으로 그대로 이전된다.
- `EnemyGridSight.FacingDirection`은 논리적인 그리드 시야 방향이므로 이번 아트 기준 변경의 영향을 받지 않는다.
