using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CircleCollider2D))]
public class RangeManager : MonoBehaviour
{
    [System.Serializable]
	private struct RangeVisualizationState
	{
		[Min(0f)] public float meshAlpha;
		[Min(0f)] public float lineWidth;
		[Min(0f)] public float lineAlpha;

		public static RangeVisualizationState Lerp(RangeVisualizationState a, RangeVisualizationState b, float t)
		{
			return new RangeVisualizationState
			{
				meshAlpha = Mathf.Lerp(a.meshAlpha, b.meshAlpha, t),
				lineWidth = Mathf.Lerp(a.lineWidth, b.lineWidth, t),
				lineAlpha = Mathf.Lerp(a.lineAlpha, b.lineAlpha, t),
			};
		}
	}

    public enum TargettingMode
    {
        Nearest,
        Farthest,
        LowestHealth,
        HighestHealth,
        Marked,
        Burning,
        Fastest,
        Slowest,
		Manual
    }

    [System.Flags]
    public enum TargettingModeFlags
    {
        Nearest     = 1 << 0,
        Farthest    = 1 << 1,
        LowestHealth  = 1 << 2,
        HighestHealth = 1 << 3,
        Marked      = 1 << 4,
        Burning     = 1 << 5,
        Fastest     = 1 << 6,
        Slowest     = 1 << 7,
        Manual      = 1 << 8,
        Everything  = ~0
    }

    [SerializeField] private TargettingMode targettingMode = TargettingMode.Nearest;
	[SerializeField] private TargettingModeFlags availableTargettingModes = TargettingModeFlags.Everything;
	[SerializeField] private bool useLOS = false;
	[SerializeField] private bool automaticMarkTargetting = false;
	[SerializeField] private bool showRangeEnabled = true;
	[SerializeField] private bool debugDrawLOS = false;
 [SerializeField] private bool enableMazeEdgeRangeVisualizerPruning = false;
	[SerializeField] private bool pruneLos = false;
	[Min(0f)]
	[SerializeField] private float pruneLosThreshold = 0f;

	private bool singleRayMode = false;
	[Header("Single Ray Mode")]
	[SerializeField] private bool singleRayBlocksOnWalls = true;
	[Min(0.001f)]
	[SerializeField] private float singleRayHalfWidth = 0.25f;
	[SerializeField] private float singleRayDirectionDegrees = 0f;

	// Cached single ray values (computed when LOS data is built)
	private Vector2 _singleRayDirWorld = Vector2.right;
	private float _singleRayMaxDist = 0f;
	private bool _singleRayDataValid;

    private LineRenderer lr;
    public HashSet<Enemy> enemiesInRange = new HashSet<Enemy>();

    // Towers whose colliders overlap this range.
    private readonly HashSet<Tower> _towersInRange = new HashSet<Tower>();
    private string rangeVisualizerMaterialColorString = "_Color";

    private Enemy _targetted;
    public CircleCollider2D _collider;

    private Enemy _registeredTarget;

	private Tower _attachedTower;

    [Header("Visualization")]
    [Min(12)]
    [SerializeField] private int circleSegments = 48;
    public float losOffset = .5f;
    public int losSegments = 60;
    public float losSegmentLength = 0.5f;
    public float hitDistanceFudge = .2f;
    public float rangeDoubleThreshold;

	[Header("Visualization States")]
	[SerializeField] private RangeVisualizationState noEnemiesInRangeVisualization = new RangeVisualizationState
	{
		meshAlpha = 0.08f,
		lineWidth = 0.1f,
		lineAlpha = 0.1f,
	};
	[SerializeField] private RangeVisualizationState enemiesInRangeVisualization = new RangeVisualizationState
	{
		meshAlpha = 0.16f,
		lineWidth = 0.14f,
		lineAlpha = 0.2f,
	};
	[SerializeField, Min(0f)] private float visualizationStateLerpSpeed = 10f;

    private Mesh _losMesh;
    private MeshRenderer mr;
	private bool _debugDrawLosPrev;
	private float[] _pruneScratch;
	private float[] _pruneSeverityScratch;
	private RangeVisualizationState _currentVisualizationState;
	private Color _rangeVisualizerBaseColor = Color.white;

	public TargettingModeFlags GetAvailableTargettingModes() => availableTargettingModes;

	public bool IsModeAvailable(TargettingMode mode)
	{
		var flag = (TargettingModeFlags)(1 << (int)mode);
		return (availableTargettingModes & flag) != 0;
	}

	public TargettingMode GetTargettingMode()
	{
		if (!IsModeAvailable(targettingMode))
			return TargettingMode.Nearest;
		return targettingMode;
	}

	private void HideMeshVisualization()
	{
		var mf = GetComponent<MeshFilter>();
		if (mf != null) mf.sharedMesh = null;
	}

	public void SetLOS(bool b)
	{
		useLOS = b;
		MeshVisualization();
	}

	public bool GetLos()
	{
		return useLOS;
	}

	public bool IsShowRangeEnabled()
	{
		return showRangeEnabled;
	}
	
	public bool IsAutomaticMarkTargettingEnabled()
	{
		return automaticMarkTargetting;
	}

	public void SetAutomaticMarkTargetting(bool enabled)
	{
		if (automaticMarkTargetting == enabled) return;
		automaticMarkTargetting = enabled;
		ForceRetarget();
	}
	
	public bool IsSingleRay()
    {
		return singleRayMode;
    }

	public void EnableSingleRayMode()
	{
		singleRayMode = true;
		useLOS = true;
		_losDataValid = false;
		_singleRayDataValid = false;
		MeshVisualization();
		ForceRetarget();
	}
	
	public void DisableSingleRayMode()
    {
		singleRayMode = false;
		_singleRayDataValid = false;
		// keep useLOS as-is; singleRayMode is an additional constraint when enabled
		_losDataValid = false;
		MeshVisualization();
		ForceRetarget();
    }

