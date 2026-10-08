using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Interaction.Toolkit.Inputs;

using UrbanAnalytics.Study;

namespace UrbanAnalytics.XR
{
    /// <summary>
    /// Lets the user choose the input: controllers only, or controllers
    /// and hand tracking (XRI's XRInputModalityManager then switches to
    /// the tracked hands when the controllers are put down).
    ///
    /// Also logs the hand-tracking state whenever it changes (hand
    /// subsystem running, left/right hand tracked, the modality manager's
    /// current input mode), so a headset test shows why hands do or do
    /// not appear.
    ///
    /// The choice is logged as the study event vr_hands.
    /// </summary>
    public sealed class XRInputModeSwitch :
        MonoBehaviour
    {
        [SerializeField]
        private XRInputModalityManager modalityManager;

        [Tooltip(
            "Start with hand tracking allowed. Off by default (decided " +
            "2026-10-08): the study uses the controllers; hand tracking over " +
            "Link was not working reliably and is kept as an opt-in " +
            "(toolbar Hands)."
        )]
        [SerializeField]
        private bool handsEnabled =
            false;

        [SerializeField]
        private StudySession studySession;


        private static readonly List<XRHandSubsystem> Subsystems =
            new List<XRHandSubsystem>();

        private GameObject leftHand;

        private GameObject rightHand;

        private string lastState;

        private bool simulatorMode;


        /// <summary>
        /// Set by XRSimulatorFallback while the XR Device Simulator runs:
        /// the modality manager does not count simulated controllers as
        /// tracked and would switch every controller and hand off, so
        /// the controllers are forced on instead.
        /// </summary>
        public bool SimulatorMode
        {
            get => simulatorMode;
            set
            {
                simulatorMode =
                    value;

                Apply();
            }
        }


        public bool HandsEnabled
        {
            get => handsEnabled;
            set
            {
                if (handsEnabled == value)
                {
                    return;
                }

                handsEnabled =
                    value;

                Apply();

                Debug.Log(
                    $"XRInputModeSwitch: hand tracking {(value ? "on" : "off (controllers only)")}.",
                    this
                );

                studySession?.LogEvent(
                    "vr_hands",
                    ("enabled", value)
                );
            }
        }


        private void Awake()
        {
            if (modalityManager == null)
            {
                modalityManager =
                    FindFirstObjectByType<XRInputModalityManager>();
            }

            if (studySession == null)
            {
                studySession =
                    FindFirstObjectByType<StudySession>();
            }

            if (modalityManager != null)
            {
                leftHand =
                    modalityManager.leftHand;

                rightHand =
                    modalityManager.rightHand;
            }
        }


        private void Start()
        {
            Apply();
        }


        private void Update()
        {
            LogStateChanges();
        }


        /// <summary>
        /// Off: the hands are taken out of the modality manager and
        /// hidden, so the controllers stay in charge. On: restored; the
        /// manager is re-enabled so it re-evaluates what is tracked.
        /// </summary>
        private void Apply()
        {
            if (modalityManager == null)
            {
                return;
            }


            modalityManager.enabled =
                false;


            if (simulatorMode)
            {
                SetActive(modalityManager.leftController, true);

                SetActive(modalityManager.rightController, true);

                SetActive(leftHand, false);

                SetActive(rightHand, false);

                return;
            }


            modalityManager.leftHand =
                handsEnabled ? leftHand : null;

            modalityManager.rightHand =
                handsEnabled ? rightHand : null;

            if (!handsEnabled)
            {
                if (leftHand != null)
                {
                    leftHand.SetActive(false);
                }

                if (rightHand != null)
                {
                    rightHand.SetActive(false);
                }
            }

            modalityManager.enabled =
                true;
        }


        private static void SetActive(
            GameObject target,
            bool active
        )
        {
            if (target != null &&
                target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }


        private void LogStateChanges()
        {
            XRHandSubsystem subsystem =
                null;

            SubsystemManager.GetSubsystems(Subsystems);

            foreach (XRHandSubsystem candidate in Subsystems)
            {
                if (candidate.running)
                {
                    subsystem =
                        candidate;

                    break;
                }
            }


            string state =
                subsystem == null
                    ? $"no running hand subsystem ({Subsystems.Count} found)"
                    : $"hand subsystem running, left tracked={subsystem.leftHand.isTracked}, " +
                      $"right tracked={subsystem.rightHand.isTracked}";

            state +=
                $", input mode={XRInputModalityManager.currentInputMode.Value}, " +
                $"hands {(handsEnabled ? "allowed" : "off")}";


            if (state != lastState)
            {
                lastState =
                    state;

                Debug.Log(
                    "XRInputModeSwitch: " + state + ".",
                    this
                );
            }
        }
    }
}
