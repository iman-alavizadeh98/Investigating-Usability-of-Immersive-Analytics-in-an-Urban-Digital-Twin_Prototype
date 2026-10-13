using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

using UrbanAnalytics.Core;
using UrbanAnalytics.Data;
using UrbanAnalytics.Interaction;
using UrbanAnalytics.Interaction.UI;
using UrbanAnalytics.Visualization;

namespace UrbanAnalytics.Study
{
    /// <summary>
    /// Runs a guided evaluation session and logs it.
    ///
    /// Flow (facilitator panel bottom left on the PC, or the VR Task tab):
    ///   Start session → per scenario: Show view (applies the preset,
    ///   starts the timer, puts the auto-picked areas into comparison
    ///   slots A/B) → the participant explores, compares and answers the
    ///   sub-questions (choices; open questions aloud, optionally coded by
    ///   the facilitator) and a confidence 1–5 → Submit → next. Scenarios
    ///   with two matched views have a switch button.
    ///
    /// Expected answers are computed from the packaged data at Submit for
    /// the areas actually in slots A/B (StudyAnswerKey); the scenario file
    /// holds no answers and no locations.
    ///
    /// Logged (StudyLog, JSON Lines + answers CSV in
    /// persistentDataPath/study_logs/): session start/end with build and
    /// dataset versions; scenario start; pair_assigned (rule, A/B ids,
    /// counterbalancing); view_switch; per sub-question scenario_part
    /// (answer, expected, correct, basis); scenario_answer (confidence,
    /// seconds, interactions, view switches, final A/B ids); every applied
    /// visualization, hover change, selection and comparison change; the
    /// camera pose at `poseHz`. F2 hides/shows the panel.
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

        [Tooltip("Wait at most this long for the view before placing the comparison areas.")]
        [SerializeField]
        [Range(5.0f, 120.0f)]
        private float pairTimeoutSeconds = 45.0f;

        [Header("References (found automatically when empty)")]
        [SerializeField] private ProjectManager projectManager;
        [SerializeField] private VisualizationManager visualizationManager;
        [SerializeField] private VisualizationSwitcher visualizationSwitcher;
        [SerializeField] private InteractionManager interactionManager;
        [SerializeField] private ComparisonManager comparisonManager;
        [SerializeField] private DataLayerManager dataLayerManager;
        [SerializeField] private Camera viewCamera;


        private StudyLog log;
        private StudyScenario[] scenarios = Array.Empty<StudyScenario>();
        private string studyId;
        private int index = -1;
        private bool viewShown;
        private bool showingAlternate;
        private int viewSwitches;
        private double scenarioStart;
        private int interactions;
        private int confidence;
        private string pairA;
        private string pairB;
        private string pairBasis;
        private string lastHoverId;
        private float nextPoseTime;
        private string status = string.Empty;
        private StudyAnswerKey.Evaluator evaluator;
        private Coroutine pairRoutine;
        private readonly Dictionary<string, string> answers = new Dictionary<string, string>(StringComparer.Ordinal);

        // UI
        private Canvas canvas;
        private CanvasScaler scaler;
        private CanvasScaler referenceScaler;
        private RectTransform panel;
        private TMP_Text header;
        private TMP_Text question;
        private TMP_Text dataText;
        private RectTransform partList;
        private RectTransform confidenceRow;
        private Button startButton;
        private Button showButton;
        private Button switchButton;
        private Button submitButton;
        private Button skipButton;
        private TMP_Text statusText;
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

        /// <summary>Confidence 1–5 for the current scenario (0 = not set).</summary>
        public int Confidence => confidence;

        /// <summary>How the comparison areas were picked (rule, counts), null before.</summary>
        public string PairBasis => pairBasis;

        /// <summary>True while the second matched view of the scenario is shown.</summary>
        public bool ShowingAlternate => showingAlternate;

        /// <summary>The sub-questions shown to the participant (not the hidden checks).</summary>
        public IEnumerable<StudyPart> VisibleParts
        {
            get
            {
                foreach (StudyPart part in Current?.parts ?? Array.Empty<StudyPart>())
                {
                    if (!part.IsHidden)
                    {
                        yield return part;
                    }
                }
            }
        }

