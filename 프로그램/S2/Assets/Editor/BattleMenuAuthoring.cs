using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>승인된 수첩 파츠와 메뉴 원화를 1-1 씬의 편집 가능한 UI로 조립한다.</summary>
public static class BattleMenuAuthoring
{
    // 원화의 독립 파츠와 조립 좌표를 가져온 프로젝트 경로다.
    private const string Art = "Assets/Art/UI/BattleManual/";
    private const string MenuArt = "Assets/Art/UI/BattleMenu/";
    // JSON 조립 자료의 읽기 전용 형식이다.
    [Serializable] private class Layout { public Asset[] assets; public Page[] pages; }
    [Serializable] private class Asset { public string key, file; }
    [Serializable] private class Page { public string title; public Layer[] layers; }
    [Serializable] private class Layer
    {
        public string id, kind, asset, parent, text, align, color, fit;
        public float[] rect;
        public float angle, font_size;
        public bool bold;
    }
    // 한 번 조립하는 동안 사용할 글꼴과 Sprite 표다.
    private static TMP_FontAsset font;
    private static Dictionary<string, Sprite> sprites;

    /// <summary>다른 튜토리얼 배치·대사는 건드리지 않고 메뉴 UI만 생성한다.</summary>
    [MenuItem("Tools/S2/Assemble Battle Manual and Menu")]
    public static void Assemble()
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.name != "BattleTest01" || scene.isDirty)
            throw new InvalidOperationException("저장된 BattleTest01 편집 상태가 필요합니다.");
        if (UnityEngine.Object.FindFirstObjectByType<BattleMenuContext>() != null)
            throw new InvalidOperationException("메뉴가 이미 조립돼 있습니다. 재생성 대신 기존 UI를 수정하십시오.");
        var layout = JsonUtility.FromJson<Layout>(File.ReadAllText(Art + "Layout.json"));
        var hud = UnityEngine.Object.FindFirstObjectByType<BattleHudController>();
        font = ((BattleHudAssetSet)new SerializedObject(hud).FindProperty("assetSet").objectReferenceValue).Font;
        sprites = new Dictionary<string, Sprite>();
        foreach (var asset in layout.assets) sprites.Add(asset.key, Import(asset.file, false));
        foreach (var file in Directory.GetFiles(MenuArt, "*.png")) sprites.Add(Path.GetFileNameWithoutExtension(file), Import(file.Replace('\\', '/'), true));

        var root = new GameObject("BattleMenuSystem");
        var context = root.AddComponent<BattleMenuContext>();
        context.controller = root.AddComponent<BattleMenuController>();
        context.manualView = root.AddComponent<BattleManualView>();
        Set(context.controller, "context", context); Set(context.manualView, "context", context);
        var canvas = Rect("MenuCanvas", root.transform, 1920, 1080);
        var screen = canvas.gameObject.AddComponent<Canvas>(); screen.renderMode = RenderMode.ScreenSpaceOverlay; screen.sortingOrder = 3000;
        var scaler = canvas.gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
        canvas.gameObject.AddComponent<GraphicRaycaster>(); context.canvasRect = canvas;

        context.manualOpen = Button("HUD 수첩", canvas, new Vector2(-154, -24), new Vector2(82, 104), "닫힌표지", "");
        context.menuOpen = Button("HUD 메뉴", canvas, new Vector2(-42, -24), new Vector2(82, 82), "닫기_빈버튼", "메뉴", 22);
        TopRight((RectTransform)context.manualOpen.transform); TopRight((RectTransform)context.menuOpen.transform);
        var shade = Rect("Modal Blocker", canvas, 0, 0); Stretch(shade);
        var shadeImage = shade.gameObject.AddComponent<Image>(); shadeImage.color = new Color(0, 0, 0, .72f); shadeImage.raycastTarget = true;
        context.overlay = shade.gameObject;
        BuildMenu(context, shade); BuildQuit(context, shade); BuildBook(context, shade, layout);

        var tutorial = UnityEngine.Object.FindFirstObjectByType<BattleTutorialController>();
        Set(tutorial.Context.presenter, "menuContext", context);
        var data = tutorial.Data;
        foreach (var step in data.steps.Where(x => x.kind == BattleTutorialStepKind.Guide))
        {
            // 문구에 의존하는 런타임 분기 대신 명시적 페이지 번호를 데이터에 기록한다.
            int number = int.Parse(step.id.Split(' ')[0]);
            step.manualPage = number == 16 ? 1 : number == 48 || number == 51 ? 3 : number == 77 ? 4 :
                number == 27 || number == 29 || number == 36 || number == 71 ? 2 : 0;
        }
        EditorUtility.SetDirty(data);
        if (!context.controller.HasValidReference() || !context.manualView.HasValidReference()) throw new InvalidOperationException("메뉴 연결 검증 실패");
        context.overlay.SetActive(false);
        Undo.RegisterCreatedObjectUndo(root, "전투 수첩·메뉴 조립");
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        Debug.Log("수첩 5페이지·메뉴·종료 확인창 조립과 튜토리얼 안내 연결을 완료했습니다.");
    }

    /// <summary>화면 검사에서 발견한 HUD 겹침만 저장된 UI에서 보정한다.</summary>
    [MenuItem("Tools/S2/Polish Battle Menu Layout")]
    public static void Polish()
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.isDirty) throw new InvalidOperationException("저장된 편집 씬이 필요합니다.");
        var c = UnityEngine.Object.FindFirstObjectByType<BattleMenuContext>();
        if (c == null || !c.controller.HasValidReference()) throw new InvalidOperationException("메뉴 참조가 필요합니다.");
        var canvas = c.canvasRect.GetComponent<Canvas>(); canvas.sortingOrder = 3000;
        ((RectTransform)c.manualOpen.transform).anchoredPosition = new Vector2(-154, -24);
        ((RectTransform)c.menuOpen.transform).anchoredPosition = new Vector2(-42, -24);
        ((RectTransform)c.manualOpen.transform).sizeDelta = new Vector2(82, 104);
        foreach (var button in new[] {c.menuOpen, c.menuClose, c.quitClose, c.previous, c.next})
            ((Image)button.targetGraphic).type = Image.Type.Simple;
        foreach (var button in c.canvasRect.GetComponentsInChildren<BattleUiButton>(true))
        {
            button.SetDecorations(button.GetComponentsInChildren<Graphic>(true).Where(x => x != button.targetGraphic).ToArray());
            EditorUtility.SetDirty(button);
        }
        c.activeTab.SetAsLastSibling();
        foreach (Transform child in c.activeTab.parent.Cast<Transform>().Where(x => x.name.StartsWith("tab_text_")).ToArray()) child.SetAsLastSibling();
        PolishManualBars(c);
        c.overlay.SetActive(false);
        PolishNotebookText(c);
        EditorUtility.SetDirty(canvas); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Debug.Log("메뉴·수첩 표시 순서와 HUD 버튼 위치를 보정했습니다.");
    }

    /// <summary>PNG를 투명 Sprite로 가져오고 메뉴 몸통은 모서리를 보존한다.</summary>
    private static Sprite Import(string path, bool slice)
    {
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true; importer.mipmapEnabled = false; importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 2048; importer.spritePixelsPerUnit = 100;
        importer.spriteBorder = slice ? new Vector4(32, 32, 32, 32) : Vector4.zero;
        importer.SaveAndReimport();
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null) throw new InvalidOperationException("Sprite 가져오기 실패: " + path);
        return sprite;
    }

    /// <summary>설정 준비 중·매뉴얼·종료 버튼을 독립 파츠로 배치한다.</summary>
    private static void BuildMenu(BattleMenuContext c, Transform parent)
    {
        var panel = Rect("메뉴", parent, 490, 650); Body(panel, "메뉴_빈바탕"); c.menu = panel.gameObject;
        Label("제목", panel, new Vector2(0, 235), new Vector2(320, 75), "메뉴", 50, "#eee9dd", true, true);
        c.menuClose = Button("메뉴 닫기", panel, new Vector2(181, 245), new Vector2(48, 48), "닫기_빈버튼", "×", 30);
        c.settings = Button("설정", panel, new Vector2(0, 85), new Vector2(360, 114), "버튼_비활성", "설정\n<size=19>준비 중</size>", 32);
        c.menuManual = Button("매뉴얼", panel, new Vector2(0, -53), new Vector2(360, 114), "버튼_청록", "매뉴얼", 32);
        c.menuQuit = Button("종료", panel, new Vector2(0, -191), new Vector2(360, 114), "버튼_비활성", "종료", 32);
        AddIcon(c.settings, "아이콘_설정"); AddIcon(c.menuManual, "아이콘_매뉴얼"); AddIcon(c.menuQuit, "아이콘_종료");
        c.settings.interactable = false;
    }

    /// <summary>종료 여부와 취소·실행 버튼을 별도 확인창에 배치한다.</summary>
    private static void BuildQuit(BattleMenuContext c, Transform parent)
    {
        var panel = Rect("종료 확인", parent, 720, 408); Body(panel, "종료확인_빈바탕"); c.quit = panel.gameObject;
        Label("제목", panel, new Vector2(0, 117), new Vector2(470, 62), "게임 종료", 39, "#eee9dd", true, true);
        Label("질문", panel, new Vector2(0, 30), new Vector2(610, 55), "정말로 종료하시겠습니까?", 29, "#eee9dd", false, true);
        c.quitClose = Button("종료 확인 닫기", panel, new Vector2(284, 122), new Vector2(48, 48), "닫기_빈버튼", "×", 30);
        c.quitCancel = Button("취소", panel, new Vector2(-157, -100), new Vector2(264, 86), "버튼_청록", "취소", 30);
        c.quitAccept = Button("게임 종료", panel, new Vector2(157, -100), new Vector2(264, 86), "버튼_종료확인", "종료", 30);
    }

    /// <summary>고정 제본·색인과 좌우 종이 내용을 구분해 다섯 페이지를 조립한다.</summary>
    private static void BuildBook(BattleMenuContext c, Transform parent, Layout layout)
    {
        var manual = Rect("수첩", parent, 0, 0); Stretch(manual); c.manual = manual.gameObject;
        c.book = Rect("조립 수첩", manual, 1672, 941);
        c.pages = new GameObject[5]; c.leftPages = new RectTransform[5]; c.rightPages = new RectTransform[5]; c.tabs = new UnityEngine.UI.Button[5];
        var fixedBack = Rect("고정 표지", c.book, 1672, 941);
        AddLayer(layout.pages[0].layers.Single(x => x.id == "fixed_cover"), fixedBack);
        for (int i = 0; i < 5; i++)
        {
            var page = Rect($"{i + 1:00} {layout.pages[i].title}", c.book, 1672, 941); c.pages[i] = page.gameObject;
            // 각 종이의 pivot을 제본에 두어 글과 그림이 함께 접히도록 한다.
            var left = Rect("왼쪽 종이와 내용", page, 836, 941); left.pivot = new Vector2(1, .5f); left.anchoredPosition = Vector2.zero;
            var right = Rect("오른쪽 종이와 내용", page, 836, 941); right.pivot = new Vector2(0, .5f); right.anchoredPosition = Vector2.zero;
            c.leftPages[i] = left; c.rightPages[i] = right;
            foreach (var layer in layout.pages[i].layers.Where(x => x.parent != "fixed"))
                AddLayer(layer, layer.parent == "left" ? left : right, layer.parent == "left" ? 0 : 836);
            page.gameObject.SetActive(i == 0);
        }
        var fixedFront = Rect("제본과 색인", c.book, 1672, 941);
        foreach (var layer in layout.pages[0].layers.Where(x => x.parent == "fixed" && x.id != "fixed_cover"))
        {
            var obj = AddLayer(layer, fixedFront);
            if (layer.id == "active_tab") c.activeTab = obj;
            if (layer.id == "close_button") c.manualClose = ConfigureButton(obj, obj.GetComponent<Image>());
            if (layer.id.StartsWith("tab_") && !layer.id.StartsWith("tab_text_"))
                c.tabs[int.Parse(layer.id.Substring(4))] = ConfigureButton(obj, obj.GetComponent<Image>());
        }
        c.tabs[4] = UnityEngine.Object.Instantiate(c.tabs[3], fixedFront); c.tabs[4].name = "tab_4";
        var fifthLabel = UnityEngine.Object.Instantiate(fixedFront.Find("tab_text_3").gameObject, fixedFront); fifthLabel.name = "tab_text_4";
        string[] tabNames = {"이동", "시야", "전투", "검", "침입"};
        for (int i = 0; i < 5; i++)
        {
            ((RectTransform)c.tabs[i].transform).anchoredPosition = new Vector2(1477, -(124 + i * 112));
            var label = fixedFront.Find("tab_text_" + i).GetComponent<TMP_Text>();
            label.text = tabNames[i]; label.rectTransform.anchoredPosition = new Vector2(1484, -(151 + i * 112));
        }
        c.activeTab.SetAsLastSibling();
        foreach (Transform child in fixedFront.Cast<Transform>().Where(x => x.name.StartsWith("tab_text_")).ToArray()) child.SetAsLastSibling();
        c.previous = Button("이전 페이지", manual, new Vector2(-190, 44), new Vector2(140, 56), "닫기_빈버튼", "이전", 23);
        c.next = Button("다음 페이지", manual, new Vector2(190, 44), new Vector2(140, 56), "닫기_빈버튼", "다음", 23);
        Bottom((RectTransform)c.previous.transform); Bottom((RectTransform)c.next.transform);
        c.pageNumber = Label("쪽수", manual, new Vector2(0, 44), new Vector2(180, 50), "1 / 5", 23, "#eee9dd", true, true); Bottom(c.pageNumber.rectTransform);
        c.guideText = Label("이번 단계 안내", manual, new Vector2(0, -47), new Vector2(1480, 82), "", 27, "#eee9dd", false, true);
        c.guideText.rectTransform.anchorMin = c.guideText.rectTransform.anchorMax = new Vector2(.5f, 1);
        c.guideText.enableAutoSizing = true; c.guideText.fontSizeMin = 20; c.guideText.fontSizeMax = 27;
        PolishManualBars(c);
        PolishNotebookText(c);
        BattleManualPolish.PolishContext(c);
    }

    /// <summary>실제 한글 글꼴 폭에 맞춰 메모 줄바꿈과 덮기 버튼 옆 각주 공간을 정리한다.</summary>
    private static void PolishNotebookText(BattleMenuContext c)
    {
        foreach (var text in c.pages.SelectMany(x => x.GetComponentsInChildren<TMP_Text>(true)))
        {
            if (text.name == "intro" && text.transform.IsChildOf(c.pages[0].transform))
            { text.text = "3칸 이동에 행동력(AP) 1을 사용합니다.\n시작 칸은 거리에 포함하지 않습니다."; text.fontSizeMax = 23; }
            if (text.name == "right_note_610") text.text = "AP는 행동력입니다.\n부족하면 턴을 종료하세요.";
            if (text.name == "right_note_636") text.text = "터미널은 아군 시야와\n접속 사거리 안에 있어야 합니다.";
            if (text.name == "right_note_655")
            { text.text = "배치된 검을 다시 움직일 수 있습니다.\n검이 떨어져 있으면\n근접 공격은 맨손으로 합니다."; text.fontSizeMax = 22; text.fontSizeMin = 20; }
            if (text.name == "fixed_results" || text.name == "blocked")
            {
                text.rectTransform.sizeDelta = new Vector2(290, 66);
                text.rectTransform.anchoredPosition = new Vector2(66, -807);
                text.fontSize = text.fontSizeMax = 18; text.fontSizeMin = 16;
            }
            EditorUtility.SetDirty(text);
        }
    }

    /// <summary>상하 안내 뒤를 가려 기존 HUD가 문구와 겹치지 않게 하고 HUD 수첩 이름을 붙인다.</summary>
    private static void PolishManualBars(BattleMenuContext c)
    {
        font = c.guideText.font;
        if (c.manual.transform.Find("상단 안내 바탕") == null)
        {
            var top = Rect("상단 안내 바탕", c.manual.transform, 1530, 90);
            top.anchorMin = top.anchorMax = new Vector2(.5f, 1); top.anchoredPosition = new Vector2(0, -47);
            var image = top.gameObject.AddComponent<Image>(); image.color = new Color(.015f, .025f, .025f, .97f); image.raycastTarget = false;
            c.guideText.transform.SetParent(top, false);
            c.guideText.rectTransform.anchorMin = c.guideText.rectTransform.anchorMax = c.guideText.rectTransform.pivot = new Vector2(.5f, .5f);
            c.guideText.rectTransform.anchoredPosition = Vector2.zero;
            var bottom = Rect("쪽수 안내 바탕", c.manual.transform, 650, 76); Bottom(bottom); bottom.anchoredPosition = new Vector2(0, 44);
            var footer = bottom.gameObject.AddComponent<Image>(); footer.color = new Color(.015f, .025f, .025f, .97f); footer.raycastTarget = false;
            bottom.SetSiblingIndex(c.previous.transform.GetSiblingIndex());
        }
        BattleManualPolish.PolishHud(c);
    }

    /// <summary>원본 좌표의 이미지 또는 수정 가능한 TMP 문구를 생성한다.</summary>
    private static RectTransform AddLayer(Layer layer, Transform parent, float offset = 0)
    {
        var r = Rect(layer.id, parent, layer.rect[2], layer.rect[3]);
        r.anchorMin = r.anchorMax = new Vector2(0, 1); r.pivot = new Vector2(0, 1);
        r.anchoredPosition = new Vector2(layer.rect[0] - offset, -layer.rect[1]);
        if (layer.kind == "image")
        {
            var image = r.gameObject.AddComponent<Image>(); image.sprite = sprites[layer.asset]; image.raycastTarget = false;
            image.preserveAspect = layer.fit == "contain";
        }
        else
        {
            var text = r.gameObject.AddComponent<TextMeshProUGUI>(); text.font = font; text.text = layer.text;
            text.fontSize = layer.font_size; text.fontStyle = layer.bold ? FontStyles.Bold : FontStyles.Normal;
            text.alignment = layer.align == "center" ? TextAlignmentOptions.Top : TextAlignmentOptions.TopLeft;
            ColorUtility.TryParseHtmlString(layer.color, out var color); text.color = color;
            text.raycastTarget = false; text.textWrappingMode = TextWrappingModes.Normal; text.overflowMode = TextOverflowModes.Overflow;
            // 원화의 폰트와 TMP 자폭 차이 때문에 같은 영역 안에서만 작은 보정을 허용한다.
            text.enableAutoSizing = true; text.fontSizeMax = layer.font_size; text.fontSizeMin = layer.font_size * .8f;
        }
        if (layer.angle != 0) r.localRotation = Quaternion.Euler(0, 0, -layer.angle);
        return r;
    }

    /// <summary>몸통과 별도 아이콘을 조립하며 아이콘은 클릭을 가리지 않는다.</summary>
    private static void AddIcon(UnityEngine.UI.Button button, string key)
    {
        var r = Rect("아이콘", button.transform, 56, 56); r.anchoredPosition = new Vector2(-112, 0); Body(r, key, false);
        var text = button.GetComponentInChildren<TMP_Text>(); text.rectTransform.anchoredPosition = new Vector2(35, 0); text.rectTransform.sizeDelta = new Vector2(205, 85);
        ((BattleUiButton)button).SetDecorations(button.GetComponentsInChildren<Graphic>().Where(x => x != button.targetGraphic).ToArray());
    }

    /// <summary>버튼의 보통·호버·눌림·선택·비활성 색을 명시적으로 작성한다.</summary>
    private static BattleUiButton ConfigureButton(RectTransform r, Image image)
    {
        image.raycastTarget = true;
        var button = r.gameObject.AddComponent<BattleUiButton>(); button.targetGraphic = image;
        var colors = button.colors; colors.normalColor = Color.white; colors.highlightedColor = new Color(.7f, 1f, .95f);
        colors.pressedColor = new Color(.35f, .65f, .62f); colors.selectedColor = new Color(.8f, 1f, .96f);
        colors.disabledColor = new Color(.36f, .38f, .38f, .65f); colors.fadeDuration = .09f; button.colors = colors;
        var navigation = button.navigation; navigation.mode = Navigation.Mode.None; button.navigation = navigation;
        return button;
    }

    /// <summary>독립 몸통과 글자를 가진 조작 버튼을 만든다.</summary>
    private static BattleUiButton Button(string name, Transform parent, Vector2 position, Vector2 size, string sprite, string text, float fontSize = 25)
    {
        var r = Rect(name, parent, size.x, size.y); r.anchoredPosition = position;
        var image = Body(r, sprite); var button = ConfigureButton(r, image);
        if (sprite == "닫기_빈버튼") image.type = Image.Type.Simple;
        if (!string.IsNullOrEmpty(text)) Label("문구", r, Vector2.zero, size * .85f, text, fontSize, "#eee9dd", true, true);
        button.SetDecorations(r.GetComponentsInChildren<Graphic>().Where(x => x != image).ToArray());
        return button;
    }

    /// <summary>그림과 텍스트 배치에 사용할 중앙 기준 사각형을 만든다.</summary>
    private static RectTransform Rect(string name, Transform parent, float width, float height)
    {
        var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); r.SetParent(parent, false);
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f); r.sizeDelta = new Vector2(width, height); return r;
    }

    /// <summary>투명 원화 몸통을 표시하며 메뉴 파츠의 모서리를 보존한다.</summary>
    private static Image Body(RectTransform r, string sprite, bool sliced = true)
    {
        var image = r.gameObject.AddComponent<Image>(); image.sprite = sprites[sprite]; image.raycastTarget = false;
        image.type = sliced && image.sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple; return image;
    }

    /// <summary>한글 글꼴을 사용한 별도 UI 문구를 만든다.</summary>
    private static TMP_Text Label(string name, Transform parent, Vector2 position, Vector2 size, string value, float fontSize, string color, bool bold, bool center)
    {
        var r = Rect(name, parent, size.x, size.y); r.anchoredPosition = position;
        var text = r.gameObject.AddComponent<TextMeshProUGUI>(); text.font = font; text.text = value; text.fontSize = fontSize;
        text.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal; text.alignment = center ? TextAlignmentOptions.Center : TextAlignmentOptions.TopLeft;
        ColorUtility.TryParseHtmlString(color, out var parsed); text.color = parsed; text.raycastTarget = false; return text;
    }

    /// <summary>부모 화면 전체를 덮는 입력 차단 영역으로 설정한다.</summary>
    private static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
    /// <summary>HUD 버튼을 오른쪽 위에 고정한다.</summary>
    private static void TopRight(RectTransform r) { r.anchorMin = r.anchorMax = r.pivot = Vector2.one; }
    /// <summary>쪽수와 페이지 이동 버튼을 화면 아래에 고정한다.</summary>
    private static void Bottom(RectTransform r) { r.anchorMin = r.anchorMax = new Vector2(.5f, 0); }
    /// <summary>Inspector의 명시적 참조를 저장한다.</summary>
    private static void Set(UnityEngine.Object target, string field, UnityEngine.Object value)
    { var so = new SerializedObject(target); so.FindProperty(field).objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
}
