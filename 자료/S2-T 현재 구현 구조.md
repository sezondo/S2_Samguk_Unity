# S2-T 현재 구현 구조

- 최신 기준: 2026-08-14
- 기준 브랜치: `main`
- Unity 버전: `6000.0.64f1`
- 기준 테스트 씬: `Assets/Scenes/Test/BootstrapTest.unity`, `Assets/Scenes/Test/LobbyTest.unity`, `Assets/Scenes/Test/StoryTest.unity`, `Assets/Scenes/Test/BattleTest01.unity`, `Assets/Scenes/Test/BattleTest02.unity`

이 문서는 S2-T의 **현재 실제 코드와 씬 구조만** 설명하는 최신 스냅샷이다.
날짜별 작업 과정, 이전 설계, 제거된 구조와 테스트 이력은 `S2-T 작업일지.md`에서 관리한다.

## 1. 프로젝트 현재 방향

S2-T는 사이버 한국 삼국시대 세계관을 사용하는 보드게임식 턴제 잠입 퍼즐·소규모 전술 게임이다.

현재 플레이 가능한 핵심 루프는 다음과 같다.

1. 플레이어 진영의 여러 유닛 중 하나를 선택한다.
2. AP를 사용해 이동, 해킹, 검 투척·회수, 근접 공격, 총 공격을 실행한다.
3. 이동 경로가 적 시야에 들어가면 적이 경계 상태로 전환되고 주변 적에게 애드가 전파된다.
4. 경계 상태가 된 적은 즉시 엄폐 반응 이동을 할 수 있다.
5. 적 턴에는 경계 상태의 적이 플레이어를 공격하거나 유리한 엄폐 위치로 이동한다.
6. 목표 칸에 살아 있는 플레이어 조작 유닛이 도착하면 스테이지가 클리어된다.

현재 전투 플레이는 이동, AP, 잠입, 발각, 적 턴, 전투, 해킹, 목표 달성까지 핵심 뼈대가 연결된 상태로 판단한다.
캠페인 1단계의 데이터·저장·로딩 기반, 2단계의 분기 없는 선형 비주얼 노벨 Story, 3단계의 저장 상태 기반 Lobby UI와 4단계의 Story·Battle·저장 수직 슬라이스까지 구현하고 핵심 한 바퀴 사용자 테스트를 완료했다.
현재는 `BootstrapTest → LobbyTest → 전투 전 Story → BattleTest → 선택적 전투 후 Story → LobbyTest` 한 바퀴가 코드와 Inspector 기준으로 연결된 상태다.

기본 캠페인 흐름은 `로비의 스테이지 선택 → 전투 전 스토리 → 전투 씬 → 선택적 전투 후 스토리 → 진행 저장 → 로비 복귀`다.
챕터와 스테이지는 `1-1`, `1-2` 형식으로 구분하며, 앞 숫자는 챕터를 뜻한다.
장비 시스템은 현재 범위에 넣지 않고, 폭발 가능한 화염병, 해킹 시 아군이 되는 로봇, 동료 NPC처럼 스테이지마다 배치되는 오브젝트와 참여 유닛으로 전술 차이를 만든다.

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

`Assets/Script`의 최상위 기능 영역은 현재 `Campaign`, `Battle`, `Story`, `Lobby`로 정리되어 있다.

### Campaign

- `Assets/Script/Campaign/Data`: `CampaignData`, `StageDefinitionData`.
- `Assets/Script/Campaign/Flow`: Bootstrap, Campaign Context와 전체 흐름 단계.
- `Assets/Script/Campaign/Save`: JSON 저장 데이터, 진행 레코드와 저장 매니저.
- `Assets/Script/Campaign/Loading`: 비동기 씬 전환과 영속 로딩 화면.

### Battle Logic

- `Assets/Script/Battle/Logic/Grid`: 격자 좌표, 셀, 점유와 경로 탐색.
- `Assets/Script/Battle/Logic/Vision`: 플레이어 합산 시야, 누적 탐색 상태, 시야 스냅샷과 명시적 참조 Context.
- `Assets/Script/Battle/Logic/Turn`: 플레이어·적 턴과 AP.
- `Assets/Script/Battle/Logic/Unit`: 공통 전술 유닛, Context, Registry, 제어권과 표적 선택.
- `Assets/Script/Battle/Logic/Unit/Data`: 플레이어 전술 유닛 튜닝 데이터.
- `Assets/Script/Battle/Logic/Unit/Interfaces`: `ITacticalUnit`.
- `Assets/Script/Battle/Logic/Player/Input`: 원시 플레이어 입력.
- `Assets/Script/Battle/Logic/Player/State`: 총알과 검 위치 런타임 상태.
- `Assets/Script/Battle/Logic/Enemy`: 적 Context, 시야, 경계, 엄폐 반응과 적 턴 AI.
- `Assets/Script/Battle/Logic/Enemy/Data`: 적 튜닝 데이터.
- `Assets/Script/Battle/Logic/Combat`: HP, 피해, 사망과 해킹 대상 처리.
- `Assets/Script/Battle/Logic/Combat/Data`: 해킹 대상 튜닝 데이터.
- `Assets/Script/Battle/Logic/Combat/Interfaces`: `IDamageable`, `IHackable`.
- `Assets/Script/Battle/Logic/Stage`: 목표, 스테이지 상태와 동적 보안문 논리.
- `Assets/Script/Battle/Logic/Actions/Core`: 논리 이벤트 버스, 행동 해석 문맥과 논리 이벤트 데이터.
- `Assets/Script/Battle/Logic/Actions/Interfaces`: `IActionLogicEvent`, `IActionLogicEventHandler`.
- `Assets/Script/Battle/Logic/Actions/Grid`: 이동 범위·경로·위험도 계산과 하이라이트.
- `Assets/Script/Battle/Logic/Actions/Input`: 현재 조작 유닛 행동 입력 전달.
- `Assets/Script/Battle/Logic/Actions/Player`: 이동·해킹·검·근접·총 행동과 행동 흐름.

### Battle Presentation

- `Assets/Script/Battle/Presentation`: 연출 큐, Actor 연결과 동기화.
- `Assets/Script/Battle/Presentation/Events`: 연출 이벤트, 타입, 단계와 완료 Handle.
- `Assets/Script/Battle/Presentation/Interfaces`: `IPresentationEventHandler`.
- `Assets/Script/Battle/Presentation/Presenter`: 이동, 발각, 전투, 검, 해킹, 문과 스테이지 결과 Presenter.
- `Assets/Script/Battle/Presentation/Visual`: 실제 Actor 화면 표시 제어.
- `Assets/Script/Battle/Presentation/Data`: 이동·전투 연출 튜닝 데이터.
- `Assets/Script/Battle/Presentation/Vfx`: 공용 전투 VFX 풀.
- `Assets/Script/Battle/Presentation/Debug`: 연출 이벤트와 큐 시험 도구.

