using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>판단 시나리오와 실제 AP·피해·턴 연결을 검증한다. 플레이 변경은 저장하지 않는다.</summary>
[InitializeOnLoad]
public static class EnemyAIPlanVerification
{
    // 리로드를 넘어 실행 요청을 전달하고 결과를 보관한다.
    private const string Pending="S2.EnemyAI.Verify";
    private const string Output="Temp/EnemyAI/verification.txt";
    private static readonly List<string> results=new();
    private static IEnumerator routine;
    private static double nextStep;
    /// <summary>플레이 진입 후 실제 씬 검증을 시작한다.</summary>
    static EnemyAIPlanVerification(){EditorApplication.playModeStateChanged+=OnMode;}
    /// <summary>순수 판단 테스트와 실제 전투 테스트를 실행한다.</summary>
    [MenuItem("Tools/S2/Verify Enemy AP AI")]
    public static void Verify()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().name!="BattleTest01"||SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("저장된 BattleTest01 편집 상태에서 실행해야 합니다.");
        Directory.CreateDirectory("Temp/EnemyAI");results.Clear();
        try{PureTests();File.WriteAllText(Output,"RUNNING\n"+string.Join("\n",results));SessionState.SetBool(Pending,true);EditorApplication.isPlaying=true;}
        catch(Exception e){File.WriteAllText(Output,"FAIL: "+e+"\n"+string.Join("\n",results));Debug.LogError("적 판단 검증 실패: "+e);}
    }
    /// <summary>독립된 선형 전장으로 행동 조합과 점수 선택을 확인한다.</summary>
    private static void PureTests()
    {
        var planner=new EnemyCombatPlanner();var s=Settings();var w=new World{positions=new[]{5},health=new[]{20},melee=true};
        var p=planner.Plan(w,s,3,1,3);
        Check(p.Actions.Count(a=>a.Kind==EnemyPlannedActionKind.Move)==2&&p.Attacks==1,"5칸 대상: 2AP 접근 후 근접 공격");
        Check(p.SpentAP==3,"이동 두 번과 공격의 AP 합계");
        w.positions=new[]{2};w.coverAt=0;s.coverWeight=5;
        p=planner.Plan(w,s,3,1,3);
        Check(p.Actions.Count==3&&p.Actions[1].Kind==EnemyPlannedActionKind.Melee&&p.Position.x==0,"접근 → 근접 공격 → 엄폐 복귀");
        w=new World{positions=new[]{10},health=new[]{20},melee=true};s=Settings();
        p=planner.Plan(w,s,2,1,3);Check(p.Attacks==0&&p.Position.x==6,"이번 턴 공격 불가: 2AP로 실제 접근");
        w=new World{positions=new[]{2},health=new[]{5},melee=true,ranged=true,meleeDamage=5,rangedDamage=4};s=Settings();s.allowRanged=true;
        p=planner.Plan(w,s,3,1,3);Check(p.Actions.Any(a=>a.Kind==EnemyPlannedActionKind.Melee),"하이브리드: 원거리 4 대신 근접 5로 처치");
        w.health=new[]{20};w.threatAt=1;s.dangerWeight=20;
        p=planner.Plan(w,s,3,1,3);Check(p.Actions.Any(a=>a.Kind==EnemyPlannedActionKind.Melee)&&p.Position.x!=1,"하이브리드: 안전하게 복귀할 AP가 있으면 근접 피해 이득 선택");
        p=planner.Plan(w,s,2,1,3);Check(p.Actions.Any(a=>a.Kind==EnemyPlannedActionKind.Ranged)&&!p.Actions.Any(a=>a.Kind==EnemyPlannedActionKind.Melee),"하이브리드: 처치 이득 없고 복귀 AP도 없으면 원거리");
        w=new World{positions=new[]{4},health=new[]{20},ranged=true,coverAt=-1};s=Settings();s.allowMelee=false;s.allowRanged=true;s.coverWeight=5;
        p=planner.Plan(w,s,2,1,3);Check(p.Attacks==1&&p.Position.x==-1,"원거리: 공격과 안전한 엄폐 위치를 함께 선택");
        p=planner.Plan(w,s,3,0,3);Check(p.Attacks==0&&p.Position.x==-1,"공격 횟수 소진 뒤에도 이동 가능");
        w=new World{positions=new[]{1},health=new[]{20},melee=true};s=Settings();
        p=planner.Plan(w,s,3,1,0);Check(p.Attacks==1&&p.SpentAP==1,"일반 적은 남은 AP가 있어도 공격 한 번");
        p=planner.Plan(w,s,3,2,0);Check(p.Attacks==2&&p.SpentAP==2,"엘리트는 별도 클래스 없이 공격 두 번");
        s.meleeCost=2;p=planner.Plan(w,s,3,2,0);Check(p.Attacks==1&&p.SpentAP==2,"공격 비용 2AP와 공격 한도 독립 적용");
        s=Settings();w.health=new[]{1};p=planner.Plan(w,s,3,2,0);Check(p.Attacks==1,"확정 처치된 대상을 가상 계획에서 재공격하지 않음");
        w=new World{positions=new[]{1},health=new[]{5},ranged=true,rangedDamage=5,chance=.5f};s=Settings();s.allowMelee=false;s.allowRanged=true;s.damageWeight=0;s.killWeight=100;s.attackWeight=0;s.targetDistanceWeight=0;s.woundedTargetWeight=0;
        p=planner.Plan(w,s,2,2,0);Check(Mathf.Abs(p.Score.Kills-75)<.01f,"명중률 50% 두 발의 처치 확률 75%, 중복 처치 점수 없음");
        w=new World{positions=new[]{1,3},health=new[]{20,2},ranged=true,rangedDamage=2};s=Settings();s.allowRanged=true;s.allowMelee=false;
        p=planner.Plan(w,s,1,1,0);Check(p.Actions[0].TargetIndex==1,"가까운 만피보다 먼 처치 가능 표적 선택");
        s.damageWeight=0;s.killWeight=0;s.targetDistanceWeight=100;s.woundedTargetWeight=0;
        p=planner.Plan(w,s,1,1,0);Check(p.Actions[0].TargetIndex==0,"가까운 대상 가중치를 높이면 표적 변경");
        p=planner.Plan(w,s,3,1,3);Check(p.SpentAP==1,"이미 사격 가능하면 표적 거리 보너스만 얻으려는 이동 금지");
        var randomBefore=JsonUtility.ToJson(UnityEngine.Random.state);
        var first=string.Join(";",planner.Plan(w,s,3,1,3).Actions);var second=string.Join(";",planner.Plan(w,s,3,1,3).Actions);
        Check(first==second&&randomBefore==JsonUtility.ToJson(UnityEngine.Random.state),"판단 결정성 및 실제 공격 난수 미소비");
        p=planner.Plan(w,s,0,1,3);Check(p.Actions.Count==0,"0AP이면 행동 없음");
        Check(CombatTargetRules.IsMeleeAdjacent(new(0,0),new(1,1))&&!CombatTargetRules.IsMeleeAdjacent(new(0,0),new(0,0)),"근접 대각선 허용·자기 칸 금지");
    }
    /// <summary>다른 보너스에 영향을 받지 않는 테스트 기본 가중치다.</summary>
    private static EnemyCombatSettings Settings()=>new(){allowMelee=true,allowRanged=false,damageWeight=10,killWeight=35,attackWeight=3,
        coverWeight=0,dangerWeight=0,distanceWeight=0,approachWeight=1,beamWidth=128};
    /// <summary>플레이 전환 뒤 초기화가 완료되도록 기다린다.</summary>
    private static void OnMode(PlayModeStateChange mode)
    {
        if(mode!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool(Pending,false))return;
        SessionState.SetBool(Pending,false);results.Clear();PureTests();routine=RuntimeTests();nextStep=EditorApplication.timeSinceStartup+1;
        EditorApplication.update+=Tick;
    }
    /// <summary>프레임 사이에서 루틴을 진행하고 실패를 기록한다.</summary>
    private static void Tick()
    {
        EditorApplication.QueuePlayerLoopUpdate();if(EditorApplication.timeSinceStartup<nextStep)return;
        try{if(!EditorApplication.isPlaying)throw new Exception("검증 중 플레이 종료");
            if(!routine.MoveNext()){Finish(null);return;}nextStep=EditorApplication.timeSinceStartup+(routine.Current is float delay?delay:.02f);}
        catch(Exception e){Finish(e);}
    }
    /// <summary>검증 결과를 저장하고 플레이의 임시 변경을 폐기한다.</summary>
    private static void Finish(Exception error)
    {
        EditorApplication.update-=Tick;File.WriteAllText(Output,(error==null?"PASS":"FAIL: "+error)+"\n"+string.Join("\n",results));
        if(error==null)Debug.Log($"적 AP AI 검증 {results.Count}개 통과");else Debug.LogError("적 AP AI 검증 실패: "+error);
        (routine as IDisposable)?.Dispose();routine=null;EditorApplication.isPlaying=false;
    }
    /// <summary>실제 씬의 이동·피해 이벤트와 턴 조정자를 실행한다.</summary>
    private static IEnumerator RuntimeTests()
    {
        Application.runInBackground=true;var queue=ActionPresentationQueue.Instance;double deadline=EditorApplication.timeSinceStartup+30;
        while(queue.IsPlaying&&EditorApplication.timeSinceStartup<deadline)yield return .1f;
        Check(!queue.IsPlaying,"시작 연출 완료");
        // 아트 확인용으로 꺼 둔 적도 저장 없이 플레이 검증에서만 활성화한다.
        foreach(var candidate in UnityEngine.Object.FindObjectsByType<EnemyContext>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            for(var node=candidate.transform;node!=null;node=node.parent)node.gameObject.SetActive(true);
        yield return .2f;
        var enemies=EnemyRegistry.Instance.Enemies.Where(e=>e!=null&&e.IsAlive).ToArray();
        var targets=new EnemyTargetProvider().Collect();Check(enemies.Length>0&&targets.Count>0,"실제 적·플레이어 등록과 표적 관문");
        var enemy=enemies[0];var player=targets[0];var grid=GridManager.Instance;
        var data=UnityEngine.Object.Instantiate(enemy.EnemyData);Set(enemy,"enemyData",data);
        // 데이터 에셋 원본은 건드리지 않고 플레이 인스턴스만 조정한다.
        data.Combat.allowMelee=true;data.Combat.allowRanged=false;data.Combat.dangerWeight=0;data.Combat.coverWeight=0;data.Combat.distanceWeight=0;data.Combat.approachWeight=1;
        foreach(var map in new[]{grid.WallLogicTilemap,grid.LowObstacleLogicTilemap,grid.BoundaryLogicTilemap})
            for(int x=2;x<=14;x++)for(int y=2;y<=8;y++)map.SetTile(map.WorldToCell(grid.GridToWorld(new GridPosition(x,y))),null);
        Check(player.GridActor.TryMoveTo(new(10,5))&&enemy.GridActor.TryMoveTo(new(5,5)),"임시 5칸 접근 전장 배치");
        foreach(var other in targets.Skip(1))other.gameObject.SetActive(false);
        Set(player.Health,"maxHitPoint",100);Set(player.Health,"currentHitPoint",100);
        foreach(var e in enemies)Set(e.AlertState,"currentState",EnemyAwarenessState.Alerted);
        enemy.ActionPoint.RefillForTurn();enemy.TurnAgent.BeginTurn();int hp=player.Health.CurrentHitPoint;int actions=0;
        var timer=System.Diagnostics.Stopwatch.StartNew();
        while(enemy.ActionPoint.Current>0)
        {
            int before=enemy.ActionPoint.Current;var context=new ActionResolutionContext(queue);
            if(!enemy.TurnAgent.TryExecuteTurn(context))break;context.Resolve();queue.ClearQueuedEvents();actions++;
            Check(enemy.ActionPoint.Current<before,"실행 행동 AP 감소 #"+actions);
        }
        timer.Stop();results.Add("실제 보드 3AP 판단·논리 실행 총 시간: "+timer.ElapsedMilliseconds+"ms");
        Check(actions==3&&enemy.TurnAgent.AttacksUsed==1&&player.Health.CurrentHitPoint==hp-data.Combat.meleeDamage,"실제 2회 이동 + 근접 피해 + 공격 한도");
        data.Combat.maximumAttacks=2;enemy.ActionPoint.RefillForTurn();enemy.TurnAgent.BeginTurn();hp=player.Health.CurrentHitPoint;
        for(int i=0;i<3;i++){var context=new ActionResolutionContext(queue);if(!enemy.TurnAgent.TryExecuteTurn(context))break;context.Resolve();queue.ClearQueuedEvents();}
        Check(enemy.TurnAgent.AttacksUsed==2&&player.Health.CurrentHitPoint==hp-data.Combat.meleeDamage*2,"실제 엘리트 두 번 공격, 매번 피해 확정");
        Check(enemy.GridActor.TryMoveTo(new(5,5))&&player.GridActor.TryMoveTo(new(9,5)),"장애물 사격 전장 배치");
        data.Combat.allowRanged=true;
        var tile=ScriptableObject.CreateInstance<Tile>();var wall=new GridPosition(7,5);grid.WallLogicTilemap.SetTile(grid.WallLogicTilemap.WorldToCell(grid.GridToWorld(wall)),tile);
        foreach(var other in enemies.Skip(1))other.gameObject.SetActive(false);
        Check(!enemy.AttackAction.CanAttack(player.GridActor),"높은 벽 너머 미관측 표적 사격 차단");
        var ally=enemies.Skip(1).FirstOrDefault();
        if(ally!=null)
        {
            ally.gameObject.SetActive(true);Check(ally.GridActor.TryMoveTo(new(9,7)),"합산 시야 검증용 아군 배치");
            Check(enemy.AttackAction.CanAttack(player.GridActor),"플레이어처럼 아군이 관측한 표적의 사격 허용");
            ally.gameObject.SetActive(false);
        }
        grid.WallLogicTilemap.SetTile(grid.WallLogicTilemap.WorldToCell(grid.GridToWorld(wall)),null);
        Check(enemy.AttackAction.CanAttack(player.GridActor),"벽 제거 후 사격 허용");
        grid.LowObstacleLogicTilemap.SetTile(grid.LowObstacleLogicTilemap.WorldToCell(grid.GridToWorld(wall)),tile);
        Check(enemy.AttackAction.CanAttack(player.GridActor),"낮은 장애물은 사격 시야를 차단하지 않음");
        Check(new ExcludePlayer().Collect().Count==0,"은신 예외 관문에서 제외하면 추적·공격·위험 목록 모두 비움");
        data.Combat.allowMelee=false;data.Combat.maximumAttacks=1;
        Set(data.RangedAttackAccuracy,"baseHitChance",100);Set(data.RangedAttackAccuracy,"minimumHitChance",100);Set(data.RangedAttackAccuracy,"maximumHitChance",100);
        enemy.ActionPoint.RefillForTurn();enemy.TurnAgent.BeginTurn();hp=player.Health.CurrentHitPoint;
        var rangedContext=new ActionResolutionContext(queue);
        Check(enemy.TurnAgent.TryExecuteTurn(rangedContext),"실제 원거리 전용 행동 실행");rangedContext.Resolve();queue.ClearQueuedEvents();
        Check(player.Health.CurrentHitPoint==hp-data.RangedAttackDamage&&enemy.ActionPoint.Current==2&&enemy.TurnAgent.AttacksUsed==1,"실제 원거리 명중·피해·1AP·공격 횟수 연결");
        // 실제 턴 조정자와 연출을 끝까지 실행해 다음 플레이어 턴 복귀를 확인한다.
        data.Combat.maximumAttacks=1;data.Combat.allowRanged=false;data.Combat.allowMelee=true;
        Check(enemy.GridActor.TryMoveTo(new(8,5)),"조정자 검증용 근접 위치");
        int initialHP=player.Health.CurrentHitPoint;TurnManager.Instance.StartEnemyTurn();
        deadline=EditorApplication.timeSinceStartup+45;
        while((TurnManager.Instance.IsEnemyTurn||queue.IsPlaying)&&EditorApplication.timeSinceStartup<deadline)yield return .1f;
        Check(TurnManager.Instance.IsPlayerTurn&&!queue.IsPlaying,"실제 적 턴 → 연출 완료 → 플레이어 턴 복귀");
        Check(enemy.TurnAgent.AttacksUsed==1&&player.Health.CurrentHitPoint==initialHP-data.Combat.meleeDamage,"조정자에서 공격 횟수 초기화 및 한 번 공격");
        data.Combat.maximumAttacks=2;Set(player.Health,"currentHitPoint",data.Combat.meleeDamage);
        enemy.ActionPoint.RefillForTurn();enemy.TurnAgent.BeginTurn();
        var lethalContext=new ActionResolutionContext(queue);
        Check(enemy.TurnAgent.TryExecuteTurn(lethalContext),"마지막 표적 처치 공격 실행");lethalContext.Resolve();queue.ClearQueuedEvents();
        Check(player.Health.IsDead&&new EnemyTargetProvider().Collect().Count==0,"피해 확정 직후 사망 대상 제외");
        Check(!enemy.TurnAgent.TryExecuteTurn(new ActionResolutionContext(queue))&&enemy.ActionPoint.Current==2,"남은 AP·공격 횟수가 있어도 죽은 표적 재공격 금지");
    }
    /// <summary>테스트에서만 비공개 상태를 배치한다.</summary>
    private static void Set(object target,string field,object value)=>target.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);
    /// <summary>검증 조건과 통과 내역을 기록한다.</summary>
    private static void Check(bool condition,string label){if(!condition)throw new Exception(label);results.Add("통과: "+label);}
    /// <summary>전투 대상 제외 확장 지점을 검증한다.</summary>
    private sealed class ExcludePlayer:EnemyTargetProvider{public override bool IsEligible(TacticalUnitContext unit)=>false;}
    /// <summary>복잡한 실제 씬과 독립된 작은 선형 테스트 전장이다.</summary>
    private sealed class World:IEnemyPlanningWorld
    {
        public int[] positions={1},health={20};public bool melee,ranged;public int meleeDamage=2,rangedDamage=2;
        public int coverAt=99,threatAt=99;public float chance=1;
        public GridPosition Start=>new(0,0);public int TargetCount=>positions.Length;
        public int Health(int i)=>health[i];public int MaxHealth(int i)=>20;public GridPosition TargetPosition(int i)=>new(positions[i],0);
        public IEnumerable<EnemyPlannedAction> Moves(GridPosition from,int range,int cost)
        {
            for(int direction=-1;direction<=1;direction+=2)
            {
                var path=new List<GridPosition>();
                for(int step=1;step<=range;step++)
                {
                    int x=from.x+direction*step;if(x < -6||x>15||positions.Contains(x))break;
                    path.Add(new(x,0));yield return new(EnemyPlannedActionKind.Move,new(x,0),-1,cost,path.ToArray());
                }
            }
        }
        public bool CanAttack(GridPosition from,int i,EnemyPlannedActionKind kind)=>kind==EnemyPlannedActionKind.Melee?melee&&Math.Abs(from.x-positions[i])==1:ranged&&Math.Abs(from.x-positions[i])<=6;
        public float HitProbability(GridPosition from,int i,EnemyPlannedActionKind kind)=>kind==EnemyPlannedActionKind.Melee?1:chance;
        public int Damage(EnemyPlannedActionKind kind)=>kind==EnemyPlannedActionKind.Melee?meleeDamage:rangedDamage;
        public float Cover(GridPosition p,int i)=>p.x==coverAt?1:0;
        public float Threat(GridPosition p,int i)=>p.x==threatAt?1:0;
        public int DistanceToAttack(GridPosition p,int i)=>Math.Max(0,Math.Abs(p.x-positions[i])-(ranged?6:1));
    }
}
