using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Handles movement/steering for an Enemy (pathfinding + boids + wall avoidance).
/// Split out of <see cref="Enemy"/> so movement logic is decoupled from enemy lifecycle/targeting.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Enemy))]
public class Movement : MonoBehaviour
{

	[Header("Movement")]
	[SerializeField] private float speed = 3f;
	[SerializeField] private float acceleration = 20f;
	public Transform rotationTransform;

	[Header("Target")]
	[SerializeField] private Transform goal;
	[Tooltip("If within this distance to the goal, the enemy stops steering.")]
	[SerializeField] private float stopDistance = 0.1f;

	[Header("Navigation")]
	[Tooltip("If disabled, the enemy will not call the pathfinder and will steer directly toward the goal.")]
	[SerializeField] private bool navigationEnabled = true;
	[SerializeField] private Pathfinding pathfinding;
	[SerializeField] private GridManager grid;

	[Header("Boids")]
	[SerializeField] private bool boidsEnabled = true;
	[Tooltip("How strongly the grid/pathfinding desired direction influences movement.")]
	[Min(0f)]
	[SerializeField] private float desiredDirectionWeight = 1f;
	[Tooltip("How strongly the boid steering (separation/alignment/cohesion) influences movement.")]
	[Min(0f)]
	[SerializeField] private float boidSteeringWeight = 1f;
	[Tooltip("World-space radius to search for neighbors.")]
	[SerializeField] private float neighborRadius = 2f;
	[Tooltip("How often (seconds) to refresh the cached neighbor list.")]
	[SerializeField] private float neighborRefreshInterval = 0.2f;
	[SerializeField] private float separationWeight = 1.5f;
	[SerializeField] private float alignmentWeight = 0.75f;
	[SerializeField] private float cohesionWeight = 0.5f;
	[Tooltip("Distance within which separation force ramps up.")]
	[SerializeField] private float separationDistance = 0.75f;

	[Header("Slow")]
	[Range(0f, 1f)]
	[SerializeField] private float slowPercentageMax = 0.75f;
	[Tooltip("Per-second decay of the current slow percentage.")]
	[SerializeField] private float slowDecayRate = 0.25f;
	public static float slowDecayRateGlobalMultiplier = 1;
	public float maxSlowDecayCooldown = 1; //if the enemy reaches maxSlow, don't decay until after maxSlowDecayCooldown
	public float maxSlowDeltaThreshold = .01f;
	private float maxSlowDecayTimer = -1;

	[Header("Rotation")]
	[Tooltip("Degrees per second the enemy rotates toward its desired direction.")]
	[SerializeField] private float rotationSpeed = 180f;
	[Tooltip("Minimum squared magnitude of desired direction required before updating rotation.")]
	[SerializeField] private float minDirForRotation = 0.0005f;
	[Tooltip("If true, rotation speed scales inversely with distance to nearest wall.")]
	[SerializeField] private bool wallDistanceAffectsRotationSpeed = true;
	[Tooltip("Reference wall distance used for inverse scaling (multiplier = reference / distance).")]
	[SerializeField, Min(0.01f)] private float rotationWallDistanceReference = 1f;
	[Tooltip("Exponent applied to proximity response. Higher values increase turn speed more aggressively near walls.")]
	[SerializeField, Min(0.1f)] private float rotationWallProximityExponent = 1.5f;
	[Tooltip("Minimum wall distance used in the inverse division to avoid extreme turn speeds.")]
	[SerializeField, Min(0.01f)] private float minWallDistanceForRotation = 0.25f;
	[Tooltip("Lower clamp for wall-distance rotation multiplier.")]
	[SerializeField, Min(0f)] private float minWallDistanceRotationMultiplier = 1f;
	[Tooltip("Upper clamp for wall-distance rotation multiplier.")]
	[SerializeField, Min(0f)] private float maxWallDistanceRotationMultiplier = 3f;

	private Enemy _enemy;
	private Rigidbody2D _rb;
	private SpriteRenderer _sr;

	private readonly List<Enemy> _neighbors = new List<Enemy>(32);
	private float _nextNeighborRefreshTime;

	private float _slowPercentage;
	private float _currentSpeed;

