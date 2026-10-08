using System.Collections;
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
    /// Over Quest Link the OpenXR session becomes active seconds
    /// after Start, and only once the headset is worn. So with an
    /// active XR loader this waits up to <see cref="headsetWaitSeconds"/>
    /// before falling back, and keeps watching afterwards: when the
    /// headset display starts, the simulator is destroyed (which
    /// removes its simulated HMD and controllers), so the real
    /// headset drives the camera and controllers.
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

        [Tooltip(
            "With an active XR loader, how long to wait for the " +
            "headset display before falling back to the simulator."
        )]
        [SerializeField]
        [Min(0.0f)]
        private float headsetWaitSeconds =
            15.0f;


        private GameObject simulator;


        private IEnumerator Start()
        {
            if (!Application.isEditor ||
                simulatorPrefab == null)
            {
                yield break;
            }


            bool loaderActive =
                XRGeneralSettings.Instance != null &&
                XRGeneralSettings.Instance.Manager != null &&
                XRGeneralSettings.Instance.Manager.activeLoader != null;


            if (loaderActive &&
                !forceSimulator)
            {
                float deadline =
                    Time.realtimeSinceStartup + headsetWaitSeconds;

                while (Time.realtimeSinceStartup < deadline)
                {
                    if (XRSettings.isDeviceActive)
                    {
                        Debug.Log(
                            $"XRSimulatorFallback: headset active " +
                            $"('{XRSettings.loadedDeviceName}'), " +
                            "simulator not started.",
                            this
                        );

                        yield break;
                    }

                    yield return null;
                }
            }


            simulator =
                Instantiate(simulatorPrefab);

            simulator.name =
                simulatorPrefab.name;

            // The look-at-wrist gesture is impractical with the
            // simulator: keep the wrist menu shown.
            SetWristMenuAutoShow(false);


            string reason =
                forceSimulator
                    ? "forced"
                    : loaderActive
                        ? $"XR loader active but no headset display after {headsetWaitSeconds:0} s; " +
                          "it is removed when the headset starts"
                        : "no XR loader active";

            Debug.Log(
                $"XRSimulatorFallback: no headset active ({reason}), " +
                "XR Device Simulator started (mouse + keyboard).",
                this
            );


            if (!loaderActive ||
                forceSimulator)
            {
                yield break;
            }


            // Headset put on (or Link session started) after the
            // fallback: hand control back to the real device.
            while (!XRSettings.isDeviceActive)
            {
                yield return null;
            }

            Destroy(simulator);

            simulator = null;

            SetWristMenuAutoShow(true);

            Debug.Log(
                $"XRSimulatorFallback: headset became active " +
                $"('{XRSettings.loadedDeviceName}'), simulator removed.",
                this
            );
        }


        private static void SetWristMenuAutoShow(
            bool autoShow
        )
        {
            XRHandMenu menu =
                FindFirstObjectByType<XRHandMenu>();

            if (menu != null)
            {
                menu.AutoShow =
                    autoShow;
            }
        }
    }
}