### Battle Support

- `Assets/Script/Battle/Flow`: 전투 입장 연출 데이터·실행, 전투 결과의 캠페인 전달.
- `Assets/Script/Battle/Dialogue/Data`: 인게임 말풍선 대사 데이터.
- `Assets/Script/Battle/Dialogue/Runtime`: 인게임 대사 진행과 화자.
- `Assets/Script/Battle/Dialogue/UI`: 말풍선 Presenter와 View.
- `Assets/Script/Battle/Camera`: 전투 테스트 카메라 이동.
- `Assets/Script/Battle/Debug`: 전투 런타임 정보와 그리드 점유 디버그 도구.

### Story

- `Assets/Script/Story/Data`: 선형 명령, 시퀀스, 스탠딩 위치와 페이드 종류.
- `Assets/Script/Story/Runtime`: 명령을 배열 순서대로 실행하는 `StoryRunner`, 재생 상태·완료 사유, 명시적 참조 주머니 `StoryContext`와 캠페인 요청을 연결하는 `StoryCampaignBridge`.
- `Assets/Script/Story/Input`: 좌클릭·Enter·Space 진행과 Escape 건너뛰기 입력 전달.
- `Assets/Script/Story/Presentation`: 대사 타이핑, 배경, 스탠딩, 만화 패널과 페이드 표시.
- `Assets/Script/Story/Debug`: 독립 `StoryTest` 샘플 재생 진입점.

### Lobby

- `Assets/Script/Lobby/Runtime`: `CampaignData`와 저장 상태를 읽는 `LobbyController`, 화면 참조 주머니 `LobbyContext`와 표시 항목 `LobbyStageEntry`.
- `Assets/Script/Lobby/UI`: 동적 스테이지 버튼 목록, 개별 버튼과 선택 스테이지 상세 화면.
- Lobby는 저장 값을 직접 수정하지 않고 `CampaignSaveManager.TryGetStageProgress()`로 상태만 조회한다.
- 선택 확정 시 `CampaignFlowController.TrySelectStage()`로 활성 스테이지를 정한 뒤 `TryStartSelectedStage()`로 Story·Battle 흐름을 시작한다.

인터페이스는 전역 폴더 하나에 모으지 않고 해당 책임 영역의 `Interfaces` 폴더에 둔다.
폴더 이동과 namespace 변경을 한 번에 섞지 않기 위해 현재 클래스 이름과 namespace 없는 코드 구조는 그대로 유지한다.

## 4. 테스트 씬과 캠페인 기반

### BootstrapTest

`BootstrapTest`는 Build Settings에서 첫 번째로 실행되는 초기화 씬이다.

```text
AppRoot
└─ LoadingCanvas
   └─ LoadingBackground
      └─ LoadingProgress
         └─ FillArea
            └─ Fill
```

- `CampaignBootstrap`은 중복 AppRoot를 제거하고 현재 AppRoot를 `DontDestroyOnLoad`로 영속화한다.
- `CampaignContext`는 `CampaignData`, `CampaignFlowController`, `CampaignSaveManager`, `SceneTransitionController` 참조만 모은다.
- `CampaignSaveManager`가 저장 파일을 불러오거나 최초 저장 데이터를 만든 뒤 `CampaignFlowController`가 `LobbyTest` 진입을 요청한다.
- `SceneTransitionController`는 별도 Loading 씬 없이 영속 `LoadingCanvas`를 페이드 인하고 `LoadSceneAsync`로 대상 씬을 준비한 뒤 활성화한다.
- `LoadingScreenPresenter`는 `CanvasGroup` 입력 차단과 진행 바 표시만 담당한다.
- `BootstrapTest`의 카메라와 조명은 로비 진입 때 제거되며 AppRoot와 LoadingCanvas만 다음 씬에 남는다.

현재 캠페인 데이터는 `Assets/Data/Campaign`에 있다.

- `CampaignData`: 로비·공통 Story 씬 이름과 선형 스테이지 순서.
- `Stage_1-1`, `Stage_1-2`: 스테이지 식별 정보, 전투 씬, 전투 전 Story, Bool 기준 후일담 사용 여부와 선택적 전투 후 Story.
- `Stage_1-1`의 전투 씬은 `BattleTest01`, `Stage_1-2`의 전투 씬은 `BattleTest02`다.
- `Stage_1-1`은 전투 전·후 Story가 모두 있고, `Stage_1-2`는 전투 전 Story만 있다.
- 세 캠페인 Story 데이터는 검은 `FadeOverlay`를 먼저 걷어 내도록 `Fade In` 명령으로 시작한다.
- `hasPostBattleStory`가 켜졌는데 후일담 데이터가 없거나, 꺼졌는데 데이터가 연결되면 초기화 검증에서 오류로 중단한다.

진행 상태는 `Locked`, `Available`, `BattleCleared`, `Completed` 네 단계다.
최초 저장은 `1-1`만 `Available`로 만들고 나머지는 잠근다.
전투 승리는 `BattleCleared`, 후일담 완료 또는 후일담이 없는 전투 승리는 `Completed`로 저장하며 다음 스테이지를 `Available`로 개방한다.
기존 저장 뒤에 새 선형 스테이지가 추가되면 기존 진행을 유지하면서 새 레코드만 이어 붙인다.
저장 파일은 `Application.persistentDataPath/campaign-save.json`에 기록한다.

### LobbyTest

`LobbyTest`는 캠페인 데이터와 저장 상태를 실제 선택 화면으로 보여 주는 3단계 Lobby 씬이다.

```text
LobbyRoot
└─ LobbyCanvas
   └─ Background
      ├─ Title / Subtitle
      ├─ StageListPanel
      │  └─ Viewport
      │     └─ Content
      └─ StageDetailPanel
         └─ StartButton

EventSystem
Main Camera
```

- `LobbyRoot`에는 `LobbyContext`와 `LobbyController`가 연결되어 있다.
- `LobbyController`는 `CampaignBootstrap.Instance.Context`를 명시적 영속 진입점으로 사용하며 같은 루트 컴포넌트를 임의 검색하지 않는다.
- `CampaignData.Stages` 순서대로 `LobbyStageButton.prefab`을 동적 생성하므로 새 스테이지를 코드에 하드코딩하지 않는다.
- 최초 저장 기준 `1-1`은 `Available`, `1-2`는 `Locked`로 표시한다.
- `Locked`는 선택할 수 없고, `Available`, `BattleCleared`, `Completed`는 각각 진행 가능, 후일담 대기, 완료 상태와 다음 흐름 설명을 표시한다.
- 목록 선택은 Lobby 상세 화면만 바꾸며, 상세 화면의 확정 버튼이 활성 스테이지 지정과 실제 캠페인 진입을 차례로 요청한다.
- `CampaignFlowController`는 초기화 여부, 현재 `Lobby` 단계, 스테이지 ID 존재 여부와 잠금 상태를 다시 검사한 뒤 `ActiveStage`를 바꾼다.
- `Available`과 `Completed`는 전투 전 Story부터 시작하고, 비정상 종료로 `BattleCleared`에 머문 스테이지는 후일담부터 재개한다.
- `LobbyStageButton.prefab`은 `Assets/Prefab/Lobby`에 있으며 한국어 `gulim SDF` TMP 폰트를 사용한다.
- `LobbyStageListView`는 `Awake()`에서 목록 루트, 버튼 프리팹과 빈 목록 문구 참조를 즉시 검사한다.
- Bootstrap 없이 `LobbyTest`를 직접 실행하면 영속 캠페인 시스템 누락 원인을 한국어 오류로 알리고 Lobby Controller를 비활성화한다.

