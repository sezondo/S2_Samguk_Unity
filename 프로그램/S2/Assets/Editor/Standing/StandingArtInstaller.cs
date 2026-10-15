using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>승인된 원화와 9자세를 기존 전투 동작에 연결하는 편집기 도구다.</summary>
public static class StandingArtInstaller
{
    // 이전 원화의 가져오기 좌표와 확정 파일을 전달받는 목록이다.
    [Serializable] private sealed class Transfer { public Entry[] entries; }
    [Serializable] private sealed class Entry { public string id,asset,guid;public bool animated;public float ppu,pivotX,pivotY,oldFoot,oldHeight; }
    // 확정 자세 자산과 출력 경로다.
    private const string Folder="Assets/Art/Character/Cheonha/Standing";
    private static Dictionary<string,StandingPoseData> profiles;

    /// <summary>기존 GUID를 보존하여 원화 크기와 발 위치를 설정하고 전투 씬에 연결한다.</summary>
    [MenuItem("Tools/S2/확정 적 원화와 대기 애니메이션 적용")]
    public static void Install()
    {
        if(EditorApplication.isPlaying){Debug.LogError("플레이를 종료한 뒤 확정 원화를 적용하십시오.");return;}
        var manifest=JsonUtility.FromJson<Transfer>(File.ReadAllText(Folder+"/transfer.json"));
        profiles=new Dictionary<string,StandingPoseData>();Directory.CreateDirectory(Folder+"/Profiles");
        var shader=Shader.Find("S2/Presentation/StandingPose");if(shader==null)throw new Exception("대기 표시 셰이더가 없습니다.");
        string materialPath=Folder+"/StandingPose.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if(material==null){material=new Material(shader);AssetDatabase.CreateAsset(material,materialPath);}
        foreach(var e in manifest.entries)
        {
            if(AssetDatabase.AssetPathToGUID(e.asset)!=e.guid)throw new Exception("원화 GUID 변경 감지: "+e.asset);
            var importer=(TextureImporter)AssetImporter.GetAtPath(e.asset);
            importer.spritePixelsPerUnit=e.ppu;importer.spritePivot=new Vector2(e.pivotX,e.pivotY);importer.spriteImportMode=SpriteImportMode.Single;
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteAlignment=(int)SpriteAlignment.Custom;settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);
            importer.isReadable=e.animated;importer.maxTextureSize=2048;importer.mipmapEnabled=false;importer.npotScale=TextureImporterNPOTScale.None;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.alphaIsTransparency=true;
            var platform=importer.GetDefaultPlatformTextureSettings();platform.maxTextureSize=2048;platform.textureCompression=TextureImporterCompression.Uncompressed;importer.SetPlatformTextureSettings(platform);
            importer.SaveAndReimport();
            if(!e.animated)continue;
            string path=Folder+"/Profiles/"+e.id+".asset";var profile=AssetDatabase.LoadAssetAtPath<StandingPoseData>(path);
            if(profile==null){profile=ScriptableObject.CreateInstance<StandingPoseData>();AssetDatabase.CreateAsset(profile,path);}
            // 원본 프로젝트의 객체 ID는 이 프로젝트에서 사용할 수 없어 참조만 제외한다.
            var json=File.ReadAllText(Folder+"/SourceProfiles/"+e.id+".json");
            json=System.Text.RegularExpressions.Regex.Replace(json,"\"source\"\\s*:\\s*\\{[^}]*\\},","");
            JsonUtility.FromJsonOverwrite(json,profile);profile.source=AssetDatabase.LoadAssetAtPath<Texture2D>(e.asset);profile.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(e.asset);EditorUtility.SetDirty(profile);profiles[e.id]=profile;
        }
        AssetDatabase.SaveAssets();int total=0;
        foreach(string guid in AssetDatabase.FindAssets("t:Prefab"))
        {
            string path=AssetDatabase.GUIDToAssetPath(guid);var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(prefab==null||!prefab.GetComponentsInChildren<ActorVisualController>(true).Any(v=>Kind(v)!=null))continue;
            Backup(path);var root=PrefabUtility.LoadPrefabContents(path);
            try{foreach(var visual in root.GetComponentsInChildren<ActorVisualController>(true))if(Connect(visual,material))total++;PrefabUtility.SaveAsPrefabAsset(root,path);}
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        var previous=EditorSceneManager.GetSceneManagerSetup();
        foreach(var scene in Enumerable.Range(0,SceneManager.sceneCount).Select(SceneManager.GetSceneAt).ToArray())
            if(scene.isDirty&&scene.path.StartsWith("Assets/")){Backup(scene.path);EditorSceneManager.SaveScene(scene);}
        foreach(string guid in AssetDatabase.FindAssets("t:Scene",new[]{"Assets"}))
        {
            string path=AssetDatabase.GUIDToAssetPath(guid);
            if(!AssetDatabase.GetDependencies(path).Any(p=>p.Contains("Cheonha/")&&p.EndsWith(".controller")))continue;
            Backup(path);var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);int count=0;
            foreach(var visual in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ActorVisualController>(true)))if(Connect(visual,material))count++;
            if(count>0){EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);total+=count;}
        }
        EditorSceneManager.RestoreSceneManagerSetup(previous);AssetDatabase.SaveAssets();Validate();
        Debug.Log("확정 적 원화 25장·대기 자세 9종 적용 완료. 연결된 시각 오브젝트: "+total);
    }

    /// <summary>컨트롤러 자산 이름으로 적 종류를 확인한다. 추측으로 참조를 채우지 않는다.</summary>
    private static string Kind(ActorVisualController visual)
    {
        if(visual.Animator==null||visual.Animator.runtimeAnimatorController==null)return null;
        string name=visual.Animator.runtimeAnimatorController.name;
        return name=="천하회_망치"?"Hammer":name=="천하회_작은못"?"SmallNail":name=="천하회_큰못"?"BigNail":null;
    }

    /// <summary>세 자세의 표시 참조를 명시적으로 생성하고 기존 렌더러에 연결한다.</summary>
    private static bool Connect(ActorVisualController visual,Material material)
    {
        string kind=Kind(visual);if(kind==null)return false;
        var renderer=visual.TargetRenderer;if(renderer==null)throw new Exception("적 표시 렌더러 누락: "+visual.name);
        var context=visual.GetComponent<StandingVisualContext>();if(context==null)context=visual.gameObject.AddComponent<StandingVisualContext>();
        context.visual=visual;context.sourceRenderer=renderer;
        context.idle=CreateMesh(renderer.transform,kind+"_Idle",context.idle,material);
        context.lowCover=CreateMesh(renderer.transform,kind+"_Low",context.lowCover,material);
        context.wallCover=CreateMesh(renderer.transform,kind+"_Wall",context.wallCover,material);
        var animator=visual.GetComponent<StandingPoseAnimator>();if(animator==null)animator=visual.gameObject.AddComponent<StandingPoseAnimator>();animator.context=context;
        EditorUtility.SetDirty(context);EditorUtility.SetDirty(animator);return true;
    }

    /// <summary>검증된 강체 경계를 사용하는 재사용 표시 메시를 만든다.</summary>
    private static StandingPoseMesh CreateMesh(Transform parent,string id,StandingPoseMesh existing,Material material)
    {
        var rig=existing;
        if(rig==null){var go=new GameObject("확정 대기_"+id);go.transform.SetParent(parent,false);rig=go.AddComponent<StandingPoseMesh>();rig.meshFilter=go.AddComponent<MeshFilter>();rig.meshRenderer=go.AddComponent<MeshRenderer>();}
        rig.profile=profiles[id];rig.source=rig.profile.source;rig.material=material;rig.strength=2.5f;rig.animate=true;rig.manualEvaluation=true;rig.gameObject.SetActive(false);EditorUtility.SetDirty(rig);return rig;
    }

    /// <summary>수정 전 씬과 프리팹을 프로젝트 바깥 자산 백업 영역에 보관한다.</summary>
    private static void Backup(string path)
    {
        string target="Backups/Character_v025_20261015/"+path;Directory.CreateDirectory(Path.GetDirectoryName(target));
        if(!File.Exists(target))File.Copy(path,target);
        if(File.Exists(path+".meta")&&!File.Exists(target+".meta"))File.Copy(path+".meta",target+".meta");
    }

    /// <summary>기존 GUID·발 위치·높이와 9자세의 무기·접점·반복을 검증한다.</summary>
    [MenuItem("Tools/S2/확정 적 대기 연결 검증")]
    public static void Validate()
    {
        var manifest=JsonUtility.FromJson<Transfer>(File.ReadAllText(Folder+"/transfer.json"));var report=new List<string>();
        foreach(var e in manifest.entries)
        {
            var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(e.asset);if(sprite==null||AssetDatabase.AssetPathToGUID(e.asset)!=e.guid)throw new Exception("스프라이트 참조 검증 실패: "+e.id);
            if(!e.animated)continue;
            var profile=AssetDatabase.LoadAssetAtPath<StandingPoseData>(Folder+"/Profiles/"+e.id+".asset");
            var go=new GameObject("확정 자세 검증");go.SetActive(false);var rig=go.AddComponent<StandingPoseMesh>();rig.profile=profile;rig.source=profile.source;rig.material=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/StandingPose.mat");rig.meshFilter=go.AddComponent<MeshFilter>();rig.meshRenderer=go.AddComponent<MeshRenderer>();rig.manualEvaluation=true;rig.Initialize();
            float contact=0,weapon=0,motion=0;var start=(Vector3[])rig.RuntimeMesh.vertices.Clone();
            for(int i=0;i<76;i++)
            {
                rig.Evaluate(i*.1f);var vertices=rig.RuntimeMesh.vertices;
                foreach(var p in profile.fixedContacts){int k=rig.Nearest(p.x,p.y);contact=Mathf.Max(contact,Vector3.Distance(vertices[k],rig.RestVertices[k]));}
                foreach(var p in profile.rigidProbes){int k=rig.Nearest(p.point.x,p.point.y);weapon=Mathf.Max(weapon,Vector3.Distance(vertices[k],rig.ExpectedBonePoint(k,p.bone)));}
                for(int k=0;k<vertices.Length;k++)motion=Mathf.Max(motion,Vector3.Distance(vertices[k],start[k]));
            }
            rig.Evaluate(7.6f);float loop=0;var last=rig.RuntimeMesh.vertices;for(int k=0;k<last.Length;k++)loop=Mathf.Max(loop,Vector3.Distance(start[k],last[k]));
            float foot=Mathf.Abs(rig.SpriteLocalOffset.y-e.oldFoot),height=Mathf.Abs(rig.height-e.oldHeight);
            report.Add($"{e.id}: contact={contact:E3}, weapon={weapon:E3}, loop={loop:E3}, foot={foot:E3}, height={height:E3}, motion={motion:E3}");
            if(contact>1e-5f||weapon>1e-5f||loop>1e-5f||foot>1e-4f||height>1e-4f||motion<=0)throw new Exception("확정 자세 검증 실패: "+e.id);
            UnityEngine.Object.DestroyImmediate(go);
        }
        Directory.CreateDirectory("Captures/Standing_v025");File.WriteAllLines("Captures/Standing_v025/verification.txt",report);Debug.Log("원화 참조 25장과 대기·엄폐 9자세의 발·무기·반복 검증 통과");
    }
}
