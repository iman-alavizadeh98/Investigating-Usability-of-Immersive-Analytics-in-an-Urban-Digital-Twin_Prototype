using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

using UrbanAnalytics.Core;
using UrbanAnalytics.Interaction;
using UrbanAnalytics.Interaction.UI;
using UrbanAnalytics.Visualization;

namespace UrbanAnalytics.Study
{
    /// <summary>One guided scenario (scenarios.json in the city package).</summary>
    [Serializable]
    public sealed class StudyScenario
    {
        public string id;
        public string title;
        /// <summary>Visualization preset id (catalog entry) shown for this scenario.</summary>
        public string presetId;
        public string question;
        /// <summary>
        /// Optional wording for the VR condition (controllers instead of
        /// mouse/keys). Empty = <see cref="question"/> is used.
        /// </summary>
        public string questionVr;
        /// <summary>Answer options; empty = no answer (training / free exploration).</summary>
        public string[] options;
        /// <summary>The correct option (logged for scoring, never shown).</summary>
        public string expected;
        public bool training;

        /// <summary>The question for a condition ("vr" uses <see cref="questionVr"/> when set).</summary>
        public string QuestionFor(string condition) =>
            condition == "vr" && !string.IsNullOrEmpty(questionVr) ? questionVr : question;
    }


    [Serializable]
    public sealed class StudyScenarioFile
    {
        public string studyId;
        public StudyScenario[] scenarios;
    }


    /// <summary>
    /// Runs a guided evaluation session and logs it.
    ///
    /// Flow (facilitator-driven, in the scenario panel, bottom left):
    ///   Start session → per scenario: Show view (applies the preset,
    ///   starts the timer) → participant answers aloud, facilitator
    ///   clicks the option and a confidence 1-5 → Submit → next.
    ///
    /// Logged (StudyLog, JSON Lines + answers CSV in
    /// persistentDataPath/study_logs/): session start/end with build
    /// and dataset versions, scenario start/answer/skip with time and
    /// interaction count, every applied visualization, hover changes,
    /// selections, comparison changes and the camera pose at
    /// `poseHz`.
    ///
    /// The flow methods (StartSession, ShowCurrentView, SelectOption,
    /// SetConfidence, Submit, Skip) are public so the later VR mode can
    /// drive the same session from a world-space panel.
    /// F2 hides/shows the panel.
    /// </summary>
    public sealed class StudySession : MonoBehaviour
    {
        [Header("Session")]
        [SerializeField]
        private string participantId = "P01";

        [Tooltip("Condition label written to every log line, e.g. desktop or vr.")]
        [SerializeField]
        private string condition = "desktop";

        [Tooltip("Scenario file, relative to StreamingAssets.")]
        [SerializeField]
        private string scenarioFile = "cities/helsingborg/visualizations/scenarios.json";

        [SerializeField]
        [Range(0.0f, 30.0f)]
        private float poseHz = 2.0f;

        [Header("References (found automatically when empty)")]
        [SerializeField] private ProjectManager projectManager;
        [SerializeField] private VisualizationManager visualizationManager;
        [SerializeField] private VisualizationSwitcher visualizationSwitcher;
        [SerializeField] private InteractionManager interactionManager;
        [SerializeField] private ComparisonManager comparisonManager;
        [SerializeField] private Camera viewCamera;


        private StudyLog log;
        private StudyScenario[] scenarios = Array.Empty<StudyScenario>();
        private string studyId;
        private int index = -1;
        private bool viewShown;
        private double scenarioStart;
        private int interactions;
        private string selectedOption;
        private int confidence;
        private string lastHoverId;
        private float nextPoseTime;
        private string status = string.Empty;

        // UI
        private Canvas canvas;
        private CanvasScaler scaler;
        private CanvasScaler referenceScaler;
        private RectTransform panel;
        private TMP_Text header;
        private TMP_Text question;
        private RectTransform optionList;
        private RectTransform confidenceRow;
        private Button startButton;
        private Button showButton;
        private Button submitButton;
        private Button skipButton;
        private TMP_Text statusText;
        private readonly List<Button> optionButtons = new List<Button>();
        private readonly List<Button> confidenceButtons = new List<Button>();