	/// <summary>
	/// Sets the single-ray direction in world-space degrees (0 = +X, 90 = +Y).
	/// </summary>
	public void SetSingleRayDirectionDegrees(float degrees)
	{
		singleRayDirectionDegrees = degrees;
		_singleRayDataValid = false;
		_losDataValid = false;
		if (singleRayMode) ForceRetarget();
		if (showRangeEnabled) MeshVisualization();
	}

	/// <summary>
	/// Adds to the current single-ray direction (world-space degrees).
	/// </summary>
	public void RotateSingleRay(float deltaDegrees)
	{
		SetSingleRayDirectionDegrees(singleRayDirectionDegrees + deltaDegrees);
	}

	public Vector2 GetSingleRayDirectionWorld()
	{
		EnsureSingleRayData();
		return _singleRayDirWorld;
	}

	private float GetEffectiveLosOffset(float radius)
	{
		float offset = Mathf.Clamp(losOffset, 0f, radius);

		GridManager gm = GridManager.instance;
		if (gm != null)
		{
			float gridWidthOffset = Mathf.Max(0f, gm.GetSpacing() * 0.5f + hitDistanceFudge);
			offset = Mathf.Clamp(gridWidthOffset, 0f, radius);
		}

		return offset;
	}

	private void EnsureSingleRayData()
	{
		if (_singleRayDataValid) return;
		if (_collider == null) _collider = GetComponent<CircleCollider2D>();
		if (_collider == null) return;

		float radius = Mathf.Max(0f, _collider.radius);
      float offset = GetEffectiveLosOffset(radius);
		float maxRayDist = Mathf.Max(0f, radius - offset);

		Vector2 dirLocal = Quaternion.Euler(0f, 0f, singleRayDirectionDegrees) * Vector2.right;
		Vector2 dirWorld = (Vector2)transform.TransformDirection(dirLocal).normalized;
		_singleRayDirWorld = dirWorld;

		float hitDistFromStart = maxRayDist;
		if (singleRayBlocksOnWalls && LayerMaskManager.instance != null)
		{
			Vector3 centerWorld = transform.position;
			Vector2 startWorld = (Vector2)centerWorld + dirWorld * offset;
			int hitCount = Physics2D.RaycastNonAlloc(startWorld, dirWorld, s_losHits, maxRayDist, LayerMaskManager.instance.wallLayerMask);
			for (int j = 0; j < hitCount; j++)
			{
				if (s_losHits[j].collider == null) continue;
				if (s_losHits[j].distance < hitDistFromStart)
					hitDistFromStart = Mathf.Min(maxRayDist, s_losHits[j].distance + hitDistanceFudge);
			}
		}

		_singleRayMaxDist = offset + hitDistFromStart;
		_singleRayDataValid = true;
	}

	private bool IsEnemyInsideSingleRay(Enemy e)
	{
		if (e == null) return false;
		if (_collider == null) _collider = GetComponent<CircleCollider2D>();
		if (_collider == null) return false;

		EnsureSingleRayData();
		if (!_singleRayDataValid) return false;

		Vector2 center = transform.position;
		Vector2 toEnemy = (Vector2)e.transform.position - center;
		float along = Vector2.Dot(toEnemy, _singleRayDirWorld);
		if (along < 0f) return false;
		if (along > _singleRayMaxDist) return false;

		float perp = Mathf.Abs(toEnemy.x * _singleRayDirWorld.y - toEnemy.y * _singleRayDirWorld.x);
		return perp <= Mathf.Max(0.001f, singleRayHalfWidth);
	}

	private bool IsPointInsideSingleRay(Vector3 point)
	{
		if (_collider == null) _collider = GetComponent<CircleCollider2D>();
		if (_collider == null) return false;

		EnsureSingleRayData();
		if (!_singleRayDataValid) return false;

		Vector2 center = transform.position;
		Vector2 toPoint = (Vector2)point - center;
		float along = Vector2.Dot(toPoint, _singleRayDirWorld);
		if (along < 0f) return false;
		if (along > _singleRayMaxDist) return false;

		float perp = Mathf.Abs(toPoint.x * _singleRayDirWorld.y - toPoint.y * _singleRayDirWorld.x);
		return perp <= Mathf.Max(0.001f, singleRayHalfWidth);
	}

