using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Story 전체 배경과 고정 만화 페이지의 컷 누적 공개를 담당한다.</summary>
public class StoryBackgroundPresenter : MonoBehaviour
{
    [Header("Reference")]
    // Story 화면 배경과 현재 만화 컷을 표시하는 기존 Image다.
    [SerializeField] private Image backgroundImage;

    [Header("Comic Cut")]
    // 하단 대화창을 제외한 컷 표시 영역이다. 좌하단 기준 0~1 앵커 좌표로 설정한다.
    [SerializeField] private Rect comicViewport = new Rect(.04f, .11f, .92f, .89f);


    // 페이지 전체 크기로 고정하며 원본 텍스처를 공유하는 누적 공개 Sprite다.
    private Sprite runtimeCutSprite;
    // 비스케일 시간으로 현재 컷의 진입을 진행하는 코루틴이다.
    private Coroutine cutTransition;
    // 전체 배경 표시로 돌아갈 때 복원할 원래 UI 위치와 색상이다.
    private Vector2 originalAnchorMin, originalAnchorMax, originalPosition, originalSize, originalPivot;
    private Color originalColor;
    // 기존 Image 머티리얼을 보관하고 새 컷만 페이드하는 런타임 머티리얼을 관리한다.
    private Material originalMaterial, revealMaterial;
    private Shader revealShader;
    // 현재 컷의 공개 정도다. 앞서 공개한 컷에는 이 알파를 적용하지 않는다.
    public float CurrentRevealAlpha => revealMaterial == null ? 1f : revealMaterial.GetFloat("_RevealAlpha");
    private bool originalUseSpriteMesh;
    // 현재 페이지와 공개된 컷의 픽셀 꼭짓점·삼각형을 누적한다. 페이지가 바뀌면 비운다.
    private Sprite activeComicPage;
    private readonly List<Vector2> revealedVertices = new();
    private readonly List<ushort> revealedTriangles = new();
    private int revealedCutCount;

    public int RevealedCutCount => revealedCutCount;

    public bool IsShowingComicCut => runtimeCutSprite != null;
    public bool IsCutTransitioning => cutTransition != null;

    /// <summary>필수 Image를 검사하고 기존 배경의 UI 상태를 보관한다.</summary>
    private void Awake()
    {
        if (!HasValidReference()) { enabled = false; return; }
        var rect = backgroundImage.rectTransform;
        originalAnchorMin = rect.anchorMin; originalAnchorMax = rect.anchorMax;
        originalPosition = rect.anchoredPosition; originalSize = rect.sizeDelta; originalPivot = rect.pivot;
        originalColor = backgroundImage.color; originalMaterial = backgroundImage.material; originalUseSpriteMesh = backgroundImage.useSpriteMesh;
    }

    /// <summary>현재 컷 자원을 정리하고 전체 배경 Sprite를 원래 위치에 표시한다.</summary>
    public bool ShowBackground(Sprite backgroundSprite)
    {
        if (!isActiveAndEnabled || backgroundSprite == null)
        {
            Debug.LogError($"{nameof(StoryBackgroundPresenter)} on {name}이 비어 있는 배경 Sprite를 받았습니다.", this);
            return false;
        }
        ResetComicCut();
        backgroundImage.preserveAspect = true;
        backgroundImage.sprite = backgroundSprite;
        backgroundImage.enabled = true;
        return true;
    }

    /// <summary>페이지 위치와 크기를 유지하며 해당 컷을 추가 공개하고 대사 진행을 재개한다.</summary>
    public bool PlayComicCut(Sprite page, Vector2[] polygon, float duration, Action completed)
    {
        if (!isActiveAndEnabled || !HasValidData(page, polygon, duration)) return false;
        StopComicCutTransition();
        if (activeComicPage != page) ResetComicCut();
        cutTransition = StartCoroutine(ComicCutRoutine(page, polygon, duration, completed));
        return true;
    }

    /// <summary>컷 공개 대기를 중단한다. 이미 공개된 컷은 유지한다.</summary>
    public void StopComicCutTransition()
    {
        if (cutTransition != null) { StopCoroutine(cutTransition); cutTransition = null; }
        if (revealMaterial != null) revealMaterial.SetFloat("_RevealAlpha", 1f);

    }

    /// <summary>스킵·재생·배경 변경 시 컷 Sprite를 해제하고 원래 UI 배치를 복원한다.</summary>
    public void ResetComicCut()
    {
        StopComicCutTransition();
        if (revealMaterial != null)
        {
            backgroundImage.material = originalMaterial;
            Destroy(revealMaterial); revealMaterial = null;
        }
        activeComicPage = null; revealedCutCount = 0;
        revealedVertices.Clear(); revealedTriangles.Clear();
        if (runtimeCutSprite == null || backgroundImage == null) return;
        backgroundImage.sprite = null;
        backgroundImage.enabled = false;
        Destroy(runtimeCutSprite); runtimeCutSprite = null;
        var rect = backgroundImage.rectTransform;
        rect.anchorMin = originalAnchorMin; rect.anchorMax = originalAnchorMax;
        rect.pivot = originalPivot; rect.anchoredPosition = originalPosition; rect.sizeDelta = originalSize;
        backgroundImage.color = originalColor;
        backgroundImage.useSpriteMesh = originalUseSpriteMesh;
    }

