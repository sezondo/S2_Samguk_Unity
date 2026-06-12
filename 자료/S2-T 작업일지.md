# S2-T 작업일지 요약

이 문서는 S2의 턴제 전투 게임 분기인 S2-T 작업 전 빠르게 읽기 위한 날짜별 요약이다.
각 날짜는 `핵심`, `검증`, `다음` 정도만 남기고, 상세 구현 설명은 `S2-T 현재 구현 구조.md`에 반영한다.

# 2026-05-26

## 핵심
- S2-T 명칭을 정했다.
- S2-T 작업 브랜치는 `turn-based-stealth` 기준으로 확인했다.
- 기존 S2 문서와 분리해 `S2-T 현재 구현 구조.md`, `S2-T 작업일지.md`를 만들었다.
- S2-T는 기존 S2 세계관과 해킹/잠입 콘셉트를 유지하되, 턴제 전투 구조로 실험하는 분기로 정리했다.
- Notion 프로젝트 데이터베이스에 `프로젝트 S2-T` 별도 페이지를 만들었다.
- Notion에는 S2-T 목표, 문서 기준, 초기 설계 방향, 해야 할 일 메모만 짧게 정리했다.
- 프로젝트 `AGENTS.md`에 S2-T 분기 작업일지 / Notion 규칙을 추가했다.
- S2-T 기준 브랜치, 구현 구조 문서, 작업일지, Notion 페이지 기준을 명시했다.
- 기존 S2 본류 문서와 S2-T 문서를 분리해서 관리하도록 정리했다.

## 링크
- https://www.notion.so/36c7ee5e06a48188aa3efca9f63e2f8b

## 다음
- 턴제 전투의 첫 구조를 정한다.
- 우선 자유 위치 기반 턴제와 그리드 기반 턴제 중 어느 쪽으로 갈지 결정한다.
- 첫 구현 후보는 `TurnManager`와 플레이어 턴/적 턴 전환 로그 검증이다.

# 2026-05-27

## 핵심
- S2-T 방향성을 문서에 정리했다.
- 기존 실시간 액션 방향은 방향별 이동/공격/회피, 맵 제작 등 아트 리소스 부담이 크다고 판단했다.
- S2-T는 기존 S2를 폐기하는 것이 아니라 별도 프로토타입으로 턴제 잠입 액션 가능성을 검증하는 분기로 정리했다.
- 게임을 메뉴 화면, 스토리 진행 화면, 실제 게임 플레이 화면 3개 축으로 나눴다.
- 실제 플레이는 XCOM식 소규모 격자형 턴제 잠입 전술을 기준으로 잡았다.
- 장르 표현을 `보드게임식 턴제 잠입 스테이지`로 정리했다.
- XCOM식 전술 전투보다 보드게임식 스테이지 클리어 감각을 더 앞에 두기로 했다.
- 침투, 검 투척, 해킹, AP, 행동 후 감지 판정, 아트 제작량 제한 원칙을 핵심 기준으로 정리했다.
- Notion `프로젝트 S2-T` 페이지를 `보드게임식 턴제 잠입 스테이지` 기준으로 갱신했다.
- Notion에는 장르, 화면 구성, 핵심 규칙, 아트 기준, 문서 기준, 해야 할 일 메모만 짧게 남겼다.
- S2-T 기준에서 혼란을 줄이기 위해 기존 실시간 액션 코드 대부분을 삭제했다.
- Player, Enemy, Legacy, 실시간 Dialogue 런타임, CameraMove, MeleeHitbox, 실시간 액션 데이터 스크립트를 제거했다.
- 최소 공통 코드로 `IDamageable`, `IHackable`, `HackableData`, Dialogue 데이터 3종, `VfxManager`만 남겼다.
- 삭제 후 `Assembly-CSharp.csproj`의 누락된 Compile 참조를 정리했다.
- S2-T에서도 인게임 말풍선 시스템을 유지하기로 했다.
- 기존 `PlayerInput` 의존 런타임 대신, S2-T용으로 입력 의존성이 없는 `DialogueManager`, `DialogueBubblePresenter`, `DialogueSpeaker`, `SpeechBubbleView`를 새로 추가했다.
- 보드게임식 인게임 화면에서 토큰/적/장치 위에 말풍선을 띄울 수 있는 기준으로 정리했다.

## 검증
- `dotnet build Assembly-CSharp.csproj --no-restore` 통과.
- 경고 0개, 오류 0개.
- 변수 주석과 규칙 문서 반영 후 `dotnet build Assembly-CSharp.csproj --no-restore` 재검증 통과.
- 함수 주석과 규칙 문서 반영 후 `dotnet build Assembly-CSharp.csproj --no-restore` 재검증 통과.
- 로그 한글화와 규칙 문서 반영 후 `dotnet build Assembly-CSharp.csproj --no-restore` 재검증 통과.

## 다음
- S2-T 전용 테스트 씬을 만들지, 기존 테스트 씬을 복제해 쓸지 결정한다.
- 첫 코드 작업은 `TurnManager`와 플레이어 턴/적 턴 전환 로그 검증으로 시작한다.
- 이어서 `GridManager`, 1칸 이동, AP 소비, 행동 완료 후 감지 판정 순서로 구현한다.

