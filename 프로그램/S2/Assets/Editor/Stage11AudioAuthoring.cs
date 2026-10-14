using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>승인된 1-1 전체 효과음 시안을 공유 데이터와 기존 화면에 연결한다.</summary>
public static class Stage11AudioAuthoring
{
    public const string SoundRoot = "Assets/Data/Battle/Presentation/Sound/Stage11/";
    // 음원별 초기 음량과 정지 중 재생 정책이다. 재생 순서표나 런타임 ID 등록표가 아니다.
    private static readonly (string name, float volume, bool ui)[] Catalog = {
        ("SwordScout",.32f,false),("SwordAttack",.46f,false),("SwordRecall",.32f,false),("SwordMelee",.46f,false),
        ("Unarmed",.38f,false),("Hammer",.56f,false),("Club",.48f,false),("Crossbow",.46f,false),("Bow",.43f,false),
        ("ImpactHit",.48f,false),("Dodge",.30f,false),("Death",.38f,false),("Alert",.38f,false),("Suspicion",.26f,false),
        ("HackStart",.28f,false),("HackComplete",.36f,false),("DoorOpen",.43f,false),("ElevatorDepart",.40f,false),
        ("MissionFail",.28f,false),("ManualOpen",.24f,true),("ManualClose",.24f,true),("PageTurn",.20f,true),
        ("MenuOpen",.19f,true),("MenuClose",.17f,true),("UiClick",.16f,true)
    };

