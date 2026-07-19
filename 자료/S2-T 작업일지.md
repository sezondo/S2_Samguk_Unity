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

## 2026-07-01 피해 요청 / 통합 전투 연출 이벤트 구조

## 핵심
- 공격과 피격/사망 연출은 동시에 맞물려야 하므로 피해가 있는 공격은 하나의 통합 전투 연출 이벤트로 묶는 기준으로 정리했다.
- `ApplyDamageLogicEvent`를 추가했다. 공격 행동은 직접 `TakeDamage()`와 `DamageAppliedLogicEvent`를 처리하지 않고 피해 적용 요청만 발행한다.
- `DamageResolutionCoordinator`를 추가했다. 이 기본 논리 이벤트 처리자는 씬 배치 없이 `ActionLogicEventBus`에 등록된다.
- `DamageResolutionCoordinator`는 `ApplyDamageLogicEvent`를 받아 대상의 `IDamageable.TakeDamage()`를 호출하고 `DamageAppliedLogicEvent`를 발행한다.
- 이번 피해로 새로 전투불능이 되면 `ActorDiedLogicEvent`도 발행한다.
- 피해 결과가 있는 공격 연출은 `PresentationEventType.CombatAction`으로 묶고, 공격자/피격자/공격 종류/피해 전후 HP/사망 여부를 함께 전달한다.
- `PlayerMeleeAttackAction`, `PlayerSwordThrowAction`은 더 이상 직접 `TakeDamage()`를 호출하지 않는다.
- 피해 대상이 없는 검 투척은 기존 `SwordThrow` 연출 이벤트를 유지한다.
- `SwordActionPresenter`는 `CombatAction`을 받아 임시 로그/대기 연출을 처리하며, 추후 공격/피격/사망 애니메이션 타이밍을 이 이벤트 안에서 맞춘다.
- 현재 `SwordActionPresenter`의 피격측 처리는 로그 확인용이다. 아트/애니메이션이 준비되면 `TargetActor`와 `DamageResult`를 기준으로 피격자 애니메이션과 사망 애니메이션을 연결한다.
- `S2TDebugOverlay`는 `ActorDiedLogicEvent`도 받아 마지막 사망 정보를 표시한다.

## 검증
- `dotnet build Assembly-CSharp.csproj --no-restore` 통과.
- 경고 0개, 오류 0개.
- Unity 플레이 모드에서 현재 코드 흐름 테스트 완료.

## 다음
- `CombatAction` 기반으로 실제 공격 애니메이션, 피격 반응, 사망 애니메이션 타이밍을 `SwordActionPresenter` 또는 전용 Presenter에서 확장한다.
- 피격측 연출은 `PresentationEvent.TargetActor`를 추적하고, HP 전후 값과 사망 여부는 `DamageResult` 스냅샷을 사용한다.

## 2026-07-01 S2-T 디버그 오버레이

## 핵심
- 전체 턴제 테스트 수치를 화면에서 보기 위한 `S2TDebugOverlay`를 추가했다.
- 새 폴더 `Assets/Script/Debug`를 만들고 디버그 전용 스크립트를 분리했다.
- `OnGUI`와 `GUIStyle` 기반으로 턴, 스테이지 상태, 연출 큐 상태, 플레이어 위치/AP/HP/검 상태/행동 선택 상태를 표시한다.
- `EnemyRegistry` 기준 활성 적 수와 경계 상태 적 수를 표시한다.
- `DamageAppliedLogicEvent`를 구독해 마지막 피해의 공격자, 대상, 대상 칸, 피해량, 적용 여부, HP 전후 값, 이번 피해 사망 여부를 표시한다.
- 오버레이는 게임 상태를 바꾸지 않고 연결된 참조와 이벤트 스냅샷만 읽는다.

## 검증
- `dotnet build Assembly-CSharp.csproj --no-restore` 통과.
- 경고 0개, 오류 0개.

## 다음
- Tset 씬에 `S2TDebugOverlay`를 배치하고 `PlayerContext`, 플레이어 `ActorHealth`, `StageStateManager` 참조를 연결한다.
- 플레이 모드에서 검 투척/근접 공격 시 Last Damage 수치가 `DamageResult`와 맞게 표시되는지 확인한다.

## 2026-07-01 HP 스냅샷 기반 피해 결과

## 핵심
- 실제 HP와 연출용 HP가 어긋나는 문제를 피하기 위해 피해 결과 스냅샷 구조를 추가했다.
- `DamageResult`를 추가해 피해 적용 여부, 피해량, 피해 전 HP, 피해 후 HP, 피해 전 사망 여부, 피해 후 사망 여부를 한 값으로 전달하게 했다.
- `IDamageable.TakeDamage()` 반환값을 `bool`에서 `DamageResult`로 변경했다.
- `ActorHealth`는 실제 HP를 즉시 변경하되, 연출이 사용할 수 있는 전후 HP 스냅샷을 `DamageResult`로 반환한다.
- `DamageAppliedLogicEvent`는 기존 `Damage`, `Applied` 값 대신 `DamageResult`를 보관하고, 기존 접근 편의를 위해 `Damage`, `Applied` 속성은 유지했다.
- `PlayerSwordThrowAction`, `PlayerMeleeAttackAction`은 `TakeDamage()` 결과를 받아 `DamageAppliedLogicEvent`에 전달하게 했다.
- 아직 HP바, 사망 연출, 적 제거, 플레이어 패배 처리는 추가하지 않았다.

## 검증
- `dotnet build Assembly-CSharp.csproj --no-restore` 통과.
- 경고 0개, 오류 0개.

## 다음
- `DamageResult.HitPointBefore`, `HitPointAfter`, `KilledByThisDamage`를 사용하는 피해/HP바 Presenter를 추가한다.
- 사망 처리는 논리 상태 확정과 연출 완료 후 제거/비활성화 시점을 분리해서 설계한다.
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

## 문서 / Notion 기준 정리
- S2-T를 더 이상 임시 실험 분기가 아니라 현재 S2의 메인 개발 방향으로 정리했다.
- `AGENTS.md`의 S2-T 문서/Notion 규칙을 새 기준으로 갱신했다.
- Notion `프로젝트 S2`는 세계관, 설정, 시나리오 문서로 사용한다.
- Notion `프로젝트 S2-T`는 현재 게임 방향, 핵심 규칙, 현재 상태, 다음 작업만 짧게 정리하는 간단 설명란으로 사용한다.
- 날짜별 진행 요약과 상세 구현 내용은 Notion에 쌓지 않고 로컬 작업일지와 현재 구현 구조 문서에 남긴다.

## 2026-06-13

## 핵심
- `EnemyAlertVisual`을 추가해 `EnemyAlertState.AlertLevelChanged` 이벤트를 적 스프라이트 색상 변경으로 표시하게 했다.
- 해당 컴포넌트는 게임 규칙에는 관여하지 않고, 발각 상태 확인용 임시 시각 피드백만 담당한다.

## 검증
- `dotnet build Assembly-CSharp.csproj --no-restore` 통과.
- 경고 0개, 오류 0개.

## 다음
- Unity 씬의 각 적 오브젝트에 `EnemyAlertVisual`을 추가하고 `EnemyAlertState`, `SpriteRenderer`를 연결해 발각 시 색상 전환을 확인한다.

## 추가 핵심
- `EnemyAlertVisual`을 `EnemyContext` 핵심 참조에 추가했다.
- 이후 적 UI나 발각 연출 시스템이 `EnemyContext.AlertVisual`을 통해 시각 피드백 컴포넌트에 접근할 수 있게 했다.
- `EnemyContext.HasValidReference()`에서 `EnemyAlertVisual` 누락도 검사하게 했다.

## 추가 검증
- `dotnet build Assembly-CSharp.csproj --no-restore` 통과.
- 경고 0개, 오류 0개.

## 다음
- Unity 씬의 각 적 `EnemyContext.AlertVisual`에 해당 적의 `EnemyAlertVisual`을 연결한다.

## 추가 핵심
- `EnemyAlertVisual`이 `EnemyAlertState`를 직접 인스펙터 참조로 받지 않고 `EnemyContext.AlertState`에서 꺼내 쓰게 변경했다.
- 적 시각 피드백도 Context 기준 참조 흐름을 따르도록 정리했다.

## 추가 검증
- `dotnet build Assembly-CSharp.csproj --no-restore` 통과.
- 경고 0개, 오류 0개.

## 다음
- Unity 씬의 각 적 `EnemyAlertVisual.EnemyContext`와 `TargetRenderer`, `EnemyContext.AlertVisual`을 연결한다.

## 2026-06-14

## 핵심
- `StageGoal`을 추가해 스테이지 목표 그리드 칸을 데이터로 제공하게 했다.
- `StageGoalManager`를 추가해 `PlayerGridMoveAction.MoveCompleted` 이벤트를 구독하고, 플레이어가 목표 칸에 도착하면 스테이지 클리어 이벤트를 발생시키게 했다.
- 현재 단계에서는 클리어 로그와 `StageCleared` 이벤트까지만 제공한다.
- 목표 칸은 Scene 뷰 Gizmo로 표시할 수 있게 했다.

## 검증
- `dotnet build Assembly-CSharp.csproj --no-restore` 통과.
- 경고 0개, 오류 0개.

## 다음
- Unity 씬에 `StageGoal`과 `StageGoalManager`를 배치하고 `PlayerContext`, 목표 칸을 연결해 도착 시 클리어 로그를 확인한다.

## 2026-06-15

## 핵심
- `StageStateManager`를 추가해 스테이지 전체 진행 상태를 `Playing`, `Cleared`, `Failed`로 관리하게 했다.
- `StageStateManager`는 `StageGoalManager.StageCleared` 이벤트를 구독하고 목표 달성 시 `Playing`에서 `Cleared`로 상태를 전환한다.
- 후속 실패 조건 연결을 위해 `RequestFail()` 진입점을 열어뒀다.
- `StageGoalManager` 로그 문장을 스테이지 클리어 확정이 아니라 목표 칸 도착 감지로 정리했다.

## 검증
- `dotnet build Assembly-CSharp.csproj --no-restore` 통과.
- 경고 0개, 오류 0개.

## 다음
- Unity 씬에 `StageStateManager`를 배치하고 `StageGoalManager`를 연결해 목표 도달 시 `Playing -> Cleared` 상태 전환 로그를 확인한다.
- 이후 클리어 UI 또는 입력 잠금/결과 화면 연결을 검토한다.

## 2026-06-16

## 핵심
- S2-T의 행동 처리 기준을 판정과 연출 분리 구조로 정했다.
- 판정 시스템은 논리 오브젝트 기준으로 결과를 먼저 계산하고, 연출 시스템은 계산된 결과를 큐에서 순서대로 재생하는 기준으로 정리했다.
- 논리 오브젝트와 연출 오브젝트를 분리하기로 했다. `GridActor`, `Context`, AP/상태 컴포넌트는 판정 기준이고, 향후 `GridActorView` 같은 연출 컴포넌트는 화면 표시만 담당한다.
- 판정 결과는 `PresentationEvent` 형태로 연출 큐에 넣기로 했다.
- 연출 큐는 씬 단위 싱글톤 후보 `ActionPresentationQueue`로 두고, 큐 보관, 순서 실행, 완료 대기, `IsPlaying` 제공만 담당하게 한다.
- 이벤트 처리 방식은 A안인 브로드캐스트 + bool 반환 방식으로 정했다.
- 구독자는 자신이 처리할 이벤트이면 `true`를 반환하고, 연출 종료 시 `PresentationEventHandle.Complete()`를 호출한다.
- 아무도 처리하지 않는 이벤트는 큐 매니저가 경고 로그 후 자동 완료 처리하는 기준으로 정했다.
- 연출 큐 실행 중에는 플레이어 입력과 인게임 UI 조작을 막는 기준을 세웠다.

