using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>S3에서 승인한 유진의 세 대기 자세를 S2 시각 구조에 연결한다.</summary>
public static class YujinStandingInstaller
{
    // 유진 자세 데이터와 기존 원화의 명시적인 대응이다.
    public const string Folder="Assets/Art/Character/Yujin/Standing";
    public static readonly string[] Ids={"Yujin_Idle","Yujin_Low","Yujin_Wall"};
    private static readonly string[] Images={"유진 전투 대기.png","유진_SD_09_낮은엄폐_v001.png","유진_SD_10_높은엄폐_v002.png"};
    // 편집기 연결 중에만 사용하는 세 자세와 공용 표시 재질이다.
    private static StandingPoseData[] profiles;
    private static Material material;

    /// <summary>원화의 크기·피벗을 유지하며 유진 프리팹과 씬의 세 자세를 연결한다.</summary>
    [MenuItem("Tools/S2/유진 스탠딩 엄폐 애니메이션 적용")]
    public static void Install()
    {
        if(EditorApplication.isPlaying){Debug.LogError("플레이를 종료한 뒤 유진 대기 표시를 연결하십시오.");return;}
        Directory.CreateDirectory(Folder+"/Profiles");profiles=new StandingPoseData[3];
        material=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Character/Cheonha/Standing/StandingPose.mat");
        if(material==null)throw new Exception("기존 부위별 대기 표시 재질이 없습니다.");
        for(int i=0;i<3;i++)
        {
            string imagePath="Assets/Art/Character/Yujin/Sprites/"+Images[i];Backup(imagePath+".meta");
            var before=AssetDatabase.LoadAssetAtPath<Sprite>(imagePath);if(before==null)throw new Exception("유진 자세 원화가 없습니다: "+imagePath);
            float ppu=before.pixelsPerUnit;Vector2 pivot=before.pivot;Rect rect=before.rect;string guid=AssetDatabase.AssetPathToGUID(imagePath);
            var importer=(TextureImporter)AssetImporter.GetAtPath(imagePath);importer.isReadable=true;importer.SaveAndReimport();
            var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(imagePath);
            if(sprite.pixelsPerUnit!=ppu||sprite.pivot!=pivot||sprite.rect!=rect||AssetDatabase.AssetPathToGUID(imagePath)!=guid)throw new Exception("유진 원화의 기존 표시 좌표가 변경됐습니다.");
            string path=Folder+"/Profiles/"+Ids[i]+".asset";var p=AssetDatabase.LoadAssetAtPath<StandingPoseData>(path);
            if(p==null){p=ScriptableObject.CreateInstance<StandingPoseData>();AssetDatabase.CreateAsset(p,path);}
            JsonUtility.FromJsonOverwrite(File.ReadAllText(Folder+"/SourceProfiles/"+Ids[i]+".json"),p);
            p.source=AssetDatabase.LoadAssetAtPath<Texture2D>(imagePath);p.sprite=sprite;profiles[i]=p;EditorUtility.SetDirty(p);
        }
        AssetDatabase.SaveAssets();int count=0;
        foreach(string guid in AssetDatabase.FindAssets("t:Prefab"))
        {
            string path=AssetDatabase.GUIDToAssetPath(guid);var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(asset==null||!asset.GetComponentsInChildren<ActorVisualController>(true).Any(IsYujin))continue;
            Backup(path);var root=PrefabUtility.LoadPrefabContents(path);
            try{foreach(var v in root.GetComponentsInChildren<ActorVisualController>(true))if(Connect(v))count++;PrefabUtility.SaveAsPrefabAsset(root,path);}
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        var previous=EditorSceneManager.GetSceneManagerSetup();
        foreach(var scene in Enumerable.Range(0,SceneManager.sceneCount).Select(SceneManager.GetSceneAt).ToArray())
            if(scene.isDirty&&scene.path.StartsWith("Assets/")){Backup(scene.path);EditorSceneManager.SaveScene(scene);}
        foreach(string guid in AssetDatabase.FindAssets("t:Scene",new[]{"Assets"}))
        {
            string path=AssetDatabase.GUIDToAssetPath(guid);
            if(!AssetDatabase.GetDependencies(path).Contains("Assets/Art/Character/Yujin/Animations/유진.controller"))continue;
            Backup(path);var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);int sceneCount=0;
            foreach(var v in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ActorVisualController>(true)))if(Connect(v))sceneCount++;
            if(sceneCount>0){EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);count+=sceneCount;}
        }
        EditorSceneManager.RestoreSceneManagerSetup(previous);AssetDatabase.SaveAssets();Validate();
        Debug.Log("유진 스탠딩·낮은 엄폐·벽 엄폐 연결 완료: "+count+"개 시각 오브젝트, 강도 2.5");
    }

    /// <summary>명시적인 Animator 컨트롤러로 유진 시각 오브젝트를 식별한다.</summary>
    public static bool IsYujin(ActorVisualController v){return v.Animator!=null&&v.Animator.runtimeAnimatorController!=null&&v.Animator.runtimeAnimatorController.name=="유진";}

    /// <summary>기존 시각 오브젝트에 참조 Context와 세 메시를 명시적으로 연결한다.</summary>
    private static bool Connect(ActorVisualController visual)
    {
        if(!IsYujin(visual))return false;if(visual.TargetRenderer==null)throw new Exception("유진의 원화 렌더러가 없습니다.");
        var c=visual.GetComponent<StandingVisualContext>();if(c==null)c=visual.gameObject.AddComponent<StandingVisualContext>();c.visual=visual;c.sourceRenderer=visual.TargetRenderer;
        c.idle=CreateMesh(c.sourceRenderer.transform,c.idle,0);c.lowCover=CreateMesh(c.sourceRenderer.transform,c.lowCover,1);c.wallCover=CreateMesh(c.sourceRenderer.transform,c.wallCover,2);
        var a=visual.GetComponent<StandingPoseAnimator>();if(a==null)a=visual.gameObject.AddComponent<StandingPoseAnimator>();a.context=c;EditorUtility.SetDirty(c);EditorUtility.SetDirty(a);return true;
    }

    /// <summary>검증한 원화와 강체 데이터를 사용하는 대기 메시를 준비한다.</summary>
    private static StandingPoseMesh CreateMesh(Transform parent,StandingPoseMesh rig,int index)
    {
        if(rig==null){var go=new GameObject("유진 대기_"+Ids[index]);go.transform.SetParent(parent,false);rig=go.AddComponent<StandingPoseMesh>();rig.meshFilter=go.AddComponent<MeshFilter>();rig.meshRenderer=go.AddComponent<MeshRenderer>();}
        rig.profile=profiles[index];rig.source=profiles[index].source;rig.material=material;rig.strength=2.5f;rig.animate=true;rig.manualEvaluation=true;rig.gameObject.SetActive(false);EditorUtility.SetDirty(rig);return rig;
    }

    /// <summary>수정하는 씬·프리팹·가져오기 정보만 백업하고 이미지 사본은 만들지 않는다.</summary>
    private static void Backup(string path)
    {
        string target="Backups/YujinStanding_20261015/"+path;Directory.CreateDirectory(Path.GetDirectoryName(target));if(!File.Exists(target))File.Copy(path,target);
    }

    /// <summary>유진 세 자세의 발 고정·총 강체·호흡 반복과 원화 복원을 검사한다.</summary>
    [MenuItem("Tools/S2/유진 대기 접점 총 반복 검증")]
    public static void Validate()
    {
        var report=new List<string>();
        foreach(var id in Ids)
        {
            var p=AssetDatabase.LoadAssetAtPath<StandingPoseData>(Folder+"/Profiles/"+id+".asset");if(p==null)throw new Exception("유진 자세 데이터 누락: "+id);
            var go=new GameObject("유진 자세 검증");go.SetActive(false);var r=go.AddComponent<StandingPoseMesh>();r.source=p.source;r.profile=p;r.material=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Character/Cheonha/Standing/StandingPose.mat");r.meshFilter=go.AddComponent<MeshFilter>();r.meshRenderer=go.AddComponent<MeshRenderer>();r.manualEvaluation=true;r.Initialize();
            try
            {
                var first=r.RuntimeMesh.vertices;float contact=0,gun=0,motion=0,loop=0,neutral=0;
                for(int frame=0;frame<76;frame++)
                {
                    r.Evaluate(frame*.1f);var vertices=r.RuntimeMesh.vertices;
                    foreach(var point in p.fixedContacts){int k=r.Nearest(point.x,point.y);contact=Mathf.Max(contact,Vector3.Distance(vertices[k],r.RestVertices[k]));}
                    foreach(var probe in p.rigidProbes){int k=r.Nearest(probe.point.x,probe.point.y);gun=Mathf.Max(gun,Vector3.Distance(vertices[k],r.ExpectedBonePoint(k,probe.bone)));}
                    for(int k=0;k<vertices.Length;k++){if(float.IsNaN(vertices[k].x)||float.IsNaN(vertices[k].y))throw new Exception("유진의 유효하지 않은 표시 정점");motion=Mathf.Max(motion,Vector3.Distance(first[k],vertices[k]));}
                }
                r.Evaluate(7.6f);var last=r.RuntimeMesh.vertices;for(int k=0;k<last.Length;k++)loop=Mathf.Max(loop,Vector3.Distance(first[k],last[k]));
                r.strength=0;r.Evaluate(1);last=r.RuntimeMesh.vertices;for(int k=0;k<last.Length;k++)neutral=Mathf.Max(neutral,Vector3.Distance(r.RestVertices[k],last[k]));
                string line=id+" 발="+contact+" 총="+gun+" 반복="+loop+" 원화복원="+neutral+" 움직임="+motion;report.Add(line);
                if(contact>1e-5f||gun>1e-5f||loop>1e-5f||neutral>1e-5f||motion<=0)throw new Exception("유진 대기 검증 실패: "+line);
            }
            finally{UnityEngine.Object.DestroyImmediate(go);}
        }
        Directory.CreateDirectory("Captures/YujinStanding");File.WriteAllLines("Captures/YujinStanding/geometry.txt",report);Debug.Log("유진 세 자세 발·총 강체·반복·원화 복원 검증 통과");
    }
}
