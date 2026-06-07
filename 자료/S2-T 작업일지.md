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
