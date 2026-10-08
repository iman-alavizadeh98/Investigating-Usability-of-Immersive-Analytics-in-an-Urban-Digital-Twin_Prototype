using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Management;

namespace UrbanAnalytics.XR
{
    /// <summary>
    /// Development aid: in the Editor, when no headset is running
    /// (no active XR loader or no XR display), spawns the XR Device
    /// Simulator so the VR scene can be tested with mouse and
    /// keyboard. With a headset (Quest Link) nothing happens.
    ///
    /// Never active in a player build.
    /// </summary>
    public sealed class XRSimulatorFallback :
        MonoBehaviour
    {
        [Tooltip(
            "XR Device Simulator prefab (XRI sample " +
            "'XR Device Simulator')."
        )]
        [SerializeField]
        private GameObject simulatorPrefab;

        [Tooltip("Spawn the simulator even when a headset is active.")]
        [SerializeField]
        private bool forceSimulator;


        private void Start()
        {
            if (!Application.isEditor ||
                simulatorPrefab == null)
            {
                return;
            }


            bool headsetActive =
                XRGeneralSettings.Instance != null &&
                XRGeneralSettings.Instance.Manager != null &&
                XRGeneralSettings.Instance.Manager.activeLoader != null &&
                XRSettings.isDeviceActive;


            if (headsetActive &&
                !forceSimulator)
            {
                Debug.Log(
                    $"XRSimulatorFallback: headset active " +
                    $"('{XRSettings.loadedDeviceName}'), " +
                    "simulator not started.",
                    this
                );

                return;
            }


            Instantiate(
                simulatorPrefab
            ).name =
                simulatorPrefab.name;


            Debug.Log(
                "XRSimulatorFallback: no headset active, XR Device " +
                "Simulator started (mouse + keyboard).",
                this
            );
        }
    }
}
