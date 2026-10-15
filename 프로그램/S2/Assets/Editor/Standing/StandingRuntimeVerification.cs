using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>실제 전투 씬의 복제 시각 오브젝트로 동작 전환과 표시 속성을 검사한다.</summary>
public static class StandingRuntimeVerification
{
    /// <summary>S2에 연결된 실제 시각 오브젝트로 9자세의 호흡 미리보기를 렌더한다.</summary>
    [MenuItem("Tools/S2/확정 적 대기 미리보기 캡처")]
    public static void Capture()
    {
        if(!EditorApplication.isPlaying||Camera.main==null){Debug.LogError("전투 씬을 재생한 뒤 캡처하십시오.");return;}
        var originals=UnityEngine.Object.FindObjectsByType<StandingPoseAnimator>(FindObjectsInactive.Exclude,FindObjectsSortMode.None).Where(a=>!YujinStandingInstaller.IsYujin(a.context.visual)).ToArray();
        var rigs=new List<StandingPoseMesh>();var clones=new List<GameObject>();
        var late=typeof(StandingPoseAnimator).GetMethod("LateUpdate",BindingFlags.NonPublic|BindingFlags.Instance);
        var cameraObject=new GameObject("확정 자세 캡처 카메라");var camera=cameraObject.AddComponent<Camera>();camera.CopyFrom(Camera.main);
        var cameraData=cameraObject.AddComponent<UniversalAdditionalCameraData>();cameraData.renderType=CameraRenderType.Base;cameraData.SetRenderer(-1);cameraData.renderPostProcessing=false;
        camera.transform.position=new Vector3(-1000,1.3f,-10);camera.transform.rotation=Quaternion.identity;camera.orthographic=true;camera.orthographicSize=5.7f;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.1f,.13f,.16f);camera.cullingMask=1<<31;
        var rt=new RenderTexture(1440,1080,24);rt.Create();camera.targetTexture=rt;camera.aspect=4f/3;
        var image=new Texture2D(1440,1080,TextureFormat.RGB24,false);var oldActive=RenderTexture.active;
        string folder="Captures/Standing_v025/Preview";Directory.CreateDirectory(folder);
        try
        {
            var ids=new[]{"Hammer","SmallNail","BigNail"};var states=new[]{"Idle","LowCoverIdle","WallCoverIdle"};
            for(int actor=0;actor<3;actor++)for(int pose=0;pose<3;pose++)
            {
                var original=originals.Single(o=>o.context.idle.profile.name==ids[actor]+"_Idle");
                var clone=UnityEngine.Object.Instantiate(original.gameObject,new Vector3(-1000+(actor-1)*4.5f,1.2f+(1-pose)*3.6f,0),Quaternion.identity);clones.Add(clone);clone.transform.localScale=Vector3.one;
                // 씬의 넓은 시야 가림막과 분리된 임시 캡처 레이어를 쓴다.
                foreach(var child in clone.GetComponentsInChildren<Transform>(true))child.gameObject.layer=31;
                var bridge=clone.GetComponent<StandingPoseAnimator>();var c=bridge.context;c.sourceRenderer.enabled=true;c.sourceRenderer.color=Color.white;
                foreach(var rig in new[]{c.idle,c.lowCover,c.wallCover})rig.gameObject.SetActive(false);
                c.visual.Animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;c.visual.Animator.Play(states[pose],0,0);c.visual.Animator.Update(0);c.sourceRenderer.color=Color.white;c.sourceRenderer.flipX=false;late.Invoke(bridge,null);
                var active=new[]{c.idle,c.lowCover,c.wallCover}.Single(r=>r.gameObject.activeSelf);rigs.Add(active);
            }
            for(int frame=0;frame<38;frame++)
            {
                foreach(var rig in rigs)rig.Evaluate(frame*.2f);RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1440,1080),0,0);image.Apply();File.WriteAllBytes(folder+"/frame_"+frame.ToString("D3")+".png",image.EncodeToPNG());
            }
            Debug.Log("S2 실제 적 표시의 9자세 미리보기 38프레임 캡처 완료");
        }
        finally
        {
            RenderTexture.active=oldActive;camera.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(cameraObject);foreach(var clone in clones)UnityEngine.Object.DestroyImmediate(clone);
        }
    }

    /// <summary>전투 로직을 건드리지 않고 모든 적의 기존 Animator와 새 대기 표시를 검증한다.</summary>
    [MenuItem("Tools/S2/실제 적 동작 전환과 대기 표시 검증")]
    public static void Verify()
    {
        if(!EditorApplication.isPlaying){Debug.LogError("전투 씬을 재생한 뒤 표시 검증을 실행하십시오.");return;}
        var originals=UnityEngine.Object.FindObjectsByType<StandingPoseAnimator>(FindObjectsInactive.Exclude,FindObjectsSortMode.None).Where(a=>!YujinStandingInstaller.IsYujin(a.context.visual)).ToArray();
        var report=new List<string>();var clones=new List<GameObject>();
        var late=typeof(StandingPoseAnimator).GetMethod("LateUpdate",BindingFlags.NonPublic|BindingFlags.Instance);
        try
        {
            foreach(var original in originals)
            {
                var clone=UnityEngine.Object.Instantiate(original.gameObject,new Vector3(-1000,-1000,0),Quaternion.identity);clones.Add(clone);
                var bridge=clone.GetComponent<StandingPoseAnimator>();var context=bridge.context;var renderer=context.sourceRenderer;var animator=context.visual.Animator;
                animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;renderer.enabled=true;
                foreach(var rig in new[]{context.idle,context.lowCover,context.wallCover})rig.gameObject.SetActive(false);
                foreach(string state in new[]{"Idle","LowCoverIdle","WallCoverIdle","Move","MeleeWithSword","EnemyRanged","Hit","Dodge","Death"})
                {
                    if(!animator.HasState(0,Animator.StringToHash(state)))continue;
                    animator.Play(state,0,0);animator.Update(0);late.Invoke(bridge,null);
                    bool idle=state=="Idle"||state=="LowCoverIdle"||state=="WallCoverIdle";
                    var active=new[]{context.idle,context.lowCover,context.wallCover}.Where(r=>r.gameObject.activeSelf).ToArray();
                    if(idle?(active.Length!=1||renderer.enabled):(active.Length!=0||!renderer.enabled))throw new Exception("적 동작 표시 전환 실패: "+state+" / 메시="+active.Length+" / 원화="+renderer.enabled+" / 표시="+context.visual.enabled+" / 스프라이트="+renderer.sprite?.name);
                    if(idle)
                    {
                        var rig=active[0];if(renderer.sprite!=rig.profile.sprite)throw new Exception("대기 원화 불일치");
                        renderer.flipX=true;renderer.color=new Color(.7f,.8f,.9f,.25f);renderer.sortingOrder=123;late.Invoke(bridge,null);
                        var block=new MaterialPropertyBlock();rig.meshRenderer.GetPropertyBlock(block);
                        if(rig.transform.localScale.x!=-1||rig.meshRenderer.sortingOrder!=123||Vector4.Distance(block.GetColor("_Color"),renderer.color)>1e-5f)throw new Exception("대기 방향·정렬·알파 전달 실패");
                        renderer.flipX=false;renderer.color=Color.white;
                    }
                    report.Add(context.idle.profile.displayName+" / "+state+": "+(idle?"호흡 메시 표시":"기존 단일 원화 표시")+" 정상");
                }
            }
            if(originals.Length!=3)throw new Exception("전투 씬의 적 세 종류가 모두 연결됐는지 확인하십시오.");
            Directory.CreateDirectory("Captures/Standing_v025");File.WriteAllLines("Captures/Standing_v025/runtime.txt",report);
            Debug.Log("적 3종의 동작 전환·엄폐 호흡·방향·정렬·시야 알파 검사 통과 ("+report.Count+" 상태)");
        }
        finally{foreach(var clone in clones)UnityEngine.Object.DestroyImmediate(clone);}
    }
}
