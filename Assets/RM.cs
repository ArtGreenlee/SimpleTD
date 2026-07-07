using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Linq;
using TMPro;
using UnityEngine;

public class RM : MonoBehaviour //Relic Manager
{
    public const float fireTickRateUnlock = 100;
    public static float zealGoalDistanceThreshold = 5f;
    public static float zealDamagePerStack = 0.1f;
    public static float wolfPackDamagePerTower = 0.05f;
    public static float vengeancePercentMaxHealthDamage = 0.2f;
    public static float plagueSpreadChance = 0.2f;
    public static float plagueSpreadRange = 3f;
    public static int plagueSpreadStacks = 1;
    public static float plagueUnlockPoisonDamage = 250f;
    public static int devilsPactRoundGold = 50;
    public static float inspirationRelicChargeAmount = 1f;
    public static float chargeOnCritKillChargeAmount = 1f;
    public static float inspirationRelicRadius = 3f;
    public static float genericDamageTypeBaseDamageBonus = 1f;
    public static float genericDamageTypeDamageMultiplierBonus = 0.1f;

    // Unlock thresholds
    public static int laserStunUnlockEnemiesHit = 10;
    public static int laserBounceUnlockBounces = 7;
    public static int wolfPackUnlockTowersPlaced = 8;
    public static int explosionTradeoffUnlockRedTagTowers = 5;
    public static int speedyAgentUnlockAgentsOnField = 10;
    public static int rerollDiscountUnlockRerolls = 100;
    public static float lightningChainUnlockDamage = 1000f;
    public static float overkillUnlockSingleHitDamage = 100f;
    public static int currencyDamageUnlockCurrency = 100;
    public static float exposeNearestUnlockExposedDamage = 1000f;

    [Header("Default Unlocks")]
    private List<ID> defaultUnlockedRelics = new List<ID>()
    {
        ID.redDamageBuff,
        ID.blueDamageBuff,
        ID.greenDamageBuff,
        ID.yellowDamageBuff,
        ID.purpleDamageBuff,
        ID.orangeDamageBuff,
        ID.whiteDamageBuff,
        ID.cyanDamageBuff,
        ID.blackDamageBuff,
        ID.goldDamageBuff,
        ID.mossDamageBuff,

    };

    private static readonly HashSet<ID> ExplicitUnlockConditionRelics = new HashSet<ID>
    {
        ID.fireTickRate,
        ID.overkill,
        ID.currencyDamage,
        ID.plague,
        ID.devilsPact,
        ID.exposeNearest,
        ID.laserStun,
        ID.LaserBounce,
        ID.wolfPack,
        ID.explosionTradeoff,
        ID.speedyAgent,
        ID.rerollDiscount,
        ID.lightningChain,
    };


    [Header("Relic Unlock Notification")]
    public TextMeshProUGUI RelicUnlockNotificationText;
    [Min(0f)] public float relicUnlockNotificationFadeSeconds = 2f;
    private Coroutine _relicUnlockNotificationRoutine;

    [Header("Wolf Pack Hover Visualization")]
    [SerializeField, Min(0.05f)] private float wolfPackHoverFloatingTextInterval = 0.12f;
    [SerializeField] private Vector3 wolfPackHoverFloatingTextOffset = new Vector3(0f, 0.7f, 0f);
    [SerializeField] private Color wolfPackHoverFloatingTextColor = Color.red;
    [SerializeField, Min(0.01f)] private float wolfPackHoverFloatingTextSize = 0.85f;
    private Coroutine _wolfPackHoverVisualizationRoutine;
    private bool _showWolfPackHoverVisualization;
    private readonly Dictionary<Tower, GameObject> _wolfPackHoverTextInstances = new Dictionary<Tower, GameObject>();

    public Dictionary<ID, List<CM.ColorType>> relicColorMapping = new Dictionary<ID, List<CM.ColorType>>()
    {
        { ID.fireTickRate, new List<CM.ColorType> { CM.ColorType.Orange } },
        { ID.slowDamageBuff, new List<CM.ColorType> { CM.ColorType.Blue, CM.ColorType.Red } },
        { ID.slowTickRate, new List<CM.ColorType> { CM.ColorType.Blue } },
        { ID.criticalSlow, new List<CM.ColorType> { CM.ColorType.Blue, CM.ColorType.Red, CM.ColorType.White } },
        { ID.criticalCharge, new List<CM.ColorType> { CM.ColorType.White, CM.ColorType.Purple } },
        { ID.criticalDamageBuff, new List<CM.ColorType> { CM.ColorType.Red, CM.ColorType.White } },
        { ID.slowOverload, new List<CM.ColorType> { CM.ColorType.Blue, CM.ColorType.Orange } },
        { ID.fireExplosion, new List<CM.ColorType> { CM.ColorType.Orange, CM.ColorType.Red } },
        { ID.entropyArtifact, new List<CM.ColorType> { CM.ColorType.Purple, CM.ColorType.Black } },
        { ID.loneWolfArtifact, new List<CM.ColorType> { CM.ColorType.White, CM.ColorType.Black, CM.ColorType.Gold } },
        { ID.exposeNearest, new List<CM.ColorType> { CM.GetExposeColor() } },
        { ID.placementIncrease, new List<CM.ColorType> { CM.ColorType.Green, CM.ColorType.Yellow } },
        { ID.InventoryBuff, new List<CM.ColorType> { CM.ColorType.Moss, CM.ColorType.Black } },
        { ID.lifeBoost, new List<CM.ColorType> { CM.ColorType.Green, CM.ColorType.White } },
        { ID.LaserBounce, new List<CM.ColorType> { CM.ColorType.Purple, CM.ColorType.Cyan } },
        { ID.ChargeOnCrit, new List<CM.ColorType> { CM.ColorType.Red, CM.ColorType.White, CM.ColorType.Cyan } },
        { ID.currencyDamage, new List<CM.ColorType> { CM.ColorType.Gold, CM.ColorType.Yellow } },
        { ID.inspirationRelic, new List<CM.ColorType> { CM.ColorType.Cyan, CM.ColorType.White, CM.ColorType.Orange } },
        { ID.agentMaxIncrease, new List<CM.ColorType> { CM.ColorType.White, CM.ColorType.Cyan } },
        { ID.redDamageBuff, new List<CM.ColorType> { CM.ColorType.Red, CM.ColorType.None, CM.ColorType.Red } },
        { ID.blueDamageBuff, new List<CM.ColorType> { CM.ColorType.Blue, CM.ColorType.None, CM.ColorType.Blue } },
        { ID.greenDamageBuff, new List<CM.ColorType> { CM.ColorType.Green, CM.ColorType.None, CM.ColorType.Green } },
        { ID.yellowDamageBuff, new List<CM.ColorType> { CM.ColorType.Yellow, CM.ColorType.None, CM.ColorType.Yellow } },
        { ID.purpleDamageBuff, new List<CM.ColorType> { CM.ColorType.Purple, CM.ColorType.None, CM.ColorType.Purple } },
        { ID.orangeDamageBuff, new List<CM.ColorType> { CM.ColorType.Orange, CM.ColorType.None, CM.ColorType.Orange } },
        { ID.whiteDamageBuff, new List<CM.ColorType> { CM.ColorType.White, CM.ColorType.None, CM.ColorType.White } },
        { ID.cyanDamageBuff, new List<CM.ColorType> { CM.ColorType.Cyan, CM.ColorType.None, CM.ColorType.Cyan } },
        { ID.blackDamageBuff, new List<CM.ColorType> { CM.ColorType.Black, CM.ColorType.None, CM.ColorType.Black } },
        { ID.goldDamageBuff, new List<CM.ColorType> { CM.ColorType.Gold, CM.ColorType.None, CM.ColorType.Gold } },
        { ID.mossDamageBuff, new List<CM.ColorType> { CM.ColorType.Moss, CM.ColorType.None, CM.ColorType.Moss } },
        { ID.redDamageMultiplierBuff, new List<CM.ColorType> { CM.ColorType.Red, CM.ColorType.None, CM.ColorType.Red } },
        { ID.blueDamageMultiplierBuff, new List<CM.ColorType> { CM.ColorType.Blue, CM.ColorType.None, CM.ColorType.Blue } },
        { ID.greenDamageMultiplierBuff, new List<CM.ColorType> { CM.ColorType.Green, CM.ColorType.None, CM.ColorType.Green } },
        { ID.yellowDamageMultiplierBuff, new List<CM.ColorType> { CM.ColorType.Yellow, CM.ColorType.None, CM.ColorType.Yellow } },
        { ID.purpleDamageMultiplierBuff, new List<CM.ColorType> { CM.ColorType.Purple, CM.ColorType.None, CM.ColorType.Purple } },
        { ID.orangeDamageMultiplierBuff, new List<CM.ColorType> { CM.ColorType.Orange, CM.ColorType.None, CM.ColorType.Orange } },
        { ID.whiteDamageMultiplierBuff, new List<CM.ColorType> { CM.ColorType.White, CM.ColorType.None, CM.ColorType.White } },
        { ID.cyanDamageMultiplierBuff, new List<CM.ColorType> { CM.ColorType.Cyan, CM.ColorType.None, CM.ColorType.Cyan } },
        { ID.blackDamageMultiplierBuff, new List<CM.ColorType> { CM.ColorType.Black, CM.ColorType.None, CM.ColorType.Black } },
        { ID.goldDamageMultiplierBuff, new List<CM.ColorType> { CM.ColorType.Gold, CM.ColorType.None, CM.ColorType.Gold } },
        { ID.mossDamageMultiplierBuff, new List<CM.ColorType> { CM.ColorType.Moss, CM.ColorType.None, CM.ColorType.Moss } },
        { ID.criticalPierce, new List<CM.ColorType> { CM.ColorType.White, CM.ColorType.Red } },
        { ID.vengeance, new List<CM.ColorType> { CM.ColorType.Black, CM.ColorType.Red } },
        { ID.overkill, new List<CM.ColorType> { CM.ColorType.Red, CM.ColorType.Orange } },
        { ID.laserStun, new List<CM.ColorType> { CM.ColorType.Cyan, CM.ColorType.Purple } },
        { ID.wolfPack, new List<CM.ColorType> { CM.ColorType.Yellow, CM.ColorType.Green } },
        { ID.explosionTradeoff, new List<CM.ColorType> { CM.ColorType.Red, CM.ColorType.Orange } },
        { ID.speedyAgent, new List<CM.ColorType> { CM.ColorType.Cyan, CM.ColorType.Green } },
        { ID.rerollDiscount, new List<CM.ColorType> { CM.ColorType.Gold, CM.ColorType.White } },
        { ID.lightningChain, new List<CM.ColorType> { CM.ColorType.Cyan, CM.ColorType.Purple } },
        { ID.zeal, new List<CM.ColorType> { CM.ColorType.Red, CM.ColorType.White } },
        { ID.alwaysCritOnFullyCharge, new List<CM.ColorType> { CM.ColorType.White, CM.ColorType.Red } },
        { ID.plague, new List<CM.ColorType> { CM.ColorType.Green, CM.ColorType.Black } },
        { ID.devilsPact, new List<CM.ColorType> { CM.ColorType.Red, CM.ColorType.Black } },
    };

