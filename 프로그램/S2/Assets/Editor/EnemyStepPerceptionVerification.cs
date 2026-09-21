using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>저장된 전투 씬에서 칸별 감지·이동 중단·발각 후 엄폐 연계를 검증한다. 플레이 변경은 저장하지 않는다.</summary>
[InitializeOnLoad]
public static class EnemyStepPerceptionVerification
{
    // 도메인 리로드 이후 실행 요청과 검증 결과를 보관한다.
    private const string Pending = "S2.StepPerception.Verify";
    private const string Output = "Temp/StepPerception/verification.txt";
    private static readonly List<string> results = new();
    private static IEnumerator routine;
    private static double nextStep;

    /// <summary>플레이 진입 후 실제 씬 검증을 시작한다.</summary>
    static EnemyStepPerceptionVerification() => EditorApplication.playModeStateChanged += OnMode;

    /// <summary>편집 씬을 보존한 채 검증용 플레이를 시작한다.</summary>
    [MenuItem("Tools/S2/Verify Enemy Step Perception")]
    public static void Verify()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().name != "BattleTest01" || SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("저장된 BattleTest01 편집 상태에서 실행해야 합니다.");
        Directory.CreateDirectory("Temp/StepPerception");
        File.WriteAllText(Output, "RUNNING");
        SessionState.SetBool(Pending, true);
        EditorApplication.isPlaying = true;
    }

    /// <summary>플레이 초기화 뒤 검사 루틴을 연결한다.</summary>
    private static void OnMode(PlayModeStateChange mode)
    {
        if (mode != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
        SessionState.SetBool(Pending, false);
        results.Clear();
        routine = Run(); nextStep = EditorApplication.timeSinceStartup + 1;
        EditorApplication.update += Tick;
    }

    /// <summary>실제 프레임을 진행하며 검사 종료·예외를 기록한다.</summary>
    private static void Tick()
    {
        EditorApplication.QueuePlayerLoopUpdate();
        if (EditorApplication.timeSinceStartup < nextStep) return;
        try
        {
            if (!EditorApplication.isPlaying) throw new Exception("검증 중 플레이 종료");
            if (!routine.MoveNext()) { Finish(null); return; }
            nextStep = EditorApplication.timeSinceStartup + .02;
        }
        catch (Exception exception) { Finish(exception); }
    }

    /// <summary>결과를 기록하고 플레이 변경을 폐기한다.</summary>
    private static void Finish(Exception exception)
    {
        EditorApplication.update -= Tick;
        (routine as IDisposable)?.Dispose(); routine = null;
        File.WriteAllText(Output, (exception == null ? "PASS" : "FAIL: " + exception) + "\n" + string.Join("\n", results));
        if (exception == null) Debug.Log($"칸별 적 감지 검증 {results.Count}개 통과");
        else Debug.LogError("칸별 적 감지 검증 실패: " + exception);
        EditorApplication.isPlaying = false;
    }

    /// <summary>실제 적·시야·발각 처리자로 중간 감지와 복귀 중단을 검증한다. 설정은 플레이 사본에만 적용한다.</summary>
    private static IEnumerator Run()
    {
        Application.runInBackground = true;
        var queue = ActionPresentationQueue.Instance;
        double deadline = EditorApplication.timeSinceStartup + 30;
        while (queue.IsBusy && EditorApplication.timeSinceStartup < deadline) yield return null;
        Check(!queue.IsBusy, "시작 연출 완료");
        var grid = GridManager.Instance;
        var enemy = EnemyRegistry.Instance.Enemies.First(e => e.transform.parent.name == "천하회_큰못");
        var player = PlayerUnitControlManager.Instance.ActiveUnit;
        foreach(var other in EnemyRegistry.Instance.Enemies.ToArray())
            if(other != enemy) other.transform.parent.gameObject.SetActive(false);
        foreach(var other in TacticalUnitRegistry.Instance.PlayerControllableUnits.ToArray())
            if(other != player) other.gameObject.SetActive(false);
        foreach(var map in new[]{grid.WallLogicTilemap,grid.LowObstacleLogicTilemap,grid.BoundaryLogicTilemap})
            for(int x=2;x<=20;x++)for(int y=2;y<=16;y++)
                map.SetTile(map.WorldToCell(grid.GridToWorld(new GridPosition(x,y))),null);
        var data = UnityEngine.Object.Instantiate(enemy.EnemyData);
        Set(enemy,"enemyData",data);
        Set(data,"sightRange",2); Set(data,"useAdjacentDetection",false);
        Set(data,"patrolMoveRange",3); Set(data,"alertSpreadRange",0);
        Set(data,"alertReactionMoveRange",3); Set(data,"suspicionReactionMoveRange",3);
        var bentPath = new[]{new GridPosition(6,5),new GridPosition(7,5),new GridPosition(7,6)};

        Reset(enemy,player,queue,new GridPosition(9,5));
        Check(enemy.GridActor.TryMoveTo(new GridPosition(7,6)),"기존 끝점 비교 배치");
        enemy.GridSight.SetFacingDirection(GridDirection.Up); enemy.GridSight.RefreshSight();
        Check(!enemy.GridSight.CanDetectPlayer(player.GridActor.GridPosition),"기존 경로 끝 시야에서는 플레이어가 안 보임");
        Reset(enemy,player,queue,new GridPosition(9,5));
        var context = new ActionResolutionContext(queue);
        int moved = EnemyMovementUtility.MoveAlongPath(enemy,bentPath,context,"검증 원래 이동",out bool detected);
        Check(moved==2&&detected&&enemy.GridActor.GridPosition==new GridPosition(7,5),"둘째 칸 감지 즉시 기존 경로 중단");
        var moves=Moves(queue,enemy);
        Check(moves.Length==2&&moves[0].MovePhase==MovePresentationPhase.Start&&moves[1].MovePhase==MovePresentationPhase.End,
            "실제 두 칸만 Start/End로 예약");
        Check(!moves.Any(m=>m.ToPosition==new GridPosition(7,6)),"진행하지 않은 셋째 칸 연출 없음");
        var captures=((Queue<IActionLogicEvent>)Get(context,"logicEvents")).OfType<EnemyPerceptionChangedLogicEvent>().ToArray();
        Check(captures.Length==2&&!captures[0].HasDetectedPlayer&&captures[1].HasDetectedPlayer,"각 칸의 감지 결과 개별 보존");
        enemy.GridSight.SetFacingDirection(GridDirection.Down);enemy.GridSight.RefreshSight();
        Check(!enemy.GridSight.CanDetectPlayer(player.GridActor.GridPosition),"후속 방향 변경은 현재 시야를 덮어씀");
        var frozenContext=new ActionResolutionContext(queue);
        UnityEngine.Object.FindFirstObjectByType<EnemyPerceptionCoordinator>().Handle(captures[1],frozenContext);
        Check(((Queue<IActionLogicEvent>)Get(frozenContext,"logicEvents")).OfType<AlertTriggeredLogicEvent>().Single().DetectedPosition==new GridPosition(9,5),
            "감지 처리자는 덮어쓴 시야 대신 저장된 발견 결과 사용");

        Reset(enemy,player,queue,new GridPosition(8,5));
        moved=EnemyMovementUtility.MoveAlongPath(enemy,bentPath,new ActionResolutionContext(queue),"첫 칸 검증",out detected);
        Check(moved==1&&detected&&Moves(queue,enemy).Single().MovePhase==MovePresentationPhase.Single,"첫 칸 감지는 Single로 종료");
        Reset(enemy,player,queue,new GridPosition(10,5));
        var straight=new[]{new GridPosition(6,5),new GridPosition(7,5),new GridPosition(8,5)};
        moved=EnemyMovementUtility.MoveAlongPath(enemy,straight,new ActionResolutionContext(queue),"끝 칸 검증",out detected);
        Check(moved==3&&detected&&Moves(queue,enemy).Last().MovePhase==MovePresentationPhase.End,"원래 마지막 칸 감지도 End 유지");
        Reset(enemy,player,queue,new GridPosition(10,5));
        moved=EnemyMovementUtility.MoveAlongPath(enemy,bentPath,new ActionResolutionContext(queue),"미감지 검증",out detected);
        Check(moved==3&&!detected&&Moves(queue,enemy).Last().MovePhase==MovePresentationPhase.End,"미감지는 전체 경로 완료");
        Reset(enemy,player,queue,new GridPosition(8,5));
        Set(enemy.AlertState,"currentState",EnemyAwarenessState.Alerted);
        moved=EnemyMovementUtility.MoveAlongPath(enemy,bentPath,new ActionResolutionContext(queue),"발각 후 이동 검증",out detected);
        Check(moved==3&&!detected,"이미 발각된 적의 엄폐·전투 이동은 재감지로 끊지 않음");

        // 실제 경비 복귀 호출자까지 검사해 도착 방향 복원·평상 전환이 감지를 덮지 않게 한다.
        foreach(var target in new[]{new GridPosition(9,5),new GridPosition(10,5)})
        {
            Reset(enemy,player,queue,target);
            Set(enemy.RoutineController,"routineType",EnemyRoutineType.Guard);
            Set(enemy.RoutineController,"guardPosition",new GridPosition(8,5));
            Set(enemy.RoutineController,"guardLookDirection",GridDirection.Down);
            Set(enemy.AlertState,"suspiciousPhase",SuspiciousBehaviorPhase.ReturningToRoutine);
            enemy.InvestigationAgent.TryExecuteSuspiciousTurn(new ActionResolutionContext(queue));
            Check(enemy.GridActor.GridPosition==new GridPosition(target.x-2,5),"경비 복귀 실제 감지 칸 중단 "+target);
            Check(enemy.GridSight.FacingDirection==GridDirection.Right,"감지 후 원래 경비 방향으로 돌지 않음 "+target);
            Check(enemy.AlertState.IsSuspicious,"발각 논리 대기 중 평상 복귀를 실행하지 않음 "+target);
        }
        Reset(enemy,player,queue,new GridPosition(7,5));
        Set(enemy.AlertState,"suspiciousPhase",SuspiciousBehaviorPhase.Searching);
        Set(enemy.AlertState,"remainingSuspicionTurns",1);
        Set(enemy.RoutineController,"guardPosition",new GridPosition(5,8));
        enemy.GridSight.SetFacingDirection(GridDirection.Up);enemy.GridSight.RefreshSight();
        enemy.InvestigationAgent.TryExecuteSuspiciousTurn(new ActionResolutionContext(queue));
        Check(enemy.GridActor.GridPosition==new GridPosition(5,5)&&enemy.AlertState.SuspiciousPhase==SuspiciousBehaviorPhase.Searching,
            "마지막 수색 회전에서 발견하면 복귀 이동을 시작하지 않음");

        Reset(enemy,player,queue,new GridPosition(9,5));
        Set(enemy.AlertState,"currentState",EnemyAwarenessState.Unaware);
        Set(enemy.RoutineController,"routineType",EnemyRoutineType.Patrol);Set(enemy.RoutineController,"patrolGroup",null);
        var point=new GameObject("감지 검증 임시 목적지").AddComponent<PatrolPoint>();
        point.transform.position=grid.GridToWorld(new GridPosition(8,5));
        Set(enemy.RoutineController,"targetPoint",point);Set(point,"lookDirection",GridDirection.Down);
        enemy.ActionPoint.RefillForTurn();
        enemy.RoutineController.TryExecuteRoutineTurn(new ActionResolutionContext(queue));
        Check(enemy.GridActor.GridPosition==new GridPosition(7,5)&&enemy.RoutineController.TargetPoint==point,"실제 단독 순찰도 중간 감지에서 목적지 도착 처리 중단");

        Reset(enemy,player,queue,new GridPosition(9,5));
        Set(data,"investigationMinimumDistance",0);Set(data,"investigationMaximumDistance",0);
        var info=new EnemySuspicionInfo(new GridPosition(8,5),default,enemy,0,null,default);
        enemy.InvestigationAgent.Handle(new EnemySuspicionTriggeredLogicEvent(enemy,info),new ActionResolutionContext(queue));
        Check(enemy.GridActor.GridPosition==new GridPosition(7,5),"의심 조사 이동도 중간 감지에서 중단");
        Check(enemy.AlertState.SuspiciousPhase!=SuspiciousBehaviorPhase.Observing,"감지 뒤 관찰 단계로 덮어쓰지 않음");

        // 실제 발각 처리와 엄폐 반응까지 같은 문맥에서 확정한 뒤 순서대로 재생한다.
        Reset(enemy,player,queue,new GridPosition(9,5));
        Set(data,"investigationMinimumDistance",1);Set(data,"investigationMaximumDistance",3);
        var tile=ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>();
        var low=grid.LowObstacleLogicTilemap;
        low.SetTile(low.WorldToCell(grid.GridToWorld(new GridPosition(8,6))),tile);
        context=new ActionResolutionContext(queue);
        EnemyMovementUtility.MoveAlongPath(enemy,bentPath,context,"원래 경로 검증",out detected);
        Check(detected,"엄폐 연계 시나리오 중간 감지");
        context.Resolve();
        Check(!context.HasFailed&&enemy.AlertState.IsAlerted,"같은 문맥에서 발각 논리 확정");
        var events=((Queue<PresentationEvent>)Get(queue,"eventQueue")).ToArray();
        var original=events.Where(e=>e.Type==PresentationEventType.EnemyReactionMove&&e.Message=="원래 경로 검증").ToArray();
        var reactions=events.Where(e=>e.Type==PresentationEventType.EnemyReactionMove&&e.Message=="적 경계 엄폐 이동 연출").ToArray();
        Check(original.Length==2&&original.Last().ToPosition==new GridPosition(7,5),"엄폐 반응 뒤에도 원래 경로는 감지 칸까지만 존재");
        Check(reactions.Length>0&&reactions[0].FromPosition==new GridPosition(7,5),"엄폐 반응은 감지 칸에서 바로 이어짐");
        int alert=Array.FindIndex(events,e=>e.Type==PresentationEventType.AlertDetected);
        int reaction=Array.FindIndex(events,e=>e.Type==PresentationEventType.EnemyReactionMove&&e.Message=="적 경계 엄폐 이동 연출");
        Check(alert>=0&&reaction>alert,"발각 연출 뒤 엄폐 이동 순서 유지");
        Check(events.Count(e=>e.Type==PresentationEventType.AlertDetected)==1,"감지 중복으로 발각 연출을 여러 번 만들지 않음");
        ActorPresentationRegistry.Instance.TryGetVisual(enemy.GridActor,out var visual);
        visual.SetPresentationPosition(grid.GridToWorld(new GridPosition(5,5)));
        queue.PlayQueuedEvents();
        deadline=EditorApplication.timeSinceStartup+30;
        while(queue.IsBusy&&EditorApplication.timeSinceStartup<deadline)yield return null;
        Check(!queue.IsBusy&&!queue.HasFailed,"이동 종료·발각·엄폐 연출이 교착 없이 완료");
        Check(Vector3.Distance(visual.transform.position-visual.CoverWorldOffset,grid.GridToWorld(enemy.GridActor.GridPosition))<.01f,
            "최종 비주얼 위치가 확정된 엄폐 위치와 일치");
    }

    /// <summary>독립 시나리오 사이의 플레이 사본만 초기화한다.</summary>
    private static void Reset(EnemyContext enemy,TacticalUnitContext player,ActionPresentationQueue queue,GridPosition playerPosition)
    {
        queue.ClearQueuedEvents();
        Check((player.GridActor.GridPosition == playerPosition || player.GridActor.TryMoveTo(playerPosition)) && (enemy.GridActor.GridPosition == new GridPosition(5,5) || enemy.GridActor.TryMoveTo(new GridPosition(5,5))),"검증 위치 배치 "+playerPosition);
        Set(enemy.AlertState,"currentState",EnemyAwarenessState.Suspicious);
        Set(enemy.AlertState,"suspiciousPhase",SuspiciousBehaviorPhase.MovingToInvestigationPosition);
        enemy.GridSight.SetFacingDirection(GridDirection.Down);enemy.GridSight.RefreshSight();
    }
    /// <summary>현재 예약된 해당 적 이동만 읽는다.</summary>
    private static PresentationEvent[] Moves(ActionPresentationQueue queue,EnemyContext enemy)
        => ((Queue<PresentationEvent>)Get(queue,"eventQueue")).Where(e=>e.Type==PresentationEventType.EnemyReactionMove&&e.Enemy==enemy).ToArray();
    /// <summary>테스트 사본의 비공개 설정만 조정한다.</summary>
    private static void Set(object target,string name,object value)=>target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);
    /// <summary>테스트에서 큐에 기록된 결과를 읽는다.</summary>
    private static object Get(object target,string name)=>target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(target);
    /// <summary>검사 실패는 기록 후 플레이를 종료한다.</summary>
    private static void Check(bool condition,string message)
    {
        if(!condition)throw new Exception(message);
        results.Add("PASS "+message);
    }
}

