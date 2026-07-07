using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CurrencyManager : MonoBehaviour
{
    [System.Serializable]
    private struct CurrencyTier
    {
        public string name;
        [Min(1)] public int value;
        public CM.ColorType colorType;
        [Min(0.01f)] public float scaleMultiplier;
    }

    public static CurrencyManager instance;
    public GameObject currencyPrefab;
    public int currency = 100;
#if UNITY_EDITOR
    [Header("Editor")]
    [SerializeField] private int editorCurrency = 100;
#endif

    [Header("Pool")]
    [SerializeField] private int poolSize = 16;
    private readonly Queue<GameObject> currencyPool = new Queue<GameObject>();

    [Header("Currency Objects")]
    [SerializeField, Min(1)] private int maxActiveCurrency = 64;
    [SerializeField, Min(0f)] private float currencyTimeout = 60f;
    [SerializeField, Min(0f)] private float flashDurationBeforeRelease = 0.1f;
    [SerializeField] private CurrencyTier tier1 = new CurrencyTier { name = "Tier 1", value = 1, colorType = CM.ColorType.Gold, scaleMultiplier = 1f };
    [SerializeField] private CurrencyTier tier2 = new CurrencyTier { name = "Tier 2", value = 5, colorType = CM.ColorType.Yellow, scaleMultiplier = 1.25f };
    [SerializeField] private CurrencyTier tier3 = new CurrencyTier { name = "Tier 3", value = 10, colorType = CM.ColorType.Orange, scaleMultiplier = 1.5f };

    [Header("Homing Currency")]
    [SerializeField] private bool homingCurrency = false;
    [SerializeField] private Transform homingCurrencyTarget;
    [SerializeField, Min(0f)] private float homingVelocityThreshold = 0.15f;
    [SerializeField, Min(0f)] private float homingAcceleration = 18f;
    [SerializeField, Min(0f)] private float homingMaxSpeed = 12f;
    [SerializeField, Min(0f)] private float homingPickupDistance = 0.1f;

    private readonly HashSet<Currency> activeCurrencyObjects = new HashSet<Currency>();
    public IReadOnlyCollection<Currency> ActiveCurrencyObjects => activeCurrencyObjects;

    private readonly Dictionary<Currency, float> currencySpawnTimes = new Dictionary<Currency, float>();
    private readonly HashSet<Currency> pendingReleaseCurrency = new HashSet<Currency>();
    private readonly HashSet<Currency> homingCurrencyObjects = new HashSet<Currency>();


    private void Awake()
    {
        instance = this;

#if UNITY_EDITOR
        if (Application.isEditor)
        {
            currency = editorCurrency;
        }
#endif

        if (currencyPrefab != null)
        {
            for (int i = 0; i < poolSize; i++)
            {
                var obj = Instantiate(currencyPrefab, transform);
                
                obj.SetActive(false);
                currencyPool.Enqueue(obj);
            }
        }
    }

    public GameObject GetPooledCurrency(Vector3 position, Quaternion rotation)
    {
        return GetPooledCurrency(position, rotation, tier1);
    }

    private GameObject GetPooledCurrency(Vector3 position, Quaternion rotation, CurrencyTier tier)
    {
        if (maxActiveCurrency > 0 && activeCurrencyObjects.Count >= maxActiveCurrency)
        {
            PickupRandomActiveCurrency();
        }

        GameObject obj = null;
        while (currencyPool.Count > 0 && obj == null)
        {
            obj = currencyPool.Dequeue();
        }

        if (obj == null && currencyPrefab != null)
        {
            obj = Instantiate(currencyPrefab, transform);
        }

        if (obj != null)
        {
            obj.transform.SetPositionAndRotation(position, rotation);
            obj.SetActive(true);

            Currency currencyComponent = obj.GetComponent<Currency>();
            if (currencyComponent != null)
            {
                currencyComponent.Configure(tier.value, tier.colorType, tier.scaleMultiplier);
                pendingReleaseCurrency.Remove(currencyComponent);
                homingCurrencyObjects.Remove(currencyComponent);
                activeCurrencyObjects.Add(currencyComponent);
                currencySpawnTimes[currencyComponent] = Time.time;
            }
        }

        return obj;
    }

    public void ReleasePooledCurrency(GameObject obj)
    {
        if (obj == null) return;

        Currency currencyComponent = obj.GetComponent<Currency>();
        if (currencyComponent != null)
        {
            activeCurrencyObjects.Remove(currencyComponent);
            currencySpawnTimes.Remove(currencyComponent);
            pendingReleaseCurrency.Remove(currencyComponent);
            homingCurrencyObjects.Remove(currencyComponent);
        }

        obj.SetActive(false);
        obj.transform.SetParent(transform);
        currencyPool.Enqueue(obj);
    }

    private void Update()
    {
        if (activeCurrencyObjects.Count == 0) return;

        float now = Time.time;
        List<Currency> expired = null;
        List<Currency> collectedByHoming = null;

        foreach (var currencyObj in activeCurrencyObjects)
        {
            if (currencyObj == null || pendingReleaseCurrency.Contains(currencyObj)) continue;

            if (currencyTimeout > 0f
                && currencySpawnTimes.TryGetValue(currencyObj, out float spawnTime)
                && now - spawnTime >= currencyTimeout)
            {
                expired ??= new List<Currency>();
                expired.Add(currencyObj);
                continue;
            }

            if (homingCurrency && TryUpdateHomingCurrency(currencyObj))
            {
                collectedByHoming ??= new List<Currency>();
                collectedByHoming.Add(currencyObj);
            }
        }

        if (expired != null)
        {
            for (int i = 0; i < expired.Count; i++)
            {
                TryPickupCurrency(expired[i]);
            }
        }

        if (collectedByHoming != null)
        {
            for (int i = 0; i < collectedByHoming.Count; i++)
            {
                TryPickupCurrency(collectedByHoming[i], showPickupText: false);
            }
        }
    }

    public bool TryPickupCurrency(Currency currencyObj)
    {
        bool showPickupText = !homingCurrencyObjects.Contains(currencyObj);
        return TryPickupCurrency(currencyObj, showPickupText);
    }

    private bool TryPickupCurrency(Currency currencyObj, bool showPickupText)
    {
        if (currencyObj == null) return false;
        if (!activeCurrencyObjects.Contains(currencyObj)) return false;
        if (pendingReleaseCurrency.Contains(currencyObj)) return false;

        if (showPickupText)
        {
            AddCurrency(currencyObj.Value, currencyObj.transform.position);
        }
        else
        {
            AddCurrency(currencyObj.Value);
        }

        FlashThenRelease(currencyObj);
        return true;
    }

    private void PickupRandomActiveCurrency()
    {
        if (activeCurrencyObjects.Count == 0) return;

        var candidates = new List<Currency>(activeCurrencyObjects.Count);
        foreach (var c in activeCurrencyObjects)
        {
            if (c == null) continue;
            if (pendingReleaseCurrency.Contains(c)) continue;
            candidates.Add(c);
        }

        if (candidates.Count == 0) return;

        var randomCurrency = candidates[Random.Range(0, candidates.Count)];
        TryPickupCurrency(randomCurrency);
    }

    private void DespawnCurrency(Currency currencyObj)
    {
        if (currencyObj == null) return;
        if (!activeCurrencyObjects.Contains(currencyObj)) return;
        if (pendingReleaseCurrency.Contains(currencyObj)) return;

        FlashThenRelease(currencyObj);
    }

    private void FlashThenRelease(Currency currencyObj)
    {
        if (currencyObj == null) return;
        if (pendingReleaseCurrency.Contains(currencyObj)) return;
        StartCoroutine(FlashThenReleaseRoutine(currencyObj));
    }

    private IEnumerator FlashThenReleaseRoutine(Currency currencyObj)
    {
        if (currencyObj == null) yield break;
        pendingReleaseCurrency.Add(currencyObj);

        currencyObj.Flash(flashDurationBeforeRelease);
        if (flashDurationBeforeRelease > 0f)
        {
            yield return new WaitForSeconds(flashDurationBeforeRelease);
        }

        if (currencyObj != null && activeCurrencyObjects.Contains(currencyObj))
        {
            ReleasePooledCurrency(currencyObj.gameObject);
        }
        else
        {
            pendingReleaseCurrency.Remove(currencyObj);
        }
    }

    public void AddCurrency(int amount)
    {
        if (amount <= 0) return;
        currency += amount;

        if (SaveDataManager.instance != null)
        {
            SaveDataManager.instance.NotifyCurrencyAmount(currency);
            SaveDataManager.instance.TryUnlockDevilsPactByProgress();
        }

        if (GameController.instance != null) GameController.instance.RefreshCurrencyUI();
    }

    private bool TryUpdateHomingCurrency(Currency currencyObj)
    {
        if (currencyObj == null) return false;

        Rigidbody2D rb = currencyObj.GetComponent<Rigidbody2D>();
        if (rb == null) return false;

        if (!TryGetHomingTargetWorldPosition(currencyObj.transform.position.z, out var targetPosition)) return false;

        bool isHoming = homingCurrencyObjects.Contains(currencyObj);

        if (!isHoming)
        {
            if (rb.linearVelocity.magnitude > Mathf.Max(0f, homingVelocityThreshold)) return false;

            homingCurrencyObjects.Add(currencyObj);
            ShowCurrencyText(currencyObj.Value, currencyObj.transform.position);
            isHoming = true;
        }

        if (!isHoming) return false;

        rb.angularVelocity = 0f;

        Vector2 current = rb.position;
        Vector2 target2 = new Vector2(targetPosition.x, targetPosition.y);
        Vector2 toTarget = target2 - current;
        float pickupDistance = Mathf.Max(0f, homingPickupDistance);
        if (toTarget.magnitude <= pickupDistance)
        {
            rb.linearVelocity = Vector2.zero;
            return true;
        }

        Vector2 desiredVelocity = toTarget.normalized * Mathf.Max(0f, homingMaxSpeed);
        rb.linearVelocity = Vector2.MoveTowards(
            rb.linearVelocity,
            desiredVelocity,
            Mathf.Max(0f, homingAcceleration) * Time.deltaTime);

        return false;
    }

    private bool TryGetHomingTargetWorldPosition(float worldZ, out Vector3 worldPosition)
    {
        worldPosition = default;

        Transform target = GetHomingCurrencyTarget();
        if (target == null) return false;

        RectTransform rect = target as RectTransform;
        if (rect == null)
        {
            worldPosition = target.position;
            worldPosition.z = worldZ;
            return true;
        }

        Canvas canvas = rect.GetComponentInParent<Canvas>();
        Camera cam = Camera.main;
        if (cam == null) return false;

        Camera uiCamera = null;
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            uiCamera = canvas.worldCamera;
        }

        Vector3 rectCenterWorld = rect.TransformPoint(rect.rect.center);
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(uiCamera, rectCenterWorld);
        float cameraDistance = worldZ - cam.transform.position.z;
        worldPosition = cam.ScreenToWorldPoint(new Vector3(screenPoint.x, screenPoint.y, cameraDistance));
        worldPosition.z = worldZ;
        return true;
    }

    private Transform GetHomingCurrencyTarget()
    {
        if (homingCurrencyTarget != null) return homingCurrencyTarget;
        if (GameInfoDisplay.instance == null || GameInfoDisplay.instance.currencyText == null) return null;
        return GameInfoDisplay.instance.currencyText.rectTransform;
    }

    public void AddCurrency(int amount, Vector3 position)
    {
        //TextObjectPool.instance.PlayFloatingText(position, "$" + amount.ToString(), CM.i.ColorTypeToColor(CM.ColorType.Gold), .3f, 2);
        ShowCurrencyText(amount, position);
        AddCurrency(amount);
    }

    public void ShowCurrencyText(int amount, Vector3 position)
    {
        string text = (amount >= 0 ? "+" : "-") + "$" + Mathf.Abs(amount).ToString();
        float textSize = 0.2f + (Mathf.Abs(amount) * 0.04f); // Base size + proportional scaling
        TextObjectPool.instance.PlayFloatingText(position, text, CM.i.ColorTypeToColor(CM.ColorType.Gold), textSize, 2);
    }
    public void RemoveCurrency(int amount)
    {
        if (amount <=0) return;
        currency -= amount;
        if (SaveDataManager.instance != null)
        {
            SaveDataManager.instance.NotifyCurrencyAmount(currency);
            SaveDataManager.instance.TryUnlockDevilsPactByProgress();
        }
        if (GameController.instance != null) GameController.instance.RefreshCurrencyUI();
        else if (GameInfoDisplay.instance != null && GameInfoDisplay.instance.currencyText != null)
            GameInfoDisplay.instance.currencyText.text = (GameInfoDisplay.instance.currencyPrefix ?? string.Empty) + currency.ToString();
    }

    public int GetCurrency()
    {
        return currency;
    }

    public void SpawnCurrencyForAmount(int amount, Vector3 position)
    {
        if (amount <= 0) return;

        int remaining = amount;
        CurrencyTier[] tiersDescending = BuildTiersDescending();

        for (int i = 0; i < tiersDescending.Length; i++)
        {
            CurrencyTier tier = tiersDescending[i];
            if (tier.value <= 0) continue;

            int count = remaining / tier.value;
            if (count <= 0) continue;

            for (int j = 0; j < count; j++)
            {
                GetPooledCurrency(position, Quaternion.identity, tier);
            }

            remaining -= count * tier.value;
            if (remaining <= 0) break;
        }

        if (remaining > 0)
        {
            for (int i = 0; i < remaining; i++)
            {
                GetPooledCurrency(position, Quaternion.identity, tier1);
            }
        }
    }

    private CurrencyTier[] BuildTiersDescending()
    {
        CurrencyTier[] tiers = { tier1, tier2, tier3 };
        System.Array.Sort(tiers, (a, b) => b.value.CompareTo(a.value));
        return tiers;
    }
}
