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
}