	public float Slow01
	{
		get
		{
			if (slowPercentageMax <= 0f) return 0f;
			return Mathf.Clamp01(_slowPercentage / slowPercentageMax);
		}
	}

	public bool IsSlowed => _slowPercentage > 0.0001f;

	public Transform Goal
	{
		get => goal;
		set => goal = value;
	}

	public virtual void Start()
	{
		float randomAngle = Random.Range(0f, 360f);
		Quaternion randomRotation = Quaternion.Euler(0f, 0f, randomAngle);

		if (rotationTransform != null)
		{
			rotationTransform.rotation = randomRotation;
		}
		else
		{
			transform.rotation = randomRotation;
		}

		if (_rb != null)
		{
			Vector2 forward = rotationTransform != null ? (Vector2)rotationTransform.up : (Vector2)transform.up;
			_currentSpeed = Mathf.Max(0f, speed);
			_rb.linearVelocity = forward * _currentSpeed;
		}
	}

	private void Awake()
	{
		_enemy = GetComponent<Enemy>();
		_rb = GetComponent<Rigidbody2D>();

		// Ensure Enemy has its Rigidbody cached even if script execution order differs.
		if (_enemy != null && _enemy.rb == null) _enemy.rb = _rb;

		// Get the SpriteRenderer from the SRC component
		if (_enemy != null)
		{
			var src = _enemy.GetComponent<SRC>();
			if (src != null)
			{
				_sr = src.GetPrimarySpriteRenderer();
			}
		}

		if (_sr == null)
		{
			_sr = GetComponentInChildren<SpriteRenderer>();
		}

		if (rotationTransform == null)
		{
			rotationTransform = _sr != null ? _sr.transform : transform;
		}

		if (pathfinding == null) pathfinding = FindFirstObjectByType<Pathfinding>();
		if (grid == null) grid = FindFirstObjectByType<GridManager>();
	}

	private void OnEnable()
	{
		_nextNeighborRefreshTime = Time.time + Random.Range(0f, Mathf.Max(0.01f, neighborRefreshInterval));

		// Register in quadtree for boid queries.
		if (QuadTree2D.instance != null && _enemy != null) QuadTree2D.instance.Register(_enemy);
	}

	private void OnDisable()
	{
		if (QuadTree2D.instance != null && _enemy != null) QuadTree2D.instance.Unregister(_enemy);
	}

	private void FixedUpdate()
	{
		// Keep quadtree position current.
		if (QuadTree2D.instance != null && _enemy != null) QuadTree2D.instance.UpdateEnemy(_enemy);

		// Slow decay affects effective speed.
		TickSlow(Time.fixedDeltaTime);

		// Boid neighbor refresh (throttled).
		if (boidsEnabled && Time.time >= _nextNeighborRefreshTime)
		{
			RefreshNeighbors();
			_nextNeighborRefreshTime = Time.time + Mathf.Max(0.01f, neighborRefreshInterval);
		}

		TickMovement(Time.fixedDeltaTime);
	}

	public void SetMaxSlow(float s)
    {
		slowPercentageMax = Mathf.Clamp01(s);
		_slowPercentage = Mathf.Clamp(_slowPercentage, 0f, slowPercentageMax);
    }

	public float GetMaxSlow()
    {
		return slowPercentageMax;
    }

	public void RemoveSlow()
	{
		_slowPercentage = 0f;
	}

	public void ReduceSlow(float slowAmount)
	{
		_slowPercentage = Mathf.Max(0f, _slowPercentage - Mathf.Max(0f, slowAmount));
	}

	public bool AtMaxSlow()
	{
		return Mathf.Abs(slowPercentageMax - _slowPercentage) < maxSlowDeltaThreshold;
	}

	public void ApplySlow(float slowAmount)
	{
		_slowPercentage = Mathf.Clamp(_slowPercentage + Mathf.Max(0f, slowAmount), 0f, Mathf.Clamp01(slowPercentageMax));
		if (Mathf.Abs(slowPercentageMax - _slowPercentage) < maxSlowDeltaThreshold) 
		{
			maxSlowDecayTimer = Time.time + maxSlowDecayCooldown;
		}
	}