    public Dictionary<ID, int> rarityDict = new Dictionary<ID, int>()
    {
        { ID.fireTickRate, 1 },
        { ID.slowDamageBuff, 1 },
        { ID.slowTickRate, 1 },
        { ID.criticalSlow, 2 },
        { ID.criticalCharge, 1 },
        { ID.criticalDamageBuff, 2 },
        { ID.slowOverload, 2 },
        { ID.fireExplosion, 2 },
        { ID.entropyArtifact, 3 },
        { ID.loneWolfArtifact, 3 },
        { ID.exposeNearest, 2 },
        { ID.placementIncrease, 1 },
        { ID.InventoryBuff, 2 },
        { ID.lifeBoost, 1 },
        { ID.LaserBounce, 2 },
        { ID.ChargeOnCrit, 3 },
        { ID.currencyDamage, 1 },
        { ID.inspirationRelic, 3 },
        { ID.agentMaxIncrease, 2 },
        { ID.redDamageBuff, 1 },
        { ID.blueDamageBuff, 1 },
        { ID.greenDamageBuff, 1 },
        { ID.yellowDamageBuff, 1 },
        { ID.purpleDamageBuff, 1 },
        { ID.orangeDamageBuff, 1 },
        { ID.whiteDamageBuff, 1 },
        { ID.cyanDamageBuff, 1 },
        { ID.blackDamageBuff, 1 },
        { ID.goldDamageBuff, 1 },
        { ID.mossDamageBuff, 1 },
        { ID.redDamageMultiplierBuff, 2 },
        { ID.blueDamageMultiplierBuff, 2 },
        { ID.greenDamageMultiplierBuff, 2 },
        { ID.yellowDamageMultiplierBuff, 2 },
        { ID.purpleDamageMultiplierBuff, 2 },
        { ID.orangeDamageMultiplierBuff, 2 },
        { ID.whiteDamageMultiplierBuff, 2 },
        { ID.cyanDamageMultiplierBuff, 2 },
        { ID.blackDamageMultiplierBuff, 2 },
        { ID.goldDamageMultiplierBuff, 2 },
        { ID.mossDamageMultiplierBuff, 2 },
        { ID.criticalPierce, 1 },
        { ID.vengeance, 2 },
        { ID.overkill, 2 },
        { ID.laserStun, 2 },
        { ID.wolfPack, 2 },
        { ID.explosionTradeoff, 2 },
        { ID.speedyAgent, 1 },
        { ID.rerollDiscount, 1 },
        { ID.lightningChain, 2 },
        { ID.zeal, 2 },
        { ID.alwaysCritOnFullyCharge, 2 },
        { ID.plague, 2 },
        { ID.devilsPact, 2 },
    };

    public Dictionary<ID, int> relicCosts = new Dictionary<ID, int>
    {
        { ID.fireTickRate, 2 },
        { ID.fireExplosion, 4 },
        { ID.slowDamageBuff, 3 },
        { ID.slowTickRate, 2 },
        { ID.slowOverload, 3 },
        { ID.criticalSlow, 4 },
        { ID.criticalCharge, 3 },
        { ID.criticalDamageBuff, 3 },
        { ID.entropyArtifact, 3 },
        { ID.loneWolfArtifact, 5 },
        { ID.exposeNearest, 2 },
        { ID.InventoryBuff, 3 },
        { ID.lifeBoost, 4 },
        { ID.LaserBounce, 4 },
        { ID.ChargeOnCrit, 5 },
        { ID.currencyDamage, 4 },
        { ID.agentMaxIncrease, 3 },
        { ID.redDamageBuff, 2 },
        { ID.blueDamageBuff, 2 },
        { ID.greenDamageBuff, 2 },
        { ID.yellowDamageBuff, 2 },
        { ID.purpleDamageBuff, 2 },
        { ID.orangeDamageBuff, 2 },
        { ID.whiteDamageBuff, 2 },
        { ID.cyanDamageBuff, 2 },
        { ID.blackDamageBuff, 2 },
        { ID.goldDamageBuff, 2 },
        { ID.mossDamageBuff, 2 },
        { ID.redDamageMultiplierBuff, 3 },
        { ID.blueDamageMultiplierBuff, 3 },
        { ID.greenDamageMultiplierBuff, 3 },
        { ID.yellowDamageMultiplierBuff, 3 },
        { ID.purpleDamageMultiplierBuff, 3 },
        { ID.orangeDamageMultiplierBuff, 3 },
        { ID.whiteDamageMultiplierBuff, 3 },
        { ID.cyanDamageMultiplierBuff, 3 },
        { ID.blackDamageMultiplierBuff, 3 },
        { ID.goldDamageMultiplierBuff, 3 },
        { ID.mossDamageMultiplierBuff, 3 },
        { ID.criticalPierce, 2 },
        { ID.vengeance, 3 },
        { ID.overkill, 3 },
        { ID.laserStun, 3 },
        { ID.wolfPack, 3 },
        { ID.explosionTradeoff, 3 },
        { ID.speedyAgent, 1 },
        { ID.rerollDiscount, 2 },
        { ID.lightningChain, 3 },
        { ID.zeal, 3 },
        { ID.alwaysCritOnFullyCharge, 4 },
        { ID.plague, 15 },
        { ID.devilsPact, 10 },
    };


