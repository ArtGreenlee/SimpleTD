using System;
using System.Collections.Generic;
using UnityEngine;

public class TagManager : MonoBehaviour
{
    private readonly Dictionary<Tower.Tag, int> tagCounts = new Dictionary<Tower.Tag, int>();
    private readonly Dictionary<Tower.Tag, int> previousTagCounts = new Dictionary<Tower.Tag, int>();
    private bool hasTagCountSnapshot;
    private bool _dirty = true;

    public static TagManager instance;

    public Dictionary<Tower.Tag, List<Tower>> tagToTowers = new Dictionary<Tower.Tag, List<Tower>>();

    public GameObject tagDisplayParent;

    private List<TagDisplay> tagDisplays = new List<TagDisplay>();

    private Dictionary<Tower.Tag, TagDisplay> tagToTagDisplay = new Dictionary<Tower.Tag, TagDisplay>();

    [Serializable]
    public struct TagInfo
    {
        public Tower.Tag tag;
        public List<int> tagLevelCounts;
    }

    private List<TagInfo> tagInfos = new List<TagInfo>
    {
        new TagInfo { tag = Tower.Tag.Red,    tagLevelCounts = new List<int> { 1, 3, 5 } },
        new TagInfo { tag = Tower.Tag.Blue,   tagLevelCounts = new List<int> { 1, 3, 5 } },
        new TagInfo { tag = Tower.Tag.Green,  tagLevelCounts = new List<int> { 1, 3, 5 } },
        new TagInfo { tag = Tower.Tag.Orange, tagLevelCounts = new List<int> { 2, 4, 6 } },
        new TagInfo { tag = Tower.Tag.Purple, tagLevelCounts = new List<int> { 1, 3, 5 } },
        new TagInfo { tag = Tower.Tag.White,  tagLevelCounts = new List<int> { 1, 3, 5 } },
        new TagInfo { tag = Tower.Tag.Black,  tagLevelCounts = new List<int> { 1, 3, 5 } },
        new TagInfo { tag = Tower.Tag.Yellow, tagLevelCounts = new List<int> { 1, 3, 5 } },
        new TagInfo { tag = Tower.Tag.Cyan,   tagLevelCounts = new List<int> { 2, 3, 4 } },
    };
    public Dictionary<Tower.Tag, TagInfo> tagToTagInfo = new Dictionary<Tower.Tag, TagInfo>();

    public List<Vector3> tagDisplayPositions = new List<Vector3>();

    public int GetTagCount(Tower.Tag tag)
    {
        RebuildCachesIfDirty();
        return tagCounts.ContainsKey(tag) ? tagCounts[tag] :0;
    }

    /// <summary>
    /// Gets the current level (1, 2, 3, etc.) for a tag based on the tower count threshold.
    /// Level is determined by how many towers have the tag compared to tagLevelCounts thresholds.
    /// </summary>
    public int GetTagLevel(Tower.Tag tag)
    {
        int count = GetTagCount(tag);
        if (!tagToTagInfo.TryGetValue(tag, out var info)) return 0;
        
        for (int level = info.tagLevelCounts.Count; level >= 1; level--)
        {
            if (count >= info.tagLevelCounts[level - 1])
            {
                return level;
            }
        }
        return 0;
    }

    public float GetRedTagAOESizeBonus()
    {
        int level = GetTagLevel(Tower.Tag.Red);
        if (level == 3) return redTagLevelThreeAOESizeIncrease;
        if (level == 2) return redTagLevelTwoAOESizeIncrease;
        if (level == 1) return redTagLevelOneAOESizeIncrease;
        return 0f;
    }

    public float GetGreenTagCritChanceBonus(bool isGlobalBonus)
    {
        int level = GetTagLevel(Tower.Tag.Green);
        if (level == 3 && isGlobalBonus) return greenTagLevelThreeGlobalCritChanceIncrease;
        if (level == 2) return greenTagLevelTwoCritChanceIncrease;
        if (level == 1) return greenTagLevelOneCritChanceIncrease;
        return 0f;
    }