### StoryTest

`StoryTest`는 독립 Story 재생과 캠페인 전투 전·후 Story 재생에 함께 사용하는 공통 씬이다.

```text
StoryRoot
└─ StoryCanvas
   ├─ Background
   ├─ StandingRoot
   │  ├─ LeftStanding
   │  ├─ CenterStanding
   │  └─ RightStanding
   ├─ DialoguePanel
   ├─ ComicPanelRoot
   └─ FadeOverlay
```

- `StoryRoot`에는 `StoryContext`, `StoryRunner`, `StoryInputReader`, 각 Presenter, `StoryTestLauncher`와 `StoryCampaignBridge`가 연결되어 있다.
- Bootstrap 없이 직접 실행하면 `StoryTestLauncher`가 기존 샘플을 재생하고, 캠페인으로 진입하면 Launcher는 비활성화되고 Bridge가 `ActiveStorySequence`를 스킵 가능 상태로 재생한다.
- `StoryCampaignBridge`는 새 씬의 `Start()`가 캠페인 씬 전환 완료 콜백보다 먼저 실행될 수 있는 순서를 고려해 로딩 종료를 기다린 뒤 `Story` 단계와 활성 Story 요청을 검사한다.
- 정상 완료와 Escape 스킵은 모두 캠페인 다음 흐름으로 전달되며, 재플레이에서도 전투 전·후 Story를 다시 보여 준다.
- 샘플 `StorySequenceDataTest`는 대사, 배경 변경, 스탠딩 표시·숨김·표정·초점, 완성 만화 이미지 표시, 페이드와 시간 대기를 포함한 27개 명령을 분기 없이 순서대로 실행한다.
- 대사 타이핑 중 진행 입력은 현재 문장을 즉시 완성하고, 완성 뒤 입력은 다음 명령으로 이동한다.
- 만화 연출은 외부에서 컷 배치까지 완성한 Sprite 한 장을 전체 화면 Image에 그대로 표시한다. Story 코드는 컷 위치와 분할 레이아웃을 계산하지 않는다.
- `StoryRunner.Play()` 호출자가 건너뛰기 허용 여부를 전달하고, 정상 완료와 건너뛰기 완료를 구분해 한 번만 알린다.
- 현재 테스트 이미지는 기존 프로젝트 리소스를 사용한 임시 시각 자료이며 최종 Story 아트가 아니다.

### BattleTest01·BattleTest02 씬 단위 시스템

`BattleTest01`, `BattleTest02`의 씬 단위 시스템은 `BattleRoot` 아래에서 책임별로 구분한다.

```text
BattleRoot
├─ BattleCore
│  └─ GridManager / TurnManager / TacticalUnitRegistry / EnemyRegistry / HackableRegistry
├─ PlayerSystem
│  └─ PlayerInputReader / PlayerUnitControlManager / PlayerUnitInputController / PlayerUnitActionFlowController
├─ EnemySystem
│  └─ EnemyAlertCoordinator / EnemyTurnCoordinator
├─ BattleStage
│  └─ StageFailureCoordinator
├─ BattlePresentation
│  ├─ ActorPresentationRegistry / CombatActionPresenter / VfxManager
│  └─ ActionPresentationQueue
├─ BattleFlow
│  └─ BattleIntroPresenter / BattleEntryCoordinator / BattleCampaignBridge
├─ BattleVision
│  ├─ PlayerVisionContext / PlayerVisionManager / PlayerVisionPresenter
│  └─ PlayerVisionFogRoot
└─ BattleDebug
   └─ Debug
```

`ActionPresentationQueue`에는 씬 단일 연출 큐와 현재 디버그 리시버·테스터가 있으며 `BattlePresentation` 아래에 배치한다.
두 씬의 시야 Context 참조와 Fog 표시 값은 같은 기준으로 연결되어 있고, 유진·동료 테스트 데이터의 시야 거리는 각각 `6칸`으로 명시한다.
`BattleCampaignBridge`는 로딩 종료 뒤 `Battle` 단계와 활성 스테이지를 검사하고 결과 UI 확정을 영속 캠페인 흐름에 전달한다.
`CampaignBattleFlowCanvas`에는 입장 미션 문구와 임시 클리어·실패 결과 UI가 있다.

- 입장 연출은 `BattleIntroSequenceData`의 `Wait`, `MoveCamera`, `ShowMissionMessage`, `PlayDialogue` 명령을 배열 순서대로 자동 실행한다.
- 현재 테스트 데이터는 미션 문구와 카메라 왕복 이동을 사용하며, `PlayDialogue`는 자동 진행 말풍선 데이터와 발화자를 나중에 연결할 확장 자리다.
- 입장 연출은 `ActionPresentationQueue`의 첫 이벤트로 실행되어 행동, 유닛 선택과 수동 카메라 이동을 막는다.
- `StageFailureCoordinator`는 `DefeatOnDeath` 중요 조작 유닛 사망 또는 플레이어 조작 유닛 전원 사망을 실패로 판정한다.
- 현재 유진 테스트 데이터는 `DefeatOnDeath`가 켜져 있고 동료 테스트 데이터는 꺼져 있다.
- 결과 UI 확인 뒤 승리는 저장 성공을 전제로 후일담 또는 완료·로비 흐름으로 이동하며, 실패는 진행을 바꾸지 않고 로비로 돌아간다.

### 맵 계층

```text
MapVisualGrid
├─ FloorTilemap
├─ ObjectTilemap
├─ WallLogicTilemap
└─ LowObstacleLogicTilemap
```

- `FloorTilemap`: 바닥 화면 표시.
- `ObjectTilemap`: 벽과 장식 오브젝트 화면 표시.
- `WallLogicTilemap`: 이동과 시야를 모두 차단하는 벽 칸의 논리 원본.
- `LowObstacleLogicTilemap`: 이동은 차단하지만 시야는 통과시키는 낮은 상자·엄폐물 칸의 논리 원본.
- 화면용 타일맵과 논리 타일맵은 서로 독립적이다.
- 문처럼 런타임에 열리는 장애물은 두 논리 타일맵에 칠하지 않고 `GridActor` 점유와 동적 시야 차단 등록으로 막는다.

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

