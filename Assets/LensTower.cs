using UnityEngine;
using System.Collections.Generic;
using System.Text;
using TMPro;

public class LensTower : Tower
{
    public float lensDamageMultiplier = 3f;
    public bool lightningRodEnabled = false;

    [Header("Charge/Buff Scaling")]
    public float chargeMultiplierIncreaseMin = 0f;
    public float chargeMultiplierIncreaseMax = 1f;
    public float buffMultiplierIncreaseMin = 0f;
    public float buffMultiplierIncreaseMax = 1f;
    public float onLensMultiplierAppliedDecrease = 0.5f;

    public Transform lensTransform;
    public Transform rodTransform;
    public float lensRadius;
    public LineRenderer lensEdgeLineRenderer;
    public Transform lensDamageMultiplierTextCanvas;
    public TMP_Text lensText;
    [Range(0f, 1f)] public float lensTextAlpha = 1f;

    [Header("Hit Visualization")]
    [Range(0f, 1f)]
    [SerializeField] private float hitAlphaIncrease = 0.2f;
    [Range(0f, 1f)]
    [SerializeField] private float maxLensObjectAlpha = 1f;
    [Range(0f, 1f)]
    [SerializeField] private float lensEdgeAlphaBonus = 0.15f;
    [Min(0f)]
    [SerializeField] private float desiredAlphaDecaySpeed = 4f;
    [Min(0f)]
    [SerializeField] private float alphaLerpBaseSpeed = 8f;
    [Min(0f)]
    [SerializeField] private float alphaLerpDistanceSpeedMultiplier = 20f;
    [Range(0f, 0.5f)]
    [SerializeField] private float hitScaleBounceAmount = 0.08f;
    [Min(0f)]
    [SerializeField] private float hitScaleBounceDecaySpeed = 2.5f;
    [Min(8)]
    [SerializeField] private int edgeCircleSegments = 64;

    [Header("Body Rotation")]
    [SerializeField] private bool rotateTowerTowardLens = true;
    [SerializeField, Min(0f)] private float bodyRotationLerpSpeed = 30f;
    [SerializeField] private float bodyRotationAngleOffset = -90f;

    [HideInInspector] public Lens lensController;

    private SRC _src;
    private SRC _lensControllerSrc;
    private float _baseLensAlpha = 1f;
    private float _hitPulseAlpha = 0f;
    private float _currentLensAlpha = 1f;
    private float _hitScalePulse = 0f;
    private float _intendedLensScale = 0f;
    private float _currentLensScaleMultiplier = 1f;
    [Min(0f)]
    [SerializeField] private float lensStunScaleLerpSpeed = 5f;
    private bool _hasManualTargetBlockedCell;
    private Vector2Int _manualTargetBlockedCell = new Vector2Int(int.MinValue, int.MinValue);

    public override void Awake()
    {
        lensController = GetComponentInChildren<Lens>();
        if (rodTransform == null)
        {
            Rod rod = GetComponentInChildren<Rod>(includeInactive: true);
            if (rod != null) rodTransform = rod.transform;
        }
        if (lensEdgeLineRenderer == null && lensTransform != null)
        {
            lensEdgeLineRenderer = lensTransform.GetComponent<LineRenderer>();
        }

        ApplyLensScale();
        _intendedLensScale = Mathf.Max(0f, lensRadius);
        _currentLensScaleMultiplier = 1f;
        _src = lensTransform.GetComponent<SRC>();
        _lensControllerSrc = lensController != null ? lensController.GetComponent<SRC>() : null;
        base.Awake();
        
        SyncLensControllerActiveState();
        SyncRodActiveState();
        CacheStartingLensAlpha();
        SetupLensEdgeCircle();
    }

    public override void Update()
    {
        base.Update();
        UpdateTowerRotationTowardsLens();
        UpdateLensAlphaVisualization();
        UpdateLensEdgeLineVisual();
        UpdateLensScaleBounceVisualization();
        UpdateLensStunScale();
        SyncLensControllerActiveState();
        SyncRodActiveState();
        SyncLensTextCanvasScale();
        UpdateLensMultiplierText();
    }

