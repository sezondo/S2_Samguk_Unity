using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>실제 배틀의 모든 효과음 연결·중복·시간 정지·취소를 플레이 사본에서 검사한다.</summary>
[InitializeOnLoad]
public static class Stage11AudioVerification
{
    private const string Pending="S2.Stage11Audio.Verify", Report="Temp/Stage11Audio/verification.txt";
    // 실행 제한과 재생 이력을 도메인 리로드 뒤 새 검증마다 초기화한다.
    private static IEnumerator routine;
    private static double nextStep;
    private static readonly List<string> results=new();
    private static readonly List<string> errors=new();
    private static readonly List<SoundHandle> heard=new();
    private static readonly HashSet<string> coverage=new();
    private static AudioManager audio;

    /// <summary>명시적 실행 요청이 있을 때만 플레이 진입 뒤 검사를 시작한다.</summary>
    static Stage11AudioVerification()
    {
        EditorApplication.update+=Tick;
        EditorApplication.playModeStateChanged+=mode=>
        {
            if(mode!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool(Pending,false))return;
            SessionState.SetBool(Pending,false); results.Clear(); errors.Clear(); heard.Clear(); coverage.Clear();
            Application.logMessageReceived+=CaptureError; routine=Run(); nextStep=EditorApplication.timeSinceStartup+1;
        };
    }

    /// <summary>저장된 1-1 편집 씬에서 전체 효과음 검증을 시작한다.</summary>
    [MenuItem("Tools/S2/Verify 1-1 Full Audio Draft")]
    public static void Verify()
    {
        var scene=SceneManager.GetActiveScene();
        if(EditorApplication.isPlayingOrWillChangePlaymode||scene.isDirty||scene.path!=PresentationMediaAuthoring.Scenes[0])throw new Exception("저장된 BattleTest01에서 검사하세요.");
        Directory.CreateDirectory("Temp/Stage11Audio"); File.WriteAllText(Report,"RUNNING");
        SessionState.SetBool(Pending,true); EditorApplication.isPlaying=true;
    }

    /// <summary>실제 플레이 프레임을 진행하고 예외·중단을 보고한다.</summary>
    private static void Tick()
    {
        if(routine==null||EditorApplication.timeSinceStartup<nextStep)return;
        EditorApplication.QueuePlayerLoopUpdate();
        try
        {
            if(!EditorApplication.isPlaying)throw new Exception("검증 도중 플레이 종료");
            if(!routine.MoveNext())Finish(null);
            nextStep=EditorApplication.timeSinceStartup+.01;
        }
        catch(Exception exception){Finish(exception);}
    }

