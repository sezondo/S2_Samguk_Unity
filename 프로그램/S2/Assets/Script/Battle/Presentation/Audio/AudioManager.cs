using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>효과음 데이터로 독립 단발 소리를 재생하며 소스 풀·동시 재생·정지를 관리한다.</summary>
public sealed class AudioManager : MonoBehaviour
{
    // 재생 소스를 생성할 명시적인 프리팹이다. 런타임에 다른 오브젝트에서 찾지 않는다.
    [SerializeField] private AudioSource sourcePrefab;
    // 미리 준비할 소스 수와 동시 재생 한도다. 한도에서는 기존 소리를 빼앗지 않고 새 요청을 거절한다.
    [SerializeField] private int prewarmCount = 4;
    [SerializeField] private int maximumVoices = 16;
    // 개별 데이터 음량에 곱하는 이 씬의 효과음 전체 음량이다.
    [SerializeField, Range(0,1)] private float masterVolume = .8f;
    // 유휴 소스와 이번 재생의 핸들을 구분하여 이중 반납을 방지한다.
    private readonly Queue<AudioSource> pool = new();
    private readonly HashSet<SoundHandle> active = new();
    private readonly List<SoundHandle> snapshot = new();
    private int createdCount;
    // 음원·음높이 선택이 전투 명중 난수의 진행 순서를 바꾸지 않게 별도 난수를 쓴다.
    private readonly System.Random random = new();
    public int ActiveVoiceCount => active.Count;
    public int CreatedVoiceCount => createdCount;
    public float MasterVolume => masterVolume;
    // 자동 검사와 연출 관찰용 신호다. 논리와 큐 완료에는 사용하지 않는다.
    public event Action<SoundHandle> SoundStarted;

    /// <summary>소스 프리팹과 풀 수치를 확인한 뒤 지정 개수만 생성한다.</summary>
    private void Awake()
    {
        if (!HasValidReference() || prewarmCount < 0 || maximumVoices < 1 || prewarmCount > maximumVoices || !ValidUnit(masterVolume))
        { Debug.LogError("오디오 소스 참조·미리 생성 개수·동시 재생 한도·음량이 올바르지 않습니다.", this); enabled=false; return; }
        for(int i=0;i<prewarmCount;i++) pool.Enqueue(CreateSource());
    }

    /// <summary>메뉴의 시간 정지에 맞춰 소리를 멈추고 실제 재생 종료 뒤에만 소스를 회수한다.</summary>
    private void Update()
    {
        snapshot.Clear(); snapshot.AddRange(active);
        foreach(var handle in snapshot)
        {
            if (!handle.IsValid) { Stop(handle); continue; }
            bool pause = Time.timeScale == 0f;
            if(pause && !handle.Paused) { handle.Source.Pause(); handle.Paused=true; }
            else if(!pause && handle.Paused) { handle.Source.UnPause(); handle.Paused=false; }
            if(!handle.Paused && !handle.Source.isPlaying) Stop(handle);
        }
    }

    /// <summary>씬 이탈·재생 담당 비활성화 때 모든 소리와 핸들을 정리한다.</summary>
    private void OnDisable() => StopAll();

    /// <summary>필수 소스 프리팹이 루프·자동 재생 없이 연결됐는지 검사한다.</summary>
    public bool HasValidReference()
    {
        bool valid=sourcePrefab!=null && !sourcePrefab.playOnAwake && !sourcePrefab.loop;
        if(!valid) Debug.LogError("오디오 담당에는 자동 재생·반복이 꺼진 AudioSource 프리팹이 필요합니다.",this);
        return valid;
    }

