using UnityEngine;
using System.Collections.Generic;
public class TowerDamageGraph : MonoBehaviour
{
    public static TowerDamageGraph instance;
    public RectTransform panelRect;
    private Vector3 showGraphPosition;
    private Vector3 hideGraphPosition;
    private bool displayed = false; 
    public float hideGraphYOffset = 7.5f;

    private List<DamageBar> damageBars;
    private readonly List<Tower> _placedTowers = new List<Tower>(32);
    private static readonly List<SpriteRenderer> _previewRenderers = new List<SpriteRenderer>(8);
    private bool _wasWaveActive;

    private void Awake()
    {
        instance = this;
    }
    private void Start()
    {
        if (panelRect != null)
        {
            showGraphPosition = panelRect.position;
            hideGraphPosition = showGraphPosition;
            hideGraphPosition.y += hideGraphYOffset;
            damageBars = new List<DamageBar>(panelRect.GetComponentsInChildren<DamageBar>(true));
        }
    }
    public void ShowGraph()
    {
        if (RM.i != null)
        {
            RM.i.HideRelicGallery();
        }
        displayed = true;
    }

    public void HideGraph()
    {
        displayed = false;
        ClearDisplayedTowerPreviews();
    }

    public void OnShowGraphButtonPressed()
    {
        if (!displayed) ShowGraph();
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(1))
        {
            //displayed = !displayed;
            HideGraph();
        }
    }

    private void FixedUpdate()
    {
        bool isWaveActive = WaveManager.instance != null && WaveManager.instance.IsWaveActive();
        if (isWaveActive && !_wasWaveActive)
        {
            ResetTrackedTowerDamage();
        }
        _wasWaveActive = isWaveActive;

        if (panelRect == null) return;

        panelRect.position = Vector3.Lerp(panelRect.position, displayed ? showGraphPosition : hideGraphPosition, Time.fixedDeltaTime * 100f);
        if (!displayed)
        {
            ClearDisplayedTowerPreviews();
            return;
        }

        if (TowerManager.instance == null) return;

        if (damageBars == null || damageBars.Count == 0)
        {
            damageBars = new List<DamageBar>(panelRect.GetComponentsInChildren<DamageBar>(true));
            if (damageBars.Count == 0) return;
        }

        _placedTowers.Clear();
        foreach (var tower in TowerManager.instance.EnumeratePlacedTowers())
        {
            if (tower != null) _placedTowers.Add(tower);
        }

        _placedTowers.Sort((a, b) => b.GetTotalDamageDealt().CompareTo(a.GetTotalDamageDealt()));

        float totalDamage = 0f;
        for (int i = 0; i < _placedTowers.Count; i++)
        {
            totalDamage += Mathf.Max(0f, _placedTowers[i].GetTotalDamageDealt());
        }

        for (int i = 0; i < damageBars.Count; i++)
        {
            DamageBar bar = damageBars[i];
            if (bar == null) continue;

            if (i >= _placedTowers.Count)
            {
                if (bar.rankText != null) bar.rankText.text = string.Empty;
                if (bar.damageText != null)
                {
                    bar.damageText.text = string.Empty;
                    bar.damageText.color = Color.white;
                    bar.damageText.outlineColor = Color.black;
                    bar.damageText.outlineWidth = 0.2f;
                }
                if (bar.barImage != null)
                {
                    bar.SetBarImagePercentage(0f);
                    bar.barImage.color = Color.white;
                }
                if (bar.displayedTowerObject != null)
                {
                    Destroy(bar.displayedTowerObject);
                    bar.displayedTowerObject = null;
                }
                if (bar.towerSlotPosition != null) {
                    bar.towerSlotPosition.gameObject.SetActive(false);
                }
                continue;
            }

            Tower tower = _placedTowers[i];
            float damage = Mathf.Max(0f, tower.GetTotalDamageDealt());
            float percent = totalDamage > 0f ? damage / totalDamage : 0f;

            if (bar.damageText != null)
            {
                bar.damageText.text = damage.ToString("0.##");
            }
            if (bar.rankText != null)
            {
                bar.rankText.text = (i + 1).ToString();
            }
            if (bar.barImage != null)
            {
                bar.SetBarImagePercentage(percent);
                var damageType = tower.GetConfiguredDamageType();
                bar.barImage.color = CM.i != null ? CM.i.ColorTypeToColor(damageType) : Color.white;

                if (bar.damageText != null)
                {
                    bool useDarkText = IsBrightColor(bar.barImage.color);
                    bar.damageText.color = useDarkText ? Color.black : Color.white;
                    bar.damageText.outlineColor = useDarkText ? Color.white : Color.black;
                    bar.damageText.outlineWidth = 0.2f;
                }
            }

            if (bar.towerSlotPosition == null) continue;
            bar.towerSlotPosition.gameObject.SetActive(true);
            bool needsNewPreview = bar.displayedTowerObject == null || bar.displayedTowerId != tower.id;
            if (needsNewPreview)
            {
                if (bar.displayedTowerObject != null)
                {
                    Destroy(bar.displayedTowerObject);
                    bar.displayedTowerObject = null;
                }

                if (TowerManager.instance.towerPrefabDictionary.TryGetValue(tower.id, out var prefab) && prefab != null)
                {
                    var preview = Instantiate(prefab);
                    AlignPreviewToSlot(preview.transform, bar.towerSlotPosition);
                    RaisePreviewSortingOrder(preview, 10);
                    bar.displayedTowerObject = preview;
                    bar.displayedTowerId = tower.id;

                    var ti = preview.GetComponent<TowerInteractable>();
                    if (ti != null)
                    {
                        ti.pickupable = false;
                        ti.enabled = false;
                    }

                    var colliders = preview.GetComponentsInChildren<Collider2D>(true);
                    for (int c = 0; c < colliders.Length; c++)
                    {
                        colliders[c].enabled = false;
                    }
                }
            }
            else
            {
                AlignPreviewToSlot(bar.displayedTowerObject.transform, bar.towerSlotPosition);
            }
        }
    }

    private static void RaisePreviewSortingOrder(GameObject previewRoot, int orderOffset)
    {
        if (previewRoot == null || orderOffset == 0) return;

        _previewRenderers.Clear();
        previewRoot.GetComponentsInChildren(true, _previewRenderers);
        for (int i = 0; i < _previewRenderers.Count; i++)
        {
            var renderer = _previewRenderers[i];
            if (renderer == null) continue;
            renderer.sortingOrder += orderOffset;
        }
    }

    private static void AlignPreviewToSlot(Transform previewTransform, Transform slotTransform)
    {
        if (previewTransform == null || slotTransform == null) return;

        Vector3 alignedPosition = slotTransform.position;
        if (slotTransform is RectTransform slotRect)
        {
            Canvas canvas = slotRect.GetComponentInParent<Canvas>();
            if (canvas != null && canvas.renderMode != RenderMode.WorldSpace)
            {
                Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                Camera worldCamera = Camera.main;

                if (worldCamera != null)
                {
                    Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(uiCamera, slotRect.position);
                    float zDistance = Mathf.Abs(previewTransform.position.z - worldCamera.transform.position.z);
                    alignedPosition = worldCamera.ScreenToWorldPoint(new Vector3(screenPoint.x, screenPoint.y, zDistance));
                    alignedPosition.z = previewTransform.position.z;
                }
            }
        }

        previewTransform.position = alignedPosition;
    }

    private static bool IsBrightColor(Color color)
    {
        // Relative luminance approximation for readable foreground contrast.
        float luminance = (0.2126f * color.r) + (0.7152f * color.g) + (0.0722f * color.b);
        return luminance >= 0.6f;
    }

    private void ClearDisplayedTowerPreviews()
    {
        if (damageBars == null) return;

        for (int i = 0; i < damageBars.Count; i++)
        {
            DamageBar bar = damageBars[i];
            if (bar == null || bar.displayedTowerObject == null) continue;

            Destroy(bar.displayedTowerObject);
            bar.displayedTowerObject = null;
        }
    }

    private void ResetTrackedTowerDamage()
    {
        if (TowerManager.instance == null) return;

        foreach (var tower in TowerManager.instance.EnumeratePlacedTowers())
        {
            if (tower == null) continue;
            tower.ResetTotalDamageDealt();
        }
    }
}