    private void Awake()    
    {
        instance = this;

        SortTagDisplayPositionsByX();
        RefreshTagDisplayReferences();

        if (tagInfos != null)
        {
            for (int i =0; i < tagInfos.Count; i++)
            {
                var ti = tagInfos[i];
                tagToTagInfo[ti.tag] = ti;
            }
        }
    }

    private void RefreshTagDisplayReferences()
    {
        tagDisplays.Clear();
        tagToTagDisplay.Clear();

        if (tagDisplayParent == null) return;

        var displays = tagDisplayParent.GetComponentsInChildren<TagDisplay>(true);
        if (displays == null || displays.Length == 0) return;

        Array.Sort(displays, (a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));

        AutoAssignDisplayTags(displays);

        for (int i = 0; i < displays.Length; i++)
        {
            var td = displays[i];
            if (td == null) continue;

            tagDisplays.Add(td);
            tagToTagDisplay[td.towerTag] = td;
            ApplyTagDisplayVisuals(td, td.towerTag);
        }
    }

    private void SortTagDisplayPositionsByX()
    {
        if (tagDisplayPositions == null || tagDisplayPositions.Count <= 1) return;
        tagDisplayPositions.Sort((a, b) => a.x.CompareTo(b.x));
    }

    private void AutoAssignDisplayTags(TagDisplay[] displays)
    {
        if (displays == null || displays.Length == 0) return;

        var assignableTags = (Tower.Tag[])Enum.GetValues(typeof(Tower.Tag));
        int assignCount = Mathf.Min(displays.Length, assignableTags.Length);

        for (int i = 0; i < assignCount; i++)
        {
            if (displays[i] == null) continue;
            displays[i].towerTag = assignableTags[i];
        }
    }

    private void Start()
    {
        NotifyTowersChanged();
    }

    public void NotifyTowersChanged()
    {
        _dirty = true;
        RefreshTagDisplays();
    }

    private void RebuildCachesIfDirty()
    {
        if (!_dirty) return;
        _dirty = false;

        tagCounts.Clear();
        tagToTowers.Clear();

        if (TowerManager.instance == null) return;

        foreach (var tower in TowerManager.instance.EnumeratePlacedTowers())
        {
            if (tower == null) continue;
            if (tower.tags == null) continue;

            for (int i = 0; i < tower.tags.Count; i++)
            {
                var tag = tower.tags[i];

                if (!tagCounts.TryGetValue(tag, out int c)) c = 0;
                tagCounts[tag] = c + 1;

                if (!tagToTowers.TryGetValue(tag, out var list) || list == null)
                {
                    list = new List<Tower>();
                    tagToTowers[tag] = list;
                }
                list.Add(tower);
            }
        }
    }

    private void RefreshTagDisplays()
    {
        RebuildCachesIfDirty();
        RefreshTagDisplayReferences();

        if (tagDisplays == null || tagDisplays.Count == 0) return;

        EnsureTagDisplayPositionsInitialized();

        // Collect tags that have at least one tower.
        var activeTags = new List<Tower.Tag>();
        foreach (var kvp in tagCounts)
        {
            if (kvp.Value > 0) activeTags.Add(kvp.Key);
        }

        // Sort by tagCount descending (highest count = leftmost = first slot).
        activeTags.Sort((a, b) => GetTagCount(b).CompareTo(GetTagCount(a)));

        // Deactivate all displays by default.
        for (int i = 0; i < tagDisplays.Count; i++)
        {
            if (tagDisplays[i] != null) tagDisplays[i].gameObject.SetActive(false);
        }

        int activeCount = Mathf.Min(activeTags.Count, tagDisplays.Count);

        for (int i = 0; i < activeCount; i++)
        {
            Tower.Tag tag = activeTags[i];

            if (!tagToTagDisplay.TryGetValue(tag, out var display) || display == null)
            {
                for (int j = 0; j < tagDisplays.Count; j++)
                {
                    if (tagDisplays[j] != null && tagDisplays[j].towerTag.Equals(tag))
                    {
                        display = tagDisplays[j];
                        break;
                    }
                }
            }

            if (display == null) continue;

            // Move to the i-th slot position (leftmost = index 0 = highest count).
            if (i < tagDisplayPositions.Count)
            {
                display.transform.position = tagDisplayPositions[i];
            }

            ApplyTagDisplayVisuals(display, tag);
            display.gameObject.SetActive(true);
            display.SetText();

            int currentCount = GetTagCount(tag);
            if (ShouldIndicateTagDisplay(tag, currentCount))
            {
                display.AnimateTagImage();
                display.Indicate();
            }
        }

        CaptureTagCountSnapshot();
    }