# 2026-05-28

## 핵심
- S2-T의 첫 그리드 틀로 `GridPosition`, `GridManager`, `GridActor`를 추가했다.
- 턴제 규칙은 Unity 월드 좌표가 아니라 격자 좌표 `GridPosition`을 기준으로 계산하도록 방향을 잡았다.
- `GridManager`에서 보드 범위, 좌표 변환, 칸 점유, 이동 가능 여부를 관리한다.
- `GridActor`는 플레이어/적/장치가 공통으로 쓸 보드 위 말의 최소 단위로 만들었다.

## 검증
- `dotnet build Assembly-CSharp.csproj --no-restore` 통과.
- 경고 0개, 오류 0개.

## 다음
- 테스트 씬에 `GridManager`와 임시 `GridActor`를 배치해 Scene 뷰에서 격자를 확인한다.
- 이후 `TurnManager`, AP, 플레이어 1칸 이동 입력을 연결한다.

# 2026-05-29

## 핵심
- `GridActor`가 시작 칸 등록에 실패해도 화면상 같은 칸으로 스냅되던 문제를 수정했다.
- 칸을 점유하는 액터는 `GridManager.RegisterActor()` 성공 시에만 격자에 배치된다.
- 중복 시작 칸 또는 보드 범위 밖 좌표로 등록에 실패하면 오류 로그를 남기고 기본값 기준으로 오브젝트를 비활성화한다.
- 작업일지 제목을 날짜별로 합쳐 `작업 기록`, `추가 작업 기록` 같은 중복 구분을 제거했다.
- `GridPlayerDebugMover`를 추가했다.
- 정식 `PlayerInput`을 붙이기 전까지 WASD로 `GridActor`의 그리드 이동을 확인한다.
- 이동 전에 `GridManager.CanEnter()`로 목표 칸에 들어갈 수 있는지 확인하고, 가능할 때만 `GridActor.TryMoveTo()`를 호출한다.
- Unity Input System의 `Keyboard.current`를 사용하되, `PlayerInput` 컴포넌트는 사용하지 않는다.
- `GridActor`가 `GridManager`보다 먼저 활성화되어도 `Start()`에서 다시 등록을 시도하도록 보정했다.
- 디버그 이동 요청이 실패할 때 원인을 추적할 수 있도록 실패 로그를 추가했다.
- 정식 조작은 WASD가 아니라 마우스 기반으로 정했다.
- 알파 버전은 UI에서 이동/공격/해킹/검 투척 같은 행동을 선택한 뒤 그리드 칸이나 오브젝트를 클릭하는 A안으로 간다.
- 우클릭 컨텍스트 메뉴로 가능한 행동을 선택하는 B안은 알파 버전 이후 확장 단계에서 검토한다.
- 이동은 AP 1당 1칸이 아니라, AP 1을 소비해 맨해튼 거리 기준 최대 3칸까지 이동하는 방식으로 정했다.
- AP는 행동 시작 시점에 소비한다.
- 적 시야 검사는 최종 도착 칸만 보지 않고 이동 경로의 각 칸마다 수행한다.

## 검증
- `dotnet build Assembly-CSharp.csproj --no-restore` 통과.
- 경고 0개, 오류 0개.

## 추가 핵심
- `TurnManager`를 추가했다.
- `TurnSide.Player`, `TurnSide.Enemy` 기준으로 현재 턴 주체를 관리한다.
- `TurnStarted`, `TurnEnded` 이벤트를 열어 AP, 행동 선택, 적 행동이 턴 흐름에 붙을 수 있게 했다.
- 정식 턴 종료 UI 전까지 스페이스바로 턴 전환 로그를 확인할 수 있게 했다.
- `ActionPoint`를 추가했다.
- 플레이어 턴 시작 시 AP를 보충하고, `CanSpend()`, `TrySpend()`로 행동 비용을 확인/소비하게 했다.
- `GridPlayerDebugMover`가 플레이어 턴과 AP를 확인한 뒤 이동하게 연결했다.
- 같은 오브젝트에 `ActionPoint`가 있으면 WASD 1칸 이동마다 AP 1을 소비한다.
- 이 WASD 연결은 정식 조작이 아니라 턴/AP 검증용으로 둔다.

## 검증
- `dotnet build Assembly-CSharp.csproj --no-restore` 통과.
- 경고 0개, 오류 0개.

## 다음
- 씬의 Manager에 `TurnManager`를 붙이고, 플레이어 토큰에 `ActionPoint`를 붙여 AP 로그와 이동 제한을 확인한다.
- 이후 마우스 기반 이동 행동 선택 상태와 이동 가능 칸 표시를 구현한다.

# 2026-05-30

