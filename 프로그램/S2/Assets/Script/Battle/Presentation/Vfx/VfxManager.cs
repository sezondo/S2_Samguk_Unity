using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum VfxId
{
    None,
    WeaponVanish,
    WeaponReappear,
    WeaponAimingCharged,
    WeaponMeleeFlash,
    WeaponThrowStart,
    MeleeSlash1,
    MeleeSlash2,
    MeleeSlash3,
    EnemyHit,
    EnemyDeath,
    // 기존 직렬화 번호를 보존하고 확정 원화 식별자를 뒤에 추가한다.
    SharedBloodHit,
    SharedDodge,
    YujinUnarmedImpact,
    YujinSwordSlash,
    YujinGunMuzzle,
    YujinSwordTrail,
    HackCircuit,
    // 천하회 확정 공격 원화이며 기존 ID 순서는 유지한다.
    CheonhaHammer,
    CheonhaClub,
    CheonhaCrossbow,
    CheonhaBow,
}

// Acquire/Release 방식으로 빌린 이펙트의 손잡이다.
// 참격, 장판, 차징 표시처럼 "몇 프레임 동안 유지하다가 직접 끄는 이펙트"에서 사용한다.
public sealed class VfxHandle
{
    private readonly VfxManager manager;
    private readonly VfxId id;
    private readonly GameObject instance;
    private bool released;
    // 풀 생성 시 수집한 부품이며 핵심 Actor 참조를 검색하는 용도가 아니다.
    private readonly SpriteRenderer[] parts;
    // 머티리얼을 복제하지 않고 이 인스턴스의 연출 값만 바꾼다.
    private readonly MaterialPropertyBlock properties = new();

    /// <summary>대여 인스턴스와 생성 시 확인한 Sprite 부품을 보관한다.</summary>
    internal VfxHandle(VfxManager manager, VfxId id, GameObject instance, SpriteRenderer[] parts)
    {
        this.manager = manager;
        this.id = id;
        this.instance = instance;
        this.parts = parts;
    }

    // 이미 반납된 핸들은 다시 위치를 바꾸거나 반납하지 않게 막는다.
    // 같은 오브젝트가 풀에서 재사용될 수 있으므로, 오래된 핸들이 새 이펙트를 건드리면 안 된다.
    public bool IsValid => !released && manager != null && instance != null;

    public void SetTransform(Vector3 position, Quaternion rotation, Vector3 scale)
    {
        if (!IsValid)
        {
            return;
        }

        Transform effectTransform = instance.transform;
        effectTransform.SetPositionAndRotation(position, rotation);
        effectTransform.localScale = scale;
    }

    public void Release()
    {
        if (!IsValid)
        {
            return;
        }

        manager.Release(this);
    }

    // Presenter가 프리팹의 부품 계약을 확인할 때 사용하는 개수다.
    public int PartCount => parts.Length;

    /// <summary>프리팹 부품을 지정한 크기와 위치에 배치하고 표시 진행값을 갱신한다.</summary>
    public void SetPart(int index, Vector3 localPosition, Vector2 size, float opacity = 1f,
        float progress = 1f, float pulse = 0f, float repeat = 1f)
    {
        if (!IsValid) return;
        if (index < 0 || index >= parts.Length || parts[index].sprite == null)
        {
            Debug.LogError($"VFX {id}의 {index}번 Sprite 부품 연결이 올바르지 않습니다.");
            return;
        }
        SpriteRenderer part = parts[index];
        Vector3 native = part.sprite.bounds.size;
        part.transform.localPosition = localPosition;
        part.transform.localRotation = Quaternion.identity;
        part.transform.localScale = new Vector3(size.x / native.x, size.y / native.y, 1f);
        part.GetPropertyBlock(properties);
        properties.SetFloat("_Opacity", Mathf.Clamp01(opacity));
        properties.SetFloat("_Progress", Mathf.Clamp01(progress));
        properties.SetFloat("_Pulse", Mathf.Max(0f, pulse));
        properties.SetFloat("_BodyRepeat", Mathf.Max(0.001f, repeat));
        part.SetPropertyBlock(properties);
    }

    /// <summary>단일 원화의 비율을 유지하며 지정한 캔버스 높이로 표시한다.</summary>
    public void SetSingle(float height, float opacity)
    {
        if (!IsValid || parts.Length != 1 || parts[0].sprite == null) return;
        Vector3 native = parts[0].sprite.bounds.size;
        SetPart(0, Vector3.zero, new Vector2(height * native.x / native.y, height), opacity);
    }

    /// <summary>Actor의 현재 정렬 기준과 상대 부품 순서를 적용한다.</summary>
    public void SetSorting(int layer, int order)
    {
        if (!IsValid) return;
        for (int i = 0; i < parts.Length; i++)
        {
            parts[i].sortingLayerID = layer;
            parts[i].sortingOrder = order + i;
        }
    }

    internal VfxId Id => id;
    internal GameObject Instance => instance;
    internal bool IsReleased => released;
    internal void MarkReleased()
    {
        released = true;
    }
}