    private void OnDisable()
    {
        ReleaseManualTargetBlockedCell();
    }

    private void UpdateTowerRotationTowardsLens()
    {
        // if (!rotateTowerTowardLens) return;
        // if (CurrentState != State.Placed) return;
        // if (lensTransform == null) return;

        // Vector3 toLens = lensTransform.position - transform.position;
        // toLens.z = 0f;
        // if (toLens.sqrMagnitude <= 0.000001f) return;

        // float targetAngle = Mathf.Atan2(toLens.y, toLens.x) * Mathf.Rad2Deg + bodyRotationAngleOffset;
        // float t = 1f - Mathf.Exp(-Mathf.Max(0f, bodyRotationLerpSpeed) * Time.deltaTime);
        // float nextAngle = Mathf.LerpAngle(transform.eulerAngles.z, targetAngle, t);
        // transform.rotation = Quaternion.Euler(0f, 0f, nextAngle);
    }

    private void SetupLensEdgeCircle()
    {
        if (lensEdgeLineRenderer == null) return;

        int seg = Mathf.Max(8, edgeCircleSegments);
        lensEdgeLineRenderer.useWorldSpace = false;
        lensEdgeLineRenderer.loop = true;
        lensEdgeLineRenderer.positionCount = seg;

        const float unitRadius = 0.5f;
        float step = Mathf.PI * 2f / seg;
        for (int i = 0; i < seg; i++)
        {
            float a = step * i;
            lensEdgeLineRenderer.SetPosition(i, new Vector3(Mathf.Cos(a) * unitRadius, Mathf.Sin(a) * unitRadius, 0f));
        }

        UpdateLensEdgeLineVisual();
    }

    private void UpdateLensEdgeLineVisual()
    {
        if (lensEdgeLineRenderer == null) return;

        Color baseColor = _src != null ? _src.GetPrimaryColor() : Color.white;
        float alpha = Mathf.Clamp01(baseColor.a + lensEdgeAlphaBonus);
        baseColor.a = alpha;

        if (lensEdgeLineRenderer.startColor != baseColor || lensEdgeLineRenderer.endColor != baseColor)
        {
            lensEdgeLineRenderer.startColor = baseColor;
            lensEdgeLineRenderer.endColor = baseColor;
        }
    }

    public void IncreaseLensSizePercent(float percent)
    {
        float multiplier = 1f + Mathf.Max(0f, percent) / 100f;
        lensRadius = Mathf.Max(0f, lensRadius * multiplier);
        ApplyLensScale();
    }

    private void ApplyLensScale()
    {
        if (lensTransform == null) return;
        float scale = Mathf.Max(0f, lensRadius) * (1f + _hitScalePulse) * _currentLensScaleMultiplier;
        lensTransform.localScale = Vector3.one * scale;
    }

    private void UpdateLensScaleBounceVisualization()
    {
        _hitScalePulse = Mathf.MoveTowards(_hitScalePulse, 0f, hitScaleBounceDecaySpeed * Time.deltaTime);
        ApplyLensScale();
    }

    private void UpdateLensStunScale()
    {
        float targetScale = HasStatusEffect(StatusEffect.Stunned) ? 0f : 1f;
        _currentLensScaleMultiplier = Mathf.MoveTowards(_currentLensScaleMultiplier, targetScale, lensStunScaleLerpSpeed * Time.deltaTime);
        ApplyLensScale();
    }

    private void SyncLensTextCanvasScale()
    {
        if (lensDamageMultiplierTextCanvas == null) return;

        Transform parent = lensDamageMultiplierTextCanvas.parent;
        if (parent == null) return;

        Vector3 lossy = parent.lossyScale;
        float invX = Mathf.Abs(lossy.x) > 0.0001f ? 1f / lossy.x : 1f;
        float invY = Mathf.Abs(lossy.y) > 0.0001f ? 1f / lossy.y : 1f;
        float invZ = Mathf.Abs(lossy.z) > 0.0001f ? 1f / lossy.z : 1f;

        lensDamageMultiplierTextCanvas.localScale = new Vector3(invX, invY, invZ);
    }

