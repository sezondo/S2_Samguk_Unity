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

/// <summary>StoryTest에서 실제 1-1 재생·대화 표시·완료·스킵을 검사한다. 씬과 저장 파일을 수정하지 않는다.</summary>
[InitializeOnLoad]
public static class Stage11OpeningVerification
{
    // 도메인 재로드를 넘어 검증 요청을 보존하는 키와 결과 폴더다.
    private const string Pending = "S2.Stage11Opening.Verify";
    private const string Folder = "Temp/Stage11Opening";
    // 프레임마다 진행할 검사 루틴과 제한 시각이다.
    private static IEnumerator routine;
    private static double deadline;
    // 실제 런타임 검사 결과와 완료 이벤트를 누적한다.
    private static readonly List<string> results = new();
    private static int completionCount;
    private static StoryCompletionReason lastReason;

    /// <summary>플레이 모드 진입 후 검사를 연결한다.</summary>
    static Stage11OpeningVerification() => EditorApplication.playModeStateChanged += OnMode;

    /// <summary>저장된 StoryTest 씬만 재생해 1-1 시작 스토리를 검사한다.</summary>
    [MenuItem("Tools/S2/Verify 1-1 Opening Story")]
    public static void Verify()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().name != "StoryTest" || SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("저장된 StoryTest 편집 상태에서 실행해야 합니다.");
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Folder + "/verification.txt", "RUNNING");
        SessionState.SetBool(Pending, true);
        EditorApplication.isPlaying = true;
    }

    /// <summary>재생 초기화 뒤 검사 루틴을 시작한다.</summary>
    private static void OnMode(PlayModeStateChange mode)
    {
        if (mode != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
        SessionState.SetBool(Pending, false);
        results.Clear(); completionCount = 0;
        deadline = EditorApplication.timeSinceStartup + 60;
        routine = Run();
        EditorApplication.update += Tick;
    }

    /// <summary>실제 프레임을 진행하며 실패 또는 완료를 결과 파일에 남긴다.</summary>
    private static void Tick()
    {
        EditorApplication.QueuePlayerLoopUpdate();
        try
        {
            if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup > deadline)
                throw new InvalidOperationException("검증 재생이 중단되거나 제한 시간을 초과했습니다.");
            if (!routine.MoveNext()) Finish(null);
        }
        catch (Exception exception) { Finish(exception); }
    }

    /// <summary>검사 결과를 저장하고 플레이 상태를 폐기한다.</summary>
    private static void Finish(Exception exception)
    {
        EditorApplication.update -= Tick;
        (routine as IDisposable)?.Dispose(); routine = null;
        File.WriteAllText(Folder + "/verification.txt", (exception == null ? "PASS" : "FAIL: " + exception) + "\n" + string.Join("\n", results));
        if (exception == null) Debug.Log($"1-1 시작 스토리 검증 {results.Count}개 통과");
        else Debug.LogError("1-1 시작 스토리 검증 실패: " + exception);
        EditorApplication.isPlaying = false;
    }

    /// <summary>조건이 실패하면 해당 검증 이름과 함께 중단한다.</summary>
    private static void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException(label);
        results.Add(label);
    }

    /// <summary>검증에 필요한 실제 Presenter 내부 UI 참조를 읽는다.</summary>
    private static T Read<T>(object target, string field) =>
        (T)target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

    /// <summary>실제 시퀀스 전체를 진행하며 장면·대사·타이핑·무대사 정지·완료·스킵을 확인한다.</summary>
    private static IEnumerator Run()
    {
        VerifyGeometry();
        Application.runInBackground = true;
        yield return null;
        var context = UnityEngine.Object.FindFirstObjectByType<StoryContext>();
        Check(context != null && context.HasValidReference(), "Story Context와 필수 참조");
        var runner = context.Runner;
        var sequence = AssetDatabase.LoadAssetAtPath<StorySequenceData>("Assets/Data/Story/CampaignPrototype/Stage_1-1_Pre.asset");
        var firstCut = sequence.Commands.First(c => c.CommandType == StoryCommandType.ShowComicCut);
        string detail = " / 뷰포트=" + Read<Rect>(context.BackgroundPresenter, "comicViewport") +
            " 컷=" + firstCut.ComicCutId + " Sprite=" + firstCut.VisualSprite + " 꼭짓점=" + (firstCut.ComicCutVertices == null ? -1 : firstCut.ComicCutVertices.Length) + " 시간=" + firstCut.Duration;
        if (firstCut.VisualSprite != null) detail += " Packed=" + firstCut.VisualSprite.packed + " Geometry=" + StoryComicCutGeometry.TryBuild(firstCut.ComicCutVertices, firstCut.VisualSprite.rect, firstCut.VisualSprite.pixelsPerUnit, out _, out _, out _);
        Check(runner.HasValidData(sequence), "1-1 명령 데이터와 Sprite 참조" + detail);
        Check(runner.ActiveSequence == sequence, "StoryTest 단독 실행에서 1-1 자동 시작");
        Check(CampaignBootstrap.Instance == null, "캠페인 저장 및 배틀 진입 없이 단독 검증");
        Check(sequence.Commands.Count(c => c.CommandType == StoryCommandType.Dialogue) == 92, "원고 대사 92개 포함");
        Check((int)StoryCommandType.Wait == 9 && (int)StoryCommandType.HideDialogue == 10, "기존 직렬화 명령 번호 보존");
        Check(sequence.Commands.Count(c => c.CommandType == StoryCommandType.ShowComicCut) == 10, "원고의 10개 컷 연결");
        Check(sequence.Commands.Count(c => c.CommandType == StoryCommandType.Dialogue && c.HideSpeakerName) == 35, "독백 35개 화자 숨김 설정");
        var background = Read<Image>(context.BackgroundPresenter, "backgroundImage");
        var dialogueGroup = Read<CanvasGroup>(context.DialoguePresenter, "dialogueGroup");
        var fadeGroup = Read<CanvasGroup>(context.FadePresenter, "fadeGroup");
        var comicGroup = Read<CanvasGroup>(context.ComicPanelPresenter, "comicPanelGroup");
        var body = Read<TMP_Text>(context.DialoguePresenter, "dialogueBodyText");
        var speaker = Read<TMP_Text>(context.DialoguePresenter, "speakerNameText");
        var captured = new HashSet<string>();
        int dialogueCount = 0;
        int longest = sequence.Commands.Where(c => c.CommandType == StoryCommandType.Dialogue).Max(c => c.DialogueText.Length);
        bool sawPause = false;
        var enteredCuts = new HashSet<string>();
        var fadedCuts = new HashSet<string>();
        bool capturedFade = false;
        runner.StoryCompleted += (_, reason) => { completionCount++; lastReason = reason; };
        while (runner.IsPlaying)
        {
            int index = runner.CurrentCommandIndex;
            var command = sequence.Commands[index];
            if (runner.PlaybackState == StoryPlaybackState.WaitingForTimedCommand)
            {
                if (command.CommandType == StoryCommandType.Wait && !sawPause)
                {
                    sawPause = true;
                    Check(dialogueGroup.alpha == 0f && body.text == string.Empty, "무대사 정지 중 대화창 숨김");
                    runner.RequestAdvance();
                    Check(runner.CurrentCommandIndex == index, "시간 대기를 클릭으로 건너뛰지 않음");
                }
                if (command.CommandType == StoryCommandType.ShowComicCut && enteredCuts.Add(command.ComicCutId))
                {
                    Check(context.BackgroundPresenter.IsCutTransitioning && dialogueGroup.alpha == 0f, "컷 진입 중 대화창 숨김 " + command.ComicCutId);
                    runner.RequestAdvance();
                    Check(runner.CurrentCommandIndex == index, "진입 중 진행 입력 무시 " + command.ComicCutId);
                }
                if (command.CommandType == StoryCommandType.ShowComicCut && context.BackgroundPresenter.CurrentRevealAlpha > 0f && context.BackgroundPresenter.CurrentRevealAlpha < 1f)
                {
                    if (fadedCuts.Add(command.ComicCutId))
                    {
                        Check(background.color.a == 1f && background.rectTransform.anchoredPosition == Vector2.zero, "현재 컷만 점진 공개하며 이전 페이지 위치·알파 유지 " + command.ComicCutId);
                        Check(background.material.shader.name == "S2/UI/StoryComicReveal", "컷별 페이드 셰이더 적용 " + command.ComicCutId);
                    }
                    if (!capturedFade && command.ComicCutId == "meeting-sword" && context.BackgroundPresenter.CurrentRevealAlpha > .3f && context.BackgroundPresenter.CurrentRevealAlpha < .7f)
                    {
                        capturedFade = true;
                        ScreenCapture.CaptureScreenshot(Folder + "/meeting-sword-fading.png");
                    }
                }
                yield return null;
                continue;
            }
            Check(runner.PlaybackState == StoryPlaybackState.WaitingForDialogueInput, "대사 입력 대기 " + index);
            var visualCommand = sequence.Commands.Take(index + 1).Last(c => c.CommandType == StoryCommandType.ChangeBackground || c.CommandType == StoryCommandType.ShowComicCut);
            var expectedBackground = visualCommand.VisualSprite;
            if (visualCommand.CommandType == StoryCommandType.ShowComicCut)
            {
                Check(context.BackgroundPresenter.IsShowingComicCut && !context.BackgroundPresenter.IsCutTransitioning, "현재 컷 유지 " + index);
                Check(background.sprite != expectedBackground && background.sprite.texture == expectedBackground.texture && background.useSpriteMesh, "원화 공유 및 다각형 컷 표시 " + index);
                var pageCuts = sequence.Commands.Take(index + 1).Where(c => c.CommandType == StoryCommandType.ShowComicCut && c.VisualSprite == expectedBackground).ToArray();
                int expectedVertices = 4, expectedTriangles = 0;
                foreach (var cut in pageCuts)
                {
                    Check(StoryComicCutGeometry.TryBuild(cut.ComicCutVertices, expectedBackground.rect, expectedBackground.pixelsPerUnit, out _, out var cutVertices, out var cutTriangles), "누적 컷 기하 계산 " + cut.ComicCutId);
                    expectedVertices += cutVertices.Length; expectedTriangles += cutTriangles.Length;
                }
                Check(context.BackgroundPresenter.RevealedCutCount == pageCuts.Length, "페이지별 공개 컷 수와 초기화 " + index);
                Check(background.sprite.rect == expectedBackground.rect && background.sprite.vertices.Length == expectedVertices && background.sprite.triangles.Length == expectedTriangles, "전체 페이지 크기와 누적 경계 " + index);
                Check(Vector2.Distance(background.sprite.bounds.size, expectedBackground.rect.size / expectedBackground.pixelsPerUnit) < .001f, "공개 수와 무관한 고정 페이지 크기 " + index);
                foreach (var cut in sequence.Commands.Where(c => c.CommandType == StoryCommandType.ShowComicCut && c.VisualSprite == expectedBackground))
                {
                    StoryComicCutGeometry.TryBuild(cut.ComicCutVertices, expectedBackground.rect, expectedBackground.pixelsPerUnit, out var cutRect, out var cutVertices, out var cutTriangles);
                    Vector2 sample = (cutVertices[cutTriangles[0]] + cutVertices[cutTriangles[1]] + cutVertices[cutTriangles[2]]) / 3f;
                    sample += (cutRect.center - expectedBackground.rect.center) / expectedBackground.pixelsPerUnit;
                    Check(MeshContains(background.sprite, sample) == pageCuts.Contains(cut), "공개 컷 유지 및 미공개 컷 가림 " + cut.ComicCutId + " / " + index);
                }
                Check(background.color.a == 1f && context.BackgroundPresenter.CurrentRevealAlpha == 1f && background.rectTransform.anchoredPosition == Vector2.zero, "페이지 위치와 알파 고정 및 현재 컷 페이드 완료 " + index);
            }
            else Check(background.sprite == expectedBackground && !context.BackgroundPresenter.IsShowingComicCut, "검은 화면 독백 " + index);
            Check(background.preserveAspect, "원본 비율 유지 " + index);
            Check(dialogueGroup.alpha == 1f && fadeGroup.alpha == 0f && comicGroup.alpha == 0f, "대화창 표시와 페이드 해제 " + index);
            Check(body.text == command.DialogueText && speaker.text == (command.HideSpeakerName ? string.Empty : command.SpeakerName), "본문과 화자 일치 " + index);
            Check(speaker.enabled == !command.HideSpeakerName, "독백 이름 숨김과 일반 대사 이름 복원 " + index);
            if (!context.DialoguePresenter.IsTextComplete)
            {
                runner.RequestAdvance();
                Check(runner.CurrentCommandIndex == index && context.DialoguePresenter.IsTextComplete, "첫 입력은 타이핑 완성 " + index);
            }
            body.ForceMeshUpdate();
            Check(!body.isTextOverflowing, "대사 영역 넘침 없음 " + index);
            string capture = visualCommand.CommandType == StoryCommandType.ShowComicCut ? visualCommand.ComicCutId : expectedBackground.name;
            if (command.DialogueText.Length == longest) capture = "longest-dialogue";
            if (captured.Add(capture))
            {
                double until = EditorApplication.timeSinceStartup + .2;
                while (EditorApplication.timeSinceStartup < until) yield return null;
                ScreenCapture.CaptureScreenshot(Folder + "/" + capture + ".png");
                until = EditorApplication.timeSinceStartup + .2;
                while (EditorApplication.timeSinceStartup < until) yield return null;
            }
            dialogueCount++;
            runner.RequestAdvance();
            yield return null;
        }
        Check(fadedCuts.Count == 10 && capturedFade, "10개 컷 점진 공개와 중간 화면 캡처");
        Check(enteredCuts.Count == 10, "10개 컷 진입 실행");
        Check(dialogueCount == 92 && sawPause, "전체 원고와 무대사 정지 실행");
        Check(completionCount == 1 && lastReason == StoryCompletionReason.Completed, "정상 완료 이벤트 1회");
        Check(dialogueGroup.alpha == 0f && fadeGroup.alpha == 1f, "종료 대화창 숨김과 검은 화면");
        runner.RequestAdvance();
        Check(completionCount == 1, "완료 후 추가 입력 중복 통지 없음");
        Check(runner.Play(sequence, true), "완료 후 재생 가능");
        while (runner.PlaybackState != StoryPlaybackState.WaitingForTimedCommand || sequence.Commands[runner.CurrentCommandIndex].CommandType != StoryCommandType.ShowComicCut)
        {
            if (runner.PlaybackState == StoryPlaybackState.WaitingForDialogueInput) runner.RequestAdvance();
            yield return null;
        }
        Check(context.BackgroundPresenter.IsCutTransitioning, "컷 진입 도중 스킵 검증 시작");
        runner.RequestSkip();
        Check(!runner.IsPlaying && completionCount == 2 && lastReason == StoryCompletionReason.Skipped, "스킵 완료 이벤트 1회");
        Check(dialogueGroup.alpha == 0f && fadeGroup.alpha == 0f, "스킵 시 대화창과 페이드 정리");
        Check(!context.BackgroundPresenter.IsShowingComicCut && !context.BackgroundPresenter.IsCutTransitioning && !background.useSpriteMesh, "스킵 시 컷 자원과 전환 정리");
        yield return null;
        Check(completionCount == 2, "스킵 뒤 지연 콜백 중복 완료 없음");
        Check(runner.Play(sequence, false), "스킵 비허용 재생 시작");
        runner.RequestSkip();
        Check(runner.IsPlaying && !runner.CanSkip && completionCount == 2, "비허용 스킵 무시");
    }
    /// <summary>실제 Sprite 삼각형에서 표본이 공개되어 있는지 검사한다.</summary>
    private static bool MeshContains(Sprite sprite, Vector2 point)
    {
        var vertices = sprite.vertices; var triangles = sprite.triangles;
        for (int i = 0; i < triangles.Length; i += 3)
        {
            Vector2 a = vertices[triangles[i]], b = vertices[triangles[i + 1]], c = vertices[triangles[i + 2]];
            float ab = (b.x-a.x)*(point.y-a.y)-(b.y-a.y)*(point.x-a.x);
            float bc = (c.x-b.x)*(point.y-b.y)-(c.y-b.y)*(point.x-b.x);
            float ca = (a.x-c.x)*(point.y-c.y)-(a.y-c.y)*(point.x-c.x);
            if ((ab >= -.00001f && bc >= -.00001f && ca >= -.00001f) || (ab <= .00001f && bc <= .00001f && ca <= .00001f)) return true;
        }
        return false;
    }

    /// <summary>오목·역방향 컷의 삼각형 면적과 잘못된 다각형 거부를 검사한다.</summary>
    private static void VerifyGeometry()
    {
        var lShape = new[] { new Vector2(0,0), new Vector2(1,0), new Vector2(1,.4f), new Vector2(.4f,.4f), new Vector2(.4f,1), new Vector2(0,1) };
        foreach (var polygon in new[] { lShape, lShape.Reverse().ToArray() })
        {
            Check(StoryComicCutGeometry.TryBuild(polygon, new Rect(0,0,100,100), 100, out var crop, out var vertices, out var triangles), "오목 컷 및 역방향 꼭짓점 지원");
            float area = 0f;
            for (int i=0; i<triangles.Length; i+=3)
            {
                Vector2 a=vertices[triangles[i]], b=vertices[triangles[i+1]], c=vertices[triangles[i+2]];
                area += Mathf.Abs((b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x))*.5f;
                Vector2 center=(a+b+c)/3f+Vector2.one*.5f;
                Check(!(center.x>.4f && center.y<.6f), "오목 컷 빈 영역에 삼각형이 없음");
            }
            Check(Mathf.Abs(area-.64f)<.0001f && triangles.Length==12, "오목 컷 면적 보존");
        }
        var invalid = new Vector2[][] {
            null, new[] { Vector2.zero, Vector2.one },
            new[] { Vector2.zero, new Vector2(1,1), new Vector2(0,1), new Vector2(1,0) },
            new[] { Vector2.zero, Vector2.right, new Vector2(2,0) },
            new[] { Vector2.zero, Vector2.one, new Vector2(float.NaN,0) },
            new[] { Vector2.zero, Vector2.zero, Vector2.one }
        };
        foreach (var polygon in invalid)
            Check(!StoryComicCutGeometry.TryBuild(polygon,new Rect(0,0,100,100),100,out _,out _,out _), "잘못된 컷 다각형 거부");
    }

}
