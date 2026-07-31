using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// 전투 입장 연출 이벤트를 받아 데이터 배열의 카메라·미션·대사 명령을 자동 실행한다.
/// </summary>
public class BattleIntroPresenter : MonoBehaviour, IPresentationEventHandler
{
    [Header("Reference")]
    // 입장 연출 중 이동시킬 전투 카메라다.
    [SerializeField] private Camera targetCamera;
    // PlayDialogue 명령을 실행할 기존 전투 대사 매니저다.
    [SerializeField] private DialogueManager dialogueManager;
    // 대사 데이터의 speakerTag와 연결할 씬 발화자 목록이다.
    [SerializeField] private DialogueSpeaker[] dialogueSpeakers;
    // 임시 미션 안내 문구를 표시할 루트다.
    [SerializeField] private GameObject missionMessageRoot;
    // 임시 미션 안내 문구를 표시할 텍스트다.
    [SerializeField] private TMP_Text missionMessageText;

    [Header("Data")]
    // 이 전투 씬에서 재생할 입장 연출 데이터다.
    [SerializeField] private BattleIntroSequenceData sequence;

    // 현재 실행 중인 입장 연출 코루틴이다.
    private Coroutine playRoutine;

    /// <summary>
    /// 미션 안내를 감추고 필수 참조와 데이터 구성을 즉시 검사한다.
    /// </summary>
    private void Awake()
    {
        if (missionMessageRoot != null)
        {
            missionMessageRoot.SetActive(false);
        }

        if (!HasValidReference() || !HasValidData())
        {
            enabled = false;
        }
    }

    /// <summary>
    /// 활성화될 때 전투 연출 큐에 핸들러 등록을 시도한다.
    /// </summary>
    private void OnEnable()
    {
        TryRegisterQueue(false);
    }

    /// <summary>
    /// 초기화 순서로 놓친 연출 큐 등록을 다시 시도한다.
    /// </summary>
    private void Start()
    {
        TryRegisterQueue(true);
    }

    /// <summary>
    /// 비활성화될 때 코루틴과 큐 등록을 정리한다.
    /// </summary>
    private void OnDisable()
    {
        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
            playRoutine = null;
        }