    public List<Transform> relicDisplayPositions;
    public GameObject relicPrefab;

    private Dictionary<ID, Relic> activeVisualizers = new Dictionary<ID, Relic>();
    private Dictionary<Transform, Relic> slotToRelic = new Dictionary<Transform, Relic>();
    private readonly List<GameObject> spawnedGalleryRelics = new List<GameObject>();
    private readonly Dictionary<ID, Relic> galleryRelicsById = new Dictionary<ID, Relic>();
    public List<ID> debugList;
    private Dictionary<ID, bool> state = new Dictionary<ID, bool>();
    public Dictionary<ID, bool> locked = new Dictionary<ID, bool>();
    public static RM i;

    public GameObject relicGalleryPanelGameObject;
    public Transform relicGalleryPositionsParent;
    private List<Transform> relicGalleryDisplayPositions = new List<Transform>();
    private bool _isRelicGalleryVisible = true;

    private const float FallbackSpacing = 1f;
    private float _exposeNearestTimer;
    private const float ExposeNearestInterval = 0.25f;
    private const float ExposeNearestDuration = 0.4f;

    public void Awake()
    {
        i = this;
        activeVisualizers.Clear();
        slotToRelic.Clear();
        EnsureRelicDictionariesInitialized();
        _isRelicGalleryVisible = relicGalleryPanelGameObject == null || relicGalleryPanelGameObject.activeSelf;

        if (SaveDataManager.instance != null)
        {
            SaveDataManager.instance.LoadRelicLockStates(locked);
        }

        ApplyDefaultUnlockList(saveIfChanged: true);
        ApplyEditorDefaultRelicUnlocks();
    }

    private void OnDisable()
    {
        SetWolfPackHoverVisualizationVisible(false);
    }

    private void Start()
    {
        PopulateRelicGalleryDisplayPositions();
        PopulateRelicGallery();

        if (debugList == null) return;

        for (int i = 0; i < debugList.Count; i++)
        {
            Activate(debugList[i]);
        }
    }

    private void Update()
    {
        if (!_isRelicGalleryVisible) return;
        if (Input.GetMouseButtonDown(1))
        {
            HideRelicGallery();
        }
    }

    private void PopulateRelicGalleryDisplayPositions()
    {
        relicGalleryDisplayPositions.Clear();

        if (relicGalleryPositionsParent == null) return;

        for (int i = 0; i < relicGalleryPositionsParent.childCount; i++)
        {
            relicGalleryDisplayPositions.Add(relicGalleryPositionsParent.GetChild(i));
        }
    }

    private void PopulateRelicGallery()
    {
        ClearRelicGallery();

        if (relicPrefab == null) return;

        var relicIds = ((ID[])System.Enum.GetValues(typeof(ID)))
        .Where(IsRelicAvailable)
        .OrderBy(id => GetName(id))
        .ToArray();

        // Vector3 fallbackOrigin = relicGalleryPositionsParent != null ? relicGalleryPositionsParent.position : transform.position;
        // Quaternion fallbackRotation = relicGalleryPositionsParent != null ? relicGalleryPositionsParent.rotation : Quaternion.identity;

        for (int i = 0; i < relicIds.Length; i++)
        {
            ID id = relicIds[i];
            Transform slot = i < relicGalleryDisplayPositions.Count ? relicGalleryDisplayPositions[i] : null;

            Vector3 spawnPos = slot != null ? slot.position : transform.position + new Vector3((i % 10) * FallbackSpacing, -(i / 10) * FallbackSpacing, 0f);
            Quaternion spawnRot = slot != null ? slot.rotation : Quaternion.identity;

            GameObject instance = Instantiate(relicPrefab, spawnPos, spawnRot);
            // if (slot != null)
            // {
            // instance.transform.SetParent(slot, false);
            // instance.transform.localRotation = Quaternion.identity;
            // }
            // else if (relicGalleryPositionsParent != null)
            // {
            // instance.transform.SetParent(relicGalleryPositionsParent, true);
            // }
            // instance.transform.position = slot.transform.position;
            if (slot != null)
            {
                instance.transform.SetParent(slot.transform, true);
                instance.transform.localPosition = Vector2.zero;
            }
            // instance.transform.localScale = Vector3.one
            // instance.transform.position = slot.transform.position

            // instance.transform.localPosition = Vector3.zero;
            // instance.transform.localScale = Vector3.one * 8f;

            Relic relic = instance.GetComponent<Relic>();
            if (relic == null)
            {
                relic = instance.AddComponent<Relic>();
            }

            relic.id = id;
            ConfigureRelicInstance(relic, id);
            //relic.GetComponent<SRC>().ChangeSortingOrder(6);
            spawnedGalleryRelics.Add(instance);
            galleryRelicsById[id] = relic;
            instance.SetActive(_isRelicGalleryVisible);
        }
    }

    public void ShowRelicGallery()
    {
        SetRelicGalleryVisible(true);
    }

    public void HideRelicGallery()
    {
        SetRelicGalleryVisible(false);
    }

