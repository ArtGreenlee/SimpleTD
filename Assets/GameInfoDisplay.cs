using TMPro;
using UnityEngine;

public class GameInfoDisplay : MonoBehaviour
{
    public static GameInfoDisplay instance;

    [Header("UI")]
    public TextMeshProUGUI waveText;
    public TextMeshProUGUI livesText;
    public TextMeshProUGUI currencyText;
    public TextMeshProUGUI placedTowerText;

    [Header("Prefixes")]
    public string wavePrefix = "Wave: ";
    public string livesPrefix = "Lives: ";
    public string currencyPrefix = "$";
    public string placedTowersPrefix = "Towers: ";

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            // Avoid overwriting a live instance; only remove the duplicate component.
            Destroy(this);
            return;
        }

        instance = this;
    }

    private void OnDisable()
    {
        if (instance == this) instance = null;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void OnEnable()
    {
        // Do a one-time best-effort refresh when UI becomes visible.
        ForceRefresh();
    }

    public void ForceRefresh()
    {
        RefreshWaveText();
        RefreshLivesText();
        RefreshCurrencyText();
        RefreshPlacedTowersText();
    }

    public void RefreshWaveText()
    {
        if (waveText == null) return;

        int waveNumber =0;
        if (WaveManager.instance != null)
        {
            waveNumber = WaveManager.instance.GetCurrentWaveNumber();
        }

        waveText.text = (wavePrefix ?? string.Empty) + waveNumber.ToString();
    }

    public void RefreshLivesText()
    {
        if (livesText == null) return;

        int lives =0;
        if (GameController.instance != null)
        {
            lives = GameController.instance.GetLives();
        }

        livesText.text = (livesPrefix ?? string.Empty) + lives.ToString();
    }

    public void RefreshCurrencyText()
    {
        if (currencyText == null) return;

        int c =0;
        if (CurrencyManager.instance != null)
        {
            c = CurrencyManager.instance.GetCurrency();
        }

        currencyText.text = (currencyPrefix ?? string.Empty) + c.ToString();
    }

    public void RefreshPlacedTowersText()
    {
        if (placedTowerText == null) return;

        int placed = 0;
        int max = 0;
        bool atCap = false;
        if (TowerManager.instance != null)
        {
            placed = TowerManager.instance.GetCurrentPlacedTowers();
            max = TowerManager.instance.GetMaximumPlacedTowers();
            atCap = max > 0 && placed >= max;
        }

        placedTowerText.text = (placedTowersPrefix ?? string.Empty) + placed.ToString() + "/" + max.ToString();
        placedTowerText.color = atCap && CM.i != null ? CM.i.ColorTypeToColor(CM.ColorType.Red) : Color.white;
    }
}