## 핵심
- `PlayerGridMoveAction`을 추가했다.
- UI 버튼이 붙기 전까지 `M` 키로 이동 행동 선택을 검증할 수 있게 했다.
- 이동 행동 선택 중 좌클릭한 칸을 `GridManager.WorldToGrid()`로 변환해 목표 칸으로 사용한다.
- 이동 가능 칸은 현재 플레이어 위치 기준 맨해튼 거리 3칸 이내로 계산한다.
- 이동 행동 1회는 AP 1을 행동 시작 시점에 소비한다.
- 정식 경로 탐색 전까지 X축 우선, Y축 후속의 단순 맨해튼 경로를 사용한다.
- 이동 가능 칸 표시, 칸 진입 시야 검사, 이동 완료 후속 처리를 붙을 수 있도록 이벤트를 열어뒀다.
- S2-T 핵심 스크립트의 멤버 변수와 주요 상태 변수에 한국어 주석을 추가했다.
- S2-T 핵심 스크립트의 함수에 무엇을 하는 함수인지 한국어 XML 주석을 추가했다.
- `AGENTS.md`와 구현 구조 문서에 앞으로 새 변수와 함수에는 의미 주석을 붙이는 규칙을 명시했다.
- S2-T 스크립트의 런타임 로그 문장을 한글로 바꿨다.
- `AGENTS.md`와 구현 구조 문서에 앞으로 런타임 로그는 기본적으로 한국어로 작성하는 규칙을 명시했다.

## 검증
- `dotnet build Assembly-CSharp.csproj --no-restore` 통과.
- 경고 0개, 오류 0개.

## 다음
- 테스트 씬에서 플레이어 토큰에 `PlayerGridMoveAction`을 붙여 마우스 이동 흐름을 확인한다.
- 이동 가능 칸 표시용 하이라이트 컴포넌트를 추가한다.
- 이후 경로 계산과 `MoveStepEntered` 기반 적 시야 검사를 연결한다.

# 2026-06-01

## 핵심
- 전체 화면 Game 뷰에서 `PlayerGridMoveAction`의 `M` 키 이동 행동 선택과 마우스 클릭 이동 흐름을 확인했다.
- `GridMoveRangeHighlighter`를 추가했다.
- `PlayerGridMoveAction.MoveRangeShown`, `MoveRangeHidden` 이벤트를 구독해 이동 가능 칸 하이라이트를 표시하고 숨기게 했다.
- 하이라이트 프리팹이 없어도 런타임에 임시 반투명 사각형 스프라이트를 생성해 테스트할 수 있게 했다.
- `Tset` 씬의 플레이어 토큰에 `GridMoveRangeHighlighter`를 연결했다.
- 하이라이트 표시 책임을 공용 `GridCellHighlighter`와 이동 행동 연결용 `GridMoveRangeHighlighter`로 분리했다.
- `GridCellHighlighter`는 이동뿐 아니라 검 투척 범위, 해킹 예상 범위 같은 다른 칸 표시에도 재사용할 수 있는 표시 전용 컴포넌트로 둔다.

## 검증
- `dotnet build Assembly-CSharp.csproj --no-restore` 통과.
- 경고 0개, 오류 0개.
- Unity 플레이 모드에서 `M` 입력 시 이동 가능 칸 하이라이트가 표시되는 것을 확인했다.
- 이동 완료 후 하이라이트가 사라지는 것을 확인했다.

## 다음
- 하이라이트는 현재 이동 가능 칸 표시까지만 유지한다.
- `GridHighlightPurpose`, `IGridHighlightTarget`, 대상 투명도/점멸 표현, 검 투척/해킹 범위 하이라이트는 실제 사용처가 생길 때 추가한다.
- 다음 구현 후보는 단순 X축 우선 경로를 대체할 경로 계산 구조 정리다.
- 이후 `MoveStepEntered` 기반 적 시야 검사 연결을 준비한다.

## 추가 핵심
- 플레이어 턴 수치가 `ActionPoint`, `PlayerGridMoveAction`, `GridPlayerDebugMover`에 흩어져 있던 상태를 정리하기 시작했다.
- `PlayerTurnData`를 추가해 최대 AP, 턴 시작 AP, 이동 범위, 이동 AP 비용을 데이터 에셋으로 관리하게 했다.
- `PlayerContext`를 추가해 플레이어의 `PlayerTurnData`, `GridActor`, `ActionPoint`, `PlayerGridMoveAction` 참조를 모았다.
- `ActionPoint`는 AP 수치를 `PlayerContext.TurnData` 기준으로만 사용한다.
- `PlayerGridMoveAction`은 이동 범위와 이동 AP 비용을 `PlayerContext.TurnData` 기준으로만 사용한다.
- `GridPlayerDebugMover`는 디버그 이동 AP 비용을 `PlayerContext.TurnData` 기준으로만 사용한다.
- 필수 데이터나 참조가 비어 있으면 fallback 없이 `HasValidReference()`, `HasValidData()`에서 오류 로그를 남기고 컴포넌트를 비활성화하게 했다.
- `PlayerTurnData` 자체가 유효성 검사를 들고 있지 않고, 데이터를 사용하는 `ActionPoint`, `PlayerGridMoveAction`, `GridPlayerDebugMover`가 각자 필요한 필드를 `HasValidData()`에서 직접 검사하고 로그를 남기게 했다.
- `PlayerGridMoveAction`, `ActionPoint`, `GridPlayerDebugMover`에서 핵심 컴포넌트를 직접 `GetComponent<T>()`로 보정하던 흐름을 제거했다.
- `GridMoveRangeHighlighter`도 필수 표시 컴포넌트 참조가 비어 있으면 `HasValidReference()`에서 오류를 남기고 비활성화하게 했다.
- `Tset` 씬의 플레이어 토큰에 `PlayerContext`를 붙이고 `PlayerTurnData` 에셋을 연결했다.