    public string GetRelicUnlockDescription(ID id)
    {
        if (defaultUnlockedRelics.Contains(id) || !HasExplicitUnlockCondition(id))
        {
            return "Unlocked by default";
        }
        switch (id)
        {
            case ID.fireTickRate:
            {
                float current = SaveDataManager.instance != null ? SaveDataManager.instance.GetFireDamageDealt() : 0f;
                float target = Mathf.Max(0f, fireTickRateUnlock);
                float shown = Mathf.Clamp(current, 0f, target);

                return "Deal " + CM.i.RTC(CM.ColorType.Orange, target.ToString("0")) +
                " fire damage to unlock (" + shown.ToString("0") + "/" + target.ToString("0") + ").";
            }
            case ID.overkill:
            {
                float current = SaveDataManager.instance != null ? SaveDataManager.instance.GetMaxSingleHitDamage() : 0f;
                float target = RM.overkillUnlockSingleHitDamage;
                float shown = Mathf.Clamp(current, 0f, target);
                return "Deal " + CM.i.RTC(CM.ColorType.Red, target.ToString("0")) + " damage in a single hit to unlock (" + shown.ToString("0") + "/" + target.ToString("0") + ").";
            }
            case ID.currencyDamage:
            {
                int current = SaveDataManager.instance != null ? SaveDataManager.instance.GetMaxCurrencyCollected() : 0;
                int target = RM.currencyDamageUnlockCurrency;
                int shown = Mathf.Clamp(current, 0, target);
                return "Have more than " + CM.i.RTC(CM.ColorType.Gold, target.ToString()) + " currency at once to unlock (" + shown.ToString() + "/" + target.ToString() + ").";
            }
            case ID.exposeNearest:
            {
                float current = SaveDataManager.instance != null ? SaveDataManager.instance.GetExposedDamageDealt() : 0f;
                float target = RM.exposeNearestUnlockExposedDamage;
                float shown = Mathf.Clamp(current, 0f, target);
                return "Deal " + CM.i.RTC(CM.GetExposeColor(), target.ToString("0")) + " damage to " + CM.i.RTC(CM.GetExposeColor(), "Exposed") + " enemies to unlock (" + shown.ToString("0") + "/" + target.ToString("0") + ").";
            }
            case ID.laserStun:
            {
                int current = SaveDataManager.instance != null ? SaveDataManager.instance.GetMaxEnemiesHitByLaser() : 0;
                int target = RM.laserStunUnlockEnemiesHit;
                int shown = Mathf.Clamp(current, 0, target);
                return "Hit " + CM.i.RTC(CM.ColorType.Cyan, target.ToString()) + " enemies with a single " + CM.i.RTC(CM.ColorType.Cyan, "laser") + " to unlock (" + shown.ToString() + "/" + target.ToString() + ").";
            }
            case ID.LaserBounce:
            {
                int current = SaveDataManager.instance != null ? SaveDataManager.instance.GetMaxLaserBounces() : 0;
                int target = RM.laserBounceUnlockBounces;
                int shown = Mathf.Clamp(current, 0, target);
                return "Bounce a laser more than " + CM.i.RTC(CM.ColorType.Purple, target.ToString()) + " times to unlock (" + shown.ToString() + "/" + target.ToString() + ").";
            }
            case ID.wolfPack:
            {
                int current = SaveDataManager.instance != null ? SaveDataManager.instance.GetMaxTowersPlacedAtOnce() : 0;
                int target = RM.wolfPackUnlockTowersPlaced;
                int shown = Mathf.Clamp(current, 0, target);
                return "Have " + CM.i.RTC(CM.ColorType.Yellow, target.ToString()) + " towers placed at the same time to unlock (" + shown.ToString() + "/" + target.ToString() + ").";
            }
            case ID.explosionTradeoff:
            {
                int current = SaveDataManager.instance != null ? SaveDataManager.instance.GetMaxRedTagTowersPlaced() : 0;
                int target = RM.explosionTradeoffUnlockRedTagTowers;
                int shown = Mathf.Clamp(current, 0, target);
                return "Have " + CM.i.RTC(CM.ColorType.Red, target.ToString()) + " " + CM.i.RTC(CM.ColorType.Red, "Red") + " tag towers placed at once to unlock (" + shown.ToString() + "/" + target.ToString() + ").";
            }
            case ID.speedyAgent:
            {
                int current = SaveDataManager.instance != null ? SaveDataManager.instance.GetMaxAgentsOnField() : 0;
                int target = RM.speedyAgentUnlockAgentsOnField;
                int shown = Mathf.Clamp(current, 0, target);
                return "Have " + CM.i.RTC(CM.ColorType.Cyan, target.ToString()) + " agents on the field at once to unlock (" + shown.ToString() + "/" + target.ToString() + ").";
            }
            case ID.rerollDiscount:
            {
                int current = SaveDataManager.instance != null ? SaveDataManager.instance.GetTotalRerollCount() : 0;
                int target = RM.rerollDiscountUnlockRerolls;
                int shown = Mathf.Clamp(current, 0, target);
                return "Reroll either shop " + CM.i.RTC(CM.ColorType.Gold, target.ToString()) + " times to unlock (" + shown.ToString() + "/" + target.ToString() + ").";
            }
            case ID.lightningChain:
            {
                float current = SaveDataManager.instance != null ? SaveDataManager.instance.GetLightningDamageDealt() : 0f;
                float target = RM.lightningChainUnlockDamage;
                float shown = Mathf.Clamp(current, 0f, target);
                return "Deal " + CM.i.RTC(CM.ColorType.Cyan, target.ToString("0")) + " " + CM.i.RTC(CM.ColorType.Cyan, "lightning") + " damage to unlock (" + shown.ToString("0") + "/" + target.ToString("0") + ").";
            }
            default:
            return "Unknown";
        }
    }

    public void SetRelicGalleryVisible(bool isVisible)
    {
        _isRelicGalleryVisible = isVisible;

        if (isVisible && TowerDamageGraph.instance != null)
        {
            TowerDamageGraph.instance.HideGraph();
        }

        if (relicGalleryPanelGameObject != null)
        {
            relicGalleryPanelGameObject.SetActive(isVisible);
        }

        SetSpawnedGalleryRelicsActive(isVisible);
    }

    private void SetSpawnedGalleryRelicsActive(bool isActive)
    {
        for (int i = 0; i < spawnedGalleryRelics.Count; i++)
        {
            if (spawnedGalleryRelics[i] == null) continue;
            spawnedGalleryRelics[i].SetActive(isActive);
        }
    }

    private void ClearRelicGallery()
    {
        for (int i = 0; i < spawnedGalleryRelics.Count; i++)
        {
            if (spawnedGalleryRelics[i] != null)
            {
                Destroy(spawnedGalleryRelics[i]);
            }
        }

        spawnedGalleryRelics.Clear();
        galleryRelicsById.Clear();
    }

    public void RefreshRelicGalleryVisuals()
    {
        foreach (var kvp in galleryRelicsById)
        {
            if (kvp.Value == null) continue;
            ConfigureRelicInstance(kvp.Value, kvp.Key);
        }
    }

    private void FixedUpdate()
    {
        if (Active(ID.exposeNearest))
        {
            _exposeNearestTimer -= Time.fixedDeltaTime;
            if (_exposeNearestTimer <= 0f)
            {
                _exposeNearestTimer = ExposeNearestInterval;
                ExposeNearestToGoal();
            }
        }
    }

    public enum ID
    {
        fireTickRate,
        slowDamageBuff,
        slowTickRate,
        criticalSlow,
        criticalCharge,
        criticalDamageBuff,
        slowOverload,
        fireExplosion,
        entropyArtifact,
        loneWolfArtifact,
        exposeNearest,
        placementIncrease,
        InventoryBuff,
        lifeBoost,
        LaserBounce,
        ChargeOnCrit,
        currencyDamage,
        inspirationRelic,
        agentMaxIncrease,
        redDamageBuff,
        blueDamageBuff,
        greenDamageBuff,
        criticalPierce,
        vengeance,
        overkill,
        laserStun,
        wolfPack,
        explosionTradeoff,
        speedyAgent,
        rerollDiscount,
        lightningChain,
        zeal,
        alwaysCritOnFullyCharge,
        plague,
        devilsPact,
        yellowDamageBuff,
        purpleDamageBuff,
        orangeDamageBuff,
        whiteDamageBuff,
        cyanDamageBuff,
        blackDamageBuff,
        goldDamageBuff,
        mossDamageBuff,
        redDamageMultiplierBuff,
        blueDamageMultiplierBuff,
        greenDamageMultiplierBuff,
        yellowDamageMultiplierBuff,
        purpleDamageMultiplierBuff,
        orangeDamageMultiplierBuff,
        whiteDamageMultiplierBuff,
        cyanDamageMultiplierBuff,
        blackDamageMultiplierBuff,
        goldDamageMultiplierBuff,
        mossDamageMultiplierBuff,
    }

    public bool Active(ID id)
    {
        EnsureRelicDictionariesInitialized();
        if (!state.ContainsKey(id))
        {
            state[id] = false;
        }
        return state[id];
    }

    public void Activate(ID id)
    {
        if (Active(id)) return;

        if (id == ID.fireTickRate)
        {
            Health.fireTickCooldownGlobal /= 2;
        }
        if (id == ID.slowTickRate)
        {
            Movement.slowDecayRateGlobalMultiplier /= 2;
        }
        if (id == ID.placementIncrease && TowerManager.instance != null)
        {
            TowerManager.instance.SetMaximumPlacedTowers(TowerManager.instance.GetMaximumPlacedTowers() + 1);
        }
        if (id == ID.lifeBoost && GameController.instance != null)
        {
            GameController.instance.AddLifes(10);
        }

        SetState(id, true);

        if (id == ID.wolfPack && TowerManager.instance != null)
        {
            TowerManager.instance.VisualizeWolfPackForAllPlacedTowersOnRelicPurchase();
        }
    }

    public void Deactivate(ID id)
    {
        if (!Active(id)) return;

        if (id == ID.placementIncrease && TowerManager.instance != null)
        {
            TowerManager.instance.SetMaximumPlacedTowers(TowerManager.instance.GetMaximumPlacedTowers() - 1);
        }

        if (id == ID.wolfPack)
        {
            SetWolfPackHoverVisualizationVisible(false);
        }

        SetState(id, false);
    }

