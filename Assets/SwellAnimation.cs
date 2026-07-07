using System;
using System.Collections;
using UnityEngine;

public class SwellAnimation : MonoBehaviour
{
    public float swellInDuration = 0.3f;
    public float swellOutDuration = 0.2f;
    public bool swellInOnEnable = true;

    private Vector3 _initialScale;
    private Coroutine _activeRoutine;

    private void Awake()
    {
        _initialScale = transform.localScale;
    }

    private void OnEnable()
    {
        if (swellInOnEnable)
        {
            transform.localScale = Vector3.zero;
            SwellIn();
        }
    }

    private void OnDisable()
    {
        if (_activeRoutine != null)
        {
            StopCoroutine(_activeRoutine);
            _activeRoutine = null;
        }
    }

    public void SwellIn(Action onComplete = null)
    {
        if (_activeRoutine != null) StopCoroutine(_activeRoutine);
        _activeRoutine = StartCoroutine(SwellRoutine(_initialScale, swellInDuration, onComplete));
    }

    public void CancelAndReset()
    {
        swellInOnEnable = false;
        if (_activeRoutine != null)
        {
            StopCoroutine(_activeRoutine);
            _activeRoutine = null;
        }
        transform.localScale = _initialScale;
    }

    public void SwellOut(bool destroy = false, Action onComplete = null)
    {
        if (_activeRoutine != null) StopCoroutine(_activeRoutine);
        _activeRoutine = StartCoroutine(SwellRoutine(Vector3.zero, swellOutDuration, () =>
        {
            onComplete?.Invoke();
            if (destroy) Destroy(gameObject);
        }));
    }

    private IEnumerator SwellRoutine(Vector3 target, float duration, Action onComplete)
    {
        Vector3 from = transform.localScale;

        if (duration <= 0f)
        {
            transform.localScale = target;
            _activeRoutine = null;
            onComplete?.Invoke();
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = t * t * (3f - 2f * t);
            transform.localScale = Vector3.Lerp(from, target, t);
            yield return null;
        }

        transform.localScale = target;
        _activeRoutine = null;
        onComplete?.Invoke();
    }
}
