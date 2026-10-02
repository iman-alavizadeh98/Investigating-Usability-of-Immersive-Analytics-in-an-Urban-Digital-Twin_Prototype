using NUnit.Framework;
using UrbanAnalytics.Interaction;

namespace UrbanAnalytics.Tests
{
    /// <summary>
    /// EditMode tests for the interaction model: entity
    /// references, percentile ranks and value formatting.
    /// Picking, highlights and copies need a loaded scene and are
    /// verified in play mode (see docs/UNITY_DESKTOP_INTERACTION.md).
    /// </summary>
    public sealed class InteractionTests
    {
        // ---------------------------------------------------------
        // Entity references
        // ---------------------------------------------------------

        [Test]
        public void UnitReference_IsItsOwnBlock()
        {
            var cell = EntityReference.ForUnit("ruta_250", "ruta_250:1");

            Assert.IsTrue(cell.TryGetUnit(out EntityReference block));
            Assert.AreEqual(cell, block);
            Assert.AreEqual("ruta_250:1", block.UnitId);
        }


        [Test]
        public void BuildingReference_ResolvesToItsCell()
        {
            var building = EntityReference.ForBuilding("building:7", "ruta_250:1", "ruta_250");

            Assert.IsTrue(building.TryGetUnit(out EntityReference block));
            Assert.AreEqual(EntityKind.SpatialUnit, block.Kind);
            Assert.AreEqual("ruta_250:1", block.Id);
            Assert.AreEqual("ruta_250", block.SpatialLayerId);
        }


        [Test]
        public void UnassociatedBuilding_HasNoBlock()
        {
            var building = EntityReference.ForBuilding("building:7", null, null);

            Assert.IsTrue(building.IsValid);
            Assert.IsFalse(building.HasUnit);
            Assert.IsFalse(building.TryGetUnit(out _));
        }


        [Test]
        public void References_CompareByKindAndId()
        {
            Assert.AreEqual(
                EntityReference.ForUnit("ruta_250", "ruta_250:1"),
                EntityReference.ForUnit("ruta_250", "ruta_250:1"));

            // Same ID string, different kind: not equal.
            Assert.AreNotEqual(
                EntityReference.ForUnit("x", "a:1"),
                EntityReference.ForBuilding("a:1", null, null));

            Assert.IsFalse(default(EntityReference).IsValid);
        }


        [Test]
        public void ShortId_DropsLayerPrefix()
        {
            Assert.AreEqual("3175006390000", EntityReference.ShortId("ruta_250:3175006390000"));
            Assert.AreEqual("plain", EntityReference.ShortId("plain"));
        }


        // ---------------------------------------------------------
        // Percentile rank (mid-rank for ties)
        // ---------------------------------------------------------

        [Test]
        public void PercentileRank_UsesMidRank()
        {
            double[] sorted = { 1, 2, 3, 4 };

            Assert.AreEqual(12.5, EntityInfoBuilder.PercentileRank(sorted, 1).Value, 1e-12);
            Assert.AreEqual(87.5, EntityInfoBuilder.PercentileRank(sorted, 4).Value, 1e-12);
            // Not in the data: share strictly below.
            Assert.AreEqual(50.0, EntityInfoBuilder.PercentileRank(sorted, 2.5).Value, 1e-12);
        }


        [Test]
        public void PercentileRank_TiesShareOneRank()
        {
            double[] sorted = { 5, 5, 5, 5 };

            Assert.AreEqual(50.0, EntityInfoBuilder.PercentileRank(sorted, 5).Value, 1e-12);
        }


        [Test]
        public void PercentileRank_EmptyIsNull()
        {
            Assert.IsNull(EntityInfoBuilder.PercentileRank(new double[0], 1));
        }


        // ---------------------------------------------------------
        // Formatting
        // ---------------------------------------------------------

        [Test]
        public void FormatNumber_IsInvariantAndCompact()
        {
            Assert.AreEqual("342,948", EntityInfoBuilder.FormatNumber(342948));
            Assert.AreEqual("-3,975,825", EntityInfoBuilder.FormatNumber(-3975825));
            Assert.AreEqual("12", EntityInfoBuilder.FormatNumber(12));
            Assert.AreEqual("13.8", EntityInfoBuilder.FormatNumber(13.84));
            Assert.AreEqual("0.25", EntityInfoBuilder.FormatNumber(0.25));
            Assert.AreEqual("—", EntityInfoBuilder.FormatNumber(double.NaN));
        }


        [Test]
        public void FormatPercentile_RoundsToWholePercent()
        {
            Assert.AreEqual("p52", EntityInfoBuilder.FormatPercentile(51.6));
            Assert.AreEqual(string.Empty, EntityInfoBuilder.FormatPercentile(null));
        }
    }
}
