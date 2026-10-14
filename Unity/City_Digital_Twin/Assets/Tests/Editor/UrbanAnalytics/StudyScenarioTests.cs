using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UrbanAnalytics.Study;

namespace UrbanAnalytics.Tests
{
    /// <summary>
    /// EditMode tests for the evaluation scenarios (2026-10-09): the math
    /// that picks comparison areas and derives expected answers
    /// (StudyAnswerKey), and the scenario file itself (presets in the
    /// catalog, variables in the package — the same checks as the Python
    /// package builder, runnable without Python).
    /// </summary>
    public sealed class StudyScenarioTests
    {
        private static string RepoRoot =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));

        private static string RepoConfigs =>
            Path.Combine(RepoRoot, "configs/visualizations/evaluation");

        private static string PatternConfigs =>
            Path.Combine(RepoRoot, "configs/visualizations/pattern");

        private static string PackageDir =>
            Path.Combine(Application.streamingAssetsPath, "cities/helsingborg");


        [Test]
        public void PickAtQuantile_TakesSortedPosition_TiesById()
        {
            var scores = new Dictionary<string, double>
            {
                ["c"] = 3, ["a"] = 1, ["b"] = 2, ["d"] = 4, ["e"] = 5
            };

            Assert.AreEqual("a", StudyAnswerKey.PickAtQuantile(scores, 0.0));
            Assert.AreEqual("c", StudyAnswerKey.PickAtQuantile(scores, 0.5));
            Assert.AreEqual("e", StudyAnswerKey.PickAtQuantile(scores, 1.0));
            Assert.AreEqual("d", StudyAnswerKey.PickAtQuantile(scores, 0.85)); // round(0.85 · 4) = 3
            Assert.IsNull(StudyAnswerKey.PickAtQuantile(scores, -1.0));

            var ties = new Dictionary<string, double> { ["y"] = 1, ["x"] = 1 };
            Assert.AreEqual("x", StudyAnswerKey.PickAtQuantile(ties, 0.0));
        }


        [Test]
        public void PercentileRanks_ShareTies()
        {
            var ranks = StudyAnswerKey.PercentileRanks(new Dictionary<string, double>
            {
                ["a"] = 10, ["b"] = 20, ["c"] = 20, ["d"] = 30
            });

            Assert.AreEqual(0.0, ranks["a"], 1e-9);
            Assert.AreEqual(0.5, ranks["b"], 1e-9);
            Assert.AreEqual(0.5, ranks["c"], 1e-9);
            Assert.AreEqual(1.0, ranks["d"], 1e-9);
        }


        [Test]
        public void Spearman_MonotoneAndReversed()
        {
            var x = new List<double> { 1, 2, 3, 4, 5 };

            Assert.AreEqual(1.0, StudyAnswerKey.Spearman(x, new List<double> { 2, 4, 8, 16, 32 }), 1e-9);
            Assert.AreEqual(-1.0, StudyAnswerKey.Spearman(x, new List<double> { 9, 7, 5, 3, 1 }), 1e-9);
            Assert.IsTrue(double.IsNaN(StudyAnswerKey.Spearman(new List<double> { 1, 2 }, new List<double> { 1, 2 })));
        }


        [Test]
        public void Compare_AbsoluteAndRelativeTolerance()
        {
            Assert.AreEqual(StudyAnswerKey.ExpectA, StudyAnswerKey.Compare(30, 20, 2, false));
            Assert.AreEqual(StudyAnswerKey.ExpectB, StudyAnswerKey.Compare(20, 30, 2, false));
            Assert.AreEqual(StudyAnswerKey.ExpectSame, StudyAnswerKey.Compare(21, 20, 2, false));
            // 5 % of 1000 = 50
            Assert.AreEqual(StudyAnswerKey.ExpectSame, StudyAnswerKey.Compare(1000, 960, 0.05, true));
            Assert.AreEqual(StudyAnswerKey.ExpectA, StudyAnswerKey.Compare(1000, 900, 0.05, true));
        }


        [Test]
        public void Pattern_UsesThreshold()
        {
            Assert.AreEqual(0, StudyAnswerKey.Pattern(0.6, 0.5));
            Assert.AreEqual(1, StudyAnswerKey.Pattern(-0.7, 0.5));
            Assert.AreEqual(2, StudyAnswerKey.Pattern(0.3, 0.5));
            Assert.AreEqual(2, StudyAnswerKey.Pattern(double.NaN, 0.5));
        }


        [Test]
        public void Counterbalanced_IsDeterministicAndVaries()
        {
            Assert.AreEqual(
                StudyAnswerKey.Counterbalanced("P01", "E1"),
                StudyAnswerKey.Counterbalanced("P01", "E1"));

            int swapped = Enumerable.Range(1, 40)
                .Count(i => StudyAnswerKey.Counterbalanced($"P{i:00}", "E1"));

            Assert.That(swapped, Is.InRange(10, 30), "about half of the participants are swapped");
        }


        [Test]
        public void ScenarioFile_ReferencesExistingPresetsAndVariables()
        {
            CheckScenarioFolder(RepoConfigs);
        }


        [Test]
        public void PatternStudy_ReferencesExistingPresetsAndVariables()
        {
            CheckScenarioFolder(PatternConfigs);
        }


        private static void CheckScenarioFolder(string folder)
        {
            string scenariosPath = Path.Combine(folder, "scenarios.json");
            Assume.That(File.Exists(scenariosPath), scenariosPath + " not found");

            StudyScenarioFile file = JsonUtility.FromJson<StudyScenarioFile>(File.ReadAllText(scenariosPath));
            string catalog = File.ReadAllText(Path.Combine(folder, "catalog.json"));

            var kinds = new HashSet<string> { "compare", "compareCombined", "relativeParty", "pattern", "slotCheck", "open", "choice" };
            var problems = new List<string>();

            // Variables per data layer, when the (git-ignored) package is present.
            var variables = new Dictionary<string, string>();

            foreach (string layer in Directory.Exists(PackageDir)
                         ? Directory.GetDirectories(Path.Combine(PackageDir, "data_layers"))
                         : new string[0])
            {
                string json = Path.Combine(layer, "layer.json");

                if (File.Exists(json))
                {
                    variables[Path.GetFileName(layer)] = File.ReadAllText(json);
                }
            }

            void Var(string scenario, string layer, string variable)
            {
                if (string.IsNullOrEmpty(variable) || variables.Count == 0)
                {
                    return;
                }

                if (!variables.TryGetValue(layer ?? "", out string json))
                {
                    problems.Add($"{scenario}: data layer '{layer}' not in package");
                }
                else if (!json.Contains($"\"id\": \"{variable}\""))
                {
                    problems.Add($"{scenario}: variable '{layer}.{variable}' not in package");
                }
            }

            foreach (StudyScenario s in file.scenarios)
            {
                if (!catalog.Contains($"\"id\": \"{s.presetId}\""))
                    problems.Add($"{s.id}: preset '{s.presetId}' not in catalog");

                if (s.HasAlternateView && !catalog.Contains($"\"id\": \"{s.alternatePresetId}\""))
                    problems.Add($"{s.id}: preset '{s.alternatePresetId}' not in catalog");

                if (s.HasPair)
                {
                    Var(s.id, s.pair.dataLayerId, s.pair.variableId);
                    Var(s.id, s.pair.dataLayerId2, s.pair.variableId2);
                    Var(s.id, s.pair.requireDataLayerId, s.pair.requireVariableId);
                    Var(s.id, s.pair.filterDataLayerId, s.pair.filterVariableId);
                }

                foreach (StudyPart p in s.parts ?? new StudyPart[0])
                {
                    if (!kinds.Contains(p.kind))
                        problems.Add($"{s.id}.{p.id}: unknown kind '{p.kind}'");

                    if (!p.IsOpen && p.kind != "choice" && !s.HasPair)
                        problems.Add($"{s.id}.{p.id}: needs a pair rule");

                    Var(s.id, p.dataLayerId, p.variableId);
                    Var(s.id, string.IsNullOrEmpty(p.dataLayerId2) ? p.dataLayerId : p.dataLayerId2, p.variableId2);

                    if (p.kind == "relativeParty")
                    {
                        Var(s.id, p.dataLayerId, p.variableId + "_diff");
                        Var(s.id, p.dataLayerId, p.variableId2 + "_diff");
                    }

                    if (StudyAnswerKey.OptionsOf(p).Length == 0 && p.NeedsAnswer)
                        problems.Add($"{s.id}.{p.id}: no options");
                }

                if (!s.training && (s.data == null || s.data.Length == 0))
                    problems.Add($"{s.id}: no data lines (variable, unit, spatial unit, year)");
            }

            Assert.IsEmpty(problems, string.Join("\n", problems));
        }


        [System.Serializable]
        private sealed class StudyDesign
        {
            public StudyDesignScenario[] scenarios;
        }


        [System.Serializable]
        private sealed class StudyDesignScenario
        {
            public string id;
            public string question;
        }


        [Test]
        public void PatternStudy_QuestionsAreWordForWordFromTheStudyDesign()
        {
            string designPath = Path.Combine(RepoRoot, "configs/study/pattern_scenarios.json");
            string unityPath = Path.Combine(PatternConfigs, "scenarios.json");
            Assume.That(File.Exists(designPath) && File.Exists(unityPath), "pattern study files not found");

            StudyDesign design = JsonUtility.FromJson<StudyDesign>(File.ReadAllText(designPath));
            StudyScenarioFile unity = JsonUtility.FromJson<StudyScenarioFile>(File.ReadAllText(unityPath));

            foreach (StudyDesignScenario expected in design.scenarios)
            {
                StudyScenario actual = unity.scenarios.FirstOrDefault(s => s.id == expected.id);
                Assert.IsNotNull(actual, expected.id + " missing in configs/visualizations/pattern/scenarios.json");
                Assert.AreEqual(expected.question, actual.question, expected.id + ": question must be word for word");
                Assert.IsTrue(actual.parts == null || actual.parts.Length == 0, expected.id + ": no multiple-choice parts");
            }

            Assert.AreEqual(design.scenarios.Length + 1, unity.scenarios.Count(s => !s.optional), "the study scenarios plus the practice T0");
            Assert.IsTrue(unity.scenarios[0].training, "T0 practice comes first");
            Assert.IsTrue(unity.scenarios.Where(s => s.optional).All(s => System.Array.IndexOf(unity.scenarios, s) > design.scenarios.Length),
                "the optional showcase comes after the study scenarios");
        }


        [Test]
        public void UnitFilter_RejectedUnitsHaveNoValueAndLeaveTheScale()
        {
            var definition = JsonUtility.FromJson<UrbanAnalytics.Data.DataLayerDefinition>(
                "{\"id\":\"test_layer\",\"targetSpatialLayerId\":\"ruta\"}");

            var layer = new UrbanAnalytics.Data.DataLayer(
                definition,
                new[] { "ruta:250_a", "ruta:250_b", "ruta:1000_c" },
                new Dictionary<string, UrbanAnalytics.Data.DataColumn>
                {
                    ["share"] = new UrbanAnalytics.Data.FloatDataColumn(new[] { 10.0, 20.0, 30.0 }, new[] { true, true, true })
                });

            // 250 m cells only, and not "b" (as if it had fewer than 50 residents).
            var filtered = layer.WithUnitFilter(u => u.StartsWith("ruta:250_") && u != "ruta:250_b");

            Assert.IsTrue(filtered.TryGetDouble("ruta:250_a", "share", out double a));
            Assert.AreEqual(10.0, a);
            Assert.IsFalse(filtered.TryGetDouble("ruta:250_b", "share", out _));
            Assert.IsFalse(filtered.TryGetDouble("ruta:1000_c", "share", out _));
            Assert.IsTrue(filtered.TryGetNumericValues("share", out double[] values));
            CollectionAssert.AreEqual(new[] { 10.0 }, values, "scales see only the kept units");
            Assert.IsTrue(layer.TryGetDouble("ruta:250_b", "share", out _), "the original layer is unchanged");
        }


        [Test]
        public void StudyOranges_FiveClassesGiveTheStudyMapColours()
        {
            Assert.IsTrue(UrbanAnalytics.Visualization.ColorPaletteLibrary.TryGet("study_oranges5", out var palette));

            string[] expected = { "FBCAA9", "F6A77A", "EB6834", "C4521F", "9A3E15" };

            for (int i = 0; i < 5; i++)
            {
                // A 5-class quantile scale maps class i to i / 4.
                Color32 c = palette.Evaluate(i / 4.0f);
                Assert.AreEqual(expected[i], ColorUtility.ToHtmlStringRGB(c), "class " + (i + 1));
            }
        }
    }
}
