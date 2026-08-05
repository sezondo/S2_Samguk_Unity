using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// BattleTest 씬의 단일 Manager 컴포넌트를 책임별 시스템 오브젝트로 분리하고 시야 구성을 통일한다.
/// 기존 컴포넌트 참조는 새 컴포넌트로 치환한 뒤 원본 Manager를 제거한다.
/// </summary>
public static class BattleSceneHierarchyOrganizer
{
    private const string BattleTest01Path = "Assets/Scenes/Test/BattleTest01.unity";
    private const string BattleTest02Path = "Assets/Scenes/Test/BattleTest02.unity";
    private const string EugeneDataPath = "Assets/Data/TestStage/Player/유진 테스트 데이터.asset";
    private const string AllyDataPath = "Assets/Data/TestStage/Player/동료 테스트 데이터.asset";

    private static readonly string[] ScenePaths =
    {
        BattleTest01Path,
        BattleTest02Path,
    };

    private static readonly string[] GroupNames =
    {
        "BattleCore",
        "PlayerSystem",
        "EnemySystem",
        "BattleStage",
        "BattlePresentation",
        "BattleFlow",
        "BattleVision",
        "BattleDebug",
    };

    /// <summary>
    /// 메뉴에서 두 Battle 씬의 시스템 Hierarchy를 정리한다.
    /// </summary>
    [MenuItem("Tools/S2-T/Battle/두 전투 씬 Hierarchy 정리")]
    public static void OrganizeFromMenu()
    {
        if (!EditorUtility.DisplayDialog(
                "Battle 씬 Hierarchy 정리",
                "BattleTest01과 BattleTest02의 Manager 컴포넌트를 책임별로 분리하고 시야 설정을 통일합니다.",
                "정리",
                "취소"))
        {
            return;
        }

        RunBatch();
        EditorUtility.DisplayDialog("Battle 씬 Hierarchy 정리", "두 Battle 씬 정리와 검증을 완료했습니다.", "확인");
    }

