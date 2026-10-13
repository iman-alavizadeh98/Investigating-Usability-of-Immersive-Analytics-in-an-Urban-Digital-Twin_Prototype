using System;

namespace UrbanAnalytics.Study
{
    /// <summary>
    /// One guided scenario (scenarios.json in the city package).
    ///
    /// A scenario shows a view (<see cref="presetId"/>, optionally a second
    /// matched view to switch to), the scenario text (<see cref="question"/>),
    /// the data shown (<see cref="data"/>: variable, unit, spatial unit,
    /// year) and its sub-questions (<see cref="parts"/>). Comparison areas
    /// A/B are picked from the data by a rule (<see cref="pair"/>), never by
    /// hand, and the expected answers of the sub-questions are computed from
    /// the packaged data when the answer is submitted (StudyAnswerKey).
    /// </summary>
    [Serializable]
    public sealed class StudyScenario
    {
        public string id;
        public string title;

        /// <summary>Visualization preset id (catalog entry) shown for this scenario.</summary>
        public string presetId;

        /// <summary>Scenario text shown to the participant.</summary>
        public string question;

        /// <summary>
        /// Optional wording for the VR condition (controllers instead of
        /// mouse/keys). Empty = <see cref="question"/> is used.
        /// </summary>
        public string questionVr;

        /// <summary>
        /// What is shown, one line each: role, variable, unit, spatial unit
        /// and data year (e.g. "Height · Population density · persons/km² ·
        /// 250 m grid (SCB) · 2024").
        /// </summary>
        public string[] data;

        /// <summary>Optional how-to line under the scenario text (e.g. how to put an area into A).</summary>
        public string instruction;
        public string instructionVr;

        /// <summary>Optional second, matched view (same geometry and scales) to switch to.</summary>
        public string alternatePresetId;

        /// <summary>Button labels for the two views, e.g. "V – Left Party" / "S – Social Democrats".</summary>
        public string presetLabel;
        public string alternateLabel;

        /// <summary>Rule that picks comparison areas A and B from the data (optional).</summary>
        public StudyPairRule pair;

        /// <summary>Sub-questions; their expected answers are computed from the data.</summary>
        public StudyPart[] parts;

        /// <summary>Legacy single question: answer options (empty = none).</summary>
        public string[] options;

        /// <summary>Legacy single question: the correct option.</summary>
        public string expected;

        public bool training;

        /// <summary>The question for a condition ("vr" uses <see cref="questionVr"/> when set).</summary>
        public string QuestionFor(string condition) =>
            condition == "vr" && !string.IsNullOrEmpty(questionVr) ? questionVr : question;

        public string InstructionFor(string condition) =>
            condition == "vr" && !string.IsNullOrEmpty(instructionVr) ? instructionVr : instruction;

        public bool HasAlternateView =>
            !string.IsNullOrEmpty(alternatePresetId);

        public bool HasPair =>
            pair != null && !string.IsNullOrEmpty(pair.spatialLayerId);
    }


    /// <summary>
    /// Picks comparison areas from the data, so locations are not chosen by
    /// hand: the eligible units of <see cref="spatialLayerId"/> (valid values,
    /// optional id prefix and minimum filter) are sorted by a score and the
    /// units at quantiles <see cref="quantileA"/> / <see cref="quantileB"/>
    /// are taken. The score is the variable's value, or, with a second
    /// variable, the sum of both variables' percentile ranks. A negative
    /// quantile leaves that slot for the participant to fill (Compare tool).
    /// With <see cref="counterbalance"/>, A and B are swapped for half of the
    /// participants (deterministic per participant and scenario), so "A" is
    /// not always the higher one.
    /// </summary>
    [Serializable]
    public sealed class StudyPairRule
    {
        public string spatialLayerId;

        /// <summary>Only unit ids starting with this (e.g. "ruta:250_" = 250 m cells).</summary>
        public string unitPrefix;

        public string dataLayerId;
        public string variableId;

        /// <summary>Optional second variable: score = sum of percentile ranks.</summary>
        public string dataLayerId2;
        public string variableId2;

        /// <summary>
        /// Score = relative strength of the first party minus that of the
        /// second (share / municipal share each, both in dataLayerId), instead
        /// of a value or rank sum. E5: B = a district where S is relatively
        /// stronger than V.
        /// </summary>
        public bool partyIndexDifference;

        /// <summary>Optional: only units with a valid value for this variable too.</summary>
        public string requireDataLayerId;
        public string requireVariableId;

        /// <summary>Optional filter: unit is eligible when this value ≥ filterMin.</summary>
        public string filterDataLayerId;
        public string filterVariableId;
        public double filterMin;

        public double quantileA = -1.0;
        public double quantileB = -1.0;

        public bool counterbalance;
    }


    /// <summary>
    /// A sub-question. Kinds:
    ///   compare         — A, B or about the same on one variable (expected
    ///                     from the two slots' values and a tolerance);
    ///   compareCombined — the same on the sum of the percentile ranks of
    ///                     two variables;
    ///   relativeParty   — in one slot, which of two parties is relatively
    ///                     stronger: share / municipal share (municipal share
    ///                     = share − "&lt;variable&gt;_diff");
    ///   pattern         — over all eligible units: Spearman ρ of two
    ///                     variables; |ρ| ≥ threshold = consistent (positive /
    ///                     negative), else mixed;
    ///   slotCheck       — not asked: checks the participant's own pick (slot
    ///                     value in the top fraction of the variable);
    ///   open            — answered aloud; the facilitator may code it
    ///                     (covered / partly / not covered);
    ///   choice          — fixed options with a fixed expected answer.
    /// Options default per kind; the expected answer is one of them.
    /// </summary>
    [Serializable]
    public sealed class StudyPart
    {
        public string id;
        public string kind;
        public string question;

        public string[] options;

        /// <summary>choice only: the correct option.</summary>
        public string expected;

        public string dataLayerId;
        public string variableId;
        public string dataLayerId2;
        public string variableId2;

        /// <summary>relativeParty / slotCheck: "A" or "B".</summary>
        public string slot;

        /// <summary>
        /// compare: below this difference the answer is "about the same"
        /// (absolute in the variable's unit, or a fraction of the larger
        /// value when <see cref="relative"/>); compareCombined: in percentile
        /// rank points (0–2); relativeParty: in index points.
        /// </summary>
        public double tolerance;
        public bool relative;

        /// <summary>pattern: |ρ| needed for "consistent" (default 0.5).</summary>
        public double threshold;

        /// <summary>slotCheck: the top fraction counted as "high" (default 0.25).</summary>
        public double topFraction;

        public bool IsOpen => kind == "open";

        public bool IsHidden => kind == "slotCheck";

        /// <summary>Asked with options that need an answer.</summary>
        public bool NeedsAnswer => !IsOpen && !IsHidden;
    }


    [Serializable]
    public sealed class StudyScenarioFile
    {
        public string studyId;
        public StudyScenario[] scenarios;
    }
}