## 설계 기준
- 입력 가능 조건은 `StageStateManager.IsPlaying == true && ActionPresentationQueue.IsPlaying == false`로 잡는다.
- 연출 오브젝트는 판정에 관여하지 않는다.
- 큐 매니저는 판정과 실제 연출 내용을 알지 않고 순서 제어만 담당한다.
- 공격, 해킹, 검 투척, 검 회수, 오브젝트 조작, 적 AI 반응도 같은 연출 큐 기준으로 확장한다.

## 다음
- `PresentationEventType`, `PresentationEvent`, `PresentationEventHandle`, `ActionPresentationQueue`의 1차 뼈대를 추가한다.
- 처음에는 실제 연출 대신 로그 기반 테스트 리시버로 큐 순서와 완료 신호 흐름을 검증한다.
- 이후 `PlayerGridMoveAction`의 즉시 이동 구조를 논리 이동과 연출 이동 분리 구조로 바꾼다.

## 추가 핵심
- 연출 큐 1차 뼈대를 `Assets/Script/Presentation` 폴더에 추가했다.
- `PresentationEventType`을 추가해 `MoveActor`, `AlertDetected`, `EnemyReactionMove`, `Attack`, `Hack`, `Interact`, `StageCleared`, `StageFailed` 이벤트 종류를 정의했다.
- `PresentationEvent`를 추가해 연출 큐에 들어갈 이벤트 데이터 구조를 만들었다.
- `PresentationEventHandle`을 추가해 이벤트 처리자가 완료 신호를 보낼 수 있게 했다.
- `ActionPresentationQueue`를 추가해 씬 단위 싱글톤 큐, 이벤트 추가, 순차 실행, 완료 대기, 처리자 없음 자동 완료, 큐 종료 이벤트를 제공하게 했다.
- `DebugPresentationEventReceiver`를 추가해 큐 이벤트 수신과 완료 신호 흐름을 로그로 검증할 수 있게 했다.
- `DebugPresentationQueueTester`를 추가해 샘플 `MoveActor`, `AlertDetected`, `StageCleared` 이벤트를 큐에 넣어 테스트할 수 있게 했다.

## 추가 검증
- `dotnet build Assembly-CSharp.csproj --no-restore` 통과.
- 경고 0개, 오류 0개.

## 다음
- Unity 씬에 `ActionPresentationQueue`, `DebugPresentationEventReceiver`, `DebugPresentationQueueTester`를 배치해 샘플 이벤트가 순서대로 실행되고 완료되는지 확인한다.
- 이후 `PlayerGridMoveAction`에서 이동 결과를 `PresentationEvent.MoveActor`로 큐에 넣는 흐름을 연결한다.

## 2026-06-17

## 테스트 확인
- Unity 플레이 모드에서 `ActionPresentationQueue`, `DebugPresentationEventReceiver`, `DebugPresentationQueueTester`를 이용한 연출 큐 샘플 이벤트 흐름을 확인했다.
- 샘플 `MoveActor`, `AlertDetected`, `StageCleared` 이벤트가 큐에 들어가고 순서대로 실행되는 것을 확인했다.
- `DebugPresentationEventReceiver`가 이벤트를 수신하고 `PresentationEventHandle.Complete()`를 호출해 다음 이벤트로 넘어가는 흐름을 확인했다.
- 연출 큐가 모두 비면 큐 종료 로그가 출력되는 것을 확인했다.

## 다음
- `PlayerGridMoveAction`의 이동 결과를 `PresentationEvent.MoveActor`로 큐에 넣는 흐름을 검토한다.
- 이후 실제 연출용 `GridActorView` 또는 `GridActorVisual`을 추가해 논리 이동과 화면 이동을 분리한다.

## 추가 설계 정리
- Actor 계층 기준을 `PlayerLogic`, `ActorPresentation`, `VisualRoot` 3단 구조로 정했다.
- 기존 플레이어 자식 `GridActor` 오브젝트는 논리 역할을 유지하고 이름만 `PlayerLogic`으로 바꾸는 방향으로 잡았다.
- `PlayerContext`는 `PlayerLogic` 계층의 핵심 참조 주머니로 유지한다.
- `ActorPresentation`에는 `GridActorMovePresenter`, 공격/해킹/피격 Presenter 같은 연출 이벤트 처리 컴포넌트를 둔다.
- `VisualRoot`에는 `SpriteRenderer`, `Animator`, 시각 제어 컴포넌트를 둔다.
- 연출 큐 완료 신호는 `VisualRoot`가 아니라 이벤트를 처리한 Presenter가 최종 호출하는 규칙으로 정했다.
- `VisualRoot`는 큐를 모르고, Presenter에게 시각 작업 완료 콜백만 돌려준다.
- 이동, 공격, 해킹 등 연출 데이터는 기능별 Presenter가 각자 들고 시작하며, 커지면 기능별 `ScriptableObject`로 분리한다.

## 다음
- 실제 코드 작업 시 `GridActorMovePresenter`부터 추가해 `PresentationEvent.MoveActor`를 받아 `VisualRoot` 이동/애니메이션 요청 후 완료 신호를 보내는 흐름을 만든다.

## 추가 핵심
- `MovePresentationData`를 추가해 이동 연출 시간, 보간 곡선, 이동/대기 애니메이션 상태 이름을 데이터 에셋으로 관리하게 했다.
- `ActorVisualController`를 추가해 VisualRoot의 `SpriteRenderer`, `Animator` 제어 틀을 만들었다.
- `GridActorMovePresenter`를 추가해 `PresentationEvent.MoveActor`와 선택적으로 `EnemyReactionMove`를 받아 자기 `GridActor` 대상 이동 연출을 처리하게 했다.
- `GridActorMovePresenter`는 `GridManager.GridToWorld()`로 그리드 좌표를 월드 좌표로 바꾸고, `VisualRoot` Transform을 `MovePresentationData.MoveCurve` 기준으로 보간한다.
- 이동 애니메이션 시작/종료 요청은 `ActorVisualController`에 맡기고, `PresentationEventHandle.Complete()`는 Presenter가 호출하는 규칙을 코드로 반영했다.
- `DebugPresentationQueueTester`에 `sampleMoveActor`, 시작/목표 칸 필드를 추가해 디버그 `MoveActor` 이벤트가 실제 Presenter 대상 Actor를 가리킬 수 있게 했다.

## 추가 검증
- `dotnet build Assembly-CSharp.csproj --no-restore` 통과.
- 경고 0개, 오류 0개.

## 다음
- Unity 씬에서 `MovePresentationData` 에셋을 만들고 `ActorPresentation` 계층에 `GridActorMovePresenter`, `VisualRoot`에 `ActorVisualController`를 연결한다.
- `DebugPresentationQueueTester.SampleMoveActor`에 같은 `GridActor`를 연결해 디버그 MoveActor 이벤트로 VisualRoot가 이동하는지 확인한다.
- 이후 `PlayerGridMoveAction`의 실제 이동 결과를 연출 큐에 넣는 흐름을 연결한다.

## 추가 정리
- `Presentation` 폴더 하위 구조를 정리했다.
- `MovePresentationData`는 `Assets/Script/Presentation/Data`로 이동했다.
- `GridActorMovePresenter`는 `Assets/Script/Presentation/Presenter`로 이동했다.
- `ActorVisualController`는 `Assets/Script/Presentation/Visual`로 이동했다.
- Unity GUID 유지를 위해 기존 `.meta` 파일도 함께 이동했다.

## 추가 검증
- `dotnet build Assembly-CSharp.csproj --no-restore` 통과.
- 경고 0개, 오류 0개.

## 추가 핵심
- `ActionResolutionContext`를 추가해 행동 하나에서 파생되는 논리 이벤트를 큐로 처리하는 1차 통로를 만들었다.
- `ActionLogicEventBus`, `IActionLogicEvent`, `IActionLogicEventHandler`를 추가해 활성 논리 시스템이 자신이 처리할 이벤트만 받도록 했다.
- `MoveStepEnteredLogicEvent`, `MoveCompletedLogicEvent`, `AlertTriggeredLogicEvent`, `EnemyAlertedLogicEvent`, `StageClearedLogicEvent`를 추가했다.
- `PlayerActionFlowController`를 추가해 플레이어 이동 행동 실행, 논리 이벤트 처리 완료, 연출 큐 실행 순서를 조정하게 했다.
- `PlayerGridMoveAction`은 이제 이동 경로의 각 칸을 논리 이동하면서 1칸 단위 `PresentationEvent.MoveActor`를 큐에 넣고, 논리 이벤트를 `ActionResolutionContext`에 발행한다.
- `GridMoveRiskEvaluator`는 기존 `MoveStepEntered` 직접 구독 대신 `MoveStepEnteredLogicEvent`를 처리해 발각 판정, `AlertDetected` 연출 이벤트, `AlertTriggeredLogicEvent` 발행을 담당한다.
- `EnemyAlertCoordinator`는 기존 `GridMoveRiskEvaluator.AlertTriggered` 직접 구독 대신 `AlertTriggeredLogicEvent`를 처리해 애드 전파와 적 상태 전환을 담당한다.
- `StageGoalManager`는 `MoveCompletedLogicEvent`를 처리해 목표 도착을 판정하고, 클리어 시 `StageClearedLogicEvent`와 `StageCleared` 연출 이벤트를 발행한다.
- `StageStateManager`는 `StageClearedLogicEvent`를 받아 스테이지 상태를 `Cleared`로 전환한다. 기존 `StageGoalManager.StageCleared` 직접 이벤트는 시작 위치 클리어 같은 보조 흐름을 위해 유지했다.
- `ActorPresentationSynchronizer`를 추가해 씬 시작 시 `VisualRoot` 위치를 논리 `GridActor.GridPosition` 기준 월드 위치로 동기화할 수 있게 했다.

## 추가 검증
- `dotnet build Assembly-CSharp.csproj --no-restore` 통과.
- 경고 0개, 오류 0개.

## 다음
- Unity 씬에 `PlayerActionFlowController`를 배치하고 `PlayerContext`, `ActionPresentationQueue`를 연결한다.
- ActorPresentation 계층에 `ActorPresentationSynchronizer`를 추가해 `TargetActor`와 `VisualRoot`를 연결한다.
- 플레이 모드에서 M 이동 입력 후 논리 이벤트 처리, 1칸 단위 이동 연출, 발각/애드/클리어 이벤트 순서를 확인한다.

## 추가 핵심
- `PlayerMoveInputController`를 추가해 M 키 이동 선택, 마우스 목표 칸 변환, 좌클릭 실행, 우클릭/Escape 취소 입력을 `PlayerGridMoveAction`에서 분리했다.
- `PlayerGridMoveAction`은 입력 처리 `Update()`를 제거하고 이동 선택 상태, 경로 미리보기 갱신, AP 소비, 논리 이동, 논리/연출 이벤트 발행 책임만 남겼다.
- 이동 목표 실행은 `PlayerMoveInputController`가 `PlayerActionFlowController`를 우선 호출하고, 연결이 없으면 기존 `PlayerGridMoveAction.TryExecuteMoveTo()` 경로를 사용하게 했다.

## 추가 검증
- `dotnet build Assembly-CSharp.csproj --no-restore` 통과.
- 경고 0개, 오류 0개.

## 다음
- Unity 씬의 플레이어 입력 담당 오브젝트에 `PlayerMoveInputController`를 추가하고 `PlayerContext`, 필요 시 `PlayerActionFlowController`, `WorldCamera`를 연결한다.

## 추가 정리
- `PlayerGridMoveAction.TryExecuteMoveTo(GridPosition)` 직접 실행 경로를 제거했다.
- 이제 이동 실행은 `PlayerMoveInputController -> PlayerActionFlowController -> PlayerGridMoveAction.TryExecuteMoveTo(GridPosition, ActionResolutionContext)` 흐름으로만 들어간다.
- `PlayerMoveInputController`는 `PlayerActionFlowController` 참조를 필수로 검사한다.

