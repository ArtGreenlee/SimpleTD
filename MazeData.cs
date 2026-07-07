using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "MazeData", menuName = "Scriptable Objects/MazeData")]
public class MazeData : ScriptableObject
{
	[System.Serializable]
	public struct BlockedCell
	{
		public int x;
		public int y;

		public BlockedCell(int x, int y)
		{
			this.x = x;
			this.y = y;
		}
	}

	[System.Serializable]
	public struct WallPlacementCell
	{
		public int x;
		public int y;
		public bool towerPlacementEnabled;

		public WallPlacementCell(int x, int y, bool towerPlacementEnabled)
		{
			this.x = x;
			this.y = y;
			this.towerPlacementEnabled = towerPlacementEnabled;
		}
	}

	[Header("Grid Snapshot")]
	public int gridWidth;
	public int gridHeight;

	[Header("Maze Bounds")]
	public bool hasMazeBounds;
	public RectInt mazeBounds;

	[Header("Blocked Cells")]
	public List<BlockedCell> blockedCells = new List<BlockedCell>(1024);

	[Header("Wall Placement Rules")]
	public List<WallPlacementCell> wallPlacementCells = new List<WallPlacementCell>(1024);

	[Header("Spawn/Goal Snapshot")]
	public List<Vector3> spawnPositions = new List<Vector3>(16);
	public Vector3 goalPosition;
	public bool hasGoalPosition;

	[Header("Debug")]
	public int seed;

	public void Clear()
	{
		gridWidth = 0;
		gridHeight = 0;
		hasMazeBounds = false;
		mazeBounds = default;
		seed = 0;
		blockedCells.Clear();
		wallPlacementCells.Clear();
		spawnPositions.Clear();
		goalPosition = default;
		hasGoalPosition = false;
	}
}
