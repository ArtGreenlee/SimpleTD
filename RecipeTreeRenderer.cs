using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
public class RecipeTreeRenderer : MonoBehaviour
{
    private class RecipeLineVisual
    {
        public Tower.ID recipeResult;
        public LineRenderer lineRenderer;
        public SpriteRenderer spriteRenderer;
        public Renderer renderer;
        public Color startColor;
        public Color endColor;
    }

    private Bounds bounds;
    private Image panelImage;
    private Dictionary<Tower.ID, Vector3> treePositions;
    private readonly List<GameObject> spawnedTowerVisuals = new List<GameObject>();
    private readonly Dictionary<Tower.ID, GameObject> spawnedTowerById = new Dictionary<Tower.ID, GameObject>();
    private readonly List<GameObject> spawnedTestObjects = new List<GameObject>();
    private readonly List<GameObject> spawnedConnectionLines = new List<GameObject>();
    private readonly List<RecipeLineVisual> spawnedRecipeLineVisuals = new List<RecipeLineVisual>();
    private readonly Dictionary<Tower.ID, List<RecipeLineVisual>> recipeLinesByResult = new Dictionary<Tower.ID, List<RecipeLineVisual>>();
    private readonly Dictionary<Tower.ID, HashSet<Tower.ID>> recipesByTower = new Dictionary<Tower.ID, HashSet<Tower.ID>>();

    public GameObject testObjectPrefab;
    public GameObject lineObject;

    [Header("Line Highlighting")]
    [Range(0f, 1f)] public float defaultLineAlpha = 0.2f;
    [Range(0f, 1f)] public float highlightedLineAlpha = 0.95f;

    [Header("Recipe Panel Sorting")]
    [Tooltip("Added to renderer sorting order so recipe towers/outlines draw above the panel canvas.")]
    [SerializeField] private int towerSortingOrderOffset = 10;
    [Tooltip("Added to renderer sorting order for recipe connection lines.")]
    [SerializeField] private int lineSortingOrderOffset = 11;

    [Header("Connection Routing")]
    public int routingGridColumns = 48;
    public int routingGridRows = 24;

    private bool recipeDisplayVisible;

    void Awake()
    {
        panelImage = GetComponent<Image>();
        var rectTransform = panelImage.rectTransform;
        var corners = new Vector3[4];
        rectTransform.GetWorldCorners(corners);

        bounds = new Bounds(corners[0], Vector3.zero);
        for (int i = 1; i < corners.Length; i++)
        {
            bounds.Encapsulate(corners[i]);
        }
    }

    public void ShowRecipeDisplay()
    {
        SetRecipeDisplayVisible(true);
    }

    public void HideRecipeDisplay()
    {
        SetRecipeDisplayVisible(false);
    }

    // Backward-compatible typo alias.
    public void HideRecipeDispaly()
    {
        HideRecipeDisplay();
    }

    void Start()
    {
        treePositions = GenerateRecipeTreePositions();
        SpawnTowersInPositions();
        SpawnRecipeConnectionLines();
        HideRecipeDispaly();
    }

    void Update()
    {
        if (recipeDisplayVisible && ShouldHideRecipeDisplay())
        {
            HideRecipeDisplay();
            return;
        }

        if (!recipeDisplayVisible) return;
        UpdateRecipeLineHighlighting();
    }

    private void SpawnTowersInPositions() 
    {
        // Clear previous visuals if this is called again.
        for (int i = spawnedTowerVisuals.Count - 1; i >= 0; i--)
        {
            if (spawnedTowerVisuals[i] != null)
            {
                Destroy(spawnedTowerVisuals[i]);
            }
        }
        spawnedTowerVisuals.Clear();
        spawnedTowerById.Clear();

        if (treePositions == null || treePositions.Count == 0) return;
        if (TowerManager.instance == null || TowerManager.instance.towerPrefabDictionary == null) return;

        // Stable order keeps visual spawning deterministic.
        var orderedNodes = treePositions
            .OrderByDescending(kvp => kvp.Value.y)
            .ThenBy(kvp => kvp.Value.x)
            .ToList();

        for (int i = 0; i < orderedNodes.Count; i++)
        {
            Tower.ID towerId = orderedNodes[i].Key;
            Vector3 spawnPosition = orderedNodes[i].Value;

            if (!TowerManager.instance.towerPrefabDictionary.TryGetValue(towerId, out var prefab) || prefab == null)
            {
                continue;
            }

            GameObject spawned = Instantiate(prefab, spawnPosition, Quaternion.identity);
            spawned.name = "RecipeTree_" + towerId;

            // These are display-only nodes; keep them non-interactive and non-gameplay.
            Tower tower = spawned.GetComponent<Tower>();
            if (tower != null)
            {
                tower.CurrentState = Tower.State.Shop;
            }
            ApplyRenderSortingRelativeToPanel(spawned, towerSortingOrderOffset);

            TowerInteractable interactable = spawned.GetComponent<TowerInteractable>();
            if (interactable != null)
            {
                interactable.pickupable = false;

            }

            //Collider2D[] colliders = spawned.GetComponentsInChildren<Collider2D>(true);
            //for (int c = 0; c < colliders.Length; c++)
            //{
            //    if (colliders[c] != null) colliders[c].enabled = false;
            //}

            spawnedTowerVisuals.Add(spawned);
            spawnedTowerById[towerId] = spawned;
        }
    }

