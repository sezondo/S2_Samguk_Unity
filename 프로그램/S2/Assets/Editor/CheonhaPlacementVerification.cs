using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>배치한 천하회 3종의 연결·순찰·공격·확정 자세를 저장 없이 플레이 검증한다.</summary>
[InitializeOnLoad]
public static class CheonhaPlacementVerification
{
    // 도메인 재시작을 넘기는 요청 키와 결과 목록이다.
    private const string Pending="S2.Cheonha.Verify";
    private static readonly List<string> results=new();
    private static IEnumerator routine;
    private static double nextStep;
    /// <summary>플레이 전환 이벤트를 연결한다.</summary>
    static CheonhaPlacementVerification(){EditorApplication.playModeStateChanged+=OnMode;}
    /// <summary>현재 저장된 배치를 검증 모드로 실행한다.</summary>
    [MenuItem("Tools/S2/Verify Cheonha Placement")]
    public static void Verify()
    {
        var scene=SceneManager.GetActiveScene();
        if(scene.name!="BattleTest01"||scene.isDirty||EditorApplication.isPlayingOrWillChangePlaymode)
            throw new Exception("저장된 BattleTest01 편집 상태에서 실행하세요.");
        Directory.CreateDirectory("Temp/Cheonha");File.WriteAllText("Temp/Cheonha/verification.txt","RUNNING");
        SessionState.SetBool(Pending,true);EditorApplication.isPlaying=true;
    }
    /// <summary>플레이 초기화 후 검증 루틴을 연결한다.</summary>
    private static void OnMode(PlayModeStateChange mode)
    {
        if(mode!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool(Pending,false))return;
        SessionState.SetBool(Pending,false);results.Clear();routine=Run();nextStep=EditorApplication.timeSinceStartup+1;
        EditorApplication.update+=Tick;
    }
    /// <summary>게임 프레임과 함께 검증 단계를 실행한다.</summary>
    private static void Tick()
    {
        EditorApplication.QueuePlayerLoopUpdate();if(EditorApplication.timeSinceStartup<nextStep)return;
        try{if(!EditorApplication.isPlaying)throw new Exception("검증 도중 플레이가 종료됐습니다.");
            if(!routine.MoveNext()){Finish(null);return;}nextStep=EditorApplication.timeSinceStartup+(routine.Current is float delay?delay:.02f);}
        catch(Exception error){Finish(error);}
    }
    /// <summary>결과를 기록하고 임시 플레이 변경을 폐기한다.</summary>
    private static void Finish(Exception error)
    {
        EditorApplication.update-=Tick;File.WriteAllText("Temp/Cheonha/verification.txt",(error==null?"PASS":"FAIL: "+error)+"\n"+string.Join("\n",results));
        if(error==null)Debug.Log($"천하회 배치 검증 {results.Count}개 통과");else Debug.LogError("천하회 배치 검증 실패: "+error);
        (routine as IDisposable)?.Dispose();routine=null;EditorApplication.isPlaying=false;
    }
    /// <summary>실제 연결과 6턴 왕복 순찰, 병종 공격 제한, 모든 그림 전환을 확인한다.</summary>
    private static IEnumerator Run()
    {
        Application.runInBackground=true;var queue=ActionPresentationQueue.Instance;double deadline=EditorApplication.timeSinceStartup+30;
        while(queue.IsPlaying&&EditorApplication.timeSinceStartup<deadline)yield return .1f;
        Check(!queue.IsPlaying,"초기 연출 완료");
        var holder=GameObject.Find("천하회_배치");Check(holder!=null,"천하회 배치 루트 활성");
        var enemies=CheonhaPlacementSetup.Names.Select(n=>holder.transform.Find(n).GetComponentInChildren<EnemyContext>()).ToArray();
        Check(EnemyRegistry.Instance.Enemies.Count==3,"활성 적 3명 등록");
        for(int i=0;i<3;i++)
        {
            var enemy=enemies[i];var root=enemy.transform.parent;var visual=root.GetComponentInChildren<ActorVisualController>();var cover=root.GetComponentInChildren<LowCoverIdlePresenter>();
            Check(enemy.enabled&&enemy.HasValidReference()&&enemy.TurnAgent.enabled&&enemy.TurnAgent.HasValidData(),root.name+" AI 참조·데이터 유효");
            Check(enemy.GridActor.IsRegisteredOnGrid&&enemy.GridActor.GridPosition==CheonhaPlacementSetup.Positions[i],root.name+" 지정 위치 점유");
            Check(cover.enabled&&cover.HasValidReference()&&visual.HasValidCoverIdleAnimation(),root.name+" 높은·낮은 엄폐 연결");
            Check(ActorPresentationRegistry.Instance.TryGetVisual(enemy.GridActor,out var registered)&&registered==visual,root.name+" 연출 등록 연결");
            Check(enemy.EnemyData.TurnActionPoint==3&&enemy.EnemyData.Combat.maximumAttacks==1,root.name+" 3AP·공격 1회");
        }
        // 검수 사진에서 원거리 배치까지 보이도록 표시 계층만 임시 공개한다.
        foreach(var renderer in UnityEngine.Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
            if(renderer.name=="Soft Fog"||renderer.name=="Outside Fog")renderer.gameObject.SetActive(false);
        foreach(var enemy in enemies)enemy.transform.parent.GetComponentInChildren<ActorVisualController>().SetVisionAlpha(1);
        yield return .4f;
        CheonhaPlacementSetup.Capture("hammer-position",new Vector3(48,21,-10),4);
        CheonhaPlacementSetup.Capture("smallnail-position",new Vector3(34,28,-10),6);
        CheonhaPlacementSetup.Capture("bignail-position",new Vector3(30,16,-10),4);
        var small=enemies[1];Check(small.RoutineController.RoutineType==EnemyRoutineType.Patrol&&small.RoutineController.PatrolGroup==null,"작은못 단독 PatrolPoint 순찰 연결");
        Check(enemies[0].RoutineController.RoutineType==EnemyRoutineType.Guard&&enemies[2].RoutineController.RoutineType==EnemyRoutineType.Guard,"망치·큰못 경비 대기");
        // 이름에 남은 옛 좌표가 아니라 현재 연결된 순찰 지점의 실제 칸을 기준으로 검사한다.
        var homePoint=small.RoutineController.CurrentPoint;
        var middlePoint=homePoint.ConnectedPoints.Single();
        var farPoint=middlePoint.ConnectedPoints.Single(p=>p!=homePoint);
        Check(homePoint.TryGetGridPosition(out var initial),"현재 순찰 시작점 좌표");
        Check(farPoint.TryGetGridPosition(out var farPosition),"현재 순찰 끝점 좌표");
        var visited=new List<GridPosition>();
        // 실제 보드의 우회 경로 길이에 맞춰 한 바퀴를 확인하되 무한 반복은 막는다.
        for(int turn=0;turn<20;turn++)
        {
            small.ActionPoint.RefillForTurn();small.TurnAgent.BeginTurn();var context=new ActionResolutionContext(queue);
            Check(small.TurnAgent.TryExecuteTurn(context),"작은못 순찰 이동 "+(turn+1));context.Resolve();
            Check(small.ActionPoint.Current==2&&small.AlertState.IsUnaware,"순찰 1AP 및 평상 상태 "+(turn+1));
            visited.Add(small.GridActor.GridPosition);queue.PlayQueuedEvents();deadline=EditorApplication.timeSinceStartup+20;
            while(queue.IsPlaying&&EditorApplication.timeSinceStartup<deadline)yield return .1f;
            Check(!queue.IsPlaying,"순찰 이동 연출 완료 "+(turn+1));
            if(small.GridActor.GridPosition==initial&&visited.Contains(farPosition))break;
        }
        Check(small.GridActor.GridPosition==initial&&visited.Contains(farPosition),"수리점 북쪽 순찰 왕복 완료 ("+visited.Count+"턴): "+string.Join(" → ",visited));
        // 실제 공격 이벤트도 동일한 확정 그림의 Animator로 재생한다.
        var player=new EnemyTargetProvider().Collect().First();SetRuntime(player.Health,"maxHitPoint",100);SetRuntime(player.Health,"currentHitPoint",100);
        for(int i=0;i<3;i++)
        {
            var enemy=enemies[i];var kind=i==0?EnemyPlannedActionKind.Melee:EnemyPlannedActionKind.Ranged;
            var attackPosition=i==0?new GridPosition(22,14):new GridPosition(26+i,14);
            Check(enemy.GridActor.TryMoveTo(attackPosition),enemy.transform.parent.name+" 공격 검증 위치");
            var visual=enemy.transform.parent.GetComponentInChildren<ActorVisualController>();visual.SetPresentationPosition(GridManager.Instance.GridToWorld(attackPosition));
            enemy.AlertState.RequestAlert(player.GridActor.GridPosition,enemy);var context=new ActionResolutionContext(queue);
            Check(enemy.AttackAction.TryExecuteAttack(player.GridActor,context,kind),enemy.transform.parent.name+" 주 공격 실행");context.Resolve();queue.PlayQueuedEvents();
            deadline=EditorApplication.timeSinceStartup+20;while(queue.IsPlaying&&EditorApplication.timeSinceStartup<deadline)yield return .1f;
            Check(!queue.IsPlaying,enemy.transform.parent.name+" 실제 공격 연출 종료");
            var near=player.GridActor.GridPosition+GridPosition.Left;
            Check(enemy.AttackAction.CanAttackFrom(near,player.GridActor.GridPosition,EnemyPlannedActionKind.Melee)==(i!=2),enemy.transform.parent.name+" 근접 사용 제한");
            Check(enemy.AttackAction.CanAttackFrom(attackPosition,player.GridActor.GridPosition,EnemyPlannedActionKind.Ranged)==(i!=0),enemy.transform.parent.name+" 원거리 사용 제한");
        }
        for(int i=0;i<3;i++)
        {
            var root=enemies[i].transform.parent;var visual=root.GetComponentInChildren<ActorVisualController>();root.GetComponentInChildren<LowCoverIdlePresenter>().enabled=false;
            var controller=(UnityEditor.Animations.AnimatorController)visual.Animator.runtimeAnimatorController;
            foreach(var child in controller.layers[0].stateMachine.states)
            {
                // 클립 자체를 샘플링하여 실제 연결 Sprite가 병종 확정 파일인지 확인한다.
                var clip=(AnimationClip)child.state.motion;clip.SampleAnimation(visual.gameObject,0);
                Check(visual.TargetRenderer.sprite!=null&&AssetDatabase.GetAssetPath(visual.TargetRenderer.sprite).Contains("/"+CheonhaPlacementSetup.Names[i]+"/"),root.name+" "+child.state.name+" 확정 Sprite");
            }
        }
    }
    /// <summary>검증용 생존 상태만 런타임 인스턴스에 설정한다.</summary>
    private static void SetRuntime(object target,string name,object value)=>target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);
    /// <summary>조건을 검사하고 통과 내역을 저장한다.</summary>
    private static void Check(bool condition,string label){if(!condition)throw new Exception(label);results.Add("통과: "+label);}
}
