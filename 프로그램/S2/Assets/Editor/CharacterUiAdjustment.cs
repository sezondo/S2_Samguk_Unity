using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>캐릭터 UI·비주얼 정렬 상태를 읽고 사용자 요청 범위를 적용하는 편집기 도구다.</summary>
public static class CharacterUiAdjustment
{
    /// <summary>현재 씬의 실제 비주얼 계층과 스프라이트 기준점을 읽기 전용으로 기록한다.</summary>
    [MenuItem("Tools/S2/Audit Character UI")]
    public static void Audit()
    {
        var text = new StringBuilder();
        foreach (var v in UnityEngine.Object.FindObjectsByType<ActorVisualController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            text.AppendLine("VISUAL " + PathOf(v.transform));
            text.AppendLine(EditorJsonUtility.ToJson(v));
            if(v.TargetRenderer==null) continue;
            for(var t=v.TargetRenderer.transform;t!=null;t=t.parent)
                text.AppendLine($"TRANSFORM {PathOf(t)} local={t.localPosition} scale={t.localScale} world={t.position}");
            var s=v.TargetRenderer.sprite;
            if(s==null) continue;
            text.AppendLine($"SPRITE {AssetDatabase.GetAssetPath(s)} pivot={s.pivot} ppu={s.pixelsPerUnit} bounds={v.TargetRenderer.bounds} ground={v.GroundWorldPosition}");
        }
        foreach(var c in UnityEngine.Object.FindObjectsByType<BattleHudTacticalOverlayController>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            text.AppendLine("HUD "+EditorJsonUtility.ToJson(c));
        foreach(var c in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            text.AppendLine("CAMERA "+EditorJsonUtility.ToJson(c));
        Directory.CreateDirectory("Temp/CharacterUI");
        File.WriteAllText("Temp/CharacterUI/audit.txt",text.ToString());
        Debug.Log("캐릭터 UI 현재 상태 기록 완료");
    }
    /// <summary>계층 경로를 표시한다.</summary>
    private static string PathOf(Transform t)=>t.parent==null?t.name:PathOf(t.parent)+"/"+t.name;
}
