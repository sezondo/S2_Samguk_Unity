using UnityEngine;

/// <summary>단발 소리의 정지 권한을 나타낸다. 반납된 핸들은 재사용된 소리에 접근할 수 없다.</summary>
public sealed class SoundHandle
{
    // 핸들을 발급한 재생 담당과 이번 대여만 가리키는 소스·데이터다.
    internal readonly AudioManager Manager;
    internal readonly AudioSource Source;
    public SoundData Data { get; }
    // 풀 재사용 뒤 오래된 핸들로 소리를 중단하는 것을 방지하는 상태다.
    internal bool Released;
    internal bool Paused;
    public bool IsValid => !Released && Manager != null && Source != null;

    /// <summary>이번 재생의 소스와 데이터를 보관한다.</summary>
    internal SoundHandle(AudioManager manager, AudioSource source, SoundData data)
    { Manager = manager; Source = source; Data = data; }

    /// <summary>아직 유효한 이번 소리만 정지하고 풀에 돌려준다.</summary>
    public void Stop() { if (IsValid) Manager.Stop(this); }
}
