using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>사용자 요청에 따라 저장된 1-1 씬에 초안 배치와 명시적 UI 연결을 작성한다.</summary>
public static partial class Stage11TutorialAuthoring
{
    // 초안 데이터는 씬과 분리해 Inspector에서 순서·대사를 수정한다.
    private const string DataPath = "Assets/Data/Campaign/Tutorial/Stage11_Tutorial.asset";

    /// <summary>깨끗한 1-1 편집 씬에만 초안을 적용한다. 자동 실행하지 않는다.</summary>
    [MenuItem("Tools/S2/Author 1-1 Tutorial Draft")]
    public static void Author()
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.name != "BattleTest01" || scene.isDirty)
            throw new InvalidOperationException("저장된 BattleTest01 편집 상태가 필요합니다.");
        if (UnityEngine.Object.FindFirstObjectByType<BattleTutorialController>() != null)
            throw new InvalidOperationException("이미 튜토리얼이 있습니다. 초안 재설정 대신 데이터 에셋을 수정하십시오.");

        var grid = UnityEngine.Object.FindFirstObjectByType<GridManager>();
        var player = GameObject.Find("Player Actor Root").GetComponentInChildren<TacticalUnitContext>();
        var enemies = UnityEngine.Object.FindObjectsByType<EnemyContext>(FindObjectsSortMode.None);
        var patrol = enemies.Single(e => e.transform.parent.name == "천하회_작은못");
        var big = enemies.Single(e => e.transform.parent.name == "천하회_큰못");
        var hammer = enemies.Single(e => e.transform.parent.name == "천하회_망치");
        var terminal = UnityEngine.Object.FindFirstObjectByType<HackableObject>();
        var camera = Camera.main;
        var turns = UnityEngine.Object.FindFirstObjectByType<TurnManager>();
        var hud = UnityEngine.Object.FindFirstObjectByType<BattleHudController>();
        var font = ((BattleHudAssetSet)new SerializedObject(hud).FindProperty("assetSet").objectReferenceValue).Font;

        Place(player.GridActor, new GridPosition(23, 14), grid);
        Place(patrol.GridActor, new GridPosition(23, 22), grid);
        Place(big.GridActor, new GridPosition(37, 14), grid);
        Place(hammer.GridActor, new GridPosition(47, 19), grid);
        Place(terminal.GridActor, new GridPosition(47, 20), grid);
        Set(patrol.GridSight, "facingDirection", (int)GridDirection.Up);
        Set(big.GridSight, "facingDirection", (int)GridDirection.Right);
        Set(hammer.GridSight, "facingDirection", (int)GridDirection.Up);
        foreach (var enemy in enemies)
        {
            Set(enemy.Health, "maxHitPoint", 5); Set(enemy.Health, "currentHitPoint", 5);
            foreach (var reaction in enemy.transform.parent.GetComponentsInChildren<EnemyAlertReactionCoordinator>()) reaction.enabled = false;
            Set(enemy.RoutineController, "routineType", (int)EnemyRoutineType.Guard);
            Set(enemy.RoutineController, "patrolGroup", null);
        }
        // 기존 순찰 경로·NPC·입장 데이터는 삭제하지 않고 1-1의 통제된 진행에서만 사용하지 않는다.
        foreach (var group in UnityEngine.Object.FindObjectsByType<EnemyPatrolGroup>(FindObjectsSortMode.None)) group.enabled = false;
        foreach (var coordinator in UnityEngine.Object.FindObjectsByType<EnemyTurnCoordinator>(FindObjectsSortMode.None)) coordinator.enabled = false;
        foreach (var entry in UnityEngine.Object.FindObjectsByType<BattleEntryCoordinator>(FindObjectsSortMode.None)) entry.enabled = false;
        foreach (var intro in UnityEngine.Object.FindObjectsByType<BattleIntroPresenter>(FindObjectsSortMode.None)) intro.enabled = false;
        GameObject.Find("NPC1")?.SetActive(false);
        Set(turns, "beginTurnOnStart", false);
        camera.transform.position = grid.GridToWorld(new GridPosition(23, 14)) + new Vector3(0, 0, -6);
        camera.orthographicSize = 6;
        var mover = camera.GetComponent<CameraKeyboardMover>();
        if (mover != null) Set(mover, "maxMoveDistance", new Vector2(40, 25));

        System.IO.Directory.CreateDirectory("Assets/Data/Campaign/Tutorial");
        var data = ScriptableObject.CreateInstance<BattleTutorialData>();
        data.steps = MakeSteps().ToArray();
        data.patrolDestination = new GridPosition(23, 26);
        data.hammerAttackPosition = new GridPosition(46, 18);
        data.firstCombatTurn = 3; data.secondCombatTurn = 7;
        AssetDatabase.CreateAsset(data, DataPath);

        var root = new GameObject("Stage11Tutorial");
        var context = root.AddComponent<BattleTutorialContext>();
        var controller = root.AddComponent<BattleTutorialController>();
        context.grid = grid; context.player = player; context.patrol = patrol; context.bigNail = big; context.hammer = hammer;
        context.terminal = terminal; context.door = UnityEngine.Object.FindFirstObjectByType<SecurityDoorController>();
        context.goal = UnityEngine.Object.FindFirstObjectByType<StageGoal>();
        context.turns = turns; context.stage = UnityEngine.Object.FindFirstObjectByType<StageStateManager>();
        context.queue = UnityEngine.Object.FindFirstObjectByType<ActionPresentationQueue>();
        context.control = UnityEngine.Object.FindFirstObjectByType<PlayerUnitControlManager>();
        context.actions = UnityEngine.Object.FindFirstObjectByType<PlayerUnitActionFlowController>(); context.worldCamera = camera;
        var canvasGo = new GameObject("TutorialCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(root.transform, false);
        var canvas = canvasGo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 80;
        var scaler = canvasGo.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
        context.presenter = BuildPresenter(canvas, camera, grid, font);
        context.dialogue = BuildDialogue(root, canvas, camera, font);
        var playerVisual = player.transform.parent.GetComponentInChildren<ActorVisualController>().transform;
        var bigVisual = big.transform.parent.GetComponentInChildren<ActorVisualController>().transform;
        context.speakers = new[] { Speaker(playerVisual, "유진"), Speaker(playerVisual, "금두꺼비", new Vector3(.8f, 1.4f, 0)), Speaker(bigVisual, "천하회 큰못") };
        Set(controller, "data", data); Set(controller, "context", context);
        EditorUtility.SetDirty(data);
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(scene);
        Debug.Log($"1-1 튜토리얼 초안 작성 완료: {data.steps.Length}개 단계, 9턴부터 자유 탈출.");
    }

    /// <summary>그리드와 VisualRoot의 기존 상대 배치를 보존하면서 시작 칸을 옮긴다.</summary>
    private static void Place(GridActor actor, GridPosition p, GridManager grid)
    {
        if (grid.IsBlocked(p)) throw new InvalidOperationException($"초안 시작 칸 {p}은 이동 불가입니다.");
        var root = actor.transform.parent;
        root.position += grid.GridToWorld(p) - grid.GridToWorld(actor.GridPosition);
        Set(actor, "gridPosition", p); actor.transform.position = grid.GridToWorld(p);
    }

    /// <summary>초안에 합의한 대사와 지정 행동을 배열로 작성한다.</summary>
    private static List<BattleTutorialStep> MakeSteps()
    {
        var steps = new List<BattleTutorialStep>();
        void Add(BattleTutorialStepKind kind, string id, string text, string speaker = "", BattleHudActionType action = BattleHudActionType.Move, int x = 0, int y = 0, int turn = 0)
            => steps.Add(new BattleTutorialStep { id = $"{steps.Count + 1:D2} {id}", kind = kind, text = text, speaker = speaker, action = action, target = new GridPosition(x, y), turn = turn });
        void D(string who, string text) => Add(BattleTutorialStepKind.Dialogue, "대화", text, who);
        void G(string title, string text) => Add(BattleTutorialStepKind.Guide, title, text);
        void A(string title, BattleHudActionType action, int x, int y, string text) => Add(BattleTutorialStepKind.Action, title, text, action: action, x:x, y:y);
        void E(int turn, bool automatic = false) => Add(BattleTutorialStepKind.EndTurn, "턴 종료", automatic ? "적의 행동을 확인하십시오." : "턴 종료 버튼을 눌러 적의 행동을 확인하십시오.", turn:turn);
        G("목표", "유진을 승강기까지 이동시키십시오. 먼저 집 앞 코너에서 주변을 살펴봅니다.");
        D("금두꺼비", "정지를 요청합니다. 해당 구역에 경비가 존재할 가능성이 높습니다.");
        D("금두꺼비", "손상된 기록에 따르면 승강기를 지키는 이가 존재할 가능성이 높으며, 교전이 예상됩니다.");
        D("유진", "여기 길은 내가 너보다 잘 알걸. 가까운 곳에 뒷문이 있어. 거기로 가면 돼.");
        Add(BattleTutorialStepKind.Camera, "승강기 위치", "승강기가 이번 목적지입니다.", x:52, y:26);
        G("목적지 확인", "보안문을 지나 승강기에 도착하면 탈출할 수 있습니다.");
        Add(BattleTutorialStepKind.Camera, "유진에게 복귀", "집 앞에서 출발합니다.", x:23, y:14);
        D("유진", "따라와. 여기로 가면 안전할 거니.");
        G("이동", "이동 버튼을 누르고 청록색으로 강조된 칸을 클릭하십시오. 이동 경로 3칸마다 1AP를 사용합니다.");
        A("집 앞 코너", BattleHudActionType.Move, 23,17,"이동 버튼 → 강조된 코너 칸. 다른 행동과 다른 목적지는 잠겨 있습니다.");
        Add(BattleTutorialStepKind.Camera,"순찰 발견","코너 너머에 순찰 중인 적이 있습니다.",x:23,y:22);
        D("금두꺼비", "분명 안전하다는 말씀을 들었습니다만, 유진님이 잘못 알고 있거나 전산상의 오류가 있을 것으로 판단됩니다.");
        D("유진", "이 골목까지 지킨다고? 제법 부지런한 깡패들이네."); D("유진", "일단 지나가게 두자고."); E(1);
        G("시야 밖의 적", "적이 시야 밖으로 사라져도 없어지는 것은 아닙니다. 이번에는 길을 따라 이동한 뒤 적과 조우하여 전투를 익힙니다.");
        D("유진", "좋아. 이동하자고.");
        A("골목 접근",BattleHudActionType.Move,26,14,"이동 버튼 → 강조된 칸. 코너를 돌아 골목으로 접근합니다. 2AP를 사용합니다."); E(2);
        A("첫 조우",BattleHudActionType.Move,35,14,"이동 버튼 → 강조된 칸. 3AP를 사용해 경비와 조우합니다.");
        D("유진", "젠장, 적이다."); D("금두꺼비", "침착하십시오. 상대 또한 지성인으로 확인됩니다. 여기서는 대화를 유도하심이…");
        D("천하회 큰못", "유진 네 이놈!"); D("천하회 큰못", "오늘이야말로 네놈 제삿날인 줄 알아라!");
        D("금두꺼비", "해당 상황에 대한 설명을 요청드립니다."); D("유진", "그렇게 됐다.");
        G("엄폐와 전투", "아군 턴에는 AP로 행동하고, 턴을 마치면 적이 행동합니다. 엄폐는 공격 방향에 따라 피격 가능성을 낮춥니다. 완전히 막아주지는 않습니다. 이번 공격은 학습을 위해 회피하도록 고정됩니다."); E(3,true);
        G("총격", "총격 버튼을 누르고 강조된 적을 클릭하십시오. 1AP와 탄약 1발을 사용합니다. 이번 총격은 학습을 위해 명중합니다.");
        A("첫 총격",BattleHudActionType.Gun,37,14,"총격 버튼 → 큰못. 2의 피해를 줍니다.");
        D("유진", "젠장, 용량도 얼마 없는데."); D("유진", "얌마, 너 튼튼하지?"); D("금두꺼비", "유진님의 질문 의도를 파악하지 못하였습니다."); D("유진", "그래, 뭐 튼튼하겠지. 아무래도.");
        A("근접 거리",BattleHudActionType.Move,36,14,"이동 버튼 → 적 옆의 강조된 칸. 1AP를 사용합니다.");
        G("근접 공격", "근접 공격은 주변 8칸의 적에게 사용할 수 있습니다. 검을 들고 있으면 3의 피해를 줍니다.");
        A("검 근접 공격",BattleHudActionType.Melee,37,14,"근접 공격 버튼 → 큰못. 남은 1AP로 적을 처치합니다.");
        D("금두꺼비", "해당 사용을 정지해 주십시오."); D("금두꺼비", "본 개체는 한성의 신호관제 기령, 코드 네임 금두꺼비로서 이러한 폭력적인 행위에…"); D("유진", "시끄러워. 계속 이동하자고."); E(4);
        A("정찰 준비",BattleHudActionType.Move,39,14,"이동 버튼 → 강조된 칸. 1AP를 사용합니다.");
        D("유진", "거의 다 왔다."); D("금두꺼비", "목적지 앞에 경비가 있을 가능성이 높습니다. 무력 충돌이 예상됩니다."); D("유진", "그래, 그러려니 해라. 그냥.");
        D("금두꺼비", "제안합니다. 본 개체의 자율부양 능력을 활용하여 먼저 정찰해 봄이 어떠십니까?");
        G("검 정찰", "금두꺼비가 도깨비검을 타고 먼저 정찰합니다. 검 투척 버튼으로 이동 칸을 지정하십시오. 검도 시야를 제공하며, 배치된 검에서 다시 이동할 수 있습니다.");
        A("검 정찰 1",BattleHudActionType.SwordThrow,44,14,"검 투척 버튼 → 첫 정찰 칸. 1AP를 사용합니다.");
        A("검 정찰 2",BattleHudActionType.SwordThrow,45,18,"검 투척 버튼 → 두 번째 정찰 칸. 남은 1AP로 망치를 발견합니다.");
        G("검의 기척", "검을 적에게 너무 가까이 보내면 적이 기척을 느끼고 주변을 조사합니다. 이번 정찰 칸은 기척 감지 범위 밖입니다."); E(5);
        A("검 회수",BattleHudActionType.SwordRecall,0,0,"검 회수 버튼을 눌러 도깨비검을 유진에게 돌려보내십시오. 1AP를 사용합니다.");
        D("유진", "얌마, 대답도 안 했는데 왜 혼자 뛰쳐나가냐?"); D("금두꺼비", "조우했던 단체와 비슷한 차림새의 경비 한 명이 확인되었습니다. 이 앞에서 전투가 예상됩니다.");
        D("유진", "그래, 이 개구리 같은 놈아. 한번 가보자."); D("금두꺼비", "정정 요청. 본 개체는 개구리가 아닌 코드 네임…");
        A("두 번째 접근",BattleHudActionType.Move,45,14,"이동 버튼 → 강조된 대기 칸. 2AP를 사용하며 아직 적에게 발각되지 않습니다."); E(6);
        A("망치 조우",BattleHudActionType.Move,45,17,"이동 버튼 → 강조된 칸. 1AP를 사용해 적과 조우합니다.");
        A("두 번째 총격",BattleHudActionType.Gun,47,19,"총격 버튼 → 망치. 이번 총격은 명중하며 2의 피해를 줍니다.");
        D("유진", "너 보기보다 튼튼하던데. 혹시 전투 기령도 탑재되어 있나?"); D("금두꺼비", "부정.");
        D("금두꺼비", "거듭 말씀드리지만 본 개체는 신호관제 기령으로서 전투 같은 야만적인 행위에는 적합하지…");
        D("유진", "좋아. 그럼 가서 박치기다. 가라, 개구리!"); D("금두꺼비", "아까부터 저의 말을 끊는 행위를 멈춰…"); D("유진", "가라!");
        A("공격용 검 투척",BattleHudActionType.SwordThrow,47,19,"검 투척 버튼 → 망치. 2의 피해를 주고 검은 적 위치에 남습니다."); E(7,true);
        D("유진", "마무리는 주먹이다!");
        G("맨손 공격", "검이 떨어져 있으면 근접 공격은 맨손으로 실행되어 1의 피해를 줍니다.");
        A("맨손 마무리",BattleHudActionType.Melee,46,18,"근접 공격 버튼 → 망치. 1AP를 사용해 적을 처치합니다.");
        A("전투 후 검 회수",BattleHudActionType.SwordRecall,0,0,"검 회수 버튼을 누르십시오. 1AP를 사용합니다.");
        D("금두꺼비", "적 개체 무력화가 완료되었습니다. 다만 이러한 행위는 본 개체에게 적합하지 않은 수단이었음을 인지해 주셨으면 합니다.");
        D("유진", "그것보다 문이 막혀 있는데, 이거 방법 없나?"); D("금두꺼비", "한성의 신호관제 기령인 저에게 맡겨 주십시오.");
        G("전산 침입", "해킹 버튼을 누르고 터미널을 클릭하십시오. 1AP를 사용해 접속하면 보안문이 열립니다.");
        A("보안문 해제",BattleHudActionType.Hack,47,20,"해킹 버튼 → 강조된 터미널. 남은 1AP로 보안문을 개방합니다.");
        D("금두꺼비", "터미널에 접속하여 전산 침입을 완료했습니다."); D("유진", "오, 이런 것도 할 줄 알았네?");
        D("금두꺼비", "한성의 신호관제 기령으로서 기본적인 소양입니다."); D("유진", "좋아. 이제 지상으로 올라가 보자고."); E(8);
        G("탈출", "이제 자유롭게 행동할 수 있습니다. 열린 보안문을 지나 승강기 표시 칸에 도착하십시오. AP가 부족하면 턴을 종료하고 계속 이동합니다.");
        Add(BattleTutorialStepKind.FreePlay,"자유 탈출","승강기에 도착하면 이번 스테이지를 완료합니다.",x:52,y:26);
        return Stage11TutorialOpeningRevision.ReviseSteps(steps.ToArray()).ToList();
    }

    /// <summary>대사 입력과 목표 칸 강조를 편집 시 명시적으로 생성한다.</summary>
    private static BattleTutorialPresenter BuildPresenter(Canvas canvas, Camera camera, GridManager grid, TMP_FontAsset font)
    {
        var presenter = canvas.gameObject.AddComponent<BattleTutorialPresenter>();
        var marker=Rect("TargetCell",canvas.transform,new Vector2(60,60)); var group=marker.gameObject.AddComponent<CanvasGroup>(); group.blocksRaycasts=false;
        for(int i=0;i<4;i++)
        {
            var edge=Rect("Edge"+i,marker,Vector2.zero); var image=edge.gameObject.AddComponent<Image>(); image.color=new Color(.2f,1,.95f); image.raycastTarget=false;
            bool horizontal=i<2; edge.anchorMin=horizontal?new Vector2(0,i):new Vector2(i-2,0); edge.anchorMax=horizontal?new Vector2(1,i):new Vector2(i-2,1);
            edge.sizeDelta=horizontal?new Vector2(0,4):new Vector2(4,0);
        }
        Set(presenter,"canvas",canvas); Set(presenter,"worldCamera",camera); Set(presenter,"grid",grid);
        Set(presenter,"marker",marker); Set(presenter,"markerGroup",group);
        return presenter;
    }

    /// <summary>기존 대사 매니저를 사용하는 말풍선 화면을 연결한다.</summary>
    private static DialogueManager BuildDialogue(GameObject root, Canvas canvas, Camera camera, TMP_FontAsset font)
    {
        var manager=root.AddComponent<DialogueManager>(); var bubbles=canvas.gameObject.AddComponent<DialogueBubblePresenter>();
        var layer=Rect("SpeechBubbles",canvas.transform,Vector2.zero); layer.anchorMin=Vector2.zero; layer.anchorMax=Vector2.one; layer.sizeDelta=Vector2.zero;
        var template=Rect("SpeechBubbleTemplate",layer,new Vector2(520,140)); template.pivot=new Vector2(.5f,0);
        var image=template.gameObject.AddComponent<Image>(); image.color=new Color(.03f,.04f,.04f,.97f); image.raycastTarget=false;
        var text=Text("Dialogue",template,font,27,new Vector2(480,104),Vector2.zero); text.rectTransform.anchorMin=Vector2.zero; text.rectTransform.anchorMax=Vector2.one; text.rectTransform.offsetMin=new Vector2(20,16); text.rectTransform.offsetMax=new Vector2(-20,-16);
        var view=template.gameObject.AddComponent<SpeechBubbleView>(); Set(view,"bubbleImage",image); Set(view,"dialogueText",text); Set(view,"worldCamera",camera);
        template.gameObject.SetActive(false);
        Set(bubbles,"speechBubblePrefab",view); Set(bubbles,"bubbleRoot",layer); Set(bubbles,"worldCamera",camera); Set(bubbles,"rootCanvas",canvas);
        Set(manager,"bubblePresenter",bubbles); return manager;
    }

    /// <summary>화자별로 명시적 말풍선 기준점을 연결한다.</summary>
    private static DialogueSpeaker Speaker(Transform parent,string tag, Vector3? offset=null)
    {
        var go=new GameObject(tag+"_말풍선기준"); go.transform.SetParent(parent,false); go.transform.localPosition=offset??new Vector3(0,1.7f,0);
        var speaker=go.AddComponent<DialogueSpeaker>(); Set(speaker,"speakerTag",tag); Set(speaker,"speechBubbleAnchor",go.transform); return speaker;
    }

    /// <summary>편집 시 UI 계층의 RectTransform을 생성한다.</summary>
    private static RectTransform Rect(string name,Transform parent,Vector2 size)
    { var go=new GameObject(name,typeof(RectTransform)); var rect=(RectTransform)go.transform; rect.SetParent(parent,false); rect.sizeDelta=size; return rect; }

    /// <summary>필수 한국어 폰트를 명시적으로 사용하는 텍스트를 생성한다.</summary>
    private static TMP_Text Text(string name,Transform parent,TMP_FontAsset font,float size,Vector2 bounds,Vector2 position)
    {
        var rect=Rect(name,parent,bounds); rect.anchoredPosition=position;
        var text=rect.gameObject.AddComponent<TextMeshProUGUI>(); text.font=font; text.fontSize=size; text.color=new Color(.9f,.91f,.88f); text.raycastTarget=false; text.textWrappingMode=TextWrappingModes.Normal; text.overflowMode=TextOverflowModes.Truncate;
        return text;
    }

    /// <summary>허가된 초안 작성에서만 직렬화 값과 필수 연결을 저장한다.</summary>
    private static void Set(UnityEngine.Object target,string field,object value)
    {
        var so=new SerializedObject(target); var property=so.FindProperty(field);
        if(property==null)throw new InvalidOperationException($"{target.name}.{field} 필드가 없습니다.");
        if(value is int i)property.intValue=i;
        else if(value is bool b)property.boolValue=b;
        else if(value is string s)property.stringValue=s;
        else if(value is Vector2 v)property.vector2Value=v;
        else if(value is GridPosition p){property.FindPropertyRelative("x").intValue=p.x;property.FindPropertyRelative("y").intValue=p.y;}
        else property.objectReferenceValue=value as UnityEngine.Object;
        so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(target);
    }
}