    private bool ShouldIndicateTagDisplay(Tower.Tag tag, int currentCount)
    {
        if (currentCount <= 0) return false;
        if (!hasTagCountSnapshot) return false;

        int previousCount = 0;
        previousTagCounts.TryGetValue(tag, out previousCount);
        return previousCount != currentCount;
    }

    private void CaptureTagCountSnapshot()
    {
        previousTagCounts.Clear();

        var allTags = (Tower.Tag[])Enum.GetValues(typeof(Tower.Tag));
        for (int i = 0; i < allTags.Length; i++)
        {
            Tower.Tag tag = allTags[i];
            previousTagCounts[tag] = GetTagCount(tag);
        }

        hasTagCountSnapshot = true;
    }

    private void EnsureTagDisplayPositionsInitialized()
    {
        if (tagDisplayPositions == null)
        {
            tagDisplayPositions = new List<Vector3>();
        }

        if (tagDisplayPositions.Count < tagDisplays.Count)
        {
            tagDisplayPositions.Clear();
            for (int i = 0; i < tagDisplays.Count; i++)
            {
                if (tagDisplays[i] == null) continue;
                tagDisplayPositions.Add(tagDisplays[i].transform.position);
            }
        }

        SortTagDisplayPositionsByX();
    }

    private void ApplyTagDisplayVisuals(TagDisplay display, Tower.Tag tag)
    {
        if (display == null) return;

        Color iconColor = Color.white;
        if (CM.i != null)
        {
            CM.ColorType colorType = CM.i.TagToColorType(tag);
            iconColor = CM.i.ColorTypeToColor(colorType);
        }

        Color countTextColor = GetReadableTextColor(iconColor);
        display.ApplyVisuals(iconColor, countTextColor);
    }

    private static Color GetReadableTextColor(Color backgroundColor)
    {
        float luminance = (0.2126f * backgroundColor.r)
                        + (0.7152f * backgroundColor.g)
                        + (0.0722f * backgroundColor.b);

        return luminance > 0.55f ? new Color(0.1f, 0.1f, 0.1f, 1f) : Color.white;
    }

    public string GetLevelDescription(Tower.Tag tag, int level)
    {
        switch (tag)
        {
            case Tower.Tag.Green:
                return GetGreenTagLevelDescriptions(level);
            case Tower.Tag.Red:
                return GetRedTagLevelDescription(level);
            case Tower.Tag.Purple:
                return GetPurpleTagLevelDescription(level);
            case Tower.Tag.Blue:
                return GetBlueTagLevelDescription(level);
            case Tower.Tag.Yellow:
                return GetYellowTagLevelDescription(level);
            case Tower.Tag.Orange:
                return GetOrangeTagLevelDescription(level);
            case Tower.Tag.Cyan:
                return GetCyanTagLevelDescription(level);
            default:
                return "Not implemented yet";
        }
    }

    public static float greenTagLevelOneCritChanceIncrease = .1f;
    public static float greenTagLevelTwoCritChanceIncrease = .2f;
    public static float greenTagLevelThreeGlobalCritChanceIncrease = .2f;

    public static float redTagLevelOneAOESizeIncrease = .5f;
    public static float redTagLevelTwoAOESizeIncrease = 1f;
    public static float redTagLevelThreeAOESizeIncrease = 1f;

    public static int purpleTagLevelOneBounceIncrease = 1;
    public static int purpleTagLevelTwoBounceIncrease = 2;
    public static int purpleTagLevelThreeBounceIncrease = 3;

