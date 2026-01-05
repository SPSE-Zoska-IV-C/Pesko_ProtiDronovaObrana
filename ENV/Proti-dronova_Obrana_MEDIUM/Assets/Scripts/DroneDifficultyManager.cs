using System.Collections;
using UnityEngine;

public static class DroneMediumPresetGlobal
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        Debug.Log("[DroneMediumPresetGlobal] Init: forcing MEDIUM drone settings.");

        var go = new GameObject("DroneMediumPresetGlobalRunner");
        Object.DontDestroyOnLoad(go);
        go.hideFlags = HideFlags.HideAndDontSave;
        go.AddComponent<Runner>();
    }

    private class Runner : MonoBehaviour
    {
        IEnumerator Start()
        {
            // Wait a couple frames so MLDroneController Awake/Start have time to init. [web:114]
            yield return null;
            yield return null;

            // Apply repeatedly for a few frames to catch late-enabled/spawned drones.
            for (int i = 0; i < 10; i++)
            {
                ApplyMedium(i);
                yield return null;
            }
        }

        static void ApplyMedium(int pass)
        {
            var drones = Object.FindObjectsByType<MLDroneController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            ); // [web:24]

            Debug.Log($"[DroneMediumPresetGlobal] Pass {pass}: found {drones.Length} drone(s).");

            foreach (var d in drones)
            {
                if (!d) continue;

                // MEDIUM PRESET
                d.xBounds = new Vector2(-35f, 35f);
                d.zBounds = new Vector2(-35f, 35f);

                d.baseSpeed = 5f;
                d.turnSpeed = 70f;
                d.changeInterval = 2.5f;

                d.randomSpread = 20f;
                d.boundarySpread = 12f;
                d.noiseYawAmplitude = 4f;
                d.noiseYawSpeed = 0.4f;

                d.baseVerticalJitter = 1.0f;
                d.verticalChangeInterval = 3.0f;
                d.verticalJitterNoise = 0.2f;

                d.ResetDronePosition();
            }
        }
    }
}
