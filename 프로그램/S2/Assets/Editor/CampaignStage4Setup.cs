#if UNITY_EDITOR
using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// 캠페인 4단계 테스트 데이터와 Story·Battle 씬 연결을 일괄 구성하는 일회성 편집 도구다.
/// </summary>
public static class CampaignStage4Setup
{
    private const string StoryDataFolder = "Assets/Data/Story/CampaignPrototype";
    private const string BattleIntroFolder = "Assets/Data/Campaign/BattleIntro";

    /// <summary>
    /// 테스트 Story·입장 연출 데이터를 만들고 캠페인과 세 테스트 씬의 참조를 연결한다.
    /// </summary>
    [MenuItem("Tools/S2/Apply Campaign Stage 4 Setup")]
    public static void Apply()
    {
        EnsureFolder(StoryDataFolder);
        EnsureFolder(BattleIntroFolder);

        StorySequenceData stage11Pre = CreateStory(
            $"{StoryDataFolder}/Stage_1-1_Pre.asset",
            "1-1-pre",
            "1-1 전투 전",
            ("유진", "창고 안의 감시를 피해서 목표 지점까지 이동한다. 들키더라도 멈추지는 마."),
            ("금두꺼비", "작전 경로를 기록했습니다. 모든 조작 유닛이 쓰러지면 작전은 실패합니다."));
        StorySequenceData stage11Post = CreateStory(
            $"{StoryDataFolder}/Stage_1-1_Post.asset",
            "1-1-post",
            "1-1 전투 후",
            ("유진", "첫 번째 길은 열었어. 다음 구역도 같은 방식으로 돌파한다."),
            ("금두꺼비", "전투 기록 저장 완료. 다음 스테이지가 개방됩니다."));
        StorySequenceData stage12Pre = CreateStory(
            $"{StoryDataFolder}/Stage_1-2_Pre.asset",
            "1-2-pre",
            "1-2 전투 전",
            ("유진", "이번에는 경비가 더 많아. 행동 순서와 남은 AP를 확인하면서 움직이자."),
            ("금두꺼비", "후일담이 없는 테스트 스테이지입니다. 승리 결과 확인 뒤 로비로 복귀합니다."));

        ConfigureCampaignData(stage11Pre, stage11Post, stage12Pre);
        ConfigureStoryScene();
        ConfigureBattleScene("Assets/Scenes/Test/BattleTest01.unity", "battle-1-1-intro", "1-1 작전 개시");
        ConfigureBattleScene("Assets/Scenes/Test/BattleTest02.unity", "battle-1-2-intro", "1-2 작전 개시");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"{nameof(CampaignStage4Setup)}: 캠페인 4단계 데이터와 씬 연결을 완료했습니다.");
    }

    /// <summary>
    /// 캠페인 4단계 데이터 규칙과 Story·Battle 씬의 필수 직렬화 참조를 다시 검사한다.
    /// </summary>
    public static void Validate()
    {
        CampaignData campaign = AssetDatabase.LoadAssetAtPath<CampaignData>("Assets/Data/Campaign/CampaignData.asset");
        if (campaign == null || campaign.StorySceneName != "StoryTest" || campaign.Stages == null || campaign.Stages.Length == 0)
        {
            throw new InvalidDataException("CampaignData의 Story 씬 또는 스테이지 목록이 올바르지 않습니다.");
        }

        for (int i = 0; i < campaign.Stages.Length; i++)
        {
            StageDefinitionData stage = campaign.Stages[i];
            if (stage == null || stage.PreBattleStory == null ||
                stage.HasPostBattleStory != (stage.PostBattleStory != null))
            {
                throw new InvalidDataException($"{i}번 스테이지의 전투 전 Story 또는 Bool 기준 후일담 구성이 올바르지 않습니다.");
            }
        }

        ValidateStoryScene();
        ValidateBattleScene("Assets/Scenes/Test/BattleTest01.unity");
        ValidateBattleScene("Assets/Scenes/Test/BattleTest02.unity");
        Debug.Log($"{nameof(CampaignStage4Setup)}: 캠페인 4단계 데이터와 씬 참조 검증을 통과했습니다.");
    }

    /// <summary>
    /// 대사 두 줄과 짧은 대기로 구성된 프로토타입 Story 에셋을 생성하거나 갱신한다.
    /// </summary>
    private static StorySequenceData CreateStory(
        string assetPath,
        string storyId,
        string displayName,
        params (string speaker, string text)[] lines)
    {
        StorySequenceData asset = AssetDatabase.LoadAssetAtPath<StorySequenceData>(assetPath);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<StorySequenceData>();
            AssetDatabase.CreateAsset(asset, assetPath);
        }

        SerializedObject serialized = new(asset);
        serialized.FindProperty("storyId").stringValue = storyId;
        serialized.FindProperty("displayName").stringValue = displayName;
        SerializedProperty commands = serialized.FindProperty("commands");
        commands.arraySize = lines.Length + 1;

        for (int i = 0; i < lines.Length; i++)
        {
            SerializedProperty command = commands.GetArrayElementAtIndex(i);
            command.FindPropertyRelative("commandType").enumValueIndex = (int)StoryCommandType.Dialogue;
            command.FindPropertyRelative("speakerName").stringValue = lines[i].speaker;
            command.FindPropertyRelative("dialogueText").stringValue = lines[i].text;
            command.FindPropertyRelative("useTypewriter").boolValue = true;
            command.FindPropertyRelative("charactersPerSecond").floatValue = 34f;
        }

        SerializedProperty wait = commands.GetArrayElementAtIndex(lines.Length);
        wait.FindPropertyRelative("commandType").enumValueIndex = (int)StoryCommandType.Wait;
        wait.FindPropertyRelative("duration").floatValue = 0.25f;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);
        return asset;
    }

    /// <summary>
    /// 공통 Story 씬과 각 스테이지의 전투 전·후 Story 참조를 캠페인 데이터에 연결한다.
    /// </summary>
    private static void ConfigureCampaignData(
        StorySequenceData stage11Pre,
        StorySequenceData stage11Post,
        StorySequenceData stage12Pre)
    {
        CampaignData campaign = AssetDatabase.LoadAssetAtPath<CampaignData>("Assets/Data/Campaign/CampaignData.asset");
        StageDefinitionData stage11 = AssetDatabase.LoadAssetAtPath<StageDefinitionData>("Assets/Data/Campaign/Stage_1-1.asset");
        StageDefinitionData stage12 = AssetDatabase.LoadAssetAtPath<StageDefinitionData>("Assets/Data/Campaign/Stage_1-2.asset");
        if (campaign == null || stage11 == null || stage12 == null)
        {
            throw new InvalidDataException("캠페인 또는 스테이지 데이터 에셋을 찾지 못했습니다.");
        }

        SerializedObject campaignSerialized = new(campaign);
        campaignSerialized.FindProperty("storySceneName").stringValue = "StoryTest";
        campaignSerialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(campaign);

        ConfigureStage(stage11, stage11Pre, true, stage11Post);
        ConfigureStage(stage12, stage12Pre, false, null);
    }

    /// <summary>
    /// 스테이지 하나의 전투 전 Story와 Bool 기준 후일담 설정을 연결한다.
    /// </summary>
    private static void ConfigureStage(
        StageDefinitionData stage,
        StorySequenceData preBattleStory,
        bool hasPostBattleStory,
        StorySequenceData postBattleStory)
    {
        SerializedObject serialized = new(stage);
        serialized.FindProperty("preBattleStory").objectReferenceValue = preBattleStory;
        serialized.FindProperty("hasPostBattleStory").boolValue = hasPostBattleStory;
        serialized.FindProperty("postBattleStory").objectReferenceValue = postBattleStory;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(stage);
    }

    /// <summary>
    /// StoryTest의 기존 Runner를 캠페인 Story 브리지에 연결한다.
    /// </summary>
    private static void ConfigureStoryScene()
    {
        Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/Test/StoryTest.unity", OpenSceneMode.Single);
        StoryRunner runner = Object.FindFirstObjectByType<StoryRunner>(FindObjectsInactive.Include);
        if (runner == null)
        {
            throw new MissingReferenceException("StoryTest 씬에서 StoryRunner를 찾지 못했습니다.");
        }

        StoryCampaignBridge bridge = runner.GetComponent<StoryCampaignBridge>();
        if (bridge == null)
        {
            bridge = runner.gameObject.AddComponent<StoryCampaignBridge>();
        }

        SetReference(bridge, "runner", runner);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    /// <summary>
    /// StoryTest 씬의 캠페인 브리지와 누락 스크립트를 검사한다.
    /// </summary>
    private static void ValidateStoryScene()
    {
        Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/Test/StoryTest.unity", OpenSceneMode.Single);
        StoryCampaignBridge bridge = Object.FindFirstObjectByType<StoryCampaignBridge>(FindObjectsInactive.Include);
        if (bridge == null || !bridge.HasValidReference())
        {
            throw new MissingReferenceException("StoryTest 씬의 StoryCampaignBridge 참조가 올바르지 않습니다.");
        }

        ValidateMissingScripts(scene);
    }

    /// <summary>
    /// 전투 씬에 입장 연출·실패 판정·결과 UI·캠페인 브리지를 구성한다.
    /// </summary>
    private static void ConfigureBattleScene(
        string scenePath,
        string introId,
        string missionMessage)
    {
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        GameObject manager = GameObject.Find("Manager");
        Camera targetCamera = Object.FindFirstObjectByType<Camera>(FindObjectsInactive.Include);
        StageStateManager stageStateManager = Object.FindFirstObjectByType<StageStateManager>(FindObjectsInactive.Include);
        StageResultPresenter resultPresenter = Object.FindFirstObjectByType<StageResultPresenter>(FindObjectsInactive.Include);
        if (manager == null || targetCamera == null || stageStateManager == null || resultPresenter == null)
        {
            throw new MissingReferenceException($"{scenePath}에서 Manager, Camera, StageStateManager 또는 StageResultPresenter를 찾지 못했습니다.");
        }

        BattleIntroSequenceData intro = CreateBattleIntro(
            $"{BattleIntroFolder}/{Path.GetFileNameWithoutExtension(scenePath)}_Intro.asset",
            introId,
            missionMessage,
            targetCamera.transform.position);

        GameObject flowCanvas = GetOrCreateFlowCanvas();
        GameObject missionRoot = CreateOrResetPanel(flowCanvas.transform, "MissionMessageRoot", new Vector2(760f, 150f));
        TMP_Text missionText = CreateText(missionRoot.transform, "MissionMessageText", 34f, TextAlignmentOptions.Center);
        SetFullStretch(missionText.rectTransform, new Vector2(24f, 18f), new Vector2(-24f, -18f));

        GameObject resultRoot = CreateOrResetPanel(flowCanvas.transform, "StageResultRoot", new Vector2(680f, 380f));
        TMP_Text title = CreateText(resultRoot.transform, "TitleText", 48f, TextAlignmentOptions.Center);
        SetAnchoredRect(title.rectTransform, new Vector2(0f, 95f), new Vector2(600f, 80f));
        TMP_Text description = CreateText(resultRoot.transform, "DescriptionText", 25f, TextAlignmentOptions.Center);
        SetAnchoredRect(description.rectTransform, new Vector2(0f, 0f), new Vector2(600f, 120f));
        Button confirm = CreateButton(resultRoot.transform, "ConfirmButton", "확인");
        SetAnchoredRect((RectTransform)confirm.transform, new Vector2(0f, -115f), new Vector2(260f, 68f));
        resultRoot.SetActive(false);
        missionRoot.SetActive(false);

        BattleIntroPresenter introPresenter = GetOrAddComponent<BattleIntroPresenter>(manager);
        SetReference(introPresenter, "targetCamera", targetCamera);
        SetReference(introPresenter, "missionMessageRoot", missionRoot);
        SetReference(introPresenter, "missionMessageText", missionText);
        SetReference(introPresenter, "sequence", intro);

        GetOrAddComponent<BattleEntryCoordinator>(manager);
        StageFailureCoordinator failure = GetOrAddComponent<StageFailureCoordinator>(manager);
        SetReference(failure, "stageStateManager", stageStateManager);

        SetReference(resultPresenter, "resultRoot", resultRoot);
        SetReference(resultPresenter, "titleText", title);
        SetReference(resultPresenter, "descriptionText", description);
        SetReference(resultPresenter, "confirmButton", confirm);

        BattleCampaignBridge campaignBridge = GetOrAddComponent<BattleCampaignBridge>(manager);
        SetReference(campaignBridge, "resultPresenter", resultPresenter);

        if (Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include) == null)
        {
            throw new MissingReferenceException($"{scenePath}에 결과 버튼 입력을 받을 EventSystem이 없습니다.");
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    /// <summary>
    /// 전투 씬의 입장 연출·실패·결과·캠페인 연결과 기존 액터 루트 존재 여부를 검사한다.
    /// </summary>
    private static void ValidateBattleScene(string scenePath)
    {
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        BattleIntroPresenter intro = Object.FindFirstObjectByType<BattleIntroPresenter>(FindObjectsInactive.Include);
        StageFailureCoordinator failure = Object.FindFirstObjectByType<StageFailureCoordinator>(FindObjectsInactive.Include);
        StageResultPresenter result = Object.FindFirstObjectByType<StageResultPresenter>(FindObjectsInactive.Include);
        BattleCampaignBridge bridge = Object.FindFirstObjectByType<BattleCampaignBridge>(FindObjectsInactive.Include);
        BattleEntryCoordinator entry = Object.FindFirstObjectByType<BattleEntryCoordinator>(FindObjectsInactive.Include);
        if (intro == null || !intro.HasValidReference() || !intro.HasValidData() ||
            failure == null || !failure.HasValidReference() ||
            result == null || !result.HasValidReference() ||
            bridge == null || !bridge.HasValidReference() ||
            entry == null)
        {
            throw new MissingReferenceException($"{scenePath}의 캠페인 전투 흐름 참조가 올바르지 않습니다.");
        }

        if (!HasGameObjectNamed(scene, "Player Actor Root") ||
            !HasGameObjectNamed(scene, "Enemy") ||
            !HasGameObjectNamed(scene, "Enemy2"))
        {
            throw new MissingReferenceException($"{scenePath}의 기존 Player 또는 Enemy 오브젝트를 찾지 못했습니다.");
        }

        ValidateMissingScripts(scene);
    }

    /// <summary>
    /// 활성 여부와 관계없이 열린 씬에 지정 이름의 GameObject가 있는지 확인한다.
    /// </summary>
    private static bool HasGameObjectNamed(Scene scene, string objectName)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform[] transforms = roots[i].GetComponentsInChildren<Transform>(true);
            for (int j = 0; j < transforms.Length; j++)
            {
                if (transforms[j].name == objectName)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// 열린 씬의 모든 GameObject에 Missing Script 컴포넌트가 없는지 확인한다.
    /// </summary>
    private static void ValidateMissingScripts(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform[] transforms = roots[i].GetComponentsInChildren<Transform>(true);
            for (int j = 0; j < transforms.Length; j++)
            {
                int missingCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transforms[j].gameObject);
                if (missingCount > 0)
                {
                    throw new MissingReferenceException(
                        $"{scene.path}의 '{transforms[j].name}' 오브젝트에 Missing Script가 {missingCount}개 있습니다.");
                }
            }
        }
    }

    /// <summary>
    /// 간단한 미션 문구와 왕복 카메라 이동으로 구성된 입장 연출 에셋을 만든다.
    /// </summary>
    private static BattleIntroSequenceData CreateBattleIntro(
        string assetPath,
        string sequenceId,
        string missionMessage,
        Vector3 startCameraPosition)
    {
        BattleIntroSequenceData asset = AssetDatabase.LoadAssetAtPath<BattleIntroSequenceData>(assetPath);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<BattleIntroSequenceData>();
            AssetDatabase.CreateAsset(asset, assetPath);
        }

        SerializedObject serialized = new(asset);
        serialized.FindProperty("sequenceId").stringValue = sequenceId;
        serialized.FindProperty("displayName").stringValue = missionMessage;
        SerializedProperty commands = serialized.FindProperty("commands");
        commands.arraySize = 4;

        SetIntroCommand(commands.GetArrayElementAtIndex(0), BattleIntroCommandType.ShowMissionMessage, 1.5f, startCameraPosition, $"{missionMessage}\n목표 지점까지 이동하십시오.");
        SetIntroCommand(commands.GetArrayElementAtIndex(1), BattleIntroCommandType.MoveCamera, 0.8f, startCameraPosition + Vector3.right * 2f, null);
        SetIntroCommand(commands.GetArrayElementAtIndex(2), BattleIntroCommandType.Wait, 0.35f, startCameraPosition, null);
        SetIntroCommand(commands.GetArrayElementAtIndex(3), BattleIntroCommandType.MoveCamera, 0.8f, startCameraPosition, null);

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);
        return asset;
    }

    /// <summary>
    /// 직렬화된 입장 연출 명령 하나의 공통 값을 설정한다.
    /// </summary>
    private static void SetIntroCommand(
        SerializedProperty command,
        BattleIntroCommandType commandType,
        float duration,
        Vector3 targetPosition,
        string message)
    {
        command.FindPropertyRelative("commandType").enumValueIndex = (int)commandType;
        command.FindPropertyRelative("duration").floatValue = duration;
        command.FindPropertyRelative("cameraTargetPosition").vector3Value = targetPosition;
        command.FindPropertyRelative("missionMessage").stringValue = message ?? string.Empty;
        command.FindPropertyRelative("dialogueSequence").objectReferenceValue = null;
    }

    /// <summary>
    /// 전투 흐름 전용 화면 오버레이 Canvas를 생성하거나 기존 것을 재사용한다.
    /// </summary>
    private static GameObject GetOrCreateFlowCanvas()
    {
        GameObject root = GameObject.Find("CampaignBattleFlowCanvas");
        if (root == null)
        {
            root = new GameObject(
                "CampaignBattleFlowCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
        }

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        return root;
    }

    /// <summary>
    /// 지정한 이름의 UI 패널을 새로 구성해 반환한다.
    /// </summary>
    private static GameObject CreateOrResetPanel(Transform parent, string objectName, Vector2 size)
    {
        Transform existing = parent.Find(objectName);
        if (existing != null)
        {
            Object.DestroyImmediate(existing.gameObject);
        }

        GameObject panel = new(objectName, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)panel.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = Vector2.zero;
        panel.GetComponent<Image>().color = new Color(0.035f, 0.045f, 0.065f, 0.94f);
        return panel;
    }

    /// <summary>
    /// 기본 TMP 폰트를 사용하는 UI 텍스트를 만든다.
    /// </summary>
    private static TMP_Text CreateText(
        Transform parent,
        string objectName,
        float fontSize,
        TextAlignmentOptions alignment)
    {
        GameObject textObject = new(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = fontSize;
        text.color = Color.white;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.Normal;
        return text;
    }

    /// <summary>
    /// 임시 결과 확정용 버튼과 자식 텍스트를 만든다.
    /// </summary>
    private static Button CreateButton(Transform parent, string objectName, string label)
    {
        GameObject buttonObject = new(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.16f, 0.46f, 0.72f, 1f);
        Button button = buttonObject.GetComponent<Button>();
        TMP_Text text = CreateText(buttonObject.transform, "Label", 28f, TextAlignmentOptions.Center);
        text.text = label;
        SetFullStretch(text.rectTransform, Vector2.zero, Vector2.zero);
        return button;
    }

    /// <summary>
    /// RectTransform을 부모 중앙 기준 위치와 크기로 설정한다.
    /// </summary>
    private static void SetAnchoredRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    /// <summary>
    /// RectTransform이 부모 전체를 지정 여백만큼 채우게 설정한다.
    /// </summary>
    private static void SetFullStretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    /// <summary>
    /// 오브젝트에 지정 컴포넌트가 없으면 추가하고 반환한다.
    /// </summary>
    private static T GetOrAddComponent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : target.AddComponent<T>();
    }

    /// <summary>
    /// 컴포넌트의 지정 직렬화 필드에 Unity 오브젝트 참조를 기록한다.
    /// </summary>
    private static void SetReference(Object target, string propertyName, Object value)
    {
        SerializedObject serialized = new(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
        {
            throw new MissingFieldException(target.GetType().Name, propertyName);
        }

        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }

    /// <summary>
    /// 중첩된 Assets 폴더 경로가 모두 존재하도록 만든다.
    /// </summary>
    private static void EnsureFolder(string folderPath)
    {
        string[] parts = folderPath.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }
}
#endif
