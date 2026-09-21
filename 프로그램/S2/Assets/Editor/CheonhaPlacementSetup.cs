using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>천하회 확정 캐릭터의 배틀씬 배치와 에셋 연결을 위한 명시적 편집 도구다.</summary>
public static class CheonhaPlacementSetup
{
    // 승인된 원본 경로와 배틀씬 배치 좌표다.
    private const string ArtRoot="C:/Users/psdd2/Desktop/원화/캐릭터/";
    private const string AssetRoot="Assets/Art/Character/Cheonha/";
    public static readonly string[] Names={"천하회_망치","천하회_작은못","천하회_큰못"};
    public static readonly GridPosition[] Positions={new(48,19),new(31,28),new(29,15)};
    private static readonly string[] Sources={
        "천하회_망치/02_확정_결과물/SD8_장도리_v004/Unity_512",
        "천하회_작은못/확정_SD_9동작/Unity_512",
        "천하회_큰못/02_확정_결과물/SD8_v011/Unity_512"};

    /// <summary>사용자가 요청한 세 병종의 확정 그림과 행동 연결을 생성하고 씬에 저장한다.</summary>
    [MenuItem("Tools/S2/Apply Cheonha Placement")]
    public static void Apply()
    {
        var scene=SceneManager.GetActiveScene();
        if(EditorApplication.isPlayingOrWillChangePlaymode||scene.name!="BattleTest01"||scene.isDirty)
            throw new InvalidOperationException("저장된 BattleTest01 편집 모드에서 적용해야 합니다.");
        if(scene.GetRootGameObjects().Any(x=>x.name=="천하회_배치"))
            throw new InvalidOperationException("천하회 배치가 이미 존재합니다. 중복 생성하지 않습니다.");
        var template=scene.GetRootGameObjects().Single(x=>x.name=="Enemy");
        var grid=UnityEngine.Object.FindFirstObjectByType<GridManager>();
        var queue=UnityEngine.Object.FindFirstObjectByType<ActionPresentationQueue>();
        var player=scene.GetRootGameObjects().Single(x=>x.name=="Player Actor Root").GetComponentInChildren<ActorVisualController>(true);
        var playerRenderer=player.GetComponent<SpriteRenderer>();
        foreach(var position in Positions)if(!grid.IsInside(position)||grid.IsBlocked(position))throw new Exception("배치 칸이 막혔습니다: "+position);
        Directory.CreateDirectory("Temp/Cheonha");
        File.Copy(scene.path,"Temp/Cheonha/BattleTest01.before-placement.unity",false);
        // 원본 픽셀은 그대로 복사한다. 발 y=407 기준을 현재 플레이어와 같은 표시 높이에 맞춘다.
        const float scale=.35f,ppu=100f;
        float groundLocal=(player.GroundWorldPosition.y-player.transform.position.y)/scale;
        var pivot=new Vector2(.5f,(105f-groundLocal*ppu)/512f);
        var controllers=new RuntimeAnimatorController[3];var idleSprites=new Sprite[3];
        for(int i=0;i<3;i++)
        {
            var sourceFiles=Directory.GetFiles(ArtRoot+Sources[i],"*.png").OrderBy(x=>x).ToArray();
            if(sourceFiles.Length!=(i==1?9:8))throw new Exception("확정 동작 수 불일치: "+Names[i]);
            string directory=AssetRoot+Names[i];Directory.CreateDirectory(directory+"/Sprites");Directory.CreateDirectory(directory+"/Animations");
            foreach(var source in sourceFiles)File.Copy(source,directory+"/Sprites/"+Path.GetFileName(source),false);
            AssetDatabase.Refresh();
            var sprites=new System.Collections.Generic.Dictionary<int,Sprite>();
            foreach(var source in sourceFiles)
            {
                string path=directory+"/Sprites/"+Path.GetFileName(source);
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
                importer.spritePixelsPerUnit=ppu;importer.mipmapEnabled=false;importer.alphaIsTransparency=true;
                importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=512;importer.filterMode=FilterMode.Bilinear;
                var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteAlignment=(int)SpriteAlignment.Custom;settings.spritePivot=pivot;
                settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);importer.SaveAndReimport();
                var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);int number=int.Parse(Path.GetFileName(source).Split('_')[3]);sprites.Add(number,sprite);
            }
            var controller=AnimatorController.CreateAnimatorControllerAtPath(directory+"/Animations/"+Names[i]+".controller");
            var states=new System.Collections.Generic.Dictionary<string,int>{{"Idle",4},{"Move",2},{"Death",3},{"Hit",i==1?6:5},{"Dodge",i==1?7:6},{"LowCoverIdle",i==1?8:7},{"WallCoverIdle",i==1?9:8}};
            if(i!=2)states.Add("MeleeWithSword",1);
            if(i!=0)states.Add("EnemyRanged",i==1?5:1);
            foreach(var entry in states)
            {
                var clip=new AnimationClip{name=entry.Key,frameRate=12};
                var binding=new EditorCurveBinding{path="",type=typeof(SpriteRenderer),propertyName="m_Sprite"};
                AnimationUtility.SetObjectReferenceCurve(clip,binding,new[]{new ObjectReferenceKeyframe{time=0,value=sprites[entry.Value]},new ObjectReferenceKeyframe{time=.5f,value=sprites[entry.Value]}});
                var clipSettings=AnimationUtility.GetAnimationClipSettings(clip);clipSettings.loopTime=entry.Key=="Idle"||entry.Key=="Move"||entry.Key.EndsWith("CoverIdle");AnimationUtility.SetAnimationClipSettings(clip,clipSettings);
                AssetDatabase.CreateAsset(clip,directory+"/Animations/"+entry.Key+".anim");
                var state=controller.layers[0].stateMachine.AddState(entry.Key);state.motion=clip;
                if(entry.Key=="Idle")controller.layers[0].stateMachine.defaultState=state;
            }
            controllers[i]=controller;idleSprites[i]=sprites[4];
        }
        var holder=new GameObject("천하회_배치");Undo.RegisterCreatedObjectUndo(holder,"천하회 3종 배치");
        var contexts=new EnemyContext[3];
        for(int i=0;i<3;i++)
        {
            var unit=UnityEngine.Object.Instantiate(template,holder.transform);unit.name=Names[i];
            Undo.RegisterCreatedObjectUndo(unit,"천하회 생성");unit.SetActive(false);unit.transform.position=grid.GridToWorld(Positions[i]);
            // 천하회는 해킹 대상이 아니므로 원본 테스트 적의 해킹 구성은 복제 후 제거한다.
            foreach(var presenter in unit.GetComponentsInChildren<HackPresenter>(true))UnityEngine.Object.DestroyImmediate(presenter);
            foreach(var hackable in unit.GetComponentsInChildren<HackableObject>(true))UnityEngine.Object.DestroyImmediate(hackable);
            var enemy=unit.GetComponentInChildren<EnemyContext>(true);contexts[i]=enemy;
            var actor=enemy.GridActor;actor.transform.localPosition=Vector3.zero;
            Set(actor,"gridPosition",Positions[i]);Set(enemy,"enemyData",AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/Data/TestStage/Enemy/"+Names[i]+".asset"));
            Set(enemy.RoutineController,"patrolGroup",null);Set(enemy.RoutineController,"startPoint",null);Set(enemy.RoutineController,"routineType",i==1?1:0);
            Set(enemy.GridSight,"facingDirection",(int)GridDirection.Down);
            var visual=unit.GetComponentInChildren<ActorVisualController>(true);visual.transform.localPosition=Vector3.zero;visual.transform.localScale=Vector3.one*scale;
            var renderer=visual.GetComponent<SpriteRenderer>();var animator=visual.GetComponent<Animator>();
            renderer.sprite=idleSprites[i];renderer.color=Color.white;renderer.sharedMaterial=playerRenderer.sharedMaterial;
            renderer.sortingLayerID=playerRenderer.sortingLayerID;renderer.sortingOrder=playerRenderer.sortingOrder;
            animator.runtimeAnimatorController=controllers[i];
            Set(visual,"targetRenderer",renderer);Set(visual,"animator",animator);Set(visual,"artworkFacesRight",true);Set(visual,"groundPointLocalOffset",new Vector2(0,groundLocal));
            var presentation=unit.transform.Find("ActorPresentation");presentation.localPosition=Vector3.zero;
            var cover=presentation.gameObject.AddComponent<LowCoverIdlePresenter>();
            Set(cover,"unitContext",null);Set(cover,"enemyContext",enemy);Set(cover,"visualController",visual);Set(cover,"gridManager",grid);Set(cover,"presentationQueue",queue);
            var indicator=presentation.GetComponent<EnemyFacingIndicatorPresenter>();Set(indicator,"worldPositionOffset",new Vector3(0,groundLocal*scale+.08f,-.05f));
        }
        var route=new GameObject("작은못_수리점북쪽_순찰");route.transform.SetParent(holder.transform,false);
        var routePositions=new[]{Positions[1],new GridPosition(35,28),new GridPosition(38,28)};
        var points=new PatrolPoint[3];
        for(int i=0;i<3;i++)
        {
            if(grid.IsBlocked(routePositions[i]))throw new Exception("순찰 지점이 막혔습니다: "+routePositions[i]);
            var point=new GameObject($"순찰_{i+1}_{routePositions[i].x}_{routePositions[i].y}");point.transform.SetParent(route.transform,false);point.transform.position=grid.GridToWorld(routePositions[i]);
            points[i]=point.AddComponent<PatrolPoint>();Set(points[i],"lookDirection",(int)(i==2?GridDirection.Left:GridDirection.Right));Set(points[i],"waitTurns",0);
        }
        SetArray(points[0],"connectedPoints",new UnityEngine.Object[]{points[1]});
        SetArray(points[1],"connectedPoints",new UnityEngine.Object[]{points[0],points[2]});
        SetArray(points[2],"connectedPoints",new UnityEngine.Object[]{points[1]});
        Set(contexts[1].RoutineController,"startPoint",points[0]);Set(contexts[1].GridSight,"facingDirection",(int)GridDirection.Right);
        foreach(var enemy in contexts)enemy.transform.parent.gameObject.SetActive(true);
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        Capture("placed-overview",new Vector3(36,22,-10),21);
        File.WriteAllText("Temp/Cheonha/applied.txt",$"완료: 25 스프라이트, 3 Animator, 3 적, 작은못 왕복 순찰. scale={scale}, PPU={ppu}, pivot={pivot}, ground={groundLocal}");
        Debug.Log("천하회 망치·작은못·큰못 배치와 순찰 연결을 저장했습니다.");
    }

    /// <summary>편집 단계에서 검증된 직렬화 필드를 명시적으로 지정한다.</summary>
    public static void Set(UnityEngine.Object target,string name,object value)
    {
        var so=new SerializedObject(target);var property=so.FindProperty(name);if(property==null)throw new Exception(target.name+" 필드 없음: "+name);
        if(value==null||value is UnityEngine.Object)property.objectReferenceValue=value as UnityEngine.Object;
        else if(value is int integer)property.intValue=integer;
        else if(value is bool boolean)property.boolValue=boolean;
        else if(value is float scalar)property.floatValue=scalar;
        else if(value is Vector2 vector2)property.vector2Value=vector2;
        else if(value is Vector3 vector3)property.vector3Value=vector3;
        else if(value is GridPosition p){property.FindPropertyRelative("x").intValue=p.x;property.FindPropertyRelative("y").intValue=p.y;}
        else throw new Exception("지원하지 않는 필드 값: "+name);
        so.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(target);
    }
    /// <summary>순찰 연결의 명시적 오브젝트 배열을 저장한다.</summary>
    private static void SetArray(UnityEngine.Object target,string name,UnityEngine.Object[] values)
    {
        var so=new SerializedObject(target);var property=so.FindProperty(name);property.arraySize=values.Length;
        for(int i=0;i<values.Length;i++)property.GetArrayElementAtIndex(i).objectReferenceValue=values[i];so.ApplyModifiedPropertiesWithoutUndo();
    }
    /// <summary>활성 맵의 건물·해킹 단말·격자와 기존 적 구성을 읽기 전용으로 기록한다.</summary>
    [MenuItem("Tools/S2/Audit Cheonha Placement")]
    public static void Audit()
    {
        var grid=UnityEngine.Object.FindFirstObjectByType<GridManager>();
        var text=new StringBuilder();
        text.AppendLine($"GRID {grid.Width}x{grid.Height}, cell={grid.CellSize}, origin={grid.GridToWorld(new(0,0))}");
        foreach(var renderer in UnityEngine.Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
            if(renderer.sprite!=null&&(renderer.bounds.size.x>3||renderer.sprite.name.Contains("Repair")||renderer.sprite.name.Contains("Workshop")))
                text.AppendLine($"SPRITE {renderer.sprite.name} | {Hierarchy(renderer.transform)} | grid={grid.WorldToGrid(renderer.transform.position)} | world={renderer.transform.position} | bounds={renderer.bounds} | layer={renderer.sortingLayerName} order={renderer.sortingOrder}");
        foreach(var actor in UnityEngine.Object.FindObjectsByType<GridActor>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            text.AppendLine($"ACTOR {Hierarchy(actor.transform)} | grid={actor.GridPosition} | active={actor.gameObject.activeInHierarchy}");
        foreach(var visual in UnityEngine.Object.FindObjectsByType<ActorVisualController>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            text.AppendLine($"VISUAL {Hierarchy(visual.transform)} | {EditorJsonUtility.ToJson(visual)} | scale={visual.transform.localScale}");
        for(int y=grid.Height-1;y>=0;y--)
        {
            text.Append($"{y:D2} ");for(int x=0;x<grid.Width;x++)text.Append(grid.IsBlocked(new(x,y))?'#':'.');text.AppendLine();
        }
        Directory.CreateDirectory("Temp/Cheonha");File.WriteAllText("Temp/Cheonha/audit.txt",text.ToString());
        Capture("overview",new Vector3(36,22,-10),21);
        Debug.Log("천하회 배치 조사 기록: Temp/Cheonha/audit.txt");
    }
    /// <summary>원래 카메라 설정을 바꾸지 않는 임시 카메라로 배치를 촬영한다.</summary>
    public static void Capture(string filename,Vector3 position,float size)
    {
        var go=new GameObject("임시 배치 검수 카메라");var camera=go.AddComponent<Camera>();camera.CopyFrom(Camera.main);
        camera.transform.position=position;camera.transform.rotation=Quaternion.identity;camera.orthographic=true;camera.orthographicSize=size;
        var texture=new RenderTexture(1600,1000,24);var previous=RenderTexture.active;
        var image=new Texture2D(1600,1000,TextureFormat.RGB24,false);
        try{camera.targetTexture=texture;camera.Render();RenderTexture.active=texture;image.ReadPixels(new Rect(0,0,1600,1000),0,0);image.Apply();
            File.WriteAllBytes("Temp/Cheonha/"+filename+".png",image.EncodeToPNG());}
        finally{RenderTexture.active=previous;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(image);texture.Release();UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(go);}
    }
    /// <summary>씬 오브젝트의 전체 경로를 반환한다.</summary>
    public static string Hierarchy(Transform node)=>node.parent==null?node.name:Hierarchy(node.parent)+"/"+node.name;
}