## 추가 검증
- `dotnet build Assembly-CSharp.csproj --no-restore` 통과.
- 경고 0개, 오류 0개.

## 작업 규칙 정리
- 실제 작업 시작 지시 전에는 코드를 수정하지 않고 먼저 작업 방향을 토론하는 기준을 `AGENTS.md`에 명시했다.
- 인스펙터 세팅은 사용자가 직접 갈아 끼우는 것을 기본으로 두고, AI는 문제 확인을 위해 조회할 수 있지만 임의 수정은 사용자 지시가 있을 때만 진행하는 기준으로 정리했다.
- 필수 데이터/참조 누락 시 fallback 없이 `HasValidData()`, `HasValidReference()`에서 오류 로그를 남기고 흐름을 중단하는 기준을 명시했다.

# 2026-06-03

## 핵심
- `GridPathfinder`를 추가해 격자 이동 가능 칸과 목표 칸까지의 경로 계산을 공용 도구로 분리했다.
- `PlayerGridMoveAction`의 X축 우선 단순 경로 계산을 제거하고 `GridPathfinder` 기반 BFS 경로 계산을 사용하게 했다.
- 이동 가능 칸 하이라이트도 실제 도달 가능한 칸만 표시하도록 `GridPathfinder.FindReachablePositions()`를 사용하게 했다.

## 검증
- `dotnet build Assembly-CSharp.csproj --no-restore` 통과.
- 경고 0개, 오류 0개.

## 다음
- Unity 플레이 모드에서 장애물/점유 칸 우회 이동과 하이라이트 표시를 확인한다.
- 이후 `MoveStepEntered` 기반 적 시야 검사 연결을 준비한다.

## 2026-06-04

## 핵심
- `GridManager`에 인스펙터 설정용 `blockedPositions`를 추가했다.
- 이동불가 칸은 런타임에 `HashSet<GridPosition>`으로 변환해 `IsBlocked()`와 `CanEnter()`에서 사용한다.
- `CanEnter()`가 보드 범위, 이동불가 칸, 점유 칸을 함께 검사하게 바꿨다.
- Scene 뷰 Gizmo에서 이동불가 칸을 별도 색상으로 표시하게 했다.

## 검증
- `dotnet build Assembly-CSharp.csproj --no-restore` 통과.
- 경고 0개, 오류 0개.

## 다음
- Unity 플레이 모드에서 `blockedPositions`를 설정한 뒤 하이라이트와 경로 우회가 정상인지 확인한다.

## 추가 핵심
- `GridCellState`를 추가해 칸의 현재 상태를 보관하는 데이터 주머니를 만들었다.
- `GridManager`의 내부 저장 구조를 `Dictionary<GridPosition, GridCellState>` 중심으로 변경했다.
- 기존 `CanEnter()`, `IsBlocked()`, `IsOccupied()`, `TryGetActorAt()`, `RegisterActor()`, `UnregisterActor()`, `TryMoveActor()` 외부 API는 유지했다.
- 점유 액터와 이동불가 상태는 이제 각 `GridCellState`에 기록된다.
- `GridManager`는 칸 상태 변경의 승인자 역할을 유지하고, 외부 시스템이 `GridCellState`를 직접 수정하지 않는 방향으로 잡았다.

## 추가 검증
- `dotnet build Assembly-CSharp.csproj --no-restore` 통과.
- 경고 0개, 오류 0개.

## 다음
- Unity 플레이 모드에서 기존 이동, 점유, 이동불가 칸 우회가 동일하게 동작하는지 확인한다.
- 이후 특수 오브젝트/칸 효과/엄폐 슬롯은 실제 기능이 필요해질 때 `GridCellState`에 단계적으로 추가한다.

# 2026-06-07

## 핵심
- `PlayerGridMoveAction`에 이동 경로 미리보기 1차 구조를 추가했다.
- 이동 행동 선택 중 마우스가 가리키는 이동 가능 칸까지 `GridPathfinder.TryFindPath()`로 경로를 계산한다.
- 현재 미리보기 표시는 별도 UI 없이 Scene 뷰 `Gizmos`로 처리한다.
- 이동 선택 취소 또는 이동 완료 시 경로 미리보기 버퍼를 정리한다.

## 검증
- `dotnet build Assembly-CSharp.csproj --no-restore` 통과.
- 경고 0개, 오류 0개.

## 다음
- Unity 플레이 모드에서 이동 선택 중 마우스 오버 경로 Gizmo가 의도대로 보이는지 확인한다.
- 이후 경로 미리보기를 런타임 표시 컴포넌트로 분리할지 검토한다.

## 추가 핵심
- 경로 미리보기를 Scene 뷰 `Gizmos` 방식에서 런타임 하이라이트 오브젝트 방식으로 바꿨다.
- `PlayerGridMoveAction`이 경로 미리보기 전용 `GridCellHighlighter`를 런타임에 생성한다.
- 마우스가 가리키는 이동 가능 칸까지의 경로가 바뀌면 경로 칸 오브젝트를 표시하고, 취소/이동 완료/무효 칸에서는 숨긴다.
- `GridCellHighlighter`에 런타임 스타일 설정용 `ConfigureFallbackStyle()`을 추가했다.

