using System;
using UnityEngine;

[Serializable]
public class DialogueStepData
{
    [Header("Lines")]
    // 한 스텝에서 동시에 표시할 대사들이다.
    public DialogueLineData[] lines;

    [Header("Advance")]
    // 켜져 있으면 일반 상호작용 입력으로 다음 스텝으로 넘긴다.
    public bool advanceByInput = true;
    // 켜져 있으면 지정한 시간이 지난 뒤 자동으로 다음 스텝으로 넘긴다.
    public bool advanceByTime;
    public float autoAdvanceTime = 2f;
}