        public bool IsRunning => log != null;

        /// <summary>Study condition ("desktop" or "vr"), as logged.</summary>
        public string Condition => condition;

        /// <summary>
        /// Logs an extra event (e.g. VR table resize, menu pin) while a
        /// session runs; ignored otherwise.
        /// </summary>
        public void LogEvent(string eventName, params (string Key, object Value)[] fields)
        {
            log?.Write(eventName, fields);
        }

        /// <summary>Last status message shown in the panel (errors included).</summary>
        public string Status => status;

        public string LogPath => log?.LogPath;

        public StudyScenario Current =>
            index >= 0 && index < scenarios.Length ? scenarios[index] : null;

        /// <summary>0-based index of the current scenario (-1 before the session).</summary>
        public int ScenarioIndex => index;

        public int ScenarioCount => scenarios.Length;

        /// <summary>True once "Show view" was pressed for the current scenario.</summary>
        public bool ViewShown => viewShown;

        /// <summary>Raised whenever the session state shown in the panel changes (also used by the VR task card).</summary>
        public event Action Changed;


        // =========================================================
        // UNITY
        // =========================================================

        private void Awake()
        {
            // Explicit checks: `??=` bypasses Unity's null for unassigned object fields.
            if (projectManager == null) projectManager = FindFirstObjectByType<ProjectManager>();
            if (visualizationManager == null) visualizationManager = FindFirstObjectByType<VisualizationManager>();
            if (visualizationSwitcher == null) visualizationSwitcher = FindFirstObjectByType<VisualizationSwitcher>();
            if (interactionManager == null) interactionManager = FindFirstObjectByType<InteractionManager>();
            if (comparisonManager == null) comparisonManager = FindFirstObjectByType<ComparisonManager>();

            BuildUi();
            Refresh();
        }


        private void OnEnable()
        {
            if (interactionManager != null)
            {
                interactionManager.HoverChanged += HandleHover;
                interactionManager.SelectionChanged += HandleSelection;
            }

            if (visualizationManager != null)
            {
                visualizationManager.VisualizationApplied += HandleApplied;
            }

            if (comparisonManager != null)
            {
                comparisonManager.Changed += HandleComparison;
            }
        }


        private void OnDisable()
        {
            if (interactionManager != null)
            {
                interactionManager.HoverChanged -= HandleHover;
                interactionManager.SelectionChanged -= HandleSelection;
            }

            if (visualizationManager != null)
            {
                visualizationManager.VisualizationApplied -= HandleApplied;
            }

            if (comparisonManager != null)
            {
                comparisonManager.Changed -= HandleComparison;
            }
        }


        private void OnDestroy()
        {
            EndSession("destroyed");
        }


        private void OnApplicationQuit()
        {
            EndSession("quit");
        }


        private void Update()
        {
            FollowUiScale();

            Keyboard keyboard = Keyboard.current;

            if (keyboard != null && keyboard.f2Key.wasPressedThisFrame && panel != null)
            {
                panel.gameObject.SetActive(!panel.gameObject.activeSelf);
            }

            if (log == null || poseHz <= 0.0f || Time.unscaledTime < nextPoseTime)
            {
                return;
            }

            nextPoseTime = Time.unscaledTime + 1.0f / poseHz;
            Camera cam = viewCamera != null ? viewCamera : Camera.main;

            if (cam != null)
            {
                log.Write(
                    "camera",
                    ("position", cam.transform.position),
                    ("euler", cam.transform.eulerAngles),
                    ("scenario", Current?.id)
                );
            }
        }


        /// <summary>
        /// Uses the desktop interaction UI's scale factor (screen size ×
        /// the [ / ] UI scale), so all panels have the same text size.
        /// </summary>
        private void FollowUiScale()
        {
            if (scaler == null)
            {
                return;
            }

            if (referenceScaler == null)
            {
                DesktopInteractionUI ui = FindFirstObjectByType<DesktopInteractionUI>();
                referenceScaler = ui != null ? ui.GetComponentInChildren<CanvasScaler>(true) : null;
            }

            float factor = referenceScaler != null
                ? referenceScaler.scaleFactor
                : Mathf.Clamp(Mathf.Min(Screen.width / 1600.0f, Screen.height / 900.0f), 0.75f, 4.0f);

            if (!Mathf.Approximately(scaler.scaleFactor, factor))
            {
                scaler.scaleFactor = factor;
            }
        }


