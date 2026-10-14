using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>이펙트 데이터별 인스턴스를 대여·회수한다. 재생할 종류의 선택은 연출 데이터가 담당한다.</summary>
public sealed class VfxManager : MonoBehaviour
{
    [Serializable]
    private sealed class PrewarmEntry
    {
        // 재생 필수 등록표가 아니라 시작 시 준비할 데이터와 개수·선택적 별도 부모다.
        public VfxData data;
        public int count;
        public Transform poolRoot;
    }
    // 비어 있어도 데이터로 직접 재생할 수 있다. 여기에 없는 데이터는 최초 재생 시 풀이 만들어진다.
    [SerializeField] private PrewarmEntry[] prewarm;
    // 데이터별 유휴 인스턴스와 선택적 풀 부모다.
    private readonly Dictionary<VfxData, Queue<GameObject>> poolByData = new();
    private readonly Dictionary<VfxData, Transform> parents = new();
    // 생성 시 수집한 원화 부품과 현재 유효한 대여 권한이다.
    private readonly Dictionary<GameObject, SpriteRenderer[]> spriteParts = new();
    private readonly HashSet<VfxHandle> activeHandles = new();
    public int ActiveHandleCount => activeHandles.Count;
    public int CreatedInstanceCount => spriteParts.Count;

    /// <summary>미리 생성 목록을 검사한 뒤 각 데이터의 풀을 준비한다.</summary>
    private void Awake()
    {
        if (prewarm == null) return;
        var seen = new HashSet<VfxData>();
        foreach(var item in prewarm)
        {
            if(item==null || !HasValidData(item.data) || item.count<0 || !seen.Add(item.data))
            { Debug.LogError("이펙트 미리 생성 목록에 누락·중복 데이터 또는 잘못된 개수가 있습니다.",this); enabled=false; return; }
        }
        foreach(var item in prewarm)
        {
            parents[item.data]=item.poolRoot;
            EnsurePool(item.data);
            for(int i=0;i<item.count;i++) ReleaseInstance(item.data,CreateInstance(item.data));
        }
    }

    /// <summary>씬 종료와 비활성화 시 모든 대여 핸들을 무효화하고 반납한다.</summary>
    private void OnDisable()
    { StopAllCoroutines(); foreach(var handle in new List<VfxHandle>(activeHandles)) Release(handle); }

    /// <summary>프리팹과 기본 단발 표시 시간이 유효한지 검사한다.</summary>
    public bool HasValidData(VfxData data)
    {
        bool valid=data!=null && data.Prefab!=null && data.DefaultLifetime>0 && !float.IsNaN(data.DefaultLifetime) && !float.IsInfinity(data.DefaultLifetime);
        if(!valid) Debug.LogError("이펙트 데이터에 프리팹과 유효한 기본 표시 시간이 필요합니다.",this);
        return valid;
    }

    /// <summary>단발 이펙트를 기본 크기로 재생한다.</summary>
    public bool Play(VfxData data,Vector3 position,Quaternion rotation) => Play(data,position,rotation,Vector3.one);

    /// <summary>단발 이펙트를 재생하고 데이터의 기본 시간 뒤 반납한다.</summary>
    public bool Play(VfxData data,Vector3 position,Quaternion rotation,Vector3 scale)
    {
        var handle=Acquire(data);
        if(handle==null)return false;
        handle.SetTransform(position,rotation,scale);
        StartCoroutine(ReleaseAfterDelay(handle,data.DefaultLifetime));
        return true;
    }

    /// <summary>데이터를 키로 이펙트를 대여한다. 반환 핸들은 호출자가 명시적으로 반납한다.</summary>
    public VfxHandle Acquire(VfxData data)
    {
        if(!isActiveAndEnabled || !HasValidData(data))return null;
        EnsurePool(data);
        var pool=poolByData[data];
        GameObject instance=null;
        while(pool.Count>0 && instance==null)instance=pool.Dequeue();
        if(instance==null)instance=CreateInstance(data);
        ActivateInstance(instance);
        var handle=new VfxHandle(this,data,instance,spriteParts[instance]);
        activeHandles.Add(handle);
        return handle;
    }

    /// <summary>이 담당이 발급한 핸들만 한 번 반납해 이중 회수와 이전 핸들의 간섭을 막는다.</summary>
    public void Release(VfxHandle handle)
    {
        if(handle==null || !activeHandles.Remove(handle))return;
        handle.MarkReleased();
        if(handle.Instance!=null)ReleaseInstance(handle.Data,handle.Instance);
    }

    /// <summary>새 데이터의 빈 풀만 만들며 재생 프리팹을 임의로 보정하지 않는다.</summary>
    private void EnsurePool(VfxData data)
    { if(!poolByData.ContainsKey(data))poolByData.Add(data,new Queue<GameObject>()); }

    /// <summary>데이터가 지정한 프리팹을 생성하고 표시 부품을 한 번 수집한다.</summary>
    private GameObject CreateInstance(VfxData data)
    {
        // 별도 부모는 미리 생성 정책의 선택 항목이며 기본은 이 담당의 Transform이다.
        Transform parent=parents.TryGetValue(data,out var selected) && selected!=null?selected:transform;
        var instance=Instantiate(data.Prefab,parent);
        instance.name=data.Prefab.name+"_"+data.name;
        spriteParts[instance]=instance.GetComponentsInChildren<SpriteRenderer>(true);
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


    /// <summary>원화·파티클을 정리하고 같은 데이터의 유휴 풀에 돌려준다.</summary>
    private void ReleaseInstance(VfxData data,GameObject instance)
    {
        instance.SetActive(false);
        foreach(var particle in instance.GetComponentsInChildren<ParticleSystem>(true))
            particle.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        poolByData[data].Enqueue(instance);
    }

    /// <summary>게임 시간에 따라 단발 이펙트를 자동 반납한다.</summary>
    private IEnumerator ReleaseAfterDelay(VfxHandle handle,float delay)
    { yield return new WaitForSeconds(delay); Release(handle); }
}
