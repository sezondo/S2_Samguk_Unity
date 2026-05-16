using UnityEngine;

[CreateAssetMenu(menuName = "Scriptable/DialogueSequenceData", fileName = "DialogueSequenceData")]
public class DialogueSequenceData : ScriptableObject
{
    [Header("Movement")]
    // 꺼져 있으면 이 시퀀스가 재생되는 동안 대사 잠금 이벤트로 플레이어 이동을 막는다.
    public bool canPlayerMoveDuringDialogue;

    [Header("Lines")]
    // 이 시퀀스에서 순서대로 표시할 대사 목록이다.
    public DialogueLineData[] lines;
}
