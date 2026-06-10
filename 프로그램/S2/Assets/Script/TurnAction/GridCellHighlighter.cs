using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 그리드 칸 목록을 받아 보드 위에 하이라이트를 표시한다.
/// 이동 범위, 검 투척 범위, 해킹 예상 범위처럼 여러 시스템에서 재사용할 수 있는 표시 전용 컴포넌트다.
/// </summary>
public class GridCellHighlighter : MonoBehaviour
{
    [Header("Highlight")]
    // 하이라이트 오브젝트를 모아둘 부모다. 비어 있으면 런타임에 별도 루트를 만든다.
    [SerializeField] private Transform highlightRoot;
    // 칸마다 배치할 하이라이트 프리팹이다. 비어 있으면 임시 사각형을 생성한다.
    [SerializeField] private GameObject highlightPrefab;
    // 하이라이트가 보드 한 칸에서 차지할 비율이다.
    [SerializeField] private float cellScaleRatio = 0.85f;
    // 임시 사각형 하이라이트에 적용할 색이다.
    [SerializeField] private Color fallbackColor = new(0.2f, 0.75f, 1f, 0.35f);
    // 하이라이트 렌더러의 정렬 순서다.
    [SerializeField] private int sortingOrder = 20;
    // 하이라이트를 월드 좌표에서 살짝 앞으로 빼거나 뒤로 보낼 때 쓰는 Z 오프셋이다.
    [SerializeField] private float zOffset = -0.05f;

    // 현재 화면에 표시 중인 하이라이트 오브젝트 목록이다.
    private readonly List<GameObject> activeHighlights = new();
    // 재사용을 위해 숨겨둔 하이라이트 오브젝트 풀이다.
    private readonly Stack<GameObject> pooledHighlights = new();

    // highlightRoot가 비어 있을 때 생성하는 런타임 전용 루트다.
    private Transform runtimeHighlightRoot;

    // 프리팹이 없을 때 사용할 기본 사각형 스프라이트다.
    private static Sprite fallbackSprite;

    /// <summary>
    /// 컴포넌트가 꺼질 때 표시 중인 하이라이트를 정리한다.
    /// </summary>
    private void OnDisable()
    {
        Hide();
    }

    /// <summary>
    /// 지정한 칸 목록에 하이라이트를 표시한다.
    /// </summary>
    public void Show(IReadOnlyList<GridPosition> positions)
    {
        Hide();

        GridManager gridManager = GridManager.Instance;
        if (gridManager == null || positions == null)
        {
            return;
        }

        for (int i = 0; i < positions.Count; i++)
        {
            GameObject highlight = GetHighlight();
            if (highlight == null)
            {
                continue;
            }

            Vector3 worldPosition = gridManager.GridToWorld(positions[i]);
            worldPosition.z += zOffset;

            ApplyHighlightStyle(highlight);
            highlight.transform.SetPositionAndRotation(worldPosition, Quaternion.identity);
            float cellScale = Mathf.Max(0.01f, gridManager.CellSize * cellScaleRatio);
            highlight.transform.localScale = new Vector3(cellScale, cellScale, 1f);
            highlight.SetActive(true);
            activeHighlights.Add(highlight);
        }
    }

    /// <summary>
    /// 런타임에서 임시 하이라이트의 색상과 표시 크기 기준을 설정한다.
    /// </summary>
    public void ConfigureFallbackStyle(Color color, float scaleRatio, int rendererSortingOrder, float worldZOffset)
    {
        fallbackColor = color;
        cellScaleRatio = scaleRatio;
        sortingOrder = rendererSortingOrder;
        zOffset = worldZOffset;
    }

    /// <summary>
    /// 현재 표시 중인 하이라이트를 숨기고 풀로 돌려보낸다.
    /// </summary>
    public void Hide()
    {
        for (int i = 0; i < activeHighlights.Count; i++)
        {
            GameObject highlight = activeHighlights[i];
            if (highlight == null)
            {
                continue;
            }

            highlight.SetActive(false);
            pooledHighlights.Push(highlight);
        }

        activeHighlights.Clear();
    }

    /// <summary>
    /// 풀에서 하이라이트 오브젝트를 꺼내거나 새로 만든다.
    /// </summary>
    private GameObject GetHighlight()
    {
        while (pooledHighlights.Count > 0) //while을 쓰는 이유는 풀 안에 null이 섞여 있을 수도 있기 때문
        {
            GameObject pooled = pooledHighlights.Pop();
            if (pooled != null)
            {
                return pooled;
            }
        }

        Transform parent = ResolveHighlightRoot();
        return highlightPrefab != null ? Instantiate(highlightPrefab, parent) : CreateFallbackHighlight(parent);
    }

    /// <summary>
    /// 하이라이트 프리팹이나 임시 스프라이트에 현재 표시 스타일을 적용한다.
    /// </summary>
    private void ApplyHighlightStyle(GameObject highlight)
    {
        if (highlight == null)
        {
            return;
        }

        SpriteRenderer[] spriteRenderers = highlight.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            spriteRenderers[i].color = fallbackColor;
            spriteRenderers[i].sortingOrder = sortingOrder;
        }
    }

    /// <summary>
    /// 프리팹 없이 테스트할 수 있는 임시 사각형 하이라이트를 만든다.
    /// </summary>
    private GameObject CreateFallbackHighlight(Transform parent)
    {
        GameObject highlight = new($"{nameof(GridCellHighlighter)}_Fallback");
        highlight.transform.SetParent(parent);

        SpriteRenderer spriteRenderer = highlight.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = GetFallbackSprite();
        spriteRenderer.color = fallbackColor;
        spriteRenderer.sortingOrder = sortingOrder;

        highlight.SetActive(false);
        return highlight;
    }

    /// <summary>
    /// 하이라이트가 특정 액터의 스케일과 이동에 끌려가지 않도록 독립 루트를 확보한다.
    /// </summary>
    private Transform ResolveHighlightRoot()
    {
        if (highlightRoot != null)
        {
            return highlightRoot;
        }

        if (runtimeHighlightRoot != null)
        {
            return runtimeHighlightRoot;
        }

        GameObject rootObject = new($"{nameof(GridCellHighlighter)}_RuntimeRoot");
        runtimeHighlightRoot = rootObject.transform;
        return runtimeHighlightRoot;
    }

    /// <summary>
    /// 런타임 임시 하이라이트에 사용할 1픽셀 사각형 스프라이트를 반환한다.
    /// </summary>
    private static Sprite GetFallbackSprite()
    {
        if (fallbackSprite != null)
        {
            return fallbackSprite;
        }

        Texture2D texture = new(1, 1)
        {
            name = $"{nameof(GridCellHighlighter)}_FallbackTexture",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
        };
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();

        fallbackSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        fallbackSprite.name = $"{nameof(GridCellHighlighter)}_FallbackSprite";
        return fallbackSprite;
    }
}