        // =========================================================
        // FLOW
        // =========================================================

        public async void StartSession()
        {
            if (log != null)
            {
                return;
            }

            try
            {
                StudyScenarioFile file = await LoadScenariosAsync();
                scenarios = file?.scenarios ?? Array.Empty<StudyScenario>();
                studyId = file?.studyId;
            }
            catch (Exception exception)
            {
                status = $"Could not load scenarios: {exception.Message}";
                Refresh();
                return;
            }

            log = new StudyLog(
                Path.Combine(Application.persistentDataPath, "study_logs"),
                participantId,
                condition
            );

            log.Write(
                "session_start",
                ("study", studyId),
                ("build", Application.version),
                ("unity", Application.unityVersion),
                ("scene", gameObject.scene.name),
                ("manifest", projectManager != null ? projectManager.ManifestPath : null),
                ("scenarioFile", scenarioFile),
                ("scenarios", scenarios.Length),
                ("screen", $"{Screen.width}x{Screen.height}")
            );

            index = 0;
            ResetScenarioState();
            status = $"Logging to {log.LogPath}";
            Refresh();
        }


        public void ShowCurrentView()
        {
            StudyScenario scenario = Current;

            if (log == null || scenario == null)
            {
                return;
            }

            ApplyPreset(scenario.presetId);
            viewShown = true;
            scenarioStart = log.Elapsed;
            interactions = 0;

            log.Write(
                "scenario_start",
                ("scenario", scenario.id),
                ("preset", scenario.presetId),
                ("training", scenario.training)
            );

            Refresh();
        }


        public void SelectOption(string option)
        {
            selectedOption = option;
            Refresh();
        }


        public void SetConfidence(int value)
        {
            confidence = Mathf.Clamp(value, 1, 5);
            Refresh();
        }


        public void Submit()
        {
            StudyScenario scenario = Current;

            if (log == null || scenario == null || !viewShown)
            {
                return;
            }

            bool hasOptions = scenario.options != null && scenario.options.Length > 0;

            if (hasOptions && string.IsNullOrEmpty(selectedOption))
            {
                status = "Select the participant's answer first.";
                Refresh();
                return;
            }

            double seconds = log.Elapsed - scenarioStart;
            bool? correct = hasOptions && !string.IsNullOrEmpty(scenario.expected)
                ? string.Equals(selectedOption, scenario.expected, StringComparison.Ordinal)
                : (bool?)null;

            log.Write(
                hasOptions ? "scenario_answer" : "scenario_done",
                ("scenario", scenario.id),
                ("preset", scenario.presetId),
                ("answer", selectedOption),
                ("expected", scenario.expected),
                ("correct", correct),
                ("confidence", confidence),
                ("seconds", seconds),
                ("interactions", interactions)
            );

            log.WriteAnswer(scenario.id, scenario.presetId, selectedOption, scenario.expected,
                correct, confidence, seconds, interactions);

            status = $"{scenario.id} logged ({seconds:0.0} s).";
            Next();
        }


        public void Skip()
        {
            StudyScenario scenario = Current;

            if (log == null || scenario == null)
            {
                return;
            }

            log.Write(
                "scenario_skip",
                ("scenario", scenario.id),
                ("seconds", viewShown ? log.Elapsed - scenarioStart : 0.0)
            );

            status = $"{scenario.id} skipped.";
            Next();
        }


        private void Next()
        {
            index++;
            ResetScenarioState();

            if (Current == null)
            {
                EndSession("completed");
            }

            Refresh();
        }


        private void EndSession(string reason)
        {
            if (log == null)
            {
                return;
            }

            log.Write("session_end", ("reason", reason), ("scenariosDone", Math.Max(0, index)));
            status = $"Session {reason}. Logs: {log.LogPath}";
            log.Dispose();
            log = null;
        }