## 추가 검증
- `dotnet build Assembly-CSharp.csproj --no-restore` 통과.
- 경고 0개, 오류 0개.

## 다음
- Unity 플레이 모드 Game 뷰에서 이동 경로 프리뷰 오브젝트가 정상 표시되는지 확인한다.

# 2026-06-08

## 핵심
- 적 시야 계산 1차 구조를 추가했다.
- `EnemyData`를 추가해 정면 시야 거리, 근접 감지 사용 여부, 근접 감지 반경을 데이터 에셋으로 관리하게 했다.
- `EnemyContext`를 추가해 `EnemyData`, `GridActor`, `EnemyGridSight` 참조를 모았다.
- `GridDirection`을 추가해 적이 바라보는 방향을 상하좌우 4방향으로 제한했다.
- `EnemyGridSight`를 추가해 정면 부채꼴 시야와 주변 8칸 근접 감지를 계산하게 했다.
- 정면 시야는 거리 1부터 5까지 전방 거리에 따라 좌우 폭이 넓어지는 구조로 계산한다.
- 장애물 칸은 정면 시야에 포함하지 않고, 같은 레인에서 장애물 뒤 칸을 차단한다.
- 근접 감지는 바라보는 방향과 장애물 영향 없이 주변 8칸을 감지한다.

## 검증
- `dotnet build Assembly-CSharp.csproj --no-restore` 통과.
- 경고 0개, 오류 0개.

## 다음
- 테스트 씬에 임시 적 오브젝트, `EnemyData`, `EnemyContext`, `EnemyGridSight`를 연결해 감지 칸 계산을 확인한다.
- 이후 `GridCellHighlighter`로 적 시야 칸 표시를 연결한다.


## 추가 핵심
- `PlayerGridMoveAction`에서 `IsValidMoveTarget()`을 제거했다.
- 이동 실행과 경로 프리뷰의 실제 도달 가능 판정은 `GridPathfinder.TryFindPath()`를 단일 기준으로 사용하게 정리했다.
- `movablePositions`는 이동 가능 범위 하이라이트 표시용 캐시로만 남겼다.
- `GridDirection`, `EnemyData`, `EnemyContext`, `EnemyGridSight`를 추가해 적 시야 계산 1차 구조를 만들었다.
- 적 정면 시야는 상하좌우 4방향 기준 부채꼴이며, 기본 거리는 5칸이다.
- 장애물 칸은 정면 시야에 포함하지 않고, 같은 레인에서 장애물 뒤 칸을 차단한다.
- 근접 감지는 방향과 장애물 영향 없이 주변 8칸을 감지한다.
- 기존 `EnemyDataTest.asset` 연결을 유지하기 위해 새 `EnemyData.cs.meta`에 기존 EnemyData GUID를 사용했다.

## 추가 검증
- `dotnet build Assembly-CSharp.csproj --no-restore` 통과.
- 경고 0개, 오류 0개.

## 다음
- 테스트 씬에 임시 적 오브젝트를 만들고 `EnemyData`, `EnemyContext`, `EnemyGridSight` 참조를 연결한다.
- `GridCellHighlighter`를 재사용해 적 시야 칸 표시를 확인한다.
- 이후 플레이어 이동 경로의 각 칸이 적 시야에 포함되는지 검사해 발각 판정을 연결한다.

# 2026-06-10

## 핵심
- S2-T의 장기 전투/잠입 방향성을 2D XCOM식 턴제 잠입 전술로 정리했다.
- 플레이어 이동은 목표 칸 선택 후 경로를 따라 슬라이드하듯 이동하는 방식으로 잡았다.
- AP가 3이고 AP 1당 이동량이 3칸이면 최대 9칸까지 이동할 수 있는 식으로, AP와 이동 가능 거리를 연결하는 방향을 잡았다.
- 이동 가능 범위는 AP 소비 구간별로 1~3칸 파랑, 4~6칸 노랑, 7~9칸 빨강처럼 색을 나누어 표시하는 기준을 남겼다.
- 적 시야 범위 자체는 항상 표시하지 않고, 이동 중 어느 지점에서 애드가 발생하는지 플레이어가 알 수 있는 범위 안에서 경고 표시를 제공하는 방향으로 정했다.
- 플레이어 시야 밖의 적은 사전 경고 없이 실제 이동 중 애드 이벤트를 발생시킨다.
- 이동 중 적 시야에 들어오면 해당 지점에서 일시 정지하고, 카메라 줌/연출/적 AI 반응 후 남은 이동을 마저 진행하는 큰 흐름을 정했다.
- 직접 공격은 즉시 애드로 보고, 해킹이나 교란은 플레이어 위치를 바로 들키는 것이 아니라 적을 경계 태세로 전환시키는 방향으로 잡았다.

## 다음
- 현재 즉시 이동 구조를 나중에 경로 순차 처리와 중간 애드 이벤트를 끼울 수 있는 액션 시퀀스 구조로 확장한다.
- 이동 경로 평가, 애드 판정, 경고 표시를 서로 다른 책임으로 분리하는 설계를 유지한다.

