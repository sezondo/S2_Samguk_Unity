using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>승인된 후일담 원고와 원화를 명시적 메뉴로 조립한다. 시작 스토리와 캠페인 저장은 변경하지 않는다.</summary>
public static class Stage11EpilogueAuthoring
{
    // 후일담의 원화·시퀀스·단독 미리보기 씬 경로다.
    public const string Art = "Assets/Art/Story/Stage_1-1/Epilogue";
    public const string Sequence = "Assets/Data/Story/CampaignPrototype/Stage_1-1_Post.asset";
    public const string Preview = "Assets/Scenes/Test/StoryEpilogueTest.unity";
    // 승인된 1672×941 원화의 공유 경계다. 좌상단 정규화 좌표로 변환해 사용한다.
    public static readonly Vector2[][] Cuts = {
        Polygon(0,0, 1672,0, 1672,290, 0,290),
        Polygon(0,290, 1672,290, 1672,675, 0,675),
        Polygon(0,675, 750,675, 740,816, 0,816),
        Polygon(750,675, 1672,675, 1672,816, 740,816)
    };

    /// <summary>픽셀 경계를 페이지 좌상단 기준 0~1 꼭짓점으로 바꾼다.</summary>
    private static Vector2[] Polygon(params float[] pixels)
    {
        var result = new Vector2[pixels.Length / 2];
        for (int i = 0; i < result.Length; i++) result[i] = new Vector2(pixels[i*2]/1672f, pixels[i*2+1]/941f);
        return result;
    }

