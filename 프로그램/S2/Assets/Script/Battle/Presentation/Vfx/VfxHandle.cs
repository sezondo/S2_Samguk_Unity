using UnityEngine;

/// <summary>대여한 이펙트의 표시와 반납 권한을 관리한다. 오래된 핸들은 재사용된 인스턴스에 접근할 수 없다.</summary>
public sealed class VfxHandle
{
    private readonly VfxManager manager;
    private readonly VfxData data;
    private readonly GameObject instance;
    private bool released;
    // 풀 생성 시 수집한 부품이며 핵심 Actor 참조를 검색하는 용도가 아니다.
    private readonly SpriteRenderer[] parts;
    // 머티리얼을 복제하지 않고 이 인스턴스의 연출 값만 바꾼다.
    private readonly MaterialPropertyBlock properties = new();

    /// <summary>대여 인스턴스와 생성 시 확인한 Sprite 부품을 보관한다.</summary>
    internal VfxHandle(VfxManager manager, VfxData data, GameObject instance, SpriteRenderer[] parts)
    {
        this.manager = manager;
        this.data = data;
        this.instance = instance;
        this.parts = parts;
    }

    // 이미 반납된 핸들은 다시 위치를 바꾸거나 반납하지 않게 막는다.
    // 같은 오브젝트가 풀에서 재사용될 수 있으므로, 오래된 핸들이 새 이펙트를 건드리면 안 된다.
    public bool IsValid => !released && manager != null && instance != null;

    /// <summary>현재 대여가 유효할 때만 표시 위치·회전·크기를 바꾼다.</summary>
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

    /// <summary>유효한 대여를 한 번 반납한다.</summary>
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
            Debug.LogError($"VFX {data.name}의 {index}번 Sprite 부품 연결이 올바르지 않습니다.");
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

    public VfxData Data => data;
    internal GameObject Instance => instance;
    internal bool IsReleased => released;
    internal void MarkReleased()
    {
        released = true;
    }
}