    public bool IsUnlocked(ID id)
    {
        EnsureRelicDictionariesInitialized();
        if (!IsRelicAvailable(id)) return false;
        return locked.TryGetValue(id, out var unlocked) && unlocked;
    }

    public bool IsRelicAvailable(ID id)
    {
        if (!TryGetDamageTypeRelicColor(id, out var colorType))
        {
            return true;
        }

        return IsRandomizableDamageType(colorType);
    }

    public void UnlockRelic(ID id)
    {
        EnsureRelicDictionariesInitialized();
        if (locked.TryGetValue(id, out var unlocked) && unlocked) return;

        locked[id] = true;
        RefreshRelicGalleryVisuals();
        ShowRelicUnlockNotification(id);
        SaveLockedToSaveData();
    }

    public void UnlockRelic(ID id, bool displayNotification = true)
    {
        EnsureRelicDictionariesInitialized();
        if (locked.TryGetValue(id, out var unlocked) && unlocked) return;

        locked[id] = true;
        RefreshRelicGalleryVisuals();

        if (displayNotification)
        {
            ShowRelicUnlockNotification(id);
        }

        SaveLockedToSaveData();
    }

    public void UnlockAllRelics(bool displayNotification = false)
    {
        EnsureRelicDictionariesInitialized();

        var ids = (ID[])System.Enum.GetValues(typeof(ID));
        for (int i = 0; i < ids.Length; i++)
        {
            UnlockRelic(ids[i], displayNotification);
        }
    }

    public void ShowRelicUnlockNotification(ID id)
    {
        if (RelicUnlockNotificationText == null) return;

        if (_relicUnlockNotificationRoutine != null)
        {
            StopCoroutine(_relicUnlockNotificationRoutine);
            _relicUnlockNotificationRoutine = null;
        }

        RelicUnlockNotificationText.text = "Unlocked " + GetName(id) + "\n" + GetRelicUnlockDescription(id);

        Color c = RelicUnlockNotificationText.color;
        c.a = 1f;
        RelicUnlockNotificationText.color = c;
        RelicUnlockNotificationText.gameObject.SetActive(true);

        _relicUnlockNotificationRoutine = StartCoroutine(FadeRelicUnlockNotificationRoutine());
    }

    private System.Collections.IEnumerator FadeRelicUnlockNotificationRoutine()
    {
        if (RelicUnlockNotificationText == null) yield break;

        float duration = Mathf.Max(0f, relicUnlockNotificationFadeSeconds);
        if (duration <= 0f)
        {
            var immediate = RelicUnlockNotificationText.color;
            immediate.a = 0f;
            RelicUnlockNotificationText.color = immediate;
            RelicUnlockNotificationText.gameObject.SetActive(false);
            _relicUnlockNotificationRoutine = null;
            yield break;
        }

        float t = 0f;
        Color baseColor = RelicUnlockNotificationText.color;
        while (t < duration)
        {
            t += Time.deltaTime;
            float a = 1f - Mathf.Clamp01(t / duration);
            baseColor.a = a;
            RelicUnlockNotificationText.color = baseColor;
            yield return null;
        }

        baseColor.a = 0f;
        RelicUnlockNotificationText.color = baseColor;
        RelicUnlockNotificationText.gameObject.SetActive(false);
        _relicUnlockNotificationRoutine = null;
    }

    private void EnsureRelicDictionariesInitialized()
    {
        var ids = (ID[])System.Enum.GetValues(typeof(ID));
        for (int i = 0; i < ids.Length; i++)
        {
            if (!state.ContainsKey(ids[i]))
            {
                state[ids[i]] = false;
            }

            if (!locked.ContainsKey(ids[i]))
            {
                locked[ids[i]] = false;
            }
        }
    }

    private void SetState(ID id, bool isActive)
    {
        bool previous = state.ContainsKey(id) && state[id];
        if (previous == isActive) return;

        state[id] = isActive;
        OnStateChanged(id, isActive);
    }

    public void IndicateRelic(ID id)
    {
        if (!Active(id) || !activeVisualizers.ContainsKey(id))
        {
            Debug.LogError("Indicating inactive relice");
            return;
        }
        activeVisualizers[id].Indicate();
    }

    public Relic GetActiveVisualizer(ID id)
    {
        if (!activeVisualizers.TryGetValue(id, out var relic)) return null;
        return relic;
    }

    private void OnStateChanged(ID id, bool isActive)
    {
        UpdateVisualizerForId(id, isActive);
    }

    public void SetWolfPackHoverVisualizationVisible(bool visible)
    {
        _showWolfPackHoverVisualization = visible;

        if (!visible)
        {
            if (_wolfPackHoverVisualizationRoutine != null)
            {
                StopCoroutine(_wolfPackHoverVisualizationRoutine);
                _wolfPackHoverVisualizationRoutine = null;
            }

            ClearWolfPackHoverTextInstances();

            return;
        }

        if (_wolfPackHoverVisualizationRoutine == null)
        {
            _wolfPackHoverVisualizationRoutine = StartCoroutine(WolfPackHoverVisualizationRoutine());
        }
    }

    private System.Collections.IEnumerator WolfPackHoverVisualizationRoutine()
    {
        while (_showWolfPackHoverVisualization)
        {
            if (!Active(ID.wolfPack))
            {
                break;
            }

            SyncWolfPackHoverTextInstances();

            yield return new WaitForSeconds(Mathf.Max(0.05f, wolfPackHoverFloatingTextInterval));
        }

        ClearWolfPackHoverTextInstances();
        _wolfPackHoverVisualizationRoutine = null;
        _showWolfPackHoverVisualization = false;
    }

    private void SyncWolfPackHoverTextInstances()
    {
        if (TowerManager.instance == null)
        {
            ClearWolfPackHoverTextInstances();
            return;
        }

        if (TextObjectPool.instance == null)
        {
            ClearWolfPackHoverTextInstances();
            return;
        }

        Dictionary<Tower, int> bonusStacksByTower = TowerManager.instance.BuildWolfPackBonusStacksForPlacedTowers();
        HashSet<Tower> activeTowers = new HashSet<Tower>();

        foreach (var kvp in bonusStacksByTower)
        {
            Tower tower = kvp.Key;
            int stacks = kvp.Value;
            if (tower == null || stacks <= 0) continue;

            float percent = Mathf.Max(0f, stacks * wolfPackDamagePerTower * 100f);
            string text = "+" + percent.ToString("0.#") + "%";
            Vector3 textPosition = tower.transform.position + wolfPackHoverFloatingTextOffset;

            if (_wolfPackHoverTextInstances.TryGetValue(tower, out var existing) && existing != null)
            {
                TextObjectPool.instance.UpdatePersistentText(existing, textPosition, text, wolfPackHoverFloatingTextColor, wolfPackHoverFloatingTextSize);
            }
            else
            {
                GameObject instance = TextObjectPool.instance.SpawnPersistentText(textPosition, text, wolfPackHoverFloatingTextColor, wolfPackHoverFloatingTextSize);
                if (instance != null)
                {
                    _wolfPackHoverTextInstances[tower] = instance;
                }
            }

            activeTowers.Add(tower);
        }

        if (_wolfPackHoverTextInstances.Count == 0) return;

        List<Tower> remove = new List<Tower>();
        foreach (var kvp in _wolfPackHoverTextInstances)
        {
            if (!activeTowers.Contains(kvp.Key))
            {
                if (kvp.Value != null)
                {
                    TextObjectPool.instance.ReleasePersistentText(kvp.Value);
                }

                remove.Add(kvp.Key);
            }
        }

        for (int i = 0; i < remove.Count; i++)
        {
            _wolfPackHoverTextInstances.Remove(remove[i]);
        }
    }

