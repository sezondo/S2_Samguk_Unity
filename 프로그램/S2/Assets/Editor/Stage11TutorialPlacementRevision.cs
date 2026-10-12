using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>사용자의 배치 이미지와 실제 맵 좌표를 대조하고 초안을 수정한다.</summary>
public static partial class Stage11TutorialAuthoring
{
    /// <summary>첫 이동·큰못을 지정 위치로 옮기고 추가 엄폐를 제거한 뒤 후속 AP를 맞춘다.</summary>
    [MenuItem("Tools/S2/Apply 1-1 User Placement Revision")]
    public static void ApplyUserPlacement()
    {
        var scene=SceneManager.GetActiveScene();
        if(EditorApplication.isPlayingOrWillChangePlaymode||scene.name!="BattleTest01"||scene.isDirty)throw new InvalidOperationException("저장된 1-1 편집 씬이 필요합니다.");
        var controller=UnityEngine.Object.FindFirstObjectByType<BattleTutorialController>();var c=controller.Context;var data=controller.Data;
        if(data.steps.Any(s=>s.id.Contains("정찰 이동 턴 종료")))throw new InvalidOperationException("이미 사용자 배치를 적용했습니다. 이후에는 진행 데이터를 직접 수정하십시오.");
        var tilemap=c.grid.LowObstacleLogicTilemap;
        tilemap.SetTile(tilemap.WorldToCell(c.grid.GridToWorld(new GridPosition(36,14))),null);
        var barrel=GameObject.Find("TutorialCoverBarrel");if(barrel!=null)UnityEngine.Object.DestroyImmediate(barrel);
        // 편집 배치 검사를 위해 논리 타일 캐시를 다시 만든다.
        typeof(GridManager).GetMethod("RebuildCellStates",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(c.grid,null);
        Place(c.bigNail.GridActor,new GridPosition(34,15),c.grid);
        foreach(var step in data.steps)
        {
            if(step.id.Contains("집 앞 코너"))step.target=new GridPosition(23,16);
            if(step.id.Contains("첫 조우"))step.target=new GridPosition(33,14);
            if(step.id.Contains("첫 총격")||step.id.Contains("검 근접 공격"))step.target=new GridPosition(34,15);
            if(step.id.Contains("근접 거리"))step.target=new GridPosition(34,14);
            if(step.id.Contains("정찰 준비"))step.text="이동 버튼 → 강조된 칸. 전투 지점에서 정찰 위치까지 3AP를 사용해 이동합니다.";
            if(step.kind==BattleTutorialStepKind.EndTurn&&step.turn>=5)step.turn++;
        }
        var steps=data.steps.ToList();int index=steps.FindIndex(s=>s.id.Contains("정찰 준비"));
        steps.Insert(index+1,new BattleTutorialStep{kind=BattleTutorialStepKind.EndTurn,id="정찰 이동 턴 종료",text="턴 종료 버튼을 눌러 다음 턴에 검 정찰을 시작하십시오.",turn=5});
        data.secondCombatTurn=8;
        for(int i=0;i<steps.Count;i++)steps[i].id=$"{i+1:D2} {System.Text.RegularExpressions.Regex.Replace(steps[i].id,@"^\d+\s+","")}";
        data.steps=steps.ToArray();
        EditorUtility.SetDirty(data);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        Debug.Log("1턴 목표 (23,16), 큰못 (34,15)로 변경하고 추가 드럼통과 논리 타일을 제거했습니다. 10턴부터 자유 탈출합니다.");
    }

    /// <summary>편집 맵의 좌표 표기를 임시로 렌더링하고 모든 임시 오브젝트를 제거한다.</summary>
    [MenuItem("Tools/S2/Capture 1-1 Placement Coordinates")]
    public static void CaptureCoordinates()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("편집 상태가 필요합니다.");
        var grid=UnityEngine.Object.FindFirstObjectByType<BattleTutorialController>().Context.grid;
        var root=new GameObject("TemporaryCoordinateReference"){hideFlags=HideFlags.HideAndDontSave};
        var cameraObject=new GameObject("TemporaryReferenceCamera"){hideFlags=HideFlags.HideAndDontSave};
        var camera=cameraObject.AddComponent<Camera>();camera.CopyFrom(Camera.main);
        camera.transform.position=new Vector3(32.5f,16.5f,-6);camera.orthographic=true;camera.orthographicSize=6;
        var rt=new RenderTexture(1600,900,24);var texture=new Texture2D(1600,900,TextureFormat.RGB24,false);
        var previous=RenderTexture.active;
        try
        {
            for(int x=20;x<=44;x++)for(int y=11;y<=22;y++)
            {
                var go=new GameObject($"{x},{y}",typeof(TextMesh));go.transform.SetParent(root.transform);
                go.transform.position=grid.GridToWorld(new GridPosition(x,y))+new Vector3(0,.18f,-2);
                var text=go.GetComponent<TextMesh>();text.text=$"{x},{y}";text.characterSize=.08f;text.fontSize=32;text.anchor=TextAnchor.MiddleCenter;text.color=Color.cyan;
            }
            camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
            texture.ReadPixels(new Rect(0,0,1600,900),0,0);texture.Apply();
            Directory.CreateDirectory("Temp/Stage11Tutorial");File.WriteAllBytes("Temp/Stage11Tutorial/coordinate-reference.png",texture.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active=previous;camera.targetTexture=null;
            UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(cameraObject);
            UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(rt);
        }
    }
}

