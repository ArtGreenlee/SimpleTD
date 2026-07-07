using System;
using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class SaveDataManager : MonoBehaviour
{
    public static SaveDataManager instance;
    [SerializeField] private SaveData saveData;
    private readonly System.Collections.Generic.Dictionary<Tower.ID, bool> _towerSeenStates = new System.Collections.Generic.Dictionary<Tower.ID, bool>();

    public enum TowerSeenMarkingMode
    {
        OnPurchase,
        OnShownInShop
    }

    [Header("Save Slots")]
    [SerializeField, Min(0)] private int activeSlot = 0;
    [SerializeField] private string saveFilePrefix = "save_slot_";
    [SerializeField] private string saveFileExtension = ".json";
    [SerializeField] private bool prettyPrintJson = false;

    [Header("Safety")]
    [SerializeField, Min(1)] private int saveFormatVersion = 1;

    [Header("Debug Paths")]
    [SerializeField] private bool logSavePaths = false;
    [SerializeField] private string persistentSaveDirectory;
    [SerializeField] private string activeSavePath;
    [SerializeField] private string activeBackupPath;

    [Header("Tower Seen")]
    [SerializeField] private TowerSeenMarkingMode towerSeenMarkingMode = TowerSeenMarkingMode.OnPurchase;

#if UNITY_EDITOR
    [Header("Editor Overrides")]
    [Tooltip("If enabled, all tower seen/purchased states are forced to false while running in the editor.")]
    [SerializeField] private bool forceAllTowersUnseenInEditor = false;
#endif

    [Serializable]
    private sealed class SaveEnvelope
    {
        public int version;
        public string savedAtUtc;
        public string dataJson;
    }

    public int ActiveSlot => activeSlot;
    public string ActiveSavePath => GetSavePath(activeSlot);
    public string ActiveBackupPath => GetBackupPath(activeSlot);

    private void Awake()
    {
        instance = this;
        RefreshDebugPaths();
        Load();
        LoadTowerSeenStates();
        LogSeenTowerPercentageAtStartup();
    }

    private void OnValidate()
    {
        if (activeSlot < 0) activeSlot = 0;
        if (string.IsNullOrWhiteSpace(saveFilePrefix)) saveFilePrefix = "save_slot_";
        if (string.IsNullOrWhiteSpace(saveFileExtension)) saveFileExtension = ".json";
        if (!saveFileExtension.StartsWith(".")) saveFileExtension = "." + saveFileExtension;

        RefreshDebugPaths();
    }

    private void OnApplicationQuit()
    {
#if UNITY_EDITOR
        // In editor, load-only behavior is desired to avoid writing persistent test data.
        return;
#else
        Save();
#endif
    }

    public void Load()
    {
        Load(activeSlot);
    }

    public void Save()
    {
        Save(activeSlot);
    }

    public void SetActiveSlot(int slot, bool autoLoad = true)
    {
        if (slot < 0) slot = 0;
        if (activeSlot == slot) return;

        activeSlot = slot;
        RefreshDebugPaths();

        if (autoLoad)
        {
            Load(activeSlot);
        }
    }

    public bool Load(int slot)
    {
        if (saveData == null)
        {
            Debug.LogError("SaveDataManager.Load failed: saveData is not assigned.");
            return false;
        }

        if (slot < 0) slot = 0;
        if (slot != activeSlot)
        {
            activeSlot = slot;
            RefreshDebugPaths();
        }

        string path = GetSavePath(slot);
        string backupPath = GetBackupPath(slot);
        string json = TryReadText(path);

        if (string.IsNullOrEmpty(json) && File.Exists(backupPath))
        {
            json = TryReadText(backupPath);
            if (!string.IsNullOrEmpty(json))
            {
                Debug.LogWarning($"SaveDataManager: primary save unreadable, restored from backup for slot {slot}. Path: {backupPath}");
            }
        }

        if (string.IsNullOrEmpty(json))
        {
            if (logSavePaths)
            {
                Debug.Log($"SaveDataManager: no save file found for slot {slot}. Expected path: {path}");
            }
            return false;
        }

        if (TryLoadFromEnvelope(json, out var envelopeError))
        {
            return true;
        }

        if (TryLoadLegacy(json, out var legacyError))
        {
            Debug.LogWarning($"SaveDataManager: loaded legacy save format for slot {slot}. Consider re-saving. Path: {path}");
            return true;
        }

        Debug.LogError($"SaveDataManager.Load failed for slot {slot}. Envelope error: {envelopeError}. Legacy error: {legacyError}");
        return false;
    }

    public bool Save(int slot)
    {
#if UNITY_EDITOR
        // Editor play mode is load-only by design to avoid modifying persistent data during iteration.
        return false;
#else
        if (saveData == null)
        {
            Debug.LogError("SaveDataManager.Save failed: saveData is not assigned.");
            return false;
        }

        if (slot < 0) slot = 0;
        if (slot != activeSlot)
        {
            activeSlot = slot;
            RefreshDebugPaths();
        }

        string dir = GetSaveDirectory();
        string path = GetSavePath(slot);
        string backupPath = GetBackupPath(slot);
        string tempPath = path + ".tmp";

        try
        {
            Directory.CreateDirectory(dir);

            var envelope = new SaveEnvelope
            {
                version = saveFormatVersion,
                savedAtUtc = DateTime.UtcNow.ToString("o"),
                dataJson = JsonUtility.ToJson(saveData, prettyPrintJson)
            };

            string json = JsonUtility.ToJson(envelope, prettyPrintJson);
            File.WriteAllText(tempPath, json);

            if (File.Exists(path))
            {
                File.Copy(path, backupPath, true);
                File.Delete(path);
            }

            File.Move(tempPath, path);

            if (logSavePaths)
            {
                Debug.Log($"SaveDataManager: saved slot {slot} to {path}");
            }

            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"SaveDataManager.Save failed for slot {slot}: {ex.Message}");
            return false;
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { }
            }
        }
