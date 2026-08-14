using System;
using System.Collections.Generic;

/// <summary>
/// 편대 구성원 하나의 이동 시작점과 실제로 확정된 전체 경로를 보관한다.
/// </summary>
public sealed class GroupMovePresentationMemberSnapshot
{
    // 연출할 논리 Actor다.
    public GridActor Actor { get; }
    // 이번 그룹 이동이 시작된 칸이다.
    public GridPosition StartPosition { get; }
    // 한 칸 단위로 확정된 이동 경로다.
    public IReadOnlyList<GridPosition> Path { get; }

    /// <summary>
    /// 전달받은 경로를 복사해 이후 논리 버퍼 변경의 영향을 받지 않는 구성원 스냅샷을 만든다.
    /// </summary>
    public GroupMovePresentationMemberSnapshot(
        GridActor actor,
        GridPosition startPosition,
        IReadOnlyList<GridPosition> path)
    {
        Actor = actor;
        StartPosition = startPosition;

        GridPosition[] copiedPath = new GridPosition[path?.Count ?? 0];
        for (int i = 0; i < copiedPath.Length; i++)
        {
            copiedPath[i] = path[i];
        }

        Path = Array.AsReadOnly(copiedPath);
    }
}

/// <summary>
/// 편대 하나와 구성원별 실제 이동 경로를 연출 시점까지 고정해 전달한다.
/// </summary>
public sealed class GroupMovePresentationSnapshot
{
    // 이번 이동을 실행한 편대다.
    public EnemyPatrolGroup Group { get; }
    // 편대 등록 순서를 유지하는 구성원별 이동 스냅샷이다.
    public IReadOnlyList<GroupMovePresentationMemberSnapshot> Members { get; }
    // 구성원 중 가장 긴 실제 이동 단계 수다.
    public int StepCount { get; }

    /// <summary>
    /// 편대와 구성원 이동 결과를 복사해 그룹 이동 연출 스냅샷을 만든다.
    /// </summary>
    public GroupMovePresentationSnapshot(
        EnemyPatrolGroup group,
        IReadOnlyList<GroupMovePresentationMemberSnapshot> members)
    {
        Group = group;

        GroupMovePresentationMemberSnapshot[] copiedMembers =
            new GroupMovePresentationMemberSnapshot[members?.Count ?? 0];
        int stepCount = 0;
        for (int i = 0; i < copiedMembers.Length; i++)
        {
            copiedMembers[i] = members[i];
            if (copiedMembers[i]?.Path != null)
            {
                stepCount = Math.Max(stepCount, copiedMembers[i].Path.Count);
            }
        }

        Members = Array.AsReadOnly(copiedMembers);
        StepCount = stepCount;
    }
}

/// <summary>
/// 편대 방향 전환 구성원 하나의 Actor와 논리 방향 변화를 보관한다.
/// </summary>
public sealed class GroupFacingTurnPresentationMemberSnapshot
{
    // 방향 전환을 연출할 논리 Actor다.
    public GridActor Actor { get; }
    // 방향 전환 전 논리 시야 방향이다.
    public GridDirection FromDirection { get; }
    // 방향 전환 후 논리 시야 방향이다.
    public GridDirection ToDirection { get; }

    /// <summary>
    /// 편대 구성원 하나의 방향 전환 결과를 연출 시점까지 고정한다.
    /// </summary>
    public GroupFacingTurnPresentationMemberSnapshot(
        GridActor actor,
        GridDirection fromDirection,
        GridDirection toDirection)
    {
        Actor = actor;
        FromDirection = fromDirection;
        ToDirection = toDirection;
    }
}

/// <summary>
/// 편대 구성원들의 동시 방향 전환 결과를 연출 시점까지 보관한다.
/// </summary>
public sealed class GroupFacingTurnPresentationSnapshot
{
    // 이번 방향 전환을 실행한 편대다.
    public EnemyPatrolGroup Group { get; }
    // 편대 등록 순서를 유지하는 구성원별 방향 전환 결과다.
    public IReadOnlyList<GroupFacingTurnPresentationMemberSnapshot> Members { get; }

    /// <summary>
    /// 편대와 구성원별 방향 변화를 복사해 동시 방향 전환 스냅샷을 만든다.
    /// </summary>
    public GroupFacingTurnPresentationSnapshot(
        EnemyPatrolGroup group,
        IReadOnlyList<GroupFacingTurnPresentationMemberSnapshot> members)
    {
        Group = group;

        GroupFacingTurnPresentationMemberSnapshot[] copiedMembers =
            new GroupFacingTurnPresentationMemberSnapshot[members?.Count ?? 0];
        for (int i = 0; i < copiedMembers.Length; i++)
        {
            copiedMembers[i] = members[i];
        }

        Members = Array.AsReadOnly(copiedMembers);
    }
}
