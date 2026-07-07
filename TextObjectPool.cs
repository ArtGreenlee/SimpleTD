using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TextObjectPool : MonoBehaviour
{
    private static readonly Vector2Int[] s_hexDirections =
    {
        new Vector2Int(1, 0),
        new Vector2Int(1, -1),
        new Vector2Int(0, -1),
        new Vector2Int(-1, 0),
        new Vector2Int(-1, 1),
        new Vector2Int(0, 1),
    };

    private sealed class DamageTextContextState
    {
        public readonly Queue<float> damageWindow = new Queue<float>(16);
        public float damageSum;
        public float angleRange;
        public float lastAngleUpdateTime;
    }

    public static TextObjectPool instance;
    public GameObject textPrefab;

    [Header("UI Parent")]
    [Tooltip("If assigned, damage texts will be spawned under this canvas.")]
    public Canvas canvas;

    [Header("Animation")]
    [SerializeField] private float lifetime =0.7f;
    [SerializeField] private float arcHeight =0.35f;
    [SerializeField] private float arcDistance =0.5f;
    [SerializeField] private float randomHorizontalJitter =0.15f;
    [SerializeField] private float scaleSurgeMultiplier =1.15f;
    [SerializeField] private float surgeDecayTime =0.12f;

    [Header("Pool")]
    [SerializeField] private int prewarmCount =24;
    [SerializeField] private bool canGrow = true;

    [Header("Smart Positioning")]
    [SerializeField] private bool smartPositioningEnabled = false;
    [SerializeField] private float smartPositionCellSize = 0.45f;

    [Header("Damage Text Settings")]
    [SerializeField] private bool verboseDamageText = true;
    [SerializeField] private Toggle verboseDamageTextToggle;
    [SerializeField, Min(1)] private int damageTextScalingWindowCount = 20;
    [SerializeField] private float damageTextAngleRangeDecay = 1f;
    [SerializeField] private float damageTextAngleRangeIncrease = 1f;
    [SerializeField] private float damageTextConstantArc = 0f;

    public float SmartPositionCellSize => smartPositionCellSize;

    [Header("Damage Text Debug")]
    [SerializeField] private bool debugAppendRandomZerosToDamageText = false;
    [SerializeField, Min(0)] private int debugMaxExtraTrailingZeros = 4;

    private readonly Queue<GameObject> _available = new Queue<GameObject>(64);
    private readonly Dictionary<Vector2Int, float> _smartCellOccupiedUntil = new Dictionary<Vector2Int, float>(256);
    private readonly Dictionary<int, DamageTextContextState> _damageTextStates = new Dictionary<int, DamageTextContextState>(128);

    private void Awake()
    {
        instance = this;
        HookVerboseDamageTextToggle();

        if (textPrefab == null)
        {
            Debug.LogError("TextObjectPool requires a textPrefab.", this);
            return;
        }

        Prewarm();
    }

    private void OnDestroy()
    {
        UnhookVerboseDamageTextToggle();
    }

    public void SetVerboseDamageText(bool value)
    {
        verboseDamageText = value;
        SyncVerboseDamageTextToggle();
    }

    public void HookVerboseDamageTextToggle()
    {
        if (verboseDamageTextToggle == null) return;

        verboseDamageTextToggle.onValueChanged.RemoveListener(SetVerboseDamageText);
        verboseDamageTextToggle.onValueChanged.AddListener(SetVerboseDamageText);
        SyncVerboseDamageTextToggle();
    }

    public void UnhookVerboseDamageTextToggle()
    {
        if (verboseDamageTextToggle == null) return;
        verboseDamageTextToggle.onValueChanged.RemoveListener(SetVerboseDamageText);
    }

    public void SetVerboseDamageTextToggle(Toggle toggle)
    {
        if (verboseDamageTextToggle == toggle)
        {
            HookVerboseDamageTextToggle();
            return;
        }

        UnhookVerboseDamageTextToggle();
        verboseDamageTextToggle = toggle;
        HookVerboseDamageTextToggle();
    }

    public void SyncVerboseDamageTextToggle()
    {
        if (verboseDamageTextToggle == null) return;
        verboseDamageTextToggle.SetIsOnWithoutNotify(verboseDamageText);
    }

    private void Prewarm()
    {
        int count = Mathf.Max(0, prewarmCount);
        for (int i =0; i < count; i++)
        {
            var go = CreateInstance();
            Release(go);
        }
    }

    private GameObject CreateInstance()
    {
        var go = Instantiate(textPrefab, transform);
        go.name = textPrefab.name + " (Pooled)";
        go.SetActive(false);

        var ctrl = go.GetComponent<DamageTextController>();
        if (ctrl == null) ctrl = go.AddComponent<DamageTextController>();
        ctrl.pool = this;

        return go;
    }

    private GameObject Get(Vector3 position, Quaternion rotation)
    {
        GameObject go = null;
        while (_available.Count >0 && go == null)
        {
            go = _available.Dequeue();
        }

        if (go == null)
        {
            if (!canGrow) return null;
            go = CreateInstance();
        }

        Transform parent = canvas != null ? canvas.transform : null;
        if (parent != null)
        {
            go.transform.SetParent(parent, worldPositionStays: false);
        }
        else
        {
            go.transform.SetParent(null, worldPositionStays: false);
        }

        go.transform.SetPositionAndRotation(position, rotation);
        go.SetActive(true);

        return go;
    }

    public void Release(GameObject go)
    {
        if (go == null) return;

        go.SetActive(false);
        go.transform.SetParent(transform, worldPositionStays: false);
        _available.Enqueue(go);
    }

    public GameObject SpawnPersistentText(Vector3 position, string text, Color textColor, float size)
    {
        if (textPrefab == null) return null;

        var go = Get(position, Quaternion.identity);
        if (go == null) return null;

        var ctrl = go.GetComponent<DamageTextController>();
        if (ctrl == null) ctrl = go.AddComponent<DamageTextController>();
        ctrl.pool = this;
        ctrl.ConfigureStatic(text, textColor, size, position);
        return go;
    }

    public void UpdatePersistentText(GameObject go, Vector3 position, string text, Color textColor, float size)
    {
        if (go == null) return;

        var ctrl = go.GetComponent<DamageTextController>();
        if (ctrl == null) ctrl = go.AddComponent<DamageTextController>();
        ctrl.pool = this;
        ctrl.ConfigureStatic(text, textColor, size, position);
    }

    public void ReleasePersistentText(GameObject go)
    {
        Release(go);
    }

    public void PlayFloatingText(Vector3 position, string s, Color textColor, float size, float lifetime=3)
    {
        if (textPrefab == null) return;
        var go = Get(position, Quaternion.identity);
        if (go == null) return;

        var ctrl = go.GetComponent<DamageTextController>();
        if (ctrl == null) ctrl = go.AddComponent<DamageTextController>();
        ctrl.pool = this;

        Vector2 direction = Vector2.up;
        float distance = 0.5f;

        if (smartPositioningEnabled && TryGetSmartDamageTextSlot(position, s, lifetime, out Vector3 slotPosition))
        {
            Vector3 toSlot = slotPosition - position;
            toSlot.z = 0f;

            if (toSlot.sqrMagnitude > 0.0001f)
            {
                direction = new Vector2(toSlot.x, toSlot.y).normalized;
                distance = toSlot.magnitude;
            }
            else
            {
                direction = Vector2.up;
                distance = 0.5f;
            }
        }

        ctrl.PlayDirectional(
            text: s,
            color: textColor,
            baseScale: size,
            surgeMult: scaleSurgeMultiplier,
            surgeDecay: surgeDecayTime,
            lifetime: lifetime,
            distance: distance,
            direction: direction
        );
    }

    public float GetDamageTextScalePreview(Transform context, float damageAmount)
    {
        float dmg = Mathf.Max(0.0001f, Mathf.Abs(damageAmount));
        float avg = GetCurrentDamageAverage(context, dmg);
        return ComputeDamageTextScaleFromAverage(dmg, avg);
    }

    public void PlayDamageText(Transform context, Vector3 position, float damageAmount, Color textColor, Tower source = null, Tower.CustomDamageData customDamageData = null, bool directional = true)
    {
        float dmg = Mathf.Max(0.0001f, Mathf.Abs(damageAmount));
        float scaleFactor = ComputeDamageTextScale(context, dmg);
        string displayText = BuildDamageTextDisplayText(damageAmount, customDamageData);
        Vector3 directionPosition = directional
            ? GetDamageTextDirectionPosition(context, position, source, customDamageData)
            : default;

        PlayDamageText(
            position,
            damageAmount,
            textColor,
            scaleFactor,
            directional,
            directionPosition,
            customDamageData != null ? customDamageData.crit : false,
            customDamageData != null ? customDamageData.critCount : 0,
            displayText);

        if (directional)
        {
            IncreaseDamageTextAngleRange(context);
        }
    }

    public void PlayDamageText(Vector3 position, float damageAmount, Color textColor, float scaleFactor, bool critical=false, int critCount = 0, string displayText = null)
    {
        PlayDamageText(position, damageAmount, textColor, scaleFactor, directional: false, directionPosition: default, critical, critCount, displayText);
    }

    public void PlayDamageText(Vector3 position, float damageAmount, Color textColor, float scaleFactor, bool directional, Vector3 directionPosition, bool critical = false, int critCount = 0, string displayText = null)
    {
        damageAmount = Mathf.Abs(damageAmount);
        if (textPrefab == null) return;

        // Don't show tiny hits and never display 0.
        if (damageAmount < 0.5f) return;

        var go = Get(position, Quaternion.identity);
        if (go == null) return;

        var ctrl = go.GetComponent<DamageTextController>();
        if (ctrl == null) ctrl = go.AddComponent<DamageTextController>();
        ctrl.pool = this;

        float baseScale = Mathf.Max(0.01f, scaleFactor);

        bool hasCustomDisplayText = !string.IsNullOrEmpty(displayText);
        int shown = Mathf.Max(1, Mathf.RoundToInt(damageAmount));
        string txt = hasCustomDisplayText ? displayText : shown.ToString();
        if (critical && !hasCustomDisplayText)
        {
            int exclamationCount = Mathf.Max(1, critCount);
            txt += new string('!', exclamationCount);
        }

        if (debugAppendRandomZerosToDamageText)
        {
            int maxZeros = Mathf.Max(0, debugMaxExtraTrailingZeros);
            int extraZeros = maxZeros > 0 ? Random.Range(0, maxZeros + 1) : 0;
            if (extraZeros > 0)
            {
                txt += new string('0', extraZeros);
            }
        }

        Vector2 dir;
        float distance;

        if (smartPositioningEnabled && TryGetSmartDamageTextSlot(position, txt, lifetime, out Vector3 slotPosition))
        {
            Vector3 toSlot = slotPosition - position;
            toSlot.z = 0f;

            if (toSlot.sqrMagnitude > 0.0001f)
            {
                dir = new Vector2(toSlot.x, toSlot.y).normalized;
                distance = toSlot.magnitude;
            }
            else
            {
                dir = Vector2.up;
                distance = arcDistance * baseScale;
            }
        }
        else if (directional)
        {
            Vector3 away3 = (position - directionPosition);
            away3.z =0f;
            dir = away3.sqrMagnitude >0.0001f ? new Vector2(away3.x, away3.y).normalized : Vector2.up;
            distance = arcDistance * baseScale * 2f;
        }
        else
        {
            // Choose a small arc direction with jitter.
            dir = Random.insideUnitCircle.normalized;
            if (dir.sqrMagnitude <0.0001f) dir = Vector2.up;
            dir.x += Random.Range(-randomHorizontalJitter, randomHorizontalJitter);
            dir = dir.sqrMagnitude >0.0001f ? dir.normalized : Vector2.up;
            distance = arcDistance * baseScale;
        }

        if (directional)
        {
            ctrl.PlayDirectional(
                text: txt,
                color: textColor,
                baseScale: baseScale,
                surgeMult: scaleSurgeMultiplier,
                surgeDecay: surgeDecayTime,
                lifetime: lifetime,
                distance: distance,
                direction: dir
            );
        }
        else
        {
            ctrl.Play(
                text: txt,
                color: textColor,
                baseScale: baseScale,
                surgeMult: scaleSurgeMultiplier,
                surgeDecay: surgeDecayTime,
                lifetime: lifetime,
                arcDistance: distance,
                arcHeight: arcHeight,
                direction: dir
            );
        }
    }

    private float ComputeDamageTextScale(Transform context, float damageAmount)
    {
        float dmg = Mathf.Max(0.0001f, Mathf.Abs(damageAmount));
        float avg = PushDamageAndGetAverage(context, dmg);
        return ComputeDamageTextScaleFromAverage(dmg, avg);
    }

    private float ComputeDamageTextScaleFromAverage(float dmg, float avg)
    {
        float scale = Mathf.Max(0.0001f, smartPositionCellSize);

        float ratio = dmg / Mathf.Max(0.0001f, avg);
        float t = ratio / (ratio + 1f);
        float lerpedScale = Mathf.Lerp(scale, scale, t);
        return Mathf.Clamp(lerpedScale, scale, scale);
    }

    private float PushDamageAndGetAverage(Transform context, float dmg)
    {
        DamageTextContextState state = GetDamageTextState(context);
        int window = Mathf.Max(1, damageTextScalingWindowCount);

        state.damageWindow.Enqueue(dmg);
        state.damageSum += dmg;

        while (state.damageWindow.Count > window)
        {
            state.damageSum -= state.damageWindow.Dequeue();
        }

        return state.damageWindow.Count > 0 ? (state.damageSum / state.damageWindow.Count) : dmg;
    }

    private float GetCurrentDamageAverage(Transform context, float defaultValue)
    {
        DamageTextContextState state = GetDamageTextState(context);
        return state.damageWindow.Count > 0 ? (state.damageSum / state.damageWindow.Count) : defaultValue;
    }

    private DamageTextContextState GetDamageTextState(Transform context)
    {
        int key = context != null ? context.GetInstanceID() : 0;
        if (!_damageTextStates.TryGetValue(key, out DamageTextContextState state))
        {
            state = new DamageTextContextState
            {
                lastAngleUpdateTime = Time.time
            };
            _damageTextStates[key] = state;
        }

        return state;
    }

    private void IncreaseDamageTextAngleRange(Transform context)
    {
        DamageTextContextState state = GetDamageTextState(context);
        DecayDamageTextAngleRange(state);
        state.angleRange += damageTextAngleRangeIncrease;
        state.lastAngleUpdateTime = Time.time;
    }

    private Vector3 GetDamageTextDirectionPosition(Transform context, Vector3 position, Tower source, Tower.CustomDamageData customDamageData)
    {
        Vector2 baseDirection;

        if (customDamageData != null && customDamageData.hitDirection.HasValue)
        {
            Vector3 hitDir = customDamageData.hitDirection.Value;
            hitDir.z = 0f;
            baseDirection = hitDir.sqrMagnitude > 0.0001f ? new Vector2(hitDir.x, hitDir.y).normalized : Vector2.up;
        }
        else if (source != null)
        {
            Vector3 away3 = position - source.transform.position;
            away3.z = 0f;
            baseDirection = away3.sqrMagnitude > 0.0001f ? new Vector2(away3.x, away3.y).normalized : Vector2.up;
        }
        else
        {
            baseDirection = Vector2.up;
        }

        DamageTextContextState state = GetDamageTextState(context);
        DecayDamageTextAngleRange(state);
        float totalArc = state.angleRange + damageTextConstantArc;
        float angleOffset = totalArc > 0f ? Random.Range(-totalArc, totalArc) : 0f;
        Vector2 rotatedDirection = RotateVector(baseDirection, angleOffset);
        return position - new Vector3(rotatedDirection.x, rotatedDirection.y, 0f);
    }

    private void DecayDamageTextAngleRange(DamageTextContextState state)
    {
        float now = Time.time;
        float dt = Mathf.Max(0f, now - state.lastAngleUpdateTime);
        state.angleRange = Mathf.Max(0f, state.angleRange - (damageTextAngleRangeDecay * dt));
        state.lastAngleUpdateTime = now;
    }

    private string BuildDamageTextDisplayText(float amount, Tower.CustomDamageData customDamageData)
    {
        bool hasVerboseBreakdown = verboseDamageText
            && customDamageData != null
            && customDamageData.hasFinalDamageBreakdown;

        if (!hasVerboseBreakdown)
        {
            return null;
        }

        float damageAmount = Mathf.Abs(amount);
        float trackedDamage = customDamageData.GetFinalDamageAmount();
        float tolerance = Mathf.Max(0.05f, damageAmount * 0.01f);
        if (Mathf.Abs(trackedDamage - damageAmount) > tolerance)
        {
            return null;
        }

        if (Mathf.Abs(customDamageData.finalMultiplier - 1f) <= 0.01f)
        {
            return FormatColoredText(ResolveVerboseDamageColorType(customDamageData), FormatDamageTextValue(customDamageData.finalBaseDamage));
        }

        if (Mathf.RoundToInt(customDamageData.finalMultiplier) == 1)
        {
            return null;
        }

        string baseDamageText = FormatColoredText(CM.ColorType.Blue, FormatDamageTextValue(customDamageData.finalBaseDamage));
        string multiplyText = FormatColoredText(CM.ColorType.White, "x");
        string multiplierText = FormatColoredText(CM.ColorType.Red, FormatDamageTextValue(customDamageData.finalMultiplier));
        string equalsText = FormatColoredText(CM.ColorType.White, "=");
        string finalDamageText = FormatColoredText(ResolveVerboseDamageColorType(customDamageData), FormatDamageTextValue(trackedDamage));

        return $"{baseDamageText}{multiplyText}{multiplierText}{equalsText}{finalDamageText}";
    }

    private static string FormatDamageTextValue(float value)
    {
        return Mathf.RoundToInt(value).ToString();
    }

    private static string FormatColoredText(CM.ColorType colorType, string text)
    {
        if (CM.i == null) return text;
        return CM.i.RTC(colorType, text);
    }

    private static CM.ColorType ResolveVerboseDamageColorType(Tower.CustomDamageData customDamageData)
    {
        if (customDamageData == null || customDamageData.damageType == CM.ColorType.None)
        {
            return CM.ColorType.White;
        }

        return customDamageData.damageType;
    }

    private static Vector2 RotateVector(Vector2 vector, float angleDegrees)
    {
        float radians = angleDegrees * Mathf.Deg2Rad;
        float sin = Mathf.Sin(radians);
        float cos = Mathf.Cos(radians);
        return new Vector2(
            (vector.x * cos) - (vector.y * sin),
            (vector.x * sin) + (vector.y * cos)
        ).normalized;
    }

    private bool TryGetSmartDamageTextSlot(Vector3 origin, string text, float textLifetime, out Vector3 slotPosition)
    {
        slotPosition = origin;

        float cellSize = Mathf.Max(0.05f, smartPositionCellSize);
        int widthCells = ComputeSmartWidthCells(text);
        float now = Time.time;
        PruneExpiredSmartCells(now);

        Vector2Int centerCell = WorldToSmartCell(origin, cellSize);

        var ring1 = new List<Vector2Int>(8);
        var ring2 = new List<Vector2Int>(16);
        AddRingCells(centerCell, 1, ring1);
        AddRingCells(centerCell, 2, ring2);

        if (TryReserveFromCandidates(ring1, widthCells, now, textLifetime, cellSize, out slotPosition))
            return true;

        if (TryReserveFromCandidates(ring2, widthCells, now, textLifetime, cellSize, out slotPosition))
            return true;

        var overlapCandidates = new List<Vector2Int>(ring1.Count + ring2.Count + 1);
        overlapCandidates.AddRange(ring1);
        overlapCandidates.AddRange(ring2);
        overlapCandidates.Add(centerCell);

        if (TryReserveLeastBusy(overlapCandidates, widthCells, now, textLifetime, cellSize, out slotPosition))
            return true;

        return false;
    }

    private static int ComputeSmartWidthCells(string text)
    {
        int length = string.IsNullOrEmpty(text) ? 1 : text.Length;
        return Mathf.Max(1, Mathf.CeilToInt(length / 4f));
    }

    private void PruneExpiredSmartCells(float now)
    {
        if (_smartCellOccupiedUntil.Count == 0) return;

        var keys = new List<Vector2Int>(_smartCellOccupiedUntil.Keys);
        for (int i = 0; i < keys.Count; i++)
        {
            var k = keys[i];
            if (_smartCellOccupiedUntil.TryGetValue(k, out float occupiedUntil) && occupiedUntil <= now)
            {
                _smartCellOccupiedUntil.Remove(k);
            }
        }
    }

    private static Vector2Int WorldToSmartCell(Vector3 world, float cellSize)
    {
        float size = Mathf.Max(0.05f, cellSize);
        float q = ((Mathf.Sqrt(3f) / 3f) * world.x - (1f / 3f) * world.y) / size;
        float r = ((2f / 3f) * world.y) / size;
        return RoundAxialHex(q, r);
    }

    private static Vector3 SmartCellToWorldCenter(Vector2Int cell, float cellSize)
    {
        float size = Mathf.Max(0.05f, cellSize);
        float x = size * Mathf.Sqrt(3f) * (cell.x + (cell.y * 0.5f));
        float y = size * 1.5f * cell.y;
        return new Vector3(x, y, 0f);
    }

    private static void AddRingCells(Vector2Int center, int ring, List<Vector2Int> results)
    {
        if (results == null) return;
        int r = Mathf.Max(1, ring);

        Vector2Int current = center + (s_hexDirections[4] * r);
        for (int side = 0; side < s_hexDirections.Length; side++)
        {
            Vector2Int step = s_hexDirections[side];
            for (int i = 0; i < r; i++)
            {
                results.Add(current);
                current += step;
            }
        }
    }

    private static Vector2Int RoundAxialHex(float q, float r)
    {
        float x = q;
        float z = r;
        float y = -x - z;

        int rx = Mathf.RoundToInt(x);
        int ry = Mathf.RoundToInt(y);
        int rz = Mathf.RoundToInt(z);

        float dx = Mathf.Abs(rx - x);
        float dy = Mathf.Abs(ry - y);
        float dz = Mathf.Abs(rz - z);

        if (dx > dy && dx > dz)
        {
            rx = -ry - rz;
        }
        else if (dy > dz)
        {
            ry = -rx - rz;
        }
        else
        {
            rz = -rx - ry;
        }

        return new Vector2Int(rx, rz);
    }

    private bool TryReserveFromCandidates(List<Vector2Int> candidates, int widthCells, float now, float textLifetime, float cellSize, out Vector3 slotPosition)
    {
        slotPosition = default;
        if (candidates == null || candidates.Count == 0) return false;

        int start = Random.Range(0, candidates.Count);
        for (int i = 0; i < candidates.Count; i++)
        {
            var cell = candidates[(start + i) % candidates.Count];
            if (!CanReserveCellBlock(cell, widthCells, now))
            {
                continue;
            }

            ReserveCellBlock(cell, widthCells, now + Mathf.Max(0.05f, textLifetime));
            slotPosition = SmartCellToWorldCenter(cell, cellSize);
            return true;
        }

        return false;
    }

    private bool TryReserveLeastBusy(List<Vector2Int> candidates, int widthCells, float now, float textLifetime, float cellSize, out Vector3 slotPosition)
    {
        slotPosition = default;
        if (candidates == null || candidates.Count == 0) return false;

        Vector2Int bestCell = candidates[0];
        float bestOccupiedUntil = float.PositiveInfinity;

        for (int i = 0; i < candidates.Count; i++)
        {
            var cell = candidates[i];
            float occupiedUntil = GetCellBlockOccupiedUntil(cell, widthCells, now);

            if (occupiedUntil < bestOccupiedUntil)
            {
                bestOccupiedUntil = occupiedUntil;
                bestCell = cell;
            }
        }

        ReserveCellBlock(bestCell, widthCells, now + Mathf.Max(0.05f, textLifetime));
        slotPosition = SmartCellToWorldCenter(bestCell, cellSize);
        return true;
    }

    private bool CanReserveCellBlock(Vector2Int centerCell, int widthCells, float now)
    {
        int left = (widthCells - 1) / 2;
        int right = widthCells - 1 - left;
        for (int x = -left; x <= right; x++)
        {
            var cell = new Vector2Int(centerCell.x + x, centerCell.y);
            if (_smartCellOccupiedUntil.TryGetValue(cell, out float occupiedUntil) && occupiedUntil > now)
            {
                return false;
            }
        }

        return true;
    }

    private void ReserveCellBlock(Vector2Int centerCell, int widthCells, float occupiedUntil)
    {
        int left = (widthCells - 1) / 2;
        int right = widthCells - 1 - left;
        for (int x = -left; x <= right; x++)
        {
            var cell = new Vector2Int(centerCell.x + x, centerCell.y);
            _smartCellOccupiedUntil[cell] = occupiedUntil;
        }
    }

    private float GetCellBlockOccupiedUntil(Vector2Int centerCell, int widthCells, float now)
    {
        float occupiedUntil = now;
        int left = (widthCells - 1) / 2;
        int right = widthCells - 1 - left;
        for (int x = -left; x <= right; x++)
        {
            var cell = new Vector2Int(centerCell.x + x, centerCell.y);
            if (_smartCellOccupiedUntil.TryGetValue(cell, out float existing))
            {
                if (existing > occupiedUntil) occupiedUntil = existing;
            }
        }

        return occupiedUntil;
    }

    private class DamageTextController : MonoBehaviour
    {
        [HideInInspector] public TextObjectPool pool;

        private SpriteRenderer _sprite;
        private TMPro.TMP_Text _tmp;

        private Coroutine _routine;

        private void Awake()
        {
            // Support either TMP text or sprite renderer (fallback).
            _tmp = GetComponentInChildren<TMPro.TMP_Text>();
            _sprite = GetComponentInChildren<SpriteRenderer>();
        }

        public void Play(string text, Color color, float baseScale, float surgeMult, float surgeDecay, float lifetime, float arcDistance, float arcHeight, Vector2 direction)
        {
            if (_routine != null) StopCoroutine(_routine);

            if (_tmp != null)
            {
                _tmp.text = text;
                _tmp.color = color;
            }
            else if (_sprite != null)
            {
                _sprite.color = color;
            }

            transform.localScale = Vector3.one * (baseScale * Mathf.Max(0.01f, surgeMult));

            _routine = StartCoroutine(AnimateArc(baseScale, surgeDecay, lifetime, arcDistance, arcHeight, direction));
        }

        public void PlayDirectional(string text, Color color, float baseScale, float surgeMult, float surgeDecay, float lifetime, float distance, Vector2 direction)
        {
            if (_routine != null) StopCoroutine(_routine);

            if (_tmp != null)
            {
                _tmp.text = text;
                _tmp.color = color;
            }
            else if (_sprite != null)
            {
                _sprite.color = color;
            }

            transform.localScale = Vector3.one * (baseScale * Mathf.Max(0.01f, surgeMult));

            _routine = StartCoroutine(AnimateDirectional(baseScale, surgeDecay, lifetime, distance, direction));
        }

        public void ConfigureStatic(string text, Color color, float baseScale, Vector3 worldPosition)
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }

            transform.position = worldPosition;
            transform.localScale = Vector3.one * Mathf.Max(0.01f, baseScale);

            if (_tmp != null)
            {
                _tmp.text = text;
                _tmp.color = color;
            }
            else if (_sprite != null)
            {
                _sprite.color = color;
            }
        }

        private IEnumerator AnimateArc(float baseScale, float surgeDecay, float lifetime, float arcDistance, float arcHeight, Vector2 direction)
        {
            Vector3 start = transform.position;
            Vector3 end = start + new Vector3(direction.x, direction.y,0f) * arcDistance;

            float t =0f;
            float dur = Mathf.Max(0.05f, lifetime);
            float surgeDur = Mathf.Max(0.01f, surgeDecay);

            Color c0 = Color.white;
            if (_tmp != null) c0 = _tmp.color;
            else if (_sprite != null) c0 = _sprite.color;

            while (t < dur)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / dur);

                // Parabolic arc in XY.
                Vector3 p = Vector3.Lerp(start, end, u);
                float h =4f * u * (1f - u); //0..1..0
                p.y += h * arcHeight;
                transform.position = p;

                ApplyScaleAndFade(baseScale, surgeDur, dur, t, u, c0);
                yield return null;
            }

            ReturnToPool();
        }

        private IEnumerator AnimateDirectional(float baseScale, float surgeDecay, float lifetime, float distance, Vector2 direction)
        {
            Vector3 start = transform.position;
            Vector3 end = start + new Vector3(direction.x, direction.y,0f) * distance;

            float t =0f;
            float dur = Mathf.Max(0.05f, lifetime);
            float surgeDur = Mathf.Max(0.01f, surgeDecay);

            Color c0 = Color.white;
            if (_tmp != null) c0 = _tmp.color;
            else if (_sprite != null) c0 = _sprite.color;

            while (t < dur)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / dur);

                // Impulse then decelerate: ease-out.
                float eased =1f - Mathf.Pow(1f - u,3f);
                transform.position = Vector3.Lerp(start, end, eased);

                ApplyScaleAndFade(baseScale, surgeDur, dur, t, u, c0);
                yield return null;
            }

            ReturnToPool();
        }

        private void ApplyScaleAndFade(float baseScale, float surgeDur, float dur, float t, float u, Color c0)
        {
            // Scale surge decay.
            float su = Mathf.Clamp01(t / surgeDur);
            float surge = Mathf.Lerp(1.15f,1f, su);
            transform.localScale = Vector3.one * (baseScale * surge);

            // Fade out near the end.
            float fade =1f;
            if (u >0.6f) fade = Mathf.InverseLerp(1f,0.6f, u);

            if (_tmp != null)
            {
                var c = c0;
                c.a = c0.a * fade;
                _tmp.color = c;
            }
            else if (_sprite != null)
            {
                var c = c0;
                c.a = c0.a * fade;
                _sprite.color = c;
            }
        }

        private void ReturnToPool()
        {
            // Return to pool.
            if (pool != null) pool.Release(gameObject);
            else Destroy(gameObject);
        }
    }
}