        if (ActionPresentationQueue.Instance != null)
        {
            ActionPresentationQueue.Instance.Unregister(this);
        }
    }

    /// <summary>
    /// 전투 입장 연출 이벤트만 처리할 수 있는지 확인한다.
    /// </summary>
    public bool CanHandle(PresentationEvent presentationEvent)
    {
        return presentationEvent.Type == PresentationEventType.BattleIntro;
    }

    /// <summary>
    /// 입장 연출 데이터의 명령을 순서대로 실행하고 끝에서 큐 완료를 알린다.
    /// </summary>
    public void Handle(PresentationEvent _, PresentationEventHandle handle)
    {
        if (playRoutine != null)
        {
            Debug.LogError($"{nameof(BattleIntroPresenter)}: 이미 입장 연출을 실행 중입니다.", this);
            handle.Complete();
            return;
        }

        playRoutine = StartCoroutine(PlayRoutine(handle));
    }

    /// <summary>
    /// 입장 연출 명령 배열을 시간 기준으로 자동 실행한다.
    /// </summary>
    private IEnumerator PlayRoutine(PresentationEventHandle handle)
    {
        BattleIntroCommandData[] commands = sequence.Commands;
        for (int i = 0; i < commands.Length; i++)
        {
            BattleIntroCommandData command = commands[i];
            switch (command.CommandType)
            {
                case BattleIntroCommandType.Wait:
                    yield return new WaitForSeconds(command.Duration);
                    break;

                case BattleIntroCommandType.MoveCamera:
                    yield return MoveCamera(command.CameraTargetPosition, command.Duration);
                    break;

                case BattleIntroCommandType.ShowMissionMessage:
                    missionMessageText.text = command.MissionMessage;
                    missionMessageRoot.SetActive(true);
                    yield return new WaitForSeconds(command.Duration);
                    missionMessageRoot.SetActive(false);
                    break;

                case BattleIntroCommandType.PlayDialogue:
                    dialogueManager.TryPlay(command.DialogueSequence, dialogueSpeakers);
                    yield return new WaitUntil(() => !dialogueManager.IsPlaying);
                    break;
            }
        }

        missionMessageRoot.SetActive(false);
        playRoutine = null;
        handle.Complete();
    }

    /// <summary>
    /// 현재 카메라 위치에서 지정 좌표까지 설정 시간 동안 선형 이동한다.
    /// </summary>
    private IEnumerator MoveCamera(Vector3 targetPosition, float duration)
    {
        Transform cameraTransform = targetCamera.transform;
        Vector3 startPosition = cameraTransform.position;
        if (duration <= 0f)
        {
            cameraTransform.position = targetPosition;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            cameraTransform.position = Vector3.Lerp(startPosition, targetPosition, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        cameraTransform.position = targetPosition;
    }

    /// <summary>
    /// 현재 씬의 연출 큐에 핸들러를 등록한다.
    /// </summary>
    private void TryRegisterQueue(bool logMissingQueue)
    {
        if (ActionPresentationQueue.Instance != null)
        {
            ActionPresentationQueue.Instance.Register(this);
            return;
        }

        if (logMissingQueue)
        {
            Debug.LogError($"{nameof(BattleIntroPresenter)} on {name}에는 씬의 {nameof(ActionPresentationQueue)}가 필요합니다.", this);
            enabled = false;
        }
    }

    /// <summary>
    /// 입장 연출에서 항상 사용하는 카메라와 미션 UI 참조를 검사한다.
    /// </summary>
    public bool HasValidReference()
    {
        if (targetCamera == null || missionMessageRoot == null || missionMessageText == null)
        {
            Debug.LogError($"{nameof(BattleIntroPresenter)} on {name}에는 카메라와 미션 안내 UI 참조가 필요합니다.", this);
            return false;
        }

        return true;
    }

    /// <summary>
    /// 입장 연출 명령별 필수 데이터와 자동 대사 설정을 검사한다.
    /// </summary>
    public bool HasValidData()
    {
        if (sequence == null ||
            string.IsNullOrWhiteSpace(sequence.SequenceId) ||
            string.IsNullOrWhiteSpace(sequence.DisplayName) ||
            sequence.Commands == null ||
            sequence.Commands.Length == 0)
        {
            Debug.LogError($"{nameof(BattleIntroPresenter)} on {name}의 입장 연출 데이터 구성을 확인하세요.", sequence);
            return false;
        }

        for (int i = 0; i < sequence.Commands.Length; i++)
        {
            BattleIntroCommandData command = sequence.Commands[i];
            if (command == null || command.Duration < 0f)
            {
                Debug.LogError($"{nameof(BattleIntroPresenter)}: '{sequence.DisplayName}'의 {i}번 명령 또는 시간이 올바르지 않습니다.", sequence);
                return false;
            }

            if (command.CommandType == BattleIntroCommandType.ShowMissionMessage &&
                string.IsNullOrWhiteSpace(command.MissionMessage))
            {
                Debug.LogError($"{nameof(BattleIntroPresenter)}: '{sequence.DisplayName}'의 {i}번 미션 문구가 비어 있습니다.", sequence);
                return false;
            }

            if (command.CommandType == BattleIntroCommandType.PlayDialogue &&
                !HasValidDialogueCommand(command, i))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 입장 대사가 입력 없이 자동 진행하도록 구성됐는지 검사한다.
    /// </summary>
    private bool HasValidDialogueCommand(BattleIntroCommandData command, int commandIndex)
    {
        if (dialogueManager == null || dialogueSpeakers == null || dialogueSpeakers.Length == 0 ||
            command.DialogueSequence == null || command.DialogueSequence.steps == null ||
            command.DialogueSequence.steps.Length == 0)
        {
            Debug.LogError($"{nameof(BattleIntroPresenter)}: '{sequence.DisplayName}'의 {commandIndex}번 대사 명령 참조가 비어 있습니다.", sequence);
            return false;
        }

        for (int i = 0; i < command.DialogueSequence.steps.Length; i++)
        {
            DialogueStepData step = command.DialogueSequence.steps[i];
            if (step == null || !step.advanceByTime || step.advanceByInput || step.autoAdvanceTime < 0f)
            {
                Debug.LogError($"{nameof(BattleIntroPresenter)}: 입장 대사 '{command.DialogueSequence.name}'의 {i}번 스텝은 입력 없이 유효한 시간으로 자동 진행해야 합니다.", command.DialogueSequence);
                return false;
            }
        }

        return true;
    }
}
