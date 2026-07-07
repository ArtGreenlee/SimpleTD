using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SpriteRenderer Controller - handles sprite renderer color and visual management.
/// Attached to each Tower and Enemy to manage their sprite colors.
/// </summary>
public class SRC : MonoBehaviour
{
    public static string materialMainColorMaterialString = "_Color";
    /// <summary>
    /// Stores a group of sprite renderers and their associated color type.
    /// Used to apply consistent colors across multiple sprite renderers.
    /// </summary>
    [System.Serializable]
    public struct SrColorInfo
    {
        public List<SpriteRenderer> sr;
        public CM.ColorType colorType;
        public bool alphaOverride;
        [Range(0f, 1f)] public float alphaOverrideValue;
        public bool enableHitEffect;
    }

    [Header("Colors")]
    public List<SrColorInfo> srColorInfos = new List<SrColorInfo>();

    [Header("Flash")]
    [SerializeField] private Color flashColor = Color.white;
    [SerializeField] private Color hiddenModeColor = Color.gray;
    [SerializeField] private bool hiddenMode;

    private Coroutine _flashRoutine;

    [Header("Hit Effect")]
    [SerializeField] private string hitEffectMaterialString = "_HitEffectBlend";
    [SerializeField] private float hitEffectBlendDecay = 20f;

    private void Start()
    {
        ApplySpriteRendererColors();
    }

    public List<Material> GetAllMaterials()
    {
        List<Material> materials = new List<Material>();
        if (srColorInfos != null)
        {
            for (int i = 0; i < srColorInfos.Count; i++)
            {
                var group = srColorInfos[i];
                if (group.sr == null) continue;
                for (int j = 0; j < group.sr.Count; j++)
                {
                    var spriteRenderer = group.sr[j];
                    if (spriteRenderer == null) continue;
                    var mat = spriteRenderer.material;
                    if (mat != null && !materials.Contains(mat))
                    {
                        materials.Add(mat);
                    }
                }
            }
        }
        return materials;
    }

    public void Indicate()
    {
        List<Material> materials = GetAllMaterials();
        for (int i = 0; i < materials.Count; i++)
        {
            Material material = materials[i];
            if (material == null || !material.HasProperty("_ShineRotate")) continue;
            material.SetFloat("_ShineRotate", Random.Range(0f, Mathf.PI * 2f));
        }

        UIAnimation.instance.AnimateMaterials(materials);
    }
    /// <summary>
    /// Applies color to a single SrColorInfo group.
    /// </summary>
    private void ApplySpriteRendererColorGroup(SrColorInfo info)
    {
        if (CM.i == null) return;

        Color color = CM.i.ColorTypeToColor(info.colorType);
        if (info.alphaOverride)
        {
            color.a = Mathf.Clamp01(info.alphaOverrideValue);
        }

        color = GetDisplayColor(color);

        if (info.sr != null && info.sr.Count > 0)
        {
            for (int i = 0; i < info.sr.Count; i++)
            {
                var spriteRenderer = info.sr[i];
                if (spriteRenderer == null) continue;
                spriteRenderer.color = color;
                spriteRenderer.material.SetColor(materialMainColorMaterialString, color);
            }
        }
    }

    /// <summary>
    /// Gets the color from the first available sprite renderer.
    /// Returns Color.white if no sprite renderers are available.
    /// </summary>
    public Color GetPrimaryColor()
    {
        if (srColorInfos == null || srColorInfos.Count == 0)
            return Color.white;

        for (int i = 0; i < srColorInfos.Count; i++)
        {
            var group = srColorInfos[i];
            if (group.sr != null && group.sr.Count > 0)
            {
                for (int j = 0; j < group.sr.Count; j++)
                {
                    if (group.sr[j] != null)
                        return group.sr[j].color;
                }
            }
        }

        return Color.white;
    }

    /// <summary>
    /// Gets the first available sprite renderer from the SrColorInfo groups.
    /// Returns null if no sprite renderers are available.
    /// </summary>
    public SpriteRenderer GetPrimarySpriteRenderer()
    {
        if (srColorInfos == null || srColorInfos.Count == 0)
            return null;

        for (int i = 0; i < srColorInfos.Count; i++)
        {
            var group = srColorInfos[i];
            if (group.sr != null && group.sr.Count > 0)
            {
                for (int j = 0; j < group.sr.Count; j++)
                {
                    if (group.sr[j] != null)
                        return group.sr[j];
                }
            }
        }

        return null;
    }

