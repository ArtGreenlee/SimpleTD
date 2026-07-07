using UnityEngine;
using UnityEngine.SceneManagement;

public class GameController : MonoBehaviour
{
    public static GameController instance;

    [SerializeField] int lifeCount = 20;

    private void Awake()
    {
        instance = this;
    }

    private void Start()
    {
        // Initialize UI once at start.
        RefreshLivesUI();
        RefreshCurrencyUI();
    }

    private void Update()
    {
        bool shiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        if (shiftHeld && Input.GetKeyDown(KeyCode.R))
        {
            RestartGame();
        }
    }

    public int GetLives() => lifeCount;

    public void LoseLifes(int amount)
    {
        if (amount <= 0) return;

        lifeCount -= amount;
        RefreshLivesUI();

        if (SaveDataManager.instance != null)
        {
            SaveDataManager.instance.TryUnlockDevilsPactByProgress();
        }

        if (lifeCount <= 0)
        {
            //Debug.Log("Game Over!");
            // Implement game over logic here (e.g., show game over screen, restart game, etc.)
        }
    }

    public void AddLifes(int amount)
    {
        if (amount <= 0) return;

        lifeCount += amount;
        RefreshLivesUI();

        if (SaveDataManager.instance != null)
        {
            SaveDataManager.instance.TryUnlockDevilsPactByProgress();
        }
    }

    public void RemoveCurrency(int amount)
    {
        if (amount <= 0) return;
        if (CurrencyManager.instance == null) return;

        CurrencyManager.instance.RemoveCurrency(amount);
        RefreshCurrencyUI();
    }

    public void RefreshLivesUI()
    {
        if (GameInfoDisplay.instance == null) return;
        if (GameInfoDisplay.instance.livesText == null) return;

        GameInfoDisplay.instance.livesText.text = (GameInfoDisplay.instance.livesPrefix ?? string.Empty) + lifeCount.ToString();
        GameInfoDisplay.instance.RefreshLivesText();
    }

    public void RefreshCurrencyUI()
    {
        if (GameInfoDisplay.instance == null) return;
        if (GameInfoDisplay.instance.currencyText == null) return;
        if (CurrencyManager.instance == null) return;

        GameInfoDisplay.instance.currencyText.text = (GameInfoDisplay.instance.currencyPrefix ?? string.Empty) + CurrencyManager.instance.GetCurrency().ToString();
        GameInfoDisplay.instance.RefreshCurrencyText();
    }

    public void RestartGame()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(activeScene.buildIndex);
    }
}
