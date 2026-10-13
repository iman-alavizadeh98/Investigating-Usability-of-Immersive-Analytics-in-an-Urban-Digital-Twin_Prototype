using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

using UrbanAnalytics.Data;

namespace UrbanAnalytics.Study
{
    /// <summary>
    /// Picks the comparison areas of a scenario and computes the expected
    /// answers of its sub-questions from the packaged data, at runtime, so no
    /// location or answer is chosen by hand (and none is fixed in the
    /// scenario file). The pure math is static and unit-tested; the
    /// evaluator reads values through DataLayerManager.
    ///
    /// Rules (documented in docs/EVALUATION_SESSION.md, "How areas and expected answers are derived"):
    ///   * eligible units: valid values for every variable involved, the
    ///     rule's id prefix and minimum filter;
    ///   * pick: units sorted by score (ties by id), the unit at
    ///     round(q · (n − 1)) is taken;
    ///   * percentile rank: mean rank of ties / (n − 1), in [0, 1];
    ///   * compare: |a − b| ≤ tolerance (or ≤ tolerance · max(|a|, |b|)
    ///     when relative) = "about the same", else the larger one;
    ///   * relative party strength: share / municipal share, with the
    ///     municipal share = share − share_diff of the same district;
    ///   * pattern: Spearman ρ; ρ ≥ t = consistent positive, ρ ≤ −t =
    ///     consistent negative, else mixed (t = 0.5 by default).
    /// </summary>
    public static class StudyAnswerKey
    {
        public const int ExpectA = 0;
        public const int ExpectB = 1;
        public const int ExpectSame = 2;


        // =========================================================
        // PURE MATH
        // =========================================================

        /// <summary>The unit at quantile q of the scores (ascending; ties by id).</summary>
        public static string PickAtQuantile(
            IReadOnlyDictionary<string, double> scores,
            double quantile
        )
        {
            if (scores == null ||
                scores.Count == 0 ||
                quantile < 0.0)
            {
                return null;
            }

            List<KeyValuePair<string, double>> sorted =
                scores
                    .OrderBy(p => p.Value)
                    .ThenBy(p => p.Key, StringComparer.Ordinal)
                    .ToList();

            int index =
                (int)Math.Round(Math.Min(1.0, quantile) * (sorted.Count - 1), MidpointRounding.AwayFromZero);

            return sorted[index].Key;
        }


        /// <summary>Percentile rank in [0, 1] per unit; ties share their mean rank.</summary>
        public static Dictionary<string, double> PercentileRanks(
            IReadOnlyDictionary<string, double> values
        )
        {
            var result =
                new Dictionary<string, double>(StringComparer.Ordinal);

            if (values == null ||
                values.Count == 0)
            {
                return result;
            }

            List<KeyValuePair<string, double>> sorted =
                values.OrderBy(p => p.Value).ToList();

            int n =
                sorted.Count;

            int i =
                0;

            while (i < n)
            {
                int j =
                    i;

                while (j + 1 < n &&
                       sorted[j + 1].Value == sorted[i].Value)
                {
                    j++;
                }

                double rank =
                    n > 1 ? 0.5 * (i + j) / (n - 1) : 0.5;

                for (int k = i; k <= j; k++)
                {
                    result[sorted[k].Key] = rank;
                }

                i = j + 1;
            }

            return result;
        }


        /// <summary>Spearman rank correlation (Pearson of the tie-averaged ranks); NaN for fewer than 3 pairs.</summary>
        public static double Spearman(
            IReadOnlyList<double> x,
            IReadOnlyList<double> y
        )
        {
            if (x == null ||
                y == null ||
                x.Count != y.Count ||
                x.Count < 3)
            {
                return double.NaN;
            }

            double[] rx = Ranks(x);
            double[] ry = Ranks(y);

            double mx = rx.Average();
            double my = ry.Average();

            double sxy = 0.0, sxx = 0.0, syy = 0.0;

            for (int i = 0; i < rx.Length; i++)
            {
                double dx = rx[i] - mx;
                double dy = ry[i] - my;
                sxy += dx * dy;
                sxx += dx * dx;
                syy += dy * dy;
            }

            return sxx > 0.0 && syy > 0.0
                ? sxy / Math.Sqrt(sxx * syy)
                : double.NaN;
        }