    public void ApplyColorToAll(CM.ColorType color)
    {
        if (srColorInfos == null || CM.i == null)
        {
            return;
        }

        for (int groupIndex = 0; groupIndex < srColorInfos.Count; groupIndex++)
        {
            var srInfo = srColorInfos[groupIndex];
            srInfo.colorType = color;

            if (srInfo.sr == null)
            {
                srColorInfos[groupIndex] = srInfo;
                continue;
            }

            for (int i = 0; i < srInfo.sr.Count; i++)
            {
                if (srInfo.sr[i] == null) continue;

                Color c = CM.i.ColorTypeToColor(color);
                if (srInfo.alphaOverride)
                {
                    c.a = srInfo.alphaOverrideValue;
                }
                else
                {
                    c.a = 1f;
                }

                c = GetDisplayColor(c);

                srInfo.sr[i].color = c;
                srInfo.sr[i].material.SetColor(materialMainColorMaterialString, c);
            }

            srColorInfos[groupIndex] = srInfo;
        }
    }

    /// <summary>
    /// Applies colors to all SrColorInfo groups.
    /// </summary>
    public void ApplySpriteRendererColors()
    {
        if (srColorInfos == null || srColorInfos.Count == 0)
        {
            return;
        }

        for (int i = 0; i < srColorInfos.Count; i++)
        {
            ApplySpriteRendererColorGroup(srColorInfos[i]);
        }
    }

    /// <summary>
    /// Sets alpha on all sprite renderers in SrColorInfo groups.
    /// Used for ghost mode, fading, and transparency effects.
    /// </summary>
    public void SetSpriteRendererAlpha(float alpha)
    {
        alpha = Mathf.Clamp01(alpha);

        if (srColorInfos == null || srColorInfos.Count == 0)
        {
            return;
        }

        HashSet<SpriteRenderer> visited = null;

        for (int i = 0; i < srColorInfos.Count; i++)
        {
            var group = srColorInfos[i];
            if (group.sr == null) continue;

            for (int j = 0; j < group.sr.Count; j++)
            {
                var spriteRenderer = group.sr[j];
                if (spriteRenderer == null) continue;

                visited ??= new HashSet<SpriteRenderer>();
                if (!visited.Add(spriteRenderer)) continue;

                var c = spriteRenderer.color;
                c.a = alpha;
                spriteRenderer.color = c;
                spriteRenderer.material.SetColor(materialMainColorMaterialString, c);
            }
        }
    }

    /// <summary>
    /// Context menu function to apply colors to all sprite renderers.
    /// Initializes ColorManager if needed.
    /// </summary>
    [ContextMenu("Apply Sprite Colors")]
    public void ApplySpritesColorsContextMenu()
    {
        if (!InitializeColorManager())
        {
            Debug.LogError("ColorManager could not be initialized. Make sure it exists in the scene.", gameObject);
            return;
        }

        ApplySpriteRendererColors();
        Debug.Log($"Applied colors to {gameObject.name}", gameObject);
    }

    /// <summary>
    /// Ensures ColorManager is initialized. Finds it in the scene if not already cached.
    /// </summary>
    private static bool InitializeColorManager()
    {
        if (CM.i != null) return true;

        // Try to find ColorManager in scene
        CM colorManager = Object.FindFirstObjectByType<CM>();
        if (colorManager != null)
        {
            return true;
        }

        Debug.LogWarning("ColorManager (CM) not found in scene. Cannot apply sprite colors.");
        return false;
    }

    public void Flash(float duration = 0.1f)
    {
        if (!isActiveAndEnabled) return;

        if (_flashRoutine != null)
        {
            StopCoroutine(_flashRoutine);
        }

        _flashRoutine = StartCoroutine(FlashRoutine(Mathf.Max(0f, duration)));
    }

