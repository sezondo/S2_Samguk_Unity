using System;
using UnityEngine;

[Serializable]
public class DialogueLineData
{
    [Header("Speaker")]
    // DialogueSpeaker가 가진 speakerTag와 맞춰서 실제 발화자를 찾는다.
    public string speakerTag;

    [Header("Text")]
    [TextArea(2, 5)]
    public string text;

    [Header("Text Style")]
    // 꺼져 있으면 SpeechBubbleView 프리팹의 기본 글자 색을 사용한다.
    public bool overrideTextColor;
    public Color textColor = Color.black;
    // 꺼져 있으면 SpeechBubbleView 프리팹의 기본 폰트 크기를 사용한다.
    public bool overrideFontSize;
    public float fontSize = 16f;

    [Header("Typewriter")]
    // 켜져 있으면 이 대사는 한 글자씩 표시된다.
    public bool useTypewriter = true;
    // 초당 표시할 글자 수다. 0 이하이면 타자기식 출력 없이 즉시 전체 문장을 보여준다.
    public float charactersPerSecond = 30f;
}
