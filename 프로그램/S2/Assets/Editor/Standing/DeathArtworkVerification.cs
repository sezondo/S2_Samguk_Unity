using System;
using System.IO;
using UnityEngine;
using UnityEditor;

/// <summary>승인된 옷가지·장비 사망 원화의 참조와 표시 좌표를 검사한다.</summary>
public static class DeathArtworkVerification
{
    // 가져오기 좌표와 기존 참조를 저장한 적용 목록이다.
    [Serializable] private sealed class Entry { public string id,asset,guid;public float ppu,pivotX,pivotY; }
    [Serializable] private sealed class Entries { public Entry[] entries; }

    /// <summary>사망 세 장의 GUID·스프라이트 피벗·표시 크기와 기존 동작 참조를 확인한다.</summary>
    [MenuItem("Tools/S2/사망 잔해 원화 연결 검증")]
    public static void Verify()
    {
        var list=JsonUtility.FromJson<Entries>(File.ReadAllText("Assets/Art/Character/Cheonha/Standing/transfer.json"));int count=0;
        foreach(var e in list.entries)
        {
            if(!e.id.EndsWith("_Death"))continue;
            var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(e.asset);var importer=AssetImporter.GetAtPath(e.asset) as TextureImporter;
            if(sprite==null||importer==null||AssetDatabase.AssetPathToGUID(e.asset)!=e.guid||Mathf.Abs(sprite.pixelsPerUnit-e.ppu)>1e-3f)throw new Exception("사망 원화 참조 또는 표시 크기 오류: "+e.id);
            var pivot=new Vector2(sprite.pivot.x/sprite.rect.width,sprite.pivot.y/sprite.rect.height);
            if(Vector2.Distance(pivot,new Vector2(e.pivotX,e.pivotY))>1e-5f)throw new Exception("사망 원화 피벗 오류: "+e.id);
            bool found=false;
            foreach(string guid in AssetDatabase.FindAssets("t:AnimationClip",new[]{"Assets/Art/Character/Cheonha"}))
            {
                var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(guid));
                foreach(var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                    foreach(var key in AnimationUtility.GetObjectReferenceCurve(clip,binding))
                    {if(key.value==null)throw new Exception("적 동작 클립에 누락된 원화가 있습니다: "+clip.name);if(key.value==sprite)found=true;}
            }
            if(!found)throw new Exception("사망 원화가 기존 동작 클립에 연결되지 않았습니다: "+e.id);
            count++;
        }
        if(count!=3)throw new Exception("사망 원화 세 종류가 필요합니다.");
        Debug.Log("옷가지·장비 사망 원화 3장: 기존 GUID·표시 크기·피벗·동작 클립 참조 검증 통과");
    }
}