    public void SetHiddenMode(bool enabled)
    {
        if (hiddenMode == enabled) return;

        hiddenMode = enabled;
        ApplySpriteRendererColors();
    }

    public bool IsHiddenModeEnabled()
    {
        return hiddenMode;
    }

    private List<SpriteRenderer> GetManagedSpriteRenderers()
    {
        var renderers = new List<SpriteRenderer>();
        var visited = new HashSet<SpriteRenderer>();

        if (srColorInfos != null)
        {
            for (int i = 0; i < srColorInfos.Count; i++)
            {
                var group = srColorInfos[i];
                if (group.sr == null) continue;

                for (int j = 0; j < group.sr.Count; j++)
                {
                    var sr = group.sr[j];
                    if (sr == null) continue;
                    if (!visited.Add(sr)) continue;
                    renderers.Add(sr);
                }
            }
        }

        if (renderers.Count == 0)
        {
            var fallback = GetComponentsInChildren<SpriteRenderer>(includeInactive: false);
            for (int i = 0; i < fallback.Length; i++)
            {
                var sr = fallback[i];
                if (sr == null) continue;
                if (!visited.Add(sr)) continue;
                renderers.Add(sr);
            }
        }

        return renderers;
    }

    private IEnumerator FlashRoutine(float duration)
    {
        var renderers = GetManagedSpriteRenderers();

        if (renderers.Count == 0)
        {
            _flashRoutine = null;
            yield break;
        }

        var originalColors = new Color[renderers.Count];
        for (int i = 0; i < renderers.Count; i++)
        {
            originalColors[i] = renderers[i].color;
            var c = flashColor;
            c.a = originalColors[i].a;
            renderers[i].color = c;
        }

        if (duration > 0f)
        {
            yield return new WaitForSeconds(duration);
        }

        for (int i = 0; i < renderers.Count; i++)
        {
            if (renderers[i] == null) continue;
            renderers[i].color = originalColors[i];
        }

        _flashRoutine = null;
    }

    private Color GetDisplayColor(Color baseColor)
    {
        if (!hiddenMode)
        {
            return baseColor;
        }

        Color displayColor = hiddenModeColor;
        displayColor.a = baseColor.a;
        return displayColor;
    }

    /// <summary>
    /// Updates the hit effect blend value, lerping it towards 0 over time.
    /// Call this in FixedUpdate for smooth decay.
    /// </summary>
    public void UpdateHitEffect()
    {
        if (srColorInfos == null) return;

        foreach (var srColorInfo in srColorInfos)
        {
            if (!srColorInfo.enableHitEffect) continue;
            if (srColorInfo.sr == null) continue;

            foreach (SpriteRenderer sr in srColorInfo.sr)
            {
                if (sr == null || sr.material == null) continue;
                float currentBlend = sr.material.GetFloat(hitEffectMaterialString);
                sr.material.SetFloat(hitEffectMaterialString, Mathf.Lerp(currentBlend, 0, hitEffectBlendDecay * Time.fixedDeltaTime));
            }
        }
    }

    /// <summary>
    /// Triggers the hit effect by setting the material blend to 1.
    /// Call this when the entity takes damage.
    /// </summary>
    public void TriggerHitEffect()
    {
        if (srColorInfos == null) return;

        foreach (var srColorInfo in srColorInfos)
        {
            if (!srColorInfo.enableHitEffect) continue;
            if (srColorInfo.sr == null) continue;

            foreach (SpriteRenderer sr in srColorInfo.sr)
            {
                if (sr == null || sr.material == null) continue;
                sr.material.SetFloat(hitEffectMaterialString, 1);
            }
        }
    }

    public void ChangeSortingOrder(int change)
    {
        var visited = new HashSet<SpriteRenderer>();
        for (int i = 0; i < srColorInfos.Count; i++)
        {
            var colorInfo = srColorInfos[i];
            if (colorInfo.sr == null) continue;

            for (int j = 0; j < colorInfo.sr.Count; j++)
            {
                var spriteRenderer = colorInfo.sr[j];
                if (spriteRenderer == null || !visited.Add(spriteRenderer)) continue;
                spriteRenderer.sortingOrder += change;
            }
        }
    }
}
