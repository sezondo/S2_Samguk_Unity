using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

/// <summary>숨긴 원본 대신 호흡 메시가 건물·소품의 기존 정렬·가림 판정에 참여하는지 검사한다.</summary>
public static class StandingDepthVerification
{
    /// <summary>실제 등록된 유진·적을 임시 표시 위치에서 검사하고 원상 복원한다.</summary>
    [MenuItem("Tools/S2/호흡 메시 건물 소품 정렬 검증")]
    public static void Verify()
    {
        if(!EditorApplication.isPlaying){Debug.LogError("전투 씬을 재생한 뒤 호흡 정렬 검증을 실행하십시오.");return;}
        var actors=UnityEngine.Object.FindObjectsByType<StandingPoseAnimator>(FindObjectsInactive.Exclude,FindObjectsSortMode.None);
        if(actors.Length!=4)throw new Exception("유진과 적 세 종류가 있는 전투 씬이 필요합니다.");
        var buildings=UnityEngine.Object.FindObjectsByType<BuildingOcclusionPresenter>(FindObjectsSortMode.None);
        var props=UnityEngine.Object.FindObjectsByType<PropDepthPresenter>(FindObjectsSortMode.None);
        var buildingEnabled=buildings.Select(b=>b.enabled).ToArray();var propEnabled=props.Select(p=>p.enabled).ToArray();
        var late=typeof(StandingPoseAnimator).GetMethod("LateUpdate",BindingFlags.NonPublic|BindingFlags.Instance);
        var occluded=typeof(BuildingOcclusionPresenter).GetMethod("HasOccludedUnit",BindingFlags.NonPublic|BindingFlags.Instance);
        var report=new List<string>();var testRoot=new GameObject("호흡 정렬 임시 검증");testRoot.SetActive(false);testRoot.transform.position=new Vector3(1000,0,0);
        var sprite=Sprite.Create(Texture2D.whiteTexture,new Rect(0,0,2,2),new Vector2(.5f,.5f),.2f);
        var renderer=testRoot.AddComponent<SpriteRenderer>();renderer.sprite=sprite;
        var building=testRoot.AddComponent<BuildingOcclusionPresenter>();var prop=testRoot.AddComponent<PropDepthPresenter>();
        Set(building,"buildingRenderer",renderer);Set(building,"unitRegistry",TacticalUnitRegistry.Instance);Set(building,"presentationRegistry",ActorPresentationRegistry.Instance);
        Set(building,"occlusionPolygon",new[]{new Vector2(-5,-5),new Vector2(5,-5),new Vector2(5,5),new Vector2(-5,5)});
        Set(building,"frontBoundary",new[]{new Vector2(-5,-2),new Vector2(5,-2)});
        Set(prop,"propRenderer",renderer);Set(prop,"unitRegistry",TacticalUnitRegistry.Instance);Set(prop,"presentationRegistry",ActorPresentationRegistry.Instance);Set(prop,"groundLocalY",-2f);
        try
        {
            foreach(var b in buildings)b.enabled=false;foreach(var p in props)p.enabled=false;
            testRoot.SetActive(true);building.enabled=false;prop.enabled=false;
            foreach(var bridge in actors)
            {
                var c=bridge.context;var visual=c.visual;var source=c.sourceRenderer;var animator=visual.Animator;
                var position=visual.transform.position;var state=animator.GetCurrentAnimatorStateInfo(0);var color=source.color;float alpha=visual.VisionAlpha;
                int order=source.sortingOrder,layer=source.sortingLayerID;
                try
                {
                    visual.SetVisionAlpha(1);source.color=Color.white;renderer.sortingLayerID=layer;renderer.sortingOrder=order+100;
                    foreach(string name in new[]{"Idle","LowCoverIdle","WallCoverIdle"})
                    {
                        animator.Play(name,0,0);animator.Update(0);late.Invoke(bridge,null);
                        var mesh=new[]{c.idle,c.lowCover,c.wallCover}.Single(r=>r.gameObject.activeSelf).meshRenderer;
                        Check(!source.enabled&&visual.HasActivePresentationRenderer,"원본 숨김 중 표시 인식",report);
                        Check((visual.PresentationBounds.center-mesh.bounds.center).sqrMagnitude<1e-8f,"실제 메시 범위 사용",report);
                        visual.transform.position+=testRoot.transform.TransformPoint(new Vector3(0,-2.25f,0))-visual.GroundWorldPosition;late.Invoke(bridge,null);
                        building.enabled=true;Check(!(bool)occluded.Invoke(building,null),"건물 앞 가림 제외",report);late.Invoke(bridge,null);
                        Check(mesh.sortingOrder>renderer.sortingOrder,"호흡 메시 건물 앞 정렬",report);building.enabled=false;
                        visual.transform.position+=testRoot.transform.TransformPoint(new Vector3(0,-1.5f,0))-visual.GroundWorldPosition;late.Invoke(bridge,null);
                        building.enabled=true;Check((bool)occluded.Invoke(building,null),"호흡 메시 건물 뒤 가림 인식",report);building.enabled=false;
                        prop.enabled=true;
                        foreach(bool front in new[]{true,false})
                        {
                            visual.transform.position+=testRoot.transform.TransformPoint(new Vector3(0,front?-2.25f:-1.5f,0))-visual.GroundWorldPosition;late.Invoke(bridge,null);prop.RefreshDepth();late.Invoke(bridge,null);
                            Check(front?mesh.sortingOrder>renderer.sortingOrder:mesh.sortingOrder<renderer.sortingOrder,"호흡 메시 소품 "+(front?"앞":"뒤")+" 정렬",report);
                        }
                        renderer.sortingOrder+=1000;visual.SetBuildingFrontSorting(renderer,true);prop.RefreshDepth();late.Invoke(bridge,null);
                        Check(mesh.sortingOrder<renderer.sortingOrder,"소품 뒤 제약이 건물 앞 요청보다 우선",report);
                        visual.BeginVisionSortingOverride(renderer.sortingLayerName,renderer.sortingOrder+2000);prop.RefreshDepth();late.Invoke(bridge,null);
                        Check(mesh.sortingOrder==renderer.sortingOrder+2000,"안개 위 공격 표시 최우선",report);
                        visual.EndVisionSortingOverride();late.Invoke(bridge,null);Check(mesh.sortingOrder<renderer.sortingOrder,"안개 해제 뒤 소품 정렬 복원",report);
                        visual.SetBuildingFrontSorting(renderer,false);prop.enabled=false;renderer.sortingOrder=order+100;
                        visual.SetVisionAlpha(0);late.Invoke(bridge,null);building.enabled=true;
                        Check(!(bool)occluded.Invoke(building,null),"시야 밖 호흡 인물이 건물을 공개하지 않음",report);building.enabled=false;visual.SetVisionAlpha(1);source.color=Color.white;
                        bridge.enabled=false;Check(source.enabled&&visual.HasActivePresentationRenderer,"메시 종료 시 원본 표시 복원",report);
                        source.enabled=false;Check(!visual.HasActivePresentationRenderer,"실제 숨긴 스프라이트는 표시로 판정하지 않음",report);source.enabled=true;bridge.enabled=true;late.Invoke(bridge,null);
                        report.Add(visual.name+" / "+c.idle.profile.displayName+" / "+name+" 통과");
                    }
                }
                finally
                {
                    building.enabled=false;prop.enabled=false;visual.EndVisionSortingOverride();visual.SetBuildingFrontSorting(renderer,false);visual.SetPropDepthSorting(renderer,null);
                    visual.transform.position=position;visual.SetVisionAlpha(alpha);source.color=color;source.sortingLayerID=layer;source.sortingOrder=order;bridge.enabled=true;
                    animator.Play(state.fullPathHash,0,state.normalizedTime);animator.Update(0);late.Invoke(bridge,null);
                }
            }
            Directory.CreateDirectory("Captures/StandingDepthFix");File.WriteAllLines("Captures/StandingDepthFix/"+UnityEngine.SceneManagement.SceneManager.GetActiveScene().name+".txt",report);
            Debug.Log("호흡 메시 건물·소품 정렬/가림·안개 우선순위·원본 복원 검증 통과: "+report.Count+"항목");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(testRoot);UnityEngine.Object.DestroyImmediate(sprite);
            for(int i=0;i<buildings.Length;i++)buildings[i].enabled=buildingEnabled[i];for(int i=0;i<props.Length;i++)props[i].enabled=propEnabled[i];
        }
    }

    /// <summary>검증용 오브젝트의 필수 참조와 경계를 활성화 전에 명시적으로 지정한다.</summary>
    private static void Set(object target,string field,object value){target.GetType().GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(target,value);}

    /// <summary>기존 정책을 만족하는지 확인하고 실패 지점을 기록한다.</summary>
    private static void Check(bool condition,string label,List<string> report){if(!condition)throw new Exception("호흡 정렬 검사 실패: "+label);report.Add(label+" 통과");}
}
