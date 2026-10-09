using System;
using UnityEngine;

using UrbanAnalytics.Interaction;
using UrbanAnalytics.Study;

namespace UrbanAnalytics.XR
{
    /// <summary>What a trigger click on the table does (toolbar).</summary>
    public enum XRClickTool
    {
        /// <summary>Select it: the Info panel shows it.</summary>
        Select,

        /// <summary>Select it and pop up a 3D copy (XRSelectionCopies).</summary>
        Copy,

        /// <summary>Select it and put its area on the Compare panel (A, then B).</summary>
        Compare
    }


    /// <summary>
    /// The table toolbar's click tools: Select, Copy or Compare. One
    /// click does the whole action (no "select first, then press
    /// copy"): every click on the city selects (InteractionManager) and
    /// then, depending on the tool, copies the clicked thing or adds it
    /// to the comparison. The tool stays on until another is chosen.
    ///
    /// Copy and Compare are independent: copies are 3D objects to place
    /// on or around the table; the comparison is a panel with the
    /// values of two areas side by side (XRComparePanel).
    ///
    /// Logged as the study events vr_tool (tool chosen) and vr_compare
    /// (entity, slot); copies log vr_copy themselves.
    /// </summary>
    public sealed class XRClickTools :
        MonoBehaviour
    {
        [Header("Systems (found automatically when empty)")]
        [SerializeField]
        private InteractionManager interactionManager;

        [SerializeField]
        private XRSelectionCopies copies;

        [SerializeField]
        private ComparisonManager comparisonManager;

        [SerializeField]
        private XRComparePanel comparePanel;

        [SerializeField]
        private StudySession studySession;


        private XRClickTool tool =
            XRClickTool.Select;


        public XRClickTool Tool =>
            tool;


        public event Action<XRClickTool> ToolChanged;

        /// <summary>A click added an area to the comparison (tutorial).</summary>
        public event Action Compared;


        private void Awake()
        {
            if (interactionManager == null)
            {
                interactionManager =
                    FindFirstObjectByType<InteractionManager>();
            }

            if (copies == null)
            {
                copies =
                    FindFirstObjectByType<XRSelectionCopies>();
            }

            if (comparisonManager == null)
            {
                comparisonManager =
                    FindFirstObjectByType<ComparisonManager>();
            }

            if (comparePanel == null)
            {
                comparePanel =
                    FindFirstObjectByType<XRComparePanel>();
            }

            if (studySession == null)
            {
                studySession =
                    FindFirstObjectByType<StudySession>();
            }
        }


        private void OnEnable()
        {
            if (interactionManager != null)
            {
                interactionManager.EntityClicked +=
                    HandleClicked;
            }

            XRFeatureLock.Changed +=
                HandleLockChanged;
        }


        private void OnDisable()
        {
            if (interactionManager != null)
            {
                interactionManager.EntityClicked -=
                    HandleClicked;
            }

            XRFeatureLock.Changed -=
                HandleLockChanged;
        }


        public void SetTool(
            XRClickTool value
        )
        {
            if (tool == value)
            {
                return;
            }


            tool =
                value;

            Debug.Log(
                $"XRClickTools: click tool = {value}.",
                this
            );

            studySession?.LogEvent(
                "vr_tool",
                ("tool", value.ToString())
            );

            ToolChanged?.Invoke(
                value
            );


            if (value == XRClickTool.Compare &&
                comparePanel != null)
            {
                comparePanel.SetVisible(true);
            }
        }


        private void HandleLockChanged()
        {
            // A locked tool falls back to Select.
            if ((tool == XRClickTool.Copy && !XRFeatureLock.Allows(XRFeature.CopyTool)) ||
                (tool == XRClickTool.Compare && !XRFeatureLock.Allows(XRFeature.CompareTool)))
            {
                SetTool(XRClickTool.Select);
            }
        }


        private void HandleClicked(
            EntityReference entity
        )
        {
            if (!entity.IsValid)
            {
                return;
            }


            switch (tool)
            {
                case XRClickTool.Copy:
                    copies?.Copy(entity);
                    break;

                case XRClickTool.Compare:
                    AddToComparison(entity);
                    break;
            }
        }


        private void AddToComparison(
            EntityReference entity
        )
        {
            if (comparisonManager == null)
            {
                return;
            }


            int slot =
                comparisonManager.CopyToNextSlot(
                    entity
                );

            comparePanel?.SetVisible(true);

            studySession?.LogEvent(
                "vr_compare",
                ("entity", entity.ToString()),
                ("slot", slot == 0 ? "A" : "B")
            );

            Compared?.Invoke();
        }
    }
}