	private int CircularRaycastDistances(float radius, float offset, float[] endDists, Vector3[] dirsLocal = null)
	{
		if (LayerMaskManager.instance == null) return 0;
		if (endDists == null) return 0;

		int baseRays = Mathf.Max(3, circleSegments);
		int maxRays = endDists.Length;
		if (dirsLocal != null) maxRays = Mathf.Min(maxRays, dirsLocal.Length);
		if (maxRays < 3) return 0;

       offset = GetEffectiveLosOffset(radius);
		float maxRayDist = Mathf.Max(0f, radius - offset);
		Vector3 centerWorld = transform.position;

		// If in single-ray mode, only cast one direction and fill the rest with zeros.
		if (singleRayMode)
		{
			Vector3 dirLocal = Quaternion.Euler(0f, 0f, singleRayDirectionDegrees) * Vector3.right;
			dirLocal.z = 0f;
			Vector3 dirWorld = transform.TransformDirection(dirLocal).normalized;
			Vector3 startWorld = centerWorld + dirWorld * offset;

			float hitDistFromStart = maxRayDist;
			if (singleRayBlocksOnWalls)
			{
				int hitCount = Physics2D.RaycastNonAlloc(startWorld, dirWorld, s_losHits, maxRayDist, LayerMaskManager.instance.wallLayerMask);
				for (int j = 0; j < hitCount; j++)
				{
					if (s_losHits[j].collider == null) continue;
					if (s_losHits[j].distance < hitDistFromStart)
						hitDistFromStart = Mathf.Min(maxRayDist, s_losHits[j].distance + hitDistanceFudge);
				}
			}

			float dist = offset + hitDistFromStart;

			int raysToUse = Mathf.Clamp(baseRays, 3, maxRays);
			for (int i = 0; i < raysToUse; i++)
			{
				endDists[i] = 0f;
				if (dirsLocal != null)
				{
					float angleDeg = (i / (float)raysToUse) * 360f;
					dirsLocal[i] = Quaternion.Euler(0f, 0f, angleDeg) * Vector3.right;
				}
			}

			// Put the ray distance into the closest angular bin so mesh draws a visible "spoke".
			float ang = singleRayDirectionDegrees;
			ang %= 360f;
			if (ang < 0f) ang += 360f;
			int idx = Mathf.RoundToInt((ang / 360f) * raysToUse) % raysToUse;
			endDists[idx] = dist;

			// Keep cached data in sync for runtime queries.
			_singleRayDirWorld = (Vector2)dirWorld;
			_singleRayMaxDist = dist;
			_singleRayDataValid = true;

			return raysToUse;
		}

		bool RunPass(int raysToUse, bool stopOnThreshold)
		{
			float angleStep = 360f / raysToUse;
			float angleSum = 0f;
			int i = 0;
			bool thresholdHit = false;

			while (angleSum < 360f && i < raysToUse)
			{
				float angleDeg = angleSum;
				Vector3 dirLocal = Quaternion.Euler(0f, 0f, angleDeg) * Vector3.right;
				dirLocal.z = 0f;
				if (dirsLocal != null) dirsLocal[i] = dirLocal;

				Vector3 dirWorld = transform.TransformDirection(dirLocal).normalized;
				Vector3 startWorld = centerWorld + dirWorld * offset;

				float hitDistFromStart = maxRayDist;
				int hitCount = Physics2D.RaycastNonAlloc(startWorld, dirWorld, s_losHits, maxRayDist, LayerMaskManager.instance.wallLayerMask);
				for (int j = 0; j < hitCount; j++)
				{
					if (s_losHits[j].collider == null) continue;
					if (s_losHits[j].distance < hitDistFromStart)
						hitDistFromStart = Mathf.Min(maxRayDist, s_losHits[j].distance + hitDistanceFudge);
				}

				endDists[i] = offset + hitDistFromStart;
				if (rangeDoubleThreshold > 0f && hitDistFromStart > rangeDoubleThreshold)
				{
					thresholdHit = true;
					if (stopOnThreshold) return true;
				}

				angleSum += angleStep;
				i++;
			}

			return thresholdHit;
		}

		void PrunePass(int raysToUse)
		{
          if (!enableMazeEdgeRangeVisualizerPruning) return;
			if (!pruneLos) return;
			if (raysToUse < 3) return;
			if (endDists == null || endDists.Length < raysToUse) return;

			if (_pruneScratch == null || _pruneScratch.Length < raysToUse)
				_pruneScratch = new float[raysToUse];
			if (_pruneSeverityScratch == null || _pruneSeverityScratch.Length < raysToUse)
				_pruneSeverityScratch = new float[raysToUse];

			System.Array.Copy(endDists, _pruneScratch, raysToUse);
			System.Array.Clear(_pruneSeverityScratch, 0, raysToUse);

			float threshold = pruneLosThreshold > 0f ? pruneLosThreshold : Mathf.Max(0.05f, radius * 0.2f);
			for (int i = 0; i < raysToUse; i++)
			{
				float prev = _pruneScratch[(i - 1 + raysToUse) % raysToUse];
				float cur = _pruneScratch[i];
				float next = _pruneScratch[(i + 1) % raysToUse];

				bool spikeHigh = (cur - prev) > threshold && (cur - next) > threshold;
				bool spikeLow = (prev - cur) > threshold && (next - cur) > threshold;
				if (!spikeHigh && !spikeLow) continue;

				_pruneSeverityScratch[i] = Mathf.Min(Mathf.Abs(cur - prev), Mathf.Abs(cur - next));
			}

			int bestStart = -1;
			int bestLen = 0;
			float bestScore = 0f;

			for (int start = 0; start < raysToUse; start++)
			{
				for (int len = 1; len <= 3; len++)
				{
					float score = 0f;
					bool allSpikes = true;
					for (int k = 0; k < len; k++)
					{
						int idx = (start + k) % raysToUse;
						float sev = _pruneSeverityScratch[idx];
						if (sev <= 0f)
						{
							allSpikes = false;
							break;
						}
						score += sev;
					}

					if (!allSpikes) continue;
					if (score <= bestScore) continue;

					bestScore = score;
					bestStart = start;
					bestLen = len;
				}
			}

			if (bestStart < 0 || bestLen <= 0) return;
			for (int k = 0; k < bestLen; k++)
			{
				int idx = (bestStart + k) % raysToUse;
				float prev = _pruneScratch[(idx - 1 + raysToUse) % raysToUse];
				float next = _pruneScratch[(idx + 1) % raysToUse];
				endDists[idx] = (prev + next) * 0.5f;
			}
		}

		int rays = Mathf.Min(baseRays, maxRays);
		bool shouldDouble = rangeDoubleThreshold > 0f && rays * 2 <= maxRays;
		if (shouldDouble)
		{
			bool thresholdHit = RunPass(rays, stopOnThreshold: true);
			if (thresholdHit)
			{
				rays = Mathf.Min(rays * 2, maxRays);
				RunPass(rays, stopOnThreshold: false);
				PrunePass(rays);
			}
			else
			{
				RunPass(rays, stopOnThreshold: false);
				PrunePass(rays);
			}
		}
		else
		{
			RunPass(rays, stopOnThreshold: false);
			PrunePass(rays);
		}

		return rays;
	}


    // Store these when you compute the mesh
    private float[] _losEndDists;
    private int _losRayCount;
    private float _losRadius;
	private bool _losDataValid;

	private bool HasActiveMeshVisualization()
	{
		var mf = GetComponent<MeshFilter>();
		return mf != null && mf.sharedMesh != null;
	}

	private bool HasEnemiesInRange()
	{
		CleanupNulls();
		return enemiesInRange.Count > 0;
	}

	private void UpdateVisualizationState(bool instant)
	{
		RangeVisualizationState targetState = HasEnemiesInRange() ? enemiesInRangeVisualization : noEnemiesInRangeVisualization;
		if (instant)
		{
			_currentVisualizationState = targetState;
		}
		else
		{
			float t = 1f - Mathf.Exp(-Mathf.Max(0f, visualizationStateLerpSpeed) * Time.deltaTime);
			_currentVisualizationState = RangeVisualizationState.Lerp(_currentVisualizationState, targetState, t);
		}

		ApplyVisualizationColors();
	}

