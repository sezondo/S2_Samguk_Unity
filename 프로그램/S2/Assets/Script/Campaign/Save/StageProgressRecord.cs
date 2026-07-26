using System;

/// <summary>
/// 저장 파일에 기록하는 스테이지 하나의 진행 상태다.
/// </summary>
[Serializable]
public class StageProgressRecord
{
    // 진행 상태가 연결되는 스테이지의 고유 ID다.
    public string stageId;
    // 해당 스테이지의 현재 잠금·클리어 상태다.
    public StageProgressState progressState;

    /// <summary>
    /// 지정한 스테이지 ID와 진행 상태로 저장 레코드를 만든다.
    /// </summary>
    public StageProgressRecord(string stageId, StageProgressState progressState)
    {
        this.stageId = stageId;
        this.progressState = progressState;
    }
}
