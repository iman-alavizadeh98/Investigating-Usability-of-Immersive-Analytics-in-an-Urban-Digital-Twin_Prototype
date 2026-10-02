using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UrbanAnalytics.Visualization;

namespace UrbanAnalytics.Tests
{
    /// <summary>
    /// EditMode tests for glyph maths/geometry, bidirectional
    /// height extents, multi-variable bindings and zero baselines.
    /// </summary>
    public sealed class GlyphAndHeightTests
    {
        private const float Eps = 1e-5f;


        // ---------------------------------------------------------
        // Stacked bars
        // ---------------------------------------------------------

        [Test]
        public void StackBoundaries_AreCumulativeShares()
        {
            float[] b = GlyphMath.StackBoundaries(new double[] { 1, 2, 3 }, 6f);
            CollectionAssert.AreEqual(new[] { 0f, 1f, 3f, 6f }, b);
        }


        [Test]
        public void StackBoundaries_ScaleToTotalHeight()
        {
            float[] b = GlyphMath.StackBoundaries(new double[] { 10, 30 }, 2f);
            Assert.AreEqual(0.5f, b[1], Eps);
            Assert.AreEqual(2f, b[2], Eps);
        }


        [Test]
        public void StackBoundaries_ZeroSum_IsFlat()
        {
            CollectionAssert.AreEqual(new[] { 0f, 0f, 0f }, GlyphMath.StackBoundaries(new double[] { 0, 0 }, 1f));
        }


        [Test]
        public void StackBoundaries_RejectNegative()
        {
            Assert.Throws<ArgumentException>(() => GlyphMath.StackBoundaries(new double[] { 1, -1 }, 1f));
        }


        // ---------------------------------------------------------
        // Radial
        // ---------------------------------------------------------

        [Test]
        public void RadialRadius_AreaProportionalToValue()
        {
            const float max = 2f;
            float r1 = GlyphMath.RadialRadius(0.25f, max);
            float r2 = GlyphMath.RadialRadius(1f, max);
            Assert.AreEqual(1f, r1, Eps);
            Assert.AreEqual(2f, r2, Eps);
            // area ratio == value ratio
            Assert.AreEqual(0.25f, (r1 * r1) / (r2 * r2), Eps);
            Assert.AreEqual(0f, GlyphMath.RadialRadius(-1f, max), Eps);
        }


        [Test]
        public void Wedges_StartNorthAndGoClockwise()
        {
            Vector3 north = GlyphMath.Direction(GlyphMath.WedgeStartAngle(0, 4));
            Vector3 east = GlyphMath.Direction(GlyphMath.WedgeStartAngle(1, 4));
            Assert.That(Vector3.Distance(north, Vector3.forward), Is.LessThan(Eps));
            Assert.That(Vector3.Distance(east, Vector3.right), Is.LessThan(Eps));
        }


        // ---------------------------------------------------------
        // Height extents
        // ---------------------------------------------------------

        [Test]
        public void Signed_CentreIsZero_EndsAreFullHeight()
        {
            HeightExtent mid = HeightExtentMath.Signed(0.5f, 2f);
            Assert.AreEqual(0f, mid.Top, Eps);
            Assert.AreEqual(0f, mid.Bottom, Eps);

            HeightExtent high = HeightExtentMath.Signed(1f, 2f);
            Assert.AreEqual(2f, high.Top, Eps);
            Assert.AreEqual(0f, high.Bottom, Eps);

            HeightExtent low = HeightExtentMath.Signed(0f, 2f);
            Assert.AreEqual(0f, low.Top, Eps);
            Assert.AreEqual(-2f, low.Bottom, Eps);
            Assert.IsTrue(low.IsBelowBase);
        }


        [Test]
        public void TwoSided_PositiveUpNegativeDown()
        {
            HeightExtent e = HeightExtentMath.TwoSided(1f, 0.5f, 4f);
            Assert.AreEqual(4f, e.Top, Eps);
            Assert.AreEqual(-2f, e.Bottom, Eps);
            Assert.IsTrue(e.HasData);
        }


        [Test]
        public void Unidirectional_NeverBelowBase()
        {
            HeightExtent e = HeightExtentMath.Unidirectional(0.3f, 1f);
            Assert.AreEqual(0.3f, e.Top, Eps);
            Assert.AreEqual(0f, e.Bottom, Eps);
            Assert.IsFalse(HeightExtent.None.HasData);
        }


        // ---------------------------------------------------------
        // Scales: zero baseline for bar-like marks
        // ---------------------------------------------------------

        [Test]
        public void IncludeZero_ExtendsLinearDomain()
        {
            var spec = JsonUtility.FromJson<NumericScaleSpec>("{\"domainMode\":0}");
            var scale = VisualizationScaleUtility.Resolve(new double[] { 100, 200 }, spec, true);
            Assert.AreEqual(0.0, scale.Minimum, 1e-9);
            Assert.AreEqual(0.5f, scale.Normalize(100), Eps);
            StringAssert.Contains("from zero", scale.Description);

            var withoutZero = VisualizationScaleUtility.Resolve(new double[] { 100, 200 }, spec, false);
            Assert.AreEqual(100.0, withoutZero.Minimum, 1e-9);
        }