    private float GetCurrentEffectiveMultiplier()
    {
        float effectiveMultiplier = lensDamageMultiplier;
        
        // Add charge bonus without consuming resources
        float chargePercent = Mathf.Clamp01(chargeAmount / maxChargeAmount);
        if (chargePercent > 0f)
        {
            float chargeBonus = Mathf.Lerp(chargeMultiplierIncreaseMin, chargeMultiplierIncreaseMax, chargePercent);
            effectiveMultiplier += chargeBonus;
        }
        
        // Add buff bonus without consuming resources
        float buffPercent = Mathf.Clamp01(buffAmount / maxBuffAmount);
        if (buffPercent > 0f)
        {
            float buffBonus = Mathf.Lerp(buffMultiplierIncreaseMin, buffMultiplierIncreaseMax, buffPercent);
            effectiveMultiplier += buffBonus;
        }
        
        return effectiveMultiplier;
    }

    public override float GetCooldown(CustomDamageData data = null)
    {
        return 100000f;
    }

    private void UpdateLensMultiplierText()
    {
        if (lensText == null) return;

        if (_lensControllerSrc == null && lensController != null)
        {
            _lensControllerSrc = lensController.GetComponent<SRC>();
        }

        if (Mathf.Approximately(lensDamageMultiplier, 0f))
        {
            lensText.text = string.Empty;
            return;
        }

        float currentMultiplier = GetCurrentEffectiveMultiplier();
        lensText.text = "x" + currentMultiplier.ToString("0.##");

        Color c = _lensControllerSrc != null ? _lensControllerSrc.GetPrimaryColor() : lensText.color;
        c.a = Mathf.Clamp01(lensTextAlpha);
        lensText.color = c;
    }

    public override void OnTargetIndicatorDropped()
    {
        base.OnTargetIndicatorDropped();
        UpdateManualTargetBlockedCell();
    }

    private void CacheStartingLensAlpha()
    {
        if (_src == null)
        {
            _baseLensAlpha = 1f;
            _hitPulseAlpha = 0f;
            return;
        }

        float alpha = 1f;
        if (_src.srColorInfos != null && _src.srColorInfos.Count > 0)
        {
            var first = _src.srColorInfos[0];
            if (first.alphaOverride)
            {
                alpha = Mathf.Clamp01(first.alphaOverrideValue);
            }
            else
            {
                var primary = _src.GetPrimarySpriteRenderer();
                if (primary != null) alpha = Mathf.Clamp01(primary.color.a);
            }
        }

        _baseLensAlpha = alpha;
        _hitPulseAlpha = 0f;
        _currentLensAlpha = Mathf.Clamp(alpha, 0f, Mathf.Clamp01(maxLensObjectAlpha));
        _src.SetSpriteRendererAlpha(_currentLensAlpha);
    }
    
    private void UpdateLensAlphaVisualization()
    {
        if (_src == null) return;

        _hitPulseAlpha = Mathf.MoveTowards(_hitPulseAlpha, 0f, desiredAlphaDecaySpeed * Time.deltaTime);
        float targetAlpha = Mathf.Clamp(_baseLensAlpha + _hitPulseAlpha, 0f, Mathf.Clamp01(maxLensObjectAlpha));
        float alphaGap = Mathf.Abs(targetAlpha - _currentLensAlpha);
        float adaptiveLerpSpeed = alphaLerpBaseSpeed + (alphaGap * alphaLerpDistanceSpeedMultiplier);
        float t = 1f - Mathf.Exp(-adaptiveLerpSpeed * Time.deltaTime);
        _currentLensAlpha = Mathf.Lerp(_currentLensAlpha, targetAlpha, t);
        _src.SetSpriteRendererAlpha(_currentLensAlpha);
    }

