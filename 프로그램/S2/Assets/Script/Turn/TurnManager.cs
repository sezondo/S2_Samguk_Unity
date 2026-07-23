using System;
using UnityEngine;
using UnityEngine.InputSystem;

public enum TurnSide
{
    Player,
    Enemy,
}

/// <summary>
/// S2-T의 현재 턴 주체를 관리한다.
/// 행동 규칙은 각 액션 컴포넌트가 처리하고, 이 클래스는 턴 시작/종료 흐름만 알린다.
/// </summary>
public class TurnManager : MonoBehaviour
{
    [Header("Reference")]
    // 턴을 시작하고 전환할 수 있는 스테이지 진행 상태를 제공한다.
    [SerializeField] private StageStateManager stageStateManager;

    [Header("Turn")]
    // 게임 시작 시 첫 턴을 잡을 진영이다.
    [SerializeField] private TurnSide startingSide = TurnSide.Player;
    // true면 Start 시점에 startingSide 턴 시작 이벤트를 발생시킨다.
    [SerializeField] private bool beginTurnOnStart = true;
    // 턴이 시작될 때 Unity 콘솔에 로그를 남길지 정한다.
    [SerializeField] private bool logTurnChanges = true;

    [Header("Debug")]
    // 정식 턴 종료 UI가 붙기 전까지 키보드로 턴 종료를 검증할지 정한다.
    [SerializeField] private bool allowKeyboardDebugEndTurn = true;
    // 디버그 턴 종료에 사용할 키다.
    [SerializeField] private Key debugEndTurnKey = Key.Space;

    // 씬에서 사용하는 단일 턴 매니저 인스턴스다.
    public static TurnManager Instance { get; private set; }

    // 현재 턴을 진행 중인 진영이다.
    public TurnSide CurrentSide { get; private set; }
    // 현재 턴이 플레이어 턴인지 빠르게 확인하는 값이다.
    public bool IsPlayerTurn => CurrentSide == TurnSide.Player;
    // 현재 턴이 적 턴인지 빠르게 확인하는 값이다.
    public bool IsEnemyTurn => CurrentSide == TurnSide.Enemy;

    // 새 턴이 시작될 때 현재 진영을 전달한다.
    public event Action<TurnSide> TurnStarted;
    // 현재 턴이 끝날 때 종료된 진영을 전달한다.
    public event Action<TurnSide> TurnEnded;

    /// <summary>
    /// 씬의 단일 TurnManager 인스턴스를 등록하고 시작 진영을 설정한다.
    /// </summary>
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"{nameof(TurnManager)}: 이미 인스턴스가 있습니다. 중복 오브젝트 {name}를 제거합니다.", this);
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (!HasValidReference())
        {
            enabled = false;
            return;
        }

        CurrentSide = startingSide;
    }

    /// <summary>
    /// 설정에 따라 게임 시작 시 첫 턴 시작 이벤트를 발생시킨다.
    /// </summary>
    private void Start()
    {
        if (beginTurnOnStart && stageStateManager.IsPlaying)
        {
            StartTurn(CurrentSide);
        }
    }

    /// <summary>
    /// 현재 인스턴스가 제거될 때 싱글톤 참조를 정리한다.
    /// </summary>
    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// 정식 UI가 붙기 전까지 디버그 턴 종료 키 입력을 확인한다.
    /// </summary>
    private void Update()
    {
        if (!stageStateManager.IsPlaying ||
            !allowKeyboardDebugEndTurn ||
            debugEndTurnKey == Key.None ||
            Keyboard.current == null)
        {
            return;
        }

        // 정식 턴 종료 UI를 붙이기 전까지 키보드로 턴 전환 로그를 확인한다.
        if (Keyboard.current[debugEndTurnKey].wasPressedThisFrame)
        {
            EndCurrentTurn();
        }
    }

    /// <summary>
    /// 현재 턴을 종료하고 다음 진영 턴을 시작한다.
    /// </summary>
    public void EndCurrentTurn()
    {
        if (!CanProgressTurn("현재 턴을 종료"))
        {
            return;
        }

        TurnSide endedSide = CurrentSide;
        TurnEnded?.Invoke(endedSide);

        TurnSide nextSide = endedSide == TurnSide.Player ? TurnSide.Enemy : TurnSide.Player;
        StartTurn(nextSide);
    }

    /// <summary>
    /// 플레이어 턴을 강제로 시작한다.
    /// </summary>
    public void StartPlayerTurn()
    {
        StartTurn(TurnSide.Player);
    }

    /// <summary>
    /// 적 턴을 강제로 시작한다.
    /// </summary>
    public void StartEnemyTurn()
    {
        StartTurn(TurnSide.Enemy);
    }

    /// <summary>
    /// 지정한 진영을 현재 턴으로 설정하고 턴 시작 이벤트를 알린다.
    /// </summary>
    private void StartTurn(TurnSide side)
    {
        if (!CanProgressTurn($"{side} 턴을 시작"))
        {
            return;
        }

        CurrentSide = side;

        if (logTurnChanges)
        {
            Debug.Log($"{nameof(TurnManager)}: {CurrentSide} 턴을 시작했습니다.", this);
        }

        TurnStarted?.Invoke(CurrentSide);
    }

    /// <summary>
    /// 현재 스테이지 상태에서 턴 시작 또는 전환을 진행할 수 있는지 확인한다.
    /// </summary>
    private bool CanProgressTurn(string requestDescription)
    {
        if (!HasValidReference())
        {
            return false;
        }

        if (stageStateManager.IsPlaying)
        {
            return true;
        }

        if (logTurnChanges)
        {
            Debug.Log($"{nameof(TurnManager)}: 스테이지가 {stageStateManager.CurrentState} 상태라 {requestDescription}할 수 없습니다.", this);
        }

        return false;
    }

    /// <summary>
    /// 턴 진행 상태를 제공할 필수 StageStateManager 참조가 연결되어 있는지 확인한다.
    /// </summary>
    public bool HasValidReference()
    {
        if (stageStateManager == null)
        {
            Debug.LogError($"{nameof(TurnManager)} on {name}에는 진행 상태를 제공할 {nameof(StageStateManager)} 참조가 필요합니다.", this);
            return false;
        }

        return true;
    }
}