    /// <summary>
    /// 배치 모드에서 두 Battle 씬을 정리하고 오류가 있으면 예외를 발생시킨다.
    /// </summary>
    public static void RunBatch()
    {
        ConfigurePlayerVisionRanges();

        for (int i = 0; i < ScenePaths.Length; i++)
        {
            OrganizeScene(ScenePaths[i]);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("BattleSceneHierarchyOrganizer: 두 Battle 씬의 Hierarchy·시야 설정 정리를 완료했습니다.");
    }

    /// <summary>
    /// 일반 Unity 에디터 프로세스에서 정리를 실행하고 결과 코드와 함께 에디터를 종료한다.
    /// </summary>
    public static void RunAndExit()
    {
        try
        {
            RunBatch();
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    /// <summary>
    /// 지정한 Battle 씬을 열어 시스템 오브젝트 분리, 시야 구성과 검증을 수행한다.
    /// </summary>
    private static void OrganizeScene(string scenePath)
    {
        if (!File.Exists(scenePath))
        {
            throw new FileNotFoundException($"Battle 씬을 찾지 못했습니다: {scenePath}", scenePath);
        }

        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        GameObject managerObject = FindSceneObject(scene, "Manager");
        GameObject battleRoot = FindSceneObject(scene, "BattleRoot");

        if (managerObject != null && battleRoot != null)
        {
            throw new InvalidOperationException($"{scenePath}에 기존 Manager와 BattleRoot가 함께 있어 안전하게 정리할 수 없습니다.");
        }

        if (managerObject != null)
        {
            battleRoot = CreateSceneObject(scene, "BattleRoot", null);
            Dictionary<string, GameObject> groups = CreateGroups(scene, battleRoot.transform);
            MoveManagerComponents(scene, managerObject, groups);
            MoveExistingSupportObjects(scene, managerObject, groups);
            UnityEngine.Object.DestroyImmediate(managerObject);
        }
        else if (battleRoot == null)
        {
            throw new MissingReferenceException($"{scenePath}에 정리할 Manager 또는 기존 BattleRoot가 없습니다.");
        }

        Dictionary<string, GameObject> organizedGroups = GetExistingGroups(battleRoot);
        EnsureLogicTilemaps(scene, organizedGroups);
        EnsureVisionSystem(scene, organizedGroups);
        ValidateScene(scene, organizedGroups);

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, scenePath))
        {
            throw new IOException($"정리한 Battle 씬을 저장하지 못했습니다: {scenePath}");
        }

        Debug.Log($"BattleSceneHierarchyOrganizer: {scenePath} 정리와 검증을 완료했습니다.");
    }

    /// <summary>
    /// 기존 Manager의 모든 MonoBehaviour를 책임별 오브젝트로 복사하고 씬 참조를 새 컴포넌트로 치환한다.
    /// </summary>
    private static void MoveManagerComponents(
        Scene scene,
        GameObject managerObject,
        IReadOnlyDictionary<string, GameObject> groups)
    {
        MonoBehaviour[] sourceComponents = managerObject.GetComponents<MonoBehaviour>();
        if (sourceComponents.Any(component => component == null))
        {
            throw new MissingReferenceException($"{scene.path}의 Manager에 Missing Script가 있어 컴포넌트를 옮길 수 없습니다.");
        }

        Dictionary<UnityEngine.Object, UnityEngine.Object> replacements = new();
        for (int i = 0; i < sourceComponents.Length; i++)
        {
            MonoBehaviour source = sourceComponents[i];
            string groupName = GetGroupName(source.GetType());
            GameObject destination = groups[groupName];
            Component copied = CopyComponentTo(source, destination);
            replacements[source] = copied;
        }

        ReplaceSceneObjectReferences(scene, replacements);

        for (int i = 0; i < sourceComponents.Length; i++)
        {
            UnityEngine.Object.DestroyImmediate(sourceComponents[i]);
        }
    }

    /// <summary>
    /// 컴포넌트 타입에 대응하는 BattleRoot 하위 책임 그룹 이름을 반환한다.
    /// </summary>
    private static string GetGroupName(Type componentType)
    {
        if (componentType == typeof(GridManager) ||
            componentType == typeof(TurnManager) ||
            componentType == typeof(TacticalUnitRegistry) ||
            componentType == typeof(EnemyRegistry) ||
            componentType == typeof(HackableRegistry))
        {
            return "BattleCore";
        }

        if (componentType == typeof(PlayerInputReader) ||
            componentType == typeof(PlayerUnitControlManager) ||
            componentType == typeof(PlayerUnitInputController) ||
            componentType == typeof(PlayerUnitActionFlowController))
        {
            return "PlayerSystem";
        }

        if (componentType == typeof(EnemyAlertCoordinator) ||
            componentType == typeof(EnemyTurnCoordinator))
        {
            return "EnemySystem";
        }

        if (componentType == typeof(StageFailureCoordinator))
        {
            return "BattleStage";
        }

        if (componentType == typeof(ActorPresentationRegistry) ||
            componentType == typeof(CombatActionPresenter) ||
            componentType == typeof(VfxManager))
        {
            return "BattlePresentation";
        }

        if (componentType == typeof(BattleIntroPresenter) ||
            componentType == typeof(BattleEntryCoordinator) ||
            componentType == typeof(BattleCampaignBridge))
        {
            return "BattleFlow";
        }

        if (componentType == typeof(PlayerVisionContext) ||
            componentType == typeof(PlayerVisionManager) ||
            componentType == typeof(PlayerVisionPresenter))
        {
            return "BattleVision";
        }

        throw new InvalidOperationException($"Manager의 {componentType.Name} 컴포넌트에 대응하는 책임 그룹이 없습니다.");
    }

    /// <summary>
    /// 기존 컴포넌트의 직렬화 값을 새 GameObject의 동일 타입 컴포넌트로 복사한다.
    /// </summary>
    private static Component CopyComponentTo(Component source, GameObject destination)
    {
        int countBeforeCopy = destination.GetComponents(source.GetType()).Length;
        if (!ComponentUtility.CopyComponent(source) || !ComponentUtility.PasteComponentAsNew(destination))
        {
            throw new InvalidOperationException($"{source.GetType().Name} 컴포넌트를 {destination.name}에 복사하지 못했습니다.");
        }

        Component[] componentsAfterCopy = destination.GetComponents(source.GetType());
        if (componentsAfterCopy.Length != countBeforeCopy + 1)
        {
            throw new InvalidOperationException($"{destination.name}의 {source.GetType().Name} 복사 개수가 예상과 다릅니다.");
        }

        return componentsAfterCopy[componentsAfterCopy.Length - 1];
    }

    /// <summary>
    /// 씬의 모든 직렬화 Object 참조에서 기존 컴포넌트를 새 컴포넌트로 치환한다.
    /// </summary>
    private static void ReplaceSceneObjectReferences(
        Scene scene,
        IReadOnlyDictionary<UnityEngine.Object, UnityEngine.Object> replacements)
    {
        Component[] sceneComponents = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Component>(true))
            .Where(component => component != null)
            .ToArray();

        for (int i = 0; i < sceneComponents.Length; i++)
        {
            SerializedObject serializedObject = new(sceneComponents[i]);
            SerializedProperty iterator = serializedObject.GetIterator();
            bool changed = false;
            bool enterChildren = true;

            while (iterator.Next(enterChildren))
            {
                enterChildren = true;
                if (iterator.propertyType != SerializedPropertyType.ObjectReference ||
                    iterator.objectReferenceValue == null ||
                    !replacements.TryGetValue(iterator.objectReferenceValue, out UnityEngine.Object replacement))
                {
                    continue;
                }

                iterator.objectReferenceValue = replacement;
                changed = true;
            }

            if (changed)
            {
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }

    /// <summary>
    /// 기존 Queue, Debug와 Fog Root를 대응하는 책임 그룹 아래로 옮긴다.
    /// </summary>
    private static void MoveExistingSupportObjects(
        Scene scene,
        GameObject managerObject,
        IReadOnlyDictionary<string, GameObject> groups)
    {
        GameObject queueObject = FindSceneObject(scene, "ActionPresentationQueue");
        if (queueObject == null || queueObject.GetComponent<ActionPresentationQueue>() == null)
        {
            throw new MissingReferenceException($"{scene.path}에서 기존 ActionPresentationQueue를 찾지 못했습니다.");
        }

        SetParentAndReset(queueObject.transform, groups["BattlePresentation"].transform);

        GameObject debugObject = FindSceneObject(scene, "Debug");
        if (debugObject == null)
        {
            throw new MissingReferenceException($"{scene.path}에서 기존 Debug 오브젝트를 찾지 못했습니다.");
        }

        SetParentAndReset(debugObject.transform, groups["BattleDebug"].transform);

        Transform fogRoot = managerObject.transform.Find("PlayerVisionFogRoot");
        if (fogRoot != null)
        {
            SetParentAndReset(fogRoot, groups["BattleVision"].transform);
        }

        if (managerObject.transform.childCount > 0)
        {
            throw new InvalidOperationException($"{scene.path}의 기존 Manager에 분류하지 않은 자식 오브젝트가 남아 있습니다.");
        }
    }

    /// <summary>
    /// BattleVision 그룹에 시야 컴포넌트와 Fog Root를 보장하고 모든 필수 참조를 연결한다.
    /// </summary>
    private static void EnsureVisionSystem(Scene scene, IReadOnlyDictionary<string, GameObject> groups)
    {
        GameObject visionObject = groups["BattleVision"];
        PlayerVisionPresenter presenter = GetOrAddSingleComponent<PlayerVisionPresenter>(visionObject);
        PlayerVisionManager manager = GetOrAddSingleComponent<PlayerVisionManager>(visionObject);
        PlayerVisionContext context = GetOrAddSingleComponent<PlayerVisionContext>(visionObject);

        Transform fogRoot = visionObject.transform.Find("PlayerVisionFogRoot");
        if (fogRoot == null)
        {
            fogRoot = CreateSceneObject(scene, "PlayerVisionFogRoot", visionObject.transform).transform;
        }

        GameObject queueObject = FindSceneObject(scene, "ActionPresentationQueue");
        ActionPresentationQueue queue = queueObject != null
            ? queueObject.GetComponent<ActionPresentationQueue>()
            : null;

        SetObjectReference(manager, "context", context);
        SetObjectReference(presenter, "context", context);

        SetObjectReference(context, "gridManager", groups["BattleCore"].GetComponent<GridManager>());
        SetObjectReference(context, "tacticalUnitRegistry", groups["BattleCore"].GetComponent<TacticalUnitRegistry>());
        SetObjectReference(context, "enemyRegistry", groups["BattleCore"].GetComponent<EnemyRegistry>());
        SetObjectReference(context, "actorPresentationRegistry", groups["BattlePresentation"].GetComponent<ActorPresentationRegistry>());
        SetObjectReference(context, "presentationQueue", queue);
        SetObjectReference(context, "playerVisionManager", manager);
        SetObjectReference(context, "playerVisionPresenter", presenter);
        SetObjectReference(context, "fogRoot", fogRoot);
    }

    /// <summary>
    /// 기존 논리 타일맵을 벽 전용으로 보존하고 이동만 막는 낮은 장애물 타일맵을 별도로 연결한다.
    /// </summary>
    private static void EnsureLogicTilemaps(Scene scene, IReadOnlyDictionary<string, GameObject> groups)
    {
        GameObject mapGrid = FindSceneObject(scene, "MapVisualGrid");
        if (mapGrid == null)
        {
            throw new MissingReferenceException($"{scene.path}에서 MapVisualGrid를 찾지 못했습니다.");
        }

        Transform wallTransform = mapGrid.transform.Find("WallLogicTilemap");
        if (wallTransform == null)
        {
            wallTransform = mapGrid.transform.Find("LogicTilemap");
            if (wallTransform == null)
            {
                throw new MissingReferenceException($"{scene.path}에서 기존 LogicTilemap을 찾지 못했습니다.");
            }

            wallTransform.name = "WallLogicTilemap";
        }

        Tilemap wallTilemap = wallTransform.GetComponent<Tilemap>();
        if (wallTilemap == null)
        {
            throw new MissingReferenceException($"{scene.path}의 WallLogicTilemap에 Tilemap 컴포넌트가 없습니다.");
        }

        Transform lowObstacleTransform = mapGrid.transform.Find("LowObstacleLogicTilemap");
        if (lowObstacleTransform == null)
        {
            GameObject lowObstacleObject = CreateSceneObject(scene, "LowObstacleLogicTilemap", mapGrid.transform);
            lowObstacleObject.layer = wallTransform.gameObject.layer;
            lowObstacleObject.AddComponent<Tilemap>();

            TilemapRenderer wallRenderer = wallTransform.GetComponent<TilemapRenderer>();
            if (wallRenderer != null &&
                ComponentUtility.CopyComponent(wallRenderer) &&
                ComponentUtility.PasteComponentAsNew(lowObstacleObject))
            {
                // 기존 논리 타일맵과 같은 편집 표시 설정을 사용한다.
            }
            else
            {
                lowObstacleObject.AddComponent<TilemapRenderer>();
            }

            lowObstacleTransform = lowObstacleObject.transform;
        }

        Tilemap lowObstacleTilemap = lowObstacleTransform.GetComponent<Tilemap>();
        if (lowObstacleTilemap == null)
        {
            throw new MissingReferenceException($"{scene.path}의 LowObstacleLogicTilemap에 Tilemap 컴포넌트가 없습니다.");
        }

        GridManager gridManager = groups["BattleCore"].GetComponent<GridManager>();
        SetObjectReference(gridManager, "wallLogicTilemap", wallTilemap);
        SetObjectReference(gridManager, "lowObstacleLogicTilemap", lowObstacleTilemap);
    }

    /// <summary>
    /// 현재 씬 구조, 컴포넌트 배치, 시야 필수 참조와 Missing Script를 검증한다.
    /// </summary>
    private static void ValidateScene(Scene scene, IReadOnlyDictionary<string, GameObject> groups)
    {
        foreach (KeyValuePair<string, Type[]> expected in GetExpectedComponents())
        {
            GameObject group = groups[expected.Key];
            for (int i = 0; i < expected.Value.Length; i++)
            {
                Type componentType = expected.Value[i];
                if (group.GetComponents(componentType).Length != 1)
                {
                    throw new InvalidOperationException($"{scene.path}의 {group.name}에는 {componentType.Name}이 정확히 1개 있어야 합니다.");
                }
            }
        }

        GameObject queueObject = FindSceneObject(scene, "ActionPresentationQueue");
        if (queueObject == null || queueObject.transform.parent != groups["BattlePresentation"].transform)
        {
            throw new InvalidOperationException($"{scene.path}의 ActionPresentationQueue가 BattlePresentation 아래에 있지 않습니다.");
        }

        GameObject debugObject = FindSceneObject(scene, "Debug");
        if (debugObject == null || debugObject.transform.parent != groups["BattleDebug"].transform)
        {
            throw new InvalidOperationException($"{scene.path}의 Debug가 BattleDebug 아래에 있지 않습니다.");
        }

        ValidateLogicTilemaps(scene, groups);

        PlayerVisionContext visionContext = groups["BattleVision"].GetComponent<PlayerVisionContext>();
        PlayerVisionManager visionManager = groups["BattleVision"].GetComponent<PlayerVisionManager>();
        PlayerVisionPresenter visionPresenter = groups["BattleVision"].GetComponent<PlayerVisionPresenter>();
        if (!visionContext.HasValidReference() ||
            !visionManager.HasValidReference() ||
            !visionPresenter.HasValidReference() ||
            !visionPresenter.HasValidData())
        {
            throw new MissingReferenceException($"{scene.path}의 플레이어 시야 필수 참조나 데이터가 올바르지 않습니다.");
        }

        GameObject[] allObjects = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .Select(transform => transform.gameObject)
            .ToArray();
        for (int i = 0; i < allObjects.Length; i++)
        {
            int missingCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(allObjects[i]);
            if (missingCount > 0)
            {
                throw new MissingReferenceException($"{scene.path}의 {allObjects[i].name}에 Missing Script {missingCount}개가 있습니다.");
            }
        }
    }

    /// <summary>
    /// 두 논리 타일맵의 이름, 공통 Grid 부모와 GridManager 연결을 검증한다.
    /// </summary>
    private static void ValidateLogicTilemaps(Scene scene, IReadOnlyDictionary<string, GameObject> groups)
    {
        GameObject wallObject = FindSceneObject(scene, "WallLogicTilemap");
        GameObject lowObstacleObject = FindSceneObject(scene, "LowObstacleLogicTilemap");
        if (wallObject == null || lowObstacleObject == null ||
            wallObject.transform.parent != lowObstacleObject.transform.parent)
        {
            throw new MissingReferenceException($"{scene.path}의 벽·낮은 장애물 논리 타일맵 구성이 올바르지 않습니다.");
        }

        GridManager gridManager = groups["BattleCore"].GetComponent<GridManager>();
        if (gridManager.WallLogicTilemap != wallObject.GetComponent<Tilemap>() ||
            gridManager.LowObstacleLogicTilemap != lowObstacleObject.GetComponent<Tilemap>())
        {
            throw new MissingReferenceException($"{scene.path}의 GridManager 논리 타일맵 참조가 올바르지 않습니다.");
        }
    }

    /// <summary>
    /// 책임 그룹별로 반드시 하나씩 존재해야 하는 컴포넌트 타입을 반환한다.
    /// </summary>
    private static IReadOnlyDictionary<string, Type[]> GetExpectedComponents()
    {
        return new Dictionary<string, Type[]>
        {
            ["BattleCore"] = new[]
            {
                typeof(GridManager), typeof(TurnManager), typeof(TacticalUnitRegistry),
                typeof(EnemyRegistry), typeof(HackableRegistry),
            },
            ["PlayerSystem"] = new[]
            {
                typeof(PlayerInputReader), typeof(PlayerUnitControlManager),
                typeof(PlayerUnitInputController), typeof(PlayerUnitActionFlowController),
            },
            ["EnemySystem"] = new[] { typeof(EnemyAlertCoordinator), typeof(EnemyTurnCoordinator) },
            ["BattleStage"] = new[] { typeof(StageFailureCoordinator) },
            ["BattlePresentation"] = new[]
            {
                typeof(ActorPresentationRegistry), typeof(CombatActionPresenter), typeof(VfxManager),
            },
            ["BattleFlow"] = new[]
            {
                typeof(BattleIntroPresenter), typeof(BattleEntryCoordinator), typeof(BattleCampaignBridge),
            },
            ["BattleVision"] = new[]
            {
                typeof(PlayerVisionContext), typeof(PlayerVisionManager), typeof(PlayerVisionPresenter),
            },
            ["BattleDebug"] = Array.Empty<Type>(),
        };
    }

    /// <summary>
    /// 두 플레이어 테스트 데이터에 시야 거리를 명시적으로 6칸으로 저장한다.
    /// </summary>
    private static void ConfigurePlayerVisionRanges()
    {
        SetUnitDataInt(EugeneDataPath, "visionRange", 6);
        SetUnitDataInt(AllyDataPath, "visionRange", 6);
        SetUnitDataInt(EugeneDataPath, "swordVisionRange", 3);
        SetUnitDataInt(AllyDataPath, "swordVisionRange", 3);
    }

    /// <summary>
    /// 지정한 플레이어 데이터 에셋의 정수 필드를 직렬화 값으로 저장한다.
    /// </summary>
    private static void SetUnitDataInt(string assetPath, string propertyName, int value)
    {
        ControllableUnitData data = AssetDatabase.LoadAssetAtPath<ControllableUnitData>(assetPath);
        if (data == null)
        {
            throw new FileNotFoundException($"플레이어 테스트 데이터 에셋을 찾지 못했습니다: {assetPath}", assetPath);
        }

        SerializedObject serializedObject = new(data);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
        {
            throw new MissingFieldException(typeof(ControllableUnitData).Name, propertyName);
        }

        property.intValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(data);
    }

    /// <summary>
    /// BattleRoot와 책임 그룹 GameObject를 새로 만든다.
    /// </summary>
    private static Dictionary<string, GameObject> CreateGroups(Scene scene, Transform battleRoot)
    {
        Dictionary<string, GameObject> groups = new();
        for (int i = 0; i < GroupNames.Length; i++)
        {
            groups[GroupNames[i]] = CreateSceneObject(scene, GroupNames[i], battleRoot);
        }

        return groups;
    }

    /// <summary>
    /// 기존 BattleRoot에서 필수 책임 그룹을 이름으로 찾아 반환한다.
    /// </summary>
    private static Dictionary<string, GameObject> GetExistingGroups(GameObject battleRoot)
    {
        Dictionary<string, GameObject> groups = new();
        for (int i = 0; i < GroupNames.Length; i++)
        {
            Transform child = battleRoot.transform.Find(GroupNames[i]);
            if (child == null)
            {
                throw new MissingReferenceException($"BattleRoot 아래에 {GroupNames[i]} 그룹이 없습니다.");
            }

            groups[GroupNames[i]] = child.gameObject;
        }

        return groups;
    }

    /// <summary>
    /// 새 씬 GameObject를 만들고 지정한 부모 아래에 Transform 기본값으로 배치한다.
    /// </summary>
    private static GameObject CreateSceneObject(Scene scene, string objectName, Transform parent)
    {
        GameObject created = new(objectName);
        SceneManager.MoveGameObjectToScene(created, scene);
        SetParentAndReset(created.transform, parent);
        return created;
    }

    /// <summary>
    /// Transform을 지정한 부모 아래로 옮기고 로컬 Transform을 기본값으로 맞춘다.
    /// </summary>
    private static void SetParentAndReset(Transform target, Transform parent)
    {
        target.SetParent(parent, false);
        target.localPosition = Vector3.zero;
        target.localRotation = Quaternion.identity;
        target.localScale = Vector3.one;
    }

    /// <summary>
    /// 씬 전체에서 지정한 이름의 GameObject 하나를 찾고 중복이면 오류를 발생시킨다.
    /// </summary>
    private static GameObject FindSceneObject(Scene scene, string objectName)
    {
        GameObject[] matches = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .Where(transform => transform.name == objectName)
            .Select(transform => transform.gameObject)
            .ToArray();

        if (matches.Length > 1)
        {
            throw new InvalidOperationException($"{scene.path}에 이름이 {objectName}인 GameObject가 {matches.Length}개 있습니다.");
        }

        return matches.Length == 1 ? matches[0] : null;
    }

    /// <summary>
    /// GameObject에 지정한 컴포넌트가 하나만 존재하도록 가져오거나 추가한다.
    /// </summary>
    private static T GetOrAddSingleComponent<T>(GameObject target) where T : Component
    {
        T[] components = target.GetComponents<T>();
        if (components.Length > 1)
        {
            throw new InvalidOperationException($"{target.name}에 {typeof(T).Name} 컴포넌트가 중복되어 있습니다.");
        }

        return components.Length == 1 ? components[0] : target.AddComponent<T>();
    }

    /// <summary>
    /// 지정한 컴포넌트의 직렬화 Object 참조 필드를 설정한다.
    /// </summary>
    private static void SetObjectReference(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
    {
        if (target == null || value == null)
        {
            throw new MissingReferenceException($"{propertyName} 필드에 설정할 대상 또는 값이 없습니다.");
        }

        SerializedObject serializedObject = new(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
        {
            throw new MissingFieldException(target.GetType().Name, propertyName);
        }

        property.objectReferenceValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }
}