## 추가 핵심
- AP 기반 다구간 이동 범위 1차 구조를 구현했다.
- `PlayerTurnData`에 `moveDistancePerActionPoint`를 추가했다.
- 현재 AP와 AP당 이동량을 기준으로 플레이어가 한 번에 이동 가능한 최대 거리를 계산하게 했다.
- 이동 경로 길이에 따라 AP 비용을 계산하게 했다.
- 기본값 기준 1~3칸은 AP 1, 4~6칸은 AP 2, 7~9칸은 AP 3을 소비한다.
- 이동 가능 칸을 AP 소비 구간별로 파랑, 노랑, 빨강 하이라이트로 나누어 표시하게 했다.
- `GridPathfinder.FindReachablePositionDistances()`를 추가해 각 이동 가능 칸까지의 실제 최단 거리를 얻을 수 있게 했다.
- 기존 이동 범위 하이라이트 참조가 끊기지 않도록 `GridMoveRangeHighlighter`의 기존 `cellHighlighter` 필드를 `FormerlySerializedAs`로 보존했다.

## 추가 검증
- `dotnet build Assembly-CSharp.csproj --no-restore` 통과.
- 경고 0개, 오류 0개.

## 다음
- Unity 플레이 모드에서 AP 3 기준 9칸 표시와 파랑/노랑/빨강 구간 표시를 확인한다.
- 4~6칸 이동 시 AP 2, 7~9칸 이동 시 AP 3이 소비되는지 확인한다.
- 이후 이동 경로의 애드 위험 지점 경고 표시를 붙인다.

## 추가 핵심
- 이동 경로 위험 평가 1차 구조를 구현했다.
- `PlayerGridMoveAction`에 `MovePathPreviewShown`, `MovePathPreviewHidden` 이벤트를 추가했다.
- `GridMoveRiskEvaluator`를 추가해 경로 미리보기 중 처음 적 시야에 들어가는 칸을 찾고 경고 하이라이트로 표시하게 했다.
- 실제 이동 중 적 시야 칸에 진입하면 1차 애드 로그를 출력하게 했다.
- 적 시야 목록은 임의 검색하지 않고 인스펙터에서 명시 연결하는 기준으로 잡았다.
- 이번 단계에서는 이동 중단, 카메라 줌, 적 AI 반응은 구현하지 않고 후속 연결 지점만 만들었다.

## 추가 검증
- `dotnet build Assembly-CSharp.csproj --no-restore` 통과.
- 경고 0개, 오류 0개.

## 다음
- Unity 플레이 모드에서 `GridMoveRiskEvaluator.enemySights`에 테스트 적 시야를 연결하고 이동 경로 위험 칸 표시를 확인한다.
- 위험 칸 진입 시 애드 로그가 1회 출력되는지 확인한다.
- 이후 경고 표시를 XCOM식 아이콘 UI로 교체하고, 실제 이동 일시 정지/카메라 연출/적 AI 반응으로 확장한다.

## 추가 핵심
- `GridMoveRiskEvaluator`를 `PlayerContext` 핵심 컴포넌트 참조에 추가했다.
- `PlayerContext.GridMoveRiskEvaluator` 프로퍼티를 추가했다.
- `PlayerContext.HasValidReference()`에서 `GridMoveRiskEvaluator` 누락을 검사하게 했다.

## 추가 검증
- `dotnet build Assembly-CSharp.csproj --no-restore` 통과.
- 경고 0개, 오류 0개.

## 추가 핵심
- `GridMoveRiskEvaluator`가 `PlayerGridMoveAction`을 직접 인스펙터 참조로 들지 않게 변경했다.
- `GridMoveRiskEvaluator`는 `PlayerContext`를 참조하고, `PlayerContext.GridMoveAction`에서 이동 행동 컴포넌트를 꺼내 이벤트를 구독한다.
- 플레이어 계열 컴포넌트의 핵심 참조는 `PlayerContext`를 통해 접근한다는 규칙에 맞췄다.

## 추가 검증
- `dotnet build Assembly-CSharp.csproj --no-restore` 통과.
- 경고 0개, 오류 0개.

## 추가 핵심
- 애드는 최초 감지 적 1명만 반응하는 사건이 아니라 주변 적 집단으로 전파되는 사건으로 정리했다.
- 플레이어를 실제로 본 적이 최초 감지자가 되고, 해당 적 주변 일정 범위 안의 적들도 함께 애드된다.
- 애드 전파 범위는 별도 값으로 관리한다.
- `GridMoveRiskEvaluator`는 최초 감지 칸과 감지 적을 찾는 역할까지만 맡기고, 실제 전파는 후속 Alert 전담 시스템으로 분리하는 방향을 잡았다.
- 전파 전담 시스템 후보 이름은 `EnemyAlertCoordinator`, `EnemyAlertManager`, `GridAlertPropagator`다.

## 다음
- 실제 애드 구현 시 최초 감지 적과 전파 대상 적 목록을 함께 다룰 수 있는 이벤트 구조를 설계한다.

