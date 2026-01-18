using UnityEngine;
using UnityEngine.InputSystem;

public class CameraSwitcher : MonoBehaviour
{
    [Header("Camera References")]
    [SerializeField] private Camera worldCam;
    [SerializeField] private Camera turretCam;

    private bool isWorldCamActive = true;
    private Keyboard keyboard;

    private void Start()
    {
        if (!worldCam)
        {
            Debug.LogError("CameraSwitcher: World camera not assigned!");
            return;
        }

        if (!turretCam)
        {
            Debug.LogError("CameraSwitcher: Turret camera not assigned!");
            return;
        }

        // Start with world camera
        SetWorldCamera();
        Debug.Log("CameraSwitcher started. Press C to toggle WorldCam/TurretCam.");
    }

    private void Update()
    {
        if (keyboard == null)
        {
            keyboard = Keyboard.current;
            return;
        }

        // C key toggles between WorldCam and TurretCam
        if (keyboard.cKey.wasPressedThisFrame)
        {
            ToggleCamera();
        }
    }

    private void ToggleCamera()
    {
        if (isWorldCamActive)
        {
            SetTurretCamera();
        }
        else
        {
            SetWorldCamera();
        }
    }

    private void SetWorldCamera()
    {
        // Disable turret cam
        turretCam.enabled = false;
        DisableAudioListener(turretCam);

        // Enable world cam
        worldCam.enabled = true;
        EnableAudioListener(worldCam);

        isWorldCamActive = true;
        Debug.Log("Switched to: World Camera");
    }

    private void SetTurretCamera()
    {
        // Disable world cam
        worldCam.enabled = false;
        DisableAudioListener(worldCam);

        // Enable turret cam
        turretCam.enabled = true;
        EnableAudioListener(turretCam);

        isWorldCamActive = false;
        Debug.Log("Switched to: Turret Camera");
    }

    private void EnableAudioListener(Camera cam)
    {
        var listener = cam.GetComponent<AudioListener>();
        if (listener) listener.enabled = true;
    }

    private void DisableAudioListener(Camera cam)
    {
        var listener = cam.GetComponent<AudioListener>();
        if (listener) listener.enabled = false;
    }
}
