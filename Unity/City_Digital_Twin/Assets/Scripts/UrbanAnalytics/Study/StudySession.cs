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
    /// <summary>A study the facilitator can pick: its view catalog and scenario file.</summary>
    [Serializable]
    public sealed class StudyChoice
    {
        public string label;
        public string catalogPath;
        public string scenarioFile;
    }


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
    ///
    /// Several studies (own view catalog + scenario file) can be listed;
    /// the facilitator picks one before Start. For timed think-aloud
    /// studies (pattern study P1–P3): <see cref="Advance"/> is the single
    /// action that starts, shows the next scenario and ends the current one
    /// (F5 at the PC, Next on the VR board); F9 logs the participant's
    /// claims (first_claim = time to first claim); scenario_end closes each
    /// scenario with its time.
    /// </summary>
    public sealed class StudySession : MonoBehaviour
    {
        [Header("Session")]
        [SerializeField]
        private string participantId = "P01";

        [Tooltip("Condition label written to every log line, e.g. desktop or vr.")]
        [SerializeField]
        private string condition = "desktop";

        [Tooltip(
            "Studies the facilitator can pick before Start: each has its own view " +
            "catalog and scenario file (paths relative to StreamingAssets)."
        )]
        [SerializeField]
        private StudyChoice[] studies =
        {
            new StudyChoice
            {
                label = "Pattern study 2D/3D (P1–P3)",
                catalogPath = "cities/helsingborg/visualizations/pattern/catalog.json",
                scenarioFile = "cities/helsingborg/visualizations/pattern/scenarios.json"
            },
            new StudyChoice
            {
                label = "Evaluation E1–E5",
                catalogPath = "cities/helsingborg/visualizations/catalog.json",
                scenarioFile = "cities/helsingborg/visualizations/scenarios.json"
            }
        };

        [Tooltip("Study selected at start (index into Studies).")]
        [SerializeField]
        private int selectedStudy;

        [Tooltip("Scenario file used when no studies are listed (relative to StreamingAssets).")]
        [SerializeField]
        private string scenarioFile = "cities/helsingborg/visualizations/scenarios.json";

        [Header("Facilitator keys (PC)")]
        [Tooltip("One action: start / show the scenario / next scenario.")]
        [SerializeField]
        private Key advanceKey = Key.F5;

        [Tooltip("Logs the participant's first claim about a pattern (time to first claim).")]
        [SerializeField]
        private Key claimKey = Key.F9;

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
        private int claims;
        private double firstClaimSeconds = -1.0;
        private bool advancing;
        private bool switchingStudy;

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
        private Button advanceButton;
        private Button claimButton;
        private RectTransform studyRow;
        private readonly List<Button> studyButtons = new List<Button>();
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

        /// <summary>The studies the facilitator can pick (before Start).</summary>
        public IReadOnlyList<StudyChoice> Studies => studies ?? Array.Empty<StudyChoice>();

        public int SelectedStudy => selectedStudy;

        private StudyChoice CurrentStudy =>
            studies != null && selectedStudy >= 0 && selectedStudy < studies.Length ? studies[selectedStudy] : null;

        private string ScenarioFilePath => CurrentStudy?.scenarioFile ?? scenarioFile;

        /// <summary>Seconds since the current scenario's view was shown (0 before).</summary>
        public double ScenarioSeconds => log != null && viewShown ? log.Elapsed - scenarioStart : 0.0;

        /// <summary>
        /// Position of the current scenario among the study scenarios (not
        /// training, not optional): 1-based number and total.
        /// </summary>
        public (int Number, int Total) StudyPosition
        {
            get
            {
                int number = 0, total = 0;

                for (int i = 0; i < scenarios.Length; i++)
                {
                    if (scenarios[i].training || scenarios[i].optional)
                    {
                        continue;
                    }

                    total++;

                    if (i <= index)
                    {
                        number = total;
                    }
                }

                return (number, total);
            }
        }

        /// <summary>Number of training scenarios in the session.</summary>
        public int TrainingCount => TrainingCountBefore(scenarios.Length);

        /// <summary>Training scenarios before the given index.</summary>
        public int TrainingCountBefore(int end)
        {
            int count = 0;

            for (int i = 0; i < Math.Min(end, scenarios.Length); i++)
            {
                if (scenarios[i].training)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>Claims logged in the current scenario (F9).</summary>
        public int Claims => claims;

        /// <summary>What <see cref="Advance"/> does next, as a button label.</summary>
        public string AdvanceLabel
        {
            get
            {
                if (log == null) return index < 0 ? "Start" : "Finished";
                if (Current == null) return "Finished";
                if (!viewShown) return "Show scenario";
                return index >= scenarios.Length - 1 ? "Finish" : "Next scenario";
            }
        }


        /// <summary>
        /// Picks the study to run (before Start): loads its view catalog
        /// into the VisualizationSwitcher and uses its scenario file.
        /// </summary>
        public async void SelectStudy(int study)
        {
            if (log != null || studies == null || study < 0 || study >= studies.Length || switchingStudy)
            {
                return;
            }

            selectedStudy = study;
            switchingStudy = true;
            status = $"Loading {studies[study].label}…";
            Refresh();

            try
            {
                if (visualizationSwitcher != null)
                {
                    await visualizationSwitcher.LoadCatalogAsync(studies[study].catalogPath);
                }

                status = $"{studies[study].label} selected.";
            }
            finally
            {
                switchingStudy = false;
                Refresh();
            }
        }


        /// <summary>
        /// The single action of a timed session (F5, or Next on the VR
        /// board): starts the session and shows the first scenario; shows a
        /// scenario whose view is not shown yet; otherwise ends the current
        /// scenario (Submit) and shows the next one. A scenario whose
        /// questions still need answers (evaluation study) is not ended.
        /// </summary>
        public async void Advance()
        {
            if (advancing || switchingStudy)
            {
                return;
            }

            advancing = true;

            try
            {
                if (log == null)
                {
                    if (index >= 0)
                    {
                        return;  // finished
                    }

                    await StartSessionAsync();

                    if (log != null)
                    {
                        ShowCurrentView();
                    }

                    return;
                }

                if (Current == null)
                {
                    return;
                }

                if (!viewShown)
                {
                    ShowCurrentView();
                    return;
                }

                int before = index;
                Submit();

                if (log != null && index != before && Current != null)
                {
                    ShowCurrentView();
                }
            }
            finally
            {
                advancing = false;
            }
        }


        /// <summary>
        /// Facilitator key (F9): the participant states a pattern. The first
        /// one in a scenario is logged as first_claim (time to first claim),
        /// later ones as claim.
        /// </summary>
        public void MarkClaim()
        {
            StudyScenario scenario = Current;

            if (log == null || scenario == null || !viewShown)
            {
                return;
            }

            claims++;
            double seconds = log.Elapsed - scenarioStart;

            if (claims == 1)
            {
                firstClaimSeconds = seconds;
            }

            log.Write(
                claims == 1 ? "first_claim" : "claim",
                ("scenario", scenario.id),
                ("claim", claims),
                ("seconds", seconds),
                ("view", showingAlternate ? scenario.alternatePresetId : scenario.presetId)
            );

            status = claims == 1
                ? $"{scenario.id}: first claim at {seconds:0.0} s."
                : $"{scenario.id}: claim {claims} at {seconds:0.0} s.";
            Refresh();
        }

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

            if (keyboard != null && keyboard[advanceKey].wasPressedThisFrame)
            {
                Advance();
            }

            if (keyboard != null && keyboard[claimKey].wasPressedThisFrame)
            {
                MarkClaim();
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
            await StartSessionAsync();
        }


        private async Task StartSessionAsync()
        {
            if (log != null)
            {
                return;
            }

            try
            {
                // The selected study's views must be the ones offered.
                if (CurrentStudy != null && visualizationSwitcher != null)
                {
                    await visualizationSwitcher.LoadCatalogAsync(CurrentStudy.catalogPath);
                }

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
                ("studyLabel", CurrentStudy?.label),
                ("catalog", visualizationSwitcher != null ? visualizationSwitcher.CatalogPath : null),
                ("scenarioFile", ScenarioFilePath),
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

            WriteScenarioEnd(scenario, "done", seconds);
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

            WriteScenarioEnd(scenario, "skip", viewShown ? log.Elapsed - scenarioStart : 0.0);
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


        /// <summary>Closes a scenario: its time and the participant's claims (time to first claim).</summary>
        private void WriteScenarioEnd(StudyScenario scenario, string reason, double seconds)
        {
            log?.Write(
                "scenario_end",
                ("scenario", scenario.id),
                ("reason", reason),
                ("seconds", seconds),
                ("claims", claims),
                ("firstClaimSeconds", firstClaimSeconds >= 0.0 ? firstClaimSeconds : (double?)null),
                ("interactions", interactions),
                ("viewSwitches", viewSwitches)
            );
        }


        private void ResetScenarioState()
        {
            claims = 0;
            firstClaimSeconds = -1.0;
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

            string json = await projectManager.AssetReader.ReadTextAsync(ScenarioFilePath, default);
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

            // Study choice (before Start).
            studyRow = RuntimeUi.CreateRect("Studies", panel);
            RuntimeUi.Horizontal(studyRow.gameObject, 0, 4.0f);

            for (int i = 0; i < Studies.Count; i++)
            {
                int study = i;
                studyButtons.Add(RuntimeUi.CreateButton(studyRow, Studies[i].label, () => SelectStudy(study), -1.0f, 26.0f, RuntimeUi.SmallSize));
                RuntimeUi.Layout(studyButtons[i].gameObject, -1.0f, 26.0f, 1.0f);
            }

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

            RectTransform timed = RuntimeUi.CreateRect("Timed", panel);
            RuntimeUi.Horizontal(timed.gameObject, 0, 6.0f);
            advanceButton = RuntimeUi.CreateButton(timed, "Next (F5)", Advance, 200.0f);
            claimButton = RuntimeUi.CreateButton(timed, "Claim (F9)", MarkClaim, 140.0f);

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

            studyRow.gameObject.SetActive(!running && index < 0 && Studies.Count > 1);

            for (int i = 0; i < studyButtons.Count; i++)
            {
                RuntimeUi.SetButton(studyButtons[i], Studies[i].label,
                    i == selectedStudy ? RuntimeUi.ActiveButtonColor : RuntimeUi.ButtonColor);
                studyButtons[i].interactable = !switchingStudy;
            }

            advanceButton.gameObject.SetActive(!(index >= 0 && !running));
            RuntimeUi.SetButton(advanceButton, AdvanceLabel + " (F5)", RuntimeUi.ActiveButtonColor);
            claimButton.gameObject.SetActive(running && scenario != null && viewShown);
            RuntimeUi.SetButton(claimButton, claims == 0 ? "First claim (F9)" : $"Claim {claims + 1} (F9)", RuntimeUi.ButtonColor);

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
