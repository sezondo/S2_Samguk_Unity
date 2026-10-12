using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>첫 이동 전 수첩 팝업과 상단 안내를 제거하는 승인된 변경을 적용한다.</summary>
public static class Stage11TutorialOpeningRevision
{
    /// <summary>기존 적·유진 배치는 유지하고 시작 안내 데이터와 화면만 수정한다.</summary>
    [MenuItem("Tools/S2/Apply 1-1 Quiet Opening")]
    public static void Apply()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.name != "BattleTest01" || scene.isDirty)
            throw new InvalidOperationException("저장된 BattleTest01 편집 상태가 필요합니다.");
        var tutorial = UnityEngine.Object.FindFirstObjectByType<BattleTutorialController>();
        if (tutorial == null || tutorial.Data == null || tutorial.Context.presenter == null)
            throw new InvalidOperationException("튜토리얼 진행 데이터와 Presenter가 필요합니다.");
        var data = tutorial.Data;
        Undo.RecordObject(data, "첫 이동 전 수첩 제거");
        data.steps = ReviseSteps(data.steps);
        var panel = tutorial.Context.presenter.transform.Find("Instruction");
        if (panel != null) Undo.DestroyObjectImmediate(panel.gameObject);
        EditorUtility.SetDirty(data); AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Debug.Log("첫 이동 전 수첩과 상단 지시창을 제거했습니다. 순찰 이후의 수첩 안내는 유지합니다.");
    }
    /// <summary>기존 고유 ID를 유지한 채 시작 수첩 두 개를 제거하고 이동 안내를 대사로 바꾼다.</summary>
    public static BattleTutorialStep[] ReviseSteps(BattleTutorialStep[] steps)
    {
        var movement = steps.FirstOrDefault(s => s.id == "09 이동");
        if (movement == null) throw new InvalidOperationException("첫 이동 안내 단계가 필요합니다.");
        movement.kind = BattleTutorialStepKind.Dialogue;
        movement.speaker = "금두꺼비";
        movement.text = "이동 버튼을 누르고, 표시된 코너로 이동하십시오.";
        return steps.Where(s => !(s.kind == BattleTutorialStepKind.Guide &&
            (s.id == "01 목표" || s.id == "06 목적지 확인"))).ToArray();
    }

}