public class VfxManager : MonoBehaviour
{
    [Serializable]
    private class VfxEntry
    {
        // 코드에서 호출할 식별자다. 프리팹 직접 참조 대신 ID를 쓰면 호출부가 프리팹 구조를 몰라도 된다.
        public VfxId id = VfxId.None;
        // 실제로 생성할 이펙트 프리팹이다. PNG SpriteRenderer, Animator, ParticleSystem 모두 가능하다.
        public GameObject prefab = null;
        // Play 방식으로 호출한 단발 이펙트를 몇 초 뒤 풀에 반납할지 정한다.
        // PNG 애니메이션이면 애니메이션 길이보다 살짝 길게 잡으면 된다.
        public float defaultLifetime = 0.5f;
        // 씬 시작 때 미리 만들어둘 개수다. 자주 쓰는 피격 이펙트는 5~20개 정도 잡아도 된다.
        public int prewarmCount = 0;
        // 특정 이펙트들을 따로 모아둘 부모 Transform이다. 비워두면 VfxManager 오브젝트 아래에 생성한다.
        public Transform poolRoot = null;
    }

    [Header("VFX Table")]
    // 씬에 VfxManager 오브젝트를 하나 만들고, 여기에서 VfxId별 프리팹을 연결한다.
    [SerializeField] private VfxEntry[] entries;

    public static VfxManager Instance { get; private set; }

    private readonly Dictionary<VfxId, VfxEntry> entryById = new();
    private readonly Dictionary<VfxId, Queue<GameObject>> poolById = new();
    private Transform defaultPoolRoot;
    // 인스턴스별 부품을 생성 시 한 번 수집하며 재사용 시 표시 상태를 초기화한다.
    private readonly Dictionary<GameObject, SpriteRenderer[]> spriteParts = new();
    // 씬 비활성화 시 대여 핸들을 무효화해 이전 연출이 새 대여분을 건드리지 않게 한다.
    private readonly HashSet<VfxHandle> activeHandles = new();