- `IsBlocked`: `WallLogicTilemap` 또는 `LowObstacleLogicTilemap`에서 읽은 고정 이동 장애물 여부.
- `OccupiedActor`: 현재 칸을 점유한 `GridActor`.
- `CanEnter`: 고정 장애물도 없고 점유자도 없는지 여부.

### GridManager

`GridManager`는 씬 단일 보드 관리자다.

- 보드 크기, 셀 크기, 월드 원점을 관리한다.
- `GridToWorld()`, `WorldToGrid()`로 좌표를 변환한다.
- 논리 칸의 월드 위치를 두 논리 타일맵의 셀로 변환해 이동 차단 타일 유무를 읽는다.
- `RegisterActor()`, `UnregisterActor()`, `TryMoveActor()`로 동적 점유를 관리한다.
- `CanEnter()`, `IsBlocked()`, `IsOccupied()`, `TryGetActorAt()`을 제공한다.
- `IsSightBlocked()`는 `WallLogicTilemap` 벽과 명시적으로 등록된 동적 구조물만 시야 차단물로 판정한다.
- `LowObstacleLogicTilemap`은 이동을 막지만 시야를 막지 않으며 추후 낮은 엄폐 판정의 논리 원본으로 확장할 수 있다.
- `RegisterSightBlocker()`와 `UnregisterSightBlocker()`는 닫힌 문처럼 런타임에 바뀌는 시야 차단물을 관리하고 변경 이벤트를 발생시킨다.
- 플레이어·NPC·적을 포함한 일반 점유 Actor는 동적 시야 차단물로 등록하지 않는다.

### 공통 시선과 플레이어 시야

- `GridLineOfSight`는 적과 플레이어 시야가 함께 사용하는 그리드 가림 계산기다.
- 정확히 대각선 모서리를 지날 때는 양쪽 인접 칸이 모두 막힌 경우에만 시야를 차단한다.
- 목표 칸 자체가 벽이어도 벽 표면은 탐색할 수 있으며, 벽 뒤쪽 칸은 가려진다.
- `PlayerVisionManager`는 살아 있는 플레이어 조작 유닛들의 360도 원형 시야를 합집합으로 계산한다.
- 플레이어 유닛 주변 8칸은 벽 모서리와 관계없이 근접 시야로 항상 보인다. 이 예외는 플레이어 시야에만 적용하고 공통 LOS와 적 시야는 바꾸지 않는다.
- 유닛별 시야 거리는 `ControllableUnitData.VisionRange`가 제공하며 기본값은 `6칸`이다.
- 검 능력 유닛의 도깨비검이 배치 상태면 검 위치에서 `SwordVisionRange`만큼 별도 시야를 합산한다. 현재 테스트 값은 `3칸`이다.
- 검 시야는 벽과 닫힌 문에 막히고 낮은 장애물을 통과하며, 플레이어 근접 시야의 주변 8칸 강제 표시 예외는 사용하지 않는다.
- 각 칸은 `Unexplored`, `Explored`, `Visible` 세 상태를 가진다. `Explored`는 한 번 보았지만 현재 시야 밖인 누적 탐색 상태다.
- 플레이어 한 칸 이동의 `MoveStepEnteredLogicEvent`마다 독립 `PlayerVisionSnapshot`을 만들어 이동 연출 바로 뒤에 `PlayerVisionChanged` 이벤트를 추가한다.
- 스냅샷은 현재·누적 칸뿐 아니라 그 논리 시점에 보였던 적 Actor도 복사해, 이후 적 논리 위치가 먼저 바뀌어도 연출 전에 정보를 누출하지 않는다.
- 플레이어 유닛 사망, 보안문 개방과 검 투척·회수·해킹 위치 변경도 합산 시야를 다시 계산한다.
- 총·근접·해킹은 시야 시스템이 활성화된 전투에서 현재 시야 밖 칸을 대상으로 선택할 수 없다.
- 검 투척은 현재 시야와 관계없이 검 위치 기준 사거리 안의 보드 칸을 선택할 수 있다. 숨은 적의 칸을 우연히 지정하면 검 도착 시야가 열린 뒤 표준 피해 연출이 이어진다.
- `PlayerVisionContext`는 Grid, 유닛·적·Actor Visual 등록소, 연출 큐, 시야 Manager·Presenter와 Fog Root 참조만 보관한다.

현재 전투 테스트 씬 설정은 `16 x 16`, 셀 크기 `1`, 원점 `(0, 0, 0)`이다.

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
- 현재 전투 테스트 씬에서는 Space 키로 턴 종료를 시험할 수 있다.

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

- 미리보기는 현재 플레이어 시야에 보이는 적만 검사해 첫 위험 칸을 경고색으로 표시한다. Fog 밖 적의 위치와 감지 범위는 경고로 누설하지 않는다.
- 실제 이동 중 처음 감지된 칸에서 `AlertTriggeredLogicEvent`를 발행한다.
- 실제 발각 판정은 표시 여부와 무관하게 살아 있는 모든 적 시야를 사용한다.
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
- 투척 목표 칸은 현재 플레이어 시야 밖이어도 선택할 수 있다.
- 목표 칸에 `IDamageable` 대상이 있으면 표준 피해 이벤트를 발행한다.
- 회수는 검을 플레이어 칸으로 되돌린다.
- 해킹은 대상 주변 실행 칸으로 검 기준 위치를 옮긴다.
- 배치된 검은 `SwordVisionRange`만큼 별도 시야를 제공하지만 전술 유닛이나 GridActor가 아니므로 적 애드를 발생시키지 않는다.
- 검 이동 연출 뒤 새 시야가 열리고, 숨은 대상이 있으면 이후 타격 연출이 이어진다.
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

`EnemyGridSight`는 상하좌우 방향 기준의 전방 부채꼴 시야와 주변 청각 근접 감지를 계산한다.

- 전방 거리가 멀어질수록 좌우 폭이 넓어진다.
- 부채꼴의 각 후보 칸까지 그리드 시선을 검사해 고정·동적 장애물이 만드는 뒤쪽 그림자를 제외한다.
- `WallLogicTilemap` 벽과 닫힌 보안문은 시야를 막고, `LowObstacleLogicTilemap`과 플레이어·NPC·적의 점유 칸은 시야를 막지 않는다.
- 장애물 칸 자체는 정면 시야 감지 칸에 포함하지 않는다.
- 시선이 정확히 대각선 모서리를 지날 때 한쪽만 막혀 있으면 허용하고 양쪽이 모두 막혀 있으면 차단한다.
- 근접 감지는 청각 규칙이며 방향과 장애물의 영향을 받지 않고 현재 설정 반경의 8방향 칸을 감지한다.
- 보안문이 열려 동적 차단 등록이 해제되면 모든 적 시야가 즉시 다시 계산된다.

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
- `PlayerVisionChanged`
- `ActorVisibilityOverride`
- `CombatAction`
- `StageCleared`

