using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>실제 배틀 씬에서 데이터 참조·풀 수명·총격 결과의 재생 동기화를 검사한다.</summary>
[InitializeOnLoad]
public static class PresentationMediaVerification
{
    // 도메인 리로드를 넘어 명시적인 검증 요청만 이어간다.
    private const string Pending = "S2.PresentationMedia.Verify";
    private const string Report = "Temp/PresentationMedia/verification.txt";
    private static IEnumerator routine;
    private static double nextStep;
    private static readonly List<string> results = new();
    private static readonly List<string> errors = new();

    /// <summary>플레이 진입 뒤 검증 루틴을 시작한다.</summary>
    static PresentationMediaVerification()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
            SessionState.SetBool(Pending, false); results.Clear(); errors.Clear();
            Application.logMessageReceived += Capture;
            routine = Run(); nextStep = EditorApplication.timeSinceStartup + 1;
        };
    }

    /// <summary>저장된 1-1 씬에서만 재현 가능한 플레이 검증을 실행한다.</summary>
    [MenuItem("Tools/S2/Verify Presentation Media")]
    public static void Verify()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty ||
            SceneManager.GetActiveScene().path != PresentationMediaAuthoring.Scenes[0]) throw new Exception("저장된 BattleTest01 씬에서 실행하세요.");
        Directory.CreateDirectory("Temp/PresentationMedia"); File.WriteAllText(Report, "RUNNING");
        SessionState.SetBool(Pending, true); EditorApplication.isPlaying = true;
    }

    /// <summary>편집기 프레임마다 제한 시간 내 검사하고 실패도 보고서에 남긴다.</summary>
    private static void Tick()
    {
        if (routine == null || EditorApplication.timeSinceStartup < nextStep) return;
        EditorApplication.QueuePlayerLoopUpdate();
        try
        {
            if (!EditorApplication.isPlaying) throw new Exception("검증 도중 플레이 종료");
            if (!routine.MoveNext()) Finish(null);
            nextStep = EditorApplication.timeSinceStartup + .01;
        }
        catch (Exception exception) { Finish(exception); }
    }

    /// <summary>풀 재사용·일시 정지·동시 재생과 실제 명중·회피 연출을 검증한다.</summary>
    private static IEnumerator Run()
    {
        Application.runInBackground = true;
        var queue = ActionPresentationQueue.Instance;
        double deadline = EditorApplication.timeSinceStartup + 30;
        while (queue.IsBusy && EditorApplication.timeSinceStartup < deadline) yield return null;
        Check(!queue.IsBusy, "입장 큐 완료");
        var context = Find<BattlePresentationContext>(); var audio = context.Audio; var vfx = context.Vfx;
        Check(audio != null && audio.HasValidReference() && audio.isActiveAndEnabled, "오디오 명시 참조와 활성 상태");
        var fire = Load<SoundData>("Sound/YujinGunFire.asset");
        var hit = Load<SoundData>("Sound/YujinGunHit.asset");
        var miss = Load<SoundData>("Sound/YujinGunMiss.asset");
        foreach (var data in new[] { fire, hit, miss }) Check(audio.HasValidData(data) && data.Clips.Length == 3, data.name + " 후보 3개");
        var effect = Load<VfxData>("Vfx/SharedBloodHit.asset");
        int created = vfx.CreatedInstanceCount;
        var old = vfx.Acquire(effect); var other = vfx.Acquire(effect);
        var oldInstance = (GameObject)Get(old, "instance"); var otherInstance = (GameObject)Get(other, "instance");
        Check(oldInstance != otherInstance && old.IsValid && other.IsValid, "동일 VFX 동시 대여 분리");
        old.Release(); other.Release(); var reused = vfx.Acquire(effect);
        Check(vfx.CreatedInstanceCount == created, "기존 VFX 예열 풀 재사용");
        var reusedInstance = (GameObject)Get(reused, "instance"); var position = reusedInstance.transform.position;
        old.SetTransform(Vector3.one * 999, Quaternion.identity, Vector3.one); old.Release();
        Check(reused.IsValid && reusedInstance.transform.position == position, "VFX 이전 핸들 재사용 간섭 차단");
        reused.Release();
        var unregistered = UnityEngine.Object.Instantiate(effect);
        var direct = vfx.Acquire(unregistered);
        Check(direct != null && direct.IsValid, "미리 생성 목록 밖 데이터 직접 재생"); direct.Release();
        var interruptedEffect = vfx.Acquire(effect); vfx.enabled = false;
        Check(!interruptedEffect.IsValid && vfx.ActiveHandleCount == 0, "VFX 비활성화 시 전체 반납"); vfx.enabled = true;
        // 오디오 변동을 위해 전투 명중 난수를 소비하면 안 된다.
        var randomState = UnityEngine.Random.state;
        var sounds = new List<SoundHandle>();
        for (int i = 0; i < 4; i++) sounds.Add(audio.Play(fire, Vector3.zero));
        var randomAfter = UnityEngine.Random.state;
        Check(JsonUtility.ToJson(randomState) == JsonUtility.ToJson(randomAfter), "효과음 후보·음높이 선택이 전투 난수에 영향 없음");
        Check(audio.ActiveVoiceCount == 4 && audio.CreatedVoiceCount == 4, "예열 오디오 4개 동시 사용");
        var source = (AudioSource)Get(sounds[0], "Source");
        Check(fire.Clips.Contains(source.clip) && source.pitch >= fire.PitchRange.x && source.pitch <= fire.PitchRange.y &&
            Mathf.Abs(source.volume - fire.Volume * audio.MasterVolume) < .001f && source.spatialBlend == 0, "음원 후보·음높이·음량·2D 설정");
        for (int i = 0; i < 4; i++) yield return null;
        Check(source.isPlaying && source.timeSamples > 0, "오디오 장치에서 실제 샘플 재생 진행");
        foreach (var sound in sounds) sound.Stop();
        var fresh = audio.Play(fire, Vector3.zero); sounds[0].Stop();
        Check(fresh.IsValid && audio.ActiveVoiceCount == 1 && audio.CreatedVoiceCount == 4, "오디오 이전 핸들 차단·풀 재사용");
        audio.SetMasterVolume(.25f);
        Check(Mathf.Abs(((AudioSource)Get(fresh, "Source")).volume - fire.Volume * .25f) < .001f, "재생 중 전체 음량 조정");
        audio.SetMasterVolume(.8f);
        Time.timeScale = 0;
        deadline = EditorApplication.timeSinceStartup + 1;
        while (EditorApplication.timeSinceStartup < deadline) yield return null;
        Check(fresh.IsValid && (bool)Get(fresh, "Paused") && audio.ActiveVoiceCount == 1, "시간 정지 동안 오디오 대여 유지");
        Time.timeScale = 1;
        deadline = EditorApplication.timeSinceStartup + 2;
        while (fresh.IsValid && EditorApplication.timeSinceStartup < deadline) yield return null;
        Check(!fresh.IsValid && audio.ActiveVoiceCount == 0, "재개 후 종료 소스 자동 회수");
        sounds.Clear();
        for (int i = 0; i < 16; i++) sounds.Add(audio.Play(fire, Vector3.zero));
        Check(audio.Play(fire, Vector3.zero) == null && sounds.All(s => s.IsValid), "동시 16개 한도에서 기존 소리 유지");
        audio.enabled = false;
        Check(sounds.All(s => !s.IsValid) && audio.ActiveVoiceCount == 0, "비활성화 시 소리 정지·핸들 무효화"); audio.enabled = true;

        // 튜토리얼 시작 대사가 제어권을 잠가도 저장된 유진을 직접 대상으로 검사한다.
        var player = GameObject.Find("Player Actor Root").GetComponentInChildren<TacticalUnitContext>();
        var target = EnemyRegistry.Instance.Enemies.First(e => e.IsAlive).GridActor;
        var combat = Find<CombatActionPresenter>();
        var recorded = new List<SoundHandle>();
        var frames = new List<int>();
        Action<SoundHandle> capture = sound => { recorded.Add(sound); frames.Add(Time.frameCount); };
        audio.SoundStarted += capture;
        foreach (bool missed in new[] { false, true })
        {
            recorded.Clear(); frames.Clear();
            int hp = player.Health.CurrentHitPoint; int ap = player.ActionPoint.Current;
            var handle = new PresentationEventHandle(() => { });
            combat.Handle(PresentationEvent.CombatAction(player.GridActor, target, player.GridActor.GridPosition, target.GridPosition,
                AttackPresentationKind.PlayerGun, missed ? new AttackResult(false, 50, 50, 99, default) : AttackResult.GuaranteedHit(),
                new DamageResult(true, 2, 10, 8, false, false), !missed), handle);
            deadline = EditorApplication.timeSinceStartup + 5;
            while (recorded.Count == 0 && EditorApplication.timeSinceStartup < deadline) yield return null;
            Check(recorded.Count == 2 && recorded[0].Data == fire && recorded[1].Data == (missed ? miss : hit), "총격 " + (missed ? "회피" : "명중") + " 발사·결과음 각각 1회");
            Check(frames[0] == frames[1] && Get(combat, "attackerEffect") != null && Get(combat, "targetEffect") != null, "총격 소리와 타격 VFX 같은 프레임");
            while (!handle.IsCompleted && EditorApplication.timeSinceStartup < deadline) yield return null;
            Check(handle.IsCompleted && recorded[0].IsValid, "연출 완료가 발사음 잔향을 기다리지 않음");
            Check(player.Health.CurrentHitPoint == hp && player.ActionPoint.Current == ap, "소리 재생으로 HP·AP 변경 없음");
            audio.StopAll();
        }
        recorded.Clear();
        var interrupted = new PresentationEventHandle(() => { });
        combat.Handle(PresentationEvent.CombatAction(player.GridActor, target, player.GridActor.GridPosition, target.GridPosition,
            AttackPresentationKind.PlayerGun, AttackResult.GuaranteedHit(), new DamageResult(true, 2, 10, 8, false, false), true), interrupted);
        deadline = EditorApplication.timeSinceStartup + 5;
        while (recorded.Count == 0 && EditorApplication.timeSinceStartup < deadline) yield return null;
        combat.enabled = false;
        Check(interrupted.IsCompleted && audio.ActiveVoiceCount == 0, "전투 담당 중단 시 자체 효과음·이벤트 정리"); combat.enabled = true;
        audio.SoundStarted -= capture;
        Check(errors.Count == 0, "새 런타임 오류 없음: " + string.Join(" | ", errors));
    }

    /// <summary>실제 실행 오류를 검사 실패에 포함한다.</summary>
    private static void Capture(string text, string stack, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(text); }
    /// <summary>검사 결과를 기록하고 실패 지점에서 즉시 중단한다.</summary>
    private static void Check(bool valid, string text)
    { results.Add((valid ? "PASS " : "FAIL ") + text); if (!valid) throw new Exception(text); }
    /// <summary>검증용 씬의 단일 담당을 찾는다.</summary>
    private static T Find<T>() where T : UnityEngine.Object => UnityEngine.Object.FindObjectsByType<T>(FindObjectsSortMode.None).Single();
    /// <summary>검증에서만 내부 수명 상태를 관찰한다.</summary>
    private static object Get(object target, string field) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(target);
    /// <summary>검증에 쓸 공유 데이터를 읽는다.</summary>
    private static T Load<T>(string path) where T : UnityEngine.Object => AssetDatabase.LoadAssetAtPath<T>(PresentationMediaAuthoring.DataRoot + path);
    /// <summary>성공·실패 보고서를 저장하고 플레이 사본을 종료한다.</summary>
    private static void Finish(Exception exception)
    {
        Application.logMessageReceived -= Capture; Time.timeScale = 1;
        (routine as IDisposable)?.Dispose(); routine = null;
        File.WriteAllText(Report, (exception == null ? "PASS" : "FAIL\n" + exception) + "\n" + string.Join("\n", results));
        if (exception != null) Debug.LogError("미디어 검증 실패: " + exception);
        else Debug.Log("미디어 " + results.Count + "개 검사 통과");
        EditorApplication.isPlaying = false;
    }
}
