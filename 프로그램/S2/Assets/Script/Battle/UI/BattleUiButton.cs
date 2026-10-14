using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>기본 색상 상태 전환에 누름 크기 변화와 선택 표시를 더한다.</summary>
public sealed class BattleUiButton : Button
{
    // 실제 클릭·키보드 확정에서 사용할 명시적인 재생 담당과 정지 중 조작음이다.
    [SerializeField] private BattlePresentationContext presentationContext;
    [SerializeField] private SoundData clickSound;
    // 해당 페이지가 선택됐을 때 유지할 청록 표시다.
    [SerializeField] private Color selectedColor = new Color(.65f, 1f, .95f, 1f);
    // 몸통과 함께 색을 바꿀 명시적으로 연결된 글자·아이콘이다.
    [SerializeField] private Graphic[] decorations;
    // 색인의 선택 상태이며 버튼 누름과는 별개다.
    private bool selected;

    /// <summary>색인 선택을 표시하되 비활성·누름 상태가 우선하도록 갱신한다.</summary>
    public void SetSelected(bool value)
    {
        selected = value;
        DoStateTransition(currentSelectionState, true);
    }

    /// <summary>누를 때 작아지고 놓거나 포인터가 떠나면 원래 크기로 복귀한다.</summary>
    protected override void DoStateTransition(SelectionState state, bool instant)
    {
        // 전투가 정지된 메뉴에서는 잠금 해제·호버 색이 이전 비활성 색에 머물지 않게 즉시 반영한다.
        instant |= Mathf.Approximately(Time.timeScale, 0);
        base.DoStateTransition(state, instant);
        transform.localScale = state == SelectionState.Pressed ? Vector3.one * .96f : Vector3.one;
        var tint = state == SelectionState.Disabled ? colors.disabledColor : state == SelectionState.Pressed ? colors.pressedColor : Color.white;
        if (decorations != null) foreach (var decoration in decorations)
            if (decoration != null) decoration.CrossFadeColor(tint, instant ? 0 : colors.fadeDuration, true, true);
        if (selected && state != SelectionState.Disabled && state != SelectionState.Pressed && targetGraphic != null)
            targetGraphic.CrossFadeColor(selectedColor, instant ? 0 : colors.fadeDuration, true, true);
    }

    /// <summary>편집 조립 단계에서 버튼 문구와 아이콘의 상태 표시 참조를 연결한다.</summary>
    public void SetDecorations(Graphic[] references) { decorations = references; }

    /// <summary>활성 버튼의 실제 왼쪽 클릭에만 한 번 소리를 재생한다.</summary>
    public override void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left && IsActive() && IsInteractable()) PlayClick();
        base.OnPointerClick(eventData);
    }

    /// <summary>키보드 확정도 포인터 클릭과 같은 한 번의 조작음을 사용한다.</summary>
    public override void OnSubmit(BaseEventData eventData)
    {
        if (IsActive() && IsInteractable()) PlayClick();
        base.OnSubmit(eventData);
    }

    /// <summary>필수 데이터가 있을 때만 재생한다. 클릭 뒤 창이 닫혀도 짧은 잔향은 유지한다.</summary>
    private void PlayClick()
    {
        if (!HasValidReference() || !HasValidData()) return;
        presentationContext.Audio.Play(clickSound, Vector3.zero);
    }

    /// <summary>조작음 재생에 사용할 필수 Context를 검사한다.</summary>
    public bool HasValidReference()
    {
        if (presentationContext != null && presentationContext.Audio != null) return true;
        Debug.LogError("메뉴 버튼에는 오디오 담당 Context가 필요합니다.", this); return false;
    }

    /// <summary>버튼 효과음이 정지된 메뉴에서도 재생 가능한지 검사한다.</summary>
    public bool HasValidData()
    {
        if (!HasValidReference() || !presentationContext.Audio.HasValidData(clickSound)) return false;
        if (clickSound.PlayDuringPause) return true;
        Debug.LogError("버튼 효과음은 정지 중 재생이 켜져 있어야 합니다.", this); return false;
    }
}