    public static float blueTagLevelOneMaxSlowIncrease = 0.1f;
    public static float blueTagLevelTwoMaxSlowIncrease = 0.2f;

    public static float yellowTagLevelOneAgentDamageIncrease = 1f;
    public static float yellowTagLevelTwoAgentDamageIncrease = 2f;
    public static float yellowTagLevelOneAgentMaxHealthIncrease = 0.2f;
    public static float yellowTagLevelTwoAgentMaxHealthIncrease = 0.4f;
    public static int yellowTagLevelThreeMaxAgentsPerTowerIncrease = 1;

    public static float orangeTagLevelOneBurnDamageMultiplier = 2f;
    public static float orangeTagLevelTwoBurningEnemyDamageMultiplierBonus = 0.5f;

    public static float cyanTagLevelOneShockLightningChance = 0.5f;
    public static float cyanTagLevelTwoShockLightningChance = 0.75f;
    public static float cyanTagLevelTwoShockLightningChainDamageBonus = 1f;
    public static float cyanTagLevelThreeShockLightningChance = 1f;
    public static int cyanTagLevelThreeShockLightningChainCountBonus = 2;

    private static string FormatPercent(float value)
    {
        return (value * 100f).ToString("0.#") + "%";
    }

    private static string FormatSize(float value)
    {
        return "+" + value.ToString("0.#");
    }

    private static string FormatBounce(int value)
    {
        return "+" + value.ToString();
    }

    private static string FormatMultiplier(float value)
    {
        return "+" + value.ToString("0.#") + "x";
    }

    public string GetGreenTagLevelDescriptions(int level)
    {
        if (level > tagToTagInfo[Tower.Tag.Green].tagLevelCounts.Count)
        {
            Debug.LogError("Level exceeds defined levels");
            return null;
        }
        switch (level)
        {
            case 1:
                return CM.i.RTC(CM.ColorType.Green, "+" + FormatPercent(greenTagLevelOneCritChanceIncrease)) + " crit chance for Towers with Green Tag";
            case 2:
                return CM.i.RTC(CM.ColorType.Green, "+" + FormatPercent(greenTagLevelTwoCritChanceIncrease)) + " crit chance for Towers with Green Tag";
            case 3:
                return CM.i.RTC(CM.ColorType.Green, "+" + FormatPercent(greenTagLevelThreeGlobalCritChanceIncrease)) + " crit chance for all Towers";
            default:
                return "Unknown Level";
        }
    }

    public string GetRedTagLevelDescription(int level)
    {
        if (level > tagToTagInfo[Tower.Tag.Red].tagLevelCounts.Count)
        {
            Debug.LogError("Level exceeds defined levels");
            return null;
        }
        switch (level)
        {
            case 1:
                return CM.i.RTC(CM.ColorType.Green, FormatSize(redTagLevelOneAOESizeIncrease)) + " AOE size for Towers with Red Tag";
            case 2:
                return CM.i.RTC(CM.ColorType.Green, FormatSize(redTagLevelTwoAOESizeIncrease)) + " AOE size for Towers with Red Tag";
            case 3:
                return CM.i.RTC(CM.ColorType.Green, FormatSize(redTagLevelThreeAOESizeIncrease)) + " AOE size for all Towers";
            default:
                return "Unknown Level";
        }
    }

    public string GetPurpleTagLevelDescription(int level)
    {
        if (!tagToTagInfo.ContainsKey(Tower.Tag.Purple) || level > tagToTagInfo[Tower.Tag.Purple].tagLevelCounts.Count)
        {
            Debug.LogError("Level exceeds defined levels");
            return null;
        }
        switch (level)
        {
            case 1:
                return CM.i.RTC(CM.ColorType.Green, FormatBounce(purpleTagLevelOneBounceIncrease)) + " bounce for Towers with Purple Tag";
            case 2:
                return CM.i.RTC(CM.ColorType.Green, FormatBounce(purpleTagLevelTwoBounceIncrease)) + " bounces for Towers with Purple Tag";
            case 3:
                return CM.i.RTC(CM.ColorType.Green, FormatBounce(purpleTagLevelThreeBounceIncrease)) + " bounces for all Lasers";
            default:
                return "Unknown Level";
        }
    }

