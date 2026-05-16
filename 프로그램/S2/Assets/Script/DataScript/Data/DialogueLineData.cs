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

    [Header("Advance")]
    // 켜져 있으면 일반 상호작용 입력으로 다음 줄로 넘긴다.
    public bool advanceByInput = true;
    // 켜져 있으면 지정한 시간이 지난 뒤 자동으로 다음 줄로 넘긴다.
    public bool advanceByTime;
    public float autoAdvanceTime = 2f;
}