## 추가 검증
- `dotnet build Assembly-CSharp.csproj --no-restore` 통과.
- 경고 0개, 오류 0개.

## 추가 정리
- `PlayerGridMoveAction`에 남아 있던 `MoveStepEntered`, `MoveCompleted` 직접 C# 이벤트를 제거했다.
- 이동 중 판정과 목표 판정은 `MoveStepEnteredLogicEvent`, `MoveCompletedLogicEvent` 논리 이벤트 통로만 사용하게 정리했다.
- `PlayerGridMoveAction` 안의 `ActionResolutionContext.Resolve()` 호출은 칸 단위 이동 연출 사이에 발각/클리어 같은 후속 연출 이벤트를 정확한 순서로 끼워 넣기 위한 규칙으로 명시했다.

## 추가 검증
- `dotnet build Assembly-CSharp.csproj --no-restore` 통과.
- 경고 0개, 오류 0개.

## 2026-06-20 테스트 확인

## 핵심
- 오늘 작업은 S2-T 행동 처리의 핵심 규칙을 세운 작업으로 정리한다.
- `ActionResolutionContext` 기반 논리 이벤트 통로, `PlayerActionFlowController`, `PlayerMoveInputController`, `ActorPresentationSynchronizer`를 실제 씬 테스트 기준으로 연결했다.
- 플레이어 이동 입력은 `PlayerMoveInputController -> PlayerActionFlowController -> PlayerGridMoveAction` 흐름으로 정리했다.
- 이동 판정은 논리 위치를 먼저 확정하고, 화면 이동은 `ActionPresentationQueue`에 쌓인 1칸 단위 `MoveActor` 연출 이벤트로 따라오게 했다.
- 이동 경로 중 칸 진입 판정은 `MoveStepEnteredLogicEvent`, 이동 완료 판정은 `MoveCompletedLogicEvent`로 통일했다.
- `MoveStepEntered`, `MoveCompleted` 직접 C# 이벤트는 제거해 게임 규칙 판정 통로를 `ActionResolutionContext`로 모았다.
- 칸 단위 `Resolve()` 호출 규칙을 정했다. 이동 연출 사이에 발각, 클리어 같은 후속 연출 이벤트를 정확한 순서로 끼워 넣기 위한 처리다.
- `ActorPresentationSynchronizer`로 씬 시작 시 VisualRoot 위치를 논리 `GridActor.GridPosition` 기준 위치에 동기화하는 흐름을 확인했다.
- 현재 구조는 논리 선처리 / 연출 후재생 구조로 유지한다. 중간 연출 결과를 보고 규칙을 바꾸는 단계형 액션 시퀀서는 후속 확장으로 남긴다.

## 검증
- Unity 플레이 모드에서 새 입력/행동/논리 이벤트/연출 큐 연결을 테스트했다.
- `dotnet build Assembly-CSharp.csproj --no-restore` 통과.
- 경고 0개, 오류 0개.

## 다음
- `AlertDetected`를 실제로 처리할 `AlertDetectedPresenter` 또는 적 발각 Presenter를 추가한다.
- `EnemyAlertVisual`은 현재 디버그 즉시 표시로 유지하되, 정식 발각 연출 시점에는 큐 기반 Presenter로 이전한다.
- 공격, 해킹, 검 투척도 같은 `ActionResolutionContext -> PresentationEvent -> Presenter` 기준으로 확장한다.


## 2026-06-21

## 핵심
- `AlertDetectedPresenter`를 추가해 담당 적의 발각 점멸과 연출 큐 완료 처리를 구현했다.
- `AlertDetected` 이벤트 생성 위치를 `GridMoveRiskEvaluator`에서 `EnemyAlertCoordinator`의 실제 상태 전환 지점으로 옮겼다.
- 최초 감지 적과 애드 전파로 새로 `Alerted`가 된 적 모두 발각 연출 이벤트를 받으며, 이미 발각된 적은 중복 연출하지 않는다.
- `EnemyAlertVisual`과 `EnemyContext.AlertVisual` 참조를 제거했다.
- `AlertDetectedPresenter`는 연출 순서와 상태별 색상을 담당하고 `ActorVisualController.ApplyColor()`가 실제 스프라이트 색상을 적용하도록 정리했다.
- 씬과 인스펙터 참조는 임의 수정하지 않았다.

## 검증
- 새 Presenter와 삭제된 스크립트 구성을 반영한 `dotnet build Assembly-CSharp.csproj --no-restore` 통과.
- 경고 0개, 오류 0개.

## 다음
- 각 적의 `AlertDetectedPresenter.VisualController`에 해당 VisualRoot의 `ActorVisualController`를 연결한다.
- 기존 `EnemyAlertVisual` 컴포넌트를 씬에서 제거한다.
- Unity 플레이 모드에서 이동 연출 뒤 최초 감지 적과 전파 적의 점멸 순서를 확인한다.


## 2026-06-23

## 핵심
- `Tset` 씬 YAML 기준으로 현재 인스펙터 연결 상태를 확인했다.
- 적 2개 모두 `AlertDetectedPresenter`가 있고, 각 `VisualController`는 해당 VisualRoot의 `ActorVisualController`에 연결되어 있다.
- 기존 `EnemyAlertVisual` 스크립트와 씬 컴포넌트 잔존 흔적은 확인되지 않았다.
- `PlayerActionFlowController`, `PlayerMoveInputController`, `PlayerContext`, `EnemyContext`, `StageGoalManager`, `StageStateManager`의 핵심 참조가 씬에 연결되어 있음을 확인했다.
- 기존 문서의 다음 작업 중 발각 Presenter 연결과 `EnemyAlertVisual` 제거 항목은 완료된 상태로 정리했다.

## 검증
- 씬/프리팹 YAML에서 Missing Script 패턴을 검색했으며 발견되지 않았다.
- 이번 작업은 문서 갱신과 인스펙터 세팅 확인만 진행했으므로 빌드는 실행하지 않았다.

## 다음
- 다음 큰 작업은 검 투척/해킹/해킹 대상 오브젝트 루프 설계와 1차 구현이다.
- 우선 `IHackable`, `HackableData`, 해킹 가능 오브젝트의 최소 런타임 컴포넌트를 현재 `ActionResolutionContext -> PresentationEvent -> Presenter` 흐름에 맞춰 설계한다.
- 해킹 구현 전에는 공격/검 투척/검 회수와 해킹의 순서 의존성을 먼저 정한다.


## 추가 핵심
- 해킹 1차 통로를 코드로 추가했다.
- `PlayerTurnData`에 `HackRange`, `HackActionPointCost`를 추가했다.
- `HackableObject`를 추가해 `IHackable` 구현 대상과 해킹 완료 상태를 제공하게 했다.
- `HackableRegistry`를 추가해 씬의 활성 해킹 가능 대상 목록과 칸 기준 조회를 담당하게 했다.
- `PlayerHackAction`을 추가해 해킹 선택 상태, AP 소비, 해킹 거리 검사, 대상 주변 8칸 실행 위치 계산, `HackCompletedLogicEvent`, `PresentationEvent.Hack()` 발행을 담당하게 했다.
- `PlayerHackInputController`를 추가해 H 키 해킹 선택, 마우스 칸 클릭, 우클릭/Escape 취소 입력 통로를 열었다.
- `HackPresenter`를 추가해 `PresentationEventType.Hack` 이벤트를 받아 `HackableData.HackDuration`만큼 대기한 뒤 큐 완료 신호를 보내는 임시 연출 통로를 만들었다.
- 실제 검 오브젝트 비행 연출은 아직 만들지 않고, 나중에 검이 도착할 대상 주변 칸을 `PresentationEvent.ExecutionPosition`에 담아두는 방식으로 통로만 열었다.

## 추가 검증
- `dotnet build Assembly-CSharp.csproj --no-restore` 통과.
- 경고 0개, 오류 0개.

## 다음
- Unity 씬에 `HackableRegistry`, `PlayerHackAction`, `PlayerHackInputController`, 테스트용 `HackableObject`, `HackPresenter`를 연결한다.
- `PlayerContext.HackAction`에 `PlayerHackAction`을 연결하고, 테스트 해킹 대상에는 `HackableData`, `GridActor`, 필요 시 `HackPresenter`를 연결한다.
- 플레이 모드에서 H 키 선택, 해킹 대상 클릭, AP 소비, 해킹 로그, `HackPresenter` 큐 완료 흐름을 확인한다.

## 추가 핵심
- 이동/해킹 임시 키 입력을 한 곳으로 모을 `PlayerInputController`를 추가했다.
- `PlayerInputController`는 M 이동 선택, H 해킹 선택, 좌클릭 확정, 우클릭/Escape 취소를 처리한다.
- 이동 선택 중에는 기존 `PlayerGridMoveAction.RefreshPathPreview()`를 호출해 경로 미리보기를 유지한다.
- 해킹 선택 중에는 클릭 칸의 `HackableObject`를 `HackableRegistry`에서 찾아 `PlayerActionFlowController.TryExecuteHack()`으로 전달한다.
- 나중에 Action Map이나 UI 버튼이 붙을 때 `RequestSelectMoveAction()`, `RequestSelectHackAction()`, `RequestCancelSelection()`, `RequestConfirmCurrentPointer()` 같은 공개 요청 메서드에 연결할 수 있게 했다.
- 기존 `PlayerMoveInputController`, `PlayerHackInputController`는 인스펙터 정리 전까지 유지했다.
- `PlayerContext`에 선택적 `PlayerInputController` 참조를 추가했다.

## 추가 검증
- `dotnet build Assembly-CSharp.csproj --no-restore` 통과.
- 경고 0개, 오류 0개.

## 다음
- Unity 씬 인스펙터 정리 단계에서 기존 개별 입력 컨트롤러 대신 `PlayerInputController`를 연결한다.
- `PlayerContext.InputController`, `PlayerContext.HackAction`을 연결하고, 기존 이동/해킹 개별 입력 컴포넌트는 동작 중복을 피하기 위해 비활성화하거나 제거한다.

## 추가 정리
- `PlayerMoveInputController`, `PlayerHackInputController`를 제거했다.
- 입력은 `PlayerInputController`로 통합하는 기준으로 정리했다.
- `Tset` 씬의 기존 `PlayerMoveInputController` 컴포넌트 참조를 제거해 Missing Script가 남지 않게 했다.
- `Assembly-CSharp.csproj`의 기존 개별 입력 컨트롤러 Compile 항목을 제거했다.

## 추가 검증
- `PlayerMoveInputController`, `PlayerHackInputController` 클래스명과 기존 GUID 참조가 남아 있지 않은 것을 검색으로 확인했다.
- `Tset.unity`에서 Missing Script 패턴이 검색되지 않았다.
- `dotnet build Assembly-CSharp.csproj --no-restore` 통과.
- 경고 0개, 오류 0개.

## 다음
- Unity 씬 인스펙터 정리 단계에서 `PlayerInputController`를 플레이어 입력 담당 컴포넌트로 연결한다.
- `PlayerContext.InputController`, `PlayerContext.HackAction`을 연결하고 H/M 입력 중복 없이 통합 입력 흐름을 확인한다.

## 추가 정리
- 입력 구조를 `PlayerInputReader`와 액션별 입력 컨트롤러로 다시 분리했다.
- 기존 통합 `PlayerInputController`와 `PlayerActionSelection`을 제거했다.
- `PlayerInputReader`는 M/H/좌클릭/우클릭/Escape 같은 임시 입력 매핑과 포인터 그리드 좌표 변환만 담당한다.
- `PlayerMoveInputController`는 `PlayerInputReader`의 이동 선택/확정/취소 입력만 읽어 `PlayerGridMoveAction`과 `PlayerActionFlowController`에 요청한다.
- `PlayerHackInputController`는 `PlayerInputReader`의 해킹 선택/확정/취소 입력만 읽어 `PlayerHackAction`과 `PlayerActionFlowController`에 요청한다.
- `PlayerContext`의 입력 참조를 `PlayerInputController`에서 `PlayerInputReader`로 변경했다.