        /// <summary>The answer chosen for a sub-question (null = none yet).</summary>
        public string AnswerOf(string partId) =>
            partId != null && answers.TryGetValue(partId, out string value) ? value : null;

        /// <summary>Raised whenever the session state changes (also used by the VR Task tab).</summary>
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
            if (dataLayerManager == null) dataLayerManager = FindFirstObjectByType<DataLayerManager>();

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

                foreach (StudyScenario scenario in scenarios)
                {
                    NormalizeLegacy(scenario);
                }
            }
            catch (Exception exception)
            {
                status = $"Could not load scenarios: {exception.Message}";
                Refresh();
                return;
            }

            evaluator = dataLayerManager != null ? new StudyAnswerKey.Evaluator(dataLayerManager) : null;

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


        /// <summary>A scenario with only top-level options becomes one "choice" part.</summary>
        private static void NormalizeLegacy(StudyScenario scenario)
        {
            if ((scenario.parts == null || scenario.parts.Length == 0) &&
                scenario.options != null && scenario.options.Length > 0)
            {
                scenario.parts = new[]
                {
                    new StudyPart
                    {
                        id = "answer",
                        kind = "choice",
                        question = string.Empty,
                        options = scenario.options,
                        expected = scenario.expected
                    }
                };
            }

            scenario.parts ??= Array.Empty<StudyPart>();
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
            showingAlternate = false;
            scenarioStart = log.Elapsed;
            interactions = 0;
            viewSwitches = 0;

            log.Write(
                "scenario_start",
                ("scenario", scenario.id),
                ("preset", scenario.presetId),
                ("alternatePreset", scenario.alternatePresetId),
                ("training", scenario.training)
            );

            if (pairRoutine != null)
            {
                StopCoroutine(pairRoutine);
                pairRoutine = null;
            }

            if (scenario.HasPair)
            {
                pairRoutine = StartCoroutine(AssignPairWhenReady(scenario));
            }

            Refresh();
        }


        /// <summary>Switches between the scenario's two matched views (same location, same slots).</summary>
        public void SwitchView()
        {
            StudyScenario scenario = Current;

            if (log == null || scenario == null || !viewShown || !scenario.HasAlternateView)
            {
                return;
            }

            showingAlternate = !showingAlternate;
            viewSwitches++;
            interactions++;

            string to = showingAlternate ? scenario.alternatePresetId : scenario.presetId;
            ApplyPreset(to);

            log.Write(
                "view_switch",
                ("scenario", scenario.id),
                ("to", to),
                ("label", showingAlternate ? scenario.alternateLabel : scenario.presetLabel),
                ("switches", viewSwitches),
                ("slotA", SlotUnit(0)),
                ("slotB", SlotUnit(1))
            );

            Refresh();
        }


        public void SelectPartOption(string partId, string option)
        {
            if (string.IsNullOrEmpty(partId))
            {
                return;
            }

            answers[partId] = option;
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

            foreach (StudyPart part in scenario.parts)
            {
                if (part.NeedsAnswer && string.IsNullOrEmpty(AnswerOf(part.id)))
                {
                    status = $"Answer \"{part.question}\" first.";
                    Refresh();
                    return;
                }
            }

            double seconds = log.Elapsed - scenarioStart;
            string slotA = SlotUnit(0);
            string slotB = SlotUnit(1);
            int correctCount = 0;
            int scoredCount = 0;

            foreach (StudyPart part in scenario.parts)
            {
                string answer = AnswerOf(part.id);
                StudyAnswerKey.Result result = evaluator != null
                    ? evaluator.Expect(scenario, part, slotA, slotB)
                    : new StudyAnswerKey.Result { Expected = part.expected, Basis = "no data access" };

                bool? correct;

                if (part.IsHidden)
                {
                    correct = evaluator?.SlotCheck(scenario, part, slotA, slotB);
                    answer = part.slot == "B" ? slotB : slotA;
                }
                else if (part.IsOpen)
                {
                    // Open questions are answered aloud; the answer is the facilitator's code.
                    correct = null;
                }
                else
                {
                    correct = !string.IsNullOrEmpty(result.Expected)
                        ? string.Equals(answer, result.Expected, StringComparison.Ordinal)
                        : (bool?)null;
                }

                if (correct.HasValue && !part.IsHidden)
                {
                    scoredCount++;
                    correctCount += correct.Value ? 1 : 0;
                }

                log.Write(
                    "scenario_part",
                    ("scenario", scenario.id),
                    ("part", part.id),
                    ("kind", part.kind),
                    ("answer", answer),
                    ("expected", part.IsOpen ? null : result.Expected),
                    ("correct", correct),
                    ("basis", result.Basis),
                    ("slotA", slotA),
                    ("slotB", slotB)
                );

                log.WriteAnswer($"{scenario.id}.{part.id}", scenario.presetId, answer,
                    part.IsOpen ? null : result.Expected, correct, confidence, seconds, interactions);
            }

            bool hasParts = scenario.parts.Length > 0;

            log.Write(
                hasParts ? "scenario_answer" : "scenario_done",
                ("scenario", scenario.id),
                ("preset", scenario.presetId),
                ("correctParts", correctCount),
                ("scoredParts", scoredCount),
                ("confidence", confidence),
                ("seconds", seconds),
                ("interactions", interactions),
                ("viewSwitches", viewSwitches),
                ("pairA", pairA),
                ("pairB", pairB),
                ("slotA", slotA),
                ("slotB", slotB)
            );

            if (!hasParts)
            {
                log.WriteAnswer(scenario.id, scenario.presetId, null, null, null, confidence, seconds, interactions);
            }

            status = $"{scenario.id} logged ({seconds:0.0} s, {correctCount}/{scoredCount} scored parts correct).";
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
            if (pairRoutine != null)
            {
                StopCoroutine(pairRoutine);
                pairRoutine = null;
            }

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
            showingAlternate = false;
            confidence = 0;
            viewSwitches = 0;
            pairA = null;
            pairB = null;
            pairBasis = null;
            answers.Clear();
        }


        private string SlotUnit(int slot)
        {
            if (comparisonManager == null ||
                comparisonManager.Slots == null ||
                slot >= comparisonManager.Slots.Count)
            {
                return null;
            }

            ComparisonManager.Slot s = comparisonManager.Slots[slot];
            return s.IsFilled ? s.Entity.UnitId : null;
        }


        /// <summary>
        /// Once the view is drawn and pickable, picks A/B by the scenario's
        /// rule and puts them into the comparison slots (a participant slot
        /// stays empty; their Compare click fills it).
        /// </summary>
        private IEnumerator AssignPairWhenReady(StudyScenario scenario)
        {
            float deadline = Time.realtimeSinceStartup + pairTimeoutSeconds;

            // Let the switcher start applying the preset first.
            yield return null;
            yield return null;

            while (Time.realtimeSinceStartup < deadline &&
                   ((visualizationSwitcher != null && visualizationSwitcher.IsApplying) ||
                    (interactionManager != null && !interactionManager.IsPickingReady)))
            {
                yield return null;
            }

            if (evaluator == null || comparisonManager == null || Current != scenario)
            {
                yield break;
            }

            Task prepare = evaluator.PrepareAsync(scenario);

            while (!prepare.IsCompleted)
            {
                yield return null;
            }

            if (prepare.IsFaulted)
            {
                status = $"Could not load the data for the comparison areas: {prepare.Exception?.GetBaseException().Message}";
                log?.Write("error", ("message", status));
                Refresh();
                yield break;
            }

            (string a, string b, bool swapped, string basis) =
                evaluator.PickPair(scenario.pair, participantId, scenario.id);

            pairA = a;
            pairB = b;
            pairBasis = basis;

            comparisonManager.ClearAll();

            if (b != null)
            {
                comparisonManager.CopyToSlot(1, EntityReference.ForUnit(scenario.pair.spatialLayerId, b));
            }

            if (a != null)
            {
                comparisonManager.CopyToSlot(0, EntityReference.ForUnit(scenario.pair.spatialLayerId, a));
            }

            log?.Write(
                "pair_assigned",
                ("scenario", scenario.id),
                ("layer", scenario.pair.spatialLayerId),
                ("pairA", a ?? "(participant)"),
                ("pairB", b ?? "(participant)"),
                ("swapped", swapped),
                ("rule", basis)
            );

            status = a == null || b == null
                ? "Comparison area " + (a == null ? "B" : "A") + " is set; the participant picks the other one."
                : "Comparison areas A and B are set.";

            pairRoutine = null;
            Refresh();
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
            CheckAreasHaveData();
            Refresh();
        }


        /// <summary>
        /// An area put into A/B by the participant that lacks a value the
        /// task needs (e.g. a district outside the statistics area has no
        /// born-abroad estimate) cannot be scored: say so, so another one is
        /// chosen, and log it.
        /// </summary>
        private void CheckAreasHaveData()
        {
            StudyScenario scenario = Current;

            if (scenario == null || !scenario.HasPair || !viewShown || evaluator == null || pairBasis == null)
            {
                return;
            }

            var eligible = new HashSet<string>(evaluator.Eligible(scenario.pair), StringComparer.Ordinal);
            string prefix = scenario.pair.spatialLayerId + ":";

            // Clear an earlier warning once the areas are usable again.
            if (status.StartsWith("Area ", StringComparison.Ordinal))
            {
                bool bothFilled = SlotUnit(0) != null && SlotUnit(1) != null;
                status = bothFilled ? "Comparison areas A and B are set." : string.Empty;
            }

            for (int slot = 0; slot < 2; slot++)
            {
                string unit = SlotUnit(slot);

                if (unit == null || eligible.Contains(unit))
                {
                    continue;
                }

                string label = slot == 0 ? "A" : "B";

                status = unit.StartsWith(prefix, StringComparison.Ordinal)
                    ? $"Area {label} has no data for every variable of this task (e.g. outside the statistics area). Please choose another area."
                    : $"Area {label} is not a {scenario.pair.spatialLayerId} area of this task. Please choose an area of the view.";

                log?.Write("pick_not_usable", ("scenario", scenario.id), ("slot", label), ("unit", unit));
            }
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
            panel.sizeDelta = new Vector2(480.0f, 0.0f);
            RuntimeUi.Vertical(panel.gameObject, 12, 6.0f);
            panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            header = RuntimeUi.CreateText(panel, "", RuntimeUi.HeadingSize, RuntimeUi.AccentColor, TextAnchor.MiddleLeft, FontStyles.Bold);
            question = RuntimeUi.CreateText(panel, "", RuntimeUi.SmallSize, RuntimeUi.TextColor);
            dataText = RuntimeUi.CreateText(panel, "", RuntimeUi.SmallSize, RuntimeUi.MutedColor);

            partList = RuntimeUi.CreateRect("Parts", panel);
            RuntimeUi.Vertical(partList.gameObject, 0, 3.0f);

            confidenceRow = RuntimeUi.CreateRect("Confidence", panel);
            RuntimeUi.Horizontal(confidenceRow.gameObject, 0, 4.0f);
            TMP_Text confidenceLabel = RuntimeUi.CreateText(confidenceRow, "Confidence", RuntimeUi.SmallSize, RuntimeUi.MutedColor);
            RuntimeUi.Layout(confidenceLabel.gameObject, 90.0f);

            for (int i = 1; i <= 5; i++)
            {
                int value = i;
                confidenceButtons.Add(RuntimeUi.CreateButton(confidenceRow, value.ToString(), () => SetConfidence(value), 44.0f, 26.0f));
            }

            RectTransform actions = RuntimeUi.CreateRect("Actions", panel);
            RuntimeUi.Horizontal(actions.gameObject, 0, 6.0f);
            startButton = RuntimeUi.CreateButton(actions, "Start session", StartSession, 140.0f);
            showButton = RuntimeUi.CreateButton(actions, "Show view", ShowCurrentView, 110.0f);
            switchButton = RuntimeUi.CreateButton(actions, "Switch view", SwitchView, 120.0f);
            submitButton = RuntimeUi.CreateButton(actions, "Submit", Submit, 90.0f);
            skipButton = RuntimeUi.CreateButton(actions, "Skip", Skip, 60.0f);

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
            switchButton.gameObject.SetActive(running && scenario != null && viewShown && scenario.HasAlternateView);
            submitButton.gameObject.SetActive(running && scenario != null && viewShown);
            skipButton.gameObject.SetActive(running && scenario != null);

            dataText.text = string.Empty;

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
                    ? scenario.QuestionFor(condition) + Instruction(scenario)
                    : $"<color=#{ColorUtility.ToHtmlStringRGB(RuntimeUi.MutedColor)}>Press \"Show view\" to load the view and start the timer.</color>";

                if (viewShown)
                {
                    dataText.text = DataBlock(scenario);
                }

                if (switchButton.gameObject.activeSelf)
                {
                    RuntimeUi.SetButton(switchButton,
                        "Show " + (showingAlternate ? scenario.presetLabel : scenario.alternateLabel),
                        RuntimeUi.ButtonColor);
                }
            }

            RebuildParts(scenario, running && viewShown);

            bool showConfidence = running && viewShown && scenario != null && !scenario.training;
            confidenceRow.gameObject.SetActive(showConfidence);

            for (int i = 0; i < confidenceButtons.Count; i++)
            {
                RuntimeUi.SetButton(confidenceButtons[i], (i + 1).ToString(),
                    confidence == i + 1 ? RuntimeUi.ActiveButtonColor : RuntimeUi.ButtonColor);
            }

            if (submitButton.gameObject.activeSelf)
            {
                RuntimeUi.SetButton(submitButton, scenario.parts.Length > 0 ? "Submit" : "Done", RuntimeUi.ButtonColor);
            }

            statusText.text = status;

            Changed?.Invoke();
        }


        private string Instruction(StudyScenario scenario)
        {
            string text = scenario.InstructionFor(condition);
            return string.IsNullOrEmpty(text) ? string.Empty : "\n" + RuntimeUi.Colorize(text, RuntimeUi.AccentColor);
        }


        /// <summary>Data shown (variable · unit · spatial unit · year) and the comparison areas.</summary>
        public string DataBlock(StudyScenario scenario)
        {
            var lines = new List<string>();

            foreach (string line in scenario?.data ?? Array.Empty<string>())
            {
                lines.Add("• " + line);
            }

            if (scenario != null && scenario.HasAlternateView)
            {
                lines.Add("• Shown now: " + (showingAlternate ? scenario.alternateLabel : scenario.presetLabel));
            }

            return string.Join("\n", lines);
        }


        private void RebuildParts(StudyScenario scenario, bool visible)
        {
            RuntimeUi.ClearChildren(partList);
            partList.gameObject.SetActive(visible && scenario?.parts != null && scenario.parts.Length > 0);

            if (!partList.gameObject.activeSelf)
            {
                return;
            }

            int number = 1;

            foreach (StudyPart part in scenario.parts)
            {
                if (part.IsHidden)
                {
                    continue;
                }

                RuntimeUi.CreateText(partList,
                    $"{number++}. {part.question}" + (part.IsOpen ? RuntimeUi.Colorize("  (aloud; code it)", RuntimeUi.MutedColor) : string.Empty),
                    RuntimeUi.SmallSize, RuntimeUi.TextColor);

                string[] options = StudyAnswerKey.OptionsOf(part);
                RectTransform row = RuntimeUi.CreateRect("Options", partList);

                if (StudyAnswerKey.FitsOneRow(options))
                {
                    RuntimeUi.Horizontal(row.gameObject, 0, 4.0f);
                }
                else
                {
                    RuntimeUi.Vertical(row.gameObject, 0, 3.0f);
                }

                foreach (string option in options)
                {
                    string value = option;
                    string partId = part.id;
                    Button button = RuntimeUi.CreateButton(row, value, () => SelectPartOption(partId, value), -1.0f, 26.0f, RuntimeUi.SmallSize);
                    RuntimeUi.Layout(button.gameObject, -1.0f, 26.0f, 1.0f);
                    RuntimeUi.SetButton(button, value,
                        string.Equals(AnswerOf(part.id), value, StringComparison.Ordinal) ? RuntimeUi.ActiveButtonColor : RuntimeUi.ButtonColor);
                }
            }
        }
    }
}