	private void TickSlow(float dt)
	{
		if (_slowPercentage > 0f && SlowDecayEnabled())
		{
			_slowPercentage = Mathf.Max(0f, _slowPercentage - Mathf.Max(0f, slowDecayRate) * dt * slowDecayRateGlobalMultiplier);
			if (_slowPercentage < .001f)
			{
				_slowPercentage = 0;
			}
		}
	}

	public bool SlowDecayEnabled()
    {
		foreach (Tower tower in TowerManager.instance.EnumerateTowersInState(Tower.State.Placed))
		{
			if (tower.UpgradeActive(UpgradeData.UID.EnemiesInRangeSlowDecayDisable) && tower.GetRangeManager().IsEnemyValidTarget(_enemy))
			{
				return false;
			}
		}
		return Time.time > maxSlowDecayTimer;
    }

	private void RefreshNeighbors()
	{
		_neighbors.Clear();
		if (QuadTree2D.instance == null) return;

		float r = Mathf.Max(0f, neighborRadius);
		if (r <= 0.0001f) return;

		Vector2 p = _rb != null ? _rb.position : (Vector2)transform.position;
		QuadTree2D.instance.QueryCircle(p, r, _neighbors, ignore: _enemy);
	}

	private void TickMovement(float dt)
	{
		if (_rb == null) return;
		if (goal == null) return;
		if (_enemy != null && _enemy.IsEngagedByAgents())
		{
			_currentSpeed = Mathf.MoveTowards(_currentSpeed, 0f, Mathf.Max(0f, acceleration) * dt);
			Vector2 forward = rotationTransform != null ? (Vector2)rotationTransform.up : (Vector2)transform.up;
			_rb.linearVelocity = forward * _currentSpeed;
			return;
		}

		Vector2 pos = _rb.position;
		Vector2 goalPos = goal.position;
		Vector2 toGoal = goalPos - pos;

		if (toGoal.sqrMagnitude <= stopDistance * stopDistance)
		{
			_currentSpeed = Mathf.MoveTowards(_currentSpeed, 0f, Mathf.Max(0f, acceleration) * dt);
			Vector2 forward = rotationTransform != null ? (Vector2)rotationTransform.up : (Vector2)transform.up;
			_rb.linearVelocity = forward * _currentSpeed;
			return;
		}

		Vector2 desiredDir;
		if (navigationEnabled && pathfinding != null)
		{
			Vector3 dir3 = pathfinding.GetDirection(new Vector3(pos.x, pos.y, 0f), goal);
			desiredDir = new Vector2(dir3.x, dir3.y);
		}
		else
		{
			desiredDir = Vector2.zero;
		}

		if (desiredDir.sqrMagnitude < 0.000001f)
			desiredDir = toGoal.normalized;

		float desiredW = Mathf.Max(0f, desiredDirectionWeight);
		float boidW = boidsEnabled ? Mathf.Max(0f, boidSteeringWeight) : 0f;

		Vector2 combined = desiredDir.sqrMagnitude > 0.000001f ? desiredDir.normalized * desiredW : Vector2.zero;

		if (boidsEnabled && boidW > 0.000001f)
		{
			Vector2 boidSteer = ComputeBoidSteering(pos);
			if (boidSteer.sqrMagnitude > 0.000001f)
			{
				combined += boidSteer.normalized * boidW;
			}
		}

		Vector2 combinedDir;
		if (combined.sqrMagnitude > 0.000001f)
		{
			combinedDir = combined.normalized;
		}
		else
		{
			combinedDir = toGoal.sqrMagnitude > 0.000001f ? toGoal.normalized : Vector2.zero;
		}

		float slowMultiplier = 1f - _slowPercentage;
		float effectiveSpeed = Mathf.Max(0f, speed) * Mathf.Clamp01(slowMultiplier);

		// Rotate toward the desired direction at rotationSpeed degrees/second.
		if (combinedDir.sqrMagnitude >= minDirForRotation)
		{
			float targetAngle = Mathf.Atan2(combinedDir.y, combinedDir.x) * Mathf.Rad2Deg - 90f;
			float currentAngle = rotationTransform != null ? rotationTransform.eulerAngles.z : transform.eulerAngles.z;
			float rotationMultiplier = GetWallDistanceRotationMultiplier(pos);
			float effectiveRotationSpeed = Mathf.Max(0f, rotationSpeed) * rotationMultiplier;
			float newAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, effectiveRotationSpeed * dt);
			if (rotationTransform != null) rotationTransform.rotation = Quaternion.Euler(0f, 0f, newAngle);
			else transform.rotation = Quaternion.Euler(0f, 0f, newAngle);
		}

