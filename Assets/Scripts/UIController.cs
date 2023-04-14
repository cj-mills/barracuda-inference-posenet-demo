using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIController : MonoBehaviour
{
    [Header("UI Components")]
    [SerializeField] private TextMeshProUGUI posesDetectedText;
    [SerializeField] private TextMeshProUGUI fpsText;
    [SerializeField] private Slider confidenceThresholdSlider;

    [Header("Settings")]
    [SerializeField, Tooltip("On-screen text color")]
    private Color textColor = Color.black;
    [SerializeField, Tooltip("Option to display number of poses detected")]
    public bool displayPoseCount = true;
    [SerializeField, Tooltip("Option to display fps")]
    private bool displayFPS = true;
    [SerializeField, Tooltip("Time in seconds between refreshing fps value"), Range(0.01f, 1.0f)]
    private float fpsRefreshRate = 0.1f;

    private float fpsTimer = 0f;

    public void Update()
    {
        // Update FPS text
        if (displayFPS)
        {
            UpdateFPS();
        }
        else
        {
            fpsText.gameObject.SetActive(false);
        }
    }

    public void UpdateUI(int poseCount)
    {
        // Update object count text
        if (displayPoseCount)
        {
            posesDetectedText.gameObject.SetActive(true);
            posesDetectedText.text = $"Poses Detected: {poseCount}";
            posesDetectedText.color = textColor;
        }
        else
        {
            posesDetectedText.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Updates the displayed FPS value.
    /// </summary>
    private void UpdateFPS()
    {
        if (Time.unscaledTime > fpsTimer)
        {
            int fps = (int)(1f / Time.unscaledDeltaTime);
            fpsText.text = $"FPS: {fps}";
            fpsText.color = textColor;

            fpsTimer = Time.unscaledTime + fpsRefreshRate;
        }
    }
}