    private void SyncLensControllerActiveState()
    {
        if (lensController == null) return;

        bool shouldBeActive = CurrentState == State.Placed;
        GameObject controllerObject = lensController.gameObject;
        if (controllerObject.activeSelf != shouldBeActive)
        {
            controllerObject.SetActive(shouldBeActive);
        }
    }

    private void SyncRodActiveState()
    {
        if (rodTransform == null) return;
        bool shouldBeActive = lightningRodEnabled;
        if (rodTransform.gameObject.activeSelf != shouldBeActive)
        {
            rodTransform.gameObject.SetActive(shouldBeActive);
        }
    }

    public float GetDamageMultiplier(Health health, CustomDamageData data = null)
    {
        if (data == null) {
            return 1;
        }
        if (data.damageType != CM.ColorType.None && IsIncludedInDamageTypes(data.damageType)) {
            float effectiveMultiplier = lensDamageMultiplier;
            
            // Scale multiplier based on charge amount
            float chargePercent = Mathf.Clamp01(chargeAmount / maxChargeAmount);
            if (chargePercent > 0f)
            {
                float chargeBonus = Mathf.Lerp(chargeMultiplierIncreaseMin, chargeMultiplierIncreaseMax, chargePercent);
                effectiveMultiplier += chargeBonus;
            }
            
            // Scale multiplier based on buff amount
            float buffPercent = Mathf.Clamp01(buffAmount / maxBuffAmount);
            if (buffPercent > 0f)
            {
                float buffBonus = Mathf.Lerp(buffMultiplierIncreaseMin, buffMultiplierIncreaseMax, buffPercent);
                effectiveMultiplier += buffBonus;
            }
            
            return effectiveMultiplier;
        }
        return 1;
    }

    public float GetAgentDamageMultiplier(Health health, CustomDamageData data = null)
    {
        return 1;
    }

    public string GetLensDescription()
    {
        List<CM.ColorType> damageTypes = GetConfiguredDamageTypes();
        StringBuilder typesText = new StringBuilder();

        if (damageTypes != null && damageTypes.Count > 0)
        {
            for (int i = 0; i < damageTypes.Count; i++)
            {
                CM.ColorType type = damageTypes[i];
                string name = CM.i != null ? CM.i.ColorToName(type) : type.ToString();
                string coloredName = CM.i != null ? CM.i.RTC(type, name) : name;

                if (typesText.Length > 0)
                {
                    typesText.Append("/");
                }

                typesText.Append(coloredName);
            }
        }
        else
        {
            typesText.Append("configured");
        }

        float increasedPercent = Mathf.Max(0f, (lensDamageMultiplier - 1f) * 100f);
        string lensWord = CM.i != null ? CM.i.RTC(CM.ColorType.White, "Lens") : "Lens";

        return "all " + typesText + " damage within the " + lensWord + " is increased by " + increasedPercent.ToString("0.#") + "%";
    }

    public void OnEnemyDamagedByLens(Enemy enemy)
    {
        _hitPulseAlpha = Mathf.Clamp01(_hitPulseAlpha + hitAlphaIncrease);
        _hitScalePulse = Mathf.Clamp(_hitScalePulse + hitScaleBounceAmount, 0f, 0.5f);
        chargeAmount = Mathf.Max(0f, chargeAmount - onLensMultiplierAppliedDecrease);
        buffAmount = Mathf.Max(0f, buffAmount - onLensMultiplierAppliedDecrease);
        if (!UpgradeActive(UpgradeData.UID.ChargeTowersInLens)) return;
        if (lensController == null) return;

        var towersInLens = lensController.PlacedTowersInRange;
        if (towersInLens == null || towersInLens.Count == 0) return;

        for (int i = 0; i < towersInLens.Count; i++)
        {
            Tower tower = towersInLens[i];
            if (tower == null) continue;
            tower.Charge();
        }
    }

    public void OnAgentDamagedInLens(Agent agent)
    {

    }