## 추가 검증
- `dotnet build Assembly-CSharp.csproj --no-restore` 통과.
- 경고 0개, 오류 0개.

## 다음
- Unity 씬 인스펙터 정리 단계에서 `PlayerInputReader`, `PlayerMoveInputController`, `PlayerHackInputController`를 연결한다.
- 이후 Action Map을 붙일 때는 `PlayerInputReader` 내부 입력 매핑만 교체한다.

## 추가 정리
- `PlayerMoveInputController`, `PlayerHackInputController`의 직접 `PlayerInputReader` 인스펙터 참조를 제거했다.
- 두 입력 컨트롤러는 이제 `PlayerContext.InputReader`에서 입력 Reader를 꺼내 쓴다.
- `PlayerContext.HasValidReference()`에서 `PlayerInputReader` 누락을 필수 참조 오류로 드러내게 했다.
- `Tset` 씬의 이동/해킹 입력 컨트롤러에 남아 있던 구 `inputReader` 직렬화 줄을 제거했다.
- `Tset` 씬 기준 `PlayerContext.InputReader`, `PlayerContext.HackAction`은 연결된 상태로 확인했다.

## 추가 검증
- 직접 `PlayerInputReader` 인스펙터 필드는 `PlayerContext`에만 남아 있음을 검색으로 확인했다.
- `dotnet build Assembly-CSharp.csproj --no-restore` 통과.
- 경고 0개, 오류 0개.
## 2026-06-24

## 핵심
- 해킹 1차 통로와 입력 구조를 오늘 기준으로 정리했다.
- `HackableRegistry`를 `EnemyRegistry`로 합칠지 검토했지만, 해킹 대상은 적뿐 아니라 장치/문/기믹까지 확장될 수 있으므로 일단 별도 등록소를 유지하기로 했다.
- `PlayerInputController` 통합 구조는 파일이 커질 위험이 있어 제거하고, `PlayerInputReader`와 액션별 입력 컨트롤러 구조로 다시 정리했다.
- `PlayerInputReader`는 PLC I/O처럼 입력값과 포인터 그리드 좌표 변환만 담당한다.
- `PlayerMoveInputController`와 `PlayerHackInputController`는 각각 이동/해킹 입력 해석과 행동 요청만 담당한다.
- 두 입력 컨트롤러의 직접 `PlayerInputReader` 인스펙터 참조를 제거하고, `PlayerContext.InputReader`에서 꺼내 쓰게 했다.
- `PlayerContext`를 플레이어 참조 주머니로 유지하고 `InputReader`, `HackAction` 참조를 포함하게 했다.
- `Tset` 씬 기준 `PlayerContext.InputReader`, `PlayerContext.HackAction` 연결과 Missing Script 없음 상태를 확인했다.

## 검증
- `dotnet build Assembly-CSharp.csproj --no-restore` 통과.
- 경고 0개, 오류 0개.
- `PlayerInputController`, `PlayerActionSelection` 잔존 참조가 없는 것을 검색으로 확인했다.
- `PlayerMoveInputController`, `PlayerHackInputController`의 직접 `PlayerInputReader` 직렬화 참조가 제거된 것을 확인했다.

## 다음
- Unity 플레이 모드에서 M 이동 선택, H 해킹 선택, 좌클릭 확정, 우클릭/Escape 취소 입력을 실제로 확인한다.
- 해킹 실행 시 AP 소비, 맨해튼 거리 판정, 대상 주변 8칸 실행 위치 계산, `HackPresenter` 큐 완료 로그를 확인한다.
- 입력 확인 후 해킹 대상별 실제 효과와 검 비행 연출 통로를 단계적으로 붙인다.


## 2026-06-25

## 핵심
- 검 투척/회수 행동의 1차 뼈대를 추가했다.
- 검은 GridActor로 점유 등록하지 않고 PlayerSwordState가 현재 기준 칸만 관리한다.
- 검 투척은 플레이어 위치가 아니라 PlayerSwordState.CurrentPosition 기준 사거리로 판정한다.
- 검 투척 목표는 GridManager.CanEnter()로 막지 않고 보드 안 칸 여부와 사거리만 검사한다.
- 검 투척과 회수는 각각 AP를 소비하고 SwordThrow/SwordRecall 연출 이벤트를 큐에 추가한다.
- 해킹 완료 시 PlayerSwordState가 HackCompletedLogicEvent를 받아 ExecutionPosition으로 검 기준 칸을 갱신한다.
- 임시 입력은 T 키 검 투척 선택, R 키 검 회수 실행으로 추가했다.

## 검증
- dotnet build Assembly-CSharp.csproj --no-restore 통과.
- 경고 0개, 오류 0개.

## 다음
- Tset 씬에 PlayerSwordState, PlayerSwordThrowAction, PlayerSwordRecallAction, 입력 컨트롤러, SwordActionPresenter를 연결한다.
- 플레이 모드에서 T 검 투척, R 검 회수, 해킹 후 검 기준 위치 갱신 흐름을 확인한다.
- 이후 검 직선 이펙트와 실제 공격/해킹 연출을 Presenter 기준으로 확장한다.


## 2026-06-25 추가 정리

## 핵심
- 입력 컨트롤러의 PlayerActionFlowController 인스펙터 참조를 제거했다.
- PlayerMoveInputController, PlayerHackInputController, PlayerSwordThrowInputController, PlayerSwordRecallInputController는 실행 시점에 PlayerActionFlowController.Instance를 조회해 행동 실행을 요청한다.
- 씬 YAML에 남아 있던 구 actionFlowController 직렬화 줄을 제거했다.
- 회수 상태의 검 위치 기준을 보정했다. PlayerSwordState.CurrentPosition은 검이 회수된 상태라면 항상 플레이어 현재 칸을 반환한다.
- 검을 이미 소유 중인 상태에서는 PlayerSwordRecallAction이 회수 행동을 막는다.

## 검증
- dotnet build Assembly-CSharp.csproj --no-restore 통과.
- 경고 0개, 오류 0개.
- actionFlowController 구 직렬화 필드가 씬/프리팹에 남아 있지 않은 것을 검색으로 확인했다.
- Tset.unity에서 Missing Script 패턴이 검색되지 않았다.

## 다음
- Tset 씬에 새 검 행동 컴포넌트를 연결하고 PlayerContext의 SwordState, SwordThrowAction, SwordRecallAction 참조를 연결한다.
- 플레이 모드에서 T 검 투척, R 검 회수, 회수 상태 이동 후 투척 기준 칸, 이미 소유 중 회수 차단, 해킹 후 검 기준 위치 갱신을 확인한다.


## 2026-06-25 내일 작업 메모

## 다음 작업 후보
- 해킹 사거리를 플레이어 기준이 아니라 검 위치 기준으로 판정하도록 바꾼다.
- 검 투척을 이용한 공격 행동 뼈대를 만든다.
- 근접 공격 행동 뼈대를 만든다.


## 2026-06-25 인터페이스 정리

## 핵심
- ActionLogicEventBus.cs 안에 함께 있던 IActionLogicEvent, IActionLogicEventHandler를 각각 별도 파일로 분리했다.
- 앞으로 새 인터페이스를 만들 때는 다른 클래스 파일 안에 숨기지 않고 인터페이스명과 같은 독립 .cs 파일로 만든다.

## 검증
- dotnet build Assembly-CSharp.csproj --no-restore 통과.
- 경고 0개, 오류 0개.
- public interface 검색 기준 모든 인터페이스가 독립 파일에 있음을 확인했다.


## 2026-06-28

## 핵심
- 해킹 사거리 기준을 플레이어 위치에서 `PlayerSwordState.CurrentPosition` 기준으로 변경했다.
- 기존 검 투척 행동에 목표 칸 `IDamageable` 피해 적용을 추가했다.
- 목표 칸에 피해 가능 대상이 없으면 기존처럼 검만 이동한다.
- `DamageAppliedLogicEvent`를 추가해 피해 적용 시도 결과를 논리 이벤트로 남기게 했다.
- `ActorHealth`를 추가해 `IDamageable` 기반 HP 감소와 전투불능 로그를 담당하게 했다.
- 근접 공격 1차 행동 `PlayerMeleeAttackAction`과 입력 컨트롤러를 추가했다.
- 근접 공격은 플레이어 주변 8방향 1칸 대상만 공격한다.
- 근접 공격은 검 보유 중 피해량과 검 없음 피해량을 `PlayerTurnData`에서 분리해 읽는다.
- 근접 공격 연출 이벤트를 `MeleeAttackWithSword`, `MeleeAttackUnarmed`로 분리했다.
- 임시 입력 기준 근접 공격 선택 키는 F다.

## 검증
- `dotnet build Assembly-CSharp.csproj --no-restore` 통과.
- 경고 0개, 오류 0개.

## 다음
- Tset 씬에 `ActorHealth`, `PlayerMeleeAttackAction`, `PlayerMeleeAttackInputController` 참조를 연결한다.
- 플레이 모드에서 T 검 투척 피해, F 근접 공격, 검 보유/미보유 근접 피해량과 연출 이벤트 분기를 확인한다.
- 이후 실제 피격 연출과 사망/제거 처리를 `ActorHealth`와 Presenter 기준으로 확장한다.

## 2026-06-28 테스트 확인

## 핵심
- Unity 플레이 모드에서 공격 / 피해 1차 통로를 확인했다.
- T 검 투척 시 목표 칸에 `IDamageable` 대상이 있으면 피해가 적용되고, 없으면 검만 이동하는 흐름을 확인했다.
- F 근접 공격 입력과 8방향 인접 대상 공격 흐름을 확인했다.
- 검 보유 중 근접 공격과 검 미보유 중 근접 공격의 피해량 분기와 연출 이벤트 분기를 확인했다.
- `ActorHealth` 기반 HP 감소와 전투불능 로그 흐름을 확인했다.
- 해킹 사거리가 `PlayerSwordState.CurrentPosition` 기준으로 동작하는 흐름을 확인했다.

## 검증
- Unity 플레이 모드 테스트 완료.

## 다음
- 실제 피격 연출과 사망/제거 처리를 `ActorHealth`와 Presenter 기준으로 확장한다.
- 검 투척/근접 공격의 임시 로그 연출을 실제 검 표시, 타격 이펙트, 애니메이션 타이밍으로 교체한다.
- 공격으로 적 경계 상태가 바뀌는 규칙을 정한다.

## 2026-06-28 경계 반응 엄폐 이동

## 핵심
- 경계 상태 전환과 경계 반응 행동을 분리하는 기준으로 정리했다.
- `EnemyAlertCoordinator`가 시야 발각뿐 아니라 `DamageAppliedLogicEvent`도 처리하게 했다.
- 피해가 실제 적용된 적은 경계 상태로 전환되고, 해당 적 기준 `AlertSpreadRange` 안의 적에게 애드가 전파된다.
- `EnemyAlertedLogicEvent`에 경계 원인 `EnemyAlertReason`과 `KnownPlayerPosition`을 추가했다.
- `EnemyData`에 `AlertReactionMoveRange`를 추가해 경계 반응 이동 가능 거리를 데이터로 조절하게 했다.
- `EnemyAlertReactionCoordinator`를 추가했다.
- `EnemyAlertReactionCoordinator`는 `EnemyAlertedLogicEvent`를 받아 이동 가능 범위 안의 벽 인접 칸을 점수식으로 고른다.
- 점수식은 플레이어 방향 쪽 벽 여부, 플레이어와의 거리, 이동 거리를 기준으로 계산한다.
- 엄폐 후보가 있으면 적 논리 위치를 경로대로 이동시키고, 각 1칸 이동을 `PresentationEvent.EnemyReactionMove`로 큐에 추가한다.
- 기존 `GridActorMovePresenter`의 `EnemyReactionMove` 처리 기능을 재사용한다.

