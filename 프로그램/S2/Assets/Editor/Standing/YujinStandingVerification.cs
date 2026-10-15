using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>실제 유진 시각 오브젝트로 동작 전환을 검사하고 세 자세를 렌더한다.</summary>
public static class YujinStandingVerification
{
    /// <summary>전투 판정에 관여하지 않는 복제로 기존 모든 유진 동작의 표시 전환을 검사한다.</summary>
    [MenuItem("Tools/S2/유진 실제 동작 전환 검증")]
    public static void Verify()
    {
        if(!EditorApplication.isPlaying){Debug.LogError("전투 씬을 재생한 뒤 유진 표시를 검증하십시오.");return;}
        var originals=UnityEngine.Object.FindObjectsByType<StandingPoseAnimator>(FindObjectsInactive.Exclude,FindObjectsSortMode.None).Where(a=>YujinStandingInstaller.IsYujin(a.context.visual)).ToArray();
        if(originals.Length==0)throw new Exception("실제 전투 씬에 유진 대기 표시 연결이 없습니다.");
        var lines=new List<string>();var clones=new List<GameObject>();
        var late=typeof(StandingPoseAnimator).GetMethod("LateUpdate",BindingFlags.NonPublic|BindingFlags.Instance);
        try
        {
            foreach(var original in originals)
            {
                var clone=UnityEngine.Object.Instantiate(original.gameObject,new Vector3(-1000,-1000,0),Quaternion.identity);clones.Add(clone);
                var bridge=clone.GetComponent<StandingPoseAnimator>();var c=bridge.context;var renderer=c.sourceRenderer;var animator=c.visual.Animator;
                renderer.enabled=true;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;foreach(var r in new[]{c.idle,c.lowCover,c.wallCover})r.gameObject.SetActive(false);
                foreach(string state in new[]{"Idle","LowCoverIdle","WallCoverIdle","Move","MeleeWithSword","MeleeUnarmed","PlayerGun","SwordThrow","Hit","Dodge","Death"})
                {
                    if(!animator.HasState(0,Animator.StringToHash(state)))throw new Exception("유진 기존 동작 상태 누락: "+state);
                    animator.Play(state,0,0);animator.Update(0);late.Invoke(bridge,null);bool idle=state.EndsWith("Idle");
                    var active=new[]{c.idle,c.lowCover,c.wallCover}.Where(r=>r.gameObject.activeSelf).ToArray();
                    if(idle?(active.Length!=1||renderer.enabled):(active.Length!=0||!renderer.enabled))throw new Exception("유진 동작 전환 오류: "+state);
                    if(idle)
                    {
                        var rig=active[0];if(renderer.sprite!=rig.profile.sprite)throw new Exception("유진 현재 자세와 원화가 일치하지 않습니다.");
                        renderer.flipX=true;renderer.color=new Color(.6f,.7f,.8f,.25f);renderer.sortingOrder=127;late.Invoke(bridge,null);
                        var block=new MaterialPropertyBlock();rig.meshRenderer.GetPropertyBlock(block);
                        if(rig.transform.localScale.x!=-1||rig.meshRenderer.sortingOrder!=127||Vector4.Distance(block.GetColor("_Color"),renderer.color)>1e-5f)throw new Exception("유진 방향·정렬·시야 표시 전달 실패");
                        renderer.flipX=false;renderer.color=Color.white;
                    }
                    lines.Add(state+": "+(idle?"호흡 메시 표시":"기존 동작 원화 표시")+" 정상");
                }
            }
            Directory.CreateDirectory("Captures/YujinStanding");File.WriteAllLines("Captures/YujinStanding/"+UnityEngine.SceneManagement.SceneManager.GetActiveScene().name+"_runtime.txt",lines);
            Debug.Log("유진 실제 "+lines.Count+"상태 동작 전환·방향·정렬·시야 알파 검증 통과");
        }
        finally{foreach(var clone in clones)UnityEngine.Object.DestroyImmediate(clone);}
    }