    private void ClearWolfPackHoverTextInstances()
    {
        if (_wolfPackHoverTextInstances.Count == 0) return;

        foreach (var kvp in _wolfPackHoverTextInstances)
        {
            if (kvp.Value == null) continue;
            if (TextObjectPool.instance != null)
            {
                TextObjectPool.instance.ReleasePersistentText(kvp.Value);
            }
            else
            {
                Destroy(kvp.Value);
            }
        }

        _wolfPackHoverTextInstances.Clear();
    }

    private void UpdateVisualizerForId(ID id, bool isActive)
    {
        if (isActive)
        {
            if (activeVisualizers.ContainsKey(id) && activeVisualizers[id] != null) return;

            if (relicPrefab == null) return;

            Transform spawnSlot = GetFirstValidSlot();
            Vector3 spawnPos = spawnSlot != null ? spawnSlot.position : transform.position;
            Quaternion spawnRot = spawnSlot != null ? spawnSlot.rotation : Quaternion.identity;

            GameObject visualizer = Instantiate(relicPrefab, spawnPos, spawnRot);
            var relicInteractable = visualizer.GetComponent<Relic>();
            if (relicInteractable == null)
            {
                relicInteractable = visualizer.AddComponent<Relic>();
            }

            ConfigureRelicInstance(relicInteractable, id);

            if (spawnSlot != null)
            {
                slotToRelic[spawnSlot] = relicInteractable;
            }

            relicInteractable.id = id;
            activeVisualizers[id] = relicInteractable;
            return;
        }

        if (!activeVisualizers.ContainsKey(id)) return;
        if (activeVisualizers[id] != null)
        {
            Destroy(activeVisualizers[id].gameObject);
        }
        activeVisualizers.Remove(id);
    }

    public void ConfigureRelicInstance(Relic relic, ID id)
    {
        if (relic == null) return;

        relic.rarity = GetRelicRarity(id);

        SRC src = relic.GetComponent<SRC>();
        if (src == null || src.srColorInfos == null || src.srColorInfos.Count == 0) return;

        relicColorMapping.TryGetValue(id, out var mappedColors);
        int colorCount = mappedColors != null ? mappedColors.Count : 0;

        for (int i = 0; i < src.srColorInfos.Count; i++)
        {
            SRC.SrColorInfo info = src.srColorInfos[i];
            info.colorType = i < colorCount ? mappedColors[i] : CM.ColorType.None;
            src.srColorInfos[i] = info;
        }

        if (!IsUnlocked(id))
        {
            for (int i = 0; i < src.srColorInfos.Count; i++)
            {
                SRC.SrColorInfo info = src.srColorInfos[i];
                info.colorType = CM.ColorType.Black;
                src.srColorInfos[i] = info;
            }
        }

        src.ApplySpriteRendererColors();
    }

    private Transform GetFirstValidSlot()
    {
        if (relicDisplayPositions == null) return null;
        for (int i = 0; i < relicDisplayPositions.Count; i++)
        {
            if (relicDisplayPositions[i] != null && !slotToRelic.ContainsKey(relicDisplayPositions[i]))
            {
                return relicDisplayPositions[i];
            }
        }
        return null;
    }
    private void ExposeNearestToGoal()
    {
        var enemies = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
        Enemy nearest = null;
        float minDistSqr = float.MaxValue;
        foreach (var enemy in enemies)
        {
            if (enemy == null) continue;
            Movement movement = enemy.GetMovement();
            if (movement == null || movement.Goal == null) continue;
            Vector2 pos = enemy.rb != null ? enemy.rb.position : (Vector2)enemy.transform.position;
            float distSqr = ((Vector2)movement.Goal.position - pos).sqrMagnitude;
            if (distSqr < minDistSqr)
            {
                minDistSqr = distSqr;
                nearest = enemy;
            }
        }
        nearest?.Expose(ExposeNearestDuration);
    }

    private string BaseDamageText(string text)
    {
        return CM.i != null ? CM.i.RTC(CM.ColorType.Blue, text) : text;
    }

    private string DamageMultiplierText(float multiplier)
    {
        string text = "+" + multiplier.ToString("0.##") + "x";
        return CM.i != null ? CM.i.RTC(CM.ColorType.Red, text) : text;
    }

    private static string ColorName(CM.ColorType colorType)
    {
        string raw = colorType.ToString();
        return string.IsNullOrEmpty(raw) ? "Unknown" : char.ToUpper(raw[0]) + raw.Substring(1);
    }

    public bool TryGetBaseDamageBuffColor(ID id, out CM.ColorType colorType)
    {
        switch (id)
        {
            case ID.redDamageBuff: colorType = CM.ColorType.Red; return true;
            case ID.blueDamageBuff: colorType = CM.ColorType.Blue; return true;
            case ID.greenDamageBuff: colorType = CM.ColorType.Green; return true;
            case ID.yellowDamageBuff: colorType = CM.ColorType.Yellow; return true;
            case ID.purpleDamageBuff: colorType = CM.ColorType.Purple; return true;
            case ID.orangeDamageBuff: colorType = CM.ColorType.Orange; return true;
            case ID.whiteDamageBuff: colorType = CM.ColorType.White; return true;
            case ID.cyanDamageBuff: colorType = CM.ColorType.Cyan; return true;
            case ID.blackDamageBuff: colorType = CM.ColorType.Black; return true;
            case ID.goldDamageBuff: colorType = CM.ColorType.Gold; return true;
            case ID.mossDamageBuff: colorType = CM.ColorType.Moss; return true;
            default: colorType = CM.ColorType.None; return false;
        }
    }

    public bool TryGetDamageMultiplierBuffColor(ID id, out CM.ColorType colorType)
    {
        switch (id)
        {
            case ID.redDamageMultiplierBuff: colorType = CM.ColorType.Red; return true;
            case ID.blueDamageMultiplierBuff: colorType = CM.ColorType.Blue; return true;
            case ID.greenDamageMultiplierBuff: colorType = CM.ColorType.Green; return true;
            case ID.yellowDamageMultiplierBuff: colorType = CM.ColorType.Yellow; return true;
            case ID.purpleDamageMultiplierBuff: colorType = CM.ColorType.Purple; return true;
            case ID.orangeDamageMultiplierBuff: colorType = CM.ColorType.Orange; return true;
            case ID.whiteDamageMultiplierBuff: colorType = CM.ColorType.White; return true;
            case ID.cyanDamageMultiplierBuff: colorType = CM.ColorType.Cyan; return true;
            case ID.blackDamageMultiplierBuff: colorType = CM.ColorType.Black; return true;
            case ID.goldDamageMultiplierBuff: colorType = CM.ColorType.Gold; return true;
            case ID.mossDamageMultiplierBuff: colorType = CM.ColorType.Moss; return true;
            default: colorType = CM.ColorType.None; return false;
        }
    }

    public bool TryGetDamageTypeRelicColor(ID id, out CM.ColorType colorType)
    {
        return TryGetBaseDamageBuffColor(id, out colorType)
        || TryGetDamageMultiplierBuffColor(id, out colorType);
    }

    public float GetDamageTypeBaseDamageBonus(CM.ColorType damageType)
    {
        if (TryGetBaseDamageBuffRelicId(damageType, out var relicId) && Active(relicId))
        {
            return genericDamageTypeBaseDamageBonus;
        }

        return 0f;
    }

    public float GetDamageTypeDamageMultiplierBonus(CM.ColorType damageType)
    {
        if (TryGetDamageMultiplierBuffRelicId(damageType, out var relicId) && Active(relicId))
        {
            return genericDamageTypeDamageMultiplierBonus;
        }

        return 0f;
    }

