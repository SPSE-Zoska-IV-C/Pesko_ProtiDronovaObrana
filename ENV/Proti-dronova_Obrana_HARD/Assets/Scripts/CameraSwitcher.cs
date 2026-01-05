using UnityEngine;
using UnityEngine.InputSystem;

public class CameraSwitcher : MonoBehaviour
{
    [Header("Camera References")]
    [SerializeField] private Camera worldCam;
    [SerializeField] private Camera[] envCameras = new Camera[25]; // 25 environment cameras

    private int currentCamIndex = -1; // -1 = world cam, 0-24 = env cams
    private Keyboard keyboard;

    private void Start()
    {
        if (!worldCam)
        {
            Debug.LogError("CameraSwitcher: World camera not assigned!");
            return;
        }

        // Start with world camera
        SetActiveCamera(-1);
        Debug.Log("CameraSwitcher started. Press C to cycle cameras, or number keys 0-9 for direct access.");
    }

    private void Update()
    {
        if (keyboard == null)
        {
            keyboard = Keyboard.current;
            return;
        }

        // C key cycles through all cameras
        if (keyboard.cKey.wasPressedThisFrame)
        {
            CycleCameras();
        }

        // Number keys for direct camera access
        // 0 = world cam, 1-9 = env cameras 1-9
        if (keyboard.digit0Key.wasPressedThisFrame) SetActiveCamera(-1);
        if (keyboard.digit1Key.wasPressedThisFrame) SetActiveCamera(0);
        if (keyboard.digit2Key.wasPressedThisFrame) SetActiveCamera(1);
        if (keyboard.digit3Key.wasPressedThisFrame) SetActiveCamera(2);
        if (keyboard.digit4Key.wasPressedThisFrame) SetActiveCamera(3);
        if (keyboard.digit5Key.wasPressedThisFrame) SetActiveCamera(4);
        if (keyboard.digit6Key.wasPressedThisFrame) SetActiveCamera(5);
        if (keyboard.digit7Key.wasPressedThisFrame) SetActiveCamera(6);
        if (keyboard.digit8Key.wasPressedThisFrame) SetActiveCamera(7);
        if (keyboard.digit9Key.wasPressedThisFrame) SetActiveCamera(8);

        // Arrow keys for prev/next
        if (keyboard.rightArrowKey.wasPressedThisFrame) NextCamera();
        if (keyboard.leftArrowKey.wasPressedThisFrame) PreviousCamera();
    }

    private void CycleCameras()
    {
        currentCamIndex++;

        // Wrap around: -1 (world) -> 0-24 (envs) -> back to -1
        if (currentCamIndex >= envCameras.Length)
        {
            currentCamIndex = -1;
        }

        SetActiveCamera(currentCamIndex);
    }

    private void NextCamera()
    {
        currentCamIndex++;
        if (currentCamIndex >= envCameras.Length) currentCamIndex = -1;
        SetActiveCamera(currentCamIndex);
    }

    private void PreviousCamera()
    {
        currentCamIndex--;
        if (currentCamIndex < -1) currentCamIndex = envCameras.Length - 1;
        SetActiveCamera(currentCamIndex);
    }

    private void SetActiveCamera(int index)
    {
        currentCamIndex = index;

        // Disable all cameras first
        DisableAllCameras();

        // Enable the selected camera
        if (index == -1)
        {
            // World camera
            worldCam.enabled = true;
            EnableAudioListener(worldCam);
            Debug.Log("Switched to: World Camera");
        }
        else if (index >= 0 && index < envCameras.Length && envCameras[index] != null)
        {
            // Environment camera
            envCameras[index].enabled = true;
            EnableAudioListener(envCameras[index]);
            Debug.Log($"Switched to: Environment Camera {index + 1}");
        }
    }

    private void DisableAllCameras()
    {
        // Disable world cam
        worldCam.enabled = false;
        DisableAudioListener(worldCam);

        // Disable all env cams
        for (int i = 0; i < envCameras.Length; i++)
        {
            if (envCameras[i] != null)
            {
                envCameras[i].enabled = false;
                DisableAudioListener(envCameras[i]);
            }
        }
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