    private Dictionary<Tower.ID, Vector3> GenerateRecipeTreePositions()
    {
        RecipeManager recipeManager = RecipeManager.instance;
        var result = new Dictionary<Tower.ID, Vector3>();

        var recipeDictionary = recipeManager != null ? recipeManager.RecipeDictionary : null;
        if (recipeDictionary == null || recipeDictionary.Count == 0)
        {
            return result;
        }

        // Build graph edges: ingredient -> results, and result -> ingredients.
        var allNodes = new HashSet<Tower.ID>();
        var outgoing = new Dictionary<Tower.ID, HashSet<Tower.ID>>();
        var incoming = new Dictionary<Tower.ID, HashSet<Tower.ID>>();
        var incomingCount = new Dictionary<Tower.ID, int>();

        foreach (var kvp in recipeDictionary)
        {
            Tower.ID resultTower = kvp.Key;
            List<Tower.ID> requiredTowers = kvp.Value;

            allNodes.Add(resultTower);

            if (requiredTowers == null) continue;

            for (int j = 0; j < requiredTowers.Count; j++)
            {
                Tower.ID ingredient = requiredTowers[j];
                allNodes.Add(ingredient);

                if (!outgoing.TryGetValue(ingredient, out var children))
                {
                    children = new HashSet<Tower.ID>();
                    outgoing[ingredient] = children;
                }

                if (!incoming.TryGetValue(resultTower, out var parents))
                {
                    parents = new HashSet<Tower.ID>();
                    incoming[resultTower] = parents;
                }

                if (children.Add(resultTower))
                {
                    parents.Add(ingredient);
                    if (!incomingCount.ContainsKey(resultTower)) incomingCount[resultTower] = 0;
                    incomingCount[resultTower]++;
                }
            }
        }

        foreach (var node in allNodes)
        {
            if (!outgoing.ContainsKey(node)) outgoing[node] = new HashSet<Tower.ID>();
            if (!incoming.ContainsKey(node)) incoming[node] = new HashSet<Tower.ID>();
            if (!incomingCount.ContainsKey(node)) incomingCount[node] = 0;
        }

        // Kahn topological pass to assign depth.
        var queue = new Queue<Tower.ID>();
        var nodeDepth = new Dictionary<Tower.ID, int>();
        var incomingRemaining = new Dictionary<Tower.ID, int>(incomingCount);

        foreach (var node in allNodes)
        {
            if (incomingRemaining[node] == 0)
            {
                queue.Enqueue(node);
                nodeDepth[node] = 0;
            }
        }

        while (queue.Count > 0)
        {
            Tower.ID node = queue.Dequeue();
            int depth = nodeDepth[node];

            foreach (var child in outgoing[node])
            {
                int candidateDepth = depth + 1;
                if (!nodeDepth.ContainsKey(child) || candidateDepth > nodeDepth[child])
                {
                    nodeDepth[child] = candidateDepth;
                }

                incomingRemaining[child]--;
                if (incomingRemaining[child] == 0)
                {
                    queue.Enqueue(child);
                }
            }
        }

        foreach (var node in allNodes)
        {
            if (!nodeDepth.ContainsKey(node)) nodeDepth[node] = 0;
        }

        int maxDepth = 0;
        foreach (var kvp in nodeDepth)
        {
            if (kvp.Value > maxDepth) maxDepth = kvp.Value;
        }

        var levels = new Dictionary<int, List<Tower.ID>>();
        foreach (var kvp in nodeDepth)
        {
            if (!levels.TryGetValue(kvp.Value, out var list))
            {
                list = new List<Tower.ID>();
                levels[kvp.Value] = list;
            }
            list.Add(kvp.Key);
        }

        // Initial alphabetical ordering as baseline.
        for (int depth = 0; depth <= maxDepth; depth++)
        {
            if (levels.TryGetValue(depth, out var list))
            {
                list.Sort((a, b) => a.ToString().CompareTo(b.ToString()));
            }
        }

        // Barycenter heuristic: reorder each level to minimize crossings.
        var nodePosition = new Dictionary<Tower.ID, float>();

        void AssignPositionIndices(int depth)
        {
            if (!levels.TryGetValue(depth, out var list) || list.Count == 0) return;
            for (int i = 0; i < list.Count; i++)
            {
                nodePosition[list[i]] = list.Count <= 1 ? 0.5f : (float)i / (list.Count - 1);
            }
        }

        for (int depth = 0; depth <= maxDepth; depth++)
        {
            AssignPositionIndices(depth);
        }

        // Count crossings between two adjacent levels given current ordering.
        int CountCrossings(int upperDepth, int lowerDepth)
        {
            if (!levels.TryGetValue(upperDepth, out var upper) || !levels.TryGetValue(lowerDepth, out var lower))
                return 0;

            // Build list of edges as (upper index, lower index).
            var edges = new List<(int u, int l)>();
            var upperIndex = new Dictionary<Tower.ID, int>();
            var lowerIndex = new Dictionary<Tower.ID, int>();
            for (int i = 0; i < upper.Count; i++) upperIndex[upper[i]] = i;
            for (int i = 0; i < lower.Count; i++) lowerIndex[lower[i]] = i;

            foreach (var uNode in upper)
            {
                foreach (var child in outgoing[uNode])
                {
                    if (lowerIndex.ContainsKey(child))
                    {
                        edges.Add((upperIndex[uNode], lowerIndex[child]));
                    }
                }
            }

            int crossings = 0;
            for (int i = 0; i < edges.Count; i++)
            {
                for (int j = i + 1; j < edges.Count; j++)
                {
                    if ((edges[i].u < edges[j].u && edges[i].l > edges[j].l) ||
                        (edges[i].u > edges[j].u && edges[i].l < edges[j].l))
                    {
                        crossings++;
                    }
                }
            }
            return crossings;
        }

        // Run barycenter sweeps followed by adjacent-swap refinement.
        for (int iter = 0; iter < 8; iter++)
        {
            // Forward sweep: order each level by barycenter of its parents.
            for (int depth = 1; depth <= maxDepth; depth++)
            {
                if (!levels.TryGetValue(depth, out var nodesAtLevel) || nodesAtLevel.Count <= 1) continue;

                var barycenters = new Dictionary<Tower.ID, float>();
                for (int i = 0; i < nodesAtLevel.Count; i++)
                {
                    Tower.ID node = nodesAtLevel[i];
                    var parents = incoming[node];
                    if (parents.Count == 0)
                    {
                        barycenters[node] = nodePosition.TryGetValue(node, out float p) ? p : 0.5f;
                        continue;
                    }
                    float sum = 0f;
                    foreach (var parent in parents)
                    {
                        sum += nodePosition.TryGetValue(parent, out float pos) ? pos : 0.5f;
                    }
                    barycenters[node] = sum / parents.Count;
                }

                nodesAtLevel.Sort((a, b) => barycenters[a].CompareTo(barycenters[b]));
                AssignPositionIndices(depth);
            }

            // Backward sweep: order each level by barycenter of its children.
            for (int depth = maxDepth - 1; depth >= 0; depth--)
            {
                if (!levels.TryGetValue(depth, out var nodesAtLevel) || nodesAtLevel.Count <= 1) continue;

                var barycenters = new Dictionary<Tower.ID, float>();
                for (int i = 0; i < nodesAtLevel.Count; i++)
                {
                    Tower.ID node = nodesAtLevel[i];
                    var children = outgoing[node];
                    if (children.Count == 0)
                    {
                        barycenters[node] = nodePosition.TryGetValue(node, out float p) ? p : 0.5f;
                        continue;
                    }
                    float sum = 0f;
                    foreach (var child in children)
                    {
                        sum += nodePosition.TryGetValue(child, out float pos) ? pos : 0.5f;
                    }
                    barycenters[node] = sum / children.Count;
                }

                nodesAtLevel.Sort((a, b) => barycenters[a].CompareTo(barycenters[b]));
                AssignPositionIndices(depth);
            }

            // Adjacent-swap refinement: swap neighbours if it reduces crossings.
            for (int depth = 0; depth <= maxDepth; depth++)
            {
                if (!levels.TryGetValue(depth, out var nodesAtLevel) || nodesAtLevel.Count <= 1) continue;

                bool improved = true;
                while (improved)
                {
                    improved = false;
                    for (int i = 0; i < nodesAtLevel.Count - 1; i++)
                    {
                        int crossBefore = 0;
                        if (depth > 0) crossBefore += CountCrossings(depth - 1, depth);
                        if (depth < maxDepth) crossBefore += CountCrossings(depth, depth + 1);

                        // Swap.
                        var tmp = nodesAtLevel[i];
                        nodesAtLevel[i] = nodesAtLevel[i + 1];
                        nodesAtLevel[i + 1] = tmp;
                        AssignPositionIndices(depth);

                        int crossAfter = 0;
                        if (depth > 0) crossAfter += CountCrossings(depth - 1, depth);
                        if (depth < maxDepth) crossAfter += CountCrossings(depth, depth + 1);

                        if (crossAfter < crossBefore)
                        {
                            improved = true;
                        }
                        else
                        {
                            // Swap back.
                            nodesAtLevel[i + 1] = nodesAtLevel[i];
                            nodesAtLevel[i] = tmp;
                            AssignPositionIndices(depth);
                        }
                    }
                }
            }
        }

        // Convert level/position indices to world positions.
        float width = Mathf.Max(0.0001f, bounds.size.x);
        float height = Mathf.Max(0.0001f, bounds.size.y);
        float xPadding = width * 0.08f;
        float yPadding = height * 0.08f;

        float xMin = bounds.min.x + xPadding;
        float xMax = bounds.max.x - xPadding;
        float yTop = bounds.max.y - yPadding;
        float yBottom = bounds.min.y + yPadding;
        float z = bounds.center.z;

        for (int depth = 0; depth <= maxDepth; depth++)
        {
            if (!levels.TryGetValue(depth, out var nodesAtLevel) || nodesAtLevel == null || nodesAtLevel.Count == 0)
            {
                continue;
            }

            float tY = maxDepth <= 0 ? 0.5f : (float)depth / maxDepth;
            float y = Mathf.Lerp(yTop, yBottom, tY);

            int count = nodesAtLevel.Count;
            if (count == 1)
            {
                result[nodesAtLevel[0]] = new Vector3((xMin + xMax) * 0.5f, y, z);
                continue;
            }

            for (int i = 0; i < count; i++)
            {
                float tX = (float)i / (count - 1);
                float x = Mathf.Lerp(xMin, xMax, tX);
                result[nodesAtLevel[i]] = new Vector3(x, y, z);
            }
        }

        return result;
    }

