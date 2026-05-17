using UnityEngine;

[CreateAssetMenu(menuName = "Scriptable/DialogueSequenceData", fileName = "DialogueSequenceData")]
public class DialogueSequenceData : ScriptableObject
{
    [Header("Control")]
    // 켜져 있으면 전투 중 대사처럼 이동, 공격, 투척, 회피를 모두 허용한다.
    public bool canPlayerControlDuringDialogue;
    // 꺼져 있으면 이 시퀀스가 재생되는 동안 대사 잠금 이벤트로 플레이어 이동을 막는다.
    public bool canPlayerMoveDuringDialogue;

    [Header("Steps")]
    // 이 시퀀스에서 순서대로 진행할 대사 스텝 목록이다.
    public DialogueStepData[] steps;
}