    public virtual void OnProjectileTriggerEnterRod(Projectile projectile)
    {
        if (projectile == null) return;

        Vector3 hitPosition = projectile.transform.position;
        Tower lightningSource = projectile.sourceTower != null ? projectile.sourceTower : this;
        TriggerRodLightning(lightningSource, hitPosition, projectile.data);
        projectile.Deactivate();
    }

    public virtual void OnLaserTriggerEnterRod(Tower sourceTower, Vector3 hitPosition, CustomDamageData data = null)
    {
        TriggerRodLightning(sourceTower != null ? sourceTower : this, hitPosition, data);
    }

    private void TriggerRodLightning(Tower lightningSource, Vector3 hitPosition, CustomDamageData data)
    {
        if (lightningSource == null) return;

        CM.ColorType damageType = lightningSource.GetDamageType(data, true);

        if (OnHitParticleEffect.instance != null)
        {
            OnHitParticleEffect.instance.OnHitVfx(hitPosition, lightningSource.GetColor(data));
        }

        if (HitIndicatorObjectPool.instance != null)
        {
            HitIndicatorObjectPool.instance.IndicateProjectileHit(hitPosition, damageType);
        }

        if (LightningHelper.instance == null || lensController == null) return;

        Enemy bestEnemy = null;
        float bestDist2 = float.MaxValue;
        var enemies = lensController.EnemiesInRange;
        for (int i = 0; i < enemies.Count; i++)
        {
            Enemy enemy = enemies[i];
            if (enemy == null) continue;

            float dist2 = (enemy.transform.position - hitPosition).sqrMagnitude;
            if (bestEnemy == null || dist2 < bestDist2)
            {
                bestEnemy = enemy;
                bestDist2 = dist2;
            }
        }

        if (bestEnemy == null) return;

        if (rodTransform != null)
        {
            Rod rod = rodTransform.GetComponent<Rod>();
            if (rod != null)
            {
                rod.Bounce(hitPosition);
            }
        }

        LightningHelper.instance.Lightning(
            lightningSource,
            bestEnemy,
            lightningSource.GetLightningChainCount(),
            applyEffects: true,
            visualize: true,
            includeTowerInVisualization: true,
            data: data,
            originOverride: rodTransform != null ? rodTransform : transform);
    }

    private void UpdateManualTargetBlockedCell()
    {
        var grid = GridManager.instance;
        if (grid == null)
        {
            return;
        }

        if (_hasManualTargetBlockedCell)
        {
            SetCellBlocked(_manualTargetBlockedCell, false);
            _hasManualTargetBlockedCell = false;
        }

        if (towerTool == null || !towerTool.gameObject.activeInHierarchy)
        {
            RefreshPathfinding();
            return;
        }

        if (!grid.TryWorldToCell(towerTool.transform.position, out var cell))
        {
            RefreshPathfinding();
            return;
        }

        SetCellBlocked(cell, true);
        _manualTargetBlockedCell = cell;
        _hasManualTargetBlockedCell = true;
        RefreshPathfinding();
    }

    private void ReleaseManualTargetBlockedCell()
    {
        if (!_hasManualTargetBlockedCell)
        {
            return;
        }

        SetCellBlocked(_manualTargetBlockedCell, false);
        _hasManualTargetBlockedCell = false;
        _manualTargetBlockedCell = new Vector2Int(int.MinValue, int.MinValue);
        RefreshPathfinding();
    }

    private void SetCellBlocked(Vector2Int cell, bool blocked)
    {
        var grid = GridManager.instance;
        if (grid == null) return;

        if (!grid.TryGetCell(cell.x, cell.y, out var current)) return;
        if (current.IsBlocked == blocked) return;

        current.IsBlocked = blocked;
        grid.TrySetCell(current);
    }

    private void RefreshPathfinding()
    {
        if (Pathfinding.instance != null)
        {
            Pathfinding.instance.ResetFlowMaps();
        }
    }
}