### Actor 비주얼 연결

- `ActorPresentationRegistry`: `GridActor`와 `ActorVisualController` 연결을 보관한다.
- `ActorPresentationBinding`: 각 Actor의 논리·비주얼 연결을 등록한다.
- `ActorPresentationSynchronizer`: 논리 위치와 VisualRoot 위치를 초기 동기화한다.
- `ActorVisualController`: SpriteRenderer 색상·좌우 반전과 Animator 상태를 적용하며, 기존 색상 연출과 독립된 시야 알파를 합성한다.
- 현재 원본 캐릭터 아트의 기본 방향은 오른쪽이다.

### Presenter 책임

- `GridActorMovePresenter`: 플레이어·적 이동 위치 보간과 이동 상태 전환. 적이 시야 경계를 넘으면 한 칸 이동과 표시 알파를 함께 보간한다.
- `AlertDetectedPresenter`: 경고색 점멸과 선택적 발각 애니메이션.
- `CombatActionPresenter`: 공격자·피격자 방향과 공격·피격·사망 자세 동시 처리.
- `SwordActionPresenter`: 검 투척·해킹·회수 위치와 근접 기울기.
- `HackPresenter`: 현재 해킹 시간 대기와 로그.
- `SecurityDoorPresenter`: 열린 문의 `DoorVisual` 비활성화.
- `StageResultPresenter`: 클리어·실패 연출 이벤트 처리. 현재는 한국어 로그 출력 후 즉시 완료.
- `PlayerUnitSelectionPresenter`: 현재 조작 유닛의 임시 LineRenderer 선택 링.
- `PlayerVisionPresenter`: 런타임 Fog 칸 생성, 미탐색·탐색·현재 시야 색 전환, 적 표시와 공격자 임시 노출. `Reveal All For Debug`를 켜면 실제 시야 판정은 유지한 채 Fog와 적 숨김만 해제하며 플레이 중에도 즉시 전환할 수 있다.
- 시야 밖 공격자는 공격 연출 동안 Sprite 정렬을 Fog보다 한 단계 위로 올리고, 공격 종료 뒤 기존 Sorting Layer·Order로 복원한다. Fog 지형 자체는 걷지 않는다.

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
- 두 칸은 고정 논리 타일맵에 칠하지 않는다.
- `SecurityDoorController.UnlockHackable`은 터미널의 `HackableObject`를 참조한다.
- `BlockingActors`에는 좌우 Logic의 `GridActor` 두 개가 연결되어 있다.
- `SecurityDoorController`는 기존 `BlockingActors`를 닫힌 동안 동적 시야 차단물로 자동 등록하며, 열릴 때 등록과 점유를 함께 해제한다.
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
- 플레이 모드에는 Game 뷰의 전체 Gizmos 토글과 `StageGoal` Gizmo 필터가 모두 켜진 경우 같은 좌표에 프리팹 비주얼 교체 전 임시 목표 표시를 그린다.
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

현재 전투 테스트 씬 값은 이동 속도 `8`, 최대 이동 거리 `(10, 10)`, 연출 중 잠금 활성화다.

### 대사

기존 말풍선 대사 구조는 유지되어 있다.

- `DialogueSequenceData`, `DialogueStepData`, `DialogueLineData`: 대사 데이터.
- `DialogueManager`: 대사 진행.
- `DialogueSpeaker`, `DialogueBubblePresenter`, `SpeechBubbleView`: 화자와 말풍선 표시.

현재 전술 행동 흐름과 튜토리얼 진행을 묶는 전용 대사 트리거는 아직 없다.

현재 말풍선 대사 시스템은 전투 씬 안의 인게임 대사용으로 유지한다.
메인 Story는 별도 `StoryTest` 씬에서 배경, 캐릭터 스탠딩, 하단 대화창을 사용하는 비주얼 노벨 방식으로 분리했다.
`StorySequenceData`의 명령 배열만 순서대로 소비하며 선택지나 분기 명령은 없다.
중요 장면의 만화는 외부에서 한 장의 완성 이미지로 제작하고 `ShowComicPanel` 명령으로 전체 화면에 표시한다.
시나리오 원문과 최종 일러스트는 Story 데이터 에셋과 이미지 참조를 교체해 적용한다.

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
- 플레이어 시야 기본값: `6칸`.

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

현재 코드와 테스트 씬에서 다음 항목을 확인했다.

