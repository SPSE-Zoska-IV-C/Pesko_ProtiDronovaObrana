using UnityEngine;
using UnityEngine.InputSystem;

public class CameraSwitcher : MonoBehaviour
{
    [Header("Camera References")]
    [SerializeField] private Camera worldCam;
    [SerializeField] private Camera turretCam;
    
    private Keyboard keyboard;

    private void Start()
    {
        if (!worldCam || !turretCam)
        {
            Debug.LogError("CameraSwitcher: Cameras not assigned!");
            return;
        }
        SetActiveCamera(world: true);
        Debug.Log("CameraSwitcher started. Press C to toggle.");
    }

    private void Update()
    {
        // Get keyboard reference
        if (keyboard == null)
        {
            keyboard = Keyboard.current;
            return; // Wait for next frame if keyboard not ready
        }

        // Check for C key press
        if (keyboard.cKey.wasPressedThisFrame)
        {
            ToggleCamera();
        }
    }

    private void ToggleCamera()
    {
        bool switchToWorldCam = !worldCam.enabled;
        SetActiveCamera(switchToWorldCam);
        Debug.Log("Camera switched to: " + (worldCam.enabled ? "WorldCam" : "TurretCam"));
    }

    private void SetActiveCamera(bool world)
    {
        worldCam.enabled = world;
        turretCam.enabled = !world;

        // Handle audio listeners
        var wl = worldCam.GetComponent<AudioListener>();
        var tl = turretCam.GetComponent<AudioListener>();
        if (wl) wl.enabled = world;
        if (tl) tl.enabled = !world;
    }
}