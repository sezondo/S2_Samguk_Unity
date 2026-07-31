using UnityEngine;

/// <summary>
/// 전투 조작을 열기 전에 자동 실행할 입장 연출 명령 목록이다.
/// </summary>
[CreateAssetMenu(menuName = "Scriptable/Battle/Battle Intro Sequence", fileName = "BattleIntroSequence")]
public class BattleIntroSequenceData : ScriptableObject
{
    [Header("Identity")]
    // 디버그와 데이터 식별에 사용하는 입장 연출 ID다.
    [SerializeField] private string sequenceId;
    // 인스펙터와 로그에서 확인할 표시 이름이다.
    [SerializeField] private string displayName;

    [Header("Commands")]
    // 시간 기반으로 순서대로 자동 실행할 입장 연출 명령 목록이다.
    [SerializeField] private BattleIntroCommandData[] commands;

    public string SequenceId => sequenceId;
    public string DisplayName => displayName;
    public BattleIntroCommandData[] Commands => commands;
}