    /// <summary>창과 전투·비전투 연출을 실행하여 25종 모두 실제 재생됐는지 검사한다.</summary>
    private static IEnumerator Run()
    {
        Application.runInBackground=true;
        var queue=ActionPresentationQueue.Instance;
        double deadline=EditorApplication.timeSinceStartup+20;
        while(queue.IsBusy&&EditorApplication.timeSinceStartup<deadline)yield return null;
        Check(!queue.IsBusy,"입장 큐 완료");
        audio=Find<BattlePresentationContext>().Audio; audio.SoundStarted+=CaptureSound;
        var menu=Find<BattleMenuController>(); var manual=Find<BattleManualView>(); var menuContext=Find<BattleMenuContext>();
        heard.Clear(); menu.OpenMenu();
        Check(Count("MenuOpen")==1&&Time.timeScale==0,"메뉴 열기와 시간 정지");
        var menuSound=heard.Single();
        var fire=audio.Play(AssetDatabase.LoadAssetAtPath<SoundData>("Assets/Data/Battle/Presentation/Sound/YujinGunFire.asset"),Vector3.zero);
        deadline=EditorApplication.timeSinceStartup+.1;
        while(EditorApplication.timeSinceStartup<deadline)yield return null;
        Check(!(bool)Get(menuSound,"Paused") && ((AudioSource)Get(menuSound,"Source")).timeSamples>0,"정지 중 UI 실제 샘플 진행");
        Check((bool)Get(fire,"Paused"),"동시에 전투 소리는 일시 정지");
        heard.Clear(); menu.OpenManual(); Check(Count("ManualOpen")==1,"메뉴에서 수첩 펼치기 1회");
        int original=manual.Page; int next=original==0?1:original-1;
        heard.Clear(); manual.TurnTo(next); manual.TurnTo(next); manual.TurnTo(-1);
        deadline=EditorApplication.timeSinceStartup+2;
        while(manual.IsTurning&&EditorApplication.timeSinceStartup<deadline)yield return null;
        Check(!manual.IsTurning&&Count("PageTurn")==1,"넘김 중 중복·잘못된 요청에는 소리 없음");
        heard.Clear(); manual.TurnTo(original);
        deadline=EditorApplication.timeSinceStartup+2;
        while(manual.IsTurning&&EditorApplication.timeSinceStartup<deadline)yield return null;
        Check(!manual.IsTurning&&Count("PageTurn")==1,"반대 방향 페이지 넘김 소리 1회");
        heard.Clear(); manual.TurnTo(original); Check(heard.Count==0,"같은 페이지 요청 무음");
        menu.Close(); Check(Count("ManualClose")==1,"수첩 덮기 소리 1회"); menu.Close();
        Check(Count("MenuClose")==1&&Time.timeScale==1,"메뉴 닫기·시간 재개"); audio.StopAll();
        heard.Clear(); var pointer=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left};
        ((BattleUiButton)menuContext.manualOpen).OnPointerClick(pointer);
        Check(Count("UiClick")==1&&Count("ManualOpen")==1,"실제 포인터 확정 클릭과 수첩 펼침");
        menu.Close(); menu.OpenMenu(); heard.Clear();
        ((BattleUiButton)menuContext.settings).OnPointerClick(pointer); Check(heard.Count==0,"준비 중 설정 버튼 무음");
        menu.Close(); audio.StopAll();
        var player=GameObject.Find("Player Actor Root").GetComponentInChildren<TacticalUnitContext>();
        var enemies=EnemyRegistry.Instance.Enemies.Where(e=>e.IsAlive).ToArray();
        var target=enemies[0].GridActor; var combat=Find<CombatActionPresenter>();
        var attacks=new List<(GridActor actor,GridActor target,AttackPresentationKind kind,string cue)> {
            (player.GridActor,target,AttackPresentationKind.SwordThrow,"SwordAttack"),
            (player.GridActor,target,AttackPresentationKind.MeleeWithSword,"SwordMelee"),
            (player.GridActor,target,AttackPresentationKind.MeleeUnarmed,"Unarmed"),
            (player.GridActor,target,AttackPresentationKind.PlayerGun,"YujinGunFire") };
        foreach(var spec in new[]{("천하회_망치",AttackPresentationKind.EnemyMelee,"Hammer"),("천하회_작은못",AttackPresentationKind.EnemyMelee,"Club"),
            ("천하회_작은못",AttackPresentationKind.EnemyRanged,"Crossbow"),("천하회_큰못",AttackPresentationKind.EnemyRanged,"Bow")})
            attacks.Add((enemies.First(e=>e.EnemyData.name==spec.Item1).GridActor,player.GridActor,spec.Item2,spec.Item3));
        foreach(var attack in attacks) foreach(bool hit in new[]{true,false})
        {
            audio.StopAll(); heard.Clear();
            var handle=new PresentationEventHandle(()=>{});
            combat.Handle(PresentationEvent.CombatAction(attack.actor,attack.target,attack.actor.GridPosition,attack.target.GridPosition,attack.kind,
                hit?AttackResult.GuaranteedHit():new AttackResult(false,50,50,99,default),new DamageResult(true,2,10,8,false,false),hit),handle);
            deadline=EditorApplication.timeSinceStartup+4;
            while(!handle.IsCompleted&&EditorApplication.timeSinceStartup<deadline)yield return null;
            Check(handle.IsCompleted&&heard.Count==2&&Count(attack.cue)==1,attack.cue+" 공격음 한 번·결과음 한 번 "+hit);
            string result=attack.kind==AttackPresentationKind.PlayerGun?(hit?"YujinGunHit":"YujinGunMiss"):(hit?"ImpactHit":"Dodge");
            Check(Count(result)==1,"판정 결과에 맞는 소리 "+attack.cue+" "+hit);
        }
        audio.StopAll(); heard.Clear();
        var deathHandle=new PresentationEventHandle(()=>{});
        combat.Handle(PresentationEvent.CombatAction(player.GridActor,target,player.GridActor.GridPosition,target.GridPosition,AttackPresentationKind.MeleeWithSword,
            AttackResult.GuaranteedHit(),new DamageResult(true,99,10,0,false,true),true),deathHandle);
        deadline=EditorApplication.timeSinceStartup+4;
        while(!deathHandle.IsCompleted&&EditorApplication.timeSinceStartup<deadline)yield return null;
        Check(deathHandle.IsCompleted&&Count("Death")==1&&heard.Count==3,"치명 피해는 공격·피격·쓰러짐 각 1회");
        audio.StopAll();
        var sword=Find<SwordActionPresenter>(); var origin=player.GridActor.GridPosition; var destination=origin+new GridPosition(1,0);
        foreach(bool recall in new[]{false,true})
        {
            heard.Clear(); var handle=new PresentationEventHandle(()=>{});
            sword.Handle(PresentationEvent.SwordMove(player.GridActor,recall?destination:origin,recall?origin:destination,recall?SwordMoveKind.Recall:SwordMoveKind.Throw),handle);
            deadline=EditorApplication.timeSinceStartup+4;
            while(!handle.IsCompleted&&EditorApplication.timeSinceStartup<deadline)yield return null;
            Check(handle.IsCompleted&&heard.Count==1&&Count(recall?"SwordRecall":"SwordScout")==1,"정찰 투척·회수는 전투음 중복 없음 "+recall);
        }
        var alert=UnityEngine.Object.FindObjectsByType<AlertDetectedPresenter>(FindObjectsSortMode.None).First();
        var enemy=(EnemyContext)Get(alert,"targetEnemy");
        foreach(bool detected in new[]{false,true})
        {
            heard.Clear(); var handle=new PresentationEventHandle(()=>{});
            alert.Handle(detected?PresentationEvent.AlertDetected(enemy.GridActor.GridPosition,enemy):PresentationEvent.SuspicionDetected(enemy.GridActor.GridPosition,enemy),handle);
            deadline=EditorApplication.timeSinceStartup+4;
            while(!handle.IsCompleted&&EditorApplication.timeSinceStartup<deadline)yield return null;
            Check(handle.IsCompleted&&heard.Count==1&&Count(detected?"Alert":"Suspicion")==1,"기척·발각 점멸 소리 1회 "+detected);
        }
        var hack=Find<HackPresenter>(); var hackable=(HackableObject)Get(hack,"targetHackable");
        heard.Clear();
        queue.Enqueue(PresentationEvent.CombatCameraFocus(origin,hackable.GridPosition,"소리 검증",player.GridActor,hackable.GridActor,hackable:hackable));
        queue.Enqueue(PresentationEvent.Hack(player.GridActor,hackable,hackable.GridPosition,hackable.GridPosition));
        queue.Enqueue(PresentationEvent.CombatCameraRestore()); queue.PlayQueuedEvents();
        deadline=EditorApplication.timeSinceStartup+20;
        while(queue.IsBusy&&EditorApplication.timeSinceStartup<deadline)yield return null;
        Check(!queue.IsBusy&&Count("HackStart")==1&&Count("HackComplete")==1,"해킹 실제 큐의 접속·완료 소리 각 1회");
        var door=Find<SecurityDoorPresenter>(); var logic=(SecurityDoorController)Get(door,"targetDoor");
        heard.Clear(); var doorHandle=new PresentationEventHandle(()=>{}); door.Handle(PresentationEvent.SecurityDoorOpened(logic),doorHandle);
        Check(doorHandle.IsCompleted&&Count("DoorOpen")==1,"문 연출 소리 1회·큐 대기 추가 없음");
        var stage=Find<StageResultPresenter>();
        foreach(bool clear in new[]{false,true})
        {
            heard.Clear(); var handle=new PresentationEventHandle(()=>{}); stage.Handle(clear?PresentationEvent.StageCleared():PresentationEvent.StageFailed(),handle);
            Check(Count(clear?"ElevatorDepart":"MissionFail")==1,"스테이지 결과 소리 한 번 "+clear);
            stage.enabled=false; Check(handle.IsCompleted,"결과 담당 중단에서 대기 해제"); stage.enabled=true;
        }
        var expected=AssetDatabase.FindAssets("t:SoundData",new[]{Stage11AudioAuthoring.SoundRoot}).Select(AssetDatabase.GUIDToAssetPath)
            .Select(path=>AssetDatabase.LoadAssetAtPath<SoundData>(path).name).ToArray();
        Check(expected.Length==25&&expected.All(coverage.Contains),"새 25종 모두 실제 Presenter·UI에서 요청됨: "+string.Join(", ",expected.Where(name=>!coverage.Contains(name))));
        Check(errors.Count==0,"새 런타임 오류 없음: "+string.Join(" | ",errors));
    }

    /// <summary>요청된 실제 데이터와 후보를 기록한다.</summary>
    private static void CaptureSound(SoundHandle sound) { heard.Add(sound); coverage.Add(sound.Data.name); }
    /// <summary>검사 구간에 특정 데이터가 몇 번 재생됐는지 센다.</summary>
    private static int Count(string name)=>heard.Count(sound=>sound.Data.name==name);
    /// <summary>실행 오류를 결과에 포함한다.</summary>
    private static void CaptureError(string message,string stack,LogType type) { if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message); }
    /// <summary>성공·실패 지점을 기록한다.</summary>
    private static void Check(bool valid,string message) { results.Add((valid?"PASS ":"FAIL ")+message); if(!valid)throw new Exception(message); }
    /// <summary>검증 대상 단일 담당을 읽는다.</summary>
    private static T Find<T>() where T:UnityEngine.Object=>UnityEngine.Object.FindObjectsByType<T>(FindObjectsSortMode.None).Single();
    /// <summary>검증에서만 명시적 내부 참조를 관찰한다.</summary>
    private static object Get(object target,string field)=>target.GetType().GetField(field,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).GetValue(target);
    /// <summary>결과를 저장하고 저장되지 않은 플레이 사본을 종료한다.</summary>
    private static void Finish(Exception exception)
    {
        Application.logMessageReceived-=CaptureError; if(audio!=null){audio.SoundStarted-=CaptureSound; audio.StopAll();}
        Time.timeScale=1; (routine as IDisposable)?.Dispose(); routine=null;
        File.WriteAllText(Report,(exception==null?"PASS":"FAIL\n"+exception)+"\n"+string.Join("\n",results)+"\n재생 데이터: "+string.Join(", ",coverage));
        if(exception==null)Debug.Log("전체 효과음 "+results.Count+"개 검사 통과"); else Debug.LogError("전체 효과음 검사 실패: "+exception);
        EditorApplication.isPlaying=false;
    }
}
