using UnityEngine;

public class EnemyEffect : MonoBehaviour
{
    public enum TriggerCondition
    {
        DamageAboveThreshold,
        HealthBelowThreshold,
        Interval,
    }

    [SerializeField] private TriggerCondition triggerCondition;
    public float triggerValue;
    public bool unlimited = true;
    [Min(1)] public int triggerLimit = 1;
    [Min(0.01f)] public float intervalSeconds = 1f;

    [Tooltip("Health percentages (0-1) at which the effect triggers. Each threshold fires only once, even if the enemy is healed above it.")]
    public System.Collections.Generic.List<float> healthThresholds = new System.Collections.Generic.List<float>();

    private int triggerCount;
    private float _nextIntervalTriggerTime;
    // Tracks which healthThresholds indices have already fired (prevents re-trigger on heal).
    private readonly System.Collections.Generic.HashSet<int> _firedThresholds = new System.Collections.Generic.HashSet<int>();
    protected Enemy enemy;

    public virtual TriggerCondition Condition => triggerCondition;
    protected Enemy Enemy => enemy;

    protected virtual void Awake()
    {
        BindEnemy();
    }

    protected virtual void OnEnable()
    {
        BindEnemy();
        triggerCount = 0;
        _firedThresholds.Clear();
        _nextIntervalTriggerTime = Time.time + Mathf.Max(0.01f, intervalSeconds);
    }

    private void Update()
    {
        if (!isActiveAndEnabled) return;
        if (Condition != TriggerCondition.Interval) return;
        if (Time.time < _nextIntervalTriggerTime) return;

        // Keep cadence even if triggering fails due to limits.
        _nextIntervalTriggerTime = Time.time + Mathf.Max(0.01f, intervalSeconds);
        TryTrigger(triggerValue);
    }

    public void BindEnemy()
    {
        if (enemy != null) return;
        enemy = GetComponent<Enemy>();
        if (enemy == null)
        {
            enemy = GetComponentInParent<Enemy>();
        }
    }

    public bool TryTrigger(float eventValue)
    {
        if (!isActiveAndEnabled) return false;

        // HealthBelowThreshold uses the threshold list instead of triggerLimit.
        if (Condition == TriggerCondition.HealthBelowThreshold)
        {
            bool triggered = false;
            for (int i = 0; i < healthThresholds.Count; i++)
            {
                if (_firedThresholds.Contains(i)) continue;
                if (eventValue <= healthThresholds[i])
                {
                    _firedThresholds.Add(i);
                    Trigger();
                    triggered = true;
                }
            }
            return triggered;
        }

        if (!unlimited && triggerCount >= Mathf.Max(1, triggerLimit)) return false;
        if (!ShouldTrigger(eventValue)) return false;

        triggerCount++;
        Trigger();
        return true;
    }

    protected virtual bool ShouldTrigger(float eventValue)
    {
        switch (Condition)
        {
            case TriggerCondition.DamageAboveThreshold:
                return eventValue >= triggerValue;
            case TriggerCondition.HealthBelowThreshold:
                return eventValue <= triggerValue;
            case TriggerCondition.Interval:
                return true;
            default:
                return false;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        triggerLimit = Mathf.Max(1, triggerLimit);
        intervalSeconds = Mathf.Max(0.01f, intervalSeconds);
    }
#endif

    public virtual void Trigger()
    {
    }
}

#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(EnemyEffect), true)]
[UnityEditor.CanEditMultipleObjects]
public class EnemyEffectEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        UnityEditor.SerializedProperty triggerCondition = serializedObject.FindProperty("triggerCondition");
        UnityEditor.SerializedProperty triggerValue = serializedObject.FindProperty("triggerValue");
        UnityEditor.SerializedProperty unlimited = serializedObject.FindProperty("unlimited");
        UnityEditor.SerializedProperty triggerLimit = serializedObject.FindProperty("triggerLimit");
        UnityEditor.SerializedProperty intervalSeconds = serializedObject.FindProperty("intervalSeconds");

        if (triggerCondition != null)
        {
            UnityEditor.EditorGUILayout.PropertyField(triggerCondition);
        }

        EnemyEffect.TriggerCondition mode = EnemyEffect.TriggerCondition.DamageAboveThreshold;
        if (triggerCondition != null)
        {
            mode = (EnemyEffect.TriggerCondition)triggerCondition.enumValueIndex;
        }

        if (mode == EnemyEffect.TriggerCondition.Interval)
        {
            if (intervalSeconds != null)
            {
                UnityEditor.EditorGUILayout.PropertyField(intervalSeconds);
            }
        }
        else if (mode == EnemyEffect.TriggerCondition.HealthBelowThreshold)
        {
            UnityEditor.SerializedProperty healthThresholds = serializedObject.FindProperty("healthThresholds");
            if (healthThresholds != null)
            {
                UnityEditor.EditorGUILayout.PropertyField(healthThresholds, true);
            }
        }
        else
        {
            if (triggerValue != null)
            {
                UnityEditor.EditorGUILayout.PropertyField(triggerValue);
            }
        }

        if (mode != EnemyEffect.TriggerCondition.HealthBelowThreshold && unlimited != null)
        {
            UnityEditor.EditorGUILayout.PropertyField(unlimited);
            if (!unlimited.boolValue && triggerLimit != null)
            {
                UnityEditor.EditorGUILayout.PropertyField(triggerLimit);
            }
        }

        DrawPropertiesExcluding(serializedObject,
            "m_Script",
            "triggerCondition",
            "triggerValue",
            "unlimited",
            "triggerLimit",
            "intervalSeconds",
            "healthThresholds");

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
