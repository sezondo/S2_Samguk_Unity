# S2-T 현재 구현 구조

최신 기준: 2026-06-16
브랜치: `turn-based-stealth`
프로젝트 명칭: `S2-T`

이 문서는 현재 S2의 메인 개발 방향인 보드게임식 턴제 잠입 퍼즐/전술 게임 S2-T의 구현 구조를 빠르게 파악하기 위한 문서다.
작업 순서, 과거 실험 기록, 변경 이력은 `S2-T 작업일지.md`에서 관리한다.

## 최신 기준 요약

- S2-T는 사이버 조선 세계관을 유지한 현재 메인 개발 방향의 보드게임식 턴제 잠입 퍼즐/전술 게임이다.
- 플레이어는 한 명이며, AP를 사용해 격자 보드에서 이동한다.
- 이동은 목표 칸을 선택하면 BFS 경로를 따라 한 칸씩 처리하는 구조다.
- 이동 가능 범위는 현재 AP와 AP당 이동 거리 기준으로 계산한다.
- 적 시야는 `EnemyGridSight`가 그리드 칸 단위로 계산한다.
- 플레이어 이동 경로가 적 시야에 들어가면 `GridMoveRiskEvaluator.AlertTriggered` 이벤트가 발생한다.
- 애드 전파는 `EnemyAlertCoordinator`가 담당한다.
- 적의 현재 발각 상태는 `EnemyAlertState`가 보관한다.
- 현재 애드 구조는 단일 단계 전파 기준이며, 연쇄 전파는 맵 크기와 적 밀도 기준이 잡힌 뒤 확장한다.

## 현재 목표

S2-T의 현재 구현 목표는 `이동 -> 위험 경고 -> 발각 이벤트 -> 애드 전파 -> 적 상태 전환`까지의 최소 루프를 만드는 것이다.
현재 실제 피해, 적 AI 반응, 카메라 연출, UI 경고, 이동 일시 정지는 아직 구현하지 않는다.

## 씬 구성 기준

현재 테스트 기준 씬은 `Assets/Scenes/Tset.unity`다.

씬에는 다음 계열 오브젝트가 필요하다.

- `GridManager`: 보드 크기, 좌표 변환, 칸 상태, 점유 상태를 관리한다.
- `TurnManager`: 플레이어/적 턴 전환 이벤트를 관리한다.
- 플레이어 토큰: `PlayerContext`, `GridActor`, `ActionPoint`, `PlayerGridMoveAction`, `GridMoveRiskEvaluator`, `GridMoveRangeHighlighter`를 가진다.
- 적 토큰: `EnemyContext`, `GridActor`, `EnemyGridSight`, `EnemyAlertState`를 가진다.
- `EnemyRegistry`: 현재 씬의 활성 `EnemyContext` 목록을 관리한다.
- `EnemyAlertCoordinator`: 플레이어 발각 이벤트를 구독하고 애드 전파를 처리한다.
- Dialogue/VFX 관련 오브젝트는 필요한 테스트에서만 배치한다.

## 코드 폴더 구조

현재 주요 스크립트 폴더 역할은 다음과 같다.

- `Assets/Script/Grid`: 격자 좌표, 보드 상태, 점유, 경로 탐색.
- `Assets/Script/Turn`: 턴 진행과 AP 관리.
- `Assets/Script/TurnAction`: 플레이어 이동 행동, 이동 범위 표시, 이동 경로 위험 평가.
- `Assets/Script/Player`: 플레이어 핵심 참조를 모으는 `PlayerContext`.
- `Assets/Script/Enemy`: 적 핵심 참조, 시야, 등록소, 애드 전파, 발각 상태.
- `Assets/Script/DataScript/Data`: 플레이어/적/대사/해킹 데이터 에셋.
- `Assets/Script/Dialogue`: 말풍선 대사 시스템.
- `Assets/Script/Common`: 공용 VFX 풀.
- `Assets/Script/Combat`: 공용 전투/해킹 인터페이스.

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
- `blockedPositions` 기준 이동불가 칸 반영.
- `Dictionary<GridPosition, GridCellState>`로 칸 상태 관리.
- `RegisterActor()`, `UnregisterActor()`, `TryMoveActor()`로 점유 상태 변경.
- Scene 뷰 Gizmo로 보드, 이동불가 칸, 점유 칸 표시.