## 검증
- `dotnet build Assembly-CSharp.csproj --no-restore` 통과.
- 경고 0개, 오류 0개.

## 다음
- Tset 씬에 `EnemyAlertReactionCoordinator`를 배치하고 경계 반응 이동을 플레이 모드에서 확인한다.
- 각 적 `EnemyData.AlertReactionMoveRange` 값을 테스트 기준에 맞게 조정한다.
- 벽 인접 엄폐 후보 점수식을 실제 맵 배치 기준으로 다듬는다.

## 2026-06-30 경계 반응 이동 구조 정리

## 핵심
- 경계 반응 엄폐 이동을 현재 코드 기준으로 플레이 모드 테스트 완료했다.
- `EnemyReactionMove` 연출 이벤트는 각 적의 `GridActorMovePresenter`가 처리해야 하며, 적 ActorPresentation에 이동 Presenter 연결이 필요하다는 점을 확인했다.
- 엄폐 후보 선택은 단순 우선순위 방식보다 점수제 유지가 더 적합하다고 판단했다.
- 점수 항목을 정면 노출 패널티, 플레이어 접근 감점, 실제 차단 엄폐 보너스, 인접 벽 수 보너스, 현재 위치 대비 엄폐 품질 개선 보너스, 이동 거리 패널티 기준으로 정리했다.
- 전술 위치 평가와 경로 선택 책임을 `EnemyAlertReactionCoordinator`에서 분리했다.
- `EnemyTacticalPositionScorer`를 추가해 엄폐 품질과 후보 점수 산정을 담당하게 했다.
- `EnemyTacticalMovePlanner`를 추가해 도달 가능한 후보 탐색, 최고 점수 후보 선택, 목표까지 경로 계산을 담당하게 했다.
- `EnemyAlertReactionCoordinator`는 적 오브젝트별 컴포넌트로 유지한다.
- `EnemyAlertReactionCoordinator`는 자기 `EnemyContext`에 해당하는 `EnemyAlertedLogicEvent`만 처리한다.
- `EnemyAlertReactionCoordinator`는 `hasReactedToAlert`로 현재 Alerted 진입에 대한 수동 반응 이동을 1회만 처리한다.
- 경계 반응 이동은 상태 변화에 따른 수동 반응이며, 적 턴 AI가 공격 위치를 잡는 능동 이동과 분리해서 본다.

## 검증
- 현재 코드로 Unity 플레이 모드 테스트 완료.
- `dotnet build Assembly-CSharp.csproj --no-restore` 통과.
- 경고 0개, 오류 0개.

## 다음
- 각 적의 `EnemyAlertReactionCoordinator.EnemyContext` 인스펙터 참조를 자기 `EnemyContext`로 연결해 둔다.
- 후보별 점수 로그를 보며 점수 가중치를 실제 맵 배치 기준으로 조정한다.
- 이후 적 턴 AI에서 `EnemyTacticalMovePlanner`와 `EnemyTacticalPositionScorer`를 재사용해 공격 가능하면서 엄폐가 좋은 위치를 고르는 흐름으로 확장한다.

## 2026-06-30 논리 이벤트 CanHandle 필터 정리

## 핵심
- `ActionLogicEventBus`는 `CanHandle()`이 true인 모든 `IActionLogicEventHandler`에 이벤트를 전달한다.
- 개별 대상이 정해진 핸들러는 `CanHandle()` 단계에서 이벤트 타입뿐 아니라 대상 참조까지 검사하도록 정리했다.
- `EnemyAlertReactionCoordinator`는 `EnemyAlertedLogicEvent` 중 이벤트의 `Enemy`가 자기 `EnemyContext`인 경우에만 처리 가능하다고 응답한다.
- `GridMoveRiskEvaluator`는 플레이어 `GridActor`의 `MoveStepEnteredLogicEvent`, `MoveCompletedLogicEvent`만 처리 가능하다고 응답한다.
- `StageGoalManager`는 플레이어 `GridActor`의 `MoveCompletedLogicEvent`만 처리 가능하다고 응답한다.
- `PlayerSwordState`는 플레이어 `GridActor`의 `HackCompletedLogicEvent`만 처리 가능하다고 응답한다.
- `Handle()` 내부의 대상 검사는 직접 호출이나 추후 구조 변경에 대한 2차 방어로 유지한다.
- `EnemyAlertCoordinator`와 `StageStateManager`는 씬/스테이지 단위 핸들러라 이벤트 타입 기준 `CanHandle()`을 유지한다.

## 검증
- `dotnet build Assembly-CSharp.csproj --no-restore` 통과.
- 경고 0개, 오류 0개.

## 2026-07-02 적 턴 AI 1차

## 핵심
- 적 턴에 경계 상태 적이 2AP 안에서 원거리 공격과 엄폐 이동을 수행하는 1차 AI 구조를 추가했다.
- `EnemyTurnCoordinator`를 추가해 적 턴 시작 시 `EnemyRegistry`의 적을 순서대로 실행하고, 각 적 연출 큐가 끝난 뒤 다음 적으로 넘어가게 했다.
- `EnemyTurnAgent`를 추가해 적 하나의 행동 우선순위를 처리하게 했다.
- 적은 살아 있고 경계 상태일 때만 행동한다. `ActorHealth.IsDead`가 true인 적은 적 턴 행동을 생략한다.
- 현재 위치에서 공격 사거리 안이면 공격 후 남은 AP로 최고 엄폐 위치를 찾고, 현재 위치가 최고면 움직이지 않는다.
- 공격 사거리 밖이면 공격 가능한 엄폐 위치 중 점수가 가장 높은 곳으로 이동한 뒤 공격한다.
- 공격 가능한 엄폐 위치가 없으면 공격하지 않고 이동 가능 범위 안의 최고 엄폐 위치로 이동 후 대기한다.
- `EnemyAttackAction`을 추가해 적 원거리 공격을 기존 `ApplyDamageLogicEvent -> DamageResolutionCoordinator -> CombatAction` 피해 처리 흐름에 연결했다.
- `EnemyTacticalMovePlanner`를 확장해 현재 위치 포함 여부와 후보 필터를 받아 적 턴 AI에서도 기존 엄폐 점수 계산을 재사용하게 했다.
- `EnemyData`에 적 턴 AP, 턴 이동 거리, 원거리 공격 사거리, 원거리 공격 피해량을 추가했다.
- `EnemyContext`에 `EnemyTurnAgent`, `EnemyAttackAction` 참조를 추가했다.
- `EnemyAttackPresenter`를 추가해 적 원거리 공격 `CombatAction`을 임시 로그/대기 연출로 처리할 수 있게 했다.

## 검증
- `dotnet build Assembly-CSharp.csproj --no-restore` 통과.
- 경고 0개, 오류 0개.

## 다음
- Tset 씬에 `EnemyTurnCoordinator`를 배치하고 `PlayerContext`, `TurnManager`, `ActionPresentationQueue`를 연결한다.
- 각 적 `EnemyContext`에 `EnemyTurnAgent`, `EnemyAttackAction`을 연결한다.
- 각 적에 `EnemyAttackPresenter`를 연결하거나 임시로 큐 자동 완료 로그를 확인한다.
- 플레이 모드에서 경계 상태 적의 공격 가능 시 공격 후 재배치, 공격 불가 시 공격 가능한 엄폐 위치 이동 후 공격, 공격 가능한 엄폐 위치 없음 시 최고 엄폐 위치 이동 후 대기 흐름을 확인한다.

## 2026-07-04 적 AP 컴포넌트 분리

## 핵심
- 적 턴 AI에서 지역 변수로 관리하던 AP를 `EnemyActionPoint` 전용 컴포넌트로 분리했다.
- `EnemyActionPoint`는 `EnemyData.TurnActionPoint`를 기준으로 `Current`, `Max`, `CanSpend()`, `TrySpend()`, `RefillForTurn()`을 제공한다.
- `EnemyContext`에 `EnemyActionPoint ActionPoint` 참조를 추가했다.
- `EnemyTurnCoordinator`는 각 적 행동 실행 직전에 `EnemyContext.ActionPoint.RefillForTurn()`을 호출한다.
- `EnemyTurnAgent`는 이동/공격 전 `CanSpend(1)`을 확인하고, 행동 성공 후 `TrySpend(1)`로 AP를 소비한다.
- 적 이동 1회와 원거리 공격 1회는 각각 AP 1을 소비한다.

## 검증
- `dotnet build Assembly-CSharp.csproj --no-restore` 통과.
- 경고 0개, 오류 0개.

## 다음
- Tset 씬의 각 적에 `EnemyActionPoint`를 추가하고 `EnemyContext.ActionPoint`에 연결한다.
- 기존 `EnemyTurnAgent`, `EnemyAttackAction` 참조도 함께 연결해 적 턴 AI를 플레이 모드에서 확인한다.

## 2026-07-04 적 턴 AI 플레이 모드 테스트 확인

## 핵심
- 현재 코드 기준으로 적 턴 AI와 `EnemyActionPoint` 분리 구조를 플레이 모드에서 테스트 완료했다.
- `EnemyTurnCoordinator`가 적 턴 시작 시 활성 적을 순서대로 실행하는 흐름을 확인했다.
- 각 적의 `EnemyActionPoint`가 턴 행동 시작 시 보충되고, 이동/공격 성공 시 AP 1씩 소비되는 흐름을 확인했다.
- 경계 상태이고 전투불능이 아닌 적만 행동하는 기준을 확인했다.
- 공격 사거리 안에서는 원거리 공격 후 엄폐 위치 재평가를 수행하는 흐름을 확인했다.
- 공격 사거리 밖에서는 공격 가능한 엄폐 위치로 이동한 뒤 공격하는 흐름을 확인했다.
- 공격 가능한 엄폐 위치가 없을 때는 공격하지 않고 최고 엄폐 위치로 이동 후 대기하는 기준을 확인했다.

## 검증
- Unity 플레이 모드 테스트 완료.
- `EnemyDataTest.asset`, `Tset.unity` 기준 인스펙터 연결과 수치 조정이 반영된 상태다.

## 다음
- 적 원거리 공격의 임시 로그 연출을 실제 투사체/피격 연출로 교체한다.
- `ActorDiedLogicEvent` 이후 사망 제거/비활성화/점유 해제 타이밍을 정한다.
- 적 유형별 자리 고수, 엄폐 우선, 즉시 공격 같은 정책 분기를 `EnemyData` 또는 별도 정책 컴포넌트로 확장할지 검토한다.

## 2026-07-04 논리 사망 처리 1차

## 핵심
- `ActorDiedLogicEvent`를 처리하는 기본 논리 핸들러 `ActorDeathCoordinator`를 추가했다.
- 사망한 액터는 GameObject를 제거하거나 비활성화하지 않고, `GridActor.ReleaseCellOccupation()`으로 현재 칸 점유만 해제한다.
- `GridActor.ReleaseCellOccupation()`은 런타임 점유 등록을 해제하고 이후 해당 액터가 칸 진입을 막지 않도록 `occupyCell`을 false로 바꾼다.
- 죽은 적은 `EnemyGridSight.CanDetect()`에서 감지자로 동작하지 않게 했다.
- 죽은 적은 `EnemyAlertCoordinator`의 애드 전파 수신 대상에서 제외했다.
- 사망 애니메이션, 시체 정렬, 플레이어와 시체가 같은 칸에 있을 때의 표시 우선순위는 아트/연출 작업 때 처리하기로 메모만 남긴다.