    /// <summary>원화를 파츠로 추출하고 표시등 상태·후일담 명령을 생성한다.</summary>
    [MenuItem("Tools/S2/Apply 1-1 Epilogue")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("저장된 편집 씬에서 후일담 적용을 실행해야 합니다.");
        string manuscript = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../자료/시나리오/1-1후일담_적용원고.md"));
        if (!File.Exists(manuscript)) throw new FileNotFoundException("후일담 적용 원고가 없습니다.", manuscript);
        BuildArt();
        var sequence = AssetDatabase.LoadAssetAtPath<StorySequenceData>(Sequence);
        if (sequence == null) throw new InvalidOperationException("기존 1-1 후일담 데이터가 필요합니다.");
        var commands = new List<Action<SerializedProperty>>();
        commands.Add(p => Set(p, StoryCommandType.ChangeBackground, AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Story/Stage_1-1/Stage11_Black.png")));
        commands.Add(p => { Set(p, StoryCommandType.Fade); p.FindPropertyRelative("duration").floatValue = .25f; });
        foreach (string raw in File.ReadAllLines(manuscript))
        {
            string line = raw.Trim();
            if (line.StartsWith("독백 ") || line.StartsWith("유진 ") || line.StartsWith("금두꺼비 "))
            {
                int space = line.IndexOf(' ');
                string speaker = line.Substring(0, space), body = line.Substring(space + 1).Trim();
                commands.Add(p => {
                    Set(p, StoryCommandType.Dialogue);
                    p.FindPropertyRelative("speakerName").stringValue = speaker == "독백" ? "유진" : speaker;
                    p.FindPropertyRelative("hideSpeakerName").boolValue = speaker == "독백";
                    p.FindPropertyRelative("dialogueText").stringValue = body;
                    p.FindPropertyRelative("useTypewriter").boolValue = true;
                    p.FindPropertyRelative("charactersPerSecond").floatValue = 34f;
                });
            }
            else if (line.StartsWith("엘레베이터 안")) AddCut(commands, 0, "elevator-interior", 70);
            else if (line.StartsWith("승강기로 올라가는")) AddCut(commands, 1, "hanseong-panorama", 70);
            else if (line.Contains("70%")) AddCut(commands, 2, "arrival-indicator", 70);
            else if (line.Contains("80%")) AddReplacement(commands, 80);
            else if (line.Contains("90%")) AddReplacement(commands, 90);
            else if (line.StartsWith("승강기가 다 도착")) AddReplacement(commands, 100);
            else if (line.StartsWith("문이 조금만")) AddCut(commands, 3, "market-door", 100);
            else if (line.Length > 0 && line != "검은화면" && !line.StartsWith("[컷신 구성"))
                throw new InvalidOperationException("해석하지 못한 후일담 원고 줄: " + line);
        }
        commands.Add(p => Set(p, StoryCommandType.Wait, null, 1.2f));
        commands.Add(p => { Set(p, StoryCommandType.Fade, null, .45f); p.FindPropertyRelative("fadeDirection").enumValueIndex = (int)StoryFadeDirection.Out; });
        var serialized = new SerializedObject(sequence);
        serialized.FindProperty("displayName").stringValue = "1-1 후일담 — 저잣거리로";
        serialized.FindProperty("overrideComicViewport").boolValue = true;
        serialized.FindProperty("comicViewport").rectValue = new Rect(.04f,.18f,.92f,.82f);
        var array = serialized.FindProperty("commands");
        array.arraySize = commands.Count;
        for (int i = 0; i < commands.Count; i++) commands[i](array.GetArrayElementAtIndex(i));
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(sequence);
        AssetDatabase.SaveAssets();
        Debug.Log($"1-1 후일담 적용 완료: 명령 {commands.Count}개, 컷 4개, 표시등 교체 3회.");
    }

    /// <summary>시작 스토리의 기본 연결을 보존한 별도 단독 미리보기 씬을 만든다.</summary>
    [MenuItem("Tools/S2/Open 1-1 Epilogue Preview")]
    public static void OpenPreview()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("현재 씬을 먼저 저장하고 편집 상태에서 실행해야 합니다.");
        if (!File.Exists(Preview) && !AssetDatabase.CopyAsset("Assets/Scenes/Test/StoryTest.unity", Preview))
            throw new InvalidOperationException("후일담 미리보기 씬을 복사하지 못했습니다.");
        var scene = EditorSceneManager.OpenScene(Preview);
        StoryTestLauncher launcher = null;
        foreach (var root in scene.GetRootGameObjects())
            foreach (var candidate in root.GetComponentsInChildren<StoryTestLauncher>(true)) launcher = candidate;
        if (launcher == null) throw new InvalidOperationException("미리보기 씬의 StoryTestLauncher 연결이 필요합니다.");
        var serialized = new SerializedObject(launcher);
        serialized.FindProperty("sampleSequence").objectReferenceValue = AssetDatabase.LoadAssetAtPath<StorySequenceData>(Sequence);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.SaveScene(scene);
    }

    /// <summary>명령 슬롯의 이전 값을 비우고 새 종류·그림·시간을 설정한다.</summary>
    private static void Set(SerializedProperty p, StoryCommandType type, Sprite sprite = null, float duration = 0f)
    {
        p.FindPropertyRelative("commandType").enumValueIndex = (int)type;
        p.FindPropertyRelative("speakerName").stringValue = "";
        p.FindPropertyRelative("hideSpeakerName").boolValue = false;
        p.FindPropertyRelative("dialogueText").stringValue = "";
        p.FindPropertyRelative("useTypewriter").boolValue = false;
        p.FindPropertyRelative("charactersPerSecond").floatValue = 0f;
        p.FindPropertyRelative("visualSprite").objectReferenceValue = sprite;
        p.FindPropertyRelative("standingPosition").enumValueIndex = 0;
        p.FindPropertyRelative("comicPanelSprites").arraySize = 0;
        p.FindPropertyRelative("comicCutId").stringValue = "";
        p.FindPropertyRelative("comicCutVertices").arraySize = 0;
        p.FindPropertyRelative("fadeDirection").enumValueIndex = 0;
        p.FindPropertyRelative("duration").floatValue = duration;
    }

    /// <summary>대화창을 숨긴 뒤 페이지의 지정 컷만 서서히 공개한다.</summary>
    private static void AddCut(List<Action<SerializedProperty>> list, int cut, string id, int state)
    {
        list.Add(p => Set(p, StoryCommandType.HideDialogue));
        list.Add(p => {
            Set(p, StoryCommandType.ShowComicCut, Page(state), .45f);
            p.FindPropertyRelative("comicCutId").stringValue = id;
            var vertices = p.FindPropertyRelative("comicCutVertices");
            vertices.arraySize = Cuts[cut].Length;
            for (int i=0; i<vertices.arraySize; i++) vertices.GetArrayElementAtIndex(i).vector2Value = Cuts[cut][i];
        });
    }

    /// <summary>표시등 그림만 교체하며 공개 컷을 다시 생성하거나 재진입하지 않는다.</summary>
    private static void AddReplacement(List<Action<SerializedProperty>> list, int state)
    {
        list.Add(p => Set(p, StoryCommandType.HideDialogue));
        list.Add(p => Set(p, StoryCommandType.ReplaceComicPage, Page(state)));
        list.Add(p => Set(p, StoryCommandType.Wait, null, .12f));
    }

    /// <summary>같은 규격으로 조립한 표시등 단계별 페이지를 읽는다.</summary>
    private static Sprite Page(int state) => AssetDatabase.LoadAssetAtPath<Sprite>($"{Art}/Stage11_Epilogue_{state}.png");

    /// <summary>승인 원화의 켜진 램프 파츠를 복제해 80·90·100% 상태와 독립 컷 파츠를 조립한다.</summary>
    private static void BuildArt()
    {
        var original = new Texture2D(2,2,TextureFormat.RGBA32,false);
        if (!original.LoadImage(File.ReadAllBytes($"{Art}/Stage11_Epilogue_70.png")) || original.width != 1672 || original.height != 941)
            throw new InvalidOperationException("승인 원화는 1672×941 PNG여야 합니다.");
        string parts = Art + "/Parts";
        Directory.CreateDirectory(parts);
        // 원화의 7번째 켜진 램프 중심과 이후 세 램프의 실제 위치다. 원형 내부만 복제해 주변 금속판을 보존한다.
        int[] centers = { 127,183,239,295,351,407,463,519,575,631 };
        int lampY = original.height - 1 - 746;
        Color32[] source = original.GetPixels32();
        for (int state = 70; state <= 100; state += 10)
        {
            var pixels = (Color32[])source.Clone();
            for (int lamp = 7; lamp < state/10; lamp++)
                for (int dy=-22; dy<=22; dy++) for (int dx=-22; dx<=22; dx++)
                    if (dx*dx+dy*dy <= 22*22)
                        pixels[(lampY+dy)*1672+centers[lamp]+dx] = source[(lampY+dy)*1672+centers[6]+dx];
            var page = new Texture2D(1672,941,TextureFormat.RGBA32,false);
            page.SetPixels32(pixels); page.Apply();
            if (state != 70) File.WriteAllBytes($"{Art}/Stage11_Epilogue_{state}.png", page.EncodeToPNG());
            SaveCrop(page, new RectInt(35,700,675,103), $"{parts}/Indicator_{state}.png");
            UnityEngine.Object.DestroyImmediate(page);
        }
        SaveCrop(original, new RectInt(0,0,1672,290), parts + "/Interior.png");
        SaveCrop(original, new RectInt(0,290,1672,385), parts + "/Panorama.png");
        SaveCrop(original, new RectInt(750,675,922,141), parts + "/Door.png");
        UnityEngine.Object.DestroyImmediate(original);
        AssetDatabase.Refresh();
        foreach (string path in Directory.GetFiles(Art, "*.png", SearchOption.AllDirectories))
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path.Replace('\\','/'));
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }
        string archive = @"C:\Users\psdd2\Desktop\원화\CG\1-1_후일담\파츠";
        Directory.CreateDirectory(archive);
        foreach (string path in Directory.GetFiles(parts,"*.png")) File.Copy(path,Path.Combine(archive,Path.GetFileName(path)),true);
    }

    /// <summary>좌상단 픽셀 사각형의 파츠를 별도 PNG로 추출한다.</summary>
    private static void SaveCrop(Texture2D source, RectInt topRect, string path)
    {
        var part = new Texture2D(topRect.width,topRect.height,TextureFormat.RGBA32,false);
        part.SetPixels(source.GetPixels(topRect.x,source.height-topRect.y-topRect.height,topRect.width,topRect.height));
        part.Apply(); File.WriteAllBytes(path,part.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(part);
    }
}
