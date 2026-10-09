using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

using UrbanAnalytics.Interaction;
using UrbanAnalytics.Interaction.UI;

namespace UrbanAnalytics.XR
{
    /// <summary>
    /// A short guided tour at the start of the VR session: one step per
    /// control or UI object (select, show data, the panels, move and
    /// size the table, copy, grab, compare, move a panel, wrist menu,
    /// clear). Each step asks the user to do the thing and moves on by
    /// itself once they did it; reading steps have a Next button.
    ///
    /// The user is locked in: only the functions taught so far work
    /// (XRFeatureLock; toolbar buttons are greyed out, city clicks,
    /// grabbing, the table grip, resizing and the wrist menu are off)
    /// until the last step. The facilitator can skip it on the PC
    /// (<see cref="skipKey"/>); the wrist menu has "Restart tutorial".
    ///
    /// The card stands close to the user: front-left at their side of the
    /// table, about eye height. The toolbar button to press is outlined.
    ///
    /// Logged as the study event vr_tutorial (step, id, event =
    /// start / done / skip / finish, seconds in the step).
    /// </summary>
    public sealed class XRTutorial :
        XRTablePanel
    {
        private sealed class Step
        {
            public string Id;

            public string Title;

            public string Text;

            /// <summary>Picture name (XRHelpPanel.DefaultCards), or null.</summary>
            public string Image;

            /// <summary>Function unlocked from this step on.</summary>
            public XRFeature? Unlocks;

            /// <summary>Toolbar buttons to outline.</summary>
            public string[] Highlight;

            /// <summary>Action counter: done once it grows after the step starts.</summary>
            public Func<int> Count;

            /// <summary>Extra condition (or the only one when Count is null).</summary>
            public Func<bool> Condition;

            /// <summary>Runs when the step starts.</summary>
            public Action OnStart;

            /// <summary>Reading step: moves on with the Next button.</summary>
            public bool IsReading =>
                Count == null && Condition == null;
        }


        // =========================================================
        // INSPECTOR
        // =========================================================

        [Header("Tutorial")]
        [Tooltip("Start the tutorial when the session starts (table placed).")]
        [SerializeField]
        private bool runOnStart =
            true;

        [Tooltip("Facilitator key on the PC keyboard: skip the tutorial.")]
        [SerializeField]
        private Key skipKey =
            Key.F8;

        [Tooltip("Pause after a step is done, before the next one.")]
        [SerializeField]
        [Range(0.0f, 3.0f)]
        private float doneSeconds =
            1.0f;

        [Tooltip("Pictures by name (help_*.png), shown on the card when a step has one.")]
        [SerializeField]
        private Texture2D[] images =
            Array.Empty<Texture2D>();


        [Header("Systems (found automatically when empty)")]
        [SerializeField]
        private InteractionManager interactionManager;

        [SerializeField]
        private VisualizationSwitcher visualizationSwitcher;

        [SerializeField]
        private ComparisonManager comparisonManager;

        [SerializeField]
        private XRSelectionCopies copies;

        [SerializeField]
        private XRClickTools clickTools;

        [SerializeField]
        private XRGrabber grabber;

        [SerializeField]
        private XRTableMover tableMover;

        [SerializeField]
        private XRTableToolbar toolbar;

        [SerializeField]
        private XRHandMenu wristMenu;

        [SerializeField]
        private XRHelpPanel helpPanel;

        [SerializeField]
        private XRComparePanel comparePanel;


        // =========================================================
        // RUNTIME
        // =========================================================

        private readonly List<Step> steps =
            new List<Step>();

        private int stepIndex =
            -1;

        private float stepStarted;

        private bool stepDone;

        private float doneAt;

        // Counts of things the user did; a step is done when its count
        // grew after the step started.
        private int selections;
        private int views;
        private int tableMoves;
        private int resizes;
        private int copiesMade;
        private int copyGrabs;
        private int panelGrabs;
        private int compares;
        private int clears;

        private int countAtStart;

        private TMP_Text progress;

        private TMP_Text title;

        private TMP_Text body;

        private TMP_Text status;

        private RawImage picture;

        private Button nextButton;


        public override string PanelName =>
            "Tutorial";

        public bool IsRunning =>
            stepIndex >= 0;


        public XRTutorial()
        {
            // 680 × 290 units at 0.9 mm = 0.61 × 0.26 m, close to the user:
            // at their side of the table, front-left, about eye height
            // (centre 0.72 m above the table top, ~1.6 m from the floor),
            // just above the Compare
            // panel's place. At the far edge (until 2026-10-09) it was
            // too far away.
            Configure(
                PanelPlacement.NearSide,
                new Vector2(680.0f, 290.0f),
                0.0009f,
                0.72f,
                -0.80f,
                false,
                false
            );
        }


        // =========================================================
        // UNITY
        // =========================================================

        protected override void Awake()
        {
            FindIfEmpty(ref interactionManager);
            FindIfEmpty(ref visualizationSwitcher);
            FindIfEmpty(ref comparisonManager);
            FindIfEmpty(ref copies);
            FindIfEmpty(ref clickTools);
            FindIfEmpty(ref grabber);
            FindIfEmpty(ref tableMover);
            FindIfEmpty(ref toolbar);
            FindIfEmpty(ref wristMenu);
            FindIfEmpty(ref helpPanel);
            FindIfEmpty(ref comparePanel);

            DefineSteps();

            base.Awake();
        }


        private void OnEnable()
        {
            if (interactionManager != null)
            {
                interactionManager.SelectionChanged += HandleSelection;
            }

            if (visualizationSwitcher != null)
            {
                visualizationSwitcher.Changed += HandleView;
            }

            if (tableMover != null)
            {
                tableMover.Moved += HandleTableMoved;
            }

            if (tabletopRig != null)
            {
                tabletopRig.Resized += HandleResized;
            }

            if (copies != null)
            {
                copies.Created += HandleCopy;
            }

            if (grabber != null)
            {
                grabber.Released += HandleGrabReleased;
            }

            if (clickTools != null)
            {
                clickTools.Compared += HandleCompared;
            }

            if (toolbar != null)
            {
                toolbar.Cleared += HandleCleared;
            }
        }


        private void Start()
        {
            if (runOnStart)
            {
                StartCoroutine(
                    StartWhenReady()
                );
            }
        }


        private void OnDisable()
        {
            if (interactionManager != null)
            {
                interactionManager.SelectionChanged -= HandleSelection;
            }

            if (visualizationSwitcher != null)
            {
                visualizationSwitcher.Changed -= HandleView;
            }

            if (tableMover != null)
            {
                tableMover.Moved -= HandleTableMoved;
            }

            if (tabletopRig != null)
            {
                tabletopRig.Resized -= HandleResized;
            }

            if (copies != null)
            {
                copies.Created -= HandleCopy;
            }

            if (grabber != null)
            {
                grabber.Released -= HandleGrabReleased;
            }

            if (clickTools != null)
            {
                clickTools.Compared -= HandleCompared;
            }

            if (toolbar != null)
            {
                toolbar.Cleared -= HandleCleared;
            }

            if (IsRunning)
            {
                Unlock();
            }
        }


        protected override void LateUpdate()
        {
            base.LateUpdate();

            if (!IsRunning)
            {
                return;
            }


            if (Keyboard.current != null &&
                Keyboard.current[skipKey].wasPressedThisFrame)
            {
                Skip();

                return;
            }


            Step step =
                steps[stepIndex];

            if (!stepDone &&
                !step.IsReading &&
                (step.Count == null || step.Count() > countAtStart) &&
                (step.Condition == null || step.Condition()))
            {
                MarkDone();
            }

            if (stepDone &&
                Time.realtimeSinceStartup >= doneAt)
            {
                GoTo(stepIndex + 1);
            }
        }


        // =========================================================
        // PUBLIC
        // =========================================================

        /// <summary>Starts again from the first step (wrist menu).</summary>
        public void Restart()
        {
            StopAllCoroutines();

            Begin();
        }


        /// <summary>Ends the tutorial at once and unlocks everything.</summary>
        public void Skip()
        {
            if (!IsRunning)
            {
                return;
            }

            Log("skip");

            Finish(false);
        }


        // =========================================================
        // FLOW
        // =========================================================

        private IEnumerator StartWhenReady()
        {
            // Lock straight away so nothing is used before the tour.
            XRFeatureLock.LockAllExcept(Array.Empty<XRFeature>());

            if (interactionManager != null)
            {
                interactionManager.CityClicksEnabled =
                    false;
            }


            float deadline =
                Time.realtimeSinceStartup + 60.0f;

            while (Time.realtimeSinceStartup < deadline &&
                   ((tabletopRig != null && !tabletopRig.IsPlaced) ||
                    !(UnityEngine.XR.XRSettings.isDeviceActive ||
                      GameObject.Find("XR Device Simulator") != null)))
            {
                yield return null;
            }


            // Let the head pose settle before placing the card.
            yield return new WaitForSecondsRealtime(1.0f);

            Begin();
        }


        private void Begin()
        {
            ResetPlacement();

            SetVisible(true);

            clickTools?.SetTool(XRClickTool.Select);

            Debug.Log(
                $"XRTutorial: started ({steps.Count} steps; {skipKey} on the PC skips).",
                this
            );

            GoTo(0);
        }


        private void GoTo(
            int index
        )
        {
            if (stepIndex >= 0 &&
                stepIndex < steps.Count)
            {
                Log("done");
            }


            if (index >= steps.Count)
            {
                Finish(true);

                return;
            }


            stepIndex =
                index;

            stepDone =
                false;

            stepStarted =
                Time.realtimeSinceStartup;


            Step step =
                steps[index];

            step.OnStart?.Invoke();

            // The step is done once its counter grows past this.
            countAtStart =
                step.Count?.Invoke() ?? 0;

            // Everything taught up to this step works; the rest is locked.
            var allowed =
                new List<XRFeature>();

            for (int i = 0; i <= index; i++)
            {
                if (steps[i].Unlocks.HasValue)
                {
                    allowed.Add(steps[i].Unlocks.Value);
                }
            }

            XRFeatureLock.LockAllExcept(allowed);

            if (interactionManager != null)
            {
                interactionManager.CityClicksEnabled =
                    allowed.Contains(XRFeature.Select);
            }

            toolbar?.Highlight(step.Highlight);

            Show(step);

            Log("start");
        }


        private void MarkDone()
        {
            stepDone =
                true;

            doneAt =
                Time.realtimeSinceStartup + doneSeconds;

            status.text =
                RuntimeUi.Colorize("Done!", new Color(0.45f, 0.9f, 0.45f));

            nextButton.gameObject.SetActive(false);
        }


        private void Finish(
            bool completed
        )
        {
            if (completed)
            {
                Log("finish");
            }

            stepIndex =
                -1;

            Unlock();

            toolbar?.Highlight();

            SetVisible(false);

            Debug.Log(
                $"XRTutorial: {(completed ? "finished" : "skipped")}; all functions unlocked.",
                this
            );
        }


        private void Unlock()
        {
            XRFeatureLock.Unlock();

            if (interactionManager != null)
            {
                interactionManager.CityClicksEnabled =
                    true;
            }
        }


        // =========================================================
        // STEPS
        // =========================================================

        private static void FindIfEmpty<T>(
            ref T field
        )
            where T : UnityEngine.Object
        {
            if (field == null)
            {
                field =
                    FindFirstObjectByType<T>();
            }
        }


        private void DefineSteps()
        {
            steps.Clear();

            steps.Add(new Step
            {
                Id = "welcome",
                Title = "Welcome",
                Text = "A short tour of the controls. Point the right controller at  Next  and pull the trigger.",
                Image = "help_controllers"
            });

            steps.Add(new Step
            {
                Id = "select",
                Title = "Select",
                Text = "Point at a building on the table and pull the trigger.",
                Image = "help_tools",
                Unlocks = XRFeature.Select,
                Count = () => selections
            });

            steps.Add(new Step
            {
                Id = "view",
                Title = "Show data",
                Text = "Press  View >  on the toolbar at the table edge in front of you.",
                Image = "help_toolbar",
                Unlocks = XRFeature.ChangeView,
                Highlight = new[] { "View >" },
                Count = () => views
            });

            steps.Add(new Step
            {
                Id = "panels",
                Title = "Panels",
                Text = "Info (on your right) shows what you select. The Legend (far side) explains colours and heights.",
                Image = "help_panels"
            });

            steps.Add(new Step
            {
                Id = "move_table",
                Title = "Move the table",
                Text = "Hold the left grip and move your hand: the table follows. Let go to drop it.",
                Image = "help_table",
                Unlocks = XRFeature.MoveTable,
                Count = () => tableMoves
            });

            steps.Add(new Step
            {
                Id = "resize",
                Title = "Table size",
                Text = "Push the right thumbstick up or down, or press  Bigger / Smaller  on the toolbar.",
                Image = "help_table",
                Unlocks = XRFeature.ResizeTable,
                Highlight = new[] { "Bigger", "Smaller" },
                Count = () => resizes
            });

            steps.Add(new Step
            {
                Id = "copy",
                Title = "Copy",
                Text = "Press  Copy  on the toolbar and click a building or area (or point at it and press A). A copy pops up.",
                Image = "help_tools",
                Unlocks = XRFeature.CopyTool,
                Highlight = new[] { "Copy" },
                Count = () => copiesMade
            });

            steps.Add(new Step
            {
                Id = "grab_copy",
                Title = "Grab",
                Text = "Point at the copy, hold the right grip, move it and let go.",
                Image = "help_controllers",
                Unlocks = XRFeature.Grab,
                Count = () => copyGrabs
            });

            steps.Add(new Step
            {
                Id = "compare",
                Title = "Compare",
                Text = "Press  Compare, then click two different areas. The Compare panel (left) shows them side by side.",
                Image = "help_tools",
                Unlocks = XRFeature.CompareTool,
                Highlight = new[] { "Compare" },
                Count = () => compares,
                Condition = BothCompared,
                OnStart = () =>
                {
                    if (comparisonManager != null)
                    {
                        comparisonManager.ClearAll();
                    }
                }
            });

            steps.Add(new Step
            {
                Id = "move_panel",
                Title = "Move a panel",
                Text = "Point at the  Grab  bar under a panel, hold the grip and move it.",
                Image = "help_panels",
                Count = () => panelGrabs
            });

            steps.Add(new Step
            {
                Id = "wrist",
                Title = "Wrist menu",
                Text = "Raise your left wrist and look at it, like a watch: views, tasks and panels.",
                Image = "help_wrist",
                Unlocks = XRFeature.WristMenu,
                Condition = () => wristMenu == null || wristMenu.IsVisible
            });

            steps.Add(new Step
            {
                Id = "clear",
                Title = "Clear the table",
                Text = "Press  Clear table: the data and copies go, only the buildings stay.",
                Image = "help_toolbar",
                Unlocks = XRFeature.ClearTable,
                Highlight = new[] { "Clear table" },
                Count = () => clears
            });

            steps.Add(new Step
            {
                Id = "ready",
                Title = "Ready",
                Text = "That's it. Help is on the wrist menu:  Panels > Help.",
                Image = "help_wrist"
            });
        }


        private bool BothCompared()
        {
            return comparisonManager == null ||
                   (comparisonManager.Slots.Count >= 2 &&
                    comparisonManager.Slots[0].IsFilled &&
                    comparisonManager.Slots[1].IsFilled);
        }


        // =========================================================
        // EVENTS
        // =========================================================

        private void HandleSelection(EntityReference entity)
        {
            if (entity.IsValid) selections++;
        }

        private void HandleView()
        {
            if (visualizationSwitcher != null &&
                visualizationSwitcher.ActiveIndex >= 0)
            {
                views++;
            }
        }

        private void HandleTableMoved() => tableMoves++;

        private void HandleResized() => resizes++;

        private void HandleCopy() => copiesMade++;

        private void HandleCompared() => compares++;

        private void HandleCleared() => clears++;

        private void HandleGrabReleased(
            XRGrabbable target
        )
        {
            if (target.KeepOutOfTable)
            {
                panelGrabs++;
            }
            else
            {
                copyGrabs++;
            }
        }


        // =========================================================
        // CARD
        // =========================================================

        protected override void BuildContent(
            Transform panel
        )
        {
            progress =
                RuntimeUi.CreateText(
                    panel,
                    string.Empty,
                    RuntimeUi.SmallSize,
                    RuntimeUi.MutedColor
                );


            RectTransform row =
                RuntimeUi.CreateRect(
                    "Row",
                    panel
                );

            RuntimeUi.Horizontal(
                row.gameObject,
                0,
                14.0f
            ).childAlignment =
                TextAnchor.UpperLeft;

            RuntimeUi.Layout(
                row.gameObject,
                flexibleHeight: 1.0f
            );


            RectTransform pictureRect =
                RuntimeUi.CreateRect(
                    "Picture",
                    row
                );

            picture =
                pictureRect.gameObject.AddComponent<RawImage>();

            picture.raycastTarget =
                false;

            // 800 × 500 pictures.
            RuntimeUi.Layout(
                pictureRect.gameObject,
                240.0f,
                150.0f
            );


            RectTransform column =
                RuntimeUi.CreateRect(
                    "Text",
                    row
                );

            RuntimeUi.Vertical(
                column.gameObject,
                0,
                6.0f
            ).childAlignment =
                TextAnchor.UpperLeft;

            RuntimeUi.Layout(
                column.gameObject,
                flexibleWidth: 1.0f
            );

            title =
                RuntimeUi.CreateText(
                    column,
                    string.Empty,
                    RuntimeUi.TitleSize,
                    RuntimeUi.AccentColor,
                    TextAnchor.MiddleLeft,
                    FontStyles.Bold
                );

            body =
                RuntimeUi.CreateText(
                    column,
                    string.Empty,
                    RuntimeUi.BodySize,
                    RuntimeUi.TextColor
                );


            RectTransform footer =
                RuntimeUi.CreateRect(
                    "Footer",
                    panel
                );

            RuntimeUi.Horizontal(
                footer.gameObject,
                0,
                8.0f
            );

            status =
                RuntimeUi.CreateText(
                    footer,
                    string.Empty,
                    RuntimeUi.BodySize,
                    RuntimeUi.MutedColor
                );

            RuntimeUi.Layout(
                status.gameObject,
                flexibleWidth: 1.0f
            );

            nextButton =
                RuntimeUi.CreateButton(
                    footer,
                    "Next",
                    () =>
                    {
                        if (IsRunning &&
                            !stepDone)
                        {
                            GoTo(stepIndex + 1);
                        }
                    },
                    140.0f,
                    48.0f
                );
        }


        private void Show(
            Step step
        )
        {
            progress.text =
                $"Tutorial · step {stepIndex + 1} of {steps.Count}";

            title.text =
                step.Title;

            body.text =
                step.Text;

            Texture2D image =
                FindImage(step.Image);

            picture.texture =
                image;

            picture.gameObject.SetActive(
                image != null
            );

            bool last =
                stepIndex == steps.Count - 1;

            nextButton.gameObject.SetActive(
                step.IsReading
            );

            RuntimeUi.SetButton(
                nextButton,
                last ? "Finish" : "Next",
                RuntimeUi.ActiveButtonColor
            );

            status.text =
                step.IsReading
                    ? string.Empty
                    : "Do it to continue.";
        }


        private Texture2D FindImage(
            string name
        )
        {
            if (string.IsNullOrEmpty(name) ||
                images == null)
            {
                return null;
            }

            foreach (Texture2D image in images)
            {
                if (image != null &&
                    image.name == name)
                {
                    return image;
                }
            }

            return null;
        }


        private void Log(
            string what
        )
        {
            if (stepIndex < 0 ||
                stepIndex >= steps.Count)
            {
                return;
            }

            studySession?.LogEvent(
                "vr_tutorial",
                ("step", stepIndex + 1),
                ("id", steps[stepIndex].Id),
                ("event", what),
                ("seconds", Time.realtimeSinceStartup - stepStarted)
            );
        }
    }
}
