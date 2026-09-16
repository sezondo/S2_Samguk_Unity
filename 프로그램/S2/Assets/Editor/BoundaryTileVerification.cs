using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>임시 미리보기 씬에서 경계의 이동·시야 차단과 엄폐 제외를 검증한다.</summary>
public static class BoundaryTileVerification
{
    /// <summary>실제 맵 배치를 건드리지 않고 경계 타일 및 기존 엄폐 규칙을 검사한다.</summary>
    [MenuItem("Tools/S2/Verify Boundary Tile")]
    public static void Verify()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("경계 검증은 편집 모드에서 실행합니다.");
        var preview = EditorSceneManager.NewPreviewScene();
        try
        {
            Tile tile = AssetDatabase.LoadAssetAtPath<Tile>("Assets/Art/Map/LogicBlockAssets/BoundaryBlockTile.asset");
            Require(tile != null, "경계 타일 에셋");
            var root = new GameObject("경계 검증 전용");
            SceneManager.MoveGameObjectToScene(root, preview);
            root.AddComponent<Grid>();
            Tilemap wall = MakeTilemap(root.transform, "벽");
            Tilemap low = MakeTilemap(root.transform, "낮은 장애물");
            Tilemap boundary = MakeTilemap(root.transform, "경계");
            GridManager grid = root.AddComponent<GridManager>();
            var data = new SerializedObject(grid);
            data.FindProperty("width").intValue = 7;
            data.FindProperty("height").intValue = 7;
            data.FindProperty("cellSize").floatValue = 1f;
            data.FindProperty("wallLogicTilemap").objectReferenceValue = wall;
            data.FindProperty("lowObstacleLogicTilemap").objectReferenceValue = low;
            data.FindProperty("boundaryLogicTilemap").objectReferenceValue = boundary;
            data.ApplyModifiedPropertiesWithoutUndo();

            boundary.SetTile(new Vector3Int(3, 3, 0), tile);
            Rebuild(grid);
            var block = new GridPosition(3, 3);
            var adjacent = new GridPosition(2, 3);
            Directory.CreateDirectory("Temp/BoundarySetup");
            File.WriteAllText("Temp/BoundarySetup/diagnostic.txt", $"world={grid.GridToWorld(block)}, cell={boundary.WorldToCell(grid.GridToWorld(block))}, tile={boundary.HasTile(new Vector3Int(3,3,0))}, boundRef={grid.BoundaryLogicTilemap == boundary}, blocked={grid.IsBlocked(block)}, width={grid.Width}, height={grid.Height}, layout={boundary.layoutGrid}, size={boundary.cellSize}");
            Require(grid.IsBlocked(block) && !grid.CanEnter(block), "경계 이동 차단");
            Require(grid.IsSightBlocked(block), "경계 시야 차단");
            Require(!GridLineOfSight.HasLineOfSight(grid, new GridPosition(1, 3), new GridPosition(5, 3)), "경계 뒤 LOS 차단");
            Require(!grid.IsWall(block) && !grid.IsLowObstacle(block), "엄폐용 벽·장애물 분류 제외");
            Require(!grid.HasAdjacentWall(adjacent) && !grid.HasAdjacentLowObstacle(adjacent), "인접 엄폐 자세 조건 없음");
            Require(!CoverCalculator.Calculate(grid, new GridPosition(5, 3), adjacent, 20, 22.5f, 67.5f).HasCover, "엄폐 명중 보정 없음");

            low.SetTile(new Vector3Int(2, 4, 0), tile);
            wall.SetTile(new Vector3Int(2, 2, 0), tile);
            Rebuild(grid);
            Require(grid.HasAdjacentLowObstacle(adjacent) && grid.HasAdjacentWall(adjacent), "경계와 동시 인접한 실제 엄폐물 유지");
            Require(CoverCalculator.Calculate(grid, new GridPosition(2, 6), adjacent, 20, 22.5f, 67.5f).HasCover, "실제 낮은 엄폐 명중 보정 유지");
            Require(!grid.IsSightBlocked(new GridPosition(2, 4)), "낮은 장애물 시야 통과 유지");

            boundary.ClearAllTiles();
            Rebuild(grid);
            Require(grid.CanEnter(block) && !grid.IsSightBlocked(block), "경계 삭제 시 차단 해제");
            data.Update();
            data.FindProperty("boundaryLogicTilemap").objectReferenceValue = null;
            data.ApplyModifiedPropertiesWithoutUndo();
            Rebuild(grid);
            Require(grid.HasValidReference() && grid.CanEnter(block), "경계를 사용하지 않는 기존 씬 호환");

            GameObject palette = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Map/Palettes/S2_BoundaryPalette.prefab");
            Require(palette != null && palette.GetComponentInChildren<Tilemap>().GetTile(new Vector3Int(-1, 0, 0)) == tile,
                "경계 팔레트 타일 연결");
            Directory.CreateDirectory("Temp/BoundarySetup");
            File.WriteAllText("Temp/BoundarySetup/verification.txt", "PASS: 이동 차단, 시야 차단, 벽 뒤 LOS, 엄폐 분류·자세·명중 보정 제외, 실제 엄폐물 동시 인접 유지, 경계 삭제, 기존 씬 호환, 팔레트 연결");
            Debug.Log("경계 차단 타일 검증 통과");
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(preview);
        }
    }

    /// <summary>검증 전용 그리드 아래에 중심을 정렬한 빈 타일맵을 만든다.</summary>
    private static Tilemap MakeTilemap(Transform parent, string name)
    {
        var child = new GameObject(name);
        child.transform.SetParent(parent, false);
        child.transform.localPosition = new Vector3(-0.5f, -0.5f, 0f);
        return child.AddComponent<Tilemap>();
    }

    /// <summary>검증 배치가 바뀌었을 때 런타임과 같은 보드 상태 생성 함수를 실행한다.</summary>
    private static void Rebuild(GridManager grid)
    {
        typeof(GridManager).GetMethod("RebuildCellStates", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(grid, null);
    }

    /// <summary>검사 실패 원인을 명시한다.</summary>
    private static void Require(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException("경계 타일 검증 실패: " + description);
    }
}