        private void ResetScenarioState()
        {
            viewShown = false;
            selectedOption = null;
            confidence = 0;
        }


        private void ApplyPreset(string presetId)
        {
            if (visualizationSwitcher == null || string.IsNullOrWhiteSpace(presetId))
            {
                return;
            }

            IReadOnlyList<VisualizationOption> options = visualizationSwitcher.Options;

            for (int i = 0; i < options.Count; i++)
            {
                if (string.Equals(options[i].Id, presetId, StringComparison.Ordinal))
                {
                    visualizationSwitcher.Apply(i);
                    return;
                }
            }

            status = $"Preset '{presetId}' is not in the visualization catalog.";
            log?.Write("error", ("message", status));
        }


        private async Task<StudyScenarioFile> LoadScenariosAsync()
        {
            if (projectManager == null || projectManager.AssetReader == null)
            {
                throw new InvalidOperationException("No ProjectManager / asset reader in the scene.");
            }

            if (projectManager.LoadTask != null)
            {
                await projectManager.LoadTask;
            }

            string json = await projectManager.AssetReader.ReadTextAsync(scenarioFile, default);
            return JsonUtility.FromJson<StudyScenarioFile>(json);
        }


        // =========================================================
        // EVENTS → LOG
        // =========================================================

        private void HandleHover(PickResult pick)
        {
            if (log == null)
            {
                return;
            }

            string id = pick.IsValid ? pick.Entity.Id : null;

            if (string.Equals(id, lastHoverId, StringComparison.Ordinal))
            {
                return;
            }

            lastHoverId = id;

            if (id == null)
            {
                return;
            }

            interactions++;
            log.Write(
                "hover",
                ("entity", id),
                ("unit", pick.Entity.UnitId),
                ("layer", pick.VisualizationLayerId),
                ("scenario", Current?.id)
            );
        }


        private void HandleSelection(EntityReference entity)
        {
            if (log == null)
            {
                return;
            }

            interactions++;
            log.Write(
                "select",
                ("entity", entity.IsValid ? entity.Id : null),
                ("unit", entity.IsValid ? entity.UnitId : null),
                ("kind", entity.IsValid ? entity.Kind.ToString() : null),
                ("scenario", Current?.id)
            );
        }


        private void HandleApplied(VisualizationSpec spec)
        {
            if (log == null)
            {
                return;
            }

            log.Write(
                "visualization_applied",
                ("visualization", spec?.Id),
                ("legends", visualizationManager != null ? visualizationManager.ActiveLegends.Count : 0),
                ("scenario", Current?.id)
            );
        }


        private void HandleComparison()
        {
            if (log == null || comparisonManager == null)
            {
                return;
            }

            interactions++;
            var slots = new List<string>();

            foreach (ComparisonManager.Slot slot in comparisonManager.Slots)
            {
                slots.Add(slot.Entity.IsValid ? slot.Entity.Id : "");
            }

            log.Write("comparison", ("slots", slots), ("scenario", Current?.id));
        }


        // =========================================================
        // UI (built from code; screen space, bottom left)
        // =========================================================

        private void BuildUi()
        {
            var canvasObject = new GameObject("StudyCanvas", typeof(RectTransform));
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 95;
            // Same size as the interaction UI: its scale factor is copied every frame (see FollowUiScale).
            scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            canvasObject.AddComponent<GraphicRaycaster>();

            Image background = RuntimeUi.CreatePanel("StudyPanel", canvasObject.transform, RuntimeUi.PanelColor);
            panel = background.rectTransform;
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.0f, 0.0f);
            panel.anchoredPosition = new Vector2(12.0f, 12.0f);
            panel.sizeDelta = new Vector2(430.0f, 0.0f);
            RuntimeUi.Vertical(panel.gameObject, 12, 8.0f);
            panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            header = RuntimeUi.CreateText(panel, "", RuntimeUi.HeadingSize, RuntimeUi.AccentColor, TextAnchor.MiddleLeft, FontStyles.Bold);
            question = RuntimeUi.CreateText(panel, "", RuntimeUi.BodySize, RuntimeUi.TextColor);

