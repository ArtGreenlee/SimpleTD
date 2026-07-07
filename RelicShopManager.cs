using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RelicShopManager : MonoBehaviour
{
    public static RelicShopManager instance;

    [Header("Debug")]
    public List<RM.ID> debugPrioritizedRerollRelics = new List<RM.ID>();

    [Header("Exclusions")]
    public List<RM.ID> permanentlyExcludedRelicIds = new List<RM.ID>();

    [Header("UI")]
    public List<TextMeshProUGUI> costTexts;

    [Header("Shop Slots")]
    public List<Transform> relicShopPositions;

    [Header("Source")]
    [SerializeField] private RM relicManager;

    private readonly List<RM.ID> _shopRelicIds = new List<RM.ID>(32);
    private readonly List<GameObject> _spawnedShopRelics = new List<GameObject>(16);
    private readonly HashSet<Relic> _shopRelicSet = new HashSet<Relic>();

    private bool _initialized;

    public KeyCode RefreshRelicShopKey;
    public TextMeshProUGUI refreshCostText;
    public Button refreshButton;
    public int refreshCost = 2;
    public int refreshCostIncrement = 2;
    [Header("Wave Refresh")]
    [SerializeField] private bool freeRefreshAfterEachWave = false;

    private bool _wasWaveActive;
    private bool _hasFreeRefreshAvailable;

    private void Awake()
    {
        instance = this;
    }

    private void Start()
    {
        _wasWaveActive = WaveManager.instance != null && WaveManager.instance.IsWaveActive();
        StartCoroutine(InitializeShopRoutine());
    }

    private IEnumerator InitializeShopRoutine()
    {
        // Wait one frame so other startup systems finish establishing relic state before the first shop build.
        yield return null;

        PopulateShopSlots();
        BuildShop(prioritizeDebugList: false);
        SyncShopRelicPositions();
        UpdateShopTexts();
        UpdateRefreshText();
        _initialized = true;
    }

    private void Update()
    {
        if (!_initialized) return;

        UpdateWaveRefreshState();

        if (Input.GetKeyDown(RefreshRelicShopKey))
        {
            OnRefreshButtonPressed();
        }
        SyncShopRelicPositions();
        UpdateShopTexts();
    }

    private void UpdateWaveRefreshState()
    {
        bool isWaveActive = WaveManager.instance != null && WaveManager.instance.IsWaveActive();
        if (_wasWaveActive && !isWaveActive && freeRefreshAfterEachWave)
        {
            _hasFreeRefreshAvailable = true;
            UpdateRefreshText();
        }

        _wasWaveActive = isWaveActive;
    }

    public void PopulateShopSlots()
    {
        if (relicManager == null) relicManager = RM.i != null ? RM.i : FindFirstObjectByType<RM>();

        _shopRelicIds.Clear();
        if (relicManager == null || relicManager.relicPrefab == null) return;

        var allIds = (RM.ID[])System.Enum.GetValues(typeof(RM.ID));
        for (int i = 0; i < allIds.Length; i++)
        {
            var id = allIds[i];
            if (!relicManager.IsRelicAvailable(id)) continue;
            if (!relicManager.IsUnlocked(id)) continue;
            if (relicManager.Active(id)) continue;
            if (IsPermanentlyExcluded(id)) continue;
            _shopRelicIds.Add(id);
        }
    }

    public void RefreshShop(bool prioritizeDebugList = false)
    {
        PopulateShopSlots();
        BuildShop(prioritizeDebugList);
        SyncShopRelicPositions();
        UpdateShopTexts();
    }
    public void UpdateRefreshText()
    {
        string costLabel;
        if (IsRefreshFree())
        {
            costLabel = CM.i != null ? CM.i.RTC(CM.ColorType.Gold, "free") : "free";
        }
        else
        {
            string paidCost = "$" + GetEffectiveRefreshCost().ToString();
            costLabel = CM.i != null ? CM.i.RTC(CM.ColorType.Gold, paidCost) : paidCost;
        }

        refreshCostText.text = "Refresh " + costLabel + " (" + RefreshRelicShopKey.ToString() + ")";
        SyncRefreshButtonAffordability();
    }

    private bool IsRefreshFree()
    {
        return freeRefreshAfterEachWave && _hasFreeRefreshAvailable;
    }

    private int GetEffectiveRefreshCost()
    {
        if (RM.i != null && RM.i.Active(RM.ID.rerollDiscount))
            return Mathf.Max(1, Mathf.RoundToInt(refreshCost * 0.9f));
        return refreshCost;
    }

    public void OnRefreshButtonPressed()
    {
        if (IsRefreshFree())
        {
            _hasFreeRefreshAvailable = false;
            _wasWaveActive = WaveManager.instance != null && WaveManager.instance.IsWaveActive();
            RefreshShop(prioritizeDebugList: true);
            UpdateRefreshText();
            return;
        }

        int cost = GetEffectiveRefreshCost();
        if (CurrencyManager.instance == null || CurrencyManager.instance.GetCurrency() < cost) return;
        CurrencyManager.instance.RemoveCurrency(cost);
        RefreshShop(prioritizeDebugList: true);
        refreshCost += refreshCostIncrement;
        if (SaveDataManager.instance != null) SaveDataManager.instance.NotifyReroll();
        UpdateRefreshText();
    }

    private void SyncRefreshButtonAffordability()
    {
        if (refreshButton == null) return;

        if (IsRefreshFree())
        {
            refreshButton.interactable = true;
            return;
        }

        int cost = GetEffectiveRefreshCost();
        bool canAfford = CurrencyManager.instance != null && CurrencyManager.instance.GetCurrency() >= cost;
        refreshButton.interactable = canAfford;
    }

    private void BuildShop(bool prioritizeDebugList)
    {
        if (relicShopPositions == null || relicShopPositions.Count == 0) return;

        for (int i = _spawnedShopRelics.Count - 1; i >= 0; i--)
        {
            var go = _spawnedShopRelics[i];
            if (go == null)
            {
                _spawnedShopRelics.RemoveAt(i);
                continue;
            }

            var relic = go.GetComponent<Relic>();
            if (relic != null) _shopRelicSet.Remove(relic);

            Destroy(go);
            _spawnedShopRelics.RemoveAt(i);
        }

        _spawnedShopRelics.Clear();
        _shopRelicSet.Clear();

        for (int i = 0; i < relicShopPositions.Count; i++)
        {
            GameObject shopRelic = null;
            var posT = relicShopPositions[i];
            if (posT != null)
            {
                if (TryGetShopRelicId(prioritizeDebugList, out var shopRelicId) && relicManager != null && relicManager.relicPrefab != null)
                {
                    shopRelic = Instantiate(relicManager.relicPrefab, posT.position, posT.rotation);
                    shopRelic.transform.SetParent(null, true);
                    shopRelic.transform.position = posT.position;
                    shopRelic.transform.rotation = posT.rotation;

                    var sw = shopRelic.GetComponent<SwellAnimation>();
                    if (sw == null) sw = shopRelic.AddComponent<SwellAnimation>();
                    sw.swellInOnEnable = true;

                    var relic = shopRelic.GetComponent<Relic>();
                    if (relic == null) relic = shopRelic.AddComponent<Relic>();
                    if (relic != null)
                    {
                        relic.id = shopRelicId;
                        relicManager.ConfigureRelicInstance(relic, shopRelicId);
                        _shopRelicSet.Add(relic);
                    }

                    ConsumeDebugRelicIfShown(shopRelicId);
                }
            }

            _spawnedShopRelics.Add(shopRelic);
        }

        SyncShopRelicPositions();
    }

    private bool TryGetShopRelicId(bool prioritizeDebugList, out RM.ID id)
    {
        if (prioritizeDebugList && TryGetDebugPriorityShopRelicId(out id))
        {
            return true;
        }

        return TryGetRandomShopRelicId(out id);
    }

    private bool TryGetDebugPriorityShopRelicId(out RM.ID id)
    {
        id = default;

        if (debugPrioritizedRerollRelics == null || debugPrioritizedRerollRelics.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < debugPrioritizedRerollRelics.Count; i++)
        {
            RM.ID candidate = debugPrioritizedRerollRelics[i];
            if (_shopRelicIds.Contains(candidate))
            {
                id = candidate;
                return true;
            }
        }

        return false;
    }

    private void ConsumeDebugRelicIfShown(RM.ID shownId)
    {
        if (debugPrioritizedRerollRelics == null || debugPrioritizedRerollRelics.Count == 0)
        {
            return;
        }

        int index = debugPrioritizedRerollRelics.IndexOf(shownId);
        if (index >= 0)
        {
            debugPrioritizedRerollRelics.RemoveAt(index);
        }
    }

    private bool IsPermanentlyExcluded(RM.ID relicId)
    {
        return permanentlyExcludedRelicIds != null && permanentlyExcludedRelicIds.Contains(relicId);
    }

    private void SyncShopRelicPositions()
    {
        int slots = relicShopPositions != null ? relicShopPositions.Count : 0;
        for (int i = 0; i < slots; i++)
        {
            if (i < 0 || i >= _spawnedShopRelics.Count) continue;

            var go = _spawnedShopRelics[i];
            var slot = relicShopPositions[i];
            if (go == null || slot == null) continue;

            go.transform.position = slot.position;
            go.transform.rotation = slot.rotation;
        }
    }

    private bool TryGetRandomShopRelicId(out RM.ID id)
    {
        if (_shopRelicIds.Count == 0)
        {
            id = default;
            return false;
        }

        id = _shopRelicIds[Random.Range(0, _shopRelicIds.Count)];
        return true;
    }

    private void UpdateShopTexts()
    {
        int slots = relicShopPositions != null ? relicShopPositions.Count : 0;
        int currency = CurrencyManager.instance != null ? CurrencyManager.instance.GetCurrency() : int.MaxValue;

        for (int i = 0; i < slots; i++)
        {
            TextMeshProUGUI txt = (costTexts != null && i >= 0 && i < costTexts.Count) ? costTexts[i] : null;
            GameObject go = (i >= 0 && i < _spawnedShopRelics.Count) ? _spawnedShopRelics[i] : null;
            var relic = go != null ? go.GetComponent<Relic>() : null;

            if (txt == null) continue;

            if (relic == null)
            {
                txt.text = string.Empty;
                continue;
            }

            bool owned = relicManager != null && relicManager.Active(relic.id);
            int cost = relicManager != null ? relicManager.GetRelicCost(relic.id) : 1;

            if (owned)
            {
                txt.text = "Owned";
            }
            else
            {
                txt.text = "$" + Mathf.Max(0, cost);
            }

            if (CM.i != null)
            {
                bool affordable = currency >= cost;
                txt.color = owned
                    ? CM.i.ColorTypeToColor(CM.ColorType.White)
                    : (affordable ? CM.i.ColorTypeToColor(CM.ColorType.Gold) : CM.i.ColorTypeToColor(CM.ColorType.Red));
            }
        }
    }

    public bool IsShopRelic(Relic relic)
    {
        return relic != null && _shopRelicSet.Contains(relic);
    }

    public bool CanPurchaseRelic(Relic relic)
    {
        if (relicManager == null) relicManager = RM.i != null ? RM.i : FindFirstObjectByType<RM>();

        if (relic == null || relicManager == null) return false;
        if (!relicManager.IsRelicAvailable(relic.id)) return false;
        if (!_shopRelicSet.Contains(relic)) return false;
        if (IsPermanentlyExcluded(relic.id)) return false;
        if (!relicManager.IsUnlocked(relic.id)) return false;
        if (relicManager.Active(relic.id)) return false;

        int cost = relicManager.GetRelicCost(relic.id);
        if (CurrencyManager.instance == null) return true;

        return CurrencyManager.instance.GetCurrency() >= cost;
    }

    public bool TryPurchaseRelic(Relic relic)
    {
        if (!CanPurchaseRelic(relic))
        {
            return false;
        }

        int cost = relicManager.GetRelicCost(relic.id);
        if (cost > 0 && CurrencyManager.instance != null)
        {
            CurrencyManager.instance.RemoveCurrency(cost);
        }

        PlayPickupVfxAt(relic.transform.position);

        int slotIndex = _spawnedShopRelics.IndexOf(relic.gameObject);
        if (slotIndex >= 0) _spawnedShopRelics[slotIndex] = null;
        _shopRelicSet.Remove(relic);

        var relicId = relic.id;

        var sw = relic.GetComponent<SwellAnimation>();
        if (sw == null) sw = relic.gameObject.AddComponent<SwellAnimation>();
        sw.swellInOnEnable = false;
        sw.SwellOut(true);

        relicManager.Activate(relicId);

        RefreshShop();
        return true;
    }

    private void PlayPickupVfxAt(Vector3 worldPos)
    {
        if (PIC.instance == null || PIC.instance.pickupDropVfx == null) return;

        PIC.instance.pickupDropVfx.transform.position = worldPos;
        PIC.instance.pickupDropVfx.Play();
    }
}
