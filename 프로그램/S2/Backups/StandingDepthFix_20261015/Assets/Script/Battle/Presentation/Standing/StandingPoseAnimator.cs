using UnityEngine;

/// <summary>기존 Animator의 동작 전환을 유지하며 대기 자세에만 부위별 호흡을 표시한다.</summary>
[DefaultExecutionOrder(500)]
public sealed class StandingPoseAnimator : MonoBehaviour
{
    // 시각 참조는 표시 Context를 통해서만 사용한다.
    public StandingVisualContext context;
    // 현재 표시 메시와 원본 렌더러를 숨겼는지 보관한다.
    private StandingPoseMesh active;
    private bool ownsHiddenRenderer;
    // 시야 알파·피격 색상·전투 배경 흐림을 전달하는 표시 속성이다.
    private MaterialPropertyBlock properties;

    /// <summary>필수 참조와 확정 자세 데이터를 검사한다.</summary>
    private void Awake()
    {
        if(!HasValidReference()||!HasValidData()){enabled=false;return;}
        properties=new MaterialPropertyBlock();
    }

    /// <summary>최종 동작·방향·정렬·색상에 맞춰 대기 메시를 표시한다.</summary>
    private void LateUpdate()
    {
        var renderer=context.sourceRenderer;var visual=context.visual;StandingPoseMesh next=null;
        if(visual.enabled&&!visual.IsMovingPresentation&&!visual.IsDeathPresentation&&visual.Animator!=null)
        {
            var state=visual.Animator.GetCurrentAnimatorStateInfo(0);
            if(state.IsName("Idle"))next=context.idle;
            else if(state.IsName("LowCoverIdle"))next=context.lowCover;
            else if(state.IsName("WallCoverIdle"))next=context.wallCover;
            // 크로스페이드 중 공격 원화가 이미 표시됐으면 기존 스프라이트를 그대로 사용한다.
            if(next!=null&&renderer.sprite!=next.profile.sprite)next=null;
        }
        if(active!=next)
        {
            if(active!=null)active.gameObject.SetActive(false);
            active=next;
            if(active!=null){active.gameObject.SetActive(true);active.Initialize();}
        }
        if(active==null){RestoreRenderer();return;}
        if(!renderer.enabled&&!ownsHiddenRenderer){active.gameObject.SetActive(false);active=null;return;}
        if(!active.enabled){RestoreRenderer();active.gameObject.SetActive(false);active=null;return;}
        ownsHiddenRenderer=true;renderer.enabled=false;
        float x=renderer.flipX?-1:1,y=renderer.flipY?-1:1;
        active.transform.localPosition=Vector3.Scale(active.SpriteLocalOffset,new Vector3(x,y,1));
        active.transform.localScale=new Vector3(x,y,1);
        active.Evaluate(Time.time);
        active.meshRenderer.sortingLayerID=renderer.sortingLayerID;active.meshRenderer.sortingOrder=renderer.sortingOrder;
        properties.Clear();properties.SetColor("_Color",renderer.color);
        var sourceMaterial=renderer.sharedMaterial;
        properties.SetFloat("_FocusAmount",sourceMaterial!=null&&sourceMaterial.HasProperty("_FocusAmount")?sourceMaterial.GetFloat("_FocusAmount"):0);
        active.meshRenderer.SetPropertyBlock(properties);
    }

    /// <summary>연출을 종료하거나 중단하면 기존 스프라이트 표시를 돌려준다.</summary>
    private void RestoreRenderer(){if(ownsHiddenRenderer&&context!=null&&context.sourceRenderer!=null)context.sourceRenderer.enabled=true;ownsHiddenRenderer=false;}

    /// <summary>비활성화 시 메시와 원본 표시를 정리한다.</summary>
    private void OnDisable(){if(active!=null)active.gameObject.SetActive(false);active=null;RestoreRenderer();}

    /// <summary>필수 시각 참조가 명시적으로 연결됐는지 검사한다.</summary>
    public bool HasValidReference()
    {
        if(context!=null&&context.visual!=null&&context.sourceRenderer!=null&&context.visual.TargetRenderer==context.sourceRenderer&&context.idle!=null&&context.lowCover!=null&&context.wallCover!=null)return true;
        Debug.LogError("대기 애니메이션의 표시 Context 참조가 누락됐습니다.",this);return false;
    }

    /// <summary>세 대기 자세에 필요한 데이터를 소비 지점에서 검사한다.</summary>
    public bool HasValidData(){return context.idle.HasValidData()&&context.lowCover.HasValidData()&&context.wallCover.HasValidData();}
}