## 검증
- `dotnet build Assembly-CSharp.csproj --no-restore` 통과.
- 경고 0개, 오류 0개.

## 다음
- Unity 플레이 모드에서 적 사망 후 해당 칸으로 플레이어가 진입 가능한지 확인한다.
- 죽은 적이 이후 이동 위험 평가에서 더 이상 감지자로 잡히지 않는지 확인한다.
- 사망 연출 아트가 준비되면 `CombatAction`의 `DamageResult.KilledByThisDamage` 기준으로 사망 애니메이션과 시체 정렬을 연결한다.

## 2026-07-04 논리 사망 처리 플레이 모드 테스트 확인

## 핵심
- Unity 플레이 모드에서 논리 사망 처리 1차 구조를 테스트 완료했다.
- 적 사망 시 GameObject는 유지되고, `GridActor.ReleaseCellOccupation()`으로 해당 칸 점유만 해제되는 흐름을 확인했다.
- 사망한 적이 있던 칸으로 플레이어가 진입 가능한 것을 확인했다.
- 죽은 적이 이후 시야 감지자로 동작하지 않는 기준을 확인했다.
- 별도 디버그 표시 보강은 현재 필요하지 않다고 판단했다.
- 사망 애니메이션, 시체 정렬, 플레이어와 시체가 같은 칸에 있을 때의 표시 우선순위는 아트가 준비된 뒤 연출 작업과 함께 처리하기로 했다.

## 검증
- Unity 플레이 모드 테스트 완료.

## 다음
- 사망 연출 아트가 준비되면 `CombatAction`의 `DamageResult.KilledByThisDamage` 기준으로 사망 애니메이션과 시체 정렬을 연결한다.
- 그 전까지는 다음 게임플레이 작업 후보를 별도로 선정한다.

## 2026-07-08 플레이어 총 공격 1차

## 핵심
- 플레이어 원거리 총 공격 행동 1차 통로를 추가했다.
- 총 공격은 플레이어 현재 칸 기준 맨해튼 사거리 안의 `IDamageable` 대상을 공격한다.
- 총 공격은 기존 피해 처리 흐름인 `ApplyDamageLogicEvent -> DamageResolutionCoordinator -> CombatAction`을 재사용한다.
- 총 공격은 AP 비용과 총알 1발을 함께 요구하며, 실행 시 AP와 총알을 각각 소비한다.
- `PlayerTurnData`에 총 공격 AP 비용, 사거리, 피해량, 최대 총알 수를 추가했다.
- 현재 총알 수는 `PlayerTurnData`가 아니라 `PlayerGunAmmo` 런타임 컴포넌트가 보관하도록 분리했다.
- `PlayerContext`, `PlayerInputReader`, `PlayerActionFlowController`에 총 공격 선택/실행 연결점을 추가했다.
- 임시 디버그 입력 기준 총 공격 선택 키는 G다.
- 디버그 오버레이에 총알 수와 총 공격 선택 상태를 표시하게 했다.

## 검증
- `dotnet build Assembly-CSharp.csproj --no-restore` 통과.
- 경고 0개, 오류 0개.

## 다음
- Tset 씬의 플레이어에 `PlayerGunAmmo`, `PlayerGunAttackAction`, `PlayerGunAttackInputController`를 추가하고 `PlayerContext` 참조를 연결한다.
- `PlayerTurnData` 에셋에서 총 공격 사거리, 피해량, 최대 총알 수를 테스트 기준으로 조정한다.
- 플레이 모드에서 G 총 공격 선택, 좌클릭 공격, AP/총알 소비, 피해/사망/애드 전파 흐름을 확인한다.

## 2026-07-09 플레이어 공격 Presenter 책임 분리

## 핵심
- 기존 `SwordActionPresenter`가 검 투척/회수뿐 아니라 근접 공격과 플레이어 총 공격까지 처리하던 책임을 기능별로 분리했다.
- `SwordActionPresenter`는 검 투척, 검 회수, 검 투척 피해의 `CombatAction`만 처리한다.
- `PlayerMeleeAttackPresenter`를 추가해 `MeleeWithSword`, `MeleeUnarmed` 근접 공격 연출을 처리하게 했다.
- `PlayerGunAttackPresenter`를 추가해 `PlayerGun` 총 공격 연출을 처리하게 했다.
- 각 Presenter가 `PresentationEventType`과 `AttackPresentationKind`, `ownerActor`를 함께 검사해 하나의 `CombatAction`을 중복 처리하지 않게 했다.
- 현재 사용하지 않는 근접 공격 전용 `PresentationEventType`은 기존 호환성을 위해 `PlayerMeleeAttackPresenter`가 계속 처리한다.
- 씬과 프리팹 인스펙터 연결은 변경하지 않았다.

## 검증
- `dotnet build Assembly-CSharp.csproj --no-restore` 통과.
- 경고 0개, 오류 0개.

## 다음
- `Tset` 씬의 기존 `SwordActionPresenter`에는 `ownerActor` 연결을 유지한다.
- 플레이어 연출 오브젝트에 `PlayerMeleeAttackPresenter`, `PlayerGunAttackPresenter`를 추가하고 각각 플레이어 `GridActor`를 `ownerActor`로 연결한다.
- 플레이 모드에서 검 투척/회수, 검 보유/미보유 근접 공격, 총 공격이 각각 한 Presenter에서만 처리되는지 확인한다.

## 2026-07-09 다중 전술 유닛 기반 전환

## 핵심
- `UnitFaction`, `UnitControlType`, `UnitAbilityType`을 추가해 진영, 조작 주체, 필수 행동 능력을 분리했다.
- `PlayerContext`를 `TacticalUnitContext`, `PlayerTurnData`를 `ControllableUnitData`, `PlayerActionFlowController`를 `PlayerUnitActionFlowController`로 변경했다.
- 기존 스크립트와 데이터 에셋 GUID는 유지했고 `turnData` 필드는 `FormerlySerializedAs`로 기존 참조를 보존했다.
- `TacticalUnitContext`가 데이터상 필수 능력과 실제 행동 컴포넌트 구성을 검사하게 했다.
- `ITacticalUnit`, `TacticalUnitRegistry`를 추가해 플레이어와 적을 진영 기준으로 조회할 통로를 열었다.
- `PlayerInputReader`를 씬 단일 입력 인스턴스로 변경했다.
- `PlayerUnitControlManager`를 추가해 클릭 선택, AP 0 유닛 선택 차단, 행동 모드 전체 취소, AP 소진 시 다음 유닛 자동 전환을 처리하게 했다.
- 기존 행동별 입력 컨트롤러는 이후 정리 작업에서 제거했고 `PlayerUnitInputController`가 현재 선택 유닛의 입력을 통합 처리한다.
- 능력이 없는 유닛의 행동 선택은 정상적인 사용 불가 안내로 처리하고 데이터상 필수 능력 누락은 구성 오류로 처리하게 구분했다.
- 검 없는 해커는 자기 위치 기준으로 해킹하고 검 없는 근접 유닛은 맨손 공격을 사용하도록 기존 행동을 확장했다.
- `EnemyTargetSelector`를 추가해 경계 반응과 적 턴 AI가 가장 가까운 살아 있는 플레이어 진영 유닛을 기준으로 행동하게 했다.
- `StageGoalManager`를 단일 플레이어 참조에서 조작 가능한 플레이어 진영 유닛 전체 기준으로 변경했다.
- 선택 시각 표시는 이번 작업에서 제외했다.
- 유진 사망 게임 오버는 `ControllableUnitData.DefeatOnDeath` 데이터 자리만 추가하고 실제 흐름은 연결하지 않았다.

## 검증
- `dotnet build Assembly-CSharp.csproj --no-restore` 통과.
- 경고 0개, 오류 0개.

## 인스펙터 연결 필요
- 씬에 `TacticalUnitRegistry`, `PlayerUnitControlManager`, `PlayerUnitInputController`를 각각 하나만 배치한다.
- `PlayerUnitActionFlowController`에는 `ActionPresentationQueue` 참조를 유지한다.
- 각 `TacticalUnitContext`에 `ControllableUnitData`, `GridActor`, `ActorHealth`, `ActionPoint`와 데이터 능력표에 맞는 행동 컴포넌트를 연결한다.
- 각 `EnemyContext`에 기존 `ActorHealth`를 새 Health 필드로 연결한다.
- 기존 행동별 입력 컨트롤러는 코드에서 제거했고 `PlayerUnitInputController` 하나만 씬 입력을 처리한다.

## 다음
- Unity가 스크립트 이름 변경과 기존 GUID를 정상 반영하는지 확인한다.
- 현재 유진 데이터의 필수 능력을 Move/Gun/Hack/Sword/Melee로 설정하고 Context 구성을 검증한다.
- Move/Gun 아군 데이터와 유닛을 추가해 클릭 선택, AP별 자동 전환, 비보유 행동 차단을 플레이 모드에서 확인한다.
- 적 발각 반응과 적 턴 공격 표적이 가장 가까운 플레이어 진영 유닛으로 바뀌는지 확인한다.

## 2026-07-10 TurnAction 명칭/폴더 정리

## 핵심
- 코드에서 쓰는 전술 유닛 데이터 명칭을 `UnitData`로 통일했다.
- 기존 직렬화 참조 보존을 위해 `TacticalUnitContext`의 `[FormerlySerializedAs("turnData")]`만 남겼다.
- 직렬화 보존용으로 남겨 두었던 행동별 입력 컨트롤러 스텁을 삭제했다.
- 입력 처리는 `PlayerUnitInputController` 하나만 담당한다.
- `TurnAction` 폴더를 `Core`, `Grid`, `Input`, `Player` 하위 폴더로 정리했다.
- `.meta`를 함께 이동해 기존 스크립트 GUID는 유지했다.

## 검증
- `dotnet build Assembly-CSharp.csproj --no-restore` 통과.
- 경고 0개, 오류 0개.

## 인스펙터 연결 필요
- 씬에 남아 있는 기존 행동별 입력 컨트롤러 컴포넌트는 Missing Script가 될 수 있으므로 제거한다.
- `PlayerUnitInputController`만 씬 단일 입력 처리 컴포넌트로 둔다.

## 2026-07-10 다중 유닛 테스트 세팅 점검 및 다음 목표 정리

## 핵심
- `Tset` 씬 기준 다중 전술 유닛 테스트 세팅을 점검했다.
- 씬에 `TacticalUnitRegistry`, `PlayerUnitControlManager`, `PlayerUnitInputController`, `PlayerUnitActionFlowController`가 배치된 것을 확인했다.
- `TacticalUnitContext`는 유진과 동료 테스트 유닛에 각각 연결되어 있다.
- 유진 테스트 데이터는 `RequiredAbilities = Move + Gun + Hack + Sword + Melee` 구성이며, 실제 행동 컴포넌트 연결도 해당 조합과 일치한다.
- 동료 테스트 데이터는 현재 `RequiredAbilities = Move + Melee` 구성이다. 일반 총기 동료 테스트가 목적이면 `Move + Gun`으로 데이터와 컴포넌트 구성을 바꿔야 한다.
- `EnemyContext`는 적 2기에 연결되어 있고, 적 데이터, `GridActor`, `ActorHealth`, `EnemyGridSight`, `EnemyAlertState`, `EnemyActionPoint`, `EnemyTurnAgent`, `EnemyAttackAction` 연결을 확인했다.
- 삭제된 기존 행동별 입력 컨트롤러 GUID가 `Tset` 씬과 프리팹에 남아 있지 않은 것을 확인했다.
- `StageGoalManager`가 씬에 2개 남아 있는 것을 확인했다. 현재 구조에서는 스테이지 단위 매니저 1개만 유지하는 것이 맞다.