    public int GetPurpleTagBounceBonus()
    {
        int level = GetTagLevel(Tower.Tag.Purple);
        if (level == 3) return purpleTagLevelThreeBounceIncrease;
        if (level == 2) return purpleTagLevelTwoBounceIncrease;
        if (level == 1) return purpleTagLevelOneBounceIncrease;
        return 0;
    }

    public string GetBlueTagLevelDescription(int level)
    {
        if (!tagToTagInfo.ContainsKey(Tower.Tag.Blue) || level > tagToTagInfo[Tower.Tag.Blue].tagLevelCounts.Count)
        {
            Debug.LogError("Level exceeds defined levels");
            return null;
        }
        switch (level)
        {
            case 1:
                return CM.i.RTC(CM.ColorType.Green, "+" + FormatPercent(blueTagLevelOneMaxSlowIncrease)) + " max " + CM.i.RTC(CM.ColorType.Blue, "slow");
            case 2:
                return CM.i.RTC(CM.ColorType.Green, "+" + FormatPercent(blueTagLevelTwoMaxSlowIncrease)) + " max " + CM.i.RTC(CM.ColorType.Blue, "slow");
            default:
                return "Unknown Level";
        }
    }

    public float GetBlueTagMaxSlowBonus()
    {
        int level = GetTagLevel(Tower.Tag.Blue);
        if (level == 2) return blueTagLevelTwoMaxSlowIncrease;
        if (level == 1) return blueTagLevelOneMaxSlowIncrease;
        return 0f;
    }

    public string GetOrangeTagLevelDescription(int level)
    {
        if (!tagToTagInfo.ContainsKey(Tower.Tag.Orange) || level > tagToTagInfo[Tower.Tag.Orange].tagLevelCounts.Count)
        {
            Debug.LogError("Level exceeds defined levels");
            return null;
        }

        switch (level)
        {
            case 1:
                return CM.i.RTC(CM.ColorType.Red, FormatMultiplier(orangeTagLevelOneBurnDamageMultiplier)) + " burn damage";
            case 2:
                return CM.i.RTC(CM.ColorType.Red, FormatMultiplier(orangeTagLevelTwoBurningEnemyDamageMultiplierBonus)) + " damage multiplier against " + CM.i.RTC(CM.ColorType.Orange, "Burning") + " enemies";
            case 3:
                return CM.i.RTC(CM.ColorType.Orange, "Burning") + " enemies are always " + CM.i.RTC(CM.GetExposeColor(), "Exposed");
            default:
                return "Unknown Level";
        }
    }

    public float GetOrangeTagBurnDamageMultiplier()
    {
        return GetTagLevel(Tower.Tag.Orange) >= 1 ? orangeTagLevelOneBurnDamageMultiplier : 1f;
    }

    public float GetOrangeTagBurningEnemyDamageMultiplierBonus()
    {
        return GetTagLevel(Tower.Tag.Orange) >= 2 ? orangeTagLevelTwoBurningEnemyDamageMultiplierBonus : 0f;
    }

    public bool OrangeTagBurningEnemiesAreAlwaysExposed()
    {
        return GetTagLevel(Tower.Tag.Orange) >= 3;
    }