    private void SpawnRecipeConnectionLines()
    {
        ClearSpawnedConnectionLines();

        if (lineObject == null) return;
        if (treePositions == null || treePositions.Count == 0) return;

        var recipeManager = RecipeManager.instance;
        var recipeDictionary = recipeManager != null ? recipeManager.RecipeDictionary : null;
        if (recipeDictionary == null || recipeDictionary.Count == 0) return;

        foreach (var kvp in recipeDictionary)
        {
            Tower.ID resultTower = kvp.Key;
            var requiredTowers = kvp.Value;

            if (requiredTowers == null || requiredTowers.Count == 0) continue;

            var seenIngredients = new HashSet<Tower.ID>();
            for (int i = 0; i < requiredTowers.Count; i++)
            {
                Tower.ID ingredient = requiredTowers[i];
                if (!seenIngredients.Add(ingredient)) continue;
                if (!treePositions.ContainsKey(ingredient) || !treePositions.ContainsKey(resultTower)) continue;

                Vector3 startPos = treePositions[ingredient];
                Vector3 endPos = treePositions[resultTower];
                SpawnConnectionSegment(startPos, endPos, resultTower);
            }
        }

        UpdateRecipeLineHighlighting();
    }

    private void AddTowerRecipeInvolvement(Tower.ID towerId, Tower.ID recipeResult)
    {
        if (!recipesByTower.TryGetValue(towerId, out var recipeSet))
        {
            recipeSet = new HashSet<Tower.ID>();
            recipesByTower[towerId] = recipeSet;
        }
        recipeSet.Add(recipeResult);
    }

