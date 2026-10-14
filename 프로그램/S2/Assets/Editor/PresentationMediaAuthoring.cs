using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>미디어 데이터와 세 배틀 씬의 명시적인 참조를 일회성으로 연결한다.</summary>
public static class PresentationMediaAuthoring
{
    public const string DataRoot = "Assets/Data/Battle/Presentation/";
    public static readonly string[] Scenes = { "Assets/Scenes/Test/BattleTest01.unity", "Assets/Scenes/Test/BattleTest02.unity", "Assets/Scenes/Tset.unity" };

    /// <summary>소스 프리팹·효과음 데이터·유진 전용 연출 데이터를 생성하고 씬을 저장한다.</summary>
    [MenuItem("Tools/S2/Apply Presentation Media Data")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty)
            throw new Exception("저장된 편집 씬에서만 미디어 참조를 연결할 수 있습니다.");
        Directory.CreateDirectory(DataRoot + "Sound");
        Directory.CreateDirectory(DataRoot + "Combat");
        Directory.CreateDirectory("Assets/Prefab/Battle/Presentation");
        AssetDatabase.Refresh();
        const string prefabPath = "Assets/Prefab/Battle/Presentation/OneShotAudioSource.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            var temporary = new GameObject("OneShotAudioSource");
            var source = temporary.AddComponent<AudioSource>();
            source.playOnAwake = false; source.loop = false; source.dopplerLevel = 0;
            prefab = PrefabUtility.SaveAsPrefabAsset(temporary, prefabPath);
            UnityEngine.Object.DestroyImmediate(temporary);
        }
        var fire = CreateSound("Fire", .7f);
        var hit = CreateSound("Hit", .65f);
        var miss = CreateSound("Miss", .5f);
        string yujinPath = DataRoot + "Combat/YujinCombat.asset";
        if (AssetDatabase.LoadAssetAtPath<CombatPresentationData>(yujinPath) == null &&
            !AssetDatabase.CopyAsset("Assets/Data/TestStage/Player/CombatPresentationDataTest.asset", yujinPath))
            throw new Exception("유진 전용 연출 데이터 복사 실패");
        var yujin = AssetDatabase.LoadAssetAtPath<CombatPresentationData>(yujinPath);
        var data = new SerializedObject(yujin);
        var entries = data.FindProperty("entries");
        for (int i = 0; i < entries.arraySize; i++)
        {
            var entry = entries.GetArrayElementAtIndex(i);
            bool gun = entry.FindPropertyRelative("attackKind").intValue == (int)AttackPresentationKind.PlayerGun;
            entry.FindPropertyRelative("playAudio").boolValue = gun;
            entry.FindPropertyRelative("fireSound").objectReferenceValue = gun ? fire : null;
            entry.FindPropertyRelative("hitSound").objectReferenceValue = gun ? hit : null;
            entry.FindPropertyRelative("missSound").objectReferenceValue = gun ? miss : null;
        }
        data.ApplyModifiedPropertiesWithoutUndo();
        // 씬의 해킹 터미널은 프리팹 인스턴스이므로 원본 데이터 참조도 함께 이관한다.
        const string terminalPath = "Assets/Prefab/Map/HackingObject/HackTerminal.prefab";
        var terminal = PrefabUtility.LoadPrefabContents(terminalPath);
        foreach (var hack in terminal.GetComponentsInChildren<HackPresenter>(true))
            Set(hack, "hackVfx", AssetDatabase.LoadAssetAtPath<VfxData>(DataRoot + "Vfx/HackCircuit.asset"));
        PrefabUtility.SaveAsPrefabAsset(terminal, terminalPath);
        PrefabUtility.UnloadPrefabContents(terminal);
        string original = SceneManager.GetActiveScene().path;
        foreach (string path in Scenes)
        {
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var vfx = UnityEngine.Object.FindObjectsByType<VfxManager>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single();
            var context = vfx.GetComponent<BattlePresentationContext>();
            if (context == null) context = vfx.gameObject.AddComponent<BattlePresentationContext>();
            var audio = vfx.GetComponent<AudioManager>();
            if (audio == null) audio = vfx.gameObject.AddComponent<AudioManager>();
            Set(audio, "sourcePrefab", prefab.GetComponent<AudioSource>());
            Set(context, "vfx", vfx); Set(context, "audio", audio);
            foreach (var presenter in UnityEngine.Object.FindObjectsByType<CombatActionPresenter>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Set(presenter, "presentationContext", context);
            foreach (var presenter in UnityEngine.Object.FindObjectsByType<SwordActionPresenter>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Set(presenter, "presentationContext", context);
            foreach (var presenter in UnityEngine.Object.FindObjectsByType<HackPresenter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Set(presenter, "presentationContext", context);
                Set(presenter, "hackVfx", AssetDatabase.LoadAssetAtPath<VfxData>(DataRoot + "Vfx/HackCircuit.asset"));
            }
            var root = GameObject.Find("Player Actor Root");
            if (root == null) throw new Exception(path + "에 유진 루트가 없습니다.");
            var visual = root.GetComponentsInChildren<ActorVisualController>(true).Single();
            Set(visual, "combatPresentationOverride", AssetDatabase.LoadAssetAtPath<CombatPresentationData>(yujinPath));
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene(original, OpenSceneMode.Single);
        Debug.Log("세 배틀 씬의 VFX·오디오 Context와 유진 총격 데이터 연결 완료");
    }

    /// <summary>세 씬의 모든 소비자가 누락 없이 연결됐는지 편집 상태에서 검사한다.</summary>
    [MenuItem("Tools/S2/Audit Presentation Media References")]
    public static void Audit()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty)
            throw new Exception("저장된 편집 씬에서 검사하세요.");
        string original = SceneManager.GetActiveScene().path;
        var report = new System.Text.StringBuilder("PASS\n");
        foreach (string path in Scenes)
        {
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var context = UnityEngine.Object.FindObjectsByType<BattlePresentationContext>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single();
            if (context.Vfx == null || context.Audio == null || !context.Audio.HasValidReference()) throw new Exception(path + " 재생 담당 누락");
            foreach (var combat in UnityEngine.Object.FindObjectsByType<CombatActionPresenter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (!combat.HasValidReference() || !combat.HasValidData()) throw new Exception(path + " 전투 연결 오류");
            foreach (var sword in UnityEngine.Object.FindObjectsByType<SwordActionPresenter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (!sword.HasValidReference() || !sword.HasValidData()) throw new Exception(path + " 검 연결 오류");
            foreach (var hack in UnityEngine.Object.FindObjectsByType<HackPresenter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (!hack.HasValidReference() || !hack.HasValidData()) throw new Exception(path + " 해킹 연결 오류");
            foreach (var visual in UnityEngine.Object.FindObjectsByType<ActorVisualController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (visual.CombatPresentationOverride != null)
                    foreach (var entry in visual.CombatPresentationOverride.Entries)
                    {
                        if (!context.Vfx.HasValidData(entry.HitVfx) || !context.Vfx.HasValidData(entry.MissVfx) ||
                            (entry.AttackerVfx != null && !context.Vfx.HasValidData(entry.AttackerVfx))) throw new Exception(path + " 배우 이펙트 누락");
                        if (entry.PlayAudio && (!context.Audio.HasValidData(entry.FireSound) || !context.Audio.HasValidData(entry.HitSound) || !context.Audio.HasValidData(entry.MissSound))) throw new Exception(path + " 배우 오디오 누락");
                    }
            var player = GameObject.Find("Player Actor Root").GetComponentInChildren<ActorVisualController>(true);
            if (player == null || player.CombatPresentationOverride == null || !player.CombatPresentationOverride.TryGetEntry(AttackPresentationKind.PlayerGun, out var gun) || !gun.PlayAudio) throw new Exception(path + " 유진 총격 연결 누락");
            report.AppendLine(path + ": 전투·검·해킹·배우 데이터·유진 총격 참조 정상");
        }
        EditorSceneManager.OpenScene(original, OpenSceneMode.Single);
        Directory.CreateDirectory("Temp/PresentationMedia"); File.WriteAllText("Temp/PresentationMedia/references.txt", report.ToString());
        Debug.Log(report.ToString());
    }

    /// <summary>원본 단발 음원을 가져오고 재생에 사용할 공유 데이터를 만든다.</summary>
    private static SoundData CreateSound(string kind, float volume)
    {
        var clips = new AudioClip[3];
        for (int i = 0; i < clips.Length; i++)
        {
            string path = $"Assets/Art/Audio/Sfx/Yujin/Pneumatic_{kind}_{i + 1:00}.wav";
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null) throw new Exception("효과음 가져오기 실패: " + path);
            importer.forceToMono = true;
            var settings = importer.defaultSampleSettings;
            settings.preloadAudioData = true;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.PCM;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
            clips[i] = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
        string assetPath = DataRoot + "Sound/YujinGun" + kind + ".asset";
        var result = AssetDatabase.LoadAssetAtPath<SoundData>(assetPath);
        if (result == null) { result = ScriptableObject.CreateInstance<SoundData>(); AssetDatabase.CreateAsset(result, assetPath); }
        var serialized = new SerializedObject(result);
        var candidates = serialized.FindProperty("clips"); candidates.arraySize = clips.Length;
        for (int i = 0; i < clips.Length; i++) candidates.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
        serialized.FindProperty("volume").floatValue = volume;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return result;
    }

    /// <summary>설치 도구에서 지정한 필수 참조만 직렬화해 저장한다.</summary>
    private static void Set(UnityEngine.Object target, string field, UnityEngine.Object value)
    {
        var serialized = new SerializedObject(target);
        var property = serialized.FindProperty(field);
        if (property == null) throw new Exception(target.name + "의 필드 누락: " + field);
        property.objectReferenceValue = value; serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
        if (PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
    }
}
