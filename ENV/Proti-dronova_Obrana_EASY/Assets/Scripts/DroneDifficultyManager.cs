using System.Collections;
using UnityEngine;

public static class DroneEasyPresetGlobal
{
    // Runs automatically on play. AfterSceneLoad is a supported load type. [web:114]
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        Debug.Log("[DroneEasyPresetGlobal] Init: forcing EASY drone settings.");

        var go = new GameObject("DroneEasyPresetGlobalRunner");
        Object.DontDestroyOnLoad(go);
        go.hideFlags = HideFlags.HideAndDontSave;
        go.AddComponent<Runner>();
    }

    private class Runner : MonoBehaviour
    {
        IEnumerator Start()
        {
            // Wait a couple frames to avoid racing object initialization,
            // then apply repeatedly to catch late-enabled/spawned drones. [web:81]
            yield return null;
            yield return null;

            for (int i = 0; i < 10; i++)
            {
                ApplyEasy(i);
                yield return null;
            }
        }

        static void ApplyEasy(int pass)
        {
            var drones = Object.FindObjectsByType<MLDroneController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            ); // [web:24]

            Debug.Log($"[DroneEasyPresetGlobal] Pass {pass}: found {drones.Length} drone(s).");

            foreach (var d in drones)
            {
                if (!d) continue;

                d.xBounds = new Vector2(-20f, 20f);
                d.zBounds = new Vector2(-20f, 20f);

                d.baseSpeed = 3f;
                d.turnSpeed = 40f;
                d.changeInterval = 3.5f;

                d.randomSpread = 8f;
                d.boundarySpread = 4f;
                d.noiseYawAmplitude = 2f;
                d.noiseYawSpeed = 0.25f;

                d.baseVerticalJitter = 0.5f;
                d.verticalChangeInterval = 4.0f;
                d.verticalJitterNoise = 0.1f;

                d.ResetDronePosition(); // now safe
            }
        }
    }
}
