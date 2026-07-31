using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// 캠페인 진행 상태를 로컬 JSON 파일로 불러오고 저장한다.
/// </summary>
public class CampaignSaveManager : MonoBehaviour
{
    private const int CurrentSaveVersion = 1;

    [Header("Reference")]
    // 저장할 스테이지 목록을 제공하는 영속 캠페인 Context다.
    [SerializeField] private CampaignContext context;

    [Header("Save")]
    // Application.persistentDataPath 아래에 생성할 캠페인 저장 파일 이름이다.
    [SerializeField] private string saveFileName = "campaign-save.json";

    // 현재 메모리에 올라온 캠페인 저장 데이터다.
    private CampaignSaveData currentSaveData;
    // 저장 시스템 초기화가 정상적으로 끝났는지 나타낸다.
    private bool isInitialized;

    public bool IsInitialized => isInitialized;
    public string SaveFilePath => Path.Combine(Application.persistentDataPath, saveFileName);

    /// <summary>
    /// 참조와 설정을 검사하고 기존 저장 파일을 불러오거나 최초 저장 데이터를 만든다.
    /// </summary>
    public bool Initialize()
    {
        if (isInitialized)
        {
            return true;
        }

        if (!HasValidReference() || !HasValidData())
        {
            enabled = false;
            return false;
        }

        if (File.Exists(SaveFilePath))
        {
            if (!TryLoadFromDisk())
            {
                enabled = false;
                return false;
            }
        }
        else
        {
            currentSaveData = CreateNewSaveData();
            if (!Save())
            {
                enabled = false;
                return false;
            }
        }

        isInitialized = true;
        return true;
    }

    /// <summary>
    /// 지정한 스테이지의 저장된 진행 상태를 가져온다.
    /// </summary>
    public bool TryGetStageProgress(string stageId, out StageProgressState progressState)
    {
        if (!isInitialized || currentSaveData == null)
        {
            Debug.LogError($"{nameof(CampaignSaveManager)}가 초기화되지 않아 스테이지 진행 상태를 읽을 수 없습니다.", this);
            progressState = StageProgressState.Locked;
            return false;
        }

        StageProgressRecord record = FindStageRecord(stageId);
        if (record == null)
        {
            Debug.LogError($"{nameof(CampaignSaveManager)}: 저장 데이터에서 스테이지 ID '{stageId}'를 찾을 수 없습니다.", this);
            progressState = StageProgressState.Locked;
            return false;
        }

        progressState = record.progressState;
        return true;
    }

    /// <summary>
    /// 전투 승리 직후 해당 스테이지를 전투 클리어 상태로 바꾸고 저장한다.
    /// </summary>
    public bool TryMarkBattleCleared(string stageId)
    {
        if (!isInitialized || currentSaveData == null)
        {
            Debug.LogError($"{nameof(CampaignSaveManager)}가 초기화되지 않아 스테이지 진행 상태를 저장할 수 없습니다.", this);
            return false;
        }

        if (!context.CampaignData.TryGetStage(stageId, out StageDefinitionData stage))
        {
            Debug.LogError($"{nameof(CampaignSaveManager)}: 캠페인 데이터에 없는 스테이지 ID '{stageId}'의 상태를 바꿀 수 없습니다.", this);
            return false;
        }

        StageProgressRecord record = FindStageRecord(stageId);
        if (record == null)
        {
            Debug.LogError($"{nameof(CampaignSaveManager)}: 저장 데이터에서 스테이지 ID '{stageId}'를 찾을 수 없습니다.", this);
            return false;
        }

        if (record.progressState == StageProgressState.Completed)
        {
            return true;
        }

        if (record.progressState != StageProgressState.Available)
        {
            Debug.LogError($"{nameof(CampaignSaveManager)}: 스테이지 '{stage.DisplayName}'은 현재 {record.progressState} 상태라 전투 클리어 처리할 수 없습니다.", this);
            return false;
        }

        StageProgressState previousState = record.progressState;
        record.progressState = StageProgressState.BattleCleared;
        if (Save())
        {
            return true;
        }

        record.progressState = previousState;
        return false;
    }