        // ---------------------------------------------------------
        // Multi-variable binding
        // ---------------------------------------------------------

        [Test]
        public void TryGetMultiple_NeedsMultipleModeAndTwoVariables()
        {
            const string two = "{\"mode\":1,\"variables\":[{\"dataLayerId\":\"a\",\"variableId\":\"x\"},{\"dataLayerId\":\"a\",\"variableId\":\"y\"}]}";
            const string single = "{\"mode\":0,\"variables\":[{\"dataLayerId\":\"a\",\"variableId\":\"x\"},{\"dataLayerId\":\"a\",\"variableId\":\"y\"}]}";
            const string one = "{\"mode\":1,\"variables\":[{\"dataLayerId\":\"a\",\"variableId\":\"x\"}]}";
            const string blank = "{\"mode\":1,\"variables\":[{\"dataLayerId\":\"a\",\"variableId\":\"x\"},{\"dataLayerId\":\"\",\"variableId\":\"y\"}]}";

            Assert.IsTrue(JsonUtility.FromJson<DataBinding>(two).TryGetMultiple(out var vars));
            CollectionAssert.AreEqual(new[] { "x", "y" }, vars.Select(v => v.VariableId));
            Assert.IsFalse(JsonUtility.FromJson<DataBinding>(single).TryGetMultiple(out _));
            Assert.IsFalse(JsonUtility.FromJson<DataBinding>(one).TryGetMultiple(out _));
            Assert.IsFalse(JsonUtility.FromJson<DataBinding>(blank).TryGetMultiple(out _));
        }


        // ---------------------------------------------------------
        // Glyph geometry
        // ---------------------------------------------------------

        private static void AssertFrontFacesMatchNormals(Mesh mesh)
        {
            Vector3[] v = mesh.vertices;
            Vector3[] n = mesh.normals;
            int[] t = mesh.triangles;
            for (int i = 0; i < t.Length; i += 3)
            {
                Vector3 cross = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
                if (cross.sqrMagnitude < 1e-12f) continue; // degenerate (zero-height side)
                Assert.Greater(Vector3.Dot(cross, n[t[i]]), 0f, $"triangle {i / 3} faces inward");
            }
        }


        [Test]
        public void Box_IsClosedOutwardAndSized()
        {
            var acc = new GlyphMeshAccumulator();
            acc.BeginUnit("u1");
            acc.AppendBox(new Vector3(10, 1, 20), 0.5f, 0f, 3f, new Color32(255, 0, 0, 255));
            Assert.IsTrue(acc.EndUnit());

            Mesh mesh = acc.BuildMesh("test");
            Assert.AreEqual(12, mesh.triangles.Length / 3);
            AssertFrontFacesMatchNormals(mesh);

            Bounds b = mesh.bounds;
            Assert.AreEqual(1f, b.size.x, Eps);
            Assert.AreEqual(3f, b.size.y, Eps);
            Assert.AreEqual(1f, b.min.y, Eps);
            Assert.AreEqual(1, acc.Ranges.Count);
            Assert.IsTrue(acc.TriangleUnitIds.All(id => id == "u1"));
            UnityEngine.Object.DestroyImmediate(mesh);
        }


        [Test]
        public void Wedge_IsOutwardWithinRadiusAndSector()
        {
            var acc = new GlyphMeshAccumulator();
            acc.BeginUnit("u1");
            // East quadrant: from 0.25*2π (east) to 0.5*2π (south).
            acc.AppendWedge(Vector3.zero, 2f, Mathf.PI * 0.5f, Mathf.PI, 0f, 0.1f, 8, Color.white);
            acc.EndUnit();

            Mesh mesh = acc.BuildMesh("wedge");
            AssertFrontFacesMatchNormals(mesh);

            foreach (Vector3 p in mesh.vertices)
            {
                Assert.LessOrEqual(new Vector2(p.x, p.z).magnitude, 2f + Eps);
                Assert.GreaterOrEqual(p.x, -Eps);   // east half
                Assert.LessOrEqual(p.z, Eps);       // south half
            }
            UnityEngine.Object.DestroyImmediate(mesh);
        }


        [Test]
        public void EmptyUnit_GetsNoRange()
        {
            var acc = new GlyphMeshAccumulator();
            acc.BeginUnit("empty");
            acc.AppendWedge(Vector3.zero, 0f, 0f, 1f, 0f, 1f, 4, Color.white); // zero radius: nothing
            Assert.IsFalse(acc.EndUnit());
            Assert.IsTrue(acc.IsEmpty);
        }
    }
}