- `dotnet build S2.slnx --no-restore`: 경고 0개, 오류 0개.
- `BootstrapTest`의 AppRoot, 캠페인 데이터, 저장·흐름·씬 전환 컴포넌트와 LoadingCanvas 필수 참조 연결.
- `BootstrapTest` 씬 YAML의 로컬 fileID 누락·중복 없음.
- `BootstrapTest → LobbyTest` 비동기 진입 코드와 Build Settings 활성 씬 이름의 정적 검증.
- 사용자가 Unity 실행 후 `Application.persistentDataPath` 아래에 `campaign-save.json`이 실제 생성되는 것을 확인했다.
- 다중 플레이어 유닛 직접 선택과 AP 소진 자동 전환.
- 플레이어 이동, 범위·경로 표시, 적 시야 발각과 경계 반응.
- 적 턴 공격·엄폐 이동.
- 검 투척·해킹·회수 Visual과 통합 전투 자세.
- `WallLogicTilemap`, `LowObstacleLogicTilemap` 기반 고정 이동·시야 장애물 판정.
- 편집 모드 `GridActor` 점유 칸 표시.
- WASD·방향키 카메라 이동과 범위 제한.
- 해킹 터미널과 2칸 보안문의 차단·개방 전체 흐름.
- `IPresentationEventHandler` 기반 연출 처리자 등록, 필터링, 실행과 완료 대기 흐름.
- 그리드 시선 알고리즘의 정면 차단, 좌우 대칭, 한쪽·양쪽 대각선 모서리 규칙.
- Unity 플레이 모드에서 고정 장애물 그림자, 청각 근접 감지와 보안문 개방 전후 시야 갱신.
- 편집 모드와 플레이 모드 Scene 뷰의 `StageGoal` 위치 표시. Game 뷰 표시는 Editor의 Game 뷰 Gizmos 토글·필터에 의존한다.
- `StageResultPresenter`의 클리어 로그 처리와 검 투척·회수 반복 Visual 유지.
- 스테이지 클리어 후 플레이어 입력, 새 행동, 턴 전환과 남은 적 행동 차단.
- 프리팹으로 배치한 해킹 터미널과 보안문의 기존 해킹·개방 흐름 유지.
- Story 런타임 C# 컴파일 경고 0개, 오류 0개.
- 사용자가 Unity 플레이 모드에서 `StoryTest` 재생을 완료했다.
- `StorySequenceDataTest`의 대사·배경·스탠딩·완성 만화 이미지·페이드·시간 대기 명령 종류 구성 확인.
- Lobby 런타임과 UI C#의 Unity 컴파일 경고 0개, 오류 0개.
- Unity Editor API로 `LobbyTest` 씬 로드, `LobbyStageButton.prefab`, 필수 컴포넌트와 모든 Inspector 참조가 유효함을 확인했다.
- `LobbyTest`와 `LobbyStageButton.prefab` YAML의 로컬 fileID와 외부 GUID 누락이 없음을 확인했다.
- `dotnet build S2.slnx --no-restore` 재검증 결과 경고 0개, 오류 0개.
- 사용자가 원본 Unity 플레이 모드에서 현재 Lobby 표시와 입력 테스트를 완료했다.
- 사용자가 원본 Unity에서 Bootstrap·Lobby를 거쳐 1-1 전투 전 Story, Battle과 전투 후 Story로 이어지는 현재 캠페인 핵심 흐름 테스트를 완료했다.
- Story·Battle Bridge가 씬 전환 완료 뒤 캠페인 단계를 검사하도록 초기화 순서를 수정한 후 진입 오류가 해소됐다.
- 세 캠페인 Story 데이터는 시작 `Fade In` 명령으로 검은 오버레이를 걷어 내고 대사를 표시한다.
- 플레이어 시야 신규 런타임 C#을 포함한 `dotnet build S2.slnx --no-restore`: 경고 0개, 오류 0개.
- 시야 밖 공격자 임시 노출이 불투명 Fog에 다시 가려지던 문제를 Sorting Layer·Order 임시 덮어쓰기와 원상 복구로 수정했다.
- 사용자가 `BattleTest01`에서 Fog, 탐색 지형 유지, 적 표시와 공격자 임시 노출을 포함한 시야 기능 플레이 테스트를 완료했다.
- `BattleTest01`, `BattleTest02`의 `BattleRoot` 책임별 Hierarchy, 시야 필수 참조와 Missing Script를 Unity Editor API로 검증했다.
- 두 씬의 고유 입장 시퀀스 참조를 유지한 상태에서 `dotnet build S2.slnx`: 경고 0개, 오류 0개.
- 두 Battle 씬의 기존 논리 타일을 `WallLogicTilemap`으로 보존하고 빈 `LowObstacleLogicTilemap`을 생성해 `GridManager`에 연결했다.
- 플레이어 주변 8칸 강제 시야, 도깨비검 `3칸` 시야, 시야 밖 검 투척과 보이는 적 기반 발각 예상 코드를 구현하고 Unity Editor 참조 검증을 통과했다.
- 사용자가 현재 코드로 두 Battle 씬의 시야·전투·캠페인 흐름을 플레이 테스트했고 문제없이 동작함을 확인했다.
- 플레이어 주변 8칸 근접 시야, 도깨비검 정찰·눈먼 투척, 검 이동·시야 개방·피해 연출 순서, 검 회수·해킹 뒤 시야 갱신을 확인했다.
- 보이는 적 기반 발각 예상 경고, 숨은 적의 실제 이동 발각과 `LowObstacleLogicTilemap`의 이동 차단·시야 통과를 확인했다.

## 17. 현재 한계

- 입력은 임시 키·마우스 매핑이며 정식 UI와 Input Action Map은 아직 없다.
- 플레이어 이동은 논리적으로 즉시 확정되며 발각 시 중간 정지나 카메라 컷은 없다.
- 적 애드는 단일 단계 전파이며 연쇄 전파는 없다.
- 엄폐 평가는 벽 인접과 노출·거리 중심의 1차 점수 구조다.
- 해킹 연출은 시간 대기와 로그, 문 열림은 비주얼 비활성화 수준이다.
- 검 경로, 총격, 근접 타격, 적 공격의 최종 VFX가 없다.
- 선택 표시는 런타임 LineRenderer 임시 링이다.
- 현재 클리어·실패 결과 UI와 입장 미션 표시는 캠페인 흐름 검증용 임시 화면이며 최종 디자인이 아니다.
- `HeavyGun`은 데이터 타입만 있고 행동 구현이 없다.
- 현재 Lobby UI는 캠페인 흐름 연결을 검증하기 위한 임시 화면이며, 이후 정식 로비를 별도로 제작할 예정이다.
- 이미 본 Story 기록과 그에 따른 건너뛰기 정책은 아직 저장 데이터에 없다. 현재는 `StoryRunner.Play()` 호출자가 건너뛰기 허용 여부를 전달한다.
- `StoryTest`의 배경·스탠딩·만화 이미지는 기존 테스트 이미지를 사용하며 최종 시나리오 데이터와 전용 아트가 아니다.
- 플레이어 시야 Fog는 현재 런타임 사각형 SpriteRenderer 방식의 1차 구현이며, 최종 마스크 Shader와 전용 시각 스타일은 후순위다.
- 적 공격의 LOS 적용 여부는 의도적으로 보류 상태다.

## 18. 다음 작업

캠페인 1~4단계의 상세 목적, 예정 책임, 상태별 흐름, 예외 처리와 완료 기준은 `S2-T 작업일지.md`의 `2026-07-26 캠페인 1~4단계 인수인계 문서화` 항목을 기준으로 한다.

현재 캠페인 진행 상태는 **1~4단계 코드·데이터·Hierarchy·Inspector 구현 완료, 1-1 핵심 캠페인 한 바퀴 사용자 플레이 테스트 완료**다.
현재 Lobby와 전투 결과 UI, 입장 미션 문구는 임시지만 게임 한 바퀴 흐름 검증에는 사용할 수 있다.

1. 완료 스테이지 재플레이에서 전투 전·후 Story가 다시 나오고 진행 상태가 낮아지지 않는지 확인한다.
2. Story Escape 스킵 뒤 전투·후일담·로비 흐름이 유지되는지 확인한다.
3. 유진 사망, 조작 유닛 전원 사망과 일반 전투 실패 시 로비 복귀를 확인한다.
4. 1-2 승리 뒤 후일담 없이 완료 저장과 Lobby 복귀가 되는지 확인한다.
5. 실제 시나리오와 최종 아트가 준비되면 임시 Story·입장 연출·결과 UI 데이터를 교체한다.
6. 대화 로그와 이미 본 Story 기록, 정식 결과 화면은 프로토타입 한 바퀴 이후 확장한다.

## 19. 문서 유지 규칙