주의:

- `GridManager`는 플레이어/적/장치 구분을 알지 않는다.
- 적 검색, 애드 전파, AI 판단은 `GridManager` 책임이 아니다.

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

현재 적 턴 행동 AI는 아직 구현하지 않았다.

### ActionPoint

`ActionPoint`는 플레이어의 AP를 관리한다.

책임:

- 플레이어 턴 시작 시 AP 보충.
- `CanSpend()`, `TrySpend()`로 AP 소비 처리.
- AP 변경 이벤트 발행.

AP 수치는 `PlayerContext.TurnData`의 `PlayerTurnData`에서 읽는다.
필수 참조나 데이터가 없으면 fallback 없이 오류를 남기고 컴포넌트를 비활성화한다.

## Player 구조

### PlayerTurnData

`PlayerTurnData`는 플레이어 턴 기반 수치를 보관하는 `ScriptableObject`다.

현재 값:

- `MaxActionPoint`: 최대 AP.
- `StartTurnActionPoint`: 턴 시작 시 보충 AP.
- `MoveDistancePerActionPoint`: AP 1개 구간당 이동 가능 칸 수.
- `MoveRange`: 기존 호환용 이동 범위 값. 새 이동 구조에서는 직접 사용하지 않는다.
- `MoveActionPointCost`: 이동 거리 구간 1개가 소비하는 AP 비용.

데이터 에셋 자체에는 `HasValidData()` 책임을 두지 않는다.
데이터를 사용하는 컴포넌트가 필요한 값의 유효성을 직접 검사한다.

### PlayerContext

`PlayerContext`는 플레이어 루트의 참조 주머니다.
정책 계산이나 상태 변경을 직접 하지 않는다.

현재 참조:

- `PlayerTurnData TurnData`
- `GridActor GridActor`
- `ActionPoint ActionPoint`
- `PlayerGridMoveAction GridMoveAction`
- `GridMoveRiskEvaluator GridMoveRiskEvaluator`
- `GridMoveRangeHighlighter GridMoveRangeHighlighter`

플레이어 계열 컴포넌트는 같은 루트의 핵심 컴포넌트를 직접 `GetComponent<T>()`로 찾지 않고 `PlayerContext`에서 꺼내 쓴다.

## Player 이동

### PlayerGridMoveAction

`PlayerGridMoveAction`은 플레이어의 마우스 기반 그리드 이동 행동을 담당한다.

현재 임시 입력:

- `M` 키로 이동 행동 선택.
- 좌클릭으로 목표 칸 선택.
- 우클릭 또는 Escape로 선택 취소.

책임:

- 플레이어 턴과 AP 조건 확인.
- 현재 AP 기준 이동 가능 거리 계산.
- `GridPathfinder`로 이동 가능 칸과 목표 경로 계산.
- 이동 시작 시 AP 소비.
- 경로 칸을 순서대로 이동 처리.
- 이동 중 각 칸 진입마다 `MoveStepEntered` 이벤트 발행.
- 이동 완료 시 `MoveCompleted` 이벤트 발행.
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

### AlertTriggered 이벤트

`GridMoveRiskEvaluator.AlertTriggered`는 플레이어가 실제 이동 중 적 시야에 처음 들어갔을 때 발생한다.

전달 값:

- 발각 칸 `GridPosition`
- 최초 감지 적 시야 `EnemyGridSight`

현재 기준:

- 이동 1회당 첫 발각만 이벤트를 발행한다.
- 여러 적이 동시에 감지할 때 대표 감지자 선택 규칙은 아직 단순 등록 순서 기반이다.

### EnemyAlertCoordinator

`EnemyAlertCoordinator`는 씬 단위 애드 전파 조정자다.

책임:

- `PlayerContext.GridMoveRiskEvaluator.AlertTriggered`를 구독한다.
- 이벤트의 `EnemyGridSight`를 기준으로 `EnemyRegistry.Enemies`에서 최초 감지 적 `EnemyContext`를 찾는다.
- 최초 감지 적의 `EnemyData.AlertSpreadRange`를 읽는다.
- 최초 감지 적 위치 기준으로 등록된 모든 적과의 맨해튼 거리를 계산한다.
- 범위 안의 적에게 `EnemyAlertState.RequestAlert()`를 호출한다.

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

### PlayerTurnData

플레이어의 AP와 이동 관련 수치를 보관한다.
실제 유효성 검사는 데이터를 사용하는 컴포넌트가 담당한다.

### EnemyData

적의 시야와 애드 전파 관련 수치를 보관한다.
실제 유효성 검사는 데이터를 사용하는 컴포넌트가 담당한다.

### HackableData

해킹 가능한 대상의 기본 데이터 형태다.
현재 S2-T 핵심 루프에는 아직 직접 연결되어 있지 않다.

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
현재 S2-T의 실제 피해 루프는 아직 구현하지 않았다.

### IHackable

해킹 가능한 대상의 공통 규약이다.
현재 S2-T의 검 투척/해킹 루프는 아직 구현하지 않았다.

## 현재 한계

- 플레이어 이동 중 발각 시 이동을 일시 정지하지 않는다.
- 발각 시 카메라 줌, 경고 UI, 컷인 연출은 없다.
- 적 상태는 `Normal`, `Alerted` 두 단계뿐이다.
- 적 AI 반응은 아직 없다.
- 애드 전파는 단일 단계이며 연쇄 전파는 아직 없다.
- 플레이어 시야/정보 공개 기준이 없어 보이지 않는 적의 위험 경고 숨김은 아직 없다.
- 공격, 해킹, 검 투척, 검 회수는 아직 핵심 루프에 연결되지 않았다.
- 승리 조건, 스테이지 목표, 메뉴/스토리 화면은 아직 구현하지 않았다.

## 다음 작업

1. Unity 씬에서 모든 적 오브젝트에 `EnemyAlertState`를 추가하고 `EnemyContext.AlertState`에 연결한다.
2. 플레이 모드에서 발각 시 적 상태가 `Normal`에서 `Alerted`로 바뀌는지 확인한다.
3. `EnemyAlertState.AlertLevelChanged`를 이용해 색상 변경 또는 임시 UI 표시를 연결한다.
4. 발각 시 플레이어 이동을 일시 정지할 수 있는 액션 시퀀스 구조를 검토한다.
5. 맵 크기와 적 배치 밀도 기준이 잡히면 애드 연쇄 전파 구조를 BFS/큐 기반으로 확장한다.
6. 이후 검 투척/해킹/해킹 대상 오브젝트 루프로 넘어간다.


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

`StageGoalManager`는 플레이어 이동 완료 이벤트를 감시해 목표 칸 도달 시 스테이지 클리어를 알린다.

책임:

- `PlayerContext.GridMoveAction.MoveCompleted` 이벤트를 구독한다.
- 이동 완료 위치가 `StageGoal.GoalPosition`과 같으면 클리어 처리한다.
- `StageCleared` 이벤트를 발행한다.
- 현재 단계에서는 클리어 로그만 출력한다.

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
- 실제 게임 시스템과의 연결은 아직 없다.
- 다음 작업은 `PlayerGridMoveAction`의 이동 결과를 `PresentationEvent.MoveActor`로 큐에 넣고, 실제 화면 이동용 View 컴포넌트를 연결하는 것이다.