    /// <summary>S2 실제 표시의 유진 세 자세를 같은 구도에서 38프레임 캡처한다.</summary>
    [MenuItem("Tools/S2/유진 적용 애니메이션 캡처")]
    public static void Capture()
    {
        if(!EditorApplication.isPlaying||Camera.main==null){Debug.LogError("전투 씬을 재생한 뒤 유진을 캡처하십시오.");return;}
        var original=UnityEngine.Object.FindObjectsByType<StandingPoseAnimator>(FindObjectsInactive.Exclude,FindObjectsSortMode.None).FirstOrDefault(a=>YujinStandingInstaller.IsYujin(a.context.visual));
        if(original==null)throw new Exception("캡처할 유진 대기 표시가 없습니다.");
        var clones=new List<GameObject>();var rigs=new List<StandingPoseMesh>();var late=typeof(StandingPoseAnimator).GetMethod("LateUpdate",BindingFlags.NonPublic|BindingFlags.Instance);
        var cameraObject=new GameObject("유진 대기 확인 카메라");var camera=cameraObject.AddComponent<Camera>();camera.CopyFrom(Camera.main);
        var data=cameraObject.AddComponent<UniversalAdditionalCameraData>();data.renderType=CameraRenderType.Base;data.SetRenderer(-1);data.renderPostProcessing=false;
        camera.transform.SetPositionAndRotation(new Vector3(-1000,0,-10),Quaternion.identity);camera.orthographic=true;camera.orthographicSize=2.5f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.1f,.13f,.16f);camera.cullingMask=1<<31;
        var rt=new RenderTexture(1440,600,24);rt.Create();camera.targetTexture=rt;camera.aspect=2.4f;
        var image=new Texture2D(1440,600,TextureFormat.RGB24,false);var oldActive=RenderTexture.active;Directory.CreateDirectory("Captures/YujinStanding/Preview");
        try
        {
            var states=new[]{"Idle","LowCoverIdle","WallCoverIdle"};
            for(int i=0;i<3;i++)
            {
                var clone=UnityEngine.Object.Instantiate(original.gameObject,new Vector3(-1000+(i-1)*4,0,0),Quaternion.identity);clones.Add(clone);clone.transform.localScale=Vector3.one;
                foreach(var child in clone.GetComponentsInChildren<Transform>(true))child.gameObject.layer=31;
                var bridge=clone.GetComponent<StandingPoseAnimator>();var c=bridge.context;c.sourceRenderer.enabled=true;foreach(var r in new[]{c.idle,c.lowCover,c.wallCover})r.gameObject.SetActive(false);
                c.visual.Animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;c.visual.Animator.Play(states[i],0,0);c.visual.Animator.Update(0);c.sourceRenderer.color=Color.white;c.sourceRenderer.flipX=false;late.Invoke(bridge,null);
                // 자세 확인용 캡처에는 별도 무기 연출·보조 표시 렌더러를 포함하지 않는다.
                foreach(var renderer in clone.GetComponentsInChildren<Renderer>(true))
                    if(renderer!=c.sourceRenderer&&renderer!=c.idle.meshRenderer&&renderer!=c.lowCover.meshRenderer&&renderer!=c.wallCover.meshRenderer)renderer.enabled=false;
                rigs.Add(new[]{c.idle,c.lowCover,c.wallCover}.Single(r=>r.gameObject.activeSelf));
            }
            for(int frame=0;frame<38;frame++)
            {
                foreach(var r in rigs)r.Evaluate(frame*.2f);RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1440,600),0,0);image.Apply();File.WriteAllBytes("Captures/YujinStanding/Preview/frame_"+frame.ToString("D3")+".png",image.EncodeToPNG());
            }
            Debug.Log("S2 유진 세 자세 적용 미리보기 38프레임 캡처 완료");
        }
        finally{RenderTexture.active=oldActive;camera.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(cameraObject);foreach(var clone in clones)UnityEngine.Object.DestroyImmediate(clone);}
    }
}
