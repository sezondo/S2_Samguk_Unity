using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>기존 종이와 도식을 유지하면서 수첩 색인·문구·조작 영역을 정돈한다.</summary>
public static class BattleManualPolish
{
    /// <summary>저장된 전투 씬의 수첩만 개선하고 명시적 참조를 저장한다.</summary>
    [MenuItem("Tools/S2/Polish Manual Reading Experience")]
    public static void Apply()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.name != "BattleTest01" || scene.isDirty)
            throw new InvalidOperationException("저장된 BattleTest01 편집 상태가 필요합니다.");
        var c = UnityEngine.Object.FindFirstObjectByType<BattleMenuContext>();
        if (c == null || c.tabs.Length < 4) throw new InvalidOperationException("기존 수첩 연결이 필요합니다.");
        Undo.RegisterFullObjectHierarchyUndo(c.gameObject, "수첩 읽기 환경 개선");
        PolishContext(c);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Debug.Log("수첩 5개 색인과 읽기 배치 개선을 저장했습니다.");
    }
    /// <summary>기존 또는 신규 수첩의 명시적 참조를 바탕으로 같은 배치와 문구를 적용한다.</summary>
    public static void PolishContext(BattleMenuContext c)
    {
        var front = c.activeTab.parent;
        if (c.tabs.Length == 4)
        {
            var buttons = c.tabs.ToList();
            var clone = UnityEngine.Object.Instantiate(c.tabs[3], front); clone.name = "tab_4";
            buttons.Add(clone); c.tabs = buttons.ToArray();
            var caption = UnityEngine.Object.Instantiate(front.Find("tab_text_3").gameObject, front);
            caption.name = "tab_text_4";
        }
        string[] names = {"이동", "시야", "전투", "검", "침입"};
        for (int i = 0; i < 5; i++)
        {
            var r = (RectTransform)c.tabs[i].transform;
            r.anchoredPosition = new Vector2(1477, -(124 + i * 112));
            var label = front.Find("tab_text_" + i).GetComponent<TMP_Text>();
            label.text = names[i]; label.rectTransform.anchoredPosition = new Vector2(1484, -(151 + i * 112));
            label.transform.SetAsLastSibling();
        }
        c.activeTab.SetAsLastSibling();
        for (int i = 0; i < 5; i++) front.Find("tab_text_" + i).SetAsLastSibling();
        // 덮기는 종이 내용에서 분리한 고정 조작 줄에 두되 넘김 중에는 통째로 숨긴다.
        var close = (RectTransform)c.manualClose.transform;
        close.SetParent(c.manual.transform, false);
        close.anchorMin = close.anchorMax = new Vector2(.5f, 0);
        close.pivot = new Vector2(.5f,.5f);
        close.sizeDelta = new Vector2(190, 60); close.anchoredPosition = new Vector2(430, 44);
        var closeLabel = front.Find("close_text");
        if (closeLabel != null)
        {
            closeLabel.SetParent(close, false);
            var r = (RectTransform)closeLabel; r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f,.5f);
            r.sizeDelta = new Vector2(175, 50); r.anchoredPosition = Vector2.zero;
            var t = closeLabel.GetComponent<TMP_Text>(); t.fontSize = 23; t.alignment = TextAlignmentOptions.Center;
        }
        ((BattleUiButton)c.manualClose).SetDecorations(close.GetComponentsInChildren<Graphic>(true).Where(x => x != c.manualClose.targetGraphic).ToArray());
        ((RectTransform)c.manual.transform.Find("쪽수 안내 바탕")).sizeDelta = new Vector2(1120, 76);
        foreach (var text in c.pages.SelectMany(p => p.GetComponentsInChildren<TMP_Text>(true)))
        {
            text.color = text.name.StartsWith("step_number_") || text.name.Contains("note_mark_") ? new Color(.95f,.94f,.9f,1) : new Color(.12f,.13f,.12f,1);
            text.characterSpacing = 0; text.wordSpacing = 0;
            text.lineSpacing = 8;
            if (text.name == "left_title" || text.name == "right_title")
            { text.fontStyle = FontStyles.Bold; text.fontSize = text.fontSizeMax = 47; text.fontSizeMin = 43; }
            if (text.name == "cover_rule") text.text = "공격 방향에 따라 엄폐 효과가 달라집니다.\n엄폐해도 공격을 완전히 막지는 못합니다.";
            if (text.name == "fixed_results") text.text = "일부 공격은 학습을 위해\n명중·회피 결과가 정해져 있습니다.";
            if (text.name == "blocked") text.text = "접속이 안 되면 거리와\n남은 행동력을 확인하세요.";
            if (text.name == "step_3" && text.transform.IsChildOf(c.pages[1].transform)) text.text = "검으로 시야 밖을 정찰한다.";
            if (text.name == "right_note_655") text.text = "배치된 검도 다시 이동할 수 있습니다.\n검이 떨어져 있으면\n근접 공격은 맨손으로 합니다.";
            if (text.name == "right_note_636") text.text = "아군 시야와 접속 사거리 안의\n터미널만 선택할 수 있습니다.";

            EditorUtility.SetDirty(text);
        }
        // 기존 HUD 위로 안내 문장이 비치지 않도록 바탕을 완전히 불투명하게 한다.
        c.manual.transform.Find("상단 안내 바탕").GetComponent<Image>().color = new Color(.025f,.035f,.035f,1);
        c.manual.transform.Find("쪽수 안내 바탕").GetComponent<Image>().color = new Color(.025f,.035f,.035f,1);
        EditorUtility.SetDirty(c);
        if (!c.controller.HasValidReference() || !c.manualView.HasValidReference()) throw new InvalidOperationException("수첩 연결 검사 실패");
        PolishHud(c);
        c.overlay.SetActive(false);
    }

    /// <summary>메뉴·수첩을 우상단에 두고 유진 상태 카드를 그 아래로 내린다.</summary>
    public static void PolishHud(BattleMenuContext c)
    {
        ((RectTransform)c.manualOpen.transform).anchoredPosition = new Vector2(-154, -24);
        ((RectTransform)c.menuOpen.transform).anchoredPosition = new Vector2(-42, -24);
        var name = c.manualOpen.transform.Find("수첩 이름");
        if (name != null) UnityEngine.Object.DestroyImmediate(name.gameObject);
        var menuText = c.menuOpen.transform.Find("문구");
        if (menuText != null) UnityEngine.Object.DestroyImmediate(menuText.gameObject);
        var icon = c.menuOpen.transform.Find("톱니바퀴");
        if (icon == null)
        {
            var obj = new GameObject("톱니바퀴", typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(c.menuOpen.transform, false); icon = obj.transform;
        }
        var iconRect = (RectTransform)icon;
        iconRect.anchorMin = iconRect.anchorMax = iconRect.pivot = new Vector2(.5f,.5f);
        iconRect.anchoredPosition = Vector2.zero; iconRect.sizeDelta = new Vector2(50,50);
        var image = icon.GetComponent<Image>();
        image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/BattleMenu/아이콘_설정.png");
        if (image.sprite == null) throw new InvalidOperationException("메뉴 톱니바퀴 Sprite가 필요합니다.");
        image.preserveAspect = true; image.raycastTarget = false;
        ((BattleUiButton)c.menuOpen).SetDecorations(new Graphic[] {image});
        var panel = UnityEngine.Object.FindFirstObjectByType<BattleHudUnitCardPanelController>();
        if (panel == null) throw new InvalidOperationException("유진 상태 카드 패널이 필요합니다.");
        var card = (RectTransform)new SerializedObject(panel).FindProperty("unitCardRoot").objectReferenceValue;
        if (card == null) throw new InvalidOperationException("유진 상태 카드 루트가 필요합니다.");
        var position = card.anchoredPosition; position.y = -160; card.anchoredPosition = position;
        EditorUtility.SetDirty(card); PrefabUtility.RecordPrefabInstancePropertyModifications(card);
    }

}
