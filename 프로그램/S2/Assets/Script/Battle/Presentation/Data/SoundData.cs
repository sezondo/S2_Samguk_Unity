using UnityEngine;
using UnityEngine.Audio;

/// <summary>공유 효과음의 후보 음원과 재생 기본값을 보관한다. 유효성 검사는 재생 담당이 한다.</summary>
[CreateAssetMenu(menuName = "S2/Presentation/Sound", fileName = "SoundData")]
public sealed class SoundData : ScriptableObject
{
    // 재생할 때 하나를 선택하는 단발 음원 후보다. 빈 후보는 허용하지 않는다.
    [SerializeField] private AudioClip[] clips;
    // 개별 효과음 음량과 후보별 자연스러운 음높이 변동 범위다.
    [SerializeField, Range(0, 1)] private float volume = .8f;
    [SerializeField] private Vector2 pitchRange = new(.97f, 1.03f);
    // 0은 화면 중심의 2D 소리, 1은 재생 위치에 따른 3D 소리다.
    [SerializeField, Range(0, 1)] private float spatialBlend;
    // 3D 재생 시 거리 감쇠를 적용하는 최소·최대 거리다.
    [SerializeField] private Vector2 distanceRange = new(1f, 30f);
    // 연결하면 해당 믹서로 출력한다. 미지정은 Unity 기본 출력이라는 명시적 선택이다.
    [SerializeField] private AudioMixerGroup mixerGroup;
    public AudioClip[] Clips => clips;
    public float Volume => volume;
    public Vector2 PitchRange => pitchRange;
    public float SpatialBlend => spatialBlend;
    public Vector2 DistanceRange => distanceRange;
    public AudioMixerGroup MixerGroup => mixerGroup;
}
