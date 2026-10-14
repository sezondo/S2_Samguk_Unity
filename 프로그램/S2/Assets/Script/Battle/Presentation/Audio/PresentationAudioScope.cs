using System.Collections.Generic;
using UnityEngine;

/// <summary>하나의 화면 담당이 시작한 소리만 보관하고 중단 때 정리한다.</summary>
public sealed class PresentationAudioScope
{
    // 정상 완료 뒤 잔향을 유지하되 이미 끝난 대여는 다음 요청에서 제거한다.
    private readonly List<SoundHandle> sounds = new();

    /// <summary>명시적인 담당에 데이터를 전달하고 이번 소리의 정지 권한을 보관한다.</summary>
    public void Play(BattlePresentationContext context, SoundData data, Vector3 position)
    {
        sounds.RemoveAll(sound => !sound.IsValid);
        var sound = context.Audio.Play(data, position);
        if (sound != null) sounds.Add(sound);
    }

    /// <summary>이 담당이 시작한 소리를 모두 정지하고 오래된 권한을 버린다.</summary>
    public void StopAll()
    {
        foreach (var sound in sounds) sound.Stop();
        sounds.Clear();
    }
}
