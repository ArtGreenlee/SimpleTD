using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// A self-contained slot that owns and manages the lifecycle of a single displayed tower.
/// Works for shop slots, recipe displays, or any other context that needs to show a tower.
/// Place this component on the slot GameObject whose world position is used as the spawn point.
/// </summary>
public class TowerSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI References")]
    [Tooltip("Background image for this slot. Hidden when slot is disabled.")]
    public RawImage slotImage;
    [Tooltip("Text label used to display cost. Shown only when tower is in Shop state.")]
    public TextMeshProUGUI costText;
    [Tooltip("Optional unlock button shown while slot is locked.")]
    public ShopButton unlockButton;

    [Header("Slot State")]
    [Tooltip("When true, the slot shows the unlock button and unlock cost instead of a tower.")]
    public bool locked = false;
    [Tooltip("Cost to unlock this slot. Assigned automatically by TowerShopManager.")]
    public int unlockCost = 0;
    [Tooltip("When false, the slot is hidden and no tower is displayed regardless of other settings.")]
    [SerializeField] private bool slotEnabled = true;

    [Header("Spawn Settings")]
    [Tooltip("Sorting order delta applied to all SpriteRenderers on the spawned tower.")]
    [SerializeField] private int sortingOrderDelta = 0;
    [Tooltip("If true, ensures the spawned tower has a SwellAnimation component.")]
    [SerializeField] private bool addSwellAnimation = true;
    [Tooltip("If true, calls Indicate on the tower using its damage-type color after spawning. Only fires when the slot is visible and enabled.")]
    [SerializeField] private bool indicateOnSpawn = true;

    // ── Runtime state ──────────────────────────────────────
    private Tower _tower;
    private GameObject _towerObject;

    // ── Accessors ──────────────────────────────────────────
    public Tower GetTower() => _tower;
    public GameObject GetTowerObject() => _towerObject;
    public bool IsSlotEnabled => slotEnabled;
    public bool IsOccupied => _towerObject != null;

    // ── Lifecycle ──────────────────────────────────────────

    private void Update()
    {
        AlignTowerToSlotImagePosition();

        if (costText != null && _tower != null)
            costText.gameObject.SetActive(_tower.CurrentState == Tower.State.Shop);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_tower == null)
            return;

        // Notify ProbabilityDisplay to show hover preview for this tower
        if (ProbabilityDisplay.instance != null)
            ProbabilityDisplay.instance.OnTowerShopSlotHovered(_tower.id);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // Notify ProbabilityDisplay to clear hover preview
        if (ProbabilityDisplay.instance != null)
            ProbabilityDisplay.instance.OnTowerShopSlotHoverEnded();
    }

    // ── Public API ─────────────────────────────────────────

    /// <summary>
    /// Instantiates <paramref name="prefab"/> at this slot's world position and configures it for display.
    /// Destroys any previously owned tower first.
    /// </summary>
    /// <param name="prefab">Tower prefab to spawn.</param>
    /// <param name="allowInteraction">Whether the tower's TowerInteractable allows pickup.</param>
    /// <param name="ghostMode">When true, enables ghost (greyed-out) rendering and blocks interaction.</param>
    /// <param name="overrideSortingOrderDelta">If set, overrides the inspector sortingOrderDelta for this call only.</param>
    public void SetTowerFromPrefab(GameObject prefab, bool allowInteraction = true, bool ghostMode = false, int? overrideSortingOrderDelta = null)
    {
        ClearTower();
        if (prefab == null) return;

        _towerObject = Instantiate(prefab, GetTowerTargetPosition(), transform.rotation);
        _tower = _towerObject.GetComponent<Tower>();
        AlignTowerToSlotImagePosition();

        int delta = overrideSortingOrderDelta ?? sortingOrderDelta;
        if (delta != 0)
            AdjustSortingOrder(_towerObject, delta);

        if (addSwellAnimation && _towerObject.GetComponent<SwellAnimation>() == null)
            _towerObject.AddComponent<SwellAnimation>();

        ApplyGhostMode(ghostMode);
        SetPickupableInternal(allowInteraction && !ghostMode);

        if (indicateOnSpawn && !ghostMode && IsVisibleAndEnabled() && CM.i != null && _tower != null)
            _tower.Indicate(CM.i.ColorTypeToColor(_tower.GetDamageType()));

        RefreshVisuals();
    }

    /// <summary>
    /// Assigns an already-instantiated tower object to this slot without spawning a new one.
    /// Use when the GameObject was created externally (e.g. RecipeDisplay).
    /// The slot does NOT take destroy-ownership; call <see cref="ClearTower"/> with destroyObject:false to detach.
    /// </summary>
    public void AssignExistingTower(GameObject towerObject, bool allowInteraction = true, bool ghostMode = false)
    {
        ClearTower(destroyObject: false);
        _towerObject = towerObject;
        _tower = towerObject != null ? towerObject.GetComponent<Tower>() : null;
        AlignTowerToSlotImagePosition();

        ApplyGhostMode(ghostMode);
        SetPickupableInternal(allowInteraction && !ghostMode);

        if (indicateOnSpawn && !ghostMode && IsVisibleAndEnabled() && CM.i != null && _tower != null)
            _tower.Indicate(CM.i.ColorTypeToColor(_tower.GetDamageType()));

        RefreshVisuals();
    }

    /// <summary>
    /// Clears the slot. Optionally destroys the owned tower GameObject.
    /// </summary>
    public void ClearTower(bool destroyObject = true)
    {
        if (destroyObject && _towerObject != null)
            Destroy(_towerObject);

        _towerObject = null;
        _tower = null;
        RefreshVisuals();
    }

    /// <summary>
    /// Detaches the tower reference without destroying it (e.g. when the player picks it up).
    /// </summary>
    public void ReleaseOwnership()
    {
        _towerObject = null;
        _tower = null;
        RefreshVisuals();
    }

    /// <summary>
    /// Enables or disables this slot. Disabled slots hide their UI and block all interaction.
    /// </summary>
    public void SetSlotEnabled(bool enabled)
    {
        slotEnabled = enabled;
        RefreshVisuals();
    }

    /// <summary>
    /// Refreshes cost text, slot image visibility, and TowerInteractable pickupability
    /// based on current currency and slot state. Safe to call every frame.
    /// </summary>
    public void RefreshVisuals()
    {
        bool visible = IsVisibleAndEnabled();
        int currency = CurrencyManager.instance != null ? CurrencyManager.instance.GetCurrency() : int.MaxValue;

        if (slotImage != null)
            slotImage.enabled = visible;

        if (unlockButton != null)
            unlockButton.gameObject.SetActive(visible && locked);

        if (!visible)
        {
            if (costText != null)
            {
                costText.enabled = false;
                costText.text = string.Empty;
            }
            SetPickupableInternal(false);
            return;
        }

        if (locked)
        {
            if (costText != null)
            {
                costText.enabled = true;
                bool canAffordUnlock = currency >= unlockCost;
                string unlockCostText = "$" + unlockCost;
                if (CM.i != null)
                {
                    unlockCostText = CM.i.RTC(canAffordUnlock ? CM.ColorType.Gold : CM.ColorType.Red, unlockCostText);
                }

                costText.text = unlockCostText;
            }
            SetPickupableInternal(false);
            return;
        }

        bool isShopTower = _tower != null && _tower.CurrentState == Tower.State.Shop;
        int cost = isShopTower ? Mathf.Max(0, _tower.GetCost()) : 0;

        if (costText != null)
        {
            costText.enabled = isShopTower;
            if (!isShopTower)
            {
                costText.text = string.Empty;
            }
            else
            {
                bool canAffordTower = currency >= cost;
                string towerCostText = "$" + cost;
                if (CM.i != null)
                {
                    towerCostText = CM.i.RTC(canAffordTower ? CM.ColorType.Gold : CM.ColorType.Red, towerCostText);
                }

                costText.text = towerCostText;
            }
        }

        if (isShopTower)
        {
            SetPickupableInternal(currency >= cost);
        }
    }

    /// <summary>
    /// Sets pickupability directly without a full visuals refresh.
    /// </summary>
    public void SetPickupable(bool pickupable)
    {
        SetPickupableInternal(pickupable);
    }

    // ── Statics (shared utilities used by external callers too) ────────────

    /// <summary>
    /// Adds or subtracts <paramref name="delta"/> from every SpriteRenderer sorting order managed by the tower's <see cref="SRC"/> component.
    /// Falls back to a full <c>GetComponentsInChildren</c> scan if no <see cref="SRC"/> is present.
    /// </summary>
    public static void AdjustSortingOrder(GameObject towerObject, int delta)
    {
        if (towerObject == null || delta == 0) return;

        var src = towerObject.GetComponent<SRC>();
        if (src != null)
        {
            for (int i = 0; i < src.srColorInfos.Count; i++)
            {
                var group = src.srColorInfos[i];
                if (group.sr == null) continue;
                for (int j = 0; j < group.sr.Count; j++)
                {
                    if (group.sr[j] != null)
                        group.sr[j].sortingOrder += delta;
                }
            }
        }
        else
        {
            var renderers = towerObject.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                    renderers[i].sortingOrder += delta;
            }
        }
    }

    // ── Private helpers ────────────────────────────────────

    private bool IsVisibleAndEnabled()
    {
        if (!slotEnabled) return false;
        if (!gameObject.activeInHierarchy) return false;
        if (slotImage != null && !slotImage.gameObject.activeInHierarchy) return false;
        return true;
    }

    private void ApplyGhostMode(bool ghostMode)
    {
        if (_tower == null) return;
        if (ghostMode)
            _tower.EnableGhostMode();
        else
            _tower.DisableGhostMode();
    }

    private void SetPickupableInternal(bool pickupable)
    {
        if (_towerObject == null) return;
        var ti = _towerObject.GetComponent<TowerInteractable>();
        if (ti != null)
            ti.pickupable = pickupable;
    }

    private Vector3 GetTowerTargetPosition()
    {
        return slotImage != null ? slotImage.transform.position : transform.position;
    }

    private void AlignTowerToSlotImagePosition()
    {
        if (_towerObject == null) return;
        _towerObject.transform.position = GetTowerTargetPosition();
    }
}
