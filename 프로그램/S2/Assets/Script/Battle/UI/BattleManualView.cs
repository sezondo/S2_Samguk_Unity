using System.Collections;
using UnityEngine;

/// <summary>독립 파츠로 조립된 다섯 페이지를 표시하고 종이만 넘긴다.</summary>
public sealed class BattleManualView : MonoBehaviour
{
    // 같은 루트의 화면 참조를 모은 주머니다.
    [SerializeField] private BattleMenuContext context;
    // 현재 페이지와 진행 중인 종이 넘김이다.
    private int page;
    private Coroutine turning;
    public int Page => page;
    public bool IsTurning => turning != null;

    /// <summary>책과 페이지 연결을 검사한다.</summary>
    private void Awake() { if (!HasValidReference()) enabled = false; }

    /// <summary>넓거나 좁은 창에서도 책 전체와 상하 안내가 화면 안에 들어오게 한다.</summary>
    private void LateUpdate()
    {
        var size = context.canvasRect.rect.size;
        float scale = Mathf.Min((size.x - 64f) / 1672f, (size.y - 180f) / 941f);
        context.book.localScale = Vector3.one * Mathf.Max(.1f, scale);
        var bar = (RectTransform)context.guideText.transform.parent;
        bar.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Min(size.x - 64, 1530));
        context.guideText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Min(size.x - 100, 1480));
    }

    /// <summary>창을 열 때 지정한 페이지와 이번 안내 문구를 즉시 표시한다.</summary>
    public void Open(int index, string guide)
    {
        ResetTurn();
        page = Mathf.Clamp(index, 0, context.pages.Length - 1);
        context.guideText.text = guide;
        Refresh();
    }

    /// <summary>이전·다음 또는 색인에서 요청한 페이지로 종이를 넘긴다.</summary>
    public void TurnTo(int index)
    {
        if (turning != null || index < 0 || index >= context.pages.Length || index == page) return;
        turning = StartCoroutine(Turn(index));
    }

    /// <summary>다음 펼침을 아래에 먼저 깔아 접힌 종이 뒤에도 다음 페이지가 보이게 한다.</summary>
    private IEnumerator Turn(int index)
    {
        SetTurningControls(true);
        bool forward = index > page;
        var old = forward ? context.rightPages[page] : context.leftPages[page];
        var incoming = forward ? context.leftPages[index] : context.rightPages[index];
        context.pages[index].SetActive(true);
        // 원래 색인 순서에 의존하면 이전 장은 현재 장 위에 놓인다. 활성 두 장의 순서를 명시한다.
        context.pages[index].transform.SetSiblingIndex(1);
        context.pages[page].transform.SetSiblingIndex(2);
        incoming.localScale = new Vector3(0, 1, 1);
        for (float t = 0; t < .28f; t += Time.unscaledDeltaTime)
        { old.localScale = new Vector3(Mathf.Cos(Mathf.SmoothStep(0, 1, t / .28f) * Mathf.PI * .5f), 1, 1); yield return null; }
        old.localScale = new Vector3(0, 1, 1);
        // 새 종이가 펼쳐질 때에도 반대편에는 기존 종이를 남긴다.
        context.pages[page].transform.SetSiblingIndex(1);
        context.pages[index].transform.SetSiblingIndex(2);
        for (float t = 0; t < .28f; t += Time.unscaledDeltaTime)
        { incoming.localScale = new Vector3(Mathf.Sin(Mathf.SmoothStep(0, 1, t / .28f) * Mathf.PI * .5f), 1, 1); yield return null; }
        incoming.localScale = old.localScale = Vector3.one;
        page = index;
        turning = null;
        Refresh();
        SetTurningControls(false);
    }

    /// <summary>넘김 중 덮기 버튼을 숨기고 색인과 페이지 이동 입력을 잠근다.</summary>
    private void SetTurningControls(bool busy)
    {
        context.manualClose.gameObject.SetActive(!busy);
        context.manualClose.interactable = !busy;
        context.previous.interactable = !busy && page > 0;
        context.next.interactable = !busy && page < context.pages.Length - 1;
        foreach (var tab in context.tabs) tab.interactable = !busy;
    }

    /// <summary>페이지를 닫거나 전환할 때 접힌 종이가 남지 않게 복원한다.</summary>
    public void ResetTurn()
    {
        if (turning != null) StopCoroutine(turning);
        turning = null;
        foreach (var rect in context.leftPages) rect.localScale = Vector3.one;
        foreach (var rect in context.rightPages) rect.localScale = Vector3.one;
        Refresh();
        SetTurningControls(false);
    }

    /// <summary>페이지와 색인, 경계의 이전·다음 버튼 상태를 함께 갱신한다.</summary>
    private void Refresh()
    {
        for (int i = 0; i < context.pages.Length; i++)
        {
            context.pages[i].SetActive(i == page);
            context.pages[i].transform.SetSiblingIndex(i + 1);
        }
        int tab = page;
        float[] positions = {130, 242, 354, 466, 578};
        context.activeTab.anchoredPosition = new Vector2(1480, -positions[tab]);
        for (int i = 0; i < context.tabs.Length; i++) ((BattleUiButton)context.tabs[i]).SetSelected(i == tab);
        context.pageNumber.text = $"{page + 1} / {context.pages.Length}";
        context.previous.interactable = page > 0;
        context.next.interactable = page < context.pages.Length - 1;
    }

    /// <summary>누락된 페이지를 임의로 생성하지 않고 명시적으로 오류를 알린다.</summary>
    public bool HasValidReference()
    {
        bool valid = context != null && context.book != null && context.canvasRect != null &&
            context.pages != null && context.pages.Length == 5 && context.leftPages?.Length == 5 && context.rightPages?.Length == 5 &&
            context.tabs?.Length == 5 && context.activeTab != null && context.pageNumber != null && context.guideText != null && context.previous != null && context.next != null && context.manualClose != null;
        if (valid) for (int i = 0; i < 5; i++) valid &= context.pages[i] != null && context.leftPages[i] != null && context.rightPages[i] != null;
        if (valid) foreach (var tab in context.tabs) valid &= tab is BattleUiButton;
        if (!valid) Debug.LogError("전투 수첩의 페이지와 화면 연결이 누락됐습니다.", this);
        return valid;
    }
}

