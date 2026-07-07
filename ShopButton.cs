using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ShopButton : MonoBehaviour
{
    public TextMeshProUGUI costText;
    public TextMeshProUGUI descriptionText;
    public Button button;
    [SerializeField] private int itemCost = 1;

    [Tooltip("If false, this button will be non-interactable regardless of currency.")]
    [SerializeField] private bool eligible = true;

    private void Start()
    {
        costText.text = itemCost.ToString();
        UpdateInteractable();
        costText.color = CM.i.ColorTypeToColor(CM.ColorType.Gold);
    }
    public void SetEligible(bool value)
    {
        eligible = value;
        UpdateInteractable();
    }

    public bool IsEligible() => eligible;

    public int GetCost() => itemCost;

    public void SetItemCost(int cost)
    {
        itemCost = cost;
        if (costText != null)
        {
            costText.text = "$" + itemCost.ToString();
        }
        UpdateInteractable();
    }

    public void SetDescription(string text)
    {
        if (descriptionText != null)
            descriptionText.text = text;
    }

    private void OnEnable()
    {
        UpdateInteractable();
    }

    void Update()
    {
        UpdateInteractable();
    }

    private void UpdateInteractable()
    {
        if (button == null || CurrencyManager.instance == null)
        {
            if (button != null) button.interactable = false;
            return;
        }

        bool affordable = CurrencyManager.instance.GetCurrency() >= itemCost;
        button.interactable = eligible && affordable;
    }
}