    public bool TryGetBaseDamageBuffRelicId(CM.ColorType damageType, out ID relicId)
    {
        if (!IsRandomizableDamageType(damageType))
        {
            relicId = ID.fireTickRate;
            return false;
        }

        switch (damageType)
        {
            case CM.ColorType.Red: relicId = ID.redDamageBuff; return true;
            case CM.ColorType.Blue: relicId = ID.blueDamageBuff; return true;
            case CM.ColorType.Green: relicId = ID.greenDamageBuff; return true;
            case CM.ColorType.Yellow: relicId = ID.yellowDamageBuff; return true;
            case CM.ColorType.Purple: relicId = ID.purpleDamageBuff; return true;
            case CM.ColorType.Orange: relicId = ID.orangeDamageBuff; return true;
            case CM.ColorType.White: relicId = ID.whiteDamageBuff; return true;
            case CM.ColorType.Cyan: relicId = ID.cyanDamageBuff; return true;
            case CM.ColorType.Black: relicId = ID.blackDamageBuff; return true;
            case CM.ColorType.Gold: relicId = ID.goldDamageBuff; return true;
            case CM.ColorType.Moss: relicId = ID.mossDamageBuff; return true;
            default: relicId = ID.fireTickRate; return false;
        }
    }

    public bool TryGetDamageMultiplierBuffRelicId(CM.ColorType damageType, out ID relicId)
    {
        if (!IsRandomizableDamageType(damageType))
        {
            relicId = ID.fireTickRate;
            return false;
        }

        switch (damageType)
        {
            case CM.ColorType.Red: relicId = ID.redDamageMultiplierBuff; return true;
            case CM.ColorType.Blue: relicId = ID.blueDamageMultiplierBuff; return true;
            case CM.ColorType.Green: relicId = ID.greenDamageMultiplierBuff; return true;
            case CM.ColorType.Yellow: relicId = ID.yellowDamageMultiplierBuff; return true;
            case CM.ColorType.Purple: relicId = ID.purpleDamageMultiplierBuff; return true;
            case CM.ColorType.Orange: relicId = ID.orangeDamageMultiplierBuff; return true;
            case CM.ColorType.White: relicId = ID.whiteDamageMultiplierBuff; return true;
            case CM.ColorType.Cyan: relicId = ID.cyanDamageMultiplierBuff; return true;
            case CM.ColorType.Black: relicId = ID.blackDamageMultiplierBuff; return true;
            case CM.ColorType.Gold: relicId = ID.goldDamageMultiplierBuff; return true;
            case CM.ColorType.Moss: relicId = ID.mossDamageMultiplierBuff; return true;
            default: relicId = ID.fireTickRate; return false;
        }
    }

    public string GetDescription(ID id)
    {
        if (TryGetBaseDamageBuffColor(id, out CM.ColorType baseBuffColor))
        {
            string colorName = CM.i != null ? CM.i.RTC(baseBuffColor, ColorName(baseBuffColor)) : ColorName(baseBuffColor);
            return colorName + " damage gains " + BaseDamageText("+" + genericDamageTypeBaseDamageBonus.ToString("0.##") + " base damage");
        }

        if (TryGetDamageMultiplierBuffColor(id, out CM.ColorType multiplierBuffColor))
        {
            string colorName = CM.i != null ? CM.i.RTC(multiplierBuffColor, ColorName(multiplierBuffColor)) : ColorName(multiplierBuffColor);
            return colorName + " damage gains " + DamageMultiplierText(1f + genericDamageTypeDamageMultiplierBonus) + " damage multiplier";
        }

        switch (id)
        {
            case ID.fireTickRate:
            return "Double " + CM.i.RTC(CM.ColorType.Orange, "Fire tick rate");
            case ID.slowDamageBuff:
            return "Damage dealt to enemies is multiplied by half their " + CM.i.RTC(CM.ColorType.Blue, " Slow") + " percentage";
            case ID.slowTickRate:
            return CM.i.RTC(CM.ColorType.Blue, "Slows") + " decay half as fast";
            case ID.criticalSlow:
            return "Critical hits on enemies at max " + CM.i.RTC(CM.ColorType.Blue, "Slow") + " trigger an " + CM.i.RTC(CM.ColorType.Red, "Explosion") + " and clears the " + CM.i.RTC(CM.ColorType.Blue, "Slow");
            case ID.criticalCharge:
            return "" + CM.i.RTC(CM.ColorType.White, "Charged") + " towers gain " + CM.i.RTC(CM.ColorType.Green, "+25%") + " critical chance";
            case ID.criticalDamageBuff:
            return "Critical hits deal " + DamageMultiplierText(1.5f) + " critical multiplier";
            case ID.slowOverload:
            return "Applying " + CM.i.RTC(CM.ColorType.Blue, "Slow") + " beyond max deals damage equal to overflow slow x10";
            case ID.fireExplosion:
            return CM.i.RTC(CM.ColorType.Orange, "Burning") + " enemies " + CM.i.RTC(CM.ColorType.Red, "Explode") + " on death, dealing their remaining burn damage to nearby enemies";
            case ID.entropyArtifact:
            return "Enemies take " + DamageMultiplierText(1.1f) + " damage per active " + CM.i.RTC(CM.ColorType.Purple, "status effect");
            case ID.loneWolfArtifact:
            return "If only one tower is placed, it gains " + DamageMultiplierText(2f) + " damage and " + CM.i.RTC(CM.ColorType.Green, "-50%") + " " + CM.i.RTC(CM.ColorType.White, "cooldown");
            case ID.exposeNearest:
            return "Always " + CM.i.RTC(CM.GetExposeColor(), "Expose") + " the enemy closest to the goal";
            case ID.placementIncrease:
            return "Increase max placed towers by " + CM.i.RTC(CM.ColorType.Green, "+1");
            case ID.InventoryBuff:
            return "Increase all damage by " + DamageMultiplierText(1.1f) + " while your inventory has no towers";
            case ID.lifeBoost:
            return "Gain " + CM.i.RTC(CM.ColorType.Green, "+10") + " lives";
            case ID.LaserBounce:
            return "All " + CM.i.RTC(CM.ColorType.Purple, "lasers") + " gain " + CM.i.RTC(CM.ColorType.Green, "+2") + " bounces";
            case ID.ChargeOnCrit:
            return "When this tower kills an enemy, gain " + CM.i.RTC(CM.ColorType.Green, "+" + chargeOnCritKillChargeAmount.ToString("0.##")) + " " + CM.i.RTC(CM.ColorType.White, "Charge");
            case ID.currencyDamage:
            return "If you have more than 100 currency, towers deal " + DamageMultiplierText(1.2f) + " damage";
            case ID.inspirationRelic:
            return "When an enemy dies, grant " + CM.i.RTC(CM.ColorType.Green, "+" + inspirationRelicChargeAmount.ToString("0.##")) + " charge to all towers within " + CM.i.RTC(CM.ColorType.White, inspirationRelicRadius.ToString("0.#")) + " range";
            case ID.agentMaxIncrease:
            return "Agent towers can spawn " + CM.i.RTC(CM.ColorType.Green, "+1") + " max agent";
            case ID.criticalPierce:
            return "Projectiles gain " + CM.i.RTC(CM.ColorType.Green, "+1") + " pierce when they critically hit";
            case ID.vengeance:
            return "When an enemy reaches the goal, all living enemies take 20% of their maximum health as damage";
            case ID.overkill:
            return "Excess damage when killing an enemy is dealt to the nearest enemy within 3 range";
            case ID.laserStun:
            return "All " + CM.i.RTC(CM.ColorType.Purple, "lasers") + " have a 1% chance to " + CM.i.RTC(CM.ColorType.Purple, "Stun") + " an enemy for 2 seconds";
            case ID.wolfPack:
            return "Each tower within range increases this tower's damage by " + DamageMultiplierText(1f + wolfPackDamagePerTower);
            case ID.explosionTradeoff:
            return "All " + CM.i.RTC(CM.ColorType.Orange, "AOEs") + " are halved in size but deal " + DamageMultiplierText(2f) + " damage";
            case ID.speedyAgent:
            return CM.i.RTC(CM.ColorType.Yellow, "Agents") + " move at " + CM.i.RTC(CM.ColorType.Green, "double speed");
            case ID.rerollDiscount:
            return "Rerolling the relic shop and tower shop costs " + CM.i.RTC(CM.ColorType.Gold, "10% less");
            case ID.lightningChain:
            return CM.i.RTC(CM.ColorType.Cyan, "Lightning") + " hits " + CM.i.RTC(CM.ColorType.Green, "+1") + " more enemy";
            case ID.zeal:
            return "When an enemy dies within " + CM.i.RTC(CM.ColorType.White, zealGoalDistanceThreshold.ToString("0.#")) + " units of the goal, the killing tower gains " + DamageMultiplierText(1.1f) + " damage";
            case ID.alwaysCritOnFullyCharge:
            return "Towers at full " + CM.i.RTC(CM.ColorType.White, "Charge") + " gain " + CM.i.RTC(CM.ColorType.Green, "+100%") + " critical hit chance";
            case ID.plague:
            return "When an enemy takes " + CM.i.RTC(CM.ColorType.Green, "Poison") + " damage, it has a " + CM.i.RTC(CM.ColorType.Green, (plagueSpreadChance * 100f).ToString("0.#") + "%") + " chance to spread " + CM.i.RTC(CM.ColorType.Green, "Poison") + " to a nearby enemy within " + CM.i.RTC(CM.ColorType.Green, plagueSpreadRange.ToString("0.#")) + " range";
            case ID.devilsPact:
            return "At the start of each round, lose 1 life and gain " + CM.i.RTC(CM.ColorType.Gold, "$" + devilsPactRoundGold) + ". If at 1 life, still gain the gold.";
            default:
            return "Unknown";
        }
    }