	private void ApplyVisualizationColors()
	{
		Color meshColor = _rangeVisualizerBaseColor;
		meshColor.a = _currentVisualizationState.meshAlpha;

		if (mr != null && mr.material != null)
		{
			mr.material.SetColor(rangeVisualizerMaterialColorString, meshColor);
		}

		if (lr == null) lr = GetComponent<LineRenderer>();
		if (lr != null)
		{
			Color lineColor = _rangeVisualizerBaseColor;
			lineColor.a = _currentVisualizationState.lineAlpha;
			lr.startColor = lineColor;
			lr.endColor = lineColor;
		}
	}

    public void SetRangeVisualizerColor(Color color)
    {
        _rangeVisualizerBaseColor = color;
		ApplyVisualizationColors();
    }

	public Color GetRangeVisualizerBaseColor()
	{
		return _rangeVisualizerBaseColor;
	}

	public float GetRangeRadius()
	{
		if (_collider == null) _collider = GetComponent<CircleCollider2D>();
		return _collider != null ? Mathf.Max(0f, _collider.radius) : 0f;
	}

    private void Start()
    {
        if (_attachedTower == null)
        {
            Debug.LogError("Something is wrong with RangeManager: no Tower found in parent hierarchy. Disabling range visualization.");
            return;
        }
        SetRangeVisualizerColor(_attachedTower.GetColor());
    }

    public void Update()
	{
     UpdateVisualizationState(instant: false);

		if (debugDrawLOS && !_debugDrawLosPrev)
		{
			DebugDrawLOS(duration: 10f);
		}
		_debugDrawLosPrev = debugDrawLOS;

		if (!ShouldShowRangeVisualization())
		{
			HideRangeVisualization();
			HideMeshVisualization();
			return;
		}

        // if (Input.GetKeyDown(KeyCode.Alpha3) && _attachedTower.CurrentState == Tower.State.Placed)
        // {
        //     MeshVisualization();
        // }
    }

	private void DebugDrawLOS(float duration)
	{
		if (_collider == null) _collider = GetComponent<CircleCollider2D>();
		if (_collider == null) return;

		if (!_losDataValid)
			EnsureLosData();
		if (!_losDataValid || _losEndDists == null || _losRayCount < 3) return;

      float offset = GetEffectiveLosOffset(_losRadius);
		Vector3 centerWorld = transform.position;

		for (int i = 0; i < _losRayCount; i++)
		{
			float angleDeg = (i / (float)_losRayCount) * 360f;
			Vector3 dirLocal = Quaternion.Euler(0f, 0f, angleDeg) * Vector3.right;
			dirLocal.z = 0f;
			Vector3 dirWorld = transform.TransformDirection(dirLocal).normalized;

			Vector3 startWorld = centerWorld + dirWorld * offset;
			Vector3 endWorld = centerWorld + dirWorld * _losEndDists[i];
			Debug.DrawLine(startWorld, endWorld, Color.red, duration);
		}
	}

    private void Awake()
    {
        lr = GetComponent<LineRenderer>();
        _collider = GetComponent<CircleCollider2D>();
        mr = GetComponent<MeshRenderer>();
        if (_collider != null) _collider.isTrigger = true;

        _attachedTower = GetComponentInParent<Tower>();
        _currentVisualizationState = noEnemiesInRangeVisualization;
		ApplyVisualizationColors();
        HideRangeVisualization();
    }

    private void OnEnable()
    {
        RefreshOverlaps();
    }

    public void SetTargettingMode(TargettingMode mode)
    {
        if (!IsModeAvailable(mode))
            mode = TargettingMode.Nearest;

        bool switchingToManual = mode == TargettingMode.Manual && targettingMode != TargettingMode.Manual;
        targettingMode = mode;

        if (switchingToManual && _attachedTower != null)
            _attachedTower.OnManualTargetModeSelected();

        ForceRetarget();
    }

    private void OnValidate()
    {
        if (_collider == null) _collider = GetComponent<CircleCollider2D>();
        if (_collider != null) _collider.isTrigger = true;
        if (!IsModeAvailable(targettingMode))
            targettingMode = TargettingMode.Nearest;
    }

    public bool IsEnemyValidTarget(Enemy e)
    {
        if (e == null) return false;
        if (!enemiesInRange.Contains(e)) return false;
		{
			if (singleRayMode)
			{
				if (!IsEnemyInsideSingleRay(e)) return false;
			}
			else
			{
				if (!IsEnemyInsideLosMesh(e)) return false;
			}
		}
        return true;
    }

	private static readonly RaycastHit2D[] s_losHits = new RaycastHit2D[64];
	private void EnsureLosData()
	{
		if (_losDataValid) return;

		if (_collider == null) _collider = GetComponent<CircleCollider2D>();
		if (_collider == null) return;
		if (LayerMaskManager.instance == null) return;

		int rays = Mathf.Max(3, circleSegments);
		int allocRays = rangeDoubleThreshold > 0f ? rays * 2 : rays;
		float radius = Mathf.Max(0f, _collider.radius);
      float offset = GetEffectiveLosOffset(radius);
		if (radius <= 0.0001f) return;

		if (_losEndDists == null || _losEndDists.Length != allocRays)
		{
			_losEndDists = new float[allocRays];
		}

		rays = CircularRaycastDistances(radius, offset, _losEndDists);
		if (rays < 3) return;
		_losRayCount = rays;
		_losRadius = radius;
		_losDataValid = true;
	}