    private bool IsTowerHighlighted(Tower.ID towerId)
    {
        if (!spawnedTowerById.TryGetValue(towerId, out var towerObject) || towerObject == null)
        {
            return false;
        }

        var interactable = towerObject.GetComponent<TowerInteractable>();
        if (interactable == null)
        {
            return false;
        }

        if (interactable.IsClicked())
        {
            return true;
        }

        var outlines = interactable.outlines;
        if (outlines == null) return false;

        for (int i = 0; i < outlines.Count; i++)
        {
            if (outlines[i] != null && outlines[i].enabled)
            {
                return true;
            }
        }

        return false;
    }

    private void UpdateRecipeLineHighlighting()
    {
        if (spawnedRecipeLineVisuals.Count == 0) return;

        var highlightedRecipeResults = new HashSet<Tower.ID>();

        foreach (var kvp in spawnedTowerById)
        {
            if (!IsTowerHighlighted(kvp.Key)) continue;
            if (!recipeLinesByResult.ContainsKey(kvp.Key)) continue;

            // Only highlight this tower's own recipe lines (its ingredients -> this result).
            highlightedRecipeResults.Add(kvp.Key);
        }

        bool anyHighlighted = highlightedRecipeResults.Count > 0;
        for (int i = 0; i < spawnedRecipeLineVisuals.Count; i++)
        {
            var line = spawnedRecipeLineVisuals[i];
            float alpha = (anyHighlighted && highlightedRecipeResults.Contains(line.recipeResult))
                ? highlightedLineAlpha
                : defaultLineAlpha;

            ApplyLineAlpha(line, alpha);
        }
    }

