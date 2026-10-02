using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UrbanAnalytics.Visualization;

namespace UrbanAnalytics.Tests
{
    /// <summary>
    /// EditMode tests for numeric scales and colour palettes.
    /// Run: Window > General > Test Runner > EditMode, or
    /// `unity command run_tests` with the Editor open.
    /// </summary>
    public sealed class ScaleAndPaletteTests
    {
        private const double Tolerance = 1e-9;


        private static NumericScaleSpec Scale(string json)
        {
            return JsonUtility.FromJson<NumericScaleSpec>(json);
        }


        private static double[] Range(int from, int to)
        {
            return Enumerable.Range(from, to - from + 1)
                .Select(v => (double)v)
                .ToArray();
        }


        // ---------------------------------------------------------
        // Percentile helper (must match numpy.percentile default)
        // ---------------------------------------------------------

        [Test]
        public void Percentile_MatchesNumpyLinearDefinition()
        {
            double[] sorted = { 1, 2, 3, 4 };
            Assert.AreEqual(2.5, VisualizationScaleUtility.Percentile(sorted, 50), Tolerance);

            double[] ten = Range(1, 10);
            Assert.AreEqual(9.1, VisualizationScaleUtility.Percentile(ten, 90), Tolerance);
            Assert.AreEqual(1.0, VisualizationScaleUtility.Percentile(ten, 0), Tolerance);
            Assert.AreEqual(10.0, VisualizationScaleUtility.Percentile(ten, 100), Tolerance);
        }


        // ---------------------------------------------------------
        // Backward compatibility
        // ---------------------------------------------------------

        [Test]
        public void LegacySpecWithoutType_IsLinearDataMinMax()
        {
            var spec = Scale("{\"domainMode\":0}");
            Assert.AreEqual(ScaleType.Linear, spec.Type);

            var scale = VisualizationScaleUtility.Resolve(new double[] { 0, 5, 10 }, spec);
            Assert.AreEqual(0.0, scale.Minimum, Tolerance);
            Assert.AreEqual(10.0, scale.Maximum, Tolerance);
            Assert.AreEqual(0.25f, scale.Normalize(2.5), 1e-6f);
        }


        [Test]
        public void LegacyConstructor_BehavesAsLinear()
        {
            var scale = new ResolvedNumericScale(0, 700000);
            Assert.AreEqual(0.5f, scale.Normalize(350000), 1e-6f);
            Assert.AreEqual(1.0f, scale.Normalize(9e9), 1e-6f);
            Assert.AreEqual(0.0f, scale.Normalize(-1), 1e-6f);
        }


        [Test]
        public void NonFiniteValues_AreIgnored()
        {
            var scale = VisualizationScaleUtility.Resolve(
                new[] { double.NaN, 1, double.PositiveInfinity, 3 },
                Scale("{\"domainMode\":0}"));

            Assert.AreEqual(1.0, scale.Minimum, Tolerance);
            Assert.AreEqual(3.0, scale.Maximum, Tolerance);
        }


        // ---------------------------------------------------------
        // Percentile domain
        // ---------------------------------------------------------

        [Test]
        public void PercentileDomain_IgnoresOutliersAndClamps()
        {
            double[] values = Range(1, 99).Concat(new[] { -1e9, 1e9 }).ToArray();
            var scale = VisualizationScaleUtility.Resolve(
                values,
                Scale("{\"domainMode\":2,\"lowerPercentile\":2,\"upperPercentile\":98}"));

            Assert.Less(scale.Maximum, 100.0);
            Assert.Greater(scale.Minimum, 0.0);
            Assert.AreEqual(1.0f, scale.Normalize(1e9), 1e-6f);
            Assert.AreEqual(0.0f, scale.Normalize(-1e9), 1e-6f);
            Assert.Greater(scale.ClampedLow, 0);
            Assert.Greater(scale.ClampedHigh, 0);
            StringAssert.Contains("percentile", scale.Description);
        }


        [Test]
        public void PercentileDomain_RejectsInvalidBounds()
        {
            Assert.Throws<InvalidOperationException>(() =>
                VisualizationScaleUtility.Resolve(
                    Range(1, 10),
                    Scale("{\"domainMode\":2,\"lowerPercentile\":90,\"upperPercentile\":10}")));
        }


        // ---------------------------------------------------------
        // Log
        // ---------------------------------------------------------

        [Test]
        public void Log_MapsDecadesEvenly()
        {
            var scale = VisualizationScaleUtility.Resolve(
                new double[] { 1, 10, 100, 1000 },
                Scale("{\"domainMode\":0,\"type\":1}"));

            Assert.AreEqual(0.0f, scale.Normalize(1), 1e-6f);
            Assert.AreEqual(1f / 3f, scale.Normalize(10), 1e-6f);
            Assert.AreEqual(2f / 3f, scale.Normalize(100), 1e-6f);
            Assert.AreEqual(1.0f, scale.Normalize(1000), 1e-6f);
        }


