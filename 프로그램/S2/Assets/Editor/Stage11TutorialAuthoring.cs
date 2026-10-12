using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>1-1 튜토리얼의 실제 맵과 연결을 확인하는 편집용 도구다.</summary>
public static partial class Stage11TutorialAuthoring
{
    /// <summary>저장된 맵을 변경하지 않고 좌표와 주요 연결 정보를 기록한다.</summary>
    [MenuItem("Tools/S2/Inspect 1-1 Tutorial Map")]
    public static void Inspect()
    {
        var grid = UnityEngine.Object.FindFirstObjectByType<GridManager>();
        if (grid == null) throw new InvalidOperationException("1-1 그리드가 필요합니다.");
        typeof(GridManager).GetMethod("RebuildCellStates", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(grid, null);
        var s = new StringBuilder();
        s.AppendLine($"BOARD {grid.Width} {grid.Height}");
        for (int y = grid.Height - 1; y >= 0; y--)
        {
            s.Append(y.ToString("D2") + " ");
            for (int x = 0; x < grid.Width; x++)
            {
                var p = new GridPosition(x, y);
                s.Append(grid.IsWall(p) ? '#' : grid.IsLowObstacle(p) ? 'c' : grid.IsBlocked(p) ? 'X' : '.');
            }
            s.AppendLine();
        }
        foreach (var c in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (c is GridActor || c is EnemyContext || c is TacticalUnitContext || c is StageGoal || c is HackableObject || c.GetType().Name.Contains("SecurityDoor") || c is DialogueManager || c is DialogueBubblePresenter || c is DialogueSpeaker || c is BattleIntroPresenter)
                s.AppendLine(c.GetType().Name + " " + PathOf(c.transform) + " " + EditorJsonUtility.ToJson(c));
        }
        Directory.CreateDirectory("Temp/Stage11Tutorial");
        File.WriteAllText("Temp/Stage11Tutorial/map.txt", s.ToString());
        Debug.Log("1-1 맵 확인 자료를 Temp/Stage11Tutorial/map.txt에 저장했습니다.");
    }

    /// <summary>동일 이름의 액터를 구분할 전체 계층 경로를 만든다.</summary>
    private static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
}