    /// <summary>씬이 해제될 때 컷 전환과 임시 Sprite를 정리한다.</summary>
    private void OnDestroy()
    {
        if (runtimeCutSprite != null) Destroy(runtimeCutSprite);
        if (revealMaterial != null) Destroy(revealMaterial);
    }

    /// <summary>다음 재생 프레임부터 새 컷의 검은 가림을 서서히 걷고 완료 후 대사를 재개한다.</summary>
    private IEnumerator ComicCutRoutine(Sprite page, Vector2[] polygon, float duration, Action completed)
    {
        // Sprite 경계 변경은 실제 재생 프레임에서 수행한다.
        yield return null;
        if (!StoryComicCutGeometry.TryBuild(polygon, page.rect, page.pixelsPerUnit, out var crop, out var vertices, out var triangles))
        {
            Debug.LogError("만화 컷의 공개 영역을 계산하지 못했습니다.", this);
            cutTransition = null;
            yield break;
        }
        if (runtimeCutSprite == null)
        {
            activeComicPage = page;
            runtimeCutSprite = Sprite.Create(page.texture, page.rect, Vector2.one * .5f, page.pixelsPerUnit, 0, SpriteMeshType.Tight);
            runtimeCutSprite.name = page.name + "_Revealed";
            revealMaterial = new Material(revealShader) { name = "만화 컷 공개" };
            backgroundImage.material = revealMaterial;
            // 삼각형에 연결하지 않는 네 모서리가 전체 페이지의 고정 표시 크기를 보존한다.
            revealedVertices.Add(Vector2.zero);
            revealedVertices.Add(new Vector2(page.rect.width, 0f));
            revealedVertices.Add(page.rect.size);
            revealedVertices.Add(new Vector2(0f, page.rect.height));
            var rect = backgroundImage.rectTransform;
            rect.anchorMin = comicViewport.min; rect.anchorMax = comicViewport.max;
            rect.pivot = Vector2.one * .5f; rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
            backgroundImage.sprite = runtimeCutSprite;
            backgroundImage.useSpriteMesh = true;
            backgroundImage.preserveAspect = true;
            backgroundImage.color = originalColor;
            backgroundImage.enabled = true;
        }
        int vertexOffset = revealedVertices.Count;
        foreach (Vector2 vertex in vertices)
        {
            // 잘라낸 컷의 로컬 좌표를 전체 페이지의 좌하단 기준 픽셀 좌표로 되돌린다.
            Vector2 pixel = vertex * page.pixelsPerUnit + crop.center - page.rect.position;
            revealedVertices.Add(new Vector2(Mathf.Clamp(pixel.x, 0f, page.rect.width), Mathf.Clamp(pixel.y, 0f, page.rect.height)));
        }
        foreach (ushort triangle in triangles) revealedTriangles.Add((ushort)(vertexOffset + triangle));
        runtimeCutSprite.OverrideGeometry(revealedVertices.ToArray(), revealedTriangles.ToArray());
        var fadePolygon = new Vector4[16];
        for (int i = 0; i < polygon.Length; i++)
            fadePolygon[i] = new Vector4((page.rect.x + polygon[i].x * page.rect.width) / page.texture.width,
                (page.rect.y + (1f - polygon[i].y) * page.rect.height) / page.texture.height, 0f, 0f);
        revealMaterial.SetVectorArray("_RevealPolygon", fadePolygon);
        revealMaterial.SetInt("_RevealCount", polygon.Length);
        revealMaterial.SetFloat("_RevealAlpha", 0f);
        backgroundImage.SetVerticesDirty();
        revealedCutCount++;
        // 새 컷만 추가하고 앞서 공개한 컷의 위치·알파·경계는 바꾸지 않는다.
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            revealMaterial.SetFloat("_RevealAlpha", Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration)));
            yield return null;
        }
        revealMaterial.SetFloat("_RevealAlpha", 1f);
        cutTransition = null;
        completed?.Invoke();
    }

    /// <summary>필수 배경 Image와 컷을 표시할 기본 Image 형식을 확인한다.</summary>
    public bool HasValidReference()
    {
        revealShader = Resources.Load<Shader>("StoryComicReveal");
        if (backgroundImage != null && backgroundImage.type == Image.Type.Simple && revealShader != null && revealShader.isSupported) return true;
        Debug.LogError($"{nameof(StoryBackgroundPresenter)} on {name}에는 Simple 형식의 배경 Image와 지원되는 StoryComicReveal 셰이더가 필요합니다.", this);
        return false;
    }

    /// <summary>컷 원화·다각형·진입 시간·표시 영역이 모두 유효한지 검사한다.</summary>
    public bool HasValidData(Sprite page, Vector2[] polygon, float duration)
    {
        bool isValid = page != null && !page.packed && duration > 0f && !float.IsInfinity(duration) &&
            comicViewport.xMin >= 0f && comicViewport.yMin >= 0f && comicViewport.xMax <= 1f && comicViewport.yMax <= 1f &&
            comicViewport.width > 0f && comicViewport.height > 0f &&
            StoryComicCutGeometry.TryBuild(polygon, page.rect, page.pixelsPerUnit, out _, out _, out _);
        if (isValid) return true;
        Debug.LogError($"{nameof(StoryBackgroundPresenter)} on {name}의 만화 컷 원화·다각형·진입 시간 또는 표시 영역이 올바르지 않습니다.", this);
        return false;
    }
}