        [Test]
        public void Log_DataDomainSkipsNonPositiveValues()
        {
            var scale = VisualizationScaleUtility.Resolve(
                new double[] { -5, 0, 10, 1000 },
                Scale("{\"domainMode\":0,\"type\":1}"));

            Assert.AreEqual(10.0, scale.Minimum, Tolerance);
            Assert.AreEqual(2, scale.ClampedLow);
            Assert.AreEqual(0.0f, scale.Normalize(-5), 1e-6f);
            Assert.AreEqual(0.5f, scale.Normalize(100), 1e-6f);
        }


        [Test]
        public void Log_ManualNonPositiveMinimum_Throws()
        {
            Assert.Throws<InvalidOperationException>(() =>
                VisualizationScaleUtility.Resolve(
                    new double[] { 1, 10 },
                    Scale("{\"domainMode\":1,\"manualMinimum\":0,\"manualMaximum\":10,\"type\":1}")));
        }


        // ---------------------------------------------------------
        // Diverging
        // ---------------------------------------------------------

        [Test]
        public void Diverging_IsSymmetricAroundManualCentre()
        {
            var scale = VisualizationScaleUtility.Resolve(
                new double[] { -10, 0, 30 },
                Scale("{\"domainMode\":0,\"type\":2,\"divergingCenterMode\":0,\"divergingCenter\":0}"));

            Assert.AreEqual(-30.0, scale.Minimum, Tolerance);
            Assert.AreEqual(30.0, scale.Maximum, Tolerance);
            Assert.AreEqual(0.5f, scale.Normalize(0), 1e-6f);
            Assert.AreEqual(1.0f, scale.Normalize(30), 1e-6f);
            // -10 is a third of the way to the extent below the centre.
            Assert.AreEqual(0.5f - 10f / 60f, scale.Normalize(-10), 1e-6f);
        }


        [Test]
        public void Diverging_MedianCentre_MapsMedianToHalf()
        {
            var scale = VisualizationScaleUtility.Resolve(
                new double[] { 1, 2, 3, 4, 100 },
                Scale("{\"domainMode\":0,\"type\":2,\"divergingCenterMode\":1}"));

            Assert.AreEqual(3.0, scale.Center, Tolerance);
            Assert.AreEqual(0.5f, scale.Normalize(3), 1e-6f);
            StringAssert.Contains("median", scale.Description);
        }


        // ---------------------------------------------------------
        // Quantile
        // ---------------------------------------------------------

        [Test]
        public void Quantile_EqualCountClasses()
        {
            double[] values = Range(1, 100);
            var scale = VisualizationScaleUtility.Resolve(
                values,
                Scale("{\"type\":3,\"quantileClasses\":4}"));

            Assert.AreEqual(4, scale.ClassCount);
            CollectionAssert.AreEqual(new[] { 25.75, 50.5, 75.25 }, scale.Breaks);

            int[] counts = new int[4];
            foreach (double v in values)
                counts[scale.ClassOf(v)]++;
            CollectionAssert.AreEqual(new[] { 25, 25, 25, 25 }, counts);

            Assert.AreEqual(0.0f, scale.Normalize(1), 1e-6f);
            Assert.AreEqual(1f / 3f, scale.Normalize(30), 1e-6f);
            Assert.AreEqual(1.0f, scale.Normalize(100), 1e-6f);
        }


        [Test]
        public void Quantile_LegendIsStepped()
        {
            var scale = VisualizationScaleUtility.Resolve(
                Range(1, 100),
                Scale("{\"type\":3,\"quantileClasses\":4}"));

            Assert.AreEqual(0.0f, scale.LegendPositionToNormalized(0.10f), 1e-6f);
            Assert.AreEqual(1f / 3f, scale.LegendPositionToNormalized(0.30f), 1e-6f);
            Assert.AreEqual(1.0f, scale.LegendPositionToNormalized(0.99f), 1e-6f);
            Assert.AreEqual(1.0f, scale.LegendPositionToNormalized(1.0f), 1e-6f);
        }


        [Test]
        public void Quantile_RejectsBadClassCount()
        {
            Assert.Throws<InvalidOperationException>(() =>
                VisualizationScaleUtility.Resolve(Range(1, 10), Scale("{\"type\":3,\"quantileClasses\":1}")));
            Assert.Throws<InvalidOperationException>(() =>
                VisualizationScaleUtility.Resolve(Range(1, 10), Scale("{\"type\":3,\"quantileClasses\":13}")));
        }


        // ---------------------------------------------------------
        // Palettes
        // ---------------------------------------------------------

