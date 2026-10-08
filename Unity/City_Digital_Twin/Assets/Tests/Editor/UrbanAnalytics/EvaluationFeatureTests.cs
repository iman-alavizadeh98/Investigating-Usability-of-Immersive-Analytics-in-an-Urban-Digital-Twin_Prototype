using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UrbanAnalytics.Associations;
using UrbanAnalytics.Study;
using UrbanAnalytics.Visualization;
using UrbanAnalytics.Visualization.UI;

namespace UrbanAnalytics.Tests
{
    /// <summary>
    /// EditMode tests for the evaluation build (2026-10-07): two-sided
    /// heights with one scale per side, the package association format,
    /// the study session log and legend number formatting.
    /// </summary>
    public sealed class EvaluationFeatureTests
    {
        [Test]
        public void TwoSidedExtent_UsesEachSidesMaximum()
        {
            HeightExtent extent = HeightExtentMath.TwoSided(1.0f, 0.5f, 0.2f, 0.12f);

            Assert.AreEqual(0.2f, extent.Top, 1e-6f);
            Assert.AreEqual(-0.06f, extent.Bottom, 1e-6f);
            Assert.IsTrue(extent.HasData);
        }


        [Test]
        public void TwoSidedExtent_SharedMaximumIsUnchanged()
        {
            HeightExtent shared = HeightExtentMath.TwoSided(0.5f, 0.25f, 0.4f);

            Assert.AreEqual(0.2f, shared.Top, 1e-6f);
            Assert.AreEqual(-0.1f, shared.Bottom, 1e-6f);
        }


        [Test]
        public void AssociationPairs_ParseAndRegister()
        {
            const string json =
                "{\"schemaVersion\":\"1.0\",\"associationId\":\"buildings_to_deso\"," +
                "\"sourceIds\":[\"building:a\",\"building:b\"]," +
                "\"targetIds\":[\"deso:1283C1400\",\"deso:1283C1390\"]}";

            AssociationPairsDto pairs = JsonUtility.FromJson<AssociationPairsDto>(json);
            Assert.AreEqual(2, pairs.sourceIds.Length);

            var go = new GameObject("associations");

            try
            {
                AssociationManager manager = go.AddComponent<AssociationManager>();

                for (int i = 0; i < pairs.sourceIds.Length; i++)
                {
                    manager.Register(pairs.associationId, pairs.sourceIds[i], pairs.targetIds[i]);
                }

                Assert.IsTrue(manager.TryResolve("buildings_to_deso", "building:b", out string target));
                Assert.AreEqual("deso:1283C1390", target);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }


        [System.Serializable]
        private sealed class LogLine
        {
            public string session;
            public string participant;
            public string @event;
            public string answer;
            public bool correct;
            public int confidence;
        }


        [Test]
        public void StudyLog_WritesParsableJsonLinesAndCsv()
        {
            string folder = Path.Combine(Path.GetTempPath(), "ua_studylog_test_" + System.Guid.NewGuid().ToString("N"));

            try
            {
                string jsonPath;
                string csvPath;

                using (var log = new StudyLog(folder, "P 07", "desktop"))
                {
                    log.Write("scenario_answer",
                        ("answer", "Low income, \"quoted\"\nnew line"),
                        ("correct", (bool?)true),
                        ("confidence", 4),
                        ("position", new Vector3(1.5f, 2.0f, -3.25f)),
                        ("missing", null));
                    log.WriteAnswer("S1", "s1", "Furutorp, Helsingborg", "Furutorp, Helsingborg", true, 4, 12.5, 3);
                    jsonPath = log.LogPath;
                    csvPath = log.AnswersPath;
                    StringAssert.Contains("P_07", log.SessionId);
                }

                string[] lines = File.ReadAllLines(jsonPath);
                Assert.AreEqual(1, lines.Length);
                LogLine parsed = JsonUtility.FromJson<LogLine>(lines[0]);
                Assert.AreEqual("scenario_answer", parsed.@event);
                Assert.AreEqual("P_07", parsed.participant);
                Assert.AreEqual("Low income, \"quoted\"\nnew line", parsed.answer);
                Assert.IsTrue(parsed.correct);
                Assert.AreEqual(4, parsed.confidence);
                StringAssert.Contains("\"position\":[1.5,2,-3.25]", lines[0]);
                StringAssert.Contains("\"missing\":null", lines[0]);

                string[] csv = File.ReadAllLines(csvPath);
                Assert.AreEqual(2, csv.Length);
                StringAssert.Contains("\"Furutorp, Helsingborg\"", csv[1]);
                Assert.AreEqual("3", csv[1].Split(',').Last());
            }
            finally
            {
                if (Directory.Exists(folder))
                {
                    Directory.Delete(folder, true);
                }
            }
        }


        [Test]
        public void RankScale_SpreadsSkewedValuesEvenly()
        {
            // One huge outlier: a linear scale would put the other four near 0.
            double[] values = { 1.0, 2.0, 3.0, 4.0, 1000.0 };
            ResolvedNumericScale scale = VisualizationScaleUtility.Resolve(
                values,
                JsonUtility.FromJson<NumericScaleSpec>("{\"type\":4}")
            );

            Assert.AreEqual(ScaleType.Rank, scale.Type);
            Assert.AreEqual(0.0f, scale.Normalize(1.0), 1e-6f);
            Assert.AreEqual(0.25f, scale.Normalize(2.0), 1e-6f);
            Assert.AreEqual(0.5f, scale.Normalize(3.0), 1e-6f);
            Assert.AreEqual(1.0f, scale.Normalize(1000.0), 1e-6f);
            Assert.AreEqual(0.375f, scale.Normalize(2.5), 1e-6f);      // between neighbours
            Assert.AreEqual(3.0, scale.ValueAt(0.5f), 1e-9);            // median for the legend
        }


        [Test]
        public void RankScale_TiesShareTheirMeanPosition()
        {
            double[] values = { 0.0, 5.0, 5.0, 5.0, 10.0 };
            ResolvedNumericScale scale = VisualizationScaleUtility.Resolve(
                values,
                JsonUtility.FromJson<NumericScaleSpec>("{\"type\":4}")
            );

            Assert.AreEqual(0.5f, scale.Normalize(5.0), 1e-6f);         // positions 1..3 → 2 of 4
        }


        [TestCase(0.0, "0")]
        [TestCase(12.34, "12.3")]
        [TestCase(1234.0, "1234")]
        [TestCase(286000.0, "286 k")]
        [TestCase(1250000.0, "1.3 M")]
        public void LegendNumbers_AreCompact(double value, string expected)
        {
            Assert.AreEqual(expected, LegendStackView.Format(value));
        }
    }
}
