using UnityEngine;
using TMPro;
using System.Collections;

public class TrainingStatsUI : MonoBehaviour
{
    [Header("References")]
    public TextMeshProUGUI statsText;
    public CanvasGroup flashPanel;

    [Header("Flash Settings")]
    public float flashDuration = 0.2f;
    public Color flashColor = new Color(0, 1, 0, 0.3f);

    private int totalHits = 0;
    private int totalShots = 0;

    void Start()
    {
        if (flashPanel) flashPanel.alpha = 0f;
        UpdateDisplay();
    }

    public void UpdateHitStats(int hits, int shots)
    {
        totalHits = hits;
        totalShots = shots;
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        if (!statsText) return;

        float accuracy = totalShots > 0 ? (float)totalHits / totalShots * 100f : 0f;

        statsText.text = $"HITS: {totalHits}\n" +
                        $"SHOTS: {totalShots}\n" +
                        $"ACCURACY: {accuracy:F1}%";
    }

    public void TriggerHitFlash()
    {
        if (flashPanel)
            StartCoroutine(FlashCoroutine());
    }

    private IEnumerator FlashCoroutine()
    {
        flashPanel.alpha = 1f;
        yield return new WaitForSeconds(flashDuration);
        flashPanel.alpha = 0f;
    }
}