    /// <summary>
    /// 스테이지를 최종 완료하고 선형 순서의 다음 스테이지를 개방한 뒤 함께 저장한다.
    /// </summary>
    public bool TryCompleteStage(string stageId)
    {
        if (!isInitialized || currentSaveData == null)
        {
            Debug.LogError($"{nameof(CampaignSaveManager)}가 초기화되지 않아 스테이지 완료 상태를 저장할 수 없습니다.", this);
            return false;
        }

        if (!context.CampaignData.TryGetStage(stageId, out StageDefinitionData stage))
        {
            Debug.LogError($"{nameof(CampaignSaveManager)}: 캠페인 데이터에 없는 스테이지 ID '{stageId}'를 완료할 수 없습니다.", this);
            return false;
        }

        StageProgressRecord currentRecord = FindStageRecord(stageId);
        if (currentRecord == null)
        {
            Debug.LogError($"{nameof(CampaignSaveManager)}: 저장 데이터에서 스테이지 ID '{stageId}'를 찾을 수 없습니다.", this);
            return false;
        }

        if (currentRecord.progressState == StageProgressState.Completed)
        {
            return true;
        }

        bool canComplete = currentRecord.progressState == StageProgressState.BattleCleared ||
            (!stage.HasPostBattleStory && currentRecord.progressState == StageProgressState.Available);
        if (!canComplete)
        {
            Debug.LogError($"{nameof(CampaignSaveManager)}: 스테이지 '{stage.DisplayName}'은 현재 {currentRecord.progressState} 상태라 최종 완료 처리할 수 없습니다.", this);
            return false;
        }

        StageDefinitionData[] stages = context.CampaignData.Stages;
        int currentStageIndex = Array.IndexOf(stages, stage);
        if (currentStageIndex < 0)
        {
            Debug.LogError($"{nameof(CampaignSaveManager)}: 스테이지 '{stage.DisplayName}'의 선형 캠페인 순서를 찾을 수 없습니다.", this);
            return false;
        }

        StageProgressRecord nextRecord = currentStageIndex + 1 < stages.Length
            ? FindStageRecord(stages[currentStageIndex + 1].StageId)
            : null;
        if (currentStageIndex + 1 < stages.Length && nextRecord == null)
        {
            Debug.LogError($"{nameof(CampaignSaveManager)}: 다음 스테이지 '{stages[currentStageIndex + 1].DisplayName}'의 저장 레코드를 찾을 수 없습니다.", this);
            return false;
        }

        StageProgressState previousCurrentState = currentRecord.progressState;
        StageProgressState previousNextState = nextRecord != null
            ? nextRecord.progressState
            : StageProgressState.Locked;

        currentRecord.progressState = StageProgressState.Completed;
        if (nextRecord != null && nextRecord.progressState == StageProgressState.Locked)
        {
            nextRecord.progressState = StageProgressState.Available;
        }

        if (Save())
        {
            return true;
        }

        currentRecord.progressState = previousCurrentState;
        if (nextRecord != null)
        {
            nextRecord.progressState = previousNextState;
        }

        return false;
    }

    /// <summary>
    /// 현재 메모리의 캠페인 저장 데이터를 JSON 파일로 기록한다.
    /// </summary>
    public bool Save()
    {
        if (currentSaveData == null)
        {
            Debug.LogError($"{nameof(CampaignSaveManager)}: 기록할 캠페인 저장 데이터가 없습니다.", this);
            return false;
        }

        try
        {
            string json = JsonUtility.ToJson(currentSaveData, true);
            File.WriteAllText(SaveFilePath, json);
            return true;
        }
        catch (Exception exception) when (
            exception is IOException ||
            exception is UnauthorizedAccessException ||
            exception is NotSupportedException)
        {
            Debug.LogError($"{nameof(CampaignSaveManager)}: 캠페인 저장 파일을 기록하지 못했습니다.\n경로: {SaveFilePath}\n{exception}", this);
            return false;
        }
    }

    /// <summary>
    /// 저장 파일을 읽고 현재 캠페인 데이터와 정확히 대응하는지 확인한다.
    /// </summary>
    private bool TryLoadFromDisk()
    {
        try
        {
            string json = File.ReadAllText(SaveFilePath);
            CampaignSaveData loadedData = JsonUtility.FromJson<CampaignSaveData>(json);
            if (!HasValidSaveData(loadedData))
            {
                return false;
            }

            currentSaveData = loadedData;
            if (!AppendNewStageRecords())
            {
                return false;
            }

            return true;
        }
        catch (Exception exception) when (
            exception is IOException ||
            exception is UnauthorizedAccessException ||
            exception is NotSupportedException ||
            exception is ArgumentException)
        {
            Debug.LogError($"{nameof(CampaignSaveManager)}: 캠페인 저장 파일을 불러오지 못했습니다.\n경로: {SaveFilePath}\n{exception}", this);
            return false;
        }
    }

