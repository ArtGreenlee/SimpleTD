using UnityEngine;
using UnityEngine.Serialization;

public class AddMarkOnHitEffect : Effect
{
    [System.Flags]
    public enum MarkTypeMask
    {
        None = 0,
        Red = 1 << 0,
        Blue = 1 << 1,
        Green = 1 << 2,
        Yellow = 1 << 3,
        Purple = 1 << 4,
        Orange = 1 << 5,
        White = 1 << 6,
        Black = 1 << 7,
        Cyan = 1 << 8,
        Gold = 1 << 9,
        Moss = 1 << 10
    }

    [SerializeField, HideInInspector, FormerlySerializedAs("markType")] private CM.ColorType legacyMarkType = CM.ColorType.Green;
    [SerializeField] private MarkTypeMask markTypes = MarkTypeMask.Green;

    private static readonly CM.ColorType[] _allSelectableMarks =
    {
        CM.ColorType.Red,
        CM.ColorType.Blue,
        CM.ColorType.Green,
        CM.ColorType.Yellow,
        CM.ColorType.Purple,
        CM.ColorType.Orange,
        CM.ColorType.White,
        CM.ColorType.Black,
        CM.ColorType.Cyan,
        CM.ColorType.Gold,
        CM.ColorType.Moss
    };

    public string GetMarkDescriptionText()
    {
        int selectedCount = 0;
        CM.ColorType firstSelected = CM.ColorType.None;
        System.Text.StringBuilder builder = new System.Text.StringBuilder();

        for (int i = 0; i < _allSelectableMarks.Length; i++)
        {
            CM.ColorType color = _allSelectableMarks[i];
            if (!HasColor(markTypes, color)) continue;

            selectedCount++;
            if (firstSelected == CM.ColorType.None) firstSelected = color;

            if (builder.Length > 0) builder.Append("/");
            builder.Append(CM.i != null ? CM.i.ColorToName(color) : color.ToString());
        }

        if (selectedCount == 0)
        {
            return CM.i != null ? CM.i.RTC(CM.ColorType.Green, "Green") : "Green";
        }

        if (selectedCount == 1)
        {
            return CM.i != null ? CM.i.RTC(firstSelected, CM.i.ColorToName(firstSelected)) : firstSelected.ToString();
        }

        return builder.ToString();
    }

    private void OnValidate()
    {
        if (markTypes == MarkTypeMask.None && legacyMarkType != CM.ColorType.None)
        {
            markTypes = ToMask(legacyMarkType);
        }
    }

    public override void ApplyEffect(Enemy enemy, Projectile projectile = null)
    {
        if (ShouldApplyEffect() && enemy.GetMark() == CM.ColorType.None)
        {
            CM.ColorType markToApply = GetMarkToApply();
            enemy.SetMark(markToApply);
        }

        base.ApplyEffect(enemy, projectile);
    }

    private CM.ColorType GetMarkToApply()
    {
        // Check for generic mark upgrades first
        if (tower != null)
        {
            if (tower.UpgradeActive(UpgradeData.UID.ApplyRedMarkOnHit)) return CM.ColorType.Red;
            if (tower.UpgradeActive(UpgradeData.UID.ApplyBlueMarkOnHit)) return CM.ColorType.Blue;
            if (tower.UpgradeActive(UpgradeData.UID.ApplyGreenMarkOnHit)) return CM.ColorType.Green;
            if (tower.UpgradeActive(UpgradeData.UID.ApplyYellowMarkOnHit)) return CM.ColorType.Yellow;
            if (tower.UpgradeActive(UpgradeData.UID.ApplyPurpleMarkOnHit)) return CM.ColorType.Purple;
            if (tower.UpgradeActive(UpgradeData.UID.ApplyOrangeMarkOnHit)) return CM.ColorType.Orange;
            if (tower.UpgradeActive(UpgradeData.UID.ApplyWhiteMarkOnHit)) return CM.ColorType.White;
            if (tower.UpgradeActive(UpgradeData.UID.ApplyCyanMarkOnHit)) return CM.ColorType.Cyan;
        }

        // Fall back to configured mark types
        return GetRandomSelectedMark();
    }

    private CM.ColorType GetRandomSelectedMark()
    {
        int count = 0;
        for (int i = 0; i < _allSelectableMarks.Length; i++)
        {
            if (HasColor(markTypes, _allSelectableMarks[i])) count++;
        }

        if (count <= 0) return CM.ColorType.Green;

        int pick = Random.Range(0, count);
        for (int i = 0; i < _allSelectableMarks.Length; i++)
        {
            CM.ColorType color = _allSelectableMarks[i];
            if (!HasColor(markTypes, color)) continue;
            if (pick == 0) return color;
            pick--;
        }

        return CM.ColorType.Green;
    }

    private static bool HasColor(MarkTypeMask mask, CM.ColorType color)
    {
        MarkTypeMask colorMask = ToMask(color);
        return colorMask != MarkTypeMask.None && (mask & colorMask) != 0;
    }

    private static MarkTypeMask ToMask(CM.ColorType color)
    {
        switch (color)
        {
            case CM.ColorType.Red: return MarkTypeMask.Red;
            case CM.ColorType.Blue: return MarkTypeMask.Blue;
            case CM.ColorType.Green: return MarkTypeMask.Green;
            case CM.ColorType.Yellow: return MarkTypeMask.Yellow;
            case CM.ColorType.Purple: return MarkTypeMask.Purple;
            case CM.ColorType.Orange: return MarkTypeMask.Orange;
            case CM.ColorType.White: return MarkTypeMask.White;
            case CM.ColorType.Black: return MarkTypeMask.Black;
            case CM.ColorType.Cyan: return MarkTypeMask.Cyan;
            case CM.ColorType.Gold: return MarkTypeMask.Gold;
            case CM.ColorType.Moss: return MarkTypeMask.Moss;
            default: return MarkTypeMask.None;
        }
    }
}
