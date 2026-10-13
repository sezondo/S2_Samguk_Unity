using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>후일담의 실제 컷 공개·표시등 교체·대사·완료·스킵을 저장 변경 없이 검사한다.</summary>
[InitializeOnLoad]
public static class Stage11EpilogueVerification
{
    // 재로드를 넘어 검사 요청을 보존하는 키와 결과 경로다.
    private const string Pending = "S2.Stage11Epilogue.Verify";
    private const string Folder = "Temp/Stage11Epilogue";
    // 프레임별 검사 루틴·기한·결과와 완료 통지 횟수다.
    private static IEnumerator routine;
    private static double deadline;
    private static readonly List<string> results = new();
    private static int completions;
    private static StoryCompletionReason reason;

    /// <summary>검사 요청이 있을 때만 플레이 진입 뒤 검사를 시작한다.</summary>
    static Stage11EpilogueVerification() => EditorApplication.playModeStateChanged += OnMode;

    /// <summary>저장된 후일담 단독 씬에서 실제 재생 검사를 실행한다.</summary>
    [MenuItem("Tools/S2/Verify 1-1 Epilogue")]
    public static void Verify()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().path != Stage11EpilogueAuthoring.Preview || SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("저장된 StoryEpilogueTest 편집 씬에서 실행해야 합니다.");
        Directory.CreateDirectory(Folder); File.WriteAllText(Folder+"/verification.txt","RUNNING");
        SessionState.SetBool(Pending,true); EditorApplication.isPlaying = true;
    }

    /// <summary>플레이 모드가 준비되면 검사 루틴과 제한 시간을 설정한다.</summary>
    private static void OnMode(PlayModeStateChange mode)
    {
        if (mode != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending,false)) return;
        SessionState.SetBool(Pending,false); results.Clear(); completions=0;
        deadline=EditorApplication.timeSinceStartup+60; routine=Run(); EditorApplication.update+=Tick;
    }

    /// <summary>실제 프레임을 진행하며 검사 실패와 완료를 파일에 기록한다.</summary>
    private static void Tick()
    {
        EditorApplication.QueuePlayerLoopUpdate();
        try {
            if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup>deadline) throw new InvalidOperationException("후일담 검사 제한 시간 초과 또는 재생 중단");
            if (!routine.MoveNext()) Finish(null);
        } catch (Exception exception) { Finish(exception); }
    }

    /// <summary>결과를 보존하고 검사 플레이의 변경은 저장하지 않는다.</summary>
    private static void Finish(Exception exception)
    {
        EditorApplication.update-=Tick; (routine as IDisposable)?.Dispose(); routine=null;
        File.WriteAllText(Folder+"/verification.txt",(exception==null?"PASS":"FAIL: "+exception)+"\n"+string.Join("\n",results));
        if(exception==null) Debug.Log($"1-1 후일담 검증 {results.Count}개 통과");
        else Debug.LogError("1-1 후일담 검증 실패: "+exception);
        EditorApplication.isPlaying=false;
    }

    /// <summary>조건 실패를 해당 검사 이름으로 명확히 보고한다.</summary>
    private static void Check(bool condition,string label)
    {
        if(!condition) throw new InvalidOperationException(label); results.Add(label);
    }

    /// <summary>검사용으로 실제 연결된 Presenter의 UI 참조를 읽는다.</summary>
    private static T Read<T>(object target,string field) => (T)target.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(target);

    /// <summary>원고 대사와 네 컷을 끝까지 실행하고 교체·완료·재시작·스킵을 검사한다.</summary>
    private static IEnumerator Run()
    {
        Application.runInBackground=true; yield return null;
        var context=UnityEngine.Object.FindFirstObjectByType<StoryContext>();
        Check(context!=null && context.HasValidReference(),"후일담 Context 연결");
        var runner=context.Runner;
        var sequence=AssetDatabase.LoadAssetAtPath<StorySequenceData>(Stage11EpilogueAuthoring.Sequence);
        Check(CampaignBootstrap.Instance==null,"캠페인 저장을 수정하지 않는 단독 검사");
        Check(runner.ActiveSequence==sequence && runner.HasValidData(sequence),"후일담 단독 자동 시작과 데이터 유효성");
        var stage=AssetDatabase.LoadAssetAtPath<StageDefinitionData>("Assets/Data/Campaign/Stage_1-1.asset");
        Check(stage.HasPostBattleStory && stage.PostBattleStory==sequence,"기존 캠페인 후일담 연결 유지");
        Check((int)StoryCommandType.ShowComicCut==11 && (int)StoryCommandType.ReplaceComicPage==12,"기존 명령 번호 보존");
        var background=Read<Image>(context.BackgroundPresenter,"backgroundImage");
        var body=Read<TMP_Text>(context.DialoguePresenter,"dialogueBodyText");
        var speaker=Read<TMP_Text>(context.DialoguePresenter,"speakerNameText");
        var group=Read<CanvasGroup>(context.DialoguePresenter,"dialogueGroup");
        var fade=Read<CanvasGroup>(context.FadePresenter,"fadeGroup");
        var captures=new HashSet<string>();
        var transitions=new HashSet<string>();
        var cutCommands=sequence.Commands.Where(c=>c.CommandType==StoryCommandType.ShowComicCut).ToArray();
        Check(cutCommands.Length==4,"컷 네 개만 공개");
        Check(sequence.Commands.Count(c=>c.CommandType==StoryCommandType.ReplaceComicPage)==3,"표시등 세 번 교체");
        Check(sequence.Commands.Count(c=>c.HideSpeakerName)==5,"독백 다섯 개 화자 숨김");
        VerifyLampPixels();
        runner.StoryCompleted+=(_,completedReason)=>{completions++;reason=completedReason;};
        int dialogues=0;
        while(runner.IsPlaying)
        {
            int index=runner.CurrentCommandIndex;
            var command=sequence.Commands[index];
            if(runner.PlaybackState==StoryPlaybackState.WaitingForTimedCommand)
            {
                if(command.CommandType==StoryCommandType.Wait && context.BackgroundPresenter.RevealedCutCount==4 && captures.Add("market-door"))
                {
                    double until=EditorApplication.timeSinceStartup+.15; while(EditorApplication.timeSinceStartup<until)yield return null;
                    ScreenCapture.CaptureScreenshot(Folder+"/market-door.png");
                    until=EditorApplication.timeSinceStartup+.15; while(EditorApplication.timeSinceStartup<until)yield return null;
                    Check(group.alpha==0 && background.sprite.triangles.Length==24,"문틈 공개 뒤 무대사 여운");
                }
                runner.RequestAdvance(); Check(runner.CurrentCommandIndex==index,"연출 중 입력 무시 "+index);
                if(command.CommandType==StoryCommandType.ShowComicCut && context.BackgroundPresenter.CurrentRevealAlpha>0 && context.BackgroundPresenter.CurrentRevealAlpha<1)
                    transitions.Add(command.ComicCutId);
                yield return null; continue;
            }
            Check(runner.PlaybackState==StoryPlaybackState.WaitingForDialogueInput,"대사 입력 대기 "+index);
            Check(body.text==command.DialogueText && speaker.enabled==!command.HideSpeakerName && speaker.text==(command.HideSpeakerName?"":command.SpeakerName),"원고 본문·화자 표시 "+index);
            Check(group.alpha==1 && fade.alpha==0,"대사 표시와 화면 페이드 해제 "+index);
            int cutCount=sequence.Commands.Take(index+1).Count(c=>c.CommandType==StoryCommandType.ShowComicCut);
            Check(context.BackgroundPresenter.RevealedCutCount==cutCount,"교체 뒤에도 공개 컷 수 유지 "+index);
            if(cutCount>0)
            {
                var visual=sequence.Commands.Take(index+1).Last(c=>c.CommandType==StoryCommandType.ShowComicCut || c.CommandType==StoryCommandType.ReplaceComicPage).VisualSprite;
                Check(background.sprite.texture==visual.texture && background.sprite.rect==visual.rect,"현재 표시등 단계 그림 적용 "+index);
                Check(background.sprite.vertices.Length==4+4*cutCount && background.sprite.triangles.Length==6*cutCount,"누적 메시 유지·미공개 컷 삼각형 없음 "+index);
                Check(background.useSpriteMesh && background.rectTransform.anchoredPosition==Vector2.zero && background.color.a==1,"교체 전후 위치와 밝기 유지 "+index);
                for(int cut=0;cut<4;cut++)
                {
                    var polygon=cutCommands[cut].ComicCutVertices;
                    Vector2 sample=polygon.Aggregate(Vector2.zero,(sum,p)=>sum+p)/polygon.Length;
                    sample=new Vector2((sample.x-.5f)*visual.rect.width/visual.pixelsPerUnit,(.5f-sample.y)*visual.rect.height/visual.pixelsPerUnit);
                    Check(MeshContains(background.sprite,sample)==(cut<cutCount),"컷 공개·검은 가림 유지 "+cut+" / "+index);
                }
            }
            if(!context.DialoguePresenter.IsTextComplete)
            {
                runner.RequestAdvance(); Check(runner.CurrentCommandIndex==index && context.DialoguePresenter.IsTextComplete,"첫 입력 타이핑 완성 "+index);
            }
            body.ForceMeshUpdate(); Check(!body.isTextOverflowing,"본문 영역 넘침 없음 "+index);
            string capture=cutCount==0?"opening-black":cutCount==1?"interior":cutCount==2?"panorama":background.sprite.texture.name;
            if(captures.Add(capture))
            {
                double until=EditorApplication.timeSinceStartup+.15; while(EditorApplication.timeSinceStartup<until)yield return null;
                ScreenCapture.CaptureScreenshot(Folder+"/"+capture+".png");
                until=EditorApplication.timeSinceStartup+.15; while(EditorApplication.timeSinceStartup<until)yield return null;
            }
            dialogues++; runner.RequestAdvance(); yield return null;
        }
        Check(dialogues==sequence.Commands.Count(c=>c.CommandType==StoryCommandType.Dialogue),"원고 대사 전체 재생 "+dialogues);
        Check(transitions.Count==4,"네 컷의 중간 페이드 확인");
        Check(completions==1 && reason==StoryCompletionReason.Completed && group.alpha==0 && fade.alpha==1,"정상 완료 한 번·검은 종료 화면");
        runner.RequestAdvance(); Check(completions==1,"완료 후 중복 통지 없음");
        Check(runner.Play(sequence,true),"완료 뒤 재시작");
        while(context.BackgroundPresenter.RevealedCutCount<3 || background.sprite.texture.name!="Stage11_Epilogue_80")
        {
            if(runner.PlaybackState==StoryPlaybackState.WaitingForDialogueInput) runner.RequestAdvance();
            yield return null;
        }
        runner.RequestSkip(); yield return null;
        Check(!runner.IsPlaying && completions==2 && reason==StoryCompletionReason.Skipped,"페이지 교체 후 스킵 한 번");
        Check(!context.BackgroundPresenter.IsShowingComicCut && !context.BackgroundPresenter.IsCutTransitioning && !background.useSpriteMesh,"스킵 자원·메시 복원");
        Check(runner.Play(sequence,true),"페이지 교체 진행 중 스킵 검사 시작");
        while(sequence.Commands[runner.CurrentCommandIndex].CommandType!=StoryCommandType.ReplaceComicPage)
        {
            if(runner.PlaybackState==StoryPlaybackState.WaitingForDialogueInput)runner.RequestAdvance();
            if(sequence.Commands[runner.CurrentCommandIndex].CommandType==StoryCommandType.ReplaceComicPage)break;
            yield return null;
        }
        Check(context.BackgroundPresenter.IsCutTransitioning,"페이지 교체의 다음 프레임 대기");
        runner.RequestSkip(); yield return null;
        Check(!runner.IsPlaying && completions==3 && !context.BackgroundPresenter.IsShowingComicCut && !context.BackgroundPresenter.IsCutTransitioning,"교체 대기 중 스킵과 지연 콜백 취소");
        Check(runner.Play(sequence,false),"스킵 뒤 재시작"); runner.RequestSkip();
        Check(runner.IsPlaying && !runner.CanSkip && completions==3,"비허용 스킵 무시");
    }

    /// <summary>실제 메시 삼각형에 점이 포함되는지 확인한다.</summary>
    private static bool MeshContains(Sprite sprite,Vector2 point)
    {
        var vertices=sprite.vertices; var triangles=sprite.triangles;
        for(int i=0;i<triangles.Length;i+=3)
        {
            Vector2 a=vertices[triangles[i]],b=vertices[triangles[i+1]],c=vertices[triangles[i+2]];
            float ab=(b.x-a.x)*(point.y-a.y)-(b.y-a.y)*(point.x-a.x);
            float bc=(c.x-b.x)*(point.y-b.y)-(c.y-b.y)*(point.x-b.x);
            float ca=(a.x-c.x)*(point.y-c.y)-(a.y-c.y)*(point.x-c.x);
            if((ab>=0&&bc>=0&&ca>=0)||(ab<=0&&bc<=0&&ca<=0))return true;
        }
        return false;
    }

    /// <summary>상태별 램프 수와 표시등 이외 픽셀의 완전한 보존을 검사한다.</summary>
    private static void VerifyLampPixels()
    {
        Color32[] baseline=null;
        for(int state=70;state<=100;state+=10)
        {
            var texture=new Texture2D(2,2);
            texture.LoadImage(File.ReadAllBytes($"{Stage11EpilogueAuthoring.Art}/Stage11_Epilogue_{state}.png"));
            var pixels=texture.GetPixels32();
            if(baseline==null)baseline=pixels;
            Check(texture.width==1672 && texture.height==941,"페이지 규격 "+state);
            for(int lamp=0;lamp<10;lamp++)
            {
                Color32 color=pixels[(941-1-746)*1672+127+56*lamp];
                Check((color.r>210 && color.g>110)==(lamp<state/10),"램프 개수·점등 순서 "+state+" / "+lamp);
            }
            bool preserved=true;
            for(int i=0;i<pixels.Length;i++)
            {
                int x=i%1672,top=940-i/1672;
                if(x>=497 && x<=653 && top>=724 && top<=768)continue;
                if(!pixels[i].Equals(baseline[i])){preserved=false;break;}
            }
            Check(preserved,"표시등 외 원화 픽셀 완전 보존 "+state);
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