        private static double[] Ranks(
            IReadOnlyList<double> values
        )
        {
            int n = values.Count;

            int[] order =
                Enumerable.Range(0, n).OrderBy(i => values[i]).ToArray();

            var ranks = new double[n];

            int a = 0;

            while (a < n)
            {
                int b = a;

                while (b + 1 < n && values[order[b + 1]] == values[order[a]])
                {
                    b++;
                }

                double rank = 0.5 * (a + b) + 1.0;

                for (int k = a; k <= b; k++)
                {
                    ranks[order[k]] = rank;
                }

                a = b + 1;
            }

            return ranks;
        }


        /// <summary>ExpectA, ExpectB or ExpectSame.</summary>
        public static int Compare(
            double a,
            double b,
            double tolerance,
            bool relative
        )
        {
            double limit =
                relative
                    ? tolerance * Math.Max(Math.Abs(a), Math.Abs(b))
                    : tolerance;

            if (Math.Abs(a - b) <= limit)
            {
                return ExpectSame;
            }

            return a > b ? ExpectA : ExpectB;
        }


        /// <summary>0 = consistent positive, 1 = consistent negative, 2 = mixed.</summary>
        public static int Pattern(
            double rho,
            double threshold
        )
        {
            if (double.IsNaN(rho))
            {
                return 2;
            }

            if (rho >= threshold) return 0;
            if (rho <= -threshold) return 1;
            return 2;
        }


        /// <summary>
        /// Deterministic coin per participant and scenario (FNV-1a of
        /// "participant|scenario"): true = swap A and B.
        /// </summary>
        public static bool Counterbalanced(
            string participantId,
            string scenarioId
        )
        {
            unchecked
            {
                uint hash = 2166136261;

                foreach (char c in (participantId ?? "") + "|" + (scenarioId ?? ""))
                {
                    hash ^= c;
                    hash *= 16777619;
                }

                return (hash & 1) == 1;
            }
        }


        /// <summary>Default options of a sub-question kind.</summary>
        public static string[] DefaultOptions(
            string kind
        )
        {
            switch (kind)
            {
                case "compare":
                case "compareCombined":
                    return new[] { "Area A", "Area B", "About the same" };

                case "pattern":
                    return new[] { "Consistent: both higher together", "Consistent: one higher where the other is lower", "Mixed: no consistent relationship" };

                case "open":
                    return new[] { "Covered", "Partly", "Not covered" };

                default:
                    return Array.Empty<string>();
            }
        }


        public static string[] OptionsOf(
            StudyPart part
        )
        {
            return part.options != null && part.options.Length > 0
                ? part.options
                : DefaultOptions(part.kind);
        }


        /// <summary>Short options go side by side; long ones (e.g. patterns) one per line.</summary>
        public static bool FitsOneRow(
            string[] options
        )
        {
            foreach (string option in options ?? Array.Empty<string>())
            {
                if (option != null && option.Length > 16)
                {
                    return false;
                }
            }

            return true;
        }


        public static string Format(
            double value
        )
        {
            return double.IsNaN(value)
                ? "NaN"
                : value.ToString("0.###", CultureInfo.InvariantCulture);
        }


        // =========================================================
        // EVALUATOR (reads the packaged data)
        // =========================================================

        /// <summary>The expected answer of one sub-question, with the numbers behind it.</summary>
        public sealed class Result
        {
            public string Expected;
            public string Basis;
        }


        /// <summary>
        /// Reads values from the data layers of the package (loaded on
        /// demand through DataLayerManager).
        /// </summary>
        public sealed class Evaluator
        {
            private readonly DataLayerManager dataLayers;