- 이 문서에는 현재 실제로 존재하는 구조만 기록한다.
- 날짜별 구현 과정, 실패한 시도, 제거한 구조는 `S2-T 작업일지.md`에만 남긴다.
- 클래스가 제거되거나 책임이 바뀌면 과거 설명을 덧붙이지 않고 해당 현재 항목을 직접 갱신한다.
- 씬 설정을 바꾸면 코드 설명뿐 아니라 `테스트 씬과 캠페인 기반`, `현재 테스트 데이터`, `현재 검증 상태`도 함께 갱신한다.
- 다음 작업을 완료하면 `현재 한계`와 `다음 작업`에서 완료 항목을 제거하거나 새 상태로 교체한다.

## 20. 적 평상 순찰과 도깨비검 의심 AI 코드 구조

### 인식 상태와 턴 분기

- `EnemyAlertState`는 `Unaware`, `Suspicious`, `Alerted`를 보관한다.
- `Unaware`는 `EnemyRoutineController`, `Suspicious`는 `EnemyInvestigationAgent`, `Alerted`는 기존 `EnemyTurnAgent` 전투 행동을 실행한다.
- 플레이어를 발견한 적 턴에는 기존 즉시 엄폐 반응만 실행되고 공격은 다음 적 턴부터 가능하다. `Alerted`는 현재 영구 상태다.
- `TurnManager.EnemyTurnIndex`가 의심 유지 턴과 그룹 행동 중복 실행의 기준 번호를 제공한다.

### PatrolPoint 그래프와 그룹 순찰

- `PatrolPoint`는 씬 위치, 도착 방향, 대기 턴과 연결 지점 목록을 가진다.
- 다음 목적지는 직전 지점을 우선 제외한 연결 후보 중 무작위 선택한다. 다른 후보가 없는 막다른 지점에서는 직전 지점으로 되돌아간다.
- 양방향 두 지점은 왕복, 원형 연결은 Loop, 분기 지점은 등록된 연결 안의 제한적 랜덤 순찰로 동작한다.
- 목적지는 도착할 때까지 유지하며, 한 적 턴에 1AP와 `EnemyData.PatrolMoveRange`만큼 이동한다. 연속 막힘은 경고 로그로 드러낸다.
- Scene 뷰 Gizmo로 지점, 도착 방향과 연결선을 확인할 수 있다.
- `EnemyPatrolGroup`은 하나의 그래프와 목적지를 공유하고 그룹원별 `FormationOffset`을 유지한다.
- 모든 생존 그룹원이 이동 가능한 공통 구간까지만 진행하므로 3칸 경로의 뒤가 막히면 1~2칸 이동할 수 있다. 한 명도 다음 공통 이동을 못 하면 그룹 전체가 멈춘다.
- 그룹원 점유는 리더 경로 탐색에서 제외하고, 고정 장애물과 외부 유닛 점유는 유지한다. 도착 후에는 지점의 공통 방향을 바라본다.
- 현재 리더가 사망하면 `EnemyPatrolGroup`의 구성원 등록 순서에서 첫 번째 생존자가 런타임 리더를 계승한다. 새 리더의 기존 오프셋을 0으로 다시 맞춰 남은 구성원의 상대 간격을 유지한다.
- 조사 후 편대 Anchor는 활성 리더의 현재 편대 기준 칸 주변 맨해튼 `8칸` 안에서만 찾고, 각 구성원의 도달 경로도 같은 거리로 제한한다.
- 편대 목표 칸이 고정 장애물이나 외부 유닛 점유로 무효화되면 즉시 Anchor를 폐기한다. 이동만 `3턴` 연속 실패해도 기존 Anchor를 폐기하고 이후 편대 복귀 실행에서 다시 계산한다.

### 도깨비검 감지와 의심 조사

- `EnemyGridSight.SwordDetectionPositions`는 플레이어 감지 시야와 별도로 계산한다.
- 검 기척은 `SwordDetectionRange`의 제곱 유클리드 거리 기반 원형이며 벽과 닫힌 문 LOS에 막히고 낮은 장애물은 통과한다.
- 검 투척·검 기반 해킹과 평상 적 이동 뒤 `EnemySuspicionCoordinator`가 감지를 검사한다.
- 적 직접 타격은 의심을 만들지 않고 기존 피해 흐름으로 즉시 `Alerted`를 발생시킨다.
- 같은 PatrolGroup은 거리와 무관하게 의심을 공유한다. 다른 그룹은 직접 감지자의 `SuspicionSpreadRange` 안에 그룹원 하나라도 있으면 그룹 전체가 공유한다. 전파는 단일 단계다.
- 직접 감지자는 `Investigator`, 전파받은 적은 `Support` 역할을 받는다.
- `EnemyInvestigationAgent`는 AP와 별개의 즉시 반응 이동으로 이상 지점을 볼 수 있는 적정 거리의 엄폐 후보를 찾는다. 지원자는 거리·분산 점수로 한 지점 밀집을 피한다.
- 즉시 반응은 조사 턴에 포함하지 않는다. 이후 적 턴 3회 동안 방향을 돌며 탐색하고, 새 검 투척·해킹 감지는 조사 정보와 카운트를 초기화한다.
- 이미 `Suspicious`인 적이 검을 다시 감지하면 최신 위치·감지자·역할을 포함한 조사 정보와 남은 턴을 갱신한다. `SuspicionDetected` 연출과 즉시 반응 이동 이벤트는 다시 만들지 않으며, 순찰 복귀 중이었다면 복귀를 중단하고 탐색 단계로 돌아간다.
- 조사 이동 자체는 같은 배치 검의 재의심을 만들지 않는다. 종료 후 Guard는 시작 위치·방향으로 복귀한다. Patrol 그룹은 생존 편대를 놓을 수 있는 가까운 기준 칸을 선택하고 구성원별 경로를 단계 단위로 동시에 이동한다. 먼저 도착한 구성원은 정지하며, 전원이 모이면 중단 당시 목적지를 이어서 순찰한다.

### 검 투척 미리보기와 의심 연출

- 검 투척 모드는 현재 보이는 `Unaware/Suspicious` 적들의 검 감지 범위 합집합을 표시하고 포인터 칸의 의심 경고를 별도 표시한다.
- `Alerted` 적의 검 감지 범위와 의심 경고는 숨기지만, 해당 적을 직접 타격하는 칸의 공격 경고는 유지한다.
- `SuspicionDetected` 연출은 기존 `AlertDetectedPresenter`가 노란 의심 상태 색과 점멸로 처리한다.
- 우클릭·Escape, 다른 행동/유닛 선택, 스테이지 종료 또는 컴포넌트 비활성화로 행동이 취소되면 이동 범위·경로 위험 표시와 검 감지 범위·목표 경고를 선택 상태와 관계없이 정리한다.

### 씬 연결과 검증 상태