    public int GetRelicRarity(ID id)
    {
        if (rarityDict != null && rarityDict.TryGetValue(id, out var rarity))
        {
            return rarity;
        }

        return 1;
    } 

    public CM.ColorType GetRelicRarityColorType(int rarity)
    {
        if (rarity >= 3) return CM.ColorType.RarityTier3;
        if (rarity == 2) return CM.ColorType.RarityTier2;
        return CM.ColorType.RarityTier1;
    }

    public int GetRelicCost(ID id)
    {
        if (relicCosts != null && relicCosts.TryGetValue(id, out int cost))
        {
            return Mathf.Max(1, cost);
        }

        return 1;
    }

    public string GetName(ID id)
    {
        if (TryGetBaseDamageBuffColor(id, out CM.ColorType baseBuffColor))
        {
            return ColorName(baseBuffColor) + " Base Damage Buff Relic";
        }

        if (TryGetDamageMultiplierBuffColor(id, out CM.ColorType multiplierBuffColor))
        {
            return ColorName(multiplierBuffColor) + " Damage Multiplier Buff Relic";
        }

        switch (id)
        {
            case ID.fireTickRate:
            return "Fire Tick Rate Relic";
            case ID.slowDamageBuff:
            return "Slow Damage Buff Relic";
            case ID.slowTickRate:
            return "Slow Tick Rate Relic";
            case ID.criticalSlow:
            return "Critical Slow Relic";
            case ID.criticalCharge:
            return "Critical Charge Relic";
            case ID.criticalDamageBuff:
            return "Critical Damage Buff Relic";
            case ID.slowOverload:
            return "Slow Overload Relic";
            case ID.fireExplosion:
            return "Fire Explosion Relic";
            case ID.entropyArtifact:
            return "Entropy Relic";
            case ID.loneWolfArtifact:
            return "Lone Wolf Relic";
            case ID.exposeNearest:
            return CM.i != null
            ? CM.i.RTC(CM.GetExposeColor(), "Expose") + " Nearest Relic"
            : "Expose Nearest Relic";
            case ID.placementIncrease:
            return "Placement Increase Relic";
            case ID.InventoryBuff:
            return "Inventory Buff Relic";
            case ID.lifeBoost:
            return "Life Boost Relic";
            case ID.LaserBounce:
            return "Laser Bounce Relic";
            case ID.ChargeOnCrit:
            return "Charge On Kill Relic";
            case ID.currencyDamage:
            return "Currency Damage Relic";
            case ID.inspirationRelic:
            return "Inspiration Relic";
            case ID.agentMaxIncrease:
            return "Agent Max Increase Relic";
            case ID.criticalPierce:
            return "Critical Pierce Relic";
            case ID.vengeance:
            return "Vengeance Relic";
            case ID.overkill:
            return "Overkill Relic";
            case ID.laserStun:
            return "Laser Stun Relic";
            case ID.wolfPack:
            return "Wolf Pack Relic";
            case ID.explosionTradeoff:
            return "Explosion Tradeoff Relic";
            case ID.speedyAgent:
            return "Speedy Agent Relic";
            case ID.rerollDiscount:
            return "Reroll Discount Relic";
            case ID.lightningChain:
            return "Lightning Chain Relic";
            case ID.zeal:
            return "Zeal Relic";
            case ID.alwaysCritOnFullyCharge:
            return "Always Crit On Full Charge Relic";
            case ID.plague:
            return "Plague";
            case ID.devilsPact:
            return "DevilsPact";
            default:
            return "Unknown Relic";
        }
    }

    [ContextMenu("Save Relic Lock State")]
    public void SaveLockedToSaveData()
    {
        EnsureRelicDictionariesInitialized();
        if (SaveDataManager.instance == null) return;
        SaveDataManager.instance.SaveRelicLockStates(locked);
    }

    [ContextMenu("Load Relic Lock State")]
    public void LoadLockedFromSaveData()
    {
        EnsureRelicDictionariesInitialized();
        if (SaveDataManager.instance == null) return;
        SaveDataManager.instance.LoadRelicLockStates(locked);
        ApplyDefaultUnlockList(saveIfChanged: false);
        ApplyEditorDefaultRelicUnlocks();
        RefreshRelicGalleryVisuals();
    }

    private void ApplyDefaultUnlockList(bool saveIfChanged)
    {
        EnsureRelicDictionariesInitialized();
        EnsureImplicitDefaultUnlockRelicsIncluded();
        if (defaultUnlockedRelics == null || defaultUnlockedRelics.Count == 0) return;

        bool changed = false;
        for (int i = 0; i < defaultUnlockedRelics.Count; i++)
        {
            ID id = defaultUnlockedRelics[i];
            if (!IsRelicAvailable(id)) continue;
            if (locked.TryGetValue(id, out bool isUnlocked) && isUnlocked) continue;

            locked[id] = true;
            changed = true;
        }

        if (changed && saveIfChanged)
        {
            SaveLockedToSaveData();
        }
    }

    private bool HasExplicitUnlockCondition(ID id)
    {
        return ExplicitUnlockConditionRelics.Contains(id);
    }

    private void EnsureImplicitDefaultUnlockRelicsIncluded()
    {
        if (defaultUnlockedRelics == null)
        {
            defaultUnlockedRelics = new List<ID>();
        }

        var ids = (ID[])System.Enum.GetValues(typeof(ID));
        for (int i = 0; i < ids.Length; i++)
        {
            ID id = ids[i];
            if (HasExplicitUnlockCondition(id)) continue;
            if (!IsRelicAvailable(id)) continue;
            if (defaultUnlockedRelics.Contains(id)) continue;
            defaultUnlockedRelics.Add(id);
        }
    }

    private static bool IsRandomizableDamageType(CM.ColorType colorType)
    {
        var randomizableDamageTypes = CM.RandomizableDamageTypes;
        if (randomizableDamageTypes == null) return false;

        for (int i = 0; i < randomizableDamageTypes.Length; i++)
        {
            if (randomizableDamageTypes[i] == colorType) return true;
        }

        return false;
    }

    private void ApplyEditorDefaultRelicUnlocks()
    {
        #if UNITY_EDITOR
        var ids = (ID[])System.Enum.GetValues(typeof(ID));
        for (int i = 0; i < ids.Length; i++)
        {
            locked[ids[i]] = true;
        }
        #endif
    }
}
