using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>메뉴와 수첩에서 사용할 화면 참조만 모아둔다.</summary>
public sealed class BattleMenuContext : MonoBehaviour
{
    // 전체 화면 가림막과 세 종류의 창이다.
    public GameObject overlay, menu, manual, quit;
    // 화면 크기에 맞춰 조립된 책을 축소할 기준과 책 루트다.
    public RectTransform canvasRect, book;
    // 각 페이지의 종이·설명·도식 묶음과 제본 기준 회전 영역이다.
    public GameObject[] pages;
    public RectTransform[] leftPages, rightPages;
    // 색인 선택 표시와 페이지·현재 튜토리얼 설명이다.
    public RectTransform activeTab;
    public TMP_Text pageNumber, guideText;
    // HUD, 메뉴, 종료 확인 및 수첩의 입력 버튼이다.
    public Button menuOpen, manualOpen, menuClose, settings, menuManual, menuQuit;
    public Button quitClose, quitCancel, quitAccept, manualClose, previous, next;
    public Button[] tabs;
    // 창의 동작과 책 표시 기능이다.
    public BattleMenuController controller;
    public BattleManualView manualView;
}