        [Test]
        public void Library_HasAllKindsAndValidPalettes()
        {
            var all = ColorPaletteLibrary.All.ToList();

            Assert.AreEqual(all.Count, all.Select(p => p.Id.ToLowerInvariant()).Distinct().Count());
            Assert.GreaterOrEqual(all.Count(p => p.Kind == ColorPaletteKind.Sequential), 3);
            Assert.GreaterOrEqual(all.Count(p => p.Kind == ColorPaletteKind.Diverging), 3);
            Assert.GreaterOrEqual(all.Count(p => p.Kind == ColorPaletteKind.Categorical), 3);

            foreach (var palette in all)
            {
                Assert.GreaterOrEqual(palette.StopCount, 2, palette.Id);
                Assert.IsFalse(string.IsNullOrWhiteSpace(palette.Source), palette.Id);
            }

            // Diverging palettes have an odd number of stops so the
            // neutral colour sits exactly at 0.5.
            foreach (var palette in all.Where(p => p.Kind == ColorPaletteKind.Diverging))
                Assert.AreEqual(1, palette.StopCount % 2, palette.Id);
        }


        [Test]
        public void Viridis_EndpointsAndMidpoint()
        {
            Assert.IsTrue(ColorPaletteLibrary.TryGet("VIRIDIS", out var viridis));
            Assert.AreEqual(new Color32(0x44, 0x01, 0x54, 255), viridis.Evaluate(0f));
            Assert.AreEqual(new Color32(0xfd, 0xe7, 0x25, 255), viridis.Evaluate(1f));
            Assert.AreEqual(new Color32(0x21, 0x91, 0x8c, 255), viridis.Evaluate(0.5f));
        }


        [Test]
        public void Diverging_PaletteNeutralAtHalf()
        {
            Assert.IsTrue(ColorPaletteLibrary.TryGet("rdbu", out var rdbu));
            Assert.AreEqual(new Color32(0xf7, 0xf7, 0xf7, 255), rdbu.Evaluate(0.5f));
        }


        [Test]
        public void Categorical_NeverBlends_AndWraps()
        {
            Assert.IsTrue(ColorPaletteLibrary.TryGet("tableau10", out var tab));
            var stops = Enumerable.Range(0, tab.StopCount).Select(tab.GetStop).ToList();

            for (int i = 0; i <= 100; i++)
                CollectionAssert.Contains(stops, tab.Evaluate(i / 100f));

            Assert.AreEqual(tab.GetStop(0), tab.GetCategory(tab.StopCount));
            Assert.AreEqual(tab.GetStop(tab.StopCount - 1), tab.GetCategory(-1));
        }


        [Test]
        public void ColorEncoding_UsesPaletteAndReverse()
        {
            var forward = JsonUtility.FromJson<ColorEncodingSettings>("{\"paletteId\":\"viridis\"}");
            var reversed = JsonUtility.FromJson<ColorEncodingSettings>("{\"paletteId\":\"viridis\",\"reverse\":true}");

            Assert.AreEqual(new Color32(0x44, 0x01, 0x54, 255), forward.Evaluate(0f));
            Assert.AreEqual(forward.Evaluate(0f), reversed.Evaluate(1f));
            Assert.AreEqual(forward.Evaluate(0.2f), reversed.Evaluate(0.8f));
        }


        [Test]
        public void ColorEncoding_UnknownPalette_Throws()
        {
            var settings = JsonUtility.FromJson<ColorEncodingSettings>("{\"paletteId\":\"not_a_palette\"}");
            Assert.Throws<InvalidOperationException>(() => settings.Evaluate(0.5f));
        }


        [Test]
        public void ColorEncoding_EmptyPalette_UsesCustomGradient()
        {
            var settings = JsonUtility.FromJson<ColorEncodingSettings>("{}");
            Assert.IsNull(settings.ResolvePalette());
            Assert.AreEqual((Color32)settings.GetGradient().Evaluate(0.3f), settings.Evaluate(0.3f));
        }


        [Test]
        public void Compatibility_FlagsPoorPairingsOnly()
        {
            ColorPaletteLibrary.TryGet("viridis", out var seq);
            ColorPaletteLibrary.TryGet("rdbu", out var div);
            ColorPaletteLibrary.TryGet("tableau10", out var cat);

            Assert.IsNull(ColorPaletteLibrary.CheckCompatibility(seq, ScaleType.Linear));
            Assert.IsNull(ColorPaletteLibrary.CheckCompatibility(seq, ScaleType.Quantile));
            Assert.IsNull(ColorPaletteLibrary.CheckCompatibility(div, ScaleType.Diverging));
            Assert.IsNull(ColorPaletteLibrary.CheckCompatibility(null, ScaleType.Diverging));

            Assert.IsNotNull(ColorPaletteLibrary.CheckCompatibility(seq, ScaleType.Diverging));
            Assert.IsNotNull(ColorPaletteLibrary.CheckCompatibility(div, ScaleType.Linear));
            Assert.IsNotNull(ColorPaletteLibrary.CheckCompatibility(cat, ScaleType.Linear));
            Assert.IsNotNull(ColorPaletteLibrary.CheckCompatibility(cat, ScaleType.Quantile));
        }
    }
}