## 검증
- `dotnet build Assembly-CSharp.csproj --no-restore` 통과.
- 경고 0개, 오류 0개.
- `Tset` 씬에서 삭제된 구 입력 컨트롤러 GUID 잔존 검색 결과 없음.

## 정리 필요
- `StageGoalManager`는 씬 단위 오브젝트 하나에만 유지한다.
- 현재 `StageStateManager`가 참조하는 `StageGoalManager`를 기준으로 남기고, 다른 유닛 오브젝트에 붙은 중복 `StageGoalManager`는 제거한다.
- 동료 테스트 유닛의 역할을 확정한다.
  - 근접 동료면 현재 `Move + Melee` 유지.
  - 총기 동료면 데이터 `RequiredAbilities`를 `Move + Gun`으로 바꾸고 `PlayerGunAmmo`, `PlayerGunAttackAction`을 연결한다.

## 다음 작업 목표
- 현재 다중 유닛/적 AI/행동 논리 위에 실제 아트를 씌워 연출을 만든다.
- 우선순위는 플레이어 조작 유닛 선택, 이동, 총 공격, 근접 공격, 검 투척/회수, 해킹, 적 공격, 피격/사망 연출이다.
- 사망 연출은 기존 논리 기준인 `DamageResult.KilledByThisDamage`와 `ActorDiedLogicEvent` 흐름에 맞춰 연결한다.
- 시체는 `GridActor.ReleaseCellOccupation()` 이후 논리 점유는 해제하되, 화면상 시체 스프라이트/애니메이션은 남기는 방향으로 처리한다.

## 2026-07-10 main 브랜치 기준 전환

## 핵심
- 기존 S2-T 작업 브랜치였던 `turn-based-stealth`의 내용을 로컬 `main` 브랜치에 fast-forward 병합했다.
- 병합 후 로컬 `main`과 `turn-based-stealth`는 같은 커밋 `0fef79e`를 가리킨다.
- `main`에 별도 선행 커밋이 없어서 강제 덮어쓰기나 reset 없이 fast-forward 방식으로 처리했다.
- 이후 S2-T 작업 기준 브랜치는 로컬 기준 `main`으로 전환한다.
- 원격 `origin/main` 반영은 아직 별도 push가 필요하다.

## 검증
- 현재 브랜치가 `main`인 것을 확인했다.
- `git status --short` 기준 작업 트리가 깨끗한 것을 확인했다.
- `main..turn-based-stealth` 차이가 없는 것을 확인했다.

## 다음
- 이후 작업은 `main`에서 이어간다.
- 원격 저장소의 `main`도 같은 상태로 맞춰야 하면 `git push origin main`을 별도로 실행한다.

## 2026-07-13 통합 전투 애니메이션 연출 기반

## 핵심
- `CombatActionPresenter`를 추가해 `CombatAction` 하나에서 공격자와 피격자의 한 프레임 애니메이션을 동시에 전환하고, 공격 종류별 지정 시간 뒤 생존 Actor를 `Idle`로 복귀시키도록 했다.
- 공격 종류는 기존 액션이 `ApplyDamageLogicEvent`에 넣는 `AttackPresentationKind`를 그대로 사용한다.
- 피격자는 `DamageResult.KilledByThisDamage`에 따라 `Hit` 또는 `Death` 상태를 사용하며, 사망자는 전투 연출 종료 뒤 `Idle`로 복귀하지 않는다.
- `ActorPresentationRegistry`, `ActorPresentationBinding`을 추가해 논리 `GridActor`와 화면 `ActorVisualController`를 연출 계층에서 연결하게 했다.
- `CombatPresentationData`를 추가해 공격 종류별 공격자 상태 이름과 동시 연출 유지 시간, 공통 `Hit`/`Death`/`Idle` 상태 이름을 데이터로 관리하게 했다.
- `ActorVisualController`에 마지막 애니메이션 상태 기억, 같은 상태 재시작 방지 옵션, 기본 왼쪽 일러스트 기준 좌우 플립 기능을 추가했다.
- `PlayerGunAttackPresenter`, `PlayerMeleeAttackPresenter`, `EnemyAttackPresenter`를 삭제하고 통합 전투 연출 책임을 `CombatActionPresenter`로 이동했다.
- `SwordActionPresenter`는 피해가 없는 검 투척과 검 회수만 처리하며, 피해가 발생한 검 투척은 기존 `AttackPresentationKind.SwordThrow`를 통해 통합 전투 연출로 처리한다.
- 검 Visual과 검 이동 궤적 이펙트는 이번 작업에서 구현하지 않았고 `FromPosition`, `ToPosition`, `SwordThrow` 공격 종류 통로를 유지했다.
- 한 칸 단위 이동 이벤트에 `Single`, `Start`, `Continue`, `End` 단계를 추가해 연속 이동 중 매 칸 `Move -> Idle` 전환이 반복되지 않게 했다.
- 플레이어 이동, 적 경계 반응 이동, 적 턴 이동이 같은 이동 단계 규칙을 사용한다.
- `AlertDetectedPresenter`에 선택적으로 `Alert` 자세를 재생하고 연출 종료 뒤 `Idle`로 복귀하는 통로를 추가했다.

## 검증
- `dotnet build Assembly-CSharp.csproj --no-restore` 통과.
- 경고 0개, 오류 0개.
- 씬과 데이터 에셋의 인스펙터 연결 및 플레이 모드 연출 확인은 아직 진행하지 않았다.

## 인스펙터 연결 필요
- 씬 단위 오브젝트에 `ActorPresentationRegistry`, `CombatActionPresenter`를 각각 하나씩 배치한다.
- `CombatActionPresenter`에 `ActorPresentationRegistry`와 새 `CombatPresentationData` 에셋을 연결한다.
- 각 캐릭터 연출 오브젝트에 `ActorPresentationBinding`을 추가하고 해당 `GridActor`, `ActorVisualController`를 연결한다.
- 각 `ActorVisualController`에 `SpriteRenderer`, `Animator`를 연결한다.
- `CombatPresentationData`에 `SwordThrow`, `MeleeWithSword`, `MeleeUnarmed`, `PlayerGun`, `EnemyRanged` 항목과 상태 이름, 유지 시간을 설정한다.
- 삭제한 기존 공격 Presenter가 붙어 있던 씬 오브젝트에서 Missing Script 컴포넌트를 제거한다.
- 이동 애니메이션을 사용할 `MovePresentationData`에서 `UseMoveAnimation`, `PlayIdleAnimationOnComplete`와 상태 이름을 설정한다.
- 발각 자세를 사용할 적의 `AlertDetectedPresenter`에서 `Play Alert Animation`을 켜고 상태 이름을 확인한다.

## 다음
- Unity에서 새 스크립트 임포트 후 Missing Script와 필수 참조를 정리한다.
- 유진과 적 한 기 기준으로 이동 방향 플립, 연속 이동, 총/근접/적 공격의 동시 공격·피격 자세, 사망 자세 유지 흐름을 플레이 모드에서 확인한다.
- 검 Visual은 후속 작업에서 한 프레임 위치 이동과 이동 경로 이펙트 방식으로 연결한다.

## 2026-07-14 통합 전투 애니메이션 인스펙터 연결 및 테스트 확인

## 핵심
- `Tset` 씬에 씬 단일 `ActorPresentationRegistry`, `CombatActionPresenter`와 캐릭터별 `ActorPresentationBinding`, `ActorVisualController` 연결을 완료했다.
- `CombatPresentationDataTest`에 `SwordThrow`, `MeleeWithSword`, `MeleeUnarmed`, `PlayerGun`, `EnemyRanged` 상태를 등록하고 각 동시 연출 유지 시간을 `0.5초`로 설정했다.
- 한 프레임 애니메이션을 즉시 교체하기 위해 전투 상태 `CrossFadeDuration`은 `0`으로 설정했다.
- 기존 공격 Presenter가 제거된 씬 구성과 새 통합 Presenter 참조를 점검했다.
- 현재 코드와 인스펙터 구성으로 플레이 모드 테스트를 완료했다.

## 확인 사항
- 공격자와 피격자 애니메이션은 `CombatAction` 한 이벤트에서 동시에 전환한다.
- 생존자는 지정 시간 뒤 `Idle`로 복귀하고, 사망자는 `Death` 상태를 유지한다.
- 기본 왼쪽 방향 일러스트는 마지막 수평 이동 방향 또는 상대 위치 기준으로 좌우 플립한다.
- `MovePresentationDataTest`의 `UseMoveAnimation`, `PlayIdleAnimationOnComplete`는 현재 꺼져 있으므로 이동 애니메이션 상태 전환은 활성화하지 않았다.
- 발각 `Alert` 애니메이션도 현재 테스트 적에서는 끈 상태이며 기존 색상 점멸 연출을 유지한다.

## 문서 정리
- `S2-T 현재 구현 구조.md`의 최신 기준일과 브랜치를 `2026-07-14`, `main`으로 갱신했다.
- 상단 요약과 씬/폴더 구조에서 단일 플레이어 및 구 입력 구조 설명을 다중 전술 유닛 기준으로 교체했다.
- 이미 구현된 공격·검·적 AI 항목이 미구현으로 남아 있던 `현재 한계`와 `다음 작업`을 실제 상태에 맞게 정리했다.
- Notion `프로젝트 S2-T`에는 상세 날짜별 이력을 쌓지 않고 현재 상태와 다음 작업만 갱신한다.

## 다음
- 현재 조작 유닛 선택 표시를 우선 구현한다.
- 검 Visual의 한 프레임 위치 이동과 경로 이펙트, 해킹 연출, 공격 타격 이펙트를 순서대로 연결한다.
- 카메라 줌/컷 연출은 캐릭터 연출과 이펙트가 안정된 뒤 추가한다.

## 2026-07-14 조작 유닛 임시 선택 링 코드 구현

## 핵심
- 플레이어 조작 유닛마다 하나씩 두는 `PlayerUnitSelectionPresenter`를 추가했다.
- 기존 `PlayerUnitControlManager.ActiveUnitChanged` 이벤트를 구독해 담당 `TacticalUnitContext`가 현재 조작 유닛일 때만 선택 링을 표시한다.
- 직접 클릭 선택과 AP 소진에 따른 자동 제어권 전환이 기존 단일 선택 이벤트 통로를 그대로 사용한다.
- 별도 아트 에셋 없이 `LineRenderer`와 런타임 머티리얼로 임시 원형 링을 생성한다.
- 유닛별로 링 위치, 반지름, 굵기, 색상, 선분 수, 정렬 순서를 인스펙터에서 조절할 수 있게 했다.
- 선택 판정과 Presentation 책임을 분리했으며 `PlayerUnitControlManager`, `TacticalUnitContext`, 씬 인스펙터는 수정하지 않았다.
- 이후 실제 아트가 준비되면 선택 이벤트 구독 구조는 유지하고 임시 링 출력만 교체할 수 있다.

## 검증
- 새 스크립트를 Unity 생성 프로젝트에 임시 포함해 `dotnet build Assembly-CSharp.csproj --no-restore`를 실행했다.
- 경고 0개, 오류 0개.
- 임시로 변경한 생성 `.csproj`는 검증 후 원상복구했다.
- Unity 씬 연결과 플레이 모드 표시는 아직 확인하지 않았다.

## 인스펙터 연결 필요
- `Tset` 씬의 각 플레이어 조작 유닛 루트에 `PlayerUnitSelectionPresenter`를 추가한다.
- 각 Presenter의 `Target Unit`에 같은 유닛의 `TacticalUnitContext`를 연결한다.
- 유진과 동료의 스프라이트 크기에 맞춰 `Local Offset`, `Radius`, `Line Width`, `Sorting Order`를 조정한다.