		// Accelerate forward speed (along the configured rotation transform's up axis) toward effectiveSpeed.
		_currentSpeed = Mathf.MoveTowards(_currentSpeed, effectiveSpeed, Mathf.Max(0f, acceleration) * dt);
		Vector2 moveForward = rotationTransform != null ? (Vector2)rotationTransform.up : (Vector2)transform.up;
		_rb.linearVelocity = moveForward * _currentSpeed;
	}

	private float GetWallDistanceRotationMultiplier(Vector2 worldPos)
	{
		if (!wallDistanceAffectsRotationSpeed) return 1f;
		if (grid == null) return 1f;
		if (!grid.TryWorldToCell(worldPos, out var cellIdx)) return 1f;
		if (!grid.TryGetCell(cellIdx.x, cellIdx.y, out var cell)) return 1f;

		// NearestBlocked is tracked in grid-cell units; convert to world units for stable tuning.
		float gridSpacing = Mathf.Max(0.0001f, grid.GetSpacing());
		float nearestWallDistanceWorld = Mathf.Max(0f, cell.NearestBlocked) * gridSpacing;
		float clampedDistance = Mathf.Max(minWallDistanceForRotation, nearestWallDistanceWorld);
		float baseRatio = Mathf.Max(0.01f, rotationWallDistanceReference) / clampedDistance;
		float rawMultiplier = Mathf.Pow(baseRatio, Mathf.Max(0.1f, rotationWallProximityExponent));

		float minMul = Mathf.Max(0f, minWallDistanceRotationMultiplier);
		float maxMul = Mathf.Max(minMul, maxWallDistanceRotationMultiplier);
		return Mathf.Clamp(rawMultiplier, minMul, maxMul);
	}

	private Vector2 ComputeBoidSteering(Vector2 selfPos)
	{
		if (_neighbors.Count == 0) return Vector2.zero;

		Vector2 separation = Vector2.zero;
		Vector2 alignment = Vector2.zero;
		Vector2 cohesion = Vector2.zero;

		int countForAlign = 0;
		int countForCohesion = 0;

		float sepDist = Mathf.Max(0.001f, separationDistance);
		float sepDistSqr = sepDist * sepDist;

		for (int i = 0; i < _neighbors.Count; i++)
		{
			var other = _neighbors[i];
			if (other == null) continue;

			Vector2 otherPos = other.rb != null ? other.rb.position : (Vector2)other.transform.position;
			Vector2 toOther = otherPos - selfPos;
			float d2 = toOther.sqrMagnitude;

			if (d2 > 0.000001f && d2 < sepDistSqr)
			{
				separation -= toOther.normalized * (sepDist / Mathf.Sqrt(d2));
			}

			if (other.rb != null)
			{
				Vector2 v = other.rb.linearVelocity;
				if (v.sqrMagnitude > 0.000001f)
				{
					alignment += v.normalized;
					countForAlign++;
				}
			}

			cohesion += otherPos;
			countForCohesion++;
		}

		Vector2 steer = Vector2.zero;

		if (separation.sqrMagnitude > 0.000001f)
			steer += separation.normalized * Mathf.Max(0f, separationWeight);

		if (countForAlign > 0)
		{
			alignment /= countForAlign;
			if (alignment.sqrMagnitude > 0.000001f)
				steer += alignment.normalized * Mathf.Max(0f, alignmentWeight);
		}

		if (countForCohesion > 0)
		{
			Vector2 center = cohesion / countForCohesion;
			Vector2 toCenter = center - selfPos;
			if (toCenter.sqrMagnitude > 0.000001f)
				steer += toCenter.normalized * Mathf.Max(0f, cohesionWeight);
		}

		return steer;
	}

}

