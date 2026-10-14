using UnityEngine;

/// <summary>연출에 사용하는 이펙트·오디오 재생 담당의 명시적인 참조 주머니다.</summary>
public sealed class BattlePresentationContext : MonoBehaviour
{
    // 현재 씬의 이펙트 풀과 효과음 풀이다. 재생 정책과 상태는 각 담당이 관리한다.
    [SerializeField] private VfxManager vfx;
    [SerializeField] private AudioManager audio;
    public VfxManager Vfx => vfx;
    public AudioManager Audio => audio;
}
