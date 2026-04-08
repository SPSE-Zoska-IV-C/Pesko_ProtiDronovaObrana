using UnityEngine;

[DefaultExecutionOrder(-500)]
public class DroneDifficultyManager : MonoBehaviour
{
    public enum Difficulty { Easy = 0, Medium = 1, Hard = 2 }
    public Difficulty difficulty = Difficulty.Easy;

    [Header("Apply Settings")]
    public bool includeInactive = true;
    public bool applyOnAwake = true;

    void Awake()
    {
        if (applyOnAwake)
            ApplyToAllDrones();
    }

    [ContextMenu("Apply To All Drones (Now)")]
    public void ApplyToAllDrones()
    {
        // Find all drone controllers in the scene
        var drones = Object.FindObjectsByType<MLDroneController>(
            includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
            FindObjectsSortMode.None
        );

        Debug.Log($"[DroneDifficultyManager] Found {drones.Length} drones. Applying {difficulty} difficulty.");

        foreach (var d in drones)
        {
            if (!d) continue;
            ApplyPreset(d, difficulty);
            d.ResetDronePosition();  // Respawn with new bounds
        }
    }

    void ApplyPreset(MLDroneController drone, Difficulty d)
    {
        // Smaller spawn bounds - drone stays closer to turret

        if (d == Difficulty.Easy)
        {
            // EASY: Very small spawn area (very close, very easy to hit)
            drone.xBounds = new Vector2(-15f, 15f);
            drone.zBounds = new Vector2(-15f, 15f);
            drone.yBounds = new Vector2(5f, 12f);
            drone.marginXZ = 3f;
            drone.yMarginFromEdge = 1f;
        }
        else if (d == Difficulty.Medium)
        {
            // MEDIUM: Small-medium spawn area
            drone.xBounds = new Vector2(-25f, 25f);
            drone.zBounds = new Vector2(-25f, 25f);
            drone.yBounds = new Vector2(4f, 15f);
            drone.marginXZ = 5f;
            drone.yMarginFromEdge = 2f;
        }
        else // Hard
        {
            // HARD: Medium spawn area (still challenging but not too far)
            drone.xBounds = new Vector2(-35f, 35f);
            drone.zBounds = new Vector2(-35f, 35f);
            drone.yBounds = new Vector2(3f, 18f);
            drone.marginXZ = 7f;
            drone.yMarginFromEdge = 3f;
        }
    }
}