            private readonly Dictionary<string, DataLayer> loaded =
                new Dictionary<string, DataLayer>(StringComparer.Ordinal);


            public Evaluator(
                DataLayerManager dataLayers
            )
            {
                this.dataLayers = dataLayers;
            }


            public async Task PrepareAsync(
                StudyScenario scenario
            )
            {
                var ids = new HashSet<string>(StringComparer.Ordinal);

                void Add(string id)
                {
                    if (!string.IsNullOrEmpty(id)) ids.Add(id);
                }

                if (scenario.pair != null)
                {
                    Add(scenario.pair.dataLayerId);
                    Add(scenario.pair.dataLayerId2);
                    Add(scenario.pair.filterDataLayerId);
                    Add(scenario.pair.requireDataLayerId);
                }

                foreach (StudyPart part in scenario.parts ?? Array.Empty<StudyPart>())
                {
                    Add(part.dataLayerId);
                    Add(part.dataLayerId2);
                }

                foreach (string id in ids)
                {
                    if (!loaded.ContainsKey(id))
                    {
                        loaded[id] = await dataLayers.LoadLayerAsync(id);
                    }
                }
            }


            public bool TryGet(
                string dataLayerId,
                string variableId,
                string unitId,
                out double value
            )
            {
                value = double.NaN;

                return !string.IsNullOrEmpty(dataLayerId) &&
                       !string.IsNullOrEmpty(unitId) &&
                       loaded.TryGetValue(dataLayerId, out DataLayer layer) &&
                       layer != null &&
                       layer.TryGetDouble(unitId, variableId, out value) &&
                       !double.IsNaN(value);
            }


