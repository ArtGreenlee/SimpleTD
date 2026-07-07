using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class TagDisplay : MonoBehaviour
{
    public Tower.Tag towerTag;
    public GameObject tagInfoPanel;
    public Image outlineImage;
    public Image tagIcon;
    public TextMeshProUGUI titleText;
    public List<TextMeshProUGUI> tagTexts;
    public TextMeshProUGUI tagCountText;

    private Material _runtimeTagIconMaterial;

    private void Awake()
    {
        EnsureRuntimeTagIconMaterial();

        if (tagInfoPanel != null)
        {
            tagInfoPanel.SetActive(false);
        }

        SetOutlineVisible(false);
    }

    public void ApplyVisuals(Color iconColor, Color countTextColor)
    {
        if (tagIcon != null)
        {
            tagIcon.color = iconColor;
        }

        if (tagCountText != null)
        {
            tagCountText.color = countTextColor;
        }
    }

    public void Indicate()
    {
        var pool = AOEObjectPool.instance;
        if (pool == null) return;

        Color color = tagIcon != null ? tagIcon.color : Color.white;
        const float diameter = 0.4f;
        pool.Indicate(transform.position, diameter, color);
    }

    public void AnimateTagImage()
    {
        if (UIAnimation.instance == null) return;

        EnsureRuntimeTagIconMaterial();
        if (_runtimeTagIconMaterial == null) return;

        UIAnimation.instance.AnimateMaterial(_runtimeTagIconMaterial);
    }

    private void EnsureRuntimeTagIconMaterial()
    {
        if (tagIcon == null) return;
        if (_runtimeTagIconMaterial != null) return;
        if (tagIcon.material == null) return;

        _runtimeTagIconMaterial = new Material(tagIcon.material);
        tagIcon.material = _runtimeTagIconMaterial;
    }

    private void SetOutlineVisible(bool visible)
    {
        if (outlineImage == null) return;
        outlineImage.enabled = visible;
    }

    public void OnMouseEnter()
    {
        if (tagInfoPanel != null)
        {
            tagInfoPanel.SetActive(true);
        }

        SetOutlineVisible(true);
        SetText();
    }

    public void SetText()
    {
        if (TagManager.instance == null) return;

        if (!TagManager.instance.tagToTagInfo.TryGetValue(towerTag, out var tagInfo))
        {
            if (tagCountText != null)
            {
                tagCountText.text = "0";
            }

            if (tagTexts != null)
            {
                for (int i = 0; i < tagTexts.Count; i++)
                {
                    if (tagTexts[i] == null) continue;
                    tagTexts[i].text = "";
                    tagTexts[i].color = Color.gray;
                }
            }

            return;
        }

        //titleText.text = towerTag.ToString();
        for (int i = 0; i < tagInfo.tagLevelCounts.Count; i++)
        {
            if (tagTexts == null || i >= tagTexts.Count || tagTexts[i] == null) continue;

            tagTexts[i].text = "Level " + (i + 1).ToString() + ": " + tagInfo.tagLevelCounts[i].ToString() + " tower" + (tagInfo.tagLevelCounts[i] > 1 ? "s" : "") + "\n" + TagManager.instance.GetLevelDescription(towerTag, i + 1);
            if (tagInfo.tagLevelCounts[i] > 0)
            {
                tagTexts[i].color = Color.white;
            }
            else
            {
                tagTexts[i].color = Color.gray;
            }
        }

        if (tagTexts != null)
        {
            for (int i = tagInfo.tagLevelCounts.Count; i < tagTexts.Count; i++)
            {
                if (tagTexts[i] == null) continue;
                tagTexts[i].text = "";
            }
        }

        if (tagCountText != null)
        {
            tagCountText.text = TagManager.instance.GetTagCount(towerTag).ToString();
        }
    }

    public void OnMouseExit()
    {
        if (tagInfoPanel != null)
        {
            tagInfoPanel.SetActive(false);
        }

        SetOutlineVisible(false);
    }
}