## 추가 핵심
- `GridMoveRangeHighlighter`의 이동 범위 하이라이터 소유 방식을 통일했다.
- 기존에는 파랑 구간만 인스펙터 `GridCellHighlighter` 참조를 쓰고 노랑/빨강은 런타임 생성했지만, 이제 파랑/노랑/빨강 모두 런타임 생성으로 맞췄다.
- `GridMoveRangeHighlighter`에서 `GridCellHighlighter` 필수 컴포넌트 요구와 인스펙터 참조 검사를 제거했다.
- 이동 범위 표시용 `GridCellHighlighter`는 이제 플레이어 오브젝트에 별도로 붙이지 않아도 된다.

## 추가 검증
- `dotnet build Assembly-CSharp.csproj --no-restore` 통과.
- 경고 0개, 오류 0개.

## 추가 핵심
- `GridMoveRangeHighlighter`를 `PlayerContext` 핵심 컴포넌트 참조에 추가했다.
- `PlayerContext.GridMoveRangeHighlighter` 프로퍼티를 추가했다.
- `PlayerContext.HasValidReference()`에서 `GridMoveRangeHighlighter` 누락을 검사하게 했다.

## 추가 검증
- `dotnet build Assembly-CSharp.csproj --no-restore` 통과.
- 경고 0개, 오류 0개.

## 추가 핵심
- `EnemyGridSight`에 `Start()` 시점 `RefreshSight()` 재계산을 추가했다.
- `GridManager` 또는 `GridActor` 초기화 순서 때문에 `Awake()` 시점 시야 계산이 비는 상황을 보정하기 위한 처리다.

## 추가 검증
- `dotnet build Assembly-CSharp.csproj --no-restore` 통과.
- 경고 0개, 오류 0개.

## 마무리 확인
- Unity 플레이 모드에서 AP 기반 이동 범위 표시를 확인했다.
- 이동 경로 중 애드 위험 칸 경고 표시를 확인했다.
- 실제 이동 중 위험 칸 진입 시 애드 로그가 출력되는 것을 확인했다.
- 오늘 작업은 여기서 마무리하고, 다음 작업은 `EnemyRegistry` 추가로 정했다.

## 다음
- `EnemyRegistry`를 추가해 `GridMoveRiskEvaluator.enemySights` 수동 연결을 제거한다.
- 적 시야 컴포넌트가 등록/해제되고, 위험 평가가 레지스트리의 적 시야 목록을 사용하도록 바꾼다.
- 이후 `AddTriggered` 이벤트와 애드 전파 시스템을 설계한다.

## 추가 핵심
- `EnemyRegistry`를 추가해 현재 씬의 활성 적 시야 목록을 등록/해제 기반으로 관리하게 했다.
- `EnemyGridSight`가 활성화 시 `EnemyRegistry`에 등록하고 비활성화 시 해제되게 연결했다.
- 씬 초기화 순서 때문에 등록소가 아직 준비되지 않은 경우를 고려해 `Start()`에서 한 번 더 등록을 시도한다.
- `GridMoveRiskEvaluator.enemySights` 수동 배열을 제거하고 `EnemyRegistry.Instance.GridSights` 기준으로 이동 경로 위험을 평가하게 했다.
- `GridMoveRiskEvaluator`의 초기화 순서를 정리해 `PlayerContext` 누락 시 NullReference보다 명확한 오류 로그가 먼저 나오도록 했다.

## 추가 검증
- `dotnet build Assembly-CSharp.csproj --no-restore` 통과.
- 경고 0개, 오류 0개.

## 다음
- 테스트 씬에 `EnemyRegistry` 오브젝트를 추가하고 플레이 모드에서 적 시야 자동 등록, 이동 경로 위험 표시, 위험 칸 진입 애드 로그를 확인한다.
- 이후 `AddTriggered` 이벤트와 애드 전파 전담 시스템을 설계한다.

## 추가 문서 정리
- `S2-T 현재 구현 구조.md`에 `EnemyRegistry 기반 적 시야 등록 구조` 섹션을 추가했다.
- `EnemyRegistry`, `EnemyGridSight`, `GridMoveRiskEvaluator`의 책임 분리와 씬 구성 기준을 명확히 정리했다.
- 기존 `GridMoveRiskEvaluator.enemySights` 수동 배열 기준은 현재 구현 기준에서 폐기하고, `EnemyRegistry.Instance.GridSights` 조회 기준으로 정리했다.
- 다음 Unity 확인 작업은 `Tset` 씬에 `EnemyRegistry` 오브젝트를 배치하고 자동 등록/위험 평가 흐름을 검증하는 것으로 남겼다.

## 2026-06-12

## 핵심
- `GridMoveRiskEvaluator`에 `AlertTriggered` 이벤트를 추가했다.
- 실제 이동 중 적 시야에 처음 들어갔을 때 감지 칸 `GridPosition`과 최초 감지 적 `EnemyGridSight`를 함께 전달하게 했다.
- 기존 애드 로그는 유지하되, 후속 시스템이 로그가 아니라 이벤트를 구독해 처리할 수 있는 연결 지점을 만들었다.
- 기존 문서의 `AddTriggered` 후보 이름은 코드에서는 의미가 더 명확한 `AlertTriggered`로 정리했다.

## 검증
- `dotnet build Assembly-CSharp.csproj --no-restore` 통과.
- 경고 0개, 오류 0개.