- `BattleTest01`에는 적 Routine·Investigation, EnemySystem 조정자, PatrolPoint 그래프와 PatrolGroup 편대 연결이 완료됐다.
- 사용자가 낮은 장애물 우회, 재의심 연출 억제, 리더 계승, 조사 후 편대 복귀와 취소 미리보기 제거를 포함한 현재 테스트 구성을 플레이했고 문제없음을 확인했다.
- `BattleTest02`와 공용 적 프리팹에는 이 순찰·의심 AI 연결을 아직 확장하지 않았다.
- 최신 조사 정보 갱신, 반경 제한 Anchor 탐색과 막힌 Anchor 재계산 코드는 정적 빌드를 통과했으며 플레이 모드 재검증이 남아 있다.

### BattleTest01 그룹 순찰 연결 예시

- `BattleTest01`만 적 AI 컴포넌트와 그룹 순찰 Inspector 연결을 완료했다. `BattleTest02`와 공용 적 데이터·프리팹은 수정하지 않았다.
- `BattleTest01`의 `LowObstacleLogicTilemap`에는 이동 차단·시야 통과와 순찰 우회를 확인할 테스트용 논리 타일 17칸이 배치되어 있다.
- 씬의 두 적은 프리팹 인스턴스가 아닌 씬 로컬 `EnemyLogic` 오브젝트다.
- 각 적에 `EnemyRoutineController`, `EnemyInvestigationAgent`를 추가하고 자기 `EnemyContext`와 상호 참조를 연결했다.
- `EnemySystem`에 `EnemyPerceptionCoordinator`, `EnemySuspicionCoordinator`를 추가했다.
- `EnemySystem/PatrolRoute_Group01` 아래에 `PatrolPoint_A_6_8`, `PatrolPoint_B_6_6`, `PatrolPoint_C_4_6`을 배치했다.
- 연결은 `A ↔ B ↔ C`이며 A 대기 0턴·위쪽, B 대기 1턴·아래쪽, C 대기 1턴·오른쪽 방향이다.
- `EnemyPatrolGroup` 리더는 `(6,8)` 적이며 편대는 리더 `(0,0)`, 두 번째 적 `(2,0)` 오프셋이다.
- 두 적은 `(6,8)/(8,8) → (6,6)/(8,6) → (4,6)/(6,6)`의 L자 경로를 편대를 유지하며 왕복한다.
- 위쪽 `(6,10)` 방향은 논리 타일맵의 좁은 입구에서 2칸 편대를 유지할 수 없어 예제 경로에서 제외했다.
- `BattleVision.PlayerVisionPresenter`의 `Reveal All For Debug`와 점유 칸 Play 표시를 켜서 조사 이동과 편대 복귀를 Fog 없이 관찰할 수 있다.
- `BattleTest01`의 유진은 전용 `유진 AI 회귀 테스트 데이터`를 사용하며 검 투척 사거리만 `7칸`으로 늘려 시작 칸 `(7,1)`에서 권장 칸 `(7,7)`에 바로 투척할 수 있다. 공용 유진 테스트 데이터와 다른 Battle 씬에는 영향을 주지 않는다.
- 카메라는 `(7, 4.5, -6)`, Orthographic Size `5.5`로 설정해 시작 플레이어와 순찰 그룹을 한 화면에 둔다.
- Unity Editor가 수정된 씬을 임포트·로드했으며 Missing Script가 없었다. YAML fileID 중복 0개, 누락 로컬 참조 0개와 런타임 어셈블리 컴파일 경고 0개·오류 0개를 확인했다.
- 현재 씬에서 낮은 장애물 우회, 취소 미리보기 제거, 재의심 갱신, 조사 후 편대 재구성과 리더 계승을 순서대로 확인할 수 있다.

## 21. 다음 개발 과제

### 적 편대 이동 연출

- 그룹 Patrol 동시 이동 코드가 구현됐다. 한 칸마다 전원 목적지를 검증하고 앞쪽 구성원부터 내부 점유를 갱신한 뒤, 전원 시야 갱신과 등록 순서상 첫 감지자 판정을 수행한다.
- 실제 확정된 구성원별 경로는 하나의 `GroupMove` 이벤트로 전달되며 `GroupMovePresenter`가 `GroupMovePresentationData`의 공통 시간·곡선으로 모든 Visual을 동시에 이동시킨다.
- 이동 중 발각되면 해당 칸에서 남은 논리 경로를 중단하고, 그룹 이동 연출 완료 뒤 기존 발각·애드·엄폐 반응 연출을 재생한다.
- `BattleTest01/EnemySystem/PatrolRoute_Group01`에 그룹 Presenter와 테스트 데이터 연결을 완료했다. 한 칸 이동 시간은 `0.2초`이며 사용자가 현재 편대 동시 이동을 플레이 테스트해 정상 동작을 확인했다.
- `AlertDetectedPresenter`가 인식 상태 변경을 구독하므로 의심 조사 종료 뒤 `Unaware`의 정상 색상으로 복원되고, 의심 중 발각 시 `Alerted` 색상으로 즉시 전환된다.
- 조사 후 Regroup도 Patrol의 그룹 이동 데이터·스냅샷·Presenter를 재사용해 구성원별 경로를 동시에 재생한다. 경로가 짧아 먼저 도착한 구성원은 멈추고 나머지 구성원만 다음 단계를 진행한다.
- 각 단계는 목표 칸 중복, 고정·외부 점유와 그룹원 점유 의존 관계를 먼저 검사한다. 다른 그룹원이 비울 칸은 해당 구성원을 먼저 점유 갱신하며 직접 자리 교환처럼 순환하는 이동은 거부하고 이후 적 턴의 재경로·Anchor 재계산 흐름에 맡긴다.
- 모든 이동 구성원의 논리 위치를 확정한 뒤 방향·시야를 갱신하고 등록 순서상 첫 감지자를 판정한다. 실제 확정 경로만 하나의 `GroupMove` 이벤트로 전달한다.
- `dotnet build S2.slnx --no-restore`에서 경고 0개, 오류 0개를 확인했다. 흩어진 조사 위치, 경로 길이 차이, 내부·외부 점유 막힘, 리더 계승과 이동 중 발각은 Unity 플레이 모드 검증이 남아 있다.

### 전투 연출과 전술 정보 표시

- 다키스트 던전식 전투 집중감을 참고해 공격 순간 카메라 이동·줌·정지·복귀를 포함한 전투 카메라 연출을 테스트한다.
- 카메라 연출은 `ActionPresentationQueue`가 완료를 기다릴 수 있는 Presenter 단위로 설계하는 방향을 우선 검토한다.
- 적이 현재 바라보는 방향과 실제 시야 방향을 읽을 수 있는 표시를 추가한다.
- 시야 방향 표시는 `EnemyGridSight`의 실제 판정과 동기화하고, 현재 시야 밖 적의 정보를 어디까지 공개할지 별도 규칙으로 정한다.
