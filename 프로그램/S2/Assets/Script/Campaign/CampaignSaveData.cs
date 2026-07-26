using System;
using System.Collections.Generic;

/// <summary>
/// JsonUtility로 파일에 직렬화하는 캠페인 저장 데이터다.
/// </summary>
[Serializable]
public class CampaignSaveData
{
    // 저장 구조 변경을 판별하는 버전 번호다.
    public int version = 1;
    // 캠페인에 포함된 스테이지별 진행 상태 목록이다.
    public List<StageProgressRecord> stageProgressRecords = new();
}
