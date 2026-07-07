using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CursorToolTipController : MonoBehaviour
{
    public static CursorToolTipController instance;

    public Transform cursorToolTipTransform;
    public TextMeshProUGUI cursorToolTipText;

    [Header("Enabled Interactable Types")]
    [SerializeField] private bool enableTowerToolTips = true;
    [SerializeField] private bool enableEnemyToolTips = true;
    [SerializeField] private bool enableUpgradeToolTips = true;
    [SerializeField] private bool enableRelicToolTips = true;
    [SerializeField] private bool enableFixedDirectionToolTips = true;
    [SerializeField] private bool enableOtherInteractableToolTips = true;

    [SerializeField, Min(0f)] private float diagonalOffset = 0.75f;

    [Header("Size Fitting")]
    [SerializeField, Min(0f)] private float widthFudge = 0.2f;
    [SerializeField, Min(0f)] private float heightFudge = 0.2f;

    private Interactable _currentInteractable;
    private bool _hasCustomTooltip;
    private string _customTooltipText;
    private Vector3 _customTooltipWorldPosition;

    private void Awake()
    {
        instance = this;
        Hide();
    }

    private void LateUpdate()
    {
        // Hide if player is holding a tower
        if (PIC.instance != null && PIC.instance.isHoldingTower())
        {
            Hide();
            return;
        }

        if (_hasCustomTooltip)
        {
            if (string.IsNullOrEmpty(_customTooltipText))
            {
                Hide();
                return;
            }

            ShowInternalAtWorld(_customTooltipWorldPosition, _customTooltipText);
            return;
        }

        if (_currentInteractable == null)
        {
            Hide();
            return;
        }

        if (!AreToolTipsEnabledFor(_currentInteractable))
        {
            Hide();
            return;
        }

        string text = _currentInteractable.GetCursorToolTipText();
        if (string.IsNullOrEmpty(text))
        {
            Hide();
            return;
        }

        ShowInternal(_currentInteractable.transform, text);
    }

    public void ShowCustomToolTip(Vector3 worldPosition, string text)
    {
        _hasCustomTooltip = !string.IsNullOrEmpty(text);
        _customTooltipWorldPosition = worldPosition;
        _customTooltipText = text ?? string.Empty;

        if (!_hasCustomTooltip)
        {
            Hide();
            return;
        }

        ShowInternalAtWorld(_customTooltipWorldPosition, _customTooltipText);
    }

    public void HideCustomToolTip()
    {
        _hasCustomTooltip = false;
        _customTooltipText = string.Empty;

        if (_currentInteractable == null)
        {
            Hide();
        }
    }

    public void ShowForInteractable(Interactable interactable)
    {
        _currentInteractable = interactable;
        if (_currentInteractable == null)
        {
            Hide();
            return;
        }

        if (!AreToolTipsEnabledFor(_currentInteractable))
        {
            Hide();
            return;
        }

        string text = _currentInteractable.GetCursorToolTipText();
        if (string.IsNullOrEmpty(text))
        {
            Hide();
            return;
        }

        ShowInternal(_currentInteractable.transform, text);
    }

    private bool AreToolTipsEnabledFor(Interactable interactable)
    {
        if (interactable == null) return false;
        if (interactable is TowerInteractable) return enableTowerToolTips;
        if (interactable is EnemyInteractable) return enableEnemyToolTips;
        if (interactable is UpgradeInteractable) return enableUpgradeToolTips;
        if (interactable is Relic) return enableRelicToolTips;
        if (interactable is FixedDirectionInteractable) return enableFixedDirectionToolTips;
        return enableOtherInteractableToolTips;
    }

    public void HideForInteractable(Interactable interactable)
    {
        if (_currentInteractable != interactable) return;
        _currentInteractable = null;
        if (!_hasCustomTooltip)
        {
            Hide();
        }
    }

    private void ShowInternal(Transform target, string text)
    {
        if (cursorToolTipTransform == null || cursorToolTipText == null || target == null)
        {
            return;
        }

        Vector3 offset = new Vector3(diagonalOffset, diagonalOffset, 0f);
        ShowInternalAtWorld(target.position + offset, text);
    }

    private void ShowInternalAtWorld(Vector3 worldPosition, string text)
    {
        if (cursorToolTipTransform == null || cursorToolTipText == null)
        {
            return;
        }

        cursorToolTipText.text = text;

        // Force layout recalculation to get preferred dimensions
        Canvas.ForceUpdateCanvases();
        
        // Get the preferred dimensions and apply with fudge to the tooltip container
        float preferredWidth = cursorToolTipText.preferredWidth + widthFudge;
        float preferredHeight = cursorToolTipText.preferredHeight + heightFudge;
        RectTransform tooltipRect = cursorToolTipTransform as RectTransform;
        if (tooltipRect != null)
        {
            tooltipRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, preferredWidth);
            tooltipRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, preferredHeight);
        }

        cursorToolTipTransform.position = worldPosition;

        if (!cursorToolTipTransform.gameObject.activeSelf)
        {
            cursorToolTipTransform.gameObject.SetActive(true);
        }
    }

    public void Hide()
    {
        if (cursorToolTipTransform != null && cursorToolTipTransform.gameObject.activeSelf)
        {
            cursorToolTipTransform.gameObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