    private void ApplyLineAlpha(RecipeLineVisual line, float alpha)
    {
        float clampedAlpha = Mathf.Clamp01(alpha);

        if (line.lineRenderer != null)
        {
            Color start = line.startColor;
            Color end = line.endColor;
            start.a = clampedAlpha;
            end.a = clampedAlpha;
            line.lineRenderer.startColor = start;
            line.lineRenderer.endColor = end;
            return;
        }

        if (line.spriteRenderer != null)
        {
            Color c = line.startColor;
            c.a = clampedAlpha;
            line.spriteRenderer.color = c;
            return;
        }

        if (line.renderer != null && line.renderer.material != null && line.renderer.material.HasProperty("_Color"))
        {
            Color c = line.startColor;
            c.a = clampedAlpha;
            line.renderer.material.color = c;
        }
    }

    private void SpawnConnectionSegment(Vector3 startPos, Vector3 endPos, Tower.ID recipeResult)
    {
        if ((endPos - startPos).sqrMagnitude <= 0.000001f) return;

        GameObject lineInstance = Instantiate(lineObject, transform);
        ApplyRenderSortingRelativeToPanel(lineInstance, lineSortingOrderOffset);

        var lineVisual = new RecipeLineVisual
        {
            recipeResult = recipeResult,
            lineRenderer = lineInstance.GetComponent<LineRenderer>(),
            spriteRenderer = lineInstance.GetComponent<SpriteRenderer>(),
            renderer = lineInstance.GetComponent<Renderer>()
        };

        if (lineVisual.lineRenderer != null)
        {
            lineVisual.lineRenderer.positionCount = 2;
            lineVisual.lineRenderer.SetPosition(0, startPos);
            lineVisual.lineRenderer.SetPosition(1, endPos);
            lineVisual.startColor = lineVisual.lineRenderer.startColor;
            lineVisual.endColor = lineVisual.lineRenderer.endColor;
        }
        else
        {
            Vector3 dir = endPos - startPos;
            lineInstance.transform.position = startPos + (dir * 0.5f);
            lineInstance.transform.right = dir.normalized;

            Vector3 scale = lineInstance.transform.localScale;
            scale.x = dir.magnitude;
            lineInstance.transform.localScale = scale;

            if (lineVisual.spriteRenderer != null)
            {
                lineVisual.startColor = lineVisual.spriteRenderer.color;
                lineVisual.endColor = lineVisual.spriteRenderer.color;
            }
            else if (lineVisual.renderer != null && lineVisual.renderer.material != null && lineVisual.renderer.material.HasProperty("_Color"))
            {
                lineVisual.startColor = lineVisual.renderer.material.color;
                lineVisual.endColor = lineVisual.renderer.material.color;
            }
            else
            {
                lineVisual.startColor = Color.white;
                lineVisual.endColor = Color.white;
            }
        }

        ApplyLineAlpha(lineVisual, defaultLineAlpha);

        spawnedConnectionLines.Add(lineInstance);
        spawnedRecipeLineVisuals.Add(lineVisual);

        if (!recipeLinesByResult.TryGetValue(recipeResult, out var linesForRecipe))
        {
            linesForRecipe = new List<RecipeLineVisual>();
            recipeLinesByResult[recipeResult] = linesForRecipe;
        }
        linesForRecipe.Add(lineVisual);
    }

