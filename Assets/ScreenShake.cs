using UnityEngine;

public class ScreenShake : MonoBehaviour
{
	public enum IntensityBlendMode
	{
		Add,
		Max,
		Override
	}

	public static ScreenShake Instance { get; private set; }

	[Header("Default Trigger Values")]
	[SerializeField] private float defaultIntensity = 0.45f;
	[SerializeField] private float defaultDuration = 0.2f;

	[Header("Shake Tuning")]
	[SerializeField] private IntensityBlendMode blendMode = IntensityBlendMode.Add;
	[SerializeField] private bool stackDuration = false;
	[SerializeField] private bool restartEnvelopeOnTrigger = true;
	[SerializeField] private bool useUnscaledTime = true;
	[SerializeField] private float maxIntensity = 1.5f;
	[SerializeField] private float intensityDecayPerSecond = 0f;
	[SerializeField] private float noiseFrequency = 28f;
	[SerializeField] private AnimationCurve intensityOverLifetime = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

	[Header("Position")]
	[SerializeField] private bool shakePosition = true;
	[SerializeField] private Vector3 maxPositionOffset = new Vector3(0.22f, 0.22f, 0f);

	[Header("Rotation")]
	[SerializeField] private bool shakeRotation = false;
	[SerializeField] private Vector3 maxRotationOffset = new Vector3(2f, 2f, 1f);

	private float currentIntensity;
	private float shakeDuration;
	private float shakeTimeRemaining;
	private float noiseClock;
	private float noiseSeedX;
	private float noiseSeedY;
	private float noiseSeedZ;
	private Vector3 appliedPositionOffset;
	private Quaternion appliedRotationOffset = Quaternion.identity;

	public static void Trigger(float intensity = -1f, float duration = -1f)
	{
		if (Instance == null)
		{
			return;
		}

		Instance.Play(intensity, duration);
	}

	private void Awake()
	{
		if (Instance != null && Instance != this)
		{
			Destroy(this);
			return;
		}

		Instance = this;
		noiseSeedX = Random.Range(-1000f, 1000f);
		noiseSeedY = Random.Range(-1000f, 1000f);
		noiseSeedZ = Random.Range(-1000f, 1000f);
	}

	private void LateUpdate()
	{
		if (shakeTimeRemaining <= 0f || currentIntensity <= 0f)
		{
			ClearAppliedOffsets();
			return;
		}

		float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
		shakeTimeRemaining = Mathf.Max(0f, shakeTimeRemaining - deltaTime);

		if (intensityDecayPerSecond > 0f)
		{
			currentIntensity = Mathf.Max(0f, currentIntensity - (intensityDecayPerSecond * deltaTime));
		}

		float normalizedTime = 1f - Mathf.Clamp01(shakeTimeRemaining / Mathf.Max(0.0001f, shakeDuration));
		float envelope = intensityOverLifetime.Evaluate(normalizedTime);
		float effectiveIntensity = Mathf.Clamp(currentIntensity * envelope, 0f, maxIntensity);
		ApplyShake(effectiveIntensity, deltaTime);
	}

	private void OnDisable()
	{
		ClearAppliedOffsets();
	}

	private void OnDestroy()
	{
		if (Instance == this)
		{
			Instance = null;
		}
	}

	public void Play(float intensity = -1f, float duration = -1f)
	{
		float resolvedIntensity = intensity < 0f ? defaultIntensity : intensity;
		float resolvedDuration = duration < 0f ? defaultDuration : duration;

		if (resolvedIntensity <= 0f || resolvedDuration <= 0f)
		{
			return;
		}

		switch (blendMode)
		{
			case IntensityBlendMode.Add:
				currentIntensity += resolvedIntensity;
				break;
			case IntensityBlendMode.Max:
				currentIntensity = Mathf.Max(currentIntensity, resolvedIntensity);
				break;
			case IntensityBlendMode.Override:
				currentIntensity = resolvedIntensity;
				break;
		}

		currentIntensity = Mathf.Clamp(currentIntensity, 0f, maxIntensity);

		if (stackDuration)
		{
			shakeTimeRemaining += resolvedDuration;
		}
		else
		{
			shakeTimeRemaining = Mathf.Max(shakeTimeRemaining, resolvedDuration);
		}

		if (restartEnvelopeOnTrigger || shakeDuration < shakeTimeRemaining)
		{
			shakeDuration = shakeTimeRemaining;
		}
	}

	public void Stop(bool clearTransformOffsets = true)
	{
		currentIntensity = 0f;
		shakeTimeRemaining = 0f;
		shakeDuration = 0f;

		if (clearTransformOffsets)
		{
			ClearAppliedOffsets();
		}
	}

	private void ApplyShake(float intensity, float deltaTime)
	{
		ClearAppliedOffsets();
		noiseClock += deltaTime * noiseFrequency;

		float noiseX = (Mathf.PerlinNoise(noiseSeedX, noiseClock) * 2f) - 1f;
		float noiseY = (Mathf.PerlinNoise(noiseSeedY, noiseClock) * 2f) - 1f;
		float noiseZ = (Mathf.PerlinNoise(noiseSeedZ, noiseClock) * 2f) - 1f;

		if (shakePosition)
		{
			appliedPositionOffset = Vector3.Scale(maxPositionOffset, new Vector3(noiseX, noiseY, noiseZ)) * intensity;
			transform.localPosition += appliedPositionOffset;
		}

		if (shakeRotation)
		{
			Vector3 eulerOffset = Vector3.Scale(maxRotationOffset, new Vector3(noiseX, noiseY, noiseZ)) * intensity;
			appliedRotationOffset = Quaternion.Euler(eulerOffset);
			transform.localRotation *= appliedRotationOffset;
		}
	}

	private void ClearAppliedOffsets()
	{
		if (appliedPositionOffset != Vector3.zero)
		{
			transform.localPosition -= appliedPositionOffset;
			appliedPositionOffset = Vector3.zero;
		}

		if (appliedRotationOffset != Quaternion.identity)
		{
			transform.localRotation *= Quaternion.Inverse(appliedRotationOffset);
			appliedRotationOffset = Quaternion.identity;
		}
	}
}
