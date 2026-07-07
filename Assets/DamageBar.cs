using TMPro;
using UnityEngine;

public class DamageBar : MonoBehaviour
{
    public TextMeshProUGUI rankText;
    public UnityEngine.UI.Image barImage;
    public Transform towerSlotPosition;
    public TextMeshProUGUI damageText;

    [HideInInspector] public GameObject displayedTowerObject;
    [HideInInspector] public Tower.ID displayedTowerId;

    public void SetBarImagePercentage(float percent)
    {
        //barImage.rectTransform.sizeDelta = new Vector2(Mathf.Lerp(130, 0, percent), barImage.rectTransform.sizeDelta.y);
        barImage.fillAmount = percent;
    }
}
