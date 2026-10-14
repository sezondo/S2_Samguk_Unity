using UnityEngine;

/// <summary>공유 이펙트의 프리팹과 단발 재생 기본값을 보관한다. 사용자가 유효성을 검사한다.</summary>
[CreateAssetMenu(menuName = "S2/Presentation/VFX", fileName = "VfxData")]
public sealed class VfxData : ScriptableObject
{
    // 풀에서 생성할 원본 이펙트 프리팹이다.
    [SerializeField] private GameObject prefab;
    // Play 호출에서 자동 반납하기까지의 게임 시간이다. 대여 호출은 Presenter가 직접 반납한다.
    [SerializeField] private float defaultLifetime = .35f;
    public GameObject Prefab => prefab;
    public float DefaultLifetime => defaultLifetime;
}