	public float GetDistance(Vector3 direction)
	{
		if (_collider == null) _collider = GetComponent<CircleCollider2D>();
		float radius = _collider != null ? Mathf.Max(0f, _collider.radius) : 0f;

		if (!useLOS) return radius;

		if (singleRayMode)
		{
			EnsureSingleRayData();
			if (!_singleRayDataValid) return 0f;
			Vector2 dirWorld = ((Vector2)direction);
			if (dirWorld.sqrMagnitude <= 0.0000001f) return 0f;
			dirWorld.Normalize();
			float alignment = Vector2.Dot(dirWorld, _singleRayDirWorld);
			if (alignment < 0.95f) return 0f;
			return _singleRayMaxDist;
		}

		Vector3 dirLocal = transform.InverseTransformDirection(direction);
		dirLocal.z = 0f;
		if (dirLocal.sqrMagnitude <= 0.0000001f) return 0f;
		dirLocal.Normalize();

		if (!_losDataValid)
			EnsureLosData();
		if (!_losDataValid || _losEndDists == null || _losRayCount < 3)
			return radius;

		float ang = Mathf.Atan2(dirLocal.y, dirLocal.x);
		if (ang < 0f) ang += Mathf.PI * 2f;

		float f = (ang / (Mathf.PI * 2f)) * _losRayCount;
		int idx = Mathf.RoundToInt(f) % _losRayCount;
		if (idx < 0) idx += _losRayCount;

		return _losEndDists[idx];
	}