## 다음
- `EnemyAlertCoordinator`를 추가해 `AlertTriggered`를 구독한다.
- 최초 감지 적 기준으로 주변 적에게 애드 전파하는 구조를 설계한다.

## 추가 핵심
- `EnemyRegistry`를 `EnemyGridSight` 목록 기반에서 `EnemyContext` 목록 기반으로 정리했다.
- `EnemyContext`가 활성화/비활성화 생명주기에 맞춰 `EnemyRegistry`에 등록/해제되게 했다.
- `EnemyGridSight`에서는 등록소 등록/해제 책임을 제거하고 시야 계산 책임만 남겼다.
- `GridMoveRiskEvaluator`는 `EnemyRegistry.Enemies`를 순회하며 각 `EnemyContext.GridSight`로 감지 여부를 확인하게 했다.
- 이 구조는 후속 `EnemyAlertCoordinator`가 같은 적 목록을 사용해 애드 전파 대상을 계산하기 위한 기준이다.

## 추가 검증
- `dotnet build Assembly-CSharp.csproj --no-restore` 통과.
- 경고 0개, 오류 0개.

## 다음
- `EnemyAlertCoordinator`를 추가해 `GridMoveRiskEvaluator.AlertTriggered`를 구독한다.
- `EnemyRegistry.Enemies` 기준으로 최초 감지 적 주변의 전파 대상 적을 계산한다.

## 추가 핵심
- `EnemyData`에 `alertSpreadRange`를 추가했다.
- 애드 전파 범위는 전역 고정값이 아니라 최초 감지 적의 데이터에서 읽도록 기준을 잡았다.
- `EnemyAlertCoordinator`를 추가했다.
- `EnemyAlertCoordinator`는 `GridMoveRiskEvaluator.AlertTriggered`를 구독하고, 최초 감지 적 기준으로 `EnemyRegistry.Enemies`를 순회해 전파 대상 적을 계산한다.
- 현재 단계에서는 전파 대상 로그 출력까지만 구현하고 실제 적 상태 전환은 아직 하지 않는다.

## 추가 검증
- `dotnet build Assembly-CSharp.csproj --no-restore` 통과.
- 경고 0개, 오류 0개.

## 다음
- Unity 씬에 `EnemyAlertCoordinator`를 배치하고 `PlayerContext`를 연결해 전파 로그를 확인한다.
- 이후 `EnemyAlertState`를 추가해 로그 대신 실제 적 상태 전환 요청을 연결한다.

## 추가 테스트 확인
- Unity 플레이 모드에서 `EnemyRegistry`, `EnemyContext` 등록 흐름을 확인했다.
- `GridMoveRiskEvaluator.AlertTriggered` 발행 이후 `EnemyAlertCoordinator`가 전파 대상 로그를 출력하는 흐름을 확인했다.
- `EnemyData.AlertSpreadRange` 기준으로 최초 감지 적 주변에 애드가 전파되는 1차 구조를 확인했다.
- 현재는 실제 적 상태 전환 없이 로그 출력 단계로 유지한다.
- 연쇄 전파 구조는 맵 크기와 적 배치 밀도 기준이 잡힌 뒤 BFS/큐 기반으로 확장하기로 했다.

## 다음
- `EnemyAlertState`를 추가해 적의 평상/발각 상태를 저장한다.
- `EnemyAlertCoordinator`가 전파 대상 로그 대신 적 상태 전환 요청을 보내게 한다.

## 추가 핵심
- `EnemyAlertLevel` enum을 추가해 적 상태를 `Normal`, `Alerted`로 구분했다.
- `EnemyAlertState`를 추가해 적 하나의 현재 발각 상태를 보관하고 `RequestAlert()` 요청으로 상태를 바꾸게 했다.
- `EnemyAlertState`는 상태 변경 시 `AlertLevelChanged` 이벤트를 발행하고 로그를 출력한다.
- `EnemyContext`에 `EnemyAlertState` 참조를 추가하고 필수 참조 검사에 포함했다.
- `EnemyAlertCoordinator`는 전파 대상 로그만 찍는 대신 `enemy.AlertState.RequestAlert()`를 호출하게 변경했다.

## 추가 검증
- `dotnet build Assembly-CSharp.csproj --no-restore` 통과.
- 경고 0개, 오류 0개.

## 다음
- Unity 씬의 각 적 오브젝트에 `EnemyAlertState`를 추가하고 `EnemyContext.AlertState`에 연결한다.
- 플레이 모드에서 발각 시 적 상태가 `Normal`에서 `Alerted`로 바뀌는지 확인한다.

## 문서 정리
- `S2-T 현재 구현 구조.md`를 최신 구현 기준 문서로 전체 재정리했다.
- 과거 작업 흐름, 완료된 다음 작업, 현재 코드와 충돌하는 예전 기준은 제거했다.
- 문서 구조를 Grid, Turn/AP, Player 이동, Enemy 구조, Enemy Alert, Data, Dialogue/VFX, 현재 한계, 다음 작업 중심으로 재구성했다.
- 상세 작업 이력은 `S2-T 작업일지.md`에 남기고, 현재 구현 구조 문서는 현재 상태와 앞으로 할 일만 담는 기준으로 정리했다.