    [ContextMenu("Spawn Test Objects At Recipe Tree Positions")]
    private void SpawnTestObjectsAtRecipeTreePositions()
    {
        ClearSpawnedTestObjects();

        if (testObjectPrefab == null)
        {
            Debug.LogWarning("RecipeTreeRenderer: testObjectPrefab is not assigned.", this);
            return;
        }

        treePositions = GenerateRecipeTreePositions();
        if (treePositions == null || treePositions.Count == 0) return;

        foreach (var kvp in treePositions)
        {
            GameObject testObject = Instantiate(testObjectPrefab, kvp.Value, Quaternion.identity, transform);
            testObject.name = "test";
            spawnedTestObjects.Add(testObject);
        }
    }

    private void ClearSpawnedTestObjects()
    {
        for (int i = spawnedTestObjects.Count - 1; i >= 0; i--)
        {
            if (spawnedTestObjects[i] != null)
            {
                DestroyImmediate(spawnedTestObjects[i]);
            }
        }
        spawnedTestObjects.Clear();
    }

    private void ClearSpawnedConnectionLines()
    {
        for (int i = spawnedConnectionLines.Count - 1; i >= 0; i--)
        {
            if (spawnedConnectionLines[i] != null)
            {
                Destroy(spawnedConnectionLines[i]);
            }
        }
        spawnedConnectionLines.Clear();
        spawnedRecipeLineVisuals.Clear();
        recipeLinesByResult.Clear();
    }

    private bool ShouldHideRecipeDisplay()
    {
        if (Input.GetMouseButtonDown(1)) return true;
        if (Input.GetKeyDown(KeyCode.Escape)) return true;

        if (Input.GetMouseButtonDown(0) && !IsMouseInsideRecipeTreeBounds())
        {
            return true;
        }

        return false;
    }

    private bool IsMouseInsideRecipeTreeBounds()
    {
        Vector3 mouse = Input.mousePosition;
        mouse.z = bounds.center.z;
        return bounds.Contains(mouse);
    }

    private void SetRecipeDisplayVisible(bool visible)
    {
        recipeDisplayVisible = visible;

        if (panelImage != null)
        {
            panelImage.enabled = visible;
        }

        for (int i = 0; i < spawnedTowerVisuals.Count; i++)
        {
            if (spawnedTowerVisuals[i] != null)
            {
                spawnedTowerVisuals[i].SetActive(visible);
            }
        }

        for (int i = 0; i < spawnedConnectionLines.Count; i++)
        {
            if (spawnedConnectionLines[i] != null)
            {
                spawnedConnectionLines[i].SetActive(visible);
            }
        }

        for (int i = 0; i < spawnedTestObjects.Count; i++)
        {
            if (spawnedTestObjects[i] != null)
            {
                spawnedTestObjects[i].SetActive(visible);
            }
        }
    }

    private void ApplyRenderSortingRelativeToPanel(GameObject root, int orderOffset)
    {
        if (root == null) return;

        int sortingLayerId = 0;
        bool hasLayer = false;

        if (panelImage != null && panelImage.canvas != null)
        {
            Canvas rootCanvas = panelImage.canvas.rootCanvas != null ? panelImage.canvas.rootCanvas : panelImage.canvas;
            sortingLayerId = rootCanvas.sortingLayerID;
            hasLayer = true;
        }

        var renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            var renderer = renderers[i];
            if (renderer == null) continue;

            if (hasLayer)
            {
                renderer.sortingLayerID = sortingLayerId;
            }

            renderer.sortingOrder += orderOffset;
        }
    }
}