    /// <summary>비활성화 시 재생 중인 이펙트를 모두 회수한다.</summary>
    private void OnDisable()
    {
        StopAllCoroutines();
        foreach (VfxHandle handle in new List<VfxHandle>(activeHandles)) Release(handle);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"{nameof(VfxManager)}: 이미 인스턴스가 있습니다. 중복 오브젝트 {name}의 컴포넌트를 비활성화합니다.", this);
            enabled = false;
            return;
        }

        Instance = this;
        defaultPoolRoot = transform;
        // entries 배열을 Dictionary로 바꿔서 런타임 호출은 빠르게 처리한다.
        BuildTable();
        // 인스펙터에서 지정한 개수만큼 미리 생성해 첫 재생 순간의 Instantiate 비용을 줄인다.
        PrewarmPools();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public static bool TryPlay(VfxId id, Vector3 position, Quaternion rotation)
    {
        return TryPlay(id, position, rotation, Vector3.one);
    }

    public static bool TryPlay(VfxId id, Vector3 position, Quaternion rotation, Vector3 scale)
    {
        if (Instance == null)
        {
            return false;
        }

        return Instance.Play(id, position, rotation, scale);
    }

    /// <summary>활성 씬 매니저에서 이펙트를 대여하고 참조 누락을 보고한다.</summary>
    public static VfxHandle TryAcquire(VfxId id)
    {
        if (Instance != null && Instance.isActiveAndEnabled) return Instance.Acquire(id);
        Debug.LogError("이펙트를 재생할 활성 VfxManager가 씬에 없습니다.");
        return null;
    }

    public bool Play(VfxId id, Vector3 position, Quaternion rotation)
    {
        return Play(id, position, rotation, Vector3.one);
    }

    public bool Play(VfxId id, Vector3 position, Quaternion rotation, Vector3 scale)
    {
        // Play는 단발 이펙트용이다.
        // 이펙트를 켠 뒤 defaultLifetime이 지나면 자동으로 풀에 반납한다.
        VfxHandle handle = Acquire(id);
        if (handle == null)
        {
            return false;
        }

        handle.SetTransform(position, rotation, scale);
        float lifetime = GetDefaultLifetime(id);
        StartCoroutine(ReleaseAfterDelay(handle, lifetime));
        return true;
    }

    /// <summary>지정한 프리팹을 풀에서 꺼내 초기화하고 유효한 대여 핸들을 발급한다.</summary>
    public VfxHandle Acquire(VfxId id)
    {
        // Acquire는 호출자가 직접 Release해야 하는 지속 이펙트용이다.
        // 예: PlayerMeleeSlashEffect가 공격 진행률 동안 참격을 켰다가 구간이 끝나면 반납한다.
        if (id == VfxId.None || !entryById.TryGetValue(id, out VfxEntry entry) || entry.prefab == null)
        {
            Debug.LogError($"VfxManager에 {id} 이펙트 프리팹이 등록되지 않았습니다.", this);
            return null;
        }

        GameObject instance = GetOrCreateInstance(entry);
        if (instance == null)
        {
            return null;
        }

        ActivateInstance(instance);
        VfxHandle handle = new VfxHandle(this, id, instance, spriteParts[instance]);
        activeHandles.Add(handle);
        return handle;
    }

    /// <summary>이 매니저가 대여한 핸들만 한 번 무효화하고 풀로 반환한다.</summary>
    public void Release(VfxHandle handle)
    {
        // 이미 반납된 핸들은 무시한다.
        // Play 코루틴과 수동 Release가 겹쳐도 같은 오브젝트가 두 번 풀에 들어가지 않게 한다.
        if (handle == null || handle.IsReleased || handle.Instance == null || !activeHandles.Contains(handle))
        {
            return;
        }

        activeHandles.Remove(handle);
        handle.MarkReleased();
        ReleaseInstance(handle.Id, handle.Instance);
    }

    private void BuildTable()
    {
        entryById.Clear();
        poolById.Clear();

        if (entries == null)
        {
            return;
        }

        foreach (VfxEntry entry in entries)
        {
            if (entry == null || entry.id == VfxId.None)
            {
                continue;
            }

            // 같은 ID가 여러 번 들어오면 뒤쪽 항목으로 덮는다.
            // 인스펙터 실수는 Unity 콘솔에서 entries를 보면 바로 잡을 수 있게 단순하게 둔다.
            entryById[entry.id] = entry;
            if (!poolById.ContainsKey(entry.id))
            {
                poolById.Add(entry.id, new Queue<GameObject>());
            }
        }
    }

    private void PrewarmPools()
    {
        foreach (VfxEntry entry in entryById.Values)
        {
            if (entry.prefab == null)
            {
                continue;
            }

            int count = Mathf.Max(0, entry.prewarmCount);
            for (int i = 0; i < count; i++)
            {
                ReleaseInstance(entry.id, CreateInstance(entry));
            }
        }
    }

    private GameObject GetOrCreateInstance(VfxEntry entry)
    {
        Queue<GameObject> pool = poolById[entry.id];
        while (pool.Count > 0)
        {
            GameObject pooled = pool.Dequeue();
            if (pooled != null)
            {
                return pooled;
            }
        }

        // 풀에 남은 것이 없으면 새로 만든다. 이후 Release되면 다시 풀에 들어간다.
        return CreateInstance(entry);
    }

    /// <summary>프리팹을 생성하고 Sprite 부품을 재사용할 수 있게 한 번 수집한다.</summary>
    private GameObject CreateInstance(VfxEntry entry)
    {
        Transform parent = entry.poolRoot != null ? entry.poolRoot : defaultPoolRoot;
        GameObject instance = Instantiate(entry.prefab, parent);
        instance.name = $"{entry.prefab.name}_{entry.id}";
        spriteParts[instance] = instance.GetComponentsInChildren<SpriteRenderer>(true);
        instance.SetActive(false);
        return instance;
    }

    /// <summary>신규 Sprite 원화 상태를 초기화하고 기존 파티클 재생을 시작한다.</summary>
    private void ActivateInstance(GameObject instance)
    {
        instance.transform.localScale = Vector3.one;
        foreach (SpriteRenderer part in spriteParts[instance])
        {
            // 기존 파티클·애니메이터 프리팹의 고유 배치는 보존한다.
            if (part.sharedMaterial == null || part.sharedMaterial.shader.name != "S2/Presentation/Effect") continue;
            part.SetPropertyBlock(null);
            part.color = Color.white;
            part.enabled = true;
            part.transform.localPosition = Vector3.zero;
            part.transform.localRotation = Quaternion.identity;
            part.transform.localScale = Vector3.one;
        }
        instance.SetActive(true);
        // 프리팹 안에 ParticleSystem이 있으면 매번 처음부터 재생되게 초기화한다.
        // PNG SpriteRenderer + Animator 프리팹은 SetActive(true)만으로 사용할 수 있다.
        ParticleSystem[] particles = instance.GetComponentsInChildren<ParticleSystem>(true);
        foreach (ParticleSystem particle in particles)
        {
            particle.Clear(true);
            particle.Play(true);
        }
    }

    private void ReleaseInstance(VfxId id, GameObject instance)
    {
        if (instance == null)
        {
            return;
        }

        instance.SetActive(false);
        // 파티클이 남아 있는 채로 풀에 들어가면 다음 재생 때 이전 잔상이 보일 수 있으므로 정리한다.
        ParticleSystem[] particles = instance.GetComponentsInChildren<ParticleSystem>(true);
        foreach (ParticleSystem particle in particles)
        {
            particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        if (!poolById.TryGetValue(id, out Queue<GameObject> pool))
        {
            pool = new Queue<GameObject>();
            poolById.Add(id, pool);
        }

        pool.Enqueue(instance);
    }

    private float GetDefaultLifetime(VfxId id)
    {
        if (!entryById.TryGetValue(id, out VfxEntry entry))
        {
            return 0.5f;
        }

        return Mathf.Max(0.01f, entry.defaultLifetime);
    }

    private IEnumerator ReleaseAfterDelay(VfxHandle handle, float delay)
    {
        yield return new WaitForSeconds(delay);
        Release(handle);
    }
}