            optionList = RuntimeUi.CreateRect("Options", panel);
            RuntimeUi.Vertical(optionList.gameObject, 0, 4.0f);

            confidenceRow = RuntimeUi.CreateRect("Confidence", panel);
            RuntimeUi.Horizontal(confidenceRow.gameObject, 0, 4.0f);
            TMP_Text confidenceLabel = RuntimeUi.CreateText(confidenceRow, "Confidence", RuntimeUi.SmallSize, RuntimeUi.MutedColor);
            RuntimeUi.Layout(confidenceLabel.gameObject, 90.0f);

            for (int i = 1; i <= 5; i++)
            {
                int value = i;
                confidenceButtons.Add(RuntimeUi.CreateButton(confidenceRow, value.ToString(), () => SetConfidence(value), 44.0f, 28.0f));
            }

            RectTransform actions = RuntimeUi.CreateRect("Actions", panel);
            RuntimeUi.Horizontal(actions.gameObject, 0, 6.0f);
            startButton = RuntimeUi.CreateButton(actions, "Start session", StartSession, 140.0f);
            showButton = RuntimeUi.CreateButton(actions, "Show view", ShowCurrentView, 120.0f);
            submitButton = RuntimeUi.CreateButton(actions, "Submit", Submit, 100.0f);
            skipButton = RuntimeUi.CreateButton(actions, "Skip", Skip, 70.0f);

            statusText = RuntimeUi.CreateText(panel, "", RuntimeUi.SmallSize, RuntimeUi.MutedColor);
        }


        private void Refresh()
        {
            if (panel == null)
            {
                return;
            }

            StudyScenario scenario = Current;
            bool running = log != null;

            startButton.gameObject.SetActive(!running && index < 0);
            showButton.gameObject.SetActive(running && scenario != null);
            submitButton.gameObject.SetActive(running && scenario != null && viewShown);
            skipButton.gameObject.SetActive(running && scenario != null);

            if (!running)
            {
                header.text = $"Study · {participantId} · {condition}";
                question.text = index < 0
                    ? "Start the session when the participant is ready. F2 hides this panel."
                    : "Session finished. Thank you!";
            }
            else if (scenario != null)
            {
                header.text = $"{index + 1} / {scenarios.Length} · {scenario.id} {scenario.title}";
                question.text = viewShown
                    ? scenario.QuestionFor(condition)
                    : $"<color=#{ColorUtility.ToHtmlStringRGB(RuntimeUi.MutedColor)}>Press \"Show view\" to load the view and start the timer.</color>";
            }

            RebuildOptions(scenario, running && viewShown);

            bool showConfidence = running && viewShown && scenario?.options != null && scenario.options.Length > 0;
            confidenceRow.gameObject.SetActive(showConfidence);

            for (int i = 0; i < confidenceButtons.Count; i++)
            {
                RuntimeUi.SetButton(confidenceButtons[i], (i + 1).ToString(),
                    confidence == i + 1 ? RuntimeUi.ActiveButtonColor : RuntimeUi.ButtonColor);
            }

            if (submitButton.gameObject.activeSelf)
            {
                bool hasOptions = scenario.options != null && scenario.options.Length > 0;
                RuntimeUi.SetButton(submitButton, hasOptions ? "Submit" : "Done", RuntimeUi.ButtonColor);
            }

            statusText.text = status;

            Changed?.Invoke();
        }


        private void RebuildOptions(StudyScenario scenario, bool visible)
        {
            RuntimeUi.ClearChildren(optionList);
            optionButtons.Clear();
            optionList.gameObject.SetActive(visible && scenario?.options != null && scenario.options.Length > 0);

            if (!optionList.gameObject.activeSelf)
            {
                return;
            }

            foreach (string option in scenario.options)
            {
                string value = option;
                Button button = RuntimeUi.CreateButton(optionList, value, () => SelectOption(value), -1.0f, 30.0f);
                RuntimeUi.SetButton(button, value,
                    string.Equals(selectedOption, value, StringComparison.Ordinal) ? RuntimeUi.ActiveButtonColor : RuntimeUi.ButtonColor);
                optionButtons.Add(button);
            }
        }
    }
}
