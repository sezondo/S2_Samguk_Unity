using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>승인된 행동 가림막과 참조를 1-1 씬에 연결하고 조작 설명용 팝업을 정리한다.</summary>
public static class Stage11TutorialSpotlightAuthoring
{
    /// <summary>다른 참조·배치를 바꾸지 않고 현재 가림막의 검정 불투명도만 70%로 맞춘다.</summary>
    [MenuItem("Tools/S2/Set Spotlight Opacity 70 Percent")]
    public static void SetOpacity()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.name != "BattleTest01" || scene.isDirty)
            throw new InvalidOperationException("저장된 BattleTest01 편집 상태가 필요합니다.");
        var c = UnityEngine.Object.FindFirstObjectByType<BattleTutorialContext>();
        if (c == null || c.spotlight == null) throw new InvalidOperationException("튜토리얼 가림막 참조가 필요합니다.");
        Undo.RecordObject(c.spotlight,"가림막 반투명 조정");
        c.spotlight.color = new Color(0,0,0,.7f);
        EditorUtility.SetDirty(c.spotlight); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Debug.Log("행동 가림막의 불투명도를 70%로 조정했습니다.");
    }

    /// <summary>저장된 편집 씬에서만 가림막 Canvas와 필수 참조를 명시적으로 저장한다.</summary>
    [MenuItem("Tools/S2/Apply 1-1 Spotlight Guidance")]
    public static void Apply()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.name != "BattleTest01" || scene.isDirty)
            throw new InvalidOperationException("저장된 BattleTest01 편집 상태가 필요합니다.");
        var tutorial = UnityEngine.Object.FindFirstObjectByType<BattleTutorialController>();
        var hud = UnityEngine.Object.FindFirstObjectByType<BattleHudController>();
        if (tutorial == null || hud == null) throw new InvalidOperationException("튜토리얼과 HUD 연결이 필요합니다.");
        var c = tutorial.Context;
        Undo.RegisterFullObjectHierarchyUndo(c.gameObject, "행동 가림막 연결");
        Undo.RecordObject(tutorial.Data, "조작 설명 팝업 정리");
        c.selectedHud = (BattleHudSelectedUnitPanelController)new SerializedObject(hud).FindProperty("selectedUnitHudController").objectReferenceValue;
        var baseHud = (BattleHudBaseController)new SerializedObject(hud).FindProperty("baseHudController").objectReferenceValue;
        c.endTurnButton = (Button)new SerializedObject(baseHud).FindProperty("endTurnButton").objectReferenceValue;
        if (c.spotlight == null)
        {
            var root = new GameObject("TutorialSpotlightCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(c.transform, false);
            var rect = (RectTransform)root.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 4000;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920,1080); scaler.matchWidthOrHeight = .5f;
            var mask = new GameObject("ActionSpotlight", typeof(RectTransform), typeof(CanvasRenderer), typeof(BattleTutorialSpotlightGraphic));
            mask.transform.SetParent(root.transform, false);
            var maskRect = (RectTransform)mask.transform; maskRect.anchorMin = Vector2.zero; maskRect.anchorMax = Vector2.one;
            maskRect.offsetMin = maskRect.offsetMax = Vector2.zero;
            c.spotlight = mask.GetComponent<BattleTutorialSpotlightGraphic>();
        }
        var spotlightCanvas = c.spotlight.transform.parent.GetComponent<Canvas>();
        if (spotlightCanvas == null) throw new InvalidOperationException("가림막 Canvas가 필요합니다.");
        spotlightCanvas.sortingOrder = 4000; EditorUtility.SetDirty(spotlightCanvas);
        c.spotlight.color = new Color(0, 0, 0, .7f); c.spotlight.raycastTarget = true; c.spotlight.Hide();
        var presenter = new SerializedObject(c.presenter); presenter.FindProperty("context").objectReferenceValue = c; presenter.ApplyModifiedProperties();
        // 버튼/목표 가림막으로 대체한 조작 대사와 새 개념이 없는 탈출 수첩은 제거한다.
        tutorial.Data.steps = tutorial.Data.steps.Where(s => s.id != "09 이동" && !(s.kind == BattleTutorialStepKind.Guide && s.id.EndsWith(" 탈출"))).ToArray();
        EditorUtility.SetDirty(c); EditorUtility.SetDirty(tutorial.Data);
        if (!c.presenter.HasValidReference() || !tutorial.HasValidReference()) throw new InvalidOperationException("가림막 연결 검사 실패");
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        Debug.Log("1-1 행동 가림막과 최초 개념 수첩 안내를 연결했습니다.");
    }
}