    /// <summary>음원 가져오기·공유 데이터·전투 항목과 세 배틀 씬의 명시적 참조를 조립한다.</summary>
    [MenuItem("Tools/S2/Apply 1-1 Full Audio Draft")]
    public static void Apply()
    {
        Guard(); Directory.CreateDirectory(SoundRoot); AssetDatabase.Refresh();
        foreach (var entry in Catalog) CreateSound(entry.name, entry.volume, entry.ui);
        foreach (string path in new[] {
            "Assets/Data/Battle/Presentation/Combat/YujinCombat.asset", "Assets/Data/TestStage/Player/CombatPresentationDataTest.asset",
            "Assets/Art/Vfx/Cheonha/천하회_망치_Combat.asset", "Assets/Art/Vfx/Cheonha/천하회_작은못_Combat.asset",
            "Assets/Art/Vfx/Cheonha/천하회_큰못_Combat.asset" }) ConfigureCombat(path);
        string original = SceneManager.GetActiveScene().path;
        try
        {
            foreach (string path in PresentationMediaAuthoring.Scenes)
            {
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var context = All<BattlePresentationContext>().Single();
                foreach (var sword in All<SwordActionPresenter>()) { Set(sword,"scoutSound",Sound("SwordScout")); Set(sword,"recallSound",Sound("SwordRecall")); }
                foreach (var hack in All<HackPresenter>()) { Set(hack,"startSound",Sound("HackStart")); Set(hack,"completeSound",Sound("HackComplete")); }
                foreach (var alert in All<AlertDetectedPresenter>())
                { Set(alert,"presentationContext",context); Set(alert,"alertSound",Sound("Alert")); Set(alert,"suspicionSound",Sound("Suspicion")); }
                foreach (var door in All<SecurityDoorPresenter>()) { Set(door,"presentationContext",context); Set(door,"openSound",Sound("DoorOpen")); }
                foreach (var result in All<StageResultPresenter>())
                { Set(result,"presentationContext",context); Set(result,"clearedSound",Sound("ElevatorDepart")); Set(result,"failedSound",Sound("MissionFail")); }
                foreach (var menu in All<BattleMenuContext>()) Set(menu,"presentation",context);
                foreach (var menu in All<BattleMenuController>())
                {
                    Set(menu,"menuOpenSound",Sound("MenuOpen")); Set(menu,"menuCloseSound",Sound("MenuClose"));
                    Set(menu,"manualOpenSound",Sound("ManualOpen")); Set(menu,"manualCloseSound",Sound("ManualClose"));
                }
                foreach (var manual in All<BattleManualView>()) Set(manual,"pageTurnSound",Sound("PageTurn"));
                foreach (var button in All<BattleUiButton>()) { Set(button,"presentationContext",context); Set(button,"clickSound",Sound("UiClick")); }
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene()); EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            }
            AssetDatabase.SaveAssets();
        }
        finally { EditorSceneManager.OpenScene(original, OpenSceneMode.Single); }
        Debug.Log("1-1 전체 효과음 25종과 전투·검·발각·해킹·문·승강기·메뉴·수첩 연결 완료");
    }

    /// <summary>타격 종류에 따라 직접 음원을 연결하며 기존 VFX·수치·시간은 수정하지 않는다.</summary>
    private static void ConfigureCombat(string path)
    {
        var data = AssetDatabase.LoadAssetAtPath<CombatPresentationData>(path);
        if (data == null) throw new Exception("전투 데이터 누락: " + path);
        var serialized = new SerializedObject(data); var entries = serialized.FindProperty("entries");
        for (int i=0; i<entries.arraySize; i++)
        {
            var entry=entries.GetArrayElementAtIndex(i);
            var kind=(AttackPresentationKind)entry.FindPropertyRelative("attackKind").intValue;
            SoundData action;
            switch (kind)
            {
                case AttackPresentationKind.SwordThrow: action=Sound("SwordAttack"); break;
                case AttackPresentationKind.MeleeWithSword: action=Sound("SwordMelee"); break;
                case AttackPresentationKind.MeleeUnarmed: action=Sound("Unarmed"); break;
                case AttackPresentationKind.PlayerGun: action=Gun("Fire"); break;
                case AttackPresentationKind.EnemyMelee: action=Sound(path.Contains("망치")?"Hammer":"Club"); break;
                case AttackPresentationKind.EnemyRanged: action=Sound(path.Contains("큰못")?"Bow":"Crossbow"); break;
                default: throw new Exception("지원하지 않는 공격 종류: " + kind);
            }
            entry.FindPropertyRelative("playAudio").boolValue=true;
            entry.FindPropertyRelative("fireSound").objectReferenceValue=action;
            entry.FindPropertyRelative("hitSound").objectReferenceValue=kind==AttackPresentationKind.PlayerGun?Gun("Hit"):Sound("ImpactHit");
            entry.FindPropertyRelative("missSound").objectReferenceValue=kind==AttackPresentationKind.PlayerGun?Gun("Miss"):Sound("Dodge");
            entry.FindPropertyRelative("deathSound").objectReferenceValue=Sound("Death");
        }
        serialized.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(data);
    }

    /// <summary>반복 적용 시 기존 공유 에셋 GUID를 유지하고 두 후보의 원본 음원을 연결한다.</summary>
    private static void CreateSound(string name,float volume,bool ui)
    {
        var clips = new AudioClip[2];
        for (int i=0; i<clips.Length; i++)
        {
            string path=$"Assets/Art/Audio/Sfx/Stage11/{name}_{i+1:00}.wav";
            var importer=AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer==null) throw new Exception("효과음 원본 누락: " + path);
            importer.forceToMono=true; var settings=importer.defaultSampleSettings;
            settings.preloadAudioData=true; settings.loadType=AudioClipLoadType.DecompressOnLoad; settings.compressionFormat=AudioCompressionFormat.PCM;
            importer.defaultSampleSettings=settings; importer.SaveAndReimport();
            clips[i]=AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
        string assetPath=SoundRoot+name+".asset";
        var data=AssetDatabase.LoadAssetAtPath<SoundData>(assetPath);
        if (data==null) { data=ScriptableObject.CreateInstance<SoundData>(); AssetDatabase.CreateAsset(data,assetPath); }
        var serialized=new SerializedObject(data); var candidates=serialized.FindProperty("clips"); candidates.arraySize=2;
        for (int i=0;i<2;i++) candidates.GetArrayElementAtIndex(i).objectReferenceValue=clips[i];
        serialized.FindProperty("volume").floatValue=volume;
        serialized.FindProperty("pitchRange").vector2Value=new Vector2(.97f,1.03f);
        serialized.FindProperty("playDuringPause").boolValue=ui;
        serialized.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(data);
    }

    /// <summary>씬 재로드 뒤 필요한 모든 소비자와 후보 음원 연결을 검사한다.</summary>
    [MenuItem("Tools/S2/Audit 1-1 Full Audio Draft")]
    public static void Audit()
    {
        Guard(); string original=SceneManager.GetActiveScene().path;
        var report=new System.Text.StringBuilder("PASS\n");
        try
        {
            foreach (string path in PresentationMediaAuthoring.Scenes)
            {
                EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
                var context=All<BattlePresentationContext>().Single();
                foreach (var entry in Catalog)
                    if (!context.Audio.HasValidData(Sound(entry.name)) || Sound(entry.name).PlayDuringPause!=entry.ui) throw new Exception(path+" 효과음 데이터 누락: "+entry.name);
                foreach (var p in All<CombatActionPresenter>()) Require(p.HasValidReference()&&p.HasValidData(),path+" 전투");
                foreach (var p in All<SwordActionPresenter>()) Require(p.HasValidReference()&&p.HasValidData(),path+" 검");
                foreach (var p in All<HackPresenter>()) Require(p.HasValidReference()&&p.HasValidData(),path+" 해킹");
                foreach (var p in All<AlertDetectedPresenter>()) Require(p.HasValidReference()&&p.HasValidData(),path+" 발각");
                foreach (var p in All<SecurityDoorPresenter>()) Require(p.HasValidReference()&&p.HasValidData(),path+" 문");
                // 구형 Tset은 원래 결과 UI가 미연결이므로 새 오디오 참조만 검사한다.
                foreach (var p in All<StageResultPresenter>()) Require(p.HasValidData() && (path.EndsWith("Tset.unity") || p.HasValidReference()),path+" 결과");
                foreach (var p in All<BattleMenuController>()) Require(p.HasValidReference()&&p.HasValidData(),path+" 메뉴");
                foreach (var p in All<BattleManualView>()) Require(p.HasValidReference()&&p.HasValidData(),path+" 수첩");
                foreach (var p in All<BattleUiButton>()) Require(p.HasValidReference()&&p.HasValidData(),path+" 버튼");
                foreach (var p in All<ActorVisualController>())
                    if (p.CombatPresentationOverride!=null) foreach (var e in p.CombatPresentationOverride.Entries)
                        Require(e.PlayAudio&&context.Audio.HasValidData(e.FireSound)&&context.Audio.HasValidData(e.HitSound)&&context.Audio.HasValidData(e.MissSound)&&context.Audio.HasValidData(e.DeathSound),path+" 배우별 공격");
                report.AppendLine(path+": 25종 후보·정지 정책·전투·검·발각·해킹·문·결과·메뉴·수첩·버튼 연결 정상");
            }
        }
        finally { EditorSceneManager.OpenScene(original,OpenSceneMode.Single); }
        Directory.CreateDirectory("Temp/Stage11Audio"); File.WriteAllText("Temp/Stage11Audio/references.txt",report.ToString()); Debug.Log(report.ToString());
    }

    /// <summary>명시적 초기 적용 전 저장된 편집 상태인지 확인한다.</summary>
    private static void Guard() { if (EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().isDirty) throw new Exception("저장된 편집 씬에서 실행하세요."); }
    /// <summary>초기 조립과 검사에서 비활성 대상까지 읽는다.</summary>
    private static T[] All<T>() where T:UnityEngine.Object => UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include,FindObjectsSortMode.None);
    /// <summary>기존 총격 데이터 참조를 유지한다.</summary>
    private static SoundData Gun(string name) => AssetDatabase.LoadAssetAtPath<SoundData>("Assets/Data/Battle/Presentation/Sound/YujinGun"+name+".asset");
    /// <summary>초안 음원 데이터 참조를 읽는다.</summary>
    public static SoundData Sound(string name) => AssetDatabase.LoadAssetAtPath<SoundData>(SoundRoot+name+".asset");
    /// <summary>오류가 있는 연결은 결과 보고 전에 중단한다.</summary>
    private static void Require(bool valid,string name) { if(!valid)throw new Exception(name+" 오디오 연결 오류"); }
    /// <summary>초기 적용에 승인된 직렬화 참조를 저장하고 프리팹 인스턴스 수정도 기록한다.</summary>
    private static void Set(UnityEngine.Object target,string field,UnityEngine.Object value)
    {
        if(value==null)throw new Exception(target.name+" 연결 대상 누락: "+field);
        var serialized=new SerializedObject(target); var property=serialized.FindProperty(field);
        if(property==null)throw new Exception(target.name+" 필드 누락: "+field);
        property.objectReferenceValue=value; serialized.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(target);
        if(PrefabUtility.IsPartOfPrefabInstance(target))PrefabUtility.RecordPrefabInstancePropertyModifications(target);
    }
}