    /// <summary>
    /// 캠페인 스테이지 순서를 기준으로 첫 스테이지만 개방된 최초 저장 데이터를 만든다.
    /// </summary>
    private CampaignSaveData CreateNewSaveData()
    {
        CampaignSaveData saveData = new()
        {
            version = CurrentSaveVersion,
        };

        StageDefinitionData[] stages = context.CampaignData.Stages;
        for (int i = 0; i < stages.Length; i++)
        {
            StageProgressState initialState = i == 0
                ? StageProgressState.Available
                : StageProgressState.Locked;
            saveData.stageProgressRecords.Add(new StageProgressRecord(stages[i].StageId, initialState));
        }

        return saveData;
    }

    /// <summary>
    /// 저장 데이터의 버전과 스테이지 레코드가 현재 캠페인 데이터와 일치하는지 검사한다.
    /// </summary>
    private bool HasValidSaveData(CampaignSaveData saveData)
    {
        if (saveData == null)
        {
            Debug.LogError($"{nameof(CampaignSaveManager)}: 캠페인 저장 파일의 JSON 구조를 읽을 수 없습니다.", this);
            return false;
        }

        if (saveData.version != CurrentSaveVersion)
        {
            Debug.LogError($"{nameof(CampaignSaveManager)}: 지원하지 않는 저장 버전입니다. 현재: {saveData.version}, 필요: {CurrentSaveVersion}", this);
            return false;
        }

        StageDefinitionData[] stages = context.CampaignData.Stages;
        if (saveData.stageProgressRecords == null ||
            saveData.stageProgressRecords.Count == 0 ||
            saveData.stageProgressRecords.Count > stages.Length)
        {
            Debug.LogError($"{nameof(CampaignSaveManager)}: 저장된 스테이지 수가 현재 캠페인 구성과 맞지 않습니다.", this);
            return false;
        }

        HashSet<string> savedStageIds = new();
        for (int i = 0; i < saveData.stageProgressRecords.Count; i++)
        {
            StageProgressRecord record = saveData.stageProgressRecords[i];
            if (record == null || string.IsNullOrWhiteSpace(record.stageId))
            {
                Debug.LogError($"{nameof(CampaignSaveManager)}: 저장 데이터의 {i}번 스테이지 레코드가 비어 있습니다.", this);
                return false;
            }

            if (!savedStageIds.Add(record.stageId))
            {
                Debug.LogError($"{nameof(CampaignSaveManager)}: 저장 데이터에 스테이지 ID '{record.stageId}'가 중복되어 있습니다.", this);
                return false;
            }

            if (!context.CampaignData.TryGetStage(record.stageId, out _))
            {
                Debug.LogError($"{nameof(CampaignSaveManager)}: 저장 데이터에 현재 캠페인에 없는 스테이지 ID '{record.stageId}'가 있습니다.", this);
                return false;
            }

            if (record.stageId != stages[i].StageId)
            {
                Debug.LogError($"{nameof(CampaignSaveManager)}: 저장된 스테이지 순서가 현재 선형 캠페인 순서와 다릅니다.", this);
                return false;
            }

            if (!Enum.IsDefined(typeof(StageProgressState), record.progressState))
            {
                Debug.LogError($"{nameof(CampaignSaveManager)}: 스테이지 ID '{record.stageId}'에 알 수 없는 진행 상태가 저장되어 있습니다.", this);
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 기존 저장 파일 뒤에 새로 추가된 선형 스테이지 레코드를 이어 붙이고 변경 내용을 저장한다.
    /// </summary>
    private bool AppendNewStageRecords()
    {
        StageDefinitionData[] stages = context.CampaignData.Stages;
        int savedStageCount = currentSaveData.stageProgressRecords.Count;
        if (savedStageCount >= stages.Length)
        {
            return true;
        }

        for (int i = savedStageCount; i < stages.Length; i++)
        {
            StageProgressState initialState = currentSaveData.stageProgressRecords[i - 1].progressState == StageProgressState.Completed
                ? StageProgressState.Available
                : StageProgressState.Locked;
            currentSaveData.stageProgressRecords.Add(new StageProgressRecord(stages[i].StageId, initialState));
        }

        return Save();
    }

    /// <summary>
    /// 현재 저장 데이터에서 지정한 스테이지 ID의 레코드를 찾는다.
    /// </summary>
    private StageProgressRecord FindStageRecord(string stageId)
    {
        if (currentSaveData?.stageProgressRecords == null)
        {
            return null;
        }

        for (int i = 0; i < currentSaveData.stageProgressRecords.Count; i++)
        {
            StageProgressRecord record = currentSaveData.stageProgressRecords[i];
            if (record != null && record.stageId == stageId)
            {
                return record;
            }
        }

        return null;
    }

    /// <summary>
    /// 저장 매니저에 필요한 캠페인 Context 참조가 연결되어 있는지 확인한다.
    /// </summary>
    public bool HasValidReference()
    {
        if (context == null)
        {
            Debug.LogError($"{nameof(CampaignSaveManager)} on {name}에는 {nameof(CampaignContext)} 참조가 필요합니다.", this);
            return false;
        }

        return context.HasValidReference();
    }

    /// <summary>
    /// 저장 파일 이름과 현재 캠페인 스테이지 정의가 사용할 수 있는 값인지 확인한다.
    /// </summary>
    public bool HasValidData()
    {
        if (string.IsNullOrWhiteSpace(saveFileName) ||
            Path.GetFileName(saveFileName) != saveFileName ||
            saveFileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            Debug.LogError($"{nameof(CampaignSaveManager)} on {name}의 저장 파일 이름은 경로를 제외한 유효한 파일 이름이어야 합니다.", this);
            return false;
        }

        CampaignData campaignData = context.CampaignData;
        if (string.IsNullOrWhiteSpace(campaignData.LobbySceneName))
        {
            Debug.LogError($"{nameof(CampaignSaveManager)}: {nameof(CampaignData)}의 로비 씬 이름이 비어 있습니다.", campaignData);
            return false;
        }

        if (string.IsNullOrWhiteSpace(campaignData.StorySceneName))
        {
            Debug.LogError($"{nameof(CampaignSaveManager)}: {nameof(CampaignData)}의 Story 씬 이름이 비어 있습니다.", campaignData);
            return false;
        }

        StageDefinitionData[] stages = campaignData.Stages;
        if (stages == null || stages.Length == 0)
        {
            Debug.LogError($"{nameof(CampaignSaveManager)}: {nameof(CampaignData)}에는 스테이지가 하나 이상 필요합니다.", campaignData);
            return false;
        }

        HashSet<string> stageIds = new();
        for (int i = 0; i < stages.Length; i++)
        {
            StageDefinitionData stage = stages[i];
            if (stage == null)
            {
                Debug.LogError($"{nameof(CampaignSaveManager)}: {nameof(CampaignData)}의 {i}번 스테이지 참조가 비어 있습니다.", campaignData);
                return false;
            }

            if (string.IsNullOrWhiteSpace(stage.StageId) ||
                stage.ChapterNumber <= 0 ||
                stage.StageNumber <= 0 ||
                string.IsNullOrWhiteSpace(stage.DisplayName) ||
                string.IsNullOrWhiteSpace(stage.BattleSceneName))
            {
                Debug.LogError($"{nameof(CampaignSaveManager)}: 스테이지 데이터 {stage.name}의 ID, 번호, 표시 이름과 전투 씬 이름을 확인하세요.", stage);
                return false;
            }

            if (stage.PreBattleStory == null)
            {
                Debug.LogError($"{nameof(CampaignSaveManager)}: 스테이지 데이터 {stage.name}에 전투 전 Story가 없습니다.", stage);
                return false;
            }

            if (stage.HasPostBattleStory != (stage.PostBattleStory != null))
            {
                string reason = stage.HasPostBattleStory
                    ? "후일담 사용이 켜져 있지만 후일담 데이터가 없습니다"
                    : "후일담 사용이 꺼져 있지만 후일담 데이터가 연결되어 있습니다";
                Debug.LogError($"{nameof(CampaignSaveManager)}: 스테이지 데이터 {stage.name}의 후일담 설정이 올바르지 않습니다. {reason}.", stage);
                return false;
            }

            if (!stageIds.Add(stage.StageId))
            {
                Debug.LogError($"{nameof(CampaignSaveManager)}: 스테이지 ID '{stage.StageId}'가 중복되어 있습니다.", campaignData);
                return false;
            }
        }

        return true;
    }
}
