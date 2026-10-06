using System;
using UnityEngine;

/// <summary>
/// 선형 Story 시퀀스에서 실행할 명령 하나와 해당 명령의 데이터를 보관한다.
/// </summary>
[Serializable]
public class StoryCommandData
{
    [Header("Command")]
    // 이 데이터가 실행할 Story 명령 종류다.
    [SerializeField] private StoryCommandType commandType;

    [Header("Dialogue")]
    // Dialogue 명령에서 대화창에 표시할 화자 이름이다.
    [SerializeField] private string speakerName;
    // 독백처럼 본문만 표시할 때 화자 이름을 숨긴다. 실제 화자 데이터는 보존한다.
    [SerializeField] private bool hideSpeakerName;
    // Dialogue 명령에서 표시할 대사 본문이다.
    [SerializeField, TextArea(2, 6)] private string dialogueText;
    // Dialogue 명령에서 글자를 한 글자씩 표시할지 나타낸다.
    [SerializeField] private bool useTypewriter = true;
    // Dialogue 명령의 초당 표시 글자 수다.
    [SerializeField] private float charactersPerSecond = 30f;

    [Header("Visual")]
    // 배경, 스탠딩 또는 표정 변경 명령에서 사용할 Sprite다.
    [SerializeField] private Sprite visualSprite;
    // 스탠딩 관련 명령이 대상으로 삼을 고정 화면 위치다.
    [SerializeField] private StoryStandingPosition standingPosition;

    [Header("Comic")]
    // 기존 Inspector 연결을 유지하기 위한 목록이며 ShowComicPanel은 0번의 완성 만화 이미지 한 장만 사용한다.
    [SerializeField] private Sprite[] comicPanelSprites;

    [Header("Comic Cut")]
    // 원고의 컷 표시 지시와 대응하는 컷 식별자다.
    [SerializeField] private string comicCutId;
    // 페이지 좌상단을 (0, 0), 우하단을 (1, 1)로 하는 컷 경계 꼭짓점이다.
    [SerializeField] private Vector2[] comicCutVertices;

    [Header("Timing")]
    // Fade 명령이 화면을 가리거나 드러내는 방향이다.
    [SerializeField] private StoryFadeDirection fadeDirection;
    // Fade·Wait·ShowComicCut 명령에 사용할 비스케일 시간 기준 길이다.
    [SerializeField] private float duration = 0.25f;

    public StoryCommandType CommandType => commandType;
    public string SpeakerName => speakerName;
    public bool HideSpeakerName => hideSpeakerName;
    public string DialogueText => dialogueText;
    public bool UseTypewriter => useTypewriter;
    public float CharactersPerSecond => charactersPerSecond;
    public Sprite VisualSprite => visualSprite;
    public StoryStandingPosition StandingPosition => standingPosition;
    public Sprite ComicPanelSprite =>
        comicPanelSprites != null && comicPanelSprites.Length > 0
            ? comicPanelSprites[0]
            : null;
    public string ComicCutId => comicCutId;
    public Vector2[] ComicCutVertices => comicCutVertices;
    public StoryFadeDirection FadeDirection => fadeDirection;
    public float Duration => duration;
}