    public string GetCyanTagLevelDescription(int level)
    {
        if (!tagToTagInfo.ContainsKey(Tower.Tag.Cyan) || level > tagToTagInfo[Tower.Tag.Cyan].tagLevelCounts.Count)
        {
            Debug.LogError("Level exceeds defined levels");
            return null;
        }

        switch (level)
        {
            case 1:
                return CM.i.RTC(CM.ColorType.Green, FormatPercent(cyanTagLevelOneShockLightningChance))
                    + " chance for " + CM.i.RTC(CM.ColorType.Cyan, "shock lightning") + " to trigger";
            case 2:
                return CM.i.RTC(CM.ColorType.Green, FormatPercent(cyanTagLevelTwoShockLightningChance))
                    + " chance for " + CM.i.RTC(CM.ColorType.Cyan, "shock lightning") + " to trigger and "
                    + CM.i.RTC(CM.ColorType.Blue, "+" + cyanTagLevelTwoShockLightningChainDamageBonus.ToString("0.#"))
                    + " chain base damage";
            case 3:
                return CM.i.RTC(CM.ColorType.Green, FormatPercent(cyanTagLevelThreeShockLightningChance))
                    + " chance for " + CM.i.RTC(CM.ColorType.Cyan, "shock lightning") + " to trigger and "
                    + CM.i.RTC(CM.ColorType.Green, "+" + cyanTagLevelThreeShockLightningChainCountBonus)
                    + " lightning chain count";
            default:
                return "Unknown Level";
        }
    }

    public float GetCyanTagShockLightningChance()
    {
        int level = GetTagLevel(Tower.Tag.Cyan);
        if (level >= 3) return cyanTagLevelThreeShockLightningChance;
        if (level >= 2) return cyanTagLevelTwoShockLightningChance;
        if (level >= 1) return cyanTagLevelOneShockLightningChance;
        return 0f;
    }

    public float GetCyanTagShockLightningChainDamageBonus()
    {
        return GetTagLevel(Tower.Tag.Cyan) >= 2 ? cyanTagLevelTwoShockLightningChainDamageBonus : 0f;
    }

    public int GetCyanTagShockLightningChainCountBonus()
    {
        return GetTagLevel(Tower.Tag.Cyan) >= 3 ? cyanTagLevelThreeShockLightningChainCountBonus : 0;
    }

    public string GetYellowTagLevelDescription(int level)
    {
        if (!tagToTagInfo.ContainsKey(Tower.Tag.Yellow) || level > tagToTagInfo[Tower.Tag.Yellow].tagLevelCounts.Count)
        {
            Debug.LogError("Level exceeds defined levels");
            return null;
        }

        switch (level)
        {
            case 1:
                return "Agents deal "
                    + CM.i.RTC(CM.ColorType.Blue, "+" + yellowTagLevelOneAgentDamageIncrease.ToString("0.#"))
                    + " base damage and have "
                    + CM.i.RTC(CM.ColorType.Green, "+" + FormatPercent(yellowTagLevelOneAgentMaxHealthIncrease))
                    + " max HP";
            case 2:
                return "Agents deal "
                    + CM.i.RTC(CM.ColorType.Blue, "+" + yellowTagLevelTwoAgentDamageIncrease.ToString("0.#"))
                    + " base damage and have "
                    + CM.i.RTC(CM.ColorType.Green, "+" + FormatPercent(yellowTagLevelTwoAgentMaxHealthIncrease))
                    + " max HP";
            case 3:
                return CM.i.RTC(CM.ColorType.Green, "+" + yellowTagLevelThreeMaxAgentsPerTowerIncrease)
                    + " max agent per tower";
            default:
                return "Unknown Level";
        }
    }

    public float GetYellowTagAgentBaseDamageBonus()
    {
        int level = GetTagLevel(Tower.Tag.Yellow);
        if (level == 3) return yellowTagLevelTwoAgentDamageIncrease;
        if (level == 2) return yellowTagLevelOneAgentDamageIncrease;
        return 0f;
    }

    public float GetYellowTagAgentMaxHealthMultiplier()
    {
        int level = GetTagLevel(Tower.Tag.Yellow);
        if (level == 3) return 1f + yellowTagLevelTwoAgentMaxHealthIncrease;
        if (level == 2) return 1f + yellowTagLevelOneAgentMaxHealthIncrease;
        return 1f;
    }

    public int GetYellowTagMaxAgentsPerTowerBonus()
    {
        int level = GetTagLevel(Tower.Tag.Yellow);
        return level == 3 ? yellowTagLevelThreeMaxAgentsPerTowerIncrease : 0;
    }
}
