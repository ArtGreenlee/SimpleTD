using UnityEngine;

public class MazeGenerator : MonoBehaviour
{
    public MazeData mazeData => mazeDataAsset;

    [Header("References")]
    [SerializeField] private GridManager grid;
    [SerializeField] private Pathfinding pathfinding;
    [SerializeField] private MazeData mazeDataAsset;
    [Header("Maze Settings")]
    [SerializeField] private GridManager.MazeBuildMode mazeBuildMode = GridManager.MazeBuildMode.PerfectMaze;
    [Tooltip("Random seed. If 0, a random seed is used when generating a new maze.")]
    [SerializeField] private int seed = 0;
    [Tooltip("Corridor width in grid cells.")]
    [Min(1)]
    [SerializeField] private int corridorWidth = 1;
    [Tooltip("Wall thickness in grid cells.")]
    [Min(1)]
    [SerializeField] private int wallThickness = 1;

    [Header("Island Mode")]
    [Tooltip("Maximum number of single-cell island walls placed inside the border.")]
    [SerializeField] private int islandModeMaxCells = 24;
    [Tooltip("Minimum poisson-disc spacing between island wall cells, measured in grid cells.")]
    [SerializeField] private float islandModeMinCellSpacing = 2f;
    [Tooltip("Minimum distance in grid cells that island walls must stay away from the outer border.")]
    [SerializeField, Min(0f)] private float islandModeMinDistanceFromBorder = 1f;
    [Tooltip("Number of poisson-disc candidate attempts generated from each active sample.")]
    [SerializeField, Min(1)] private int islandModeRejectionSamples = 30;

    [Header("Wall Placement Selection")]
    [Tooltip("World-space range used to score a wall by nearby empty cells inside the field.")]
    [SerializeField, Min(0f)] private float towerPlacementScoreRange = 4f;
    [Tooltip("Amount subtracted from nearby wall scores after one wall is selected.")]
    [SerializeField, Min(0f)] private float towerPlacementNeighborPenalty = 8f;
    [Tooltip("Maximum number of wall cells that will allow tower placement.")]
    [SerializeField, Min(0)] private int enabledWallPlacementCount = 12;
    [Tooltip("If true, border walls can be included when selecting walls to enable tower placement on.")]
    [SerializeField] private bool includeborderWallsInTowerEnablementSelection = true;

    [Header("Rendering")]
    [SerializeField] private Material meshMaterial;
    [SerializeField] private Material meshPlacementDisabledMaterial;

    private void Awake()
    {
        ConfigureGrid();
    }

    private void OnEnable()
    {
        ConfigureGrid();
    }

    private void Start()
    {
        if (!Application.isPlaying) return;
        ConfigureGrid();
    }

    [ContextMenu("Generate Maze")]
    public void GenerateMaze()
    {
        if (!TryGetConfiguredGrid(out var configuredGrid)) return;
        configuredGrid.GenerateMaze();
    }

    [ContextMenu("Save Current MazeData To Public Variable")]
    public void SaveCurrentMazeDataToPublicVariable()
    {
        if (!TryGetConfiguredGrid(out var configuredGrid)) return;
        configuredGrid.SaveCurrentMazeDataToPublicVariable();
    }

    [ContextMenu("Load Current MazeData From Public Variable")]
    public void LoadCurrentMazeDataFromPublicVariable()
    {
        if (!TryGetConfiguredGrid(out var configuredGrid)) return;
        configuredGrid.LoadCurrentMazeDataFromPublicVariable();
    }

    [ContextMenu("Clear Generated Maze")]
    public void ClearGeneratedWalls()
    {
        if (!TryGetConfiguredGrid(out var configuredGrid)) return;
        configuredGrid.ClearGeneratedWalls();
    }

    public Vector3 GetNearestCenterOfCorridor(Vector3 pos)
    {
        if (!TryGetConfiguredGrid(out var configuredGrid)) return pos;
        return configuredGrid.GetNearestCenterOfCorridor(pos);
    }

    private void ConfigureGrid()
    {
        if (grid == null) grid = GridManager.instance != null ? GridManager.instance : FindFirstObjectByType<GridManager>();
        if (pathfinding == null) pathfinding = FindFirstObjectByType<Pathfinding>();
        if (grid == null) return;

        grid.ConfigureMaze(pathfinding, mazeDataAsset, mazeBuildMode, seed, corridorWidth, wallThickness, islandModeMaxCells, islandModeMinCellSpacing, islandModeMinDistanceFromBorder, islandModeRejectionSamples, towerPlacementScoreRange, towerPlacementNeighborPenalty, enabledWallPlacementCount, includeborderWallsInTowerEnablementSelection, meshMaterial, meshPlacementDisabledMaterial);
    }

    private bool TryGetConfiguredGrid(out GridManager configuredGrid)
    {
        ConfigureGrid();
        configuredGrid = grid;
        if (configuredGrid != null) return true;

        Debug.LogError("MazeGenerator requires a GridManager reference (or one in scene).", this);
        return false;
    }
}
