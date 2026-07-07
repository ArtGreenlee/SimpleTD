using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class CM : MonoBehaviour
{
    public static CM i;
    public enum ColorType
    {
        None,
        Red,
        Blue,
        Green,
        Yellow,
        Purple,
        Orange,
        White,
        Black,
        Cyan,
        Gold,
        Moss,
        RarityTier1,
        RarityTier2,
        RarityTier3,
    }

    public static readonly ColorType[] RandomizableDamageTypes =
    {
        ColorType.Red,
        ColorType.Blue,
        ColorType.Green,
        ColorType.Purple,
        ColorType.Orange,
        ColorType.White
    };

    [Serializable] public struct ColorData
    {
        public ColorType colorType;
        public Color colorValue;
    }

    public Dictionary<ColorType, Color> colorDictionary = new Dictionary<ColorType, Color>();

    public List<ColorData> colors;
    private void Awake()
    {
        i = this;
        foreach (var color in colors)
        {
            colorDictionary[color.colorType] = color.colorValue;
        }
    }

    public Color ColorTypeToColor(ColorType colorType)
    {
        return colorDictionary.ContainsKey(colorType) ? colorDictionary[colorType] : Color.white;
    }

    public static ColorType GetExposeColor()
    {
        return ColorType.Red;
    }

    public string RTC(ColorType colorType)
    {
        Color color = ColorTypeToColor(colorType);
        string hexColor = ColorUtility.ToHtmlStringRGBA(color);
        return $"<color=#{hexColor}>" + ColorToName(colorType) + "</color>";
    }

    public string RTC(ColorType colorType, string text)
    {
        Color color = ColorTypeToColor(colorType);
        string hexColor = ColorUtility.ToHtmlStringRGBA(color);
        return $"<color=#{hexColor}>{text}</color>";
    }

    public string ColorToTooltip(ColorType color)
    {
        switch (color)
        {
            case ColorType.Red:
                return "NOT IMPLEMENTED";
            case ColorType.Blue:
                return RTC(ColorType.Blue, "Slowed") + " enemies are slowed by up to a max amount, decaying at different rates";
            case ColorType.Green:
                return "NOT IMPLEMENTED";
            case ColorType.Orange:
                return RTC(ColorType.Orange, "Burning") + " enemies take " + Mathf.RoundToInt(Health.fireTickDamageGlobal).ToString() + " damage every " + Mathf.RoundToInt(1 / Health.fireTickCooldownGlobal) + " times per second";
            case ColorType.Purple:
                return RTC(GetExposeColor(), "Exposed") + " enemies take " + RTC(ColorType.Green, Mathf.RoundToInt((Tower.exposeMultiplerGlobal * 100)).ToString() + "%") + " more damage.";
            case ColorType.Yellow:
                return "NOT IMPLEMENTED";
            case ColorType.White:
                return "NOT IMPLEMENTED";
            case ColorType.Cyan:
                return "Electric";
            default:
                return "Unknown color";
        }
    }

    public string ColorToName(ColorType color)
    {
        switch (color)
        {
            case ColorType.Red:
                return "Red";
            case ColorType.Blue:
                return "Blue";
            case ColorType.Green:
                return "Green";
            case ColorType.Orange:
                return "Orange";
            case ColorType.Purple:
                return "Purple";
            case ColorType.Yellow:
                return "Yellow";
            case ColorType.White:
                return "White";
            case ColorType.Black:
                return "Black";
            case ColorType.Cyan:
                return "Cyan";
            default:
                return "Unknown";

        }
    }

    public ColorType TagToColorType(Tower.Tag tag)
    {
        switch (tag)
        {
            case Tower.Tag.Red:
                return ColorType.Red;
            case Tower.Tag.Blue:
                return ColorType.Blue;
            case Tower.Tag.Green:
                return ColorType.Green;
            case Tower.Tag.Yellow:
                return ColorType.Yellow;
            case Tower.Tag.Orange:
                return ColorType.Orange;
            case Tower.Tag.Purple:
                return ColorType.Purple;
            case Tower.Tag.White:
                return ColorType.White;
            case Tower.Tag.Black:
                return ColorType.Black;
            case Tower.Tag.Cyan:
                return ColorType.Cyan;
            default:
                return ColorType.White;
        }
    }

    public Color TagToColor(Tower.Tag tag)
    {
        return ColorTypeToColor(TagToColorType(tag));
    }
}