#endif
    }

    public void SaveRelicLockStates(System.Collections.Generic.Dictionary<RM.ID, bool> locked)
    {
        if (saveData == null || locked == null) return;

        saveData.SetRelicLockStates(locked);
        Save();

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorUtility.SetDirty(saveData);
        }
#endif
    }

    public void LoadRelicLockStates(System.Collections.Generic.Dictionary<RM.ID, bool> locked)
    {
        if (saveData == null || locked == null) return;
        saveData.FillRelicLockStates(locked);
    }

    public void NotifyTowerPurchasedFromShop(Tower.ID towerId)
    {
        if (saveData == null) return;
        if (towerSeenMarkingMode != TowerSeenMarkingMode.OnPurchase) return;

        LoadTowerSeenStates();
        _towerSeenStates[towerId] = true;
        saveData.SetTowerSeenStates(_towerSeenStates);
    }

    public void NotifyTowerShownInShop(Tower.ID towerId)
    {
        if (saveData == null) return;
        if (towerSeenMarkingMode != TowerSeenMarkingMode.OnShownInShop) return;

        LoadTowerSeenStates();
        _towerSeenStates[towerId] = true;
        saveData.SetTowerSeenStates(_towerSeenStates);
    }

    public bool HasPurchasedTowerFromShop(Tower.ID towerId)
    {
        if (saveData == null)
        {
#if UNITY_EDITOR
            if (forceAllTowersUnseenInEditor) return false;
            return true;
#else
            return false;
#endif
        }

        LoadTowerSeenStates();
        return _towerSeenStates.TryGetValue(towerId, out bool purchased) && purchased;
    }

    [ContextMenu("Log Active Save Paths")]
    public void LogActiveSavePaths()
    {
        RefreshDebugPaths();
        Debug.Log($"SaveDataManager paths\nDirectory: {persistentSaveDirectory}\nSave: {activeSavePath}\nBackup: {activeBackupPath}");
    }

    private bool TryLoadFromEnvelope(string json, out string error)
    {
        error = string.Empty;
        try
        {
            var envelope = JsonUtility.FromJson<SaveEnvelope>(json);
            if (envelope == null)
            {
                error = "Envelope parse returned null";
                return false;
            }

            if (string.IsNullOrEmpty(envelope.dataJson))
            {
                error = "Envelope payload is empty";
                return false;
            }

            JsonUtility.FromJsonOverwrite(envelope.dataJson, saveData);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private bool TryLoadLegacy(string json, out string error)
    {
        error = string.Empty;
        try
        {
            JsonUtility.FromJsonOverwrite(json, saveData);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private string TryReadText(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            return File.ReadAllText(path);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"SaveDataManager: failed to read path {path}. {ex.Message}");
            return null;
        }
    }

    private string GetSaveDirectory()
    {
        return Path.Combine(Application.persistentDataPath, "Saves");
    }

    private string GetSavePath(int slot)
    {
        return Path.Combine(GetSaveDirectory(), $"{saveFilePrefix}{slot}{saveFileExtension}");
    }

    private string GetBackupPath(int slot)
    {
        return GetSavePath(slot) + ".bak";
    }

    private void RefreshDebugPaths()
    {
        persistentSaveDirectory = GetSaveDirectory();
        activeSavePath = GetSavePath(activeSlot);
        activeBackupPath = GetBackupPath(activeSlot);
    }

    private void LoadTowerSeenStates()
    {
        if (saveData == null) return;

#if UNITY_EDITOR
        bool defaultPurchased = true;
        if (forceAllTowersUnseenInEditor)
        {
            defaultPurchased = false;
        }
#else
        bool defaultPurchased = false;
#endif

        saveData.FillTowerSeenStates(_towerSeenStates, defaultPurchased);

    #if UNITY_EDITOR
        if (forceAllTowersUnseenInEditor)
        {
            var ids = (Tower.ID[])Enum.GetValues(typeof(Tower.ID));
            for (int i = 0; i < ids.Length; i++)
            {
            _towerSeenStates[ids[i]] = false;
            }
        }
    #endif

        saveData.SetTowerSeenStates(_towerSeenStates);
    }

    private void LogSeenTowerPercentageAtStartup()
    {
        if (saveData == null)
        {
            Debug.Log("Seen towers: 0/0 (0.0%)");
            return;
        }

        int totalTowers = Enum.GetValues(typeof(Tower.ID)).Length;
        if (totalTowers <= 0)
        {
            Debug.Log("Seen towers: 0/0 (0.0%)");
            return;
        }

        int seenCount = 0;
        foreach (var pair in _towerSeenStates)
        {
            if (pair.Value) seenCount++;
        }

        float percentSeen = (seenCount / (float)totalTowers) * 100f;
        Debug.Log($"Seen towers: {seenCount}/{totalTowers} ({percentSeen:F1}%)");
    }

    public bool HasSaveData()
    {
        return saveData != null;
    }

    public void SetDamageDealtToEnemies(float value)
    {
        if (saveData == null) return;
        saveData.damageDealtToEnemies = Mathf.Max(0f, value);
    }

    public void AddDamageDealtToEnemies(float amount)
    {
        if (saveData == null) return;
        if (amount <= 0f) return;
        saveData.damageDealtToEnemies += amount;
    }

    public void SetEnemiesDefeated(int value)
    {
        if (saveData == null) return;
        saveData.enemiesDefeated = Mathf.Max(0, value);
    }

    public void AddEnemiesDefeated(int amount)
    {
        if (saveData == null) return;
        if (amount <= 0) return;
        saveData.enemiesDefeated += amount;
    }

    public void SetCurrencyCollected(int value)
    {
        if (saveData == null) return;
        saveData.currencyCollected = Mathf.Max(0, value);
    }

    public void AddCurrencyCollected(int amount)
    {
        if (saveData == null) return;
        if (amount <= 0) return;
        saveData.currencyCollected += amount;
    }

    public void SetMaxCurrencyCollected(int value)
    {
        if (saveData == null) return;
        saveData.maxCurrencyCollected = Mathf.Max(0, value);
    }

    public void AddFireDamageDealt(float amount)
    {
        if (saveData == null) return;
        if (amount <= 0f) return;

        saveData.fireDamageDealt += amount;
        TryUnlockFireTickRateByProgress();
    }

    public void AddPoisonDamageDealt(float amount)
    {
        if (saveData == null) return;
        if (amount <= 0f) return;

        saveData.poisonDamageDealt += amount;
        TryUnlockPlagueByProgress();
    }

    public float GetPoisonDamageDealt()
    {
        if (saveData == null) return 0f;
        return Mathf.Max(0f, saveData.poisonDamageDealt);
    }

    public void SetFireDamageDealt(float value)
    {
        if (saveData == null) return;
        saveData.fireDamageDealt = Mathf.Max(0f, value);
        TryUnlockFireTickRateByProgress();
    }

    public float GetFireDamageDealt()
    {
        if (saveData == null) return 0f;
        return Mathf.Max(0f, saveData.fireDamageDealt);
    }

    private void TryUnlockFireTickRateByProgress()
    {
        if (saveData == null) return;
        if (saveData.fireDamageDealt < RM.fireTickRateUnlock) return;

        if (RM.i == null) return;
        if (RM.i.IsUnlocked(RM.ID.fireTickRate)) return;

        RM.i.locked[RM.ID.fireTickRate] = true;
        RM.i.RefreshRelicGalleryVisuals();
        RM.i.ShowRelicUnlockNotification(RM.ID.fireTickRate);
        SaveRelicLockStates(RM.i.locked);
    }

    public void NotifySingleHitDamage(float amount)
    {
        if (saveData == null) return;
        if (amount <= 0f) return;

        if (amount > saveData.maxSingleHitDamage)
        {
            saveData.maxSingleHitDamage = amount;
            TryUnlockOverkillByProgress();
        }
    }

    public float GetMaxSingleHitDamage()
    {
        if (saveData == null) return 0f;
        return Mathf.Max(0f, saveData.maxSingleHitDamage);
    }

    private void TryUnlockOverkillByProgress()
    {
        if (saveData == null) return;
        if (saveData.maxSingleHitDamage < 100f) return;

        if (RM.i == null) return;
        if (RM.i.IsUnlocked(RM.ID.overkill)) return;

        RM.i.locked[RM.ID.overkill] = true;
        RM.i.RefreshRelicGalleryVisuals();
        RM.i.ShowRelicUnlockNotification(RM.ID.overkill);
        SaveRelicLockStates(RM.i.locked);
    }

    public void NotifyCurrencyAmount(int amount)
    {
        if (saveData == null) return;

        if (amount > saveData.maxCurrencyCollected)
        {
            saveData.maxCurrencyCollected = amount;
        }

        TryUnlockCurrencyDamageByProgress(amount);
    }

    public int GetMaxCurrencyCollected()
    {
        if (saveData == null) return 0;
        return Mathf.Max(0, saveData.maxCurrencyCollected);
    }

    private void TryUnlockCurrencyDamageByProgress(int currentAmount)
    {
        if (saveData == null) return;
        if (currentAmount <= 100) return;

        if (RM.i == null) return;
        if (RM.i.IsUnlocked(RM.ID.currencyDamage)) return;

        RM.i.locked[RM.ID.currencyDamage] = true;
        RM.i.RefreshRelicGalleryVisuals();
        RM.i.ShowRelicUnlockNotification(RM.ID.currencyDamage);
        SaveRelicLockStates(RM.i.locked);
    }

    private void TryUnlockPlagueByProgress()
    {
        if (saveData == null) return;
        if (saveData.poisonDamageDealt < RM.plagueUnlockPoisonDamage) return;

        if (RM.i == null) return;
        if (RM.i.IsUnlocked(RM.ID.plague)) return;

        RM.i.locked[RM.ID.plague] = true;
        RM.i.RefreshRelicGalleryVisuals();
        RM.i.ShowRelicUnlockNotification(RM.ID.plague);
        SaveRelicLockStates(RM.i.locked);
    }

    public void TryUnlockDevilsPactByProgress()
    {
        if (RM.i == null) return;
        if (RM.i.IsUnlocked(RM.ID.devilsPact)) return;
        if (GameController.instance == null) return;
        if (CurrencyManager.instance == null) return;

        int lives = GameController.instance.GetLives();
        int currency = CurrencyManager.instance.GetCurrency();
        if (lives >= 5) return;
        if (currency <= 100) return;

        RM.i.locked[RM.ID.devilsPact] = true;
        RM.i.RefreshRelicGalleryVisuals();
        RM.i.ShowRelicUnlockNotification(RM.ID.devilsPact);
        SaveRelicLockStates(RM.i.locked);
    }

    public void AddExposedDamageDealt(float amount)
    {
        if (saveData == null) return;
        if (amount <= 0f) return;

        saveData.exposedDamageDealt += amount;
        TryUnlockExposeNearestByProgress();
    }

    public float GetExposedDamageDealt()
    {
        if (saveData == null) return 0f;
        return Mathf.Max(0f, saveData.exposedDamageDealt);
    }

    private void TryUnlockExposeNearestByProgress()
    {
        if (saveData == null) return;
        if (saveData.exposedDamageDealt < 1000f) return;

        if (RM.i == null) return;
        if (RM.i.IsUnlocked(RM.ID.exposeNearest)) return;

        RM.i.locked[RM.ID.exposeNearest] = true;
        RM.i.RefreshRelicGalleryVisuals();
        RM.i.ShowRelicUnlockNotification(RM.ID.exposeNearest);
        SaveRelicLockStates(RM.i.locked);
    }

    public void NotifyLaserEnemiesHit(int count)
    {
        if (saveData == null) return;
        if (count <= saveData.maxEnemiesHitByLaser) return;

        saveData.maxEnemiesHitByLaser = count;
        TryUnlockLaserStunByProgress();
    }

    public int GetMaxEnemiesHitByLaser()
    {
        if (saveData == null) return 0;
        return Mathf.Max(0, saveData.maxEnemiesHitByLaser);
    }

    private void TryUnlockLaserStunByProgress()
    {
        if (saveData == null) return;
        if (saveData.maxEnemiesHitByLaser < RM.laserStunUnlockEnemiesHit) return;

        if (RM.i == null) return;
        if (RM.i.IsUnlocked(RM.ID.laserStun)) return;

        RM.i.locked[RM.ID.laserStun] = true;
        RM.i.RefreshRelicGalleryVisuals();
        RM.i.ShowRelicUnlockNotification(RM.ID.laserStun);
        SaveRelicLockStates(RM.i.locked);
    }

    public void NotifyLaserBounceCount(int bounces)
    {
        if (saveData == null) return;
        if (bounces <= saveData.maxLaserBounces) return;

        saveData.maxLaserBounces = bounces;
        TryUnlockLaserBounceByProgress();
    }

    public int GetMaxLaserBounces()
    {
        if (saveData == null) return 0;
        return Mathf.Max(0, saveData.maxLaserBounces);
    }

    private void TryUnlockLaserBounceByProgress()
    {
        if (saveData == null) return;
        if (saveData.maxLaserBounces <= RM.laserBounceUnlockBounces) return;

        if (RM.i == null) return;
        if (RM.i.IsUnlocked(RM.ID.LaserBounce)) return;

        RM.i.locked[RM.ID.LaserBounce] = true;
        RM.i.RefreshRelicGalleryVisuals();
        RM.i.ShowRelicUnlockNotification(RM.ID.LaserBounce);
        SaveRelicLockStates(RM.i.locked);
    }

    public void UpdateMaxTagTowersPlaced()
    {
        if (saveData == null) return;
        if (TagManager.instance == null) return;

        saveData.maxRedTagTowersPlaced    = Mathf.Max(saveData.maxRedTagTowersPlaced,    TagManager.instance.GetTagCount(Tower.Tag.Red));
        saveData.maxBlueTagTowersPlaced   = Mathf.Max(saveData.maxBlueTagTowersPlaced,   TagManager.instance.GetTagCount(Tower.Tag.Blue));
        saveData.maxGreenTagTowersPlaced  = Mathf.Max(saveData.maxGreenTagTowersPlaced,  TagManager.instance.GetTagCount(Tower.Tag.Green));
        saveData.maxPurpleTagTowersPlaced = Mathf.Max(saveData.maxPurpleTagTowersPlaced, TagManager.instance.GetTagCount(Tower.Tag.Purple));
        saveData.maxYellowTagTowersPlaced = Mathf.Max(saveData.maxYellowTagTowersPlaced, TagManager.instance.GetTagCount(Tower.Tag.Yellow));
    }

    public void AddCritLanded(CM.ColorType damageType)
    {
        if (saveData == null) return;

        switch (damageType)
        {
            case CM.ColorType.Red:    saveData.redCritsLanded++;    break;
            case CM.ColorType.Blue:   saveData.blueCritsLanded++;   break;
            case CM.ColorType.Green:  saveData.greenCritsLanded++;  break;
            case CM.ColorType.Yellow: saveData.yellowCritsLanded++; break;
            case CM.ColorType.Purple: saveData.purpleCritsLanded++; break;
            case CM.ColorType.Orange: saveData.orangeCritsLanded++; break;
            case CM.ColorType.White:  saveData.whiteCritsLanded++;  break;
            case CM.ColorType.Cyan:   saveData.cyanCritsLanded++;   break;
        }
    }

    public void NotifyTowersPlaced(int count)
    {
        if (saveData == null) return;
        if (count <= saveData.maxTowersPlacedAtOnce) return;

        saveData.maxTowersPlacedAtOnce = count;
        TryUnlockWolfPackByProgress();
    }

    public int GetMaxTowersPlacedAtOnce()
    {
        return saveData != null ? saveData.maxTowersPlacedAtOnce : 0;
    }

    public int GetMaxRedTagTowersPlaced()
    {
        return saveData != null ? saveData.maxRedTagTowersPlaced : 0;
    }

    private void TryUnlockWolfPackByProgress()
    {
        if (saveData == null) return;
        if (saveData.maxTowersPlacedAtOnce < RM.wolfPackUnlockTowersPlaced) return;

        if (RM.i == null) return;
        if (RM.i.IsUnlocked(RM.ID.wolfPack)) return;

        RM.i.locked[RM.ID.wolfPack] = true;
        RM.i.RefreshRelicGalleryVisuals();
        RM.i.ShowRelicUnlockNotification(RM.ID.wolfPack);
        SaveRelicLockStates(RM.i.locked);
    }

    public void TryUnlockExplosionTradeoffByProgress()
    {
        if (saveData == null) return;
        if (saveData.maxRedTagTowersPlaced < RM.explosionTradeoffUnlockRedTagTowers) return;

        if (RM.i == null) return;
        if (RM.i.IsUnlocked(RM.ID.explosionTradeoff)) return;

        RM.i.locked[RM.ID.explosionTradeoff] = true;
        RM.i.RefreshRelicGalleryVisuals();
        RM.i.ShowRelicUnlockNotification(RM.ID.explosionTradeoff);
        SaveRelicLockStates(RM.i.locked);
    }

    public void NotifyAgentsOnField(int count)
    {
        if (saveData == null) return;
        if (count <= saveData.maxAgentsOnField) return;

        saveData.maxAgentsOnField = count;
        TryUnlockSpeedyAgentByProgress();
    }

    public int GetMaxAgentsOnField()
    {
        return saveData != null ? saveData.maxAgentsOnField : 0;
    }

    private void TryUnlockSpeedyAgentByProgress()
    {
        if (saveData == null) return;
        if (saveData.maxAgentsOnField < RM.speedyAgentUnlockAgentsOnField) return;

        if (RM.i == null) return;
        if (RM.i.IsUnlocked(RM.ID.speedyAgent)) return;

        RM.i.locked[RM.ID.speedyAgent] = true;
        RM.i.RefreshRelicGalleryVisuals();
        RM.i.ShowRelicUnlockNotification(RM.ID.speedyAgent);
        SaveRelicLockStates(RM.i.locked);
    }

    public void NotifyReroll()
    {
        if (saveData == null) return;
        saveData.totalRerollCount++;
        TryUnlockRerollDiscountByProgress();
    }

    public int GetTotalRerollCount()
    {
        return saveData != null ? saveData.totalRerollCount : 0;
    }

    private void TryUnlockRerollDiscountByProgress()
    {
        if (saveData == null) return;
        if (saveData.totalRerollCount < RM.rerollDiscountUnlockRerolls) return;

        if (RM.i == null) return;
        if (RM.i.IsUnlocked(RM.ID.rerollDiscount)) return;

        RM.i.locked[RM.ID.rerollDiscount] = true;
        RM.i.RefreshRelicGalleryVisuals();
        RM.i.ShowRelicUnlockNotification(RM.ID.rerollDiscount);
        SaveRelicLockStates(RM.i.locked);
    }

    public void AddLightningDamageDealt(float amount)
    {
        if (saveData == null) return;
        saveData.lightningDamageDealt += amount;
        TryUnlockLightningChainByProgress();
    }

    public float GetLightningDamageDealt()
    {
        return saveData != null ? saveData.lightningDamageDealt : 0f;
    }

    private void TryUnlockLightningChainByProgress()
    {
        if (saveData == null) return;
        if (saveData.lightningDamageDealt < RM.lightningChainUnlockDamage) return;

        if (RM.i == null) return;
        if (RM.i.IsUnlocked(RM.ID.lightningChain)) return;

        RM.i.locked[RM.ID.lightningChain] = true;
        RM.i.RefreshRelicGalleryVisuals();
        RM.i.ShowRelicUnlockNotification(RM.ID.lightningChain);
        SaveRelicLockStates(RM.i.locked);
    }
}
