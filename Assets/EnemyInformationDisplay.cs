using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EnemyInformationDisplay : MonoBehaviour
{
    public static EnemyInformationDisplay instance;
    public GameObject enemyInformationPanel;
    public GameObject panelObject;
    public Enemy displayedEnemy;
    public TextMeshProUGUI enemyNameText;
    public TextMeshProUGUI descriptionText;
    public TextMeshProUGUI healthText;
    public TextMeshProUGUI maxSlowText;
    public TextMeshProUGUI DamageCooldownText;
    [Header("Enemy Health Bar")]
    [SerializeField] private Image enemyHealthBarImage;
    [SerializeField] private TextMeshProUGUI enemyHealthPercentText;
    [SerializeField, Range(0f, 1f)] private float healthFillPercent;
    public string healthPrefix = "Health: ";
    public string maxSlowPrefix= "Max Slow: ";

    private RectTransform panelRect;
    private RectTransform _canvasRect;
    private PanelFollower _panelFollower;

    private void Awake()
    {
        instance = this;
        if (panelObject == null) panelObject = enemyInformationPanel;

        if (panelObject != null)
        {
            panelRect = panelObject.GetComponent<RectTransform>();
            _panelFollower = panelObject.GetComponent<PanelFollower>();
            panelObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (displayedEnemy == null)
        {
            if (enemyInformationPanel != null && enemyInformationPanel.activeSelf)
            {
                HideEnemyInformation();
            }
            return;
        }

        if (enemyNameText != null) enemyNameText.text = GetName(displayedEnemy);
        if (descriptionText != null) descriptionText.text = GetDescription(displayedEnemy);

        if (healthText != null && displayedEnemy.health != null)
        {
            healthText.text = healthPrefix + displayedEnemy.health.GetCurrentHealth().ToString("0") + "/" + displayedEnemy.health.GetMaxHealth().ToString("0");
        }
        UpdateHealthBarFill();

        var movement = displayedEnemy.GetMovement();
        if (maxSlowText != null && movement != null)
        {
            maxSlowText.text = maxSlowPrefix + (movement.GetMaxSlow() * 100f).ToString("0") + "%";
        }

        if (DamageCooldownText != null)
        {
            DamageCooldownText.text = "Damage: " + displayedEnemy.GetDamageToAgent().ToString("0.##") +
                                      " Cooldown: " + displayedEnemy.GetDamageToAgentCooldown().ToString("0.##");
        }

        if (_panelFollower != null)
        {
            _panelFollower.SetFollowTransform(displayedEnemy.transform);
        }
        else
        {
            PositionPanelNearEnemy(displayedEnemy.transform.position);
        }
    }

    public string GetDescription(Enemy enemy)
    {
        if (enemy == null) return string.Empty;
        return "unknown";
    }

    public string GetName(Enemy enemy)
    {
        if (enemy == null) return string.Empty;
        return enemy.name;
    }

    public void DisplayEnemyInformation(Enemy enemy)
    {
        displayedEnemy = enemy;
        if (enemy == null)
        {
            HideEnemyInformation();
            return;
        }

        // Ensure only one info panel is visible.
        if (TowerInformationDisplay.instance != null)
        {
            TowerInformationDisplay.instance.HideTowerInformation();
        }

        if (panelObject != null)
        {
            panelObject.SetActive(true);
            if (_panelFollower != null)
            {
                _panelFollower.SetFollowTransform(enemy.transform);
                _panelFollower.SnapToFollowTarget();
            }
            else
            {
                PositionPanelNearEnemy(enemy.transform.position);
            }
        }
    }

    public void HideEnemyInformation()
    {
        displayedEnemy = null;
        healthFillPercent = 0f;
        if (enemyHealthBarImage != null) enemyHealthBarImage.fillAmount = 0f;
        if (enemyHealthPercentText != null) enemyHealthPercentText.text = "0%";
        if (_panelFollower != null)
        {
            _panelFollower.SetFollowTransform(null);
        }
        if (panelObject != null) panelObject.SetActive(false);
    }

    private void PositionPanelNearEnemy(Vector3 enemyWorldPos)
    {
        Vector3 panelPosition = panelRect.anchoredPosition;
        if (enemyWorldPos.x < 0)
        {
            panelPosition.x = 265f;
        }
        else
        {
            panelPosition.x = -624.3f;
        }
        panelRect.anchoredPosition = panelPosition;
    }

    private void UpdateHealthBarFill()
    {
        if (displayedEnemy == null || displayedEnemy.health == null)
        {
            healthFillPercent = 0f;
            if (enemyHealthBarImage != null) enemyHealthBarImage.fillAmount = 0f;
            if (enemyHealthPercentText != null) enemyHealthPercentText.text = "0%";
            return;
        }

        float maxHealth = displayedEnemy.health.GetMaxHealth();
        if (maxHealth <= 0f)
        {
            healthFillPercent = 0f;
        }
        else
        {
            healthFillPercent = Mathf.Clamp01(displayedEnemy.health.GetCurrentHealth() / maxHealth);
        }

        if (enemyHealthBarImage != null)
        {
            enemyHealthBarImage.fillAmount = healthFillPercent;
        }

        if (enemyHealthPercentText != null)
        {
            enemyHealthPercentText.text = (healthFillPercent * 100f).ToString("0") + "%";
        }
    }
}
