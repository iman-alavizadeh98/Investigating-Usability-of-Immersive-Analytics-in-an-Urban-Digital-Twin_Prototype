using TMPro;
using UnityEngine;
using UnityEngine.UI;

using UrbanAnalytics.Interaction.UI;
using UrbanAnalytics.Study;

namespace UrbanAnalytics.XR
{
    /// <summary>
    /// The study board in VR: the current scenario's question word for word
    /// (scenario file), what height and colour show (variable, unit, spatial
    /// unit, year) and ONE button that does the next step of the session
    /// (StudySession.Advance: Start → show scenario → next scenario →
    /// finish). The facilitator can do the same with F5 at the PC, so moving
    /// on is always a single action. Answers are given aloud; the board has
    /// no answer buttons.
    ///
    /// Stands close to the user (their side of the table, front-left, about
    /// eye height), where the tutorial card stood; hidden while the tutorial
    /// runs. Can be carried by its Grab bar.
    /// </summary>
    public sealed class XRStudyBoard :
        XRTablePanel
    {
        [Header("Systems (found automatically when empty)")]
        [SerializeField]
        private StudySession session;


        private TMP_Text progress;

        private TMP_Text title;

        private TMP_Text question;

        private TMP_Text data;

        private TMP_Text status;

        private Button advanceButton;

        private bool lockedLastFrame;


        public override string PanelName =>
            "Study";


        public XRStudyBoard()
        {
            // 680 × 480 units at 0.9 mm = 0.61 × 0.43 m, front-left, eye height.
            Configure(
                PanelPlacement.NearSide,
                new Vector2(680.0f, 480.0f),
                0.0009f,
                0.72f,
                -0.80f,
                true,
                true
            );
        }


        protected override void Awake()
        {
            if (session == null)
            {
                session =
                    FindFirstObjectByType<StudySession>();
            }

            base.Awake();
        }


        private void OnEnable()
        {
            if (session != null)
            {
                session.Changed +=
                    Refresh;
            }
        }


        private void Start()
        {
            Refresh();
        }


        private void OnDisable()
        {
            if (session != null)
            {
                session.Changed -=
                    Refresh;
            }
        }


        protected override void LateUpdate()
        {
            // The tutorial card uses the same place; the board waits.
            bool locked =
                XRFeatureLock.IsLocked;

            if (locked != lockedLastFrame)
            {
                lockedLastFrame =
                    locked;

                SetVisible(!locked && session != null);
            }

            base.LateUpdate();
        }


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

            title =
                RuntimeUi.CreateText(
                    panel,
                    string.Empty,
                    RuntimeUi.TitleSize,
                    RuntimeUi.AccentColor,
                    TextAnchor.MiddleLeft,
                    FontStyles.Bold
                );

            question =
                RuntimeUi.CreateText(
                    panel,
                    string.Empty,
                    RuntimeUi.HeadingSize,
                    RuntimeUi.TextColor
                );

            data =
                RuntimeUi.CreateText(
                    panel,
                    string.Empty,
                    RuntimeUi.SmallSize,
                    RuntimeUi.MutedColor
                );

            RuntimeUi.Layout(
                data.gameObject,
                flexibleHeight: 1.0f
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
                    RuntimeUi.SmallSize,
                    RuntimeUi.MutedColor
                );

            RuntimeUi.Layout(
                status.gameObject,
                flexibleWidth: 1.0f
            );

            advanceButton =
                RuntimeUi.CreateButton(
                    footer,
                    "Start",
                    () => session?.Advance(),
                    220.0f,
                    52.0f,
                    RuntimeUi.HeadingSize
                );
        }


        private void Refresh()
        {
            if (question == null)
            {
                return;
            }


            if (session == null)
            {
                title.text = "No study in this scene";
                question.text = string.Empty;
                advanceButton.gameObject.SetActive(false);
                return;
            }


            StudyScenario scenario =
                session.Current;

            string label =
                session.AdvanceLabel;

            advanceButton.gameObject.SetActive(
                label != "Finished"
            );

            RuntimeUi.SetButton(
                advanceButton,
                label,
                RuntimeUi.ActiveButtonColor
            );


            if (!session.IsRunning)
            {
                progress.text =
                    session.SelectedStudy >= 0 && session.SelectedStudy < session.Studies.Count
                        ? session.Studies[session.SelectedStudy].label
                        : "Study";

                title.text =
                    session.ScenarioIndex > 0 ? "Thank you!" : "Ready";

                question.text =
                    session.ScenarioIndex > 0
                        ? "The session is finished."
                        : "Press Start when you are ready. Say aloud what you see.";

                data.text =
                    string.Empty;

                status.text =
                    string.Empty;

                return;
            }


            if (scenario == null)
            {
                return;
            }


            (int number, int total) =
                session.StudyPosition;

            progress.text =
                scenario.training
                    ? "Practice"
                    : scenario.optional
                        ? "Optional · explore freely, not timed"
                        : $"Scenario {number} of {total}";

            title.text =
                scenario.title;

            question.text =
                session.ViewShown
                    ? scenario.QuestionFor(session.Condition)
                    : RuntimeUi.Colorize("Press Show scenario to see the question and the map.", RuntimeUi.MutedColor);

            data.text =
                session.ViewShown
                    ? session.DataBlock(scenario)
                    : string.Empty;

            status.text =
                session.ViewShown
                    ? (scenario.optional
                        ? "Press " + label + " to end."
                        : "Think aloud. Press " + label + " when you are done.")
                    : string.Empty;
        }
    }
}
