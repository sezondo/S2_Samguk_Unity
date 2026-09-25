using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>전투 집중 연출의 연결 검사와 실제 큐 재생 회귀를 수행한다.</summary>
[InitializeOnLoad]
public static class CombatStageVerification
{
    // 도메인 리로드 이후 명시적인 실행 요청과 결과를 보관한다.
    private const string Folder = "Temp/CombatStage/";
    private const string Pending = "S2.CombatStage.Test";
    private static IEnumerator routine;
    private static double nextStep;
    private static readonly List<string> results = new();
    // 기존 순찰 참조 오류 외의 새로운 런타임 오류는 검증 실패로 취급한다.
    private static readonly List<string> unexpectedErrors = new();

    /// <summary>편집기 갱신과 플레이 진입에 검증 요청 처리기를 연결한다.</summary>
    static CombatStageVerification()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += mode =>
        {
            if (mode != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
            SessionState.SetBool(Pending, false);
            results.Clear(); unexpectedErrors.Clear();
            Application.logMessageReceived += CaptureError;
            routine = Run(); nextStep = EditorApplication.timeSinceStartup + 1;
        };
    }

    /// <summary>요청 파일이 있을 때만 설치 또는 검증을 실행하고 예외를 결과에 남긴다.</summary>
    private static void Tick()
    {
        if (routine != null)
        {
            EditorApplication.QueuePlayerLoopUpdate();
            if (EditorApplication.timeSinceStartup < nextStep) return;
            try
            {
                if (!EditorApplication.isPlaying) throw new Exception("검증 도중 플레이 종료");
                if (!routine.MoveNext()) Finish(null);
                nextStep = EditorApplication.timeSinceStartup + .01;
            }
            catch (Exception e) { Finish(e); }
            return;
        }
        string request = Folder + "request.txt";
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || !File.Exists(request)) return;
        string command = File.ReadAllText(request).Trim();
        File.Delete(request);
        try
        {
            if (command == "setup") Setup();
            else if (command == "test") Verify();
        }
        catch (Exception e) { File.WriteAllText(Folder + "setup.txt", "FAIL\n" + e); Debug.LogError(e); }
    }

    /// <summary>사용자가 요청한 연출에 필요한 머티리얼과 씬 참조만 명시적으로 연결한다.</summary>
    private static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty)
            throw new Exception("저장된 편집 씬에서만 전투 연출을 연결할 수 있습니다.");
        const string assetRoot = "Assets/Art/Vfx/Combat/";
        var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetRoot + "CombatBackground.shader");
        if (shader == null || ShaderUtil.ShaderHasError(shader)) throw new Exception("흐림 셰이더 컴파일 실패");
        var importer = (TextureImporter)AssetImporter.GetAtPath(assetRoot + "FlowingBorder.png");
        importer.textureType = TextureImporterType.Default;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 4096;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.SaveAndReimport();
        var material = AssetDatabase.LoadAssetAtPath<Material>(assetRoot + "CombatBackground.mat");
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, assetRoot + "CombatBackground.mat");
        }
        string original = SceneManager.GetActiveScene().path;
        foreach (string path in new[] { "Assets/Scenes/Test/BattleTest01.unity", "Assets/Scenes/Test/BattleTest02.unity", "Assets/Scenes/Tset.unity" })
        {
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var combat = Find<CombatActionPresenter>();
            var camera = Find<CombatCameraPresenter>();
            if (camera == null) camera = combat.gameObject.AddComponent<CombatCameraPresenter>();
            Set(camera, "targetCamera", Find<Camera>());
            Set(camera, "gridManager", Find<GridManager>());
            Set(camera, "presentationRegistry", Find<ActorPresentationRegistry>());
            Set(camera, "backgroundBlurMaterial", material);
            Set(camera, "borderTexture", AssetDatabase.LoadAssetAtPath<Texture2D>(assetRoot + "FlowingBorder.png"));
            Set(camera, "resultFont", AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Fonts/NotoSansKR-VF.ttf"));
            Set(camera, "hudCanvases", UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            Set(camera, "minimumOrthographicSize", 2.1f);
            Set(camera, "horizontalPadding", .35f);
            Set(camera, "verticalPadding", .3f);
            Set(camera, "focusDuration", .08f);
            Set(camera, "settleDuration", .5f);
            Set(camera, "restoreDuration", .08f);
            Set(combat, "cameraPresenter", camera);
            foreach (var hack in UnityEngine.Object.FindObjectsByType<HackPresenter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Set(hack, "cameraPresenter", camera);
            if (!camera.HasValidReference() || !camera.HasValidData() || !combat.HasValidReference()) throw new Exception("연출 참조 오류: " + path);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene(original, OpenSceneMode.Single);
        File.WriteAllText(Folder + "setup.txt", "PASS: 전투 씬 3개 참조 연결 및 셰이더 확인");
    }

    /// <summary>실제 전투 씬의 플레이 사본에서 검증하고 변경은 저장하지 않는다.</summary>
    [MenuItem("Tools/S2/Verify Combat Stage")]
    public static void Verify()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty || SceneManager.GetActiveScene().name != "BattleTest01")
            throw new Exception("저장된 BattleTest01 편집 상태에서 검증해야 합니다.");
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Folder + "verification.txt", "RUNNING");
        SessionState.SetBool(Pending, true);
        EditorApplication.isPlaying = true;
    }

    /// <summary>결과를 남기고 플레이 사본을 폐기한다.</summary>
    private static void Finish(Exception e)
    {
        Application.logMessageReceived -= CaptureError;
        (routine as IDisposable)?.Dispose(); routine = null;
        File.WriteAllText(Folder + "verification.txt", (e == null ? "PASS" : "FAIL\n" + e) + "\n" + string.Join("\n", results));
        if (e != null) Debug.LogError("전투 집중 연출 검증 실패: " + e);
        else Debug.Log("전투 집중 연출 " + results.Count + "개 검사 통과");
        EditorApplication.isPlaying = false;
    }

    /// <summary>표시 이동, 타격 동기화, 사망, 투척·회수·해킹과 중단 복귀를 검증한다.</summary>
    private static IEnumerator Run()
    {
        Application.runInBackground = true;
        var queue = ActionPresentationQueue.Instance;
        double deadline = EditorApplication.timeSinceStartup + 30;
        while (queue.IsBusy && EditorApplication.timeSinceStartup < deadline) yield return null;
        Check(!queue.IsBusy, "입장 큐 완료");
        // 자동 구도 검증 중에는 실제 키보드·휠 입력이 기준 카메라 위치를 바꾸지 않게 한다. 플레이 사본에만 적용한다.
        foreach (var mover in UnityEngine.Object.FindObjectsByType<CameraKeyboardMover>(FindObjectsSortMode.None)) mover.enabled = false;
        foreach (var zoom in UnityEngine.Object.FindObjectsByType<CameraMouseZoom>(FindObjectsSortMode.None)) zoom.enabled = false;
        var camera = Find<CombatCameraPresenter>();
        var combat = Find<CombatActionPresenter>();
        var registry = ActorPresentationRegistry.Instance;
        var player = PlayerUnitControlManager.Instance.ActiveUnit;
        var enemies = EnemyRegistry.Instance.Enemies.Where(e => e.IsAlive).ToArray();
        Check(enemies.Length >= 2, "검증용 대상과 주변 배우 존재");
        var target = enemies[0].GridActor;
        registry.TryGetVisual(player.GridActor, out var actorVisual);
        registry.TryGetVisual(target, out var targetVisual);
        registry.TryGetVisual(enemies[1].GridActor, out var otherVisual);
        var worldCamera = (Camera)Get(camera, "targetCamera");
        Vector3 originalCamera = worldCamera.transform.position;
        Quaternion originalRotation = worldCamera.transform.rotation;
        float originalSize = worldCamera.orthographicSize;
        var originalMaterial = otherVisual.TargetRenderer.sharedMaterial;
        Vector3 actorStart = actorVisual.transform.position;
        Vector3 targetStart = targetVisual.transform.position;
        GridPosition logicalPosition = player.GridActor.GridPosition;
        int originalHp = player.Health.CurrentHitPoint, originalAp = player.ActionPoint.Current;
        Set(Find<PlayerVisionPresenter>(), "revealAllForDebug", true);
        for (int i=0;i<3;i++) yield return null;
        foreach (var visual in registry.Visuals) visual.SetVisionAlpha(1f);
        // 플레이 사본에서 방향별 발 위치를 만들어 수평 강제 배치와 가까운 대상 밀어내기를 검출한다.
        foreach (Vector3 delta in new[] { new Vector3(6, 0), new Vector3(-6, 0), new Vector3(0, 6), new Vector3(0, -6),
            new Vector3(5, 4), new Vector3(-5, 4), new Vector3(5, -4), new Vector3(-5, -4), new Vector3(0, 1), new Vector3(30, 20), new Vector3(0, 30) })
        {
            targetVisual.SetCombatStaged(true);
            targetVisual.transform.position += actorVisual.GroundWorldPosition + delta - targetVisual.GroundWorldPosition;
            Vector3 initialDirection = targetVisual.GroundWorldPosition - actorVisual.GroundWorldPosition;
            var directionFocus = new PresentationEventHandle(() => { });
            camera.Handle(PresentationEvent.CombatCameraFocus(logicalPosition, target.GridPosition, "방향 검증", player.GridActor, target, AttackPresentationKind.PlayerGun), directionFocus);
            deadline = EditorApplication.timeSinceStartup + 4;
            while (!directionFocus.IsCompleted && EditorApplication.timeSinceStartup < deadline) yield return null;
            Vector3 finalDirection = targetVisual.GroundWorldPosition - actorVisual.GroundWorldPosition;
            Check(directionFocus.IsCompleted && Vector3.Angle(initialDirection, finalDirection) < .1f &&
                Mathf.Abs(finalDirection.magnitude - initialDirection.magnitude) < .01f,
                "방향·거리 고정 " + delta);
            Check(actorVisual.CombatWorldOffset == Vector3.zero, "집중 중 공격자 이동 없음 " + delta);
            foreach (float roll in new[] { 0f, 5.6f, 11.2f, -11.2f })
            {
                worldCamera.transform.rotation = originalRotation * Quaternion.Euler(0, 0, roll);
                worldCamera.transform.position = (Vector3)Get(camera,"stageCameraPosition") + Vector3.right * .16f;
                foreach (var v in new[] { actorVisual, targetVisual })
                {
                    Bounds bounds = v.TargetRenderer.bounds;
                    for (int x=0;x<2;x++) for(int y=0;y<2;y++)
                    {
                        Vector3 corner = new Vector3(x==0?bounds.min.x:bounds.max.x,y==0?bounds.min.y:bounds.max.y,bounds.center.z);
                        Vector3 viewport = worldCamera.WorldToViewportPoint(corner);
                        Check(viewport.x>=0f && viewport.x<=1f && viewport.y>=.16f && viewport.y<=.84f,
                            "두 배우 외곽·띠 안전 영역 " + delta + " 기울기 " + roll);
                    }
                }
            }
            var directionRestore = new PresentationEventHandle(() => { });
            camera.Handle(PresentationEvent.CombatCameraRestore(), directionRestore);
            deadline = EditorApplication.timeSinceStartup + 3;
            while (!directionRestore.IsCompleted && EditorApplication.timeSinceStartup < deadline) yield return null;
            Check(directionRestore.IsCompleted && actorVisual.CombatWorldOffset == Vector3.zero, "방향별 복귀 " + delta);
            targetVisual.transform.position = targetStart;
        }
        var kinds = new[] { AttackPresentationKind.MeleeWithSword, AttackPresentationKind.MeleeUnarmed, AttackPresentationKind.PlayerGun };
        foreach (var kind in kinds)
        {
            bool missed = kind == AttackPresentationKind.PlayerGun;
            queue.Enqueue(PresentationEvent.CombatCameraFocus(logicalPosition, target.GridPosition, "검증", player.GridActor, target, kind));
            queue.Enqueue(PresentationEvent.CombatAction(player.GridActor, target, logicalPosition, target.GridPosition, kind,
                missed ? new AttackResult(false, 50, 50, 99, default) : AttackResult.GuaranteedHit(),
                new DamageResult(true, 3, 10, 7, false, false), !missed));
            queue.Enqueue(PresentationEvent.CombatCameraRestore());
            queue.PlayQueuedEvents();
            bool sawStage = false, sawImpact = false, sawRestore = false;
            float stagedAt = 0f, impactedAt = 0f;
            deadline = EditorApplication.timeSinceStartup + 10;
            while (queue.IsBusy && EditorApplication.timeSinceStartup < deadline)
            {
                float weight = (float)Get(camera, "stageWeight");
                if (!sawStage && weight > .99f)
                {
                    sawStage = true;
                    stagedAt = Time.time;
                    Check(actorVisual.CombatWorldOffset == Vector3.zero, kind + " 공격자 비주얼 위치 고정");
                    Check(Vector3.Distance(targetVisual.transform.position, targetStart) < .01f, kind + " 피격자 엄폐 위치 유지");
                    Check(otherVisual.TargetRenderer.sharedMaterial != originalMaterial, kind + " 주변 배우 흐림 적용");
                    Check(((Canvas)Get(camera, "combatCanvas")).enabled, kind + " 띠 유지");
                }
                if (!sawImpact && (UnityEngine.UI.Text)Get(camera, "resultText") is { enabled: true } label)
                {
                    sawImpact = true;
                    impactedAt = Time.time;
                    Check(impactedAt - stagedAt >= .47f && impactedAt - stagedAt < .75f, kind + " 준비 0.5초");
                    Check(label.text == (missed ? "회피" : "3"), kind + " 결과 표시");
                    Check(targetVisual.CurrentAnimationStateName == (missed ? "Dodge" : "Hit"), kind + " 결과와 자세 동기화");
                    Check(Get(combat, "targetEffect") != null, kind + " 결과와 이펙트 동기화");
                    ScreenCapture.CaptureScreenshot(Folder + kind + ".png");
                }
                if (sawImpact && !sawRestore && weight > 0f && weight < .95f)
                {
                    sawRestore = true;
                    Check(Time.time - impactedAt >= .48f && Time.time - impactedAt < .7f, kind + " 타격 여운 0.5초");
                }
                yield return null;
            }
            Check(sawStage && sawImpact && sawRestore && !queue.IsBusy, kind + " 진입·타격·복귀 완료");
            Check(Vector3.Distance(actorVisual.transform.position, actorStart) < .01f, kind + " 비주얼 원위치");
            Check(player.GridActor.GridPosition == logicalPosition && player.Health.CurrentHitPoint == originalHp && player.ActionPoint.Current == originalAp,
                kind + " 논리 좌표·HP·AP 불변");
            Check(Vector3.Distance(worldCamera.transform.position, originalCamera) < .001f && Quaternion.Angle(worldCamera.transform.rotation, originalRotation) < .001f && Mathf.Abs(worldCamera.orthographicSize-originalSize)<.001f,
                kind + $" 카메라 위치·줌·회전 원복 (위치 {Vector3.Distance(worldCamera.transform.position, originalCamera):F6}, 회전 {Quaternion.Angle(worldCamera.transform.rotation, originalRotation):F6}, 줌 {Mathf.Abs(worldCamera.orthographicSize-originalSize):F6})");
            Check(otherVisual.TargetRenderer.sharedMaterial == originalMaterial, kind + " 주변 원본 머티리얼 복원");
        }
        // 좌표만 있는 기존 발각 포커스는 띠·배우 이동 없이 위치만 바뀐다.
        var alertHandle = new PresentationEventHandle(() => { });
        camera.Handle(PresentationEvent.AlertCameraFocus(enemies[1]), alertHandle);
        deadline = EditorApplication.timeSinceStartup + 3;
        while (!alertHandle.IsCompleted && EditorApplication.timeSinceStartup < deadline) yield return null;
        Check(alertHandle.IsCompleted && !camera.IsCombatActive && actorVisual.CombatWorldOffset == Vector3.zero, "발각 카메라 기존 동작 유지");
        worldCamera.transform.position = originalCamera;

        // 대상 비활성화와 상관없이 저장해 둔 카메라·비주얼 상태를 복구해야 한다.
        var focus = new PresentationEventHandle(() => { });
        camera.Handle(PresentationEvent.CombatCameraFocus(logicalPosition,target.GridPosition,"중단 검증",player.GridActor,target,AttackPresentationKind.PlayerGun),focus);
        deadline = EditorApplication.timeSinceStartup+3;
        while (!focus.IsCompleted && EditorApplication.timeSinceStartup<deadline) yield return null;
        camera.enabled = false;
        Check(!camera.IsCombatActive && actorVisual.CombatWorldOffset == Vector3.zero, "비활성화 중단에서 비주얼 복구");
        Check(Vector3.Distance(worldCamera.transform.position,originalCamera)<.001f && otherVisual.TargetRenderer.sharedMaterial==originalMaterial,"중단 시 카메라·흐림 복구");
        camera.enabled = true;

        var sword = UnityEngine.Object.FindObjectsByType<SwordActionPresenter>(FindObjectsSortMode.None).First(s=>(GridActor)Get(s,"ownerActor")==player.GridActor);
        foreach (int distance in new[]{1,3,6})
        {
            GridPosition destination = logicalPosition + new GridPosition(distance,0);
            queue.Enqueue(PresentationEvent.CombatCameraFocus(logicalPosition,destination,"빈 칸 투척",player.GridActor,null,AttackPresentationKind.SwordThrow));
            queue.Enqueue(PresentationEvent.SwordMove(player.GridActor,logicalPosition,destination,SwordMoveKind.Throw));
            queue.Enqueue(PresentationEvent.CombatCameraRestore());
            queue.PlayQueuedEvents();
            deadline=EditorApplication.timeSinceStartup+8;
            while(queue.IsBusy && EditorApplication.timeSinceStartup<deadline) yield return null;
            Check(!queue.IsBusy && !camera.IsCombatActive, distance+"칸 빈 칸 투척 복귀");
            Check(Vector3.Distance(((Transform)Get(sword,"swordVisual")).position,GridManager.Instance.GridToWorld(destination)+(Vector3)Get(sword,"deployedPositionOffset"))<.001f, distance+"칸 검 배치 위치 유지");
            queue.Enqueue(PresentationEvent.SwordMove(player.GridActor,destination,logicalPosition,SwordMoveKind.Recall));
            queue.PlayQueuedEvents();
            bool recallFocused=false;
            deadline=EditorApplication.timeSinceStartup+8;
            while(queue.IsBusy && EditorApplication.timeSinceStartup<deadline) { recallFocused |= camera.IsCombatActive; yield return null; }
            Check(!queue.IsBusy && !recallFocused, distance+"칸 회수는 집중 연출 제외");
        }
        queue.Enqueue(PresentationEvent.CombatCameraFocus(logicalPosition,target.GridPosition,"적중 투척",player.GridActor,target,AttackPresentationKind.SwordThrow));
        queue.Enqueue(PresentationEvent.SwordMove(player.GridActor,logicalPosition,target.GridPosition,SwordMoveKind.Throw));
        queue.Enqueue(PresentationEvent.CombatAction(player.GridActor,target,logicalPosition,target.GridPosition,AttackPresentationKind.SwordThrow,
            AttackResult.GuaranteedHit(),new DamageResult(true,4,10,6,false,false),true));
        queue.Enqueue(PresentationEvent.CombatCameraRestore());
        queue.PlayQueuedEvents(); bool sawSwordHit=false, sawThrowLaunch=false;
        int throwLaunchFrame=-1;
        deadline=EditorApplication.timeSinceStartup+10;
        while(queue.IsBusy && EditorApplication.timeSinceStartup<deadline)
        {
            var label=(UnityEngine.UI.Text)Get(camera,"resultText");
            if (!sawThrowLaunch && Get(sword,"trailEffect") != null)
            {
                sawThrowLaunch = true;
                throwLaunchFrame = Time.frameCount;
                Check(actorVisual.CurrentAnimationStateName == "SwordThrow", "검 출발 프레임에 투척 자세 시작");
                Check(label.enabled && label.text == "4", "잔상 최초 표시 프레임에 대미지 표시");
                Check(camera.TryGetTargetCenter(out Vector3 instantTarget) &&
                    Vector3.Distance(((Transform)Get(sword,"swordVisual")).position, instantTarget) < .001f, "잔상 최초 프레임에 검 도착 완료");
            }
            if(!sawSwordHit && label.enabled && label.text=="4")
            {
                sawSwordHit=true;
                Check(sawThrowLaunch && Time.frameCount == throwLaunchFrame, "투척 자세·잔상·타격 같은 프레임");
                Check(Get(sword,"trailEffect")!=null,"검 도착 시 잔상과 피격 동시 표시");
                Check(targetVisual.CurrentAnimationStateName=="Hit","투척 적중 자세 동기화");
                ScreenCapture.CaptureScreenshot(Folder+"SwordThrow.png");
            }
            yield return null;
        }
        Check(sawSwordHit && !queue.IsBusy && Get(sword,"swordMoveCoroutine")==null,"적중 투척 완료 및 코루틴 정리");
        Check(Vector3.Distance(((Transform)Get(sword,"swordVisual")).position,GridManager.Instance.GridToWorld(target.GridPosition)+(Vector3)Get(sword,"deployedPositionOffset"))<.001f,"적중 투척 뒤 검 배치 위치 복원");

        foreach(var kind in new[]{AttackPresentationKind.EnemyRanged,AttackPresentationKind.EnemyMelee})
        {
            string state = kind == AttackPresentationKind.EnemyRanged ? "EnemyRanged" : "MeleeWithSword";
            var enemySource = enemies.First(e => registry.TryGetVisual(e.GridActor,out var v) && v.Animator.HasState(0,Animator.StringToHash("Base Layer."+state)));
            registry.TryGetVisual(enemySource.GridActor,out var enemyVisual);
            queue.Enqueue(PresentationEvent.CombatCameraFocus(enemySource.GridActor.GridPosition,logicalPosition,"적 공격",enemySource.GridActor,player.GridActor,kind));
            queue.Enqueue(PresentationEvent.CombatAction(enemySource.GridActor,player.GridActor,enemySource.GridActor.GridPosition,logicalPosition,kind,
                AttackResult.GuaranteedHit(),new DamageResult(true,2,10,8,false,false),true));
            queue.Enqueue(PresentationEvent.CombatCameraRestore());
            queue.PlayQueuedEvents(); bool enemyApproached=false;
            deadline=EditorApplication.timeSinceStartup+10;
            while(queue.IsBusy && EditorApplication.timeSinceStartup<deadline)
            {
                enemyApproached |= enemyVisual.CombatWorldOffset.sqrMagnitude>.01f;
                yield return null;
            }
            Check(!enemyApproached && !queue.IsBusy && enemyVisual.CombatWorldOffset==Vector3.zero,kind+" 적 위치 고정·타격·복귀");
            Check(player.GridActor.GridPosition==logicalPosition && player.Health.CurrentHitPoint==originalHp,kind+" 연출은 논리 불변");
        }

        var hack = Find<HackPresenter>();
        var hackable=(HackableObject)Get(hack,"targetHackable");
        bool hackedBefore=hackable.IsHacked;
        queue.Enqueue(PresentationEvent.CombatCameraFocus(logicalPosition,hackable.GridPosition,"해킹 검증",player.GridActor,hackable.GridActor,hackable:hackable));
        queue.Enqueue(PresentationEvent.Hack(player.GridActor,hackable,hackable.GridPosition,hackable.GridPosition));
        queue.Enqueue(PresentationEvent.CombatCameraRestore());
        queue.PlayQueuedEvents(); bool sawHackComplete=false;
        deadline=EditorApplication.timeSinceStartup+12;
        while(queue.IsBusy && EditorApplication.timeSinceStartup<deadline)
        {
            var label=(UnityEngine.UI.Text)Get(camera,"resultText");
            if(label!=null && label.enabled && label.text=="해킹 완료")
            {
                if(!sawHackComplete) ScreenCapture.CaptureScreenshot(Folder+"HackComplete.png");
                sawHackComplete=true;
            }
            yield return null;
        }
        Check(!queue.IsBusy && sawHackComplete && !camera.IsCombatActive,"해킹 완료 섬광 박자와 복귀");
        Check(hackable.IsHacked==hackedBefore,"해킹 Presenter는 논리 해킹 상태 불변");

        queue.Enqueue(PresentationEvent.CombatCameraFocus(logicalPosition,target.GridPosition,"사망 검증",player.GridActor,target,AttackPresentationKind.MeleeWithSword));
        queue.Enqueue(PresentationEvent.CombatAction(player.GridActor,target,logicalPosition,target.GridPosition,AttackPresentationKind.MeleeWithSword,
            AttackResult.GuaranteedHit(),new DamageResult(true,99,7,0,false,true),true));
        queue.Enqueue(PresentationEvent.CombatCameraRestore());
        queue.PlayQueuedEvents(); bool sawDeath=false;
        deadline=EditorApplication.timeSinceStartup+10;
        while(queue.IsBusy && EditorApplication.timeSinceStartup<deadline)
        {
            if(targetVisual.IsDeathPresentation)
            {
                if(!sawDeath) Check(targetVisual.CurrentAnimationStateName=="Death","치명 피해는 피격 없이 즉시 사망 자세");
                sawDeath=true;
            }
            yield return null;
        }
        Check(sawDeath && !queue.IsBusy && targetVisual.CurrentAnimationStateName=="Death","사망 자세 유지 및 큐 완료");
        Check(!camera.IsCombatActive && actorVisual.CombatWorldOffset==Vector3.zero && Vector3.Distance(targetVisual.transform.position,targetStart)<.01f,"사망 뒤 위치·카메라 복귀");
        // 시간 0도 허용하는 기존 Inspector 계약에서 코루틴 핸들이 남지 않는지 확인한다.
        Set(camera,"focusDuration",0f); Set(camera,"settleDuration",0f); Set(camera,"restoreDuration",0f);
        for(int i=0;i<2;i++)
        {
            var instantFocus=new PresentationEventHandle(()=>{});
            camera.Handle(PresentationEvent.CombatCameraFocus(logicalPosition,logicalPosition),instantFocus);
            deadline=EditorApplication.timeSinceStartup+2;
            while(!instantFocus.IsCompleted && EditorApplication.timeSinceStartup<deadline) yield return null;
            var instantRestore=new PresentationEventHandle(()=>{});
            camera.Handle(PresentationEvent.CombatCameraRestore(),instantRestore);
            deadline=EditorApplication.timeSinceStartup+2;
            while(!instantRestore.IsCompleted && EditorApplication.timeSinceStartup<deadline) yield return null;
            Check(instantFocus.IsCompleted && instantRestore.IsCompleted && !camera.IsCombatActive,"시간 0 연출 연속 실행 "+i);
        }
        Check(unexpectedErrors.Count==0,"신규 런타임 오류 없음: "+string.Join(" / ",unexpectedErrors));
    }

    /// <summary>알려진 기존 순찰 오류를 제외하고 신규 실행 오류를 수집한다.</summary>
    private static void CaptureError(string message,string stack,LogType type)
    {
        if(type!=LogType.Error && type!=LogType.Exception && type!=LogType.Assert) return;
        if(message.Contains("PatrolRoute_Group01") && message.Contains("시작 순찰 지점")) return;
        unexpectedErrors.Add(message);
    }

    /// <summary>현재 씬에서 검증 대상 하나를 읽는다.</summary>
    private static T Find<T>() where T:UnityEngine.Object => UnityEngine.Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
    /// <summary>테스트용 비공개 상태를 읽는다.</summary>
    private static object Get(object target,string field) => target.GetType().GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(target);
    /// <summary>편집 연결 필드만 설정하고 저장 대상으로 표시한다.</summary>
    private static void Set(UnityEngine.Object target,string field,object value)
    {
        target.GetType().GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(target,value);
        EditorUtility.SetDirty(target);
        if (PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
    }
    /// <summary>검증 실패를 중단하고 통과 항목을 결과에 추가한다.</summary>
    private static void Check(bool value,string label)
    {
        if(!value) throw new Exception(label);
        results.Add("통과: "+label);
    }
}