    public void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.TryGetComponent(out Enemy enemy))
        {
			bool wasAdded = enemiesInRange.Add(enemy);
			if (wasAdded && _attachedTower != null)
			{
				_attachedTower.OnEnterRange(enemy);
				_attachedTower.SetTargettedEnemy();
			}

            if (GetTargettedEnemy() == null)
            {
                _targetted = enemy;
            }
            return;
        }

        if (collision.TryGetComponent(out Tower tower))
        {
            if (tower != null && tower.gameObject != gameObject)
            {
                _towersInRange.Add(tower);
            }
        }
    }

    public void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.TryGetComponent(out Enemy enemy))
        {
			bool wasRemoved = enemiesInRange.Remove(enemy);
			if (wasRemoved && _attachedTower != null)
			{
				_attachedTower.OnEnemyExitRange(enemy);
				_attachedTower.SetTargettedEnemy();
			}
            return;
        }

        if (collision.TryGetComponent(out Tower tower))
        {
            _towersInRange.Remove(tower);
        }
    }

    private void CleanupNulls()
    {
        enemiesInRange.RemoveWhere(e => e == null);
        _towersInRange.RemoveWhere(t => t == null);
    }

    public List<Tower> GetAllActiveTowersInRange()
    {
        CleanupNulls();

        var result = new List<Tower>(_towersInRange.Count);
        foreach (var t in _towersInRange)
        {
            if (t == null) continue;
            if (!t.isActiveAndEnabled) continue;
            result.Add(t);
        }
        return result;
    }

    public void VisualizeRange()
    {
        if (!ShouldShowRangeVisualization() && !HasActiveMeshVisualization())
		{
			HideRangeVisualization();
			HideMeshVisualization();
			return;
		}

        if (lr == null) lr = GetComponent<LineRenderer>();
        if (_collider == null) _collider = GetComponent<CircleCollider2D>();
        if (lr == null || _collider == null)
            return;

        float r = Mathf.Max(0f, _collider.radius);
        if (r <= 0.0001f)
        {
            lr.enabled = false;
            return;
        }

        int seg = Mathf.Max(12, circleSegments);
        lr.useWorldSpace = false;
        lr.loop = true;
        lr.enabled = true;
        lr.widthMultiplier = _currentVisualizationState.lineWidth;
        lr.positionCount = seg;

        Vector3 ls = transform.localScale;
        float invX = Mathf.Abs(ls.x) > 0.000001f ? 1f / ls.x : 1f;
        float invY = Mathf.Abs(ls.y) > 0.000001f ? 1f / ls.y : 1f;
       float lineWidth = _currentVisualizationState.lineWidth * 0.5f;
		float ringRadius = r + lineWidth;

        for (int i = 0; i < seg; i++)
        {
            float a = (i / (float)seg) * Mathf.PI * 2f;
            float x = Mathf.Cos(a) * ringRadius;
			float y = Mathf.Sin(a) * ringRadius;
            lr.SetPosition(i, new Vector3(x * invX, y * invY, 0f));
        }
    }

    public void HideRangeVisualization()
    {
        if (HasActiveMeshVisualization())
		{
			VisualizeRange();
			return;
		}

        if (lr == null) lr = GetComponent<LineRenderer>();
        if (lr != null) lr.enabled = false;
    }

    private static float SqrDistance2D(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dy = a.y - b.y;
        return dx * dx + dy * dy;
    }

    private void RegisterTarget(Enemy e)
    {
        if (_registeredTarget == e) return;

        if (_registeredTarget != null)
        {
            _registeredTarget.UnregisterTargetingRangeManager(this);
        }

        _registeredTarget = e;
        if (_registeredTarget != null)
        {
            _registeredTarget.RegisterTargetingRangeManager(this);
        }
    }

    public void ForceRetarget()
    {
        _targetted = null;
        UpdateTargettedEnemy();
    }

    public List<Enemy> GetAllEnemiesInRange()
    {
        CleanupNulls();
        return new List<Enemy>(enemiesInRange);
    }

    public HashSet<Enemy> GetEnemiesInRangeHashSet()
    {
        CleanupNulls();
        return enemiesInRange;
    }

    public HashSet<Enemy> GetEnemiesInRangeSortedByDistance()
    {
        CleanupNulls();
        var list = new List<Enemy>(enemiesInRange);
        list.Sort((a, b) =>
        {
            float da = SqrDistance2D(transform.position, a.transform.position);
            float db = SqrDistance2D(transform.position, b.transform.position);
            return da.CompareTo(db);
        });
        return new HashSet<Enemy>(list);
    }

	private bool IsEnemyAllowedByConstraints(Enemy enemy)
	{
		if (enemy == null) return false;
		if (!enemiesInRange.Contains(enemy)) return false;
		if (!useLOS) return true;
		return singleRayMode ? IsEnemyInsideSingleRay(enemy) : IsEnemyInsideLosMesh(enemy);
	}

	private bool CanBeMarkedPriorityTarget(Enemy enemy)
	{
		if (enemy == null || _attachedTower == null) return false;
		CM.ColorType mark = enemy.GetMark();
		return mark != CM.ColorType.None && _attachedTower.CanDealDamageType(mark);
	}

	private int CompareByTargettingMode(Enemy a, Enemy b, TargettingMode mode)
	{
		float da = SqrDistance2D(transform.position, a.transform.position);
		float db = SqrDistance2D(transform.position, b.transform.position);

		switch (mode)
		{
			case TargettingMode.Farthest:
				{
					int cmpFar = db.CompareTo(da);
					if (cmpFar != 0) return cmpFar;
					return a.GetInstanceID().CompareTo(b.GetInstanceID());
				}
			case TargettingMode.LowestHealth:
				{
					float ha = a.health != null ? a.health.currentHealth : float.PositiveInfinity;
					float hb = b.health != null ? b.health.currentHealth : float.PositiveInfinity;
					int cmpHealth = ha.CompareTo(hb);
					if (cmpHealth != 0) return cmpHealth;
					int cmpDist = da.CompareTo(db);
					if (cmpDist != 0) return cmpDist;
					return a.GetInstanceID().CompareTo(b.GetInstanceID());
				}
			case TargettingMode.HighestHealth:
				{
					float ha = a.health != null ? a.health.currentHealth : float.NegativeInfinity;
					float hb = b.health != null ? b.health.currentHealth : float.NegativeInfinity;
					int cmpHealth = hb.CompareTo(ha);
					if (cmpHealth != 0) return cmpHealth;
					int cmpDist = da.CompareTo(db);
					if (cmpDist != 0) return cmpDist;
					return a.GetInstanceID().CompareTo(b.GetInstanceID());
				}
			case TargettingMode.Fastest:
				{
					float va = a.rb != null ? a.rb.linearVelocity.sqrMagnitude : 0f;
					float vb = b.rb != null ? b.rb.linearVelocity.sqrMagnitude : 0f;
					int cmpSpeed = vb.CompareTo(va);
					if (cmpSpeed != 0) return cmpSpeed;
					int cmpDist = da.CompareTo(db);
					if (cmpDist != 0) return cmpDist;
					return a.GetInstanceID().CompareTo(b.GetInstanceID());
				}
			case TargettingMode.Slowest:
				{
					float va = a.rb != null ? a.rb.linearVelocity.sqrMagnitude : 0f;
					float vb = b.rb != null ? b.rb.linearVelocity.sqrMagnitude : 0f;
					int cmpSpeed = va.CompareTo(vb);
					if (cmpSpeed != 0) return cmpSpeed;
					int cmpDist = da.CompareTo(db);
					if (cmpDist != 0) return cmpDist;
					return a.GetInstanceID().CompareTo(b.GetInstanceID());
				}
			case TargettingMode.Burning:
				{
					bool aBurning = a.HasStatusEffect(Enemy.StatusEffect.Burning);
					bool bBurning = b.HasStatusEffect(Enemy.StatusEffect.Burning);
					if (aBurning != bBurning)
					{
						return aBurning ? -1 : 1;
					}

					if (aBurning)
					{
						int aStacks = a.health != null ? a.health.BurnStacks : 1;
						int bStacks = b.health != null ? b.health.BurnStacks : 1;
						int cmpStacks = bStacks.CompareTo(aStacks);
						if (cmpStacks != 0) return cmpStacks;
					}

					int cmpDist = da.CompareTo(db);
					if (cmpDist != 0) return cmpDist;
					return a.GetInstanceID().CompareTo(b.GetInstanceID());
				}
			case TargettingMode.Marked:
				{
					bool aMarked = CanBeMarkedPriorityTarget(a);
					bool bMarked = CanBeMarkedPriorityTarget(b);
					if (aMarked != bMarked)
					{
						return aMarked ? -1 : 1;
					}

					int cmpDist = da.CompareTo(db);
					if (cmpDist != 0) return cmpDist;
					return a.GetInstanceID().CompareTo(b.GetInstanceID());
				}
			case TargettingMode.Manual:
			case TargettingMode.Nearest:
			default:
				{
					int cmpDist = da.CompareTo(db);
					if (cmpDist != 0) return cmpDist;
					return a.GetInstanceID().CompareTo(b.GetInstanceID());
				}
		}
	}

	public List<Enemy> GetTopTargets(int count)
	{
		CleanupNulls();

		var result = new List<Enemy>();
		if (count <= 0 || enemiesInRange.Count == 0)
		{
			return result;
		}

		Enemy highlighted = null;
		bool hasHighlighted = false;
		if (PIC.forceAllTowersTargetHighlightedEnemy && PIC.instance != null)
		{
			highlighted = PIC.instance.GetHighlightedEnemy();
			hasHighlighted = IsEnemyAllowedByConstraints(highlighted);
		}

		var candidates = new List<Enemy>(enemiesInRange.Count);
		foreach (var enemy in enemiesInRange)
		{
			if (!IsEnemyAllowedByConstraints(enemy)) continue;
			candidates.Add(enemy);
		}

		if (candidates.Count == 0)
		{
			return result;
		}

		var mode = GetTargettingMode();
		candidates.Sort((a, b) => CompareByTargettingMode(a, b, mode));

		if (automaticMarkTargetting && _attachedTower != null)
		{
			candidates.Sort((a, b) =>
			{
				bool aMarked = CanBeMarkedPriorityTarget(a);
				bool bMarked = CanBeMarkedPriorityTarget(b);
				if (aMarked != bMarked)
				{
					return aMarked ? -1 : 1;
				}

				return CompareByTargettingMode(a, b, mode);
			});
		}

		if (hasHighlighted)
		{
			int highlightedIndex = candidates.IndexOf(highlighted);
			if (highlightedIndex >= 0)
			{
				candidates.RemoveAt(highlightedIndex);
				result.Add(highlighted);
			}
		}

		int needed = Mathf.Min(count, candidates.Count + result.Count);
		for (int i = 0; i < candidates.Count && result.Count < needed; i++)
		{
			result.Add(candidates[i]);
		}

		return result;
	}

    private void UpdateTargettedEnemy()
    {
		var topTargets = GetTopTargets(1);
		_targetted = topTargets.Count > 0 ? topTargets[0] : null;
        RegisterTarget(_targetted);
    }

    public Enemy GetTargettedEnemy()
    {
		if (PIC.forceAllTowersTargetHighlightedEnemy && PIC.instance != null)
		{
			Enemy highlighted = PIC.instance.GetHighlightedEnemy();
			if (IsEnemyAllowedByConstraints(highlighted) && _targetted != highlighted)
			{
				_targetted = highlighted;
				RegisterTarget(_targetted);
				return _targetted;
			}
		}

        if (!IsEnemyValidTarget(_targetted))
        {
            _targetted = null;
            UpdateTargettedEnemy();
        }

        RegisterTarget(_targetted);
        return _targetted;
    }

    private void OnDisable()
    {
        RegisterTarget(null);
    }

    public void SetRange(float range)
    {
        float r = Mathf.Max(0f, range);
        if (_collider == null) _collider = GetComponent<CircleCollider2D>();
        if (_collider != null)
        {
            _collider.radius = r;
            _collider.isTrigger = true;
        }

		_losDataValid = false;
		_singleRayDataValid = false;
		MeshVisualization();
    }

    public void SetRotation(float rotation)
    {
        transform.rotation = Quaternion.Euler(0f, 0f, rotation);
    }

    /// <summary>
    /// Rotates the manual targeting indicator around the tower by the specified degrees.
    /// Only works when the tower is in manual targeting mode.
    /// </summary>
    public void RotateManualTarget(float degrees)
    {
        if (_attachedTower == null) return;
        
        Vector3 towerPos = transform.position;
        Vector3 indicatorPos = _attachedTower.GetTargetPosition();
        
        // Vector from tower to current indicator position
        Vector3 toIndicator = indicatorPos - towerPos;
        
        // Rotate this vector by the specified degrees around the Z axis
        float radians = degrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(radians);
        float sin = Mathf.Sin(radians);
        
        Vector3 rotatedVector = new Vector3(
            toIndicator.x * cos - toIndicator.y * sin,
            toIndicator.x * sin + toIndicator.y * cos,
            toIndicator.z
        );
        
        // New position = tower center + rotated vector
        Vector3 newPos = towerPos + rotatedVector;
        
        // Ensure the new position stays within range
        if (_collider != null && !PointInsideRange(newPos))
        {
            // Clamp to range circle edge
            Vector2 direction = ((Vector2)rotatedVector).normalized;
            float radius = _collider.radius;
            newPos = towerPos + new Vector3(direction.x * radius, direction.y * radius, 0f);
        }
        
        // Update the target indicator position via Tower's public setter
        _attachedTower.SetTargetIndicatorPosition(newPos);
    }

    public void MeshVisualization()
	{
		if (!ShouldShowRangeVisualization())
		{
			HideRangeVisualization();
			HideMeshVisualization();
			return;
		}

		if (_collider == null) _collider = GetComponent<CircleCollider2D>();
		if (_collider == null) return;
		if (LayerMaskManager.instance == null) return;

		MeshFilter mf = GetComponent<MeshFilter>();
		if (mf == null) return;

		int rays = Mathf.Max(3, circleSegments);
		int allocRays = rangeDoubleThreshold > 0f ? rays * 2 : rays;
		int steps = Mathf.Max(1, losSegments);
		float stepLen = Mathf.Max(0.0001f, losSegmentLength);
		float radius = Mathf.Max(0f, _collider.radius);
      float offset = GetEffectiveLosOffset(radius);
		if (radius <= 0.0001f)
		{
			mf.sharedMesh = null;
			return;
		}

		if (!useLOS)
		{
			BuildCircleMesh(mf, radius);
          VisualizeRange();
			_losDataValid = false;
			_singleRayDataValid = false;
			return;
		}

		if (_losEndDists == null || _losEndDists.Length != allocRays)
			_losEndDists = new float[allocRays];
		float[] endDists = _losEndDists;
		var dirsLocal = new Vector3[allocRays];
		rays = CircularRaycastDistances(radius, offset, endDists, dirsLocal);
		if (rays < 3) return;

		int vertsPerRay = steps + 1;
		int vertexCount = 1 + rays * vertsPerRay;
		var vertices = new Vector3[vertexCount];
		var uvs = new Vector2[vertexCount];
		vertices[0] = Vector3.zero;
		uvs[0] = new Vector2(0.5f, 0.5f);

		int VIndex(int ray, int step) => 1 + ray * vertsPerRay + step;

		for (int i = 0; i < rays; i++)
		{
			Vector3 dir = dirsLocal[i];
			float end = Mathf.Max(offset, endDists[i]);
			for (int s = 0; s < vertsPerRay; s++)
			{
				float dist = Mathf.Min(offset + s * stepLen, end);
				int vi = VIndex(i, s);
				vertices[vi] = dir * dist;
				uvs[vi] = new Vector2(i / (float)rays, dist / radius);
			}
		}

		int triCount = (rays * 1) + (rays * steps * 2);
		int[] triangles = new int[triCount * 3];
		int ti = 0;

		for (int i = 0; i < rays; i++)
		{
			int next = (i + 1) % rays;
			triangles[ti++] = 0;
			triangles[ti++] = VIndex(i, 0);
			triangles[ti++] = VIndex(next, 0);
		}

		for (int s = 0; s < steps; s++)
		{
			for (int i = 0; i < rays; i++)
			{
				int next = (i + 1) % rays;
				int a = VIndex(i, s);
				int b = VIndex(next, s);
				int c = VIndex(i, s + 1);
				int d = VIndex(next, s + 1);

				triangles[ti++] = a;
				triangles[ti++] = c;
				triangles[ti++] = d;

				triangles[ti++] = a;
				triangles[ti++] = d;
				triangles[ti++] = b;
			}
		}

		if (_losMesh == null)
		{
			_losMesh = new Mesh();
			_losMesh.name = "RangeManager LOS Mesh";
			_losMesh.MarkDynamic();
		}

		_losMesh.Clear();
		_losMesh.vertices = vertices;
		_losMesh.triangles = triangles;
		_losMesh.uv = uvs;
		_losMesh.RecalculateBounds();
		_losMesh.RecalculateNormals();
		mf.sharedMesh = _losMesh;

        _losRayCount = rays;
        _losRadius = radius;
		_losDataValid = true;
       VisualizeRange();
    }

	private bool ShouldShowRangeVisualization()
	{
		if (!showRangeEnabled) return false;
		if (_attachedTower == null) return false;

		if (_attachedTower.CurrentState == Tower.State.Placed)
		{
			return true;
		}

		return IsAttachedTowerHeldByPlayer();
	}

	private bool IsAttachedTowerHeldByPlayer()
	{
		if (_attachedTower == null) return false;

		var pic = PIC.instance;
		if (pic == null) return false;
		if (pic.currentState != PIC.PICState.PlacingTower) return false;

		return pic.IsHoldingTower(_attachedTower);
	}

    private void BuildCircleMesh(MeshFilter mf, float radius)
	{
		if (mf == null) return;
		int seg = Mathf.Max(12, circleSegments);
		float r = Mathf.Max(0.0001f, radius);

		var vertices = new Vector3[seg + 1];
		var uvs = new Vector2[seg + 1];
		var triangles = new int[seg * 3];

		vertices[0] = Vector3.zero;
		uvs[0] = new Vector2(0.5f, 0.5f);

		for (int i = 0; i < seg; i++)
		{
			float a = (i / (float)seg) * Mathf.PI * 2f;
			Vector3 dirLocal = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
			float dist = r;
			float x = dirLocal.x * dist;
			float y = dirLocal.y * dist;
			vertices[i + 1] = new Vector3(x, y, 0f);
			uvs[i + 1] = new Vector2(0.5f + (x / (r * 2f)), 0.5f + (y / (r * 2f)));
		}

		for (int i = 0; i < seg; i++)
		{
			int next = (i + 1) % seg;
			int ti = i * 3;
			triangles[ti + 0] = 0;
			triangles[ti + 1] = i + 1;
			triangles[ti + 2] = next + 1;
		}

		if (_losMesh == null)
		{
			_losMesh = new Mesh();
			_losMesh.name = "RangeManager Circle Mesh";
			_losMesh.MarkDynamic();
		}

		_losMesh.Clear();
		_losMesh.vertices = vertices;
		_losMesh.triangles = triangles;
		_losMesh.uv = uvs;
		_losMesh.RecalculateBounds();
		_losMesh.RecalculateNormals();
		mf.sharedMesh = _losMesh;
	}

    private static readonly Collider2D[] s_overlap = new Collider2D[128];

    private void RefreshOverlaps()
    {
        if (!isActiveAndEnabled) return;
        if (_collider == null) _collider = GetComponent<CircleCollider2D>();
        if (_collider == null) return;

        enemiesInRange.RemoveWhere(e => e == null);
        _towersInRange.RemoveWhere(t => t == null);

        int count = _collider.Overlap(ContactFilter2D.noFilter, s_overlap);

        for (int i = 0; i < count; i++)
        {
            var c = s_overlap[i];
            if (c == null) continue;

            if (c.TryGetComponent(out Enemy e))
            {
                enemiesInRange.Add(e);
                continue;
            }

            if (c.TryGetComponent(out Tower t))
            {
                if (t != null && t.gameObject != gameObject)
                    _towersInRange.Add(t);
            }
        }

        if (!IsEnemyValidTarget(_targetted))
        {
            _targetted = null;
            UpdateTargettedEnemy();
        }
    }

    public bool IsEnemyInsideLosMesh(Enemy e)
    {
		if (e == null) return false;
		if (!_losDataValid)
			EnsureLosData();
		if (!_losDataValid || _losEndDists == null || _losRayCount < 3) return false;

        Vector3 local = transform.InverseTransformPoint(e.transform.position);
        local.z = 0f;

        float dist = local.magnitude;
        if (dist > _losRadius) return false;

        float ang = Mathf.Atan2(local.y, local.x);
        if (ang < 0f) ang += Mathf.PI * 2f;

        float f = (ang / (Mathf.PI * 2f)) * _losRayCount;
        int i0 = Mathf.FloorToInt(f) % _losRayCount;
        int i1 = (i0 + 1) % _losRayCount;
        float t = f - Mathf.Floor(f);

        float boundary = Mathf.Lerp(_losEndDists[i0], _losEndDists[i1], t);
        return dist <= boundary;
    }

	public bool PointInsideRange(Vector3 point)
	{
		if (_collider == null) _collider = GetComponent<CircleCollider2D>();
		if (_collider == null) return false;

		// Fast reject against base range radius before any LOS/single-ray work.
		Vector2 center = transform.position;
		Vector2 toPoint = (Vector2)point - center;
		float radius = Mathf.Max(0f, _collider.radius);
		if (toPoint.sqrMagnitude > radius * radius) return false;

		var gm = GridManager.instance;
		if (gm != null)
		{
			if (!gm.TryWorldToCell(point, out var pointCell)) return false;
			if (gm.IsWallAtCell(pointCell)) return false;
		}

		if (!useLOS) return true;
		if (singleRayMode) return IsPointInsideSingleRay(point);

		if (!_losDataValid)
			EnsureLosData();
		if (!_losDataValid || _losEndDists == null || _losRayCount < 3) return false;

		Vector3 local = transform.InverseTransformPoint(point);
		local.z = 0f;

		float dist = local.magnitude;
		if (dist > _losRadius) return false;

		float ang = Mathf.Atan2(local.y, local.x);
		if (ang < 0f) ang += Mathf.PI * 2f;

		float f = (ang / (Mathf.PI * 2f)) * _losRayCount;
		int i0 = Mathf.FloorToInt(f) % _losRayCount;
		int i1 = (i0 + 1) % _losRayCount;
		float t = f - Mathf.Floor(f);

		float boundary = Mathf.Lerp(_losEndDists[i0], _losEndDists[i1], t);
		return dist <= boundary;
	}
}