            /// <summary>
            /// The units the rule allows that have valid values for every
            /// listed (layer, variable).
            /// </summary>
            public List<string> Eligible(
                StudyPairRule rule,
                params (string Layer, string Variable)[] required
            )
            {
                var result = new List<string>();

                if (rule == null ||
                    !loaded.TryGetValue(rule.dataLayerId ?? "", out DataLayer baseLayer) ||
                    baseLayer == null)
                {
                    return result;
                }

                foreach (string unit in baseLayer.UnitIds)
                {
                    if (!string.IsNullOrEmpty(rule.unitPrefix) &&
                        !unit.StartsWith(rule.unitPrefix, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (!string.IsNullOrEmpty(rule.filterVariableId) &&
                        (!TryGet(rule.filterDataLayerId, rule.filterVariableId, unit, out double filter) ||
                         filter < rule.filterMin))
                    {
                        continue;
                    }

                    bool ok = TryGet(rule.dataLayerId, rule.variableId, unit, out _);

                    if (!string.IsNullOrEmpty(rule.requireVariableId))
                    {
                        ok &= TryGet(rule.requireDataLayerId, rule.requireVariableId, unit, out _);
                    }

                    foreach ((string layer, string variable) in required)
                    {
                        ok &= string.IsNullOrEmpty(variable) || TryGet(layer, variable, unit, out _);
                    }

                    if (ok)
                    {
                        result.Add(unit);
                    }
                }

                return result;
            }


            public Dictionary<string, double> Values(
                IEnumerable<string> units,
                string layer,
                string variable
            )
            {
                var values = new Dictionary<string, double>(StringComparer.Ordinal);

                foreach (string unit in units)
                {
                    if (TryGet(layer, variable, unit, out double v))
                    {
                        values[unit] = v;
                    }
                }

                return values;
            }


            /// <summary>Score per eligible unit: the value, or the sum of two percentile ranks.</summary>
            public Dictionary<string, double> Scores(
                StudyPairRule rule,
                List<string> units,
                string layer,
                string variable,
                string layer2,
                string variable2
            )
            {
                if (rule.partyIndexDifference)
                {
                    var difference = new Dictionary<string, double>(StringComparer.Ordinal);

                    foreach (string unit in units)
                    {
                        if (RelativeStrength(layer, variable, unit, out double first1) &&
                            RelativeStrength(layer, variable2, unit, out double second1))
                        {
                            difference[unit] = first1 - second1;
                        }
                    }

                    return difference;
                }

                Dictionary<string, double> first = Values(units, layer, variable);

                if (string.IsNullOrEmpty(variable2))
                {
                    return first;
                }

                Dictionary<string, double> r1 = PercentileRanks(first);
                Dictionary<string, double> r2 = PercentileRanks(Values(units, layer2, variable2));

                var sum = new Dictionary<string, double>(StringComparer.Ordinal);

                foreach (KeyValuePair<string, double> p in r1)
                {
                    if (r2.TryGetValue(p.Key, out double other))
                    {
                        sum[p.Key] = p.Value + other;
                    }
                }

                return sum;
            }


            /// <summary>Picks A and B by the scenario's rule (null where the participant picks).</summary>
            public (string A, string B, bool Swapped, string Basis) PickPair(
                StudyPairRule rule,
                string participantId,
                string scenarioId
            )
            {
                List<string> units =
                    rule.partyIndexDifference
                        ? Eligible(rule)
                        : Eligible(rule, (rule.dataLayerId2, rule.variableId2));

                Dictionary<string, double> scores =
                    Scores(rule, units, rule.dataLayerId, rule.variableId, rule.dataLayerId2, rule.variableId2);

                string a = PickAtQuantile(scores, rule.quantileA);
                string b = PickAtQuantile(scores, rule.quantileB);

                bool swap =
                    rule.counterbalance &&
                    a != null &&
                    b != null &&
                    Counterbalanced(participantId, scenarioId);

                if (swap)
                {
                    (a, b) = (b, a);
                }

                string basis =
                    $"{scores.Count} eligible units; score = " +
                    (rule.partyIndexDifference
                        ? $"index({rule.variableId}) - index({rule.variableId2})"
                        : string.IsNullOrEmpty(rule.variableId2)
                        ? rule.variableId
                        : $"rank({rule.variableId}) + rank({rule.variableId2})") +
                    $"; quantiles A={Format(rule.quantileA)} B={Format(rule.quantileB)}" +
                    (swap ? "; swapped (counterbalanced)" : string.Empty);

                return (a, b, swap, basis);
            }


            /// <summary>The expected answer of a sub-question for the current slots (null = cannot be computed).</summary>
            public Result Expect(
                StudyScenario scenario,
                StudyPart part,
                string slotA,
                string slotB
            )
            {
                string[] options = OptionsOf(part);

                switch (part.kind)
                {
                    case "choice":
                        return new Result { Expected = part.expected, Basis = "fixed" };

                    case "compare":
                    {
                        if (!TryGet(part.dataLayerId, part.variableId, slotA, out double a) ||
                            !TryGet(part.dataLayerId, part.variableId, slotB, out double b))
                        {
                            return new Result { Basis = "no value for A or B" };
                        }

                        int e = Compare(a, b, part.tolerance, part.relative);
                        return new Result
                        {
                            Expected = options[e],
                            Basis = $"{part.variableId}: A={Format(a)} B={Format(b)} tolerance={Format(part.tolerance)}{(part.relative ? " (relative)" : "")}"
                        };
                    }

                    case "compareCombined":
                    {
                        List<string> units = Eligible(scenario.pair, (part.dataLayerId, part.variableId), (part.dataLayerId2, part.variableId2));
                        Dictionary<string, double> r1 = PercentileRanks(Values(units, part.dataLayerId, part.variableId));
                        Dictionary<string, double> r2 = PercentileRanks(Values(units, part.dataLayerId2, part.variableId2));

                        if (slotA == null || slotB == null ||
                            !r1.ContainsKey(slotA) || !r1.ContainsKey(slotB) ||
                            !r2.ContainsKey(slotA) || !r2.ContainsKey(slotB))
                        {
                            return new Result { Basis = "A or B not among the eligible units" };
                        }

                        double a = r1[slotA] + r2[slotA];
                        double b = r1[slotB] + r2[slotB];
                        int e = Compare(a, b, part.tolerance, false);

                        return new Result
                        {
                            Expected = options[e],
                            Basis = $"rank({part.variableId}) + rank({part.variableId2}) over {units.Count} units: A={Format(a)} B={Format(b)} tolerance={Format(part.tolerance)}"
                        };
                    }

                    case "relativeParty":
                    {
                        string unit = part.slot == "B" ? slotB : slotA;

                        if (!RelativeStrength(part.dataLayerId, part.variableId, unit, out double first) ||
                            !RelativeStrength(part.dataLayerId, part.variableId2, unit, out double second))
                        {
                            return new Result { Basis = $"no values for slot {part.slot}" };
                        }

                        int e = Compare(first, second, part.tolerance, false);

                        return new Result
                        {
                            Expected = options[e],
                            Basis = $"slot {part.slot} {unit}: {part.variableId} index={Format(first)}, {part.variableId2} index={Format(second)} (share / municipal share) tolerance={Format(part.tolerance)}"
                        };
                    }

                    case "pattern":
                    {
                        List<string> units = Eligible(scenario.pair, (part.dataLayerId, part.variableId), (part.dataLayerId2, part.variableId2));
                        var x = new List<double>();
                        var y = new List<double>();

                        foreach (string unit in units)
                        {
                            if (TryGet(part.dataLayerId, part.variableId, unit, out double vx) &&
                                TryGet(part.dataLayerId2, part.variableId2, unit, out double vy))
                            {
                                x.Add(vx);
                                y.Add(vy);
                            }
                        }

                        double rho = Spearman(x, y);
                        double t = part.threshold > 0.0 ? part.threshold : 0.5;

                        return new Result
                        {
                            Expected = options[Pattern(rho, t)],
                            Basis = $"Spearman rho({part.variableId}, {part.variableId2}) = {Format(rho)} over {x.Count} units; threshold {Format(t)}"
                        };
                    }

                    case "slotCheck":
                    {
                        string unit = part.slot == "B" ? slotB : slotA;
                        List<string> units = Eligible(scenario.pair, (part.dataLayerId, part.variableId));
                        Dictionary<string, double> ranks = PercentileRanks(Values(units, part.dataLayerId, part.variableId));
                        double top = part.topFraction > 0.0 ? part.topFraction : 0.25;

                        if (unit == null || !ranks.TryGetValue(unit, out double rank))
                        {
                            return new Result { Expected = "high", Basis = $"slot {part.slot} empty or not eligible" };
                        }

                        return new Result
                        {
                            Expected = "high",
                            Basis = $"slot {part.slot} {unit}: percentile rank of {part.variableId} = {Format(rank)} (high = top {Format(top)})"
                        };
                    }

                    default:
                        return new Result { Basis = "open: no expected answer" };
                }
            }


            /// <summary>slotCheck: the participant's pick is in the top fraction.</summary>
            public bool? SlotCheck(
                StudyScenario scenario,
                StudyPart part,
                string slotA,
                string slotB
            )
            {
                string unit = part.slot == "B" ? slotB : slotA;
                List<string> units = Eligible(scenario.pair, (part.dataLayerId, part.variableId));
                Dictionary<string, double> ranks = PercentileRanks(Values(units, part.dataLayerId, part.variableId));
                double top = part.topFraction > 0.0 ? part.topFraction : 0.25;

                return unit != null && ranks.TryGetValue(unit, out double rank)
                    ? rank >= 1.0 - top
                    : (bool?)null;
            }


            /// <summary>Share / municipal share; municipal share = share − share_diff.</summary>
            private bool RelativeStrength(
                string layer,
                string variable,
                string unit,
                out double index
            )
            {
                index = double.NaN;

                if (!TryGet(layer, variable, unit, out double share) ||
                    !TryGet(layer, variable + "_diff", unit, out double diff))
                {
                    return false;
                }

                double municipal = share - diff;

                if (municipal <= 0.0)
                {
                    return false;
                }

                index = share / municipal;
                return true;
            }
        }
    }
}
