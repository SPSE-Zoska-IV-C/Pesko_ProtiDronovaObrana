using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.MLAgents;

public class TrainingStatsUI : MonoBehaviour
{
    [Header("References")]
    public TextMeshProUGUI statsText;
    public TurretAgentML[] agents;
    public Image panelBackground;  // Add this - reference to the Panel's Image component

    [Header("Flash Settings")]
    public Color normalColor = new Color(0, 0, 0, 0.78f);  // Semi-transparent black
    public Color hitColor = Color.green;  // Bright green
    public float flashDuration = 0.2f;  // How long the flash lasts

    private int totalEpisodes = 0;
    private int totalSteps = 0;
    private float averageReward = 0f;
    private float flashTimer = 0f;
    private bool isFlashing = false;

    void Start()
    {
        // Auto-find panel background if not assigned
        if (panelBackground == null)
        {
            panelBackground = GetComponent<Image>();
        }

        if (panelBackground != null)
        {
            panelBackground.color = normalColor;
        }
    }

    void Update()
    {
        if (agents == null || agents.Length == 0) return;

        totalEpisodes = 0;
        totalSteps = 0;
        float rewardSum = 0f;
        int activeAgents = 0;

        foreach (var agent in agents)
        {
            if (agent != null)
            {
                totalSteps += agent.StepCount;
                rewardSum += agent.GetCumulativeReward();
                activeAgents++;
                totalEpisodes += agent.CompletedEpisodes;
            }
        }

        averageReward = activeAgents > 0 ? rewardSum / activeAgents : 0f;
        UpdateUI();
        UpdateFlash();
    }

    void UpdateUI()
    {
        if (statsText != null)
        {
            statsText.text =
                $"<b>Training Progress</b>\n\n" +
                $"Total Episodes: {totalEpisodes}\n" +
                $"Total Steps: {totalSteps:N0}\n" +
                $"Average Reward: {averageReward:F3}";
        }
    }

    void UpdateFlash()
    {
        if (isFlashing)
        {
            flashTimer -= Time.deltaTime;

            if (flashTimer <= 0f)
            {
                // Flash finished, return to normal
                isFlashing = false;
                if (panelBackground != null)
                {
                    panelBackground.color = normalColor;
                }
            }
            else
            {
                // Lerp from green back to normal
                float t = flashTimer / flashDuration;
                if (panelBackground != null)
                {
                    panelBackground.color = Color.Lerp(normalColor, hitColor, t);
                }
            }
        }
    }

    // Call this from TurretAgentML when a hit happens
    public void TriggerHitFlash()
    {
        isFlashing = true;
        flashTimer = flashDuration;
        if (panelBackground != null)
        {
            panelBackground.color = hitColor;
        }
    }
}
