using System.Collections.Generic;
using System.IO;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class SpriteEditor : MonoBehaviour
{
    [SerializeField] private Sprite sourceSprite;
    [SerializeField, Min(0f)] private float filletRadius = 0.05f;
    [SerializeField, Min(1)] private int filletSegments = 4;
    [SerializeField] private string copiedSpriteSuffix = "_Filleted";

#if UNITY_EDITOR
    [ContextMenu("Copy Sprite, Fillet Vertices, Save")]
    public void CopyAndFilletSprite()
    {
        if (sourceSprite == null)
        {
            Debug.LogError("SpriteEditor: sourceSprite is not assigned.", this);
            return;
        }

        string sourcePath = AssetDatabase.GetAssetPath(sourceSprite);
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            Debug.LogError("SpriteEditor: Could not resolve source sprite asset path.", this);
            return;
        }

        string directory = Path.GetDirectoryName(sourcePath)?.Replace('\\', '/');
        string ext = Path.GetExtension(sourcePath);
        string baseName = Path.GetFileNameWithoutExtension(sourcePath);
        if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(ext) || string.IsNullOrWhiteSpace(baseName))
        {
            Debug.LogError("SpriteEditor: Invalid source asset path.", this);
            return;
        }

        string suffix = string.IsNullOrWhiteSpace(copiedSpriteSuffix) ? "_Filleted" : copiedSpriteSuffix;
        string candidatePath = $"{directory}/{baseName}{suffix}{ext}";
        string copiedPath = AssetDatabase.GenerateUniqueAssetPath(candidatePath);

        if (!AssetDatabase.CopyAsset(sourcePath, copiedPath))
        {
            Debug.LogError("SpriteEditor: Failed to copy sprite asset.", this);
            return;
        }

        AssetDatabase.ImportAsset(copiedPath, ImportAssetOptions.ForceUpdate);

        Sprite copiedSprite = LoadCopiedSprite(copiedPath, sourceSprite.name);
        if (copiedSprite == null)
        {
            Debug.LogError("SpriteEditor: Copied sprite was not found after import.", this);
            return;
        }

        if (!TryBuildBoundaryPolygon(copiedSprite, out var polygon))
        {
            Debug.LogError("SpriteEditor: Could not build sprite boundary polygon.", this);
            return;
        }

        float radius = Mathf.Max(0f, filletRadius);
        int segments = Mathf.Max(1, filletSegments);
        var filletedPolygon = BuildFilletedPolygon(polygon, radius, segments);
        if (filletedPolygon == null || filletedPolygon.Count < 3)
        {
            Debug.LogError("SpriteEditor: Fillet operation produced invalid geometry.", this);
            return;
        }

        if (!TryTriangulatePolygon(filletedPolygon, out var triangles))
        {
            Debug.LogError("SpriteEditor: Failed to triangulate filleted sprite polygon.", this);
            return;
        }

        copiedSprite.OverrideGeometry(filletedPolygon.ToArray(), triangles);
        EditorUtility.SetDirty(copiedSprite);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(copiedPath, ImportAssetOptions.ForceUpdate);
        EditorGUIUtility.PingObject(copiedSprite);

        Debug.Log($"SpriteEditor: Saved filleted sprite copy to {copiedPath}", copiedSprite);
    }

    private static Sprite LoadCopiedSprite(string path, string preferredName)
    {
        var direct = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (direct != null && (string.IsNullOrEmpty(preferredName) || direct.name == preferredName)) return direct;

        var representations = AssetDatabase.LoadAllAssetRepresentationsAtPath(path);
        if (representations != null)
        {
            for (int i = 0; i < representations.Length; i++)
            {
                if (representations[i] is Sprite s && (string.IsNullOrEmpty(preferredName) || s.name == preferredName))
                {
                    return s;
                }
            }

            for (int i = 0; i < representations.Length; i++)
            {
                if (representations[i] is Sprite s)
                {
                    return s;
                }
            }
        }

        return direct;
    }

    private static bool TryBuildBoundaryPolygon(Sprite sprite, out List<Vector2> polygon)
    {
        polygon = null;
        if (sprite == null) return false;

        var vertices = sprite.vertices;
        var triangles = sprite.triangles;
        if (vertices == null || vertices.Length < 3 || triangles == null || triangles.Length < 3) return false;

        var edgeCounts = new Dictionary<EdgeKey, int>(triangles.Length);
        AddTriangleEdges(triangles, edgeCounts);

        var adjacency = new Dictionary<int, List<int>>(vertices.Length);
        foreach (var kv in edgeCounts)
        {
            if (kv.Value != 1) continue;
            AddAdjacency(adjacency, kv.Key.A, kv.Key.B);
            AddAdjacency(adjacency, kv.Key.B, kv.Key.A);
        }

        if (adjacency.Count < 3) return false;

        int start = -1;
        foreach (var k in adjacency.Keys)
        {
            if (start < 0 || vertices[k].x < vertices[start].x || (Mathf.Approximately(vertices[k].x, vertices[start].x) && vertices[k].y < vertices[start].y))
            {
                start = k;
            }
        }

        if (start < 0) return false;

        var orderedIndices = new List<int>(adjacency.Count + 4);
        int previous = -1;
        int current = start;

        for (int safety = 0; safety < vertices.Length * 4; safety++)
        {
            orderedIndices.Add(current);
            if (!adjacency.TryGetValue(current, out var neighbors) || neighbors == null || neighbors.Count == 0) return false;

            int next;
            if (previous < 0)
            {
                next = neighbors[0];
            }
            else if (neighbors.Count == 1)
            {
                next = neighbors[0];
            }
            else
            {
                next = neighbors[0] == previous ? neighbors[1] : neighbors[0];
            }

            previous = current;
            current = next;

            if (current == start) break;
        }

        if (orderedIndices.Count < 3) return false;

        polygon = new List<Vector2>(orderedIndices.Count);
        for (int i = 0; i < orderedIndices.Count; i++)
        {
            polygon.Add(vertices[orderedIndices[i]]);
        }

        return true;
    }

    private static void AddTriangleEdges(ushort[] triangles, Dictionary<EdgeKey, int> edgeCounts)
    {
        for (int i = 0; i + 2 < triangles.Length; i += 3)
        {
            AddEdge(edgeCounts, triangles[i], triangles[i + 1]);
            AddEdge(edgeCounts, triangles[i + 1], triangles[i + 2]);
            AddEdge(edgeCounts, triangles[i + 2], triangles[i]);
        }
    }

    private static void AddEdge(Dictionary<EdgeKey, int> edgeCounts, int a, int b)
    {
        var key = new EdgeKey(a, b);
        if (edgeCounts.TryGetValue(key, out int existing))
        {
            edgeCounts[key] = existing + 1;
        }
        else
        {
            edgeCounts[key] = 1;
        }
    }

    private static void AddAdjacency(Dictionary<int, List<int>> adjacency, int from, int to)
    {
        if (!adjacency.TryGetValue(from, out var list))
        {
            list = new List<int>(2);
            adjacency[from] = list;
        }

        if (!list.Contains(to))
        {
            list.Add(to);
        }
    }

    private static float SignedArea(IReadOnlyList<Vector2> polygon)
    {
        float area = 0f;
        for (int i = 0; i < polygon.Count; i++)
        {
            Vector2 a = polygon[i];
            Vector2 b = polygon[(i + 1) % polygon.Count];
            area += (a.x * b.y) - (b.x * a.y);
        }

        return area * 0.5f;
    }

    private static List<Vector2> BuildFilletedPolygon(IReadOnlyList<Vector2> polygon, float radius, int segments)
    {
        var result = new List<Vector2>(polygon.Count * Mathf.Max(2, segments + 1));
        if (polygon == null || polygon.Count < 3) return result;

        bool clockwise = SignedArea(polygon) < 0f;

        for (int i = 0; i < polygon.Count; i++)
        {
            Vector2 prev = polygon[(i - 1 + polygon.Count) % polygon.Count];
            Vector2 curr = polygon[i];
            Vector2 next = polygon[(i + 1) % polygon.Count];

            Vector2 dirToPrev = (prev - curr);
            Vector2 dirToNext = (next - curr);
            float lenPrev = dirToPrev.magnitude;
            float lenNext = dirToNext.magnitude;
            if (lenPrev <= 0.0001f || lenNext <= 0.0001f)
            {
                AddUnique(result, curr);
                continue;
            }

            Vector2 inDir = dirToPrev / lenPrev;
            Vector2 outDir = dirToNext / lenNext;

            float dot = Mathf.Clamp(Vector2.Dot(inDir, outDir), -1f, 1f);
            float interior = Mathf.Acos(dot);
            if (radius <= 0.0001f || interior <= 0.001f || interior >= Mathf.PI - 0.001f)
            {
                AddUnique(result, curr);
                continue;
            }

            float tangentDistance = radius / Mathf.Tan(interior * 0.5f);
            float maxTangentDistance = Mathf.Min(lenPrev, lenNext) - 0.0001f;
            if (maxTangentDistance <= 0.0001f)
            {
                AddUnique(result, curr);
                continue;
            }

            if (tangentDistance > maxTangentDistance)
            {
                tangentDistance = maxTangentDistance;
            }

            float usedRadius = tangentDistance * Mathf.Tan(interior * 0.5f);
            if (usedRadius <= 0.0001f)
            {
                AddUnique(result, curr);
                continue;
            }

            Vector2 tangentA = curr + inDir * tangentDistance;
            Vector2 tangentB = curr + outDir * tangentDistance;

            Vector2 bisector = (inDir + outDir);
            if (bisector.sqrMagnitude <= 0.0000001f)
            {
                AddUnique(result, curr);
                continue;
            }

            bisector.Normalize();
            float centerDistance = usedRadius / Mathf.Sin(interior * 0.5f);
            Vector2 center = curr + bisector * centerDistance;

            AddUnique(result, tangentA);

            float startAngle = Mathf.Atan2(tangentA.y - center.y, tangentA.x - center.x);
            float endAngle = Mathf.Atan2(tangentB.y - center.y, tangentB.x - center.x);

            AddArc(result, center, usedRadius, startAngle, endAngle, clockwise, segments);
            AddUnique(result, tangentB);
        }

        return result;
    }

    private static void AddArc(List<Vector2> points, Vector2 center, float radius, float startAngle, float endAngle, bool clockwise, int segments)
    {
        if (points == null || radius <= 0f || segments <= 1) return;

        if (clockwise)
        {
            while (startAngle < endAngle) startAngle += Mathf.PI * 2f;
            float sweep = startAngle - endAngle;
            for (int i = 1; i < segments; i++)
            {
                float t = i / (float)segments;
                float a = startAngle - (sweep * t);
                AddUnique(points, center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
            }
        }
        else
        {
            while (endAngle < startAngle) endAngle += Mathf.PI * 2f;
            float sweep = endAngle - startAngle;
            for (int i = 1; i < segments; i++)
            {
                float t = i / (float)segments;
                float a = startAngle + (sweep * t);
                AddUnique(points, center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
            }
        }
    }

    private static void AddUnique(List<Vector2> points, Vector2 p)
    {
        if (points == null) return;
        if (points.Count > 0 && (points[points.Count - 1] - p).sqrMagnitude <= 0.0000001f) return;
        points.Add(p);
    }

    private static bool TryTriangulatePolygon(List<Vector2> polygon, out ushort[] triangles)
    {
        triangles = null;
        if (polygon == null || polygon.Count < 3) return false;

        var verts = new List<Vector2>(polygon);
        if (SignedArea(verts) < 0f)
        {
            verts.Reverse();
        }

        var indices = new List<int>(verts.Count);
        for (int i = 0; i < verts.Count; i++) indices.Add(i);

        var tris = new List<ushort>((verts.Count - 2) * 3);
        int guard = 0;
        while (indices.Count > 3 && guard < 10000)
        {
            guard++;
            bool earFound = false;

            for (int i = 0; i < indices.Count; i++)
            {
                int i0 = indices[(i - 1 + indices.Count) % indices.Count];
                int i1 = indices[i];
                int i2 = indices[(i + 1) % indices.Count];

                if (!IsEar(verts, indices, i0, i1, i2)) continue;

                tris.Add((ushort)i0);
                tris.Add((ushort)i1);
                tris.Add((ushort)i2);
                indices.RemoveAt(i);
                earFound = true;
                break;
            }

            if (!earFound) return false;
        }

        if (indices.Count == 3)
        {
            tris.Add((ushort)indices[0]);
            tris.Add((ushort)indices[1]);
            tris.Add((ushort)indices[2]);
        }

        triangles = tris.ToArray();
        return triangles.Length >= 3;
    }

    private static bool IsEar(List<Vector2> verts, List<int> polygonIndices, int i0, int i1, int i2)
    {
        Vector2 a = verts[i0];
        Vector2 b = verts[i1];
        Vector2 c = verts[i2];

        if (Cross(b - a, c - b) <= 0f) return false;

        for (int i = 0; i < polygonIndices.Count; i++)
        {
            int idx = polygonIndices[i];
            if (idx == i0 || idx == i1 || idx == i2) continue;
            if (PointInTriangle(verts[idx], a, b, c)) return false;
        }

        return true;
    }

    private static float Cross(Vector2 a, Vector2 b)
    {
        return (a.x * b.y) - (a.y * b.x);
    }

    private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float c1 = Cross(b - a, p - a);
        float c2 = Cross(c - b, p - b);
        float c3 = Cross(a - c, p - c);

        bool hasNeg = c1 < 0f || c2 < 0f || c3 < 0f;
        bool hasPos = c1 > 0f || c2 > 0f || c3 > 0f;
        return !(hasNeg && hasPos);
    }

    private readonly struct EdgeKey
    {
        public readonly int A;
        public readonly int B;

        public EdgeKey(int i0, int i1)
        {
            if (i0 <= i1)
            {
                A = i0;
                B = i1;
            }
            else
            {
                A = i1;
                B = i0;
            }
        }
    }
#endif
}
