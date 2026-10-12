using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>승인된 흰색 말풍선 원화를 1-1 템플릿에 연결하고 하단 진행 버튼을 제거한다.</summary>
public static class BattleSpeechBubbleAuthoring
{
    // 투명 여백을 보존한 원화 사본을 사용하는 프로젝트 경로다.
    private const string Art = "Assets/Art/UI/SpeechBubble/";

    /// <summary>기존 대사·화자·풀링을 유지하면서 화면 파츠만 연결한다.</summary>
    [MenuItem("Tools/S2/Apply White Battle Speech Bubble")]
    public static void Apply()
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.name != "BattleTest01" || scene.isDirty)
            throw new InvalidOperationException("저장된 BattleTest01 편집 상태가 필요합니다.");
        var tutorial = UnityEngine.Object.FindFirstObjectByType<BattleTutorialController>();
        if (tutorial == null) throw new InvalidOperationException("튜토리얼 참조가 필요합니다.");
        var presenter = (DialogueBubblePresenter)new SerializedObject(tutorial.Context.dialogue).FindProperty("bubblePresenter").objectReferenceValue;
        var template = (SpeechBubbleView)new SerializedObject(presenter).FindProperty("speechBubblePrefab").objectReferenceValue;
        var serialized = new SerializedObject(template);
        var body = (Image)serialized.FindProperty("bubbleImage").objectReferenceValue;
        var text = (TMP_Text)serialized.FindProperty("dialogueText").objectReferenceValue;
        body.sprite = Import("말풍선_9슬라이스_흰몸통_v002", new Rect(57, 135, 2058, 457), Vector4.one * 48);
        body.color = Color.white; body.type = Image.Type.Sliced; body.pixelsPerUnitMultiplier = 4;
        body.raycastTarget = false;
        text.color = new Color(.12f, .13f, .14f); text.raycastTarget = false;
        serialized.FindProperty("speakerNameColor").colorValue = new Color(.12f, .36f, .38f);
        var tail = MakeImage(template.transform, "Tail", new Vector2(54, 26), new Vector2(.5f, 1));
        tail.sprite = Import("말풍선_흰꼬리_v002", new Rect(148, 89, 1479, 709), Vector4.zero);
        tail.type = Image.Type.Simple; tail.color = Color.white;
        var join = MakeImage(template.transform, "TailJoin", new Vector2(47, 5), new Vector2(.5f, .5f));
        join.color = new Color(242f / 255, 242f / 255, 238f / 255);
        serialized.FindProperty("tailImage").objectReferenceValue = tail;
        serialized.FindProperty("tailJoinImage").objectReferenceValue = join;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        var old = tutorial.Context.presenter.transform.Find("Continue");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        EditorUtility.SetDirty(body); EditorUtility.SetDirty(text); EditorUtility.SetDirty(template);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        Debug.Log("하단 대화 버튼 제거 및 흰색 9슬라이스 말풍선·꼬리 연결을 완료했습니다.");
    }

    /// <summary>원화의 실제 가시 영역을 Sprite Rect로 등록하고 모서리 Border를 설정한다.</summary>
    private static Sprite Import(string name, Rect rect, Vector4 border)
    {
        string path = Art + name + ".png";
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.alphaIsTransparency = true; importer.mipmapEnabled = false; importer.maxTextureSize = 4096;
        importer.textureCompression = TextureImporterCompression.Uncompressed; importer.spritePixelsPerUnit = 100;
        var factories = new SpriteDataProviderFactories(); factories.Init();
        var provider = factories.GetSpriteEditorDataProviderFromObject(importer); provider.InitSpriteEditorDataProvider();
        var previous = provider.GetSpriteRects().FirstOrDefault();
        var id = previous != null ? previous.spriteID : GUID.Generate();
        provider.SetSpriteRects(new[] {new SpriteRect {name = name, rect = rect, border = border, pivot = new Vector2(.5f, .5f), alignment = (int)SpriteAlignment.Center, spriteID = id}});
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(new[] {new SpriteNameFileIdPair(name, id)});
        provider.Apply(); importer.SaveAndReimport();
        var sprite = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().SingleOrDefault();
        if (sprite == null) throw new InvalidOperationException("말풍선 Sprite 가져오기에 실패했습니다: " + path);
        return sprite;
    }

    /// <summary>명시적으로 연결할 꼬리 파츠를 만들며 클릭 판정은 막지 않는다.</summary>
    private static Image MakeImage(Transform parent, string name, Vector2 size, Vector2 pivot)
    {
        var child = parent.Find(name);
        var go = child != null ? child.gameObject : new GameObject(name, typeof(RectTransform), typeof(Image));
        var rect = go.GetComponent<RectTransform>(); rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0); rect.pivot = pivot; rect.sizeDelta = size;
        var image = go.GetComponent<Image>(); image.raycastTarget = false; return image;
    }
}
