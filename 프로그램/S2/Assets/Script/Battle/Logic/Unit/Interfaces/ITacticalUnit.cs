/// <summary>
/// 진영 기반 조회와 표적 선정에 필요한 전술 유닛 공통 규약이다.
/// </summary>
public interface ITacticalUnit
{
    UnitFaction Faction { get; }
    GridActor GridActor { get; }
    ActorHealth Health { get; }
    bool IsAlive { get; }
}