## 다음
- 직접 클릭으로 조작 유닛을 바꿀 때 기존 링이 꺼지고 새 유닛 링만 켜지는지 확인한다.
- AP가 0이 되어 자동 전환될 때 선택 링도 같은 프레임에 이동하는지 확인한다.
- 이동·공격 연출 중 선택 변경 허용 기준과 링 표시가 충돌하지 않는지 확인한다.
- 임시 링 확인 후 검 Visual, 해킹 연출, 공격 타격 이펙트를 순서대로 진행한다.

## 2026-07-14 조작 유닛 선택 링 씬 연결 및 후속 기준

## 핵심
- `Tset` 씬의 두 플레이어 유닛 `VisualRoot`에 `PlayerUnitSelectionPresenter`와 각자의 `TacticalUnitContext`를 연결했다.
- 현재 이동 구조는 `GridActorMovePresenter`와 `ActorPresentationSynchronizer`가 `VisualRoot.position`을 직접 움직이므로 선택 링도 `VisualRoot` 아래에 두는 것으로 확정했다.
- 움직이지 않는 `ActorPresentation` 오브젝트에는 선택 링을 두지 않는다.
- 현재 임시 테스트 값은 두 유닛 모두 `Radius 3`, `Line Width 0.5`, `Sorting Order -1`이다.
- 캐릭터보다 앞에 표시되던 문제는 링의 `Sorting Order`를 캐릭터보다 낮게 두는 기준으로 정리했다.

## 카메라 연출 메모
- 후속 카메라 줌·컷·컷신에서 선택 링을 숨길 필요가 생겨도 유닛별 Presenter를 일일이 끄지 않는다.
- 씬 단위 게임플레이 표시 허용 상태를 추가하고 최종 링 표시 조건을 `현재 선택 유닛 && 게임플레이 표시 허용`으로 확장한다.
- 카메라 연출 시작 시 전역 표시를 끄고 종료 또는 중단 시 복구해 현재 선택 유닛 링만 다시 표시한다.
- 구체적인 전역 컨트롤러와 카메라 연출 이벤트 연결은 카메라 시스템 구현 시 함께 설계한다.

## 확인 상태
- 씬 직렬화 기준 두 `VisualRoot`의 Presenter와 대상 Context 연결을 확인했다.
- 기본 링 표시와 캐릭터 앞뒤 정렬 조정은 진행했다.
- 직접 선택 전환, AP 0 자동 전환, 연출 중 선택 변경에 대한 최종 플레이 모드 확인은 남아 있다.

## 다음
- 선택 링 전환 세부 동작을 플레이 모드에서 마무리 확인한다.
- 검 Visual의 한 프레임 이동과 경로 이펙트 작업으로 진행한다.

## 2026-07-15 현재 구현 문서 최신화

## 핵심
- 실제 코드, `Tset` 씬, Git 상태와 `S2-T 현재 구현 구조.md`를 다시 대조했다.
- 문서 최신 기준일을 `2026-07-15`로 갱신했다.
- 구 명칭인 `PlayerTurnData` 설명을 현재 명칭과 책임에 맞는 `ControllableUnitData` 설명으로 교체했다.
- 상단의 턴/AP, 전술 유닛 Context, 이동 입력, 스테이지 목표 설명을 현재 `EnemyTurnCoordinator`, `TacticalUnitContext`, 통합 입력 계층, 논리 이벤트 버스 구조에 맞게 갱신했다.
- 피해와 해킹 루프가 미구현이라고 남아 있던 과거 설명을 현재 `ApplyDamageLogicEvent -> DamageResolutionCoordinator` 피해 흐름과 `PlayerHackAction -> HackableObject` 해킹 흐름으로 바로잡았다.
- 이미 구현되고 씬에 연결된 조작 유닛 선택 표시를 신규 구현 항목에서 제외했다.
- 선택 링의 남은 플레이 모드 확인 항목과 검·해킹·공격 이펙트 순서를 현재 다음 작업으로 정리했다.
- 과거 날짜별 구조와 작업 기록은 구현 변화 이력으로 유지했다.

## 검증
- 현재 브랜치는 `main`이며 로컬 `main`과 `origin/main`이 같은 커밋인 것을 확인했다.
- Git 작업 트리가 깨끗한 상태에서 문서 정리를 시작했다.
- `Tset` 씬에 주요 씬 단위 매니저가 각각 1개, `EnemyAttackAction`이 2개, `PlayerUnitSelectionPresenter`가 2개 연결된 것을 확인했다.
- `dotnet build S2.slnx --no-restore` 통과, 경고 0개, 오류 0개 상태를 확인했다.

## 다음
- 직접 선택, AP 0 자동 전환, 연출 중 선택 변경에서 선택 링이 정확히 전환되는지 플레이 모드에서 최종 확인한다.
- 확인 후 검 Visual의 한 프레임 이동과 이동 경로 이펙트 작업으로 진행한다.

## 2026-07-17 원본 캐릭터 아트 기본 방향 변경

## 핵심
- 앞으로 사용하는 원본 캐릭터 아트의 기본 방향을 왼쪽에서 오른쪽으로 변경했다.
- `ActorVisualController`의 `artworkFacesLeft`를 `artworkFacesRight`로 변경하고 기본값을 `true`로 설정했다.
- 오른쪽을 바라볼 때 `SpriteRenderer.flipX = false`, 왼쪽을 바라볼 때 `SpriteRenderer.flipX = true`가 되도록 반전 계산을 수정했다.
- `FormerlySerializedAs("artworkFacesLeft")`를 적용해 기존 씬 직렬화 값을 새 필드로 이어받게 했다.
- `EnemyGridSight.FacingDirection` 같은 논리 방향 계산은 변경하지 않았다.

## 검증
- `Tset` 씬의 `ActorVisualController` 네 개가 기존 `artworkFacesLeft: true` 값을 사용하는 것을 확인했다.
- 기존 값은 새 `artworkFacesRight: true` 의미로 이전되므로 별도 인스펙터 수정이 필요하지 않다.

## 다음
- Unity 플레이 모드에서 이동과 공격 시 오른쪽은 원본 방향, 왼쪽은 수평 반전으로 표시되는지 확인한다.

## 2026-07-18 SwordMove 검 Visual 연출 통로

## 핵심
- 검 투척·해킹·회수의 화면 이동을 공통 처리하는 `PresentationEventType.SwordMove`와 `SwordMoveKind`를 추가했다.
- 논리 이벤트인 `SwordThrownLogicEvent`, `SwordRecalledLogicEvent`, `HackCompletedLogicEvent`는 기존 행동 의미를 유지했다.
- 일반 검 투척과 피해 검 투척 모두 `SwordMove`를 먼저 큐에 넣고, 피해가 있으면 `CombatAction`이 뒤이어 재생되도록 정리했다.
- 검 능력 유닛의 해킹은 `SwordMove(Hack) -> Hack` 순서로 검을 실행 칸에 옮긴 뒤 해킹 연출을 재생한다.
- `SwordActionPresenter`가 검 Visual을 직접 제어하도록 구현했다. 별도 SwordAnchor는 만들지 않고 회수 상태에서는 플레이어 `VisualRoot` 자식으로 두며 현재 좌우 방향의 상단 오프셋을 적용한다.
- 투척과 해킹 시 검 Visual을 월드 공간으로 분리하고 실제 현재 검 위치에서 목표 위치까지의 방향으로 회전한 뒤 목표 칸에 즉시 배치한다.
- 경로 VFX는 중간점, 방향, 길이, 스케일 계산까지 구현하고 `pathVfxId = VfxId.None`일 때 재생을 생략하도록 준비했다.
- `CombatActionPresenter`에 전투 연출 시작·종료 알림을 추가했다. 검 소지 근접 공격 시 `SwordActionPresenter`가 플레이어 방향에 따라 검을 기본 70도로 기울이고 전투 종료 뒤 회수 자세로 복구한다.
- `ActorVisualController`는 시작 시 현재 SpriteRenderer 반전값을 `IsFacingRight`에 반영하도록 보정했다.
- 선택 링의 직접 선택·AP 자동 전환·연출 중 선택 변경 테스트는 완료된 상태로 갱신했다.

## 검증
- `dotnet build Assembly-CSharp.csproj --no-restore` 통과.
- 경고 0개, 오류 0개.
- 새 검 Visual과 `SwordActionPresenter` 필수 참조를 연결했다.
- 현재 검 연출 코드와 씬 연결 상태 기준 Unity 플레이 모드 테스트를 완료했다.

## 씬 연결 및 확인
- 검 능력 유닛의 `SwordActionPresenter`에 `PlayerSwordState`, 새 검 Visual, `ActorVisualController`, 씬 단일 `CombatActionPresenter`를 연결했다.
- 새 검 아트 기준으로 회수 위치, 배치 위치, 근접 기울기와 화면상 크기를 조정하고 현재 코드로 테스트를 마쳤다.
- `Tset` 씬의 현재 테스트 값은 `Recalled Offset = (1, 1.5, 0)`, `Deployed Position Offset = (0, 0.1, 0)`, `Melee Tilt Angle = 70`이다.
- 경로 이펙트는 아직 준비되지 않아 `Path Vfx Id`를 `None`으로 유지한다.

## 검 이동 연출 후속 튜닝 결정
- 현재 검 이동 각도와 즉시 위치 변경 동작은 기능 확인에는 사용할 수 있지만 최종 연출 품질 기준으로는 추가 튜닝이 필요하다.
- 방향별 검 각도, 회수·배치 위치, 이동 타이밍과 경로 표현은 실제 플레이 감각을 보며 더 조정해야 한다.
- 현재 단계에서 이 부분을 계속 다듬으면 작업 범위가 과도하게 늘어나므로 일단 현재 상태로 다음 작업에 진행한다.
- 검 경로 VFX와 다른 핵심 연출이 연결된 뒤 전체 화면 흐름을 기준으로 검 이동 연출을 다시 폴리싱한다.

## 다음
- 검 경로 이펙트가 준비되면 `VfxManager` 테이블과 `Path Vfx Id`를 연결한다.
- 해킹 시작/성공/대상 반응 연출과 공격별 타격 이펙트 작업으로 진행한다.
- 핵심 이펙트 연결 이후 검 이동 각도와 움직임 타이밍을 다시 튜닝한다.

## 2026-07-19 키보드 카메라 이동 구현 및 테스트 완료

## 핵심
- `CameraKeyboardMover`를 추가해 WASD와 방향키를 동일한 카메라 이동 입력으로 처리한다.
- 상하좌우 키를 함께 누르면 대각선으로 이동하며, 입력 벡터를 정규화해 대각선 속도가 더 빨라지지 않게 했다.
- 카메라 시작 위치를 기준으로 X/Y축 최대 이동 거리를 각각 제한하고 Z 위치는 유지한다.
- `ActionPresentationQueue`가 연출을 재생하는 동안에는 키보드 카메라 이동을 막는다.
- 기존 `PlayerInputReader`와 전술 행동 입력은 수정하지 않고 카메라 전용 컴포넌트로 분리했다.

## 씬 연결
- `Tset` 씬의 `Main Camera`에 `CameraKeyboardMover`를 연결했다.
- 현재 테스트 값은 `Move Speed = 8`, `Max Move Distance = (10, 10)`, `Lock While Presentation Playing = true`다.

## 검증
- Unity 생성 프로젝트에 새 스크립트를 임시 포함해 `dotnet build Assembly-CSharp.csproj --no-restore`를 실행했다.
- 경고 0개, 오류 0개.
- WASD와 방향키 카메라 이동, 대각선 이동, 시작 위치 기준 이동 제한을 Unity 플레이 모드에서 확인했다.
- 현재 씬 설정값 기준 카메라 테스트를 완료했다.

## 다음
- 키보드 카메라 이동은 현재 설정을 기준으로 유지한다.
- 후속 작업에서 전투 카메라 연출 또는 튜토리얼 1스테이지 완성 작업으로 진행한다.
