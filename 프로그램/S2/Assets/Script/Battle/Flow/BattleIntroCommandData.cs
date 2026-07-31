using System;
using UnityEngine;

/// <summary>
/// 전투 입장 연출의 카메라·미션 안내·대사 명령 하나를 보관한다.
/// </summary>
[Serializable]
public class BattleIntroCommandData
{
    [Header("Command")]
    // 이 항목이 실행할 입장 연출 명령 종류다.
    [SerializeField] private BattleIntroCommandType commandType;
    // 대기, 카메라 이동 또는 미션 문구 표시 시간이다.
    [SerializeField] private float duration = 1f;

    [Header("Camera")]
    // MoveCamera 명령이 카메라를 이동시킬 월드 좌표다.
    [SerializeField] private Vector3 cameraTargetPosition;

    [Header("Mission")]
    // ShowMissionMessage 명령에서 표시할 임시 미션 안내 문구다.
    [SerializeField, TextArea(2, 4)] private string missionMessage;

    [Header("Dialogue")]
    // PlayDialogue 명령에서 자동 재생할 전투 말풍선 시퀀스다.
    [SerializeField] private DialogueSequenceData dialogueSequence;

    public BattleIntroCommandType CommandType => commandType;
    public float Duration => duration;
    public Vector3 CameraTargetPosition => cameraTargetPosition;
    public string MissionMessage => missionMessage;
    public DialogueSequenceData DialogueSequence => dialogueSequence;
}