    /// <summary>효과음 후보·음량·음높이·공간 재생 수치의 필수 값을 검사한다.</summary>
    public bool HasValidData(SoundData data)
    {
        bool valid=data!=null && data.Clips!=null && data.Clips.Length>0 && ValidUnit(data.Volume) && ValidUnit(data.SpatialBlend) &&
            Finite(data.PitchRange.x) && Finite(data.PitchRange.y) && data.PitchRange.x>0 && data.PitchRange.y<=3 && data.PitchRange.y>=data.PitchRange.x &&
            Finite(data.DistanceRange.x) && Finite(data.DistanceRange.y) && data.DistanceRange.x>0 && data.DistanceRange.y>data.DistanceRange.x;
        if(valid) foreach(var clip in data.Clips) valid &= clip!=null && clip.samples>0;
        if(!valid) Debug.LogError("효과음 데이터의 음원·음량·음높이·거리 값이 누락됐거나 유효하지 않습니다.",this);
        return valid;
    }

    /// <summary>허용 범위 안에서 후보 하나를 재생한다. 소리 길이는 연출 큐를 대기시키지 않는다.</summary>
    public SoundHandle Play(SoundData data, Vector3 position)
    {
        if(!isActiveAndEnabled || !HasValidData(data)) return null;
        if(active.Count>=maximumVoices) { Debug.LogWarning("효과음 동시 재생 한도로 새 소리 요청을 생략했습니다.",this); return null; }
        AudioSource source=pool.Count>0?pool.Dequeue():CreateSource();
        source.transform.position=position;
        source.clip=data.Clips[random.Next(data.Clips.Length)];
        source.volume=data.Volume*masterVolume;
        source.pitch=Mathf.Lerp(data.PitchRange.x,data.PitchRange.y,(float)random.NextDouble());
        source.spatialBlend=data.SpatialBlend;
        source.minDistance=data.DistanceRange.x; source.maxDistance=data.DistanceRange.y;
        source.rolloffMode=AudioRolloffMode.Linear; source.dopplerLevel=0;
        source.outputAudioMixerGroup=data.MixerGroup;
        source.gameObject.SetActive(true);
        var handle=new SoundHandle(this,source,data); active.Add(handle);
        source.Play();
        if(Time.timeScale==0f) { source.Pause(); handle.Paused=true; }
        SoundStarted?.Invoke(handle);
        return handle;
    }

    /// <summary>현재 담당이 발급한 유효한 핸들만 한 번 정지하고 반납한다.</summary>
    public void Stop(SoundHandle handle)
    {
        if(handle==null || handle.Manager!=this || !active.Remove(handle)) return;
        handle.Released=true; handle.Paused=false;
        if(handle.Source==null) return;
        handle.Source.Stop(); handle.Source.clip=null; handle.Source.pitch=1;
        handle.Source.outputAudioMixerGroup=null; handle.Source.gameObject.SetActive(false);
        pool.Enqueue(handle.Source);
    }

    /// <summary>모든 단발 소리를 정지하고 이전 핸들의 제어권을 해제한다.</summary>
    public void StopAll()
    { snapshot.Clear(); snapshot.AddRange(active); foreach(var handle in snapshot) Stop(handle); snapshot.Clear(); }

    /// <summary>전체 효과음 음량과 현재 재생 중인 소리의 음량을 함께 갱신한다.</summary>
    public bool SetMasterVolume(float value)
    {
        if(!ValidUnit(value)) { Debug.LogError("전체 효과음 음량은 0~1의 유한한 값이어야 합니다.",this); return false; }
        masterVolume=value;
        foreach(var handle in active) if(handle.IsValid) handle.Source.volume=handle.Data.Volume*value;
        return true;
    }

    /// <summary>지정 소스 프리팹으로 재사용할 단일 음성 채널을 만든다.</summary>
    private AudioSource CreateSource()
    { var source=Instantiate(sourcePrefab,transform); source.name="효과음 소스 "+(++createdCount); source.gameObject.SetActive(false); return source; }

    /// <summary>수치가 유한한지 검사한다.</summary>
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    /// <summary>음량·공간 혼합값의 0~1 범위를 검사한다.</summary>
    private static bool ValidUnit(float value) => Finite(value) && value>=0 && value<=1;
}
