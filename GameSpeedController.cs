using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameSpeedController : MonoBehaviour
{
    public enum GameSpeed
    {
        Slow,
        Normal,
        Fast,
        Fastest
    }

    [Header("Speed Ratios")]
    public float slowRatio = 0.5f;
    public float normalRatio = 1f;
    public float fastRatio = 1.5f;
    public float fastestRatio = 2f;

    [Header("UI References")]
    public Slider speedSlider;
    public TextMeshProUGUI speedText;

    private GameSpeed currentSpeed = GameSpeed.Normal;

    void Start()
    {
        SetSpeed(GameSpeed.Normal);

        if (speedSlider != null)
        {
            speedSlider.minValue = 0;
            speedSlider.maxValue = 3;
            speedSlider.wholeNumbers = true;
            speedSlider.value = (int)currentSpeed;
            speedSlider.onValueChanged.AddListener(OnSliderValueChanged);
        }

        UpdateSpeedText();
    }

    public void SetSpeed(GameSpeed speed)
    {
        currentSpeed = speed;

        switch (speed)
        {
            case GameSpeed.Slow:
                Time.timeScale = slowRatio;
                break;
            case GameSpeed.Normal:
                Time.timeScale = normalRatio;
                break;
            case GameSpeed.Fast:
                Time.timeScale = fastRatio;
                break;
            case GameSpeed.Fastest:
                Time.timeScale = fastestRatio;
                break;
        }

        if (speedSlider != null && speedSlider.value != (int)speed)
        {
            speedSlider.value = (int)speed;
        }

        UpdateSpeedText();
    }

    private void OnSliderValueChanged(float value)
    {
        SetSpeed((GameSpeed)(int)value);
    }

    private void UpdateSpeedText()
    {
        if (speedText != null)
        {
            speedText.text = $"{currentSpeed}";
        }
    }

    public void SetSlow()
    {
        SetSpeed(GameSpeed.Slow);
    }

    public void SetNormal()
    {
        SetSpeed(GameSpeed.Normal);
    }

    public void SetFast()
    {
        SetSpeed(GameSpeed.Fast);
    }

    public void SetFastest()
    {
        SetSpeed(GameSpeed.Fastest);
    }

    public GameSpeed GetCurrentSpeed()
    {
        return currentSpeed;
    }

    public float GetCurrentSpeedRatio()
    {
        return Time.timeScale;
    }
}
